using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Abysses.Core;
using Abysses.Interaction;
using Abysses.Level;
using Abysses.Network;
using Abysses.Player;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Parties de test automatisées (Play Mode), lançables en batchmode :
    /// - solo : surface → étage 1 (panne puis courant rétabli, indice, objet perdu) → 2 → 3 → fin, nouvelle plongée,
    ///   relance par le maître du jeu, sortie (étages bien déchargés) ;
    /// - multi : l'éditeur héberge, un client Mac compilé rejoint comme 2e plongeur, un second comme pupitre spectateur ;
    ///   reconnexion d'un plongeur en pleine partie, indices demandés par le pupitre, partie jusqu'à la fin.
    /// Rapport dans docs/playtest-*.md ; erreurs de console comptées comme des échecs.
    /// Les énigmes coopératives sont résolues par les commandes du maître du jeu ; les énigmes de module (disjoncteurs, morse,
    /// sonar) le sont par de vrais appuis, erreurs comprises. Le test vérifie l'enchaînement, pas les gestes au casque.
    /// </summary>
    public static class AbyssesPlaytest
    {
        const string ModeKey = "abysses.playtest.mode";
        const string ClientApp = "Builds/TestClient/AbyssesTest.app";

        static string mode;
        static IEnumerator routine;
        /// <summary>Étapes imbriquées (un scénario qui délègue à un sous-scénario).</summary>
        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        static float waitUntilTime;
        static Func<bool> condition;
        static float conditionDeadline;
        static string conditionLabel;
        static bool running;
        static readonly List<string> results = new List<string>();
        static readonly List<string> errors = new List<string>();
        static int failures;
        static int messages;
        static string lastMessage;
        static readonly List<Process> processes = new List<Process>();
        static readonly List<string> clientLogs = new List<string>();
        /// <summary>Classement réel de cette machine : les temps des parties de test ne doivent pas y rester.</summary>
        const string LeaderboardKey = "abysses.leaderboard";
        static string savedLeaderboard;

        // ------------------------------------------------------------------ Lancement

        [MenuItem("Abysses/Tests/Partie solo automatique", priority = 80)]
        public static void RunSolo() => Launch("solo");

        [MenuItem("Abysses/Tests/Partie à 2 + pupitre (client Mac compilé)", priority = 81)]
        public static void RunMulti() => Launch("multi");

        [MenuItem("Abysses/Tests/Compiler le client de test Mac", priority = 82)]
        public static void BuildClient()
        {
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                target = BuildTarget.StandaloneOSX,
                targetGroup = BuildTargetGroup.Standalone,
                locationPathName = ClientApp,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Abysses] Client de test : {report.summary.result}, {report.summary.totalErrors} erreur(s) → {ClientApp}");
            // Le projet reste un projet Quest.
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }

        static void Launch(string testMode)
        {
            SessionState.SetString(ModeKey, testMode);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Station.unity", OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            if (string.IsNullOrEmpty(SessionState.GetString(ModeKey, ""))) return;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (EditorApplication.isPlaying) Begin();
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode) Begin();
            else if (change == PlayModeStateChange.EnteredEditMode && !string.IsNullOrEmpty(SessionState.GetString(ModeKey, "")))
            {
                SessionState.EraseString(ModeKey);
                if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
            }
        }

        static void Begin()
        {
            if (running) return;
            running = true;
            mode = SessionState.GetString(ModeKey, "solo");
            savedLeaderboard = PlayerPrefs.GetString(LeaderboardKey, "");
            Application.logMessageReceived += OnLog;
            HintSystem.MessageReceived += OnHint;
            routine = mode == "multi" ? Multi() : Solo();
            EditorApplication.update += Tick;
        }

        static void OnLog(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            errors.Add(type + " : " + message.Split('\n')[0]);
        }

        static void OnHint(string text)
        {
            messages++;
            lastMessage = text;
        }

        // ------------------------------------------------------------------ Moteur

        sealed class WaitTime { public float Seconds; }
        sealed class WaitFor { public Func<bool> Condition; public float Timeout; public string Label; }

        static WaitTime Wait(float seconds) => new WaitTime { Seconds = seconds };
        static WaitFor Until(Func<bool> condition, float timeout, string label) => new WaitFor { Condition = condition, Timeout = timeout, Label = label };

        static void Tick()
        {
            if (!EditorApplication.isPlaying || routine == null) return;
            float now = Time.realtimeSinceStartup;
            if (now < waitUntilTime) return;
            if (condition != null)
            {
                bool ok;
                try { ok = condition(); }
                catch (Exception e) { ok = false; errors.Add("Exception (condition) : " + e.Message); }
                if (!ok)
                {
                    if (now < conditionDeadline) return;
                    Fail("Délai dépassé : " + conditionLabel + (State != null ? $" (phase {State.Phase.Value}, étage {State.Floor.Value}, cabines {State.ReadyMask.Value})" : ""));
                    Finish();
                    return;
                }
                // Attente intermédiaire sans libellé : pas de ligne au rapport (seul un dépassement est signalé).
                if (conditionLabel != null) Pass(conditionLabel);
                condition = null;
            }

            bool more;
            try { more = routine.MoveNext(); }
            catch (Exception e)
            {
                Fail("Exception du test : " + e.Message);
                Finish();
                return;
            }
            if (!more)
            {
                // Sous-scénario terminé : on reprend le scénario parent à la frame suivante.
                if (stack.Count > 0) routine = stack.Pop();
                else Finish();
                return;
            }
            switch (routine.Current)
            {
                case IEnumerator child:
                    stack.Push(routine);
                    routine = child;
                    break;
                case WaitTime w:
                    waitUntilTime = now + w.Seconds;
                    break;
                case WaitFor f:
                    condition = f.Condition;
                    conditionDeadline = now + f.Timeout;
                    conditionLabel = f.Label;
                    break;
            }
        }

        static void Pass(string label) => results.Add("✅ " + label);

        static void Fail(string label)
        {
            failures++;
            results.Add("❌ " + label);
        }

        static void Check(bool ok, string label)
        {
            if (ok) Pass(label);
            else Fail(label);
        }

        static void Finish()
        {
            if (!running) return;
            running = false;
            routine = null;
            stack.Clear();
            EditorApplication.update -= Tick;
            foreach (Process p in processes)
            {
                try { if (!p.HasExited) p.Kill(); } catch { /* déjà terminé */ }
            }

            var report = new StringBuilder();
            report.AppendLine("# Partie de test automatique — " + (mode == "multi" ? "2 plongeurs + pupitre" : "solo"));
            report.AppendLine();
            report.AppendLine($"> {DateTime.Now:yyyy-MM-dd HH:mm} · Unity {Application.unityVersion} · *Abysses → Tests*. ");
            report.AppendLine("> Énigmes coopératives résolues par le maître du jeu ; énigmes de module (disjoncteurs, morse, sonar) résolues par de vrais appuis. On vérifie l'enchaînement et les systèmes, pas les gestes au casque.");
            report.AppendLine();
            foreach (string line in results) report.AppendLine("- " + line);
            report.AppendLine();
            var unique = errors.Distinct().ToList();
            report.AppendLine(unique.Count == 0 ? "**Console : aucune erreur.**" : $"**Console : {unique.Count} erreur(s) distincte(s)**");
            foreach (string e in unique.Take(30)) report.AppendLine("- `" + e.Replace("`", "'") + "`");
            foreach (string log in clientLogs)
            {
                report.AppendLine();
                report.AppendLine("**Journal " + Path.GetFileName(log) + "** : " + ClientErrors(log));
            }
            failures += unique.Count;
            report.AppendLine();
            report.AppendLine(failures == 0 ? "**Résultat : OK**" : $"**Résultat : {failures} problème(s)**");
            File.WriteAllText($"../docs/playtest-{mode}.md", report.ToString());
            Debug.Log($"[Abysses] Test {mode} terminé : {failures} problème(s) → docs/playtest-{mode}.md");

            Application.logMessageReceived -= OnLog;
            HintSystem.MessageReceived -= OnHint;
            PlayerPrefs.SetString(LeaderboardKey, savedLeaderboard ?? "");
            PlayerPrefs.Save();
            if (ConnectionManager.Instance != null) ConnectionManager.Instance.Leave();
            EditorApplication.isPlaying = false;
        }

        static string ClientErrors(string path)
        {
            if (!File.Exists(path)) return "introuvable";
            var lines = File.ReadAllLines(path).Where(l => l.Contains("Exception") || l.StartsWith("Error") || l.Contains("[Abysses]")).Distinct().Take(15).ToList();
            return lines.Count == 0 ? "rien à signaler" : "\n" + string.Join("\n", lines.Select(l => "  - `" + l.Trim().Replace("`", "'") + "`"));
        }

        // ------------------------------------------------------------------ Outils de vérification

        static GameStateManager State => GameStateManager.Instance;
        static bool Phase(GamePhase phase, int floor) => State != null && State.Phase.Value == phase && State.Floor.Value == floor;
        static bool OnlyStationLoaded() => SceneManager.sceneCount == 1;

        static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(target);

        static string WatchText()
        {
            // Sans casque, les manettes ne sont pas suivies et XRI désactive leurs objets : on rafraîchit la montre à la main.
            DiveWatch watch = Object.FindFirstObjectByType<DiveWatch>(FindObjectsInactive.Include);
            if (watch == null) return "";
            typeof(DiveWatch).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(watch, null);
            return Field<TMP_Text>(watch, "face").text;
        }

        static IEnumerator SkipFloorAndDescend(int floor)
        {
            State.GmSkipFloorRpc();
            // L'étage résolu peut ne durer qu'un instant : si tout le monde est déjà en cabine, la descente part aussitôt.
            yield return Until(() => State.Phase.Value != GamePhase.Playing || State.Floor.Value != floor, 6f,
                $"Étage {floor} : « passer l'étage » (pupitre) → étage résolu");
            Check(State.Phase.Value != GamePhase.Playing, $"Étage {floor} quitté (phase : {State.Phase.Value}, étage {State.Floor.Value})");
        }

        static T Mine<T>() where T : MonoBehaviour =>
            Object.FindObjectsByType<T>(FindObjectsSortMode.None).FirstOrDefault(c => Field<PuzzleObjective>(c, "objective").Quadrant == 0);

        static int Bits(int value)
        {
            int count = 0;
            for (; value != 0; value &= value - 1) count++;
            return count;
        }

        /// <summary>Étage 1 : tableau des disjoncteurs résolu en suivant la note (et court-circuit si on arme un interdit).</summary>
        static IEnumerator Breakers()
        {
            Check(Object.FindObjectsByType<BreakerPanel>(FindObjectsSortMode.None).Length == 4, "Étage 1 : 4 tableaux de disjoncteurs");
            BreakerPanel panel = Mine<BreakerPanel>();
            if (panel == null) yield break;
            int forbidden = panel.Forbidden.Value;
            Check(Bits(forbidden) == 2, "Disjoncteurs : 2 interdits tirés au hasard");
            string note = Field<TMP_Text[]>(panel, "notes")[0].text;
            bool named = true;
            for (int i = 0; i < BreakerPanel.Count; i++)
                if ((forbidden & (1 << i)) != 0) named &= note.Contains(BreakerPanel.NameOf(i));
            Check(named, "Note du chef mécanicien : nomme les 2 disjoncteurs à laisser coupés");
            int allowed = 0, bad = 0;
            for (int i = 0; i < BreakerPanel.Count; i++)
            {
                if ((forbidden & (1 << i)) == 0) { allowed = i; break; }
            }
            for (int i = 0; i < BreakerPanel.Count; i++)
                if ((forbidden & (1 << i)) != 0) { bad = i; break; }
            panel.Pressed(allowed);
            yield return Until(() => panel.Armed.Value == 1 << allowed, 3f, "Disjoncteur armé (appui sur la manette)");
            panel.Pressed(bad);
            yield return Until(() => panel.Armed.Value == 0, 3f, "Armer un disjoncteur interdit : court-circuit, tout disjoncte");
            var objective = Field<PuzzleObjective>(panel, "objective");
            for (int i = 0; i < BreakerPanel.Count; i++)
            {
                if ((forbidden & (1 << i)) != 0) continue;
                panel.Pressed(i);
                yield return Wait(0.2f);
            }
            yield return Until(() => objective.Solved.Value, 3f, "Disjoncteurs : réarmés en suivant la note → énigme résolue");
        }

        /// <summary>Étage 2 : la balise clignote, le cadenas refuse un mauvais code et s'ouvre avec le bon.</summary>
        static IEnumerator MorseLock()
        {
            Check(Object.FindObjectsByType<CombinationLock>(FindObjectsSortMode.None).Length == 4, "Étage 2 : 4 cadenas morse");
            CombinationLock combination = Mine<CombinationLock>();
            if (combination == null) yield break;
            MorseBeacon beacon = Field<MorseBeacon>(combination, "beacon");
            int code = beacon.Code.Value;
            Check(code >= 0 && code < 1000, "Balise morse : code à 3 chiffres tiré (" + code.ToString("000") + ")");
            var objective = Field<PuzzleObjective>(combination, "objective");
            // Mauvais code (molettes à 000, ou 001 si le code est 000).
            if (code == 0) combination.Pressed(2);
            combination.Pressed(CombinationLock.ValidateIndex);
            yield return Wait(1f);
            Check(!objective.Solved.Value, "Cadenas : un mauvais code est refusé");
            if (code == 0) combination.Pressed(MorseBeacon.Digits + 2);
            for (int w = 0; w < MorseBeacon.Digits; w++)
                for (int k = 0; k < MorseBeacon.DigitAt(code, w); k++) combination.Pressed(w);
            combination.Pressed(CombinationLock.ValidateIndex);
            yield return Until(() => objective.Solved.Value, 3f, "Cadenas : le code de la balise ouvre la vanne d'urgence");
        }

        /// <summary>Étage 3 : séquence de mémoire jouée manche par manche (avec une erreur au début).</summary>
        static IEnumerator Sonar()
        {
            Check(Object.FindObjectsByType<SimonConsole>(FindObjectsSortMode.None).Length == 4, "Étage 3 : 4 consoles sonar");
            SimonConsole console = Mine<SimonConsole>();
            if (console == null) yield break;
            int sequence = console.Sequence.Value;
            var objective = Field<PuzzleObjective>(console, "objective");
            console.Pressed(SimonConsole.ListenIndex);
            yield return Until(() => !Field<bool>(console, "playing"), 5f, "Console sonar : la première note est jouée");
            console.Pressed((SimonConsole.PadAt(sequence, 0) + 1) % SimonConsole.Pads);
            yield return Until(() => !Field<bool>(console, "playing") && Field<int>(console, "round") == 1, 5f, "Console sonar : une erreur relance la séquence");
            for (int round = 1; round <= SimonConsole.MaxLength; round++)
            {
                yield return Until(() => !Field<bool>(console, "playing"), 8f, null);  // fin de la démonstration de la manche
                for (int i = 0; i < round; i++) console.Pressed(SimonConsole.PadAt(sequence, i));
                yield return Wait(0.2f);
            }
            yield return Until(() => objective.Solved.Value, 4f, "Console sonar : séquence de 6 couleurs reproduite → calibrée");
        }

        /// <summary>
        /// Relais d'appels joué par de vrais appuis sur les panneaux. Seul, le joueur est appelant et cible ;
        /// à plusieurs, l'appelant n'est jamais la cible (le test répond pour chaque plongeur sur son panneau).
        /// </summary>
        static IEnumerator Relay(int floor)
        {
            CallOutRelay relay = Object.FindObjectsByType<CallOutRelay>(FindObjectsSortMode.None).FirstOrDefault(r => r.Objective.Floor == floor);
            Check(relay != null, $"Étage {floor} : relais d'appels en place");
            if (relay == null) yield break;
            var panels = new CallOutPanel[PlayArea.QuadrantCount];
            foreach (CallOutPanel p in Object.FindObjectsByType<CallOutPanel>(FindObjectsSortMode.None))
                panels[Field<int>(p, "quadrant")] = p;
            int mine = LocalRig.Instance.Quadrant;
            bool alone = NetworkPlayer.All.Count == 1;

            panels[mine].Pressed(CallOutPanel.StartIndex);
            yield return Until(() => relay.HasCall, 3f, "Relais : DÉMARRER lance le premier appel");
            Check(alone ? relay.Caller == relay.Target : relay.Caller != relay.Target,
                alone ? "Relais en solo : le joueur est appelant et cible" : "Relais à plusieurs : le symbole s'affiche chez un autre que la cible");
            panels[relay.Target].Pressed((relay.Symbol + 1) % CallOutRelay.Symbols);
            yield return Until(() => !relay.HasCall && relay.LastKind == CallOutRelay.Result.Wrong, 3f, "Relais : un mauvais symbole est refusé, la manche recommence");
            float end = Time.realtimeSinceStartup + 120f;
            bool rolesOk = true;
            while (!relay.Finished && Time.realtimeSinceStartup < end)
            {
                if (relay.HasCall)
                {
                    rolesOk &= alone || relay.Caller != relay.Target;
                    panels[relay.Target].Pressed(relay.Symbol);
                }
                yield return Wait(0.25f);
            }
            Check(relay.Finished && rolesOk, $"Relais : {relay.Rounds} manches réussies → énigme résolue");
        }

        static IEnumerator FloorOne(bool checkLostItem)
        {
            PowerRestore power = Object.FindFirstObjectByType<PowerRestore>();
            Check(power != null, "Étage 1 : panne de courant en place");
            int offset = power != null ? Field<int>(power, "lightmapOffset") : -1;
            Texture2D[] powered = power != null ? Field<Texture2D[]>(power, "poweredLightmaps") : null;
            Check(offset >= 0 && powered != null && powered.Length > 0, "Étage 1 : lightmaps « secours » et « courant » repérées");
            Check(WatchText().Contains("-200"), "Montre de plongée : profondeur -200 m");
            Check(Object.FindObjectsByType<OceanDrifter>(FindObjectsSortMode.None).Length == 2, "Hublots : 2 silhouettes géantes autour de la station");

            int before = messages;
            HintSystem.Instance.RequestHintRpc();
            yield return Until(() => messages > before, 3f, "Indice de l'IA reçu sur la montre");
            Check(lastMessage != null && lastMessage.Length > 30, "Indice lisible : « " + lastMessage + " »");

            if (checkLostItem)
            {
                FuseItem fuse = FuseItem.All.FirstOrDefault(f => f.gameObject.activeInHierarchy && f.GetComponent<NetworkObject>().IsSpawned);
                Check(fuse != null, "Fusible disponible pour le test d'objet perdu");
                if (fuse != null)
                {
                    Vector3 start = fuse.transform.position;
                    var lost = new Vector3(start.x, -3f, start.z);
                    fuse.GetComponent<Rigidbody>().position = lost;
                    fuse.GetComponent<NetworkTransform>().Teleport(lost, fuse.transform.rotation, fuse.transform.localScale);
                    yield return Until(() => fuse.transform.position.y > -0.3f, 6f, "Objet perdu (fusible passé sous le sol) revenu à sa place");
                }
            }

            if (checkLostItem) yield return Breakers();

            // Comme en vrai, le plongeur est aux leviers (hors de sa cabine) quand le courant revient.
            Transform rig = LocalRig.Instance.transform;
            // Point de départ connu (centre de la cabine) : les étapes précédentes ont pu déplacer le plongeur.
            LocalRig.Instance.Recenter();
            yield return Wait(0.3f);
            rig.position += new Vector3(3f, 0f, 0f);
            yield return Wait(1f);
            Debug.Log($"[Abysses] Test : plongeur aux leviers, origine {rig.position}, tête {LocalRig.Instance.Head.position}");
            State.GmSkipFloorRpc();
            yield return Until(() => Phase(GamePhase.FloorSolved, 1), 6f, "Étage 1 : « passer l'étage » (pupitre) → étage résolu");
            // 16 diffuseurs rallumés un par un (~7 s) avant l'échange des lightmaps.
            yield return Until(() => offset >= 0 && powered != null && LightmapSettings.lightmaps.Length > offset
                && LightmapSettings.lightmaps[offset].lightmapColor == powered[0], 12f,
                "Étage 1 : courant rétabli (néons rallumés un par un, lightmaps échangées)");
            Check(State.Phase.Value == GamePhase.FloorSolved, "Pas de descente tant que le plongeur n'est pas revenu dans sa cabine");
            rig.position -= new Vector3(3f, 0f, 0f);
            yield return Wait(1f);
            Debug.Log($"[Abysses] Test : retour en cabine, origine {rig.position}, tête {LocalRig.Instance.Head.position}, prêts {State.ReadyMask.Value}");
        }

        static IEnumerator FloorsTwoAndThree()
        {
            yield return Until(() => Phase(GamePhase.Playing, 2), 45f, "Retour en cabine → descente automatique vers l'étage 2");
            Check(WatchText().Contains("-1500"), "Montre : -1 500 m");
            Check(Object.FindObjectsByType<SteamVent>(FindObjectsSortMode.None).Length == 4, "Étage 2 : 4 fuites de vapeur");
            Check(Object.FindObjectsByType<RisingWater>(FindObjectsSortMode.None).Length == 4, "Étage 2 : eau qui monte dans les 4 modules");
            Check(Object.FindObjectsByType<NeonFlicker>(FindObjectsSortMode.None).Length == 4, "Étage 2 : néons défaillants");
            yield return MorseLock();
            yield return SkipFloorAndDescend(2);
            yield return Until(() => Phase(GamePhase.Playing, 3), 45f, "Descente vers l'étage 3 (salle commune)");
            Check(WatchText().Contains("-4000"), "Montre : -4 000 m");
            yield return Sonar();
            yield return Relay(3);
            State.GmSkipFloorRpc();
            yield return Until(() => State.Phase.Value == GamePhase.Finished, 6f, "Séquence du réacteur + remontée → fin de partie");
            Check(State.EndTime.Value > State.StartTime.Value, "Chrono de fin cohérent");
            Check(Leaderboard.Parse(State.Leaderboard.Value.ToString()).Count > 0, "Temps enregistré au classement : " + Leaderboard.Format(State.Leaderboard.Value.ToString()).Split('\n').ElementAtOrDefault(1));
        }

        // ------------------------------------------------------------------ Scénario solo

        static IEnumerator Solo()
        {
            yield return Wait(2f);
            // Une manette de jeu branchée sur le Mac (stick qui dérive) déplacerait le plongeur pendant le test.
            foreach (GamepadLocomotion pad in Object.FindObjectsByType<GamepadLocomotion>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                pad.enabled = false;
            ConnectionManager connection = ConnectionManager.Instance;
            connection.PlaySolo();
            yield return Until(() => connection.State == ConnectionState.Online && LocalRig.Instance.Quadrant == 0, 15f, "Partie solo lancée, quadrant ORANGE attribué");
            yield return Until(() => (State.ReadyMask.Value & 1) != 0, 10f, "Joueur détecté dans sa cabine (calibration)");

            Loc.Toggle();
            Check(Loc.English && connection.Status == "Solo game", "Langue anglaise : statut « Solo game »");
            Loc.Toggle();
            State.SetEasyModeRpc(true);
            yield return Until(() => State.EasyMode.Value, 3f, "Mode facile activé à la surface");
            State.SetEasyModeRpc(false);
            yield return Until(() => !State.EasyMode.Value, 3f, "Mode facile désactivé");

            SurfaceWorld surface = Object.FindFirstObjectByType<SurfaceWorld>();
            Transform world = Field<Transform>(surface, "world");
            Renderer[] shrouds = Field<Renderer[]>(surface, "shrouds");
            Check(world.gameObject.activeSelf && Field<Light>(surface, "sun").enabled && !shrouds[0].enabled, "Surface : plateforme, mer et soleil visibles à l'embarquement");
            State.RequestStartRpc();
            yield return Until(() => world.position.y > 2.7f, 8f, "Plongée : la cabine s'enfonce, la mer passe les hublots");
            yield return Until(() => shrouds[0].enabled && !world.gameObject.activeSelf, 12f, "Plongée : sous l'eau, le puits d'eau remplace la surface");
            Check(OnlyStationLoaded(), "L'étage 1 n'est chargé qu'une fois la surface masquée");
            yield return Until(() => Phase(GamePhase.Playing, 1), 40f, "Plongée → arrivée à l'étage 1 (-200 m)");
            Check(!shrouds[0].enabled && !world.gameObject.activeSelf, "Arrivée : puits d'eau retiré, portes sur l'étage");
            yield return FloorOne(true);
            yield return FloorsTwoAndThree();

            State.RequestRestartRpc();
            yield return Until(() => State.Phase.Value == GamePhase.Lobby && OnlyStationLoaded(), 10f, "« Nouvelle plongée » : retour surface, étages déchargés");
            Check(SurfaceWorld.HoldDoors, "Remontée : portes fermées pendant l'émersion");
            yield return Until(() => !SurfaceWorld.HoldDoors && world.gameObject.activeSelf && world.position.y < 0.01f, 12f, "Remontée : on émerge à la surface, portes rouvertes");
            yield return Until(() => (State.ReadyMask.Value & 1) != 0, 10f, "Joueur de nouveau prêt en cabine");
            State.RequestStartRpc();
            yield return Until(() => Phase(GamePhase.Playing, 1), 40f, "Seconde plongée → étage 1");
            State.GmRestartRpc();
            yield return Until(() => State.Phase.Value == GamePhase.Lobby && OnlyStationLoaded(), 10f, "Pupitre : « relancer la partie » en plein étage");

            yield return Until(() => (State.ReadyMask.Value & 1) != 0, 10f, "Joueur de nouveau prêt après la relance");
            State.RequestStartRpc();
            yield return Until(() => Phase(GamePhase.Playing, 1), 40f, "Troisième plongée → étage 1");
            connection.Leave();
            yield return Until(() => connection.State == ConnectionState.Offline && OnlyStationLoaded(), 5f, "Quitter en pleine partie : étage déchargé (plus de brouillard résiduel)");
        }

        // ------------------------------------------------------------------ Scénario à 2 + pupitre

        static Process StartClient(string name, string extraArgs)
        {
            string macos = Path.GetFullPath(ClientApp + "/Contents/MacOS");
            string exe = Directory.Exists(macos) ? Directory.GetFiles(macos).FirstOrDefault() : null;
            if (exe == null) return null;
            string log = Path.GetFullPath("Builds/TestClient/" + name + ".log");
            clientLogs.Add(log);
            var info = new ProcessStartInfo(exe, $"-batchmode -nographics -logFile \"{log}\" -abysses-join 127.0.0.1 {extraArgs}") { UseShellExecute = false };
            Process process = Process.Start(info);
            processes.Add(process);
            return process;
        }

        static NetworkPlayer PlayerIn(int quadrant) => NetworkPlayer.All.FirstOrDefault(p => p.Quadrant.Value == quadrant);

        static IEnumerator Multi()
        {
            yield return Wait(2f);
            ConnectionManager connection = ConnectionManager.Instance;
            connection.HostLan();
            yield return Until(() => connection.State == ConnectionState.Online, 15f, "Hôte réseau local lancé (plongeur ORANGE)");

            Check(StartClient("client_plongeur", "-abysses-player") != null, "Client Mac lancé (2e plongeur)");
            yield return Until(() => NetworkPlayer.All.Count == 2 && PlayerIn(1) != null, 90f, "2e plongeur connecté, quadrant CYAN");
            yield return Until(() => (State.ReadyMask.Value & 3) == 3, 20f, "Les 2 plongeurs détectés dans leur cabine");

            Check(StartClient("client_pupitre", "-abysses-spectator -abysses-gm-hint") != null, "Pupitre spectateur lancé");
            yield return Until(() => NetworkManager.Singleton.ConnectedClientsIds.Count == 3, 90f, "Pupitre connecté");
            ulong spectator = NetworkManager.Singleton.ConnectedClientsIds.First(id => NetworkPlayer.All.All(p => p.OwnerClientId != id));
            Check(NetworkPlayer.All.Count == 2, "Le pupitre n'a ni avatar ni quadrant");
            Check(ConnectionManager.IsGameMaster(spectator), "Le pupitre a les droits du maître du jeu");

            State.RequestStartRpc();
            yield return Until(() => Phase(GamePhase.Playing, 1), 40f, "Plongée à 2 → étage 1");
            Check(State.ActiveMask.Value == 3, "Modules ORANGE et CYAN actifs");

            int before = messages;
            yield return Until(() => messages > before, 25f, "Indice demandé par le pupitre, reçu par le plongeur ORANGE");

            // Reconnexion : l'hôte coupe le 2e plongeur, son casque doit revenir seul dans son module.
            ulong diver = PlayerIn(1).OwnerClientId;
            NetworkManager.Singleton.DisconnectClient(diver);
            yield return Until(() => PlayerIn(1) == null, 5f, "2e plongeur déconnecté (coupure Wi-Fi simulée)");
            yield return Until(() => PlayerIn(1) != null, 45f, "Reconnexion automatique : retour dans le module CYAN");
            yield return Wait(22f);
            PuzzleObjective cyanKeypad = PuzzleObjective.Registered.FirstOrDefault(o => o.Floor == 1 && o.Quadrant == 1);
            Check(cyanKeypad != null && !cyanKeypad.Solved.Value, "Après le délai de grâce, les énigmes du module CYAN restent à faire");

            yield return SkipFloorAndDescend(1);
            yield return FloorsTwoAndThree();
        }
    }
}
