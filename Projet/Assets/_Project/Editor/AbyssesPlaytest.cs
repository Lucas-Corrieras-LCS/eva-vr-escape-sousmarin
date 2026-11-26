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
            // Tests en arrière-plan : son de l'éditeur coupé (rétabli à la fin du test).
            EditorUtility.audioMasterMute = true;
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
            EditorUtility.audioMasterMute = false;
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

        /// <summary>
        /// Netcode applique les variables chez les clients dans l'ordre de leur liste : tout ce que lisent les abonnés de
        /// PhaseChanged doit la précéder (sinon, à l'arrivée sur un étage, un client voit encore l'ancien étage / masque).
        /// </summary>
        static bool PhaseSyncedLast()
        {
            var fields = typeof(Unity.Netcode.NetworkBehaviour).GetField("NetworkVariableFields", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.GetValue(State) as System.Collections.IList;
            if (fields == null) return false;
            int phase = fields.IndexOf(State.Phase);
            return phase > fields.IndexOf(State.Floor) && phase > fields.IndexOf(State.ActiveMask)
                && phase > fields.IndexOf(State.ReadyMask) && phase > fields.IndexOf(State.EasyMode) && phase > fields.IndexOf(State.StartTime);
        }

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

        /// <summary>Étage 1 : manuel de désamorçage — un mauvais fil (court-circuit), puis 3 tableaux réussis d'affilée.</summary>
        static IEnumerator Wiring()
        {
            Check(Object.FindObjectsByType<WiringPanel>(FindObjectsSortMode.None).Length == 4, "Étage 1 : 4 tableaux de dérivation");
            WiringPanel panel = Mine<WiringPanel>();
            if (panel == null) yield break;
            WiringPanel.Board board = panel.Current;
            Check(board.Count >= 4 && board.Count <= 6 && !string.IsNullOrEmpty(board.Serial), $"Tableau : {board.Count} fils, voyant, n° de série {board.Serial}");
            WiringManual manual = Object.FindObjectsByType<WiringManual>(FindObjectsSortMode.None).FirstOrDefault(w => Field<int>(w, "quadrant") == LocalRig.Instance.Quadrant);
            Check(manual != null && Field<TMP_Text>(manual, "text").text.Contains("1."), "Manuel : règles numérotées affichées");
            int wrong = (panel.Solution + 1) % board.Count;
            panel.Pressed(wrong);
            yield return Until(() => panel.Shorts.Value == 1 && panel.Round.Value == 0, 3f, "Tableau : un mauvais fil provoque un court-circuit");
            yield return Until(() => panel.Cut.Value == 0, 4f, "Tableau : nouveau tirage après le court-circuit");
            for (int round = 0; round < WiringPanel.Rounds; round++)
            {
                int seed = panel.Seed.Value;
                panel.Pressed(panel.Solution);
                yield return Until(() => panel.Objective.Solved.Value || panel.Seed.Value != seed && panel.Cut.Value == 0, 4f, null);
            }
            Check(panel.Objective.Solved.Value, "Tableau : 3 bons fils d'affilée → dérivation rétablie");
        }

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

        /// <summary>Étage 3 : collecteur de vannes réglé par de vrais appuis, après un coup de bélier volontaire.</summary>
        static IEnumerator Manifold()
        {
            Check(Object.FindObjectsByType<ValveManifold>(FindObjectsSortMode.None).Length == 4, "Étage 3 : 4 collecteurs de vannes");
            ValveManifold manifold = Mine<ValveManifold>();
            if (manifold == null) yield break;
            int target = manifold.Target.Value;
            Check(target > 0 && target < 1 << ValveManifold.Count, "Collecteur : combinaison tirée au hasard");
            var objective = Field<PuzzleObjective>(manifold, "objective");
            string diagram = Field<TMP_Text[]>(manifold, "diagramTexts")[0].text;
            Check(diagram.Contains(ValveManifold.ColorName(0)), "Schéma hydraulique : une ligne par vanne de couleur");
            manifold.Pressed(ValveManifold.PrimeIndex);
            yield return Until(() => manifold.Surges.Value == 1, 3f, "Collecteur : amorcer avec les vannes fermées → coup de bélier");
            for (int v = 0; v < ValveManifold.Count; v++)
                if ((target & (1 << v)) != 0)
                {
                    manifold.Pressed(v);
                    yield return Wait(0.15f);
                }
            yield return Until(() => manifold.Open.Value == target, 3f, "Collecteur : vannes ouvertes une à une (appuis sur les poignées)");
            manifold.Pressed(ValveManifold.PrimeIndex);
            yield return Until(() => objective.Solved.Value, 3f, "Collecteur : bonnes vannes ouvertes → la pompe démarre");
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

        /// <summary>Place la tête du plongeur au-dessus d'un point du sol (on déplace l'origine du rig).</summary>
        static void MoveHeadTo(Vector3 ground)
        {
            Transform rig = LocalRig.Instance.transform;
            Vector3 head = LocalRig.Instance.Head.position;
            rig.position += new Vector3(ground.x - head.x, 0f, ground.z - head.z);
        }

        /// <summary>Étage 3 : labyrinthe électrifié parcouru « à pied » (tête déplacée case par case), décharge comprise.</summary>
        static IEnumerator Maze()
        {
            Check(Object.FindObjectsByType<ShockMaze>(FindObjectsSortMode.None).Length == 4, "Étage 3 : 4 cales électrifiées");
            ShockMaze maze = Mine<ShockMaze>();
            if (maze == null) yield break;
            MazeMonitor monitor = Object.FindObjectsByType<MazeMonitor>(FindObjectsSortMode.None).FirstOrDefault(w => Field<int>(w, "quadrant") == LocalRig.Instance.Quadrant);
            Check(monitor != null && Field<Renderer[]>(monitor, "cells")[maze.StartCell].enabled, "Écran de surveillance : la carte de la cale est affichée");
            MoveHeadTo(maze.CellCenter(maze.StartCell));
            yield return Until(() => maze.Armed.Value, 3f, "Cale : la dalle DÉPART arme le parcours");
            int danger = System.Array.FindIndex(maze.Safe, safe => !safe);
            MoveHeadTo(maze.CellCenter(danger));
            yield return Until(() => maze.Shocks.Value == 1 && !maze.Armed.Value, 3f, "Cale : une dalle sous tension → décharge, retour au départ");
            // Chemin sûr (parcours en largeur sur les dalles sûres), suivi case par case.
            var previous = new int[maze.Safe.Length];
            for (int i = 0; i < previous.Length; i++) previous[i] = -2;
            var queue = new Queue<int>();
            queue.Enqueue(maze.StartCell);
            previous[maze.StartCell] = -1;
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                int column = cell % ShockMaze.Columns, row = cell / ShockMaze.Columns;
                foreach (int next in new[] { column > 0 ? cell - 1 : -1, column < ShockMaze.Columns - 1 ? cell + 1 : -1, row > 0 ? cell - ShockMaze.Columns : -1, row < ShockMaze.Rows - 1 ? cell + ShockMaze.Columns : -1 })
                {
                    if (next < 0 || !maze.Safe[next] || previous[next] != -2) continue;
                    previous[next] = cell;
                    queue.Enqueue(next);
                }
            }
            var path = new List<int>();
            for (int cell = maze.GoalCell; cell >= 0; cell = previous[cell]) path.Insert(0, cell);
            var visited = new List<int>();
            foreach (int cell in path)
            {
                MoveHeadTo(maze.CellCenter(cell));
                yield return Wait(0.3f);
                visited.Add(maze.CellAt(LocalRig.Instance.Head.position));
            }
            if (!maze.Objective.Solved.Value)
                Debug.Log($"[Abysses] Test cale : armé={maze.Armed.Value}, décharges={maze.Shocks.Value}, chemin={string.Join(",", path)}, vu={string.Join(",", visited)}");
            yield return Until(() => maze.Objective.Solved.Value, 3f, $"Cale : chemin sûr suivi ({path.Count} dalles) → vanne de purge atteinte");
            LocalRig.Instance.Recenter();
        }

        /// <summary>Étage 3 : cloisons étanches — fermer les portes autour de la fuite avant qu'elle n'atteigne la pompe.</summary>
        static IEnumerator Bulkheads()
        {
            BulkheadNetwork network = Object.FindFirstObjectByType<BulkheadNetwork>();
            Check(network != null && network.Layout.Value >= 0 && network.Leak != network.Pump, "Étage 3 : fuite et salle des pompes tirées");
            if (network == null) yield break;
            // Le plan est chez un seul plongeur (routage) : on joue depuis son pupitre.
            int holder = network.MapHolder(State.ActiveMask.Value);
            BulkheadDisplay display = Object.FindObjectsByType<BulkheadDisplay>(FindObjectsSortMode.None).FirstOrDefault(d => Field<int>(d, "quadrant") == holder);
            Check(display != null && Field<GameObject>(display, "map").activeSelf
                && Object.FindObjectsByType<BulkheadDisplay>(FindObjectsSortMode.None).Count(d => Field<GameObject>(d, "map").activeSelf) == 1,
                $"Cloisons : le plan n'est affiché que chez un plongeur (module {holder})");
            if (display == null) yield break;
            display.Pressed(0);
            yield return Wait(0.3f);
            Check(!network.IsClosed(0), "Cloisons : aucune porte ne se ferme avant l'alerte");
            display.Pressed(BulkheadDisplay.StartIndex);
            yield return Until(() => network.Running.Value, 3f, "Cloisons : alerte déclenchée, le chrono de l'eau tourne");
            for (int d = 0; d < BulkheadNetwork.Doors.Length; d++)
            {
                Vector2Int door = BulkheadNetwork.Doors[d];
                if (network.IsFlooded(door.x) != network.IsFlooded(door.y)) display.Pressed(d);
            }
            yield return Until(() => network.Objective.Solved.Value, 4f, "Cloisons : portes autour de l'eau fermées → fuite isolée, pompe au sec");
        }

        /// <summary>Étage 4 : triangulation du Léviathan — un tir raté, puis la case déduite des distances.</summary>
        static IEnumerator Triangulation()
        {
            SonarHunt hunt = Object.FindFirstObjectByType<SonarHunt>();
            Check(hunt != null, "Étage 4 : consoles sonar et lance-leurre en place");
            if (hunt == null) yield break;
            yield return Until(() => hunt.Target.Value >= 0, 3f, "Léviathan : position tirée au début de l'étage");
            int mine = LocalRig.Instance.Quadrant;
            TMP_Text distance = Field<TMP_Text[]>(hunt, "distanceTexts")[mine];
            Check(distance.text.Contains(SonarHunt.Distance(hunt.Target.Value, SonarHunt.Sonars[mine]).ToString()), "Sonar : la distance du module s'affiche");
            int wrong = (hunt.Target.Value + 1) % (SonarHunt.Size * SonarHunt.Size);
            hunt.PressedFrom(mine, wrong % SonarHunt.Size);
            hunt.PressedFrom(mine, SonarHunt.Size + wrong / SonarHunt.Size);
            hunt.PressedFrom(mine, SonarHunt.FireIndex);
            yield return Until(() => hunt.Misses.Value == 1, 3f, "Léviathan : un tir à côté est raté");
            int target = hunt.Target.Value;
            hunt.PressedFrom(mine, target % SonarHunt.Size);
            hunt.PressedFrom(mine, SonarHunt.Size + target / SonarHunt.Size);
            hunt.PressedFrom(mine, SonarHunt.FireIndex);
            yield return Until(() => hunt.Objective.Solved.Value, 3f, $"Léviathan : leurre tiré en {SonarHunt.CellName(target)} → touché");
        }

        /// <summary>Étage 4 : câblage du standard — un câblage faux (tout se débranche), puis le bon.</summary>
        static IEnumerator Switchboard()
        {
            PatchBoard board = Object.FindFirstObjectByType<PatchBoard>();
            Check(board != null && board.Ready, "Étage 4 : standard des circuits en place");
            if (board == null) yield break;
            // Les rôles tournent : on joue depuis le poste de l'opérateur, quel qu'il soit.
            int operatorModule = board.Operator(State.ActiveMask.Value);
            PatchStation station = Object.FindObjectsByType<PatchStation>(FindObjectsSortMode.None).FirstOrDefault(p => Field<int>(p, "quadrant") == operatorModule);
            Check(station != null && Field<GameObject>(station, "boardGroup").activeSelf, $"Standard : le tableau est chez l'opérateur (module {operatorModule})");
            if (station == null) yield break;
            for (int i = 0; i < PatchBoard.Sources; i++)
            {
                station.Pressed(i);
                station.Pressed(PatchBoard.Sources + board.Expected((i + 1) % PatchBoard.Sources));
                yield return Wait(0.15f);
            }
            yield return Until(() => board.Errors.Value == 1 && board.Links.Value == 0, 3f, "Standard : un câblage faux est refusé, tout se débranche");
            for (int i = 0; i < PatchBoard.Sources; i++)
            {
                station.Pressed(i);
                station.Pressed(PatchBoard.Sources + board.Expected(i));
                yield return Wait(0.15f);
            }
            yield return Until(() => board.Objective.Solved.Value, 3f, "Standard : 4 bonnes liaisons → circuits rétablis");
        }

        /// <summary>Étage 1 : code frappé — un rythme faux, puis le bon (coups simulés côté serveur, avec leurs pauses).</summary>
        static IEnumerator KnockedCode()
        {
            KnockCode code = Object.FindObjectsByType<KnockCode>(FindObjectsSortMode.None).FirstOrDefault(c => c.ObjectiveOf(0).Quadrant == 0 || c.ObjectiveOf(1).Quadrant == 0);
            Check(code != null, "Étage 1 : code frappé sur la cloison en place");
            if (code == null) yield break;
            int side = code.ObjectiveOf(0).Quadrant == 0 ? 0 : 1;
            PuzzleObjective objective = code.ObjectiveOf(side);
            if (objective.Solved.Value)
            {
                // En solo, le partenaire est absent : l'énigme est résolue d'office (personne pour écouter).
                Check(true, "Code frappé : sans partenaire, résolu d'office");
                yield break;
            }
            KnockWall wall = Field<KnockWall>(code, "wall");
            IEnumerator Knock(int pattern)
            {
                foreach (char digit in pattern.ToString())
                {
                    for (int k = 0; k < digit - '0'; k++)
                    {
                        wall.ServerKnock(0);
                        yield return Wait(0.25f);
                    }
                    yield return Wait(1.1f);
                }
            }
            int target = code.Pattern(side);
            yield return Knock(target == 111 ? 222 : 111);
            yield return Until(() => (side == 0 ? code.ReceivedA.Value : code.ReceivedB.Value) > 0 && !objective.Solved.Value, 5f, "Code frappé : un rythme faux est reçu mais refusé");
            yield return Knock(target);
            yield return Until(() => objective.Solved.Value, 5f, "Code frappé : le bon rythme frappé sur la cloison → message reçu");
        }

        /// <summary>Étage 2 : antidote — un mauvais mélange (il mousse et se vide), puis la recette traduite en couleurs.</summary>
        static IEnumerator Antidote()
        {
            Check(Object.FindObjectsByType<AntidoteMixer>(FindObjectsSortMode.None).Length == 4, "Étage 2 : 4 mélangeurs d'antidote");
            AntidoteMixer mixer = Mine<AntidoteMixer>();
            if (mixer == null) yield break;
            yield return Until(() => mixer.Ready, 3f, "Antidote : recette et réactifs tirés");
            AntidoteChart chart = Object.FindFirstObjectByType<AntidoteChart>();
            Check(chart != null && Field<TMP_Text>(chart, "text").text.Contains("="), "Antidote : tableau des réactifs (encre UV) rempli");
            AntidoteRecipe recipe = Object.FindObjectsByType<AntidoteRecipe>(FindObjectsSortMode.None).FirstOrDefault(r => Field<int>(r, "quadrant") == LocalRig.Instance.Quadrant);
            Check(recipe != null && Field<TMP_Text>(recipe, "text").text.Contains("×"), "Antidote : fiche de recette affichée");
            int failures = mixer.Failures.Value;
            int wrongFlask = 0;
            for (int i = 0; i < AntidoteMixer.Flasks; i++) if (mixer.Expected(i) < 3) { wrongFlask = i; break; }
            for (int k = 0; k <= mixer.Expected(wrongFlask); k++) mixer.Pressed(wrongFlask);
            yield return Wait(0.3f);
            mixer.Pressed(AntidoteMixer.MixIndex);
            yield return Until(() => mixer.Failures.Value == failures + 1 && mixer.Doses.Value == 0, 3f, "Antidote : un mauvais dosage mousse et se vide");
            for (int i = 0; i < AntidoteMixer.Flasks; i++)
                for (int k = 0; k < mixer.Expected(i); k++)
                {
                    mixer.Pressed(i);
                    yield return Wait(0.05f);
                }
            yield return Wait(0.3f);
            mixer.Pressed(AntidoteMixer.MixIndex);
            yield return Until(() => mixer.Objective.Solved.Value, 3f, "Antidote : recette respectée → antidote prêt");
        }

        /// <summary>Étage 3 : contrepoids — une charge fausse, puis la charge du registre (lests déplacés sur le plateau).</summary>
        static IEnumerator Counterweights()
        {
            BallastScale scale = Object.FindFirstObjectByType<BallastScale>();
            Check(scale != null, "Étage 3 : plateaux de lest en place");
            if (scale == null) yield break;
            int mine = LocalRig.Instance.Quadrant;
            yield return Until(() => scale.Target(mine) > 0, 3f, "Contrepoids : charges cibles tirées au début de l'étage");
            BallastRegister register = Object.FindObjectsByType<BallastRegister>(FindObjectsSortMode.None).FirstOrDefault(r => Field<int>(r, "quadrant") == mine);
            Check(register != null && Field<TMP_Text>(register, "text").text.Contains("kg"), "Contrepoids : registre de lest affiché");
            BallastWeight[] mineWeights = Field<BallastWeight[]>(scale, "weights").Where(w => w.name.EndsWith("_Q" + mine)).ToArray();
            Vector3 pan = scale.Pan(mine).bounds.center;
            float panFloor = scale.Pan(mine).bounds.min.y;
            void Place(BallastWeight weight, int index)
            {
                // Posé juste au-dessus du plateau (bas de la zone de pesée) : pas de chute qui ferait rebondir le lest.
                float height = weight.transform.localScale.y / 2f + 0.03f;
                Move(weight, new Vector3(pan.x + (index % 3 - 1) * 0.14f, panFloor + height, pan.z + (index / 3 - 0.5f) * 0.15f));
            }
            // Comme le sas et le retour d'objet perdu : le Rigidbody est déplacé avec l'objet, sinon la physique le ramène.
            void Move(BallastWeight weight, Vector3 position)
            {
                var body = weight.GetComponent<Rigidbody>();
                body.position = position;
                body.rotation = Quaternion.identity;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                weight.GetComponent<NetworkTransform>().Teleport(position, Quaternion.identity, weight.transform.localScale);
            }
            // Le plongeur se tient DEVANT le plateau (après la cale, son corps peut être resté dessus et chasserait les lests).
            Vector3 headNow = LocalRig.Instance.Head.position, away = new Vector3(headNow.x - pan.x, 0f, headNow.z - pan.z);
            if (away.magnitude < 0.9f) MoveHeadTo(pan + (away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward) * 0.9f);
            yield return Wait(0.3f);
            BallastWeight heavy = mineWeights.First(w => w.Kilograms == 5);
            Vector3 home = heavy.transform.position;
            Place(heavy, 0);
            yield return Wait(1.5f);
            if (scale.Load(mine) != 5)
                Debug.Log($"[Abysses] Test contrepoids (1er lest) : charge {scale.Load(mine)}, lest @{heavy.transform.position}, zone {scale.Pan(mine).bounds}, "
                    + $"propriétaire {heavy.GetComponent<NetworkObject>().OwnerClientId}, cinématique {heavy.GetComponent<Rigidbody>().isKinematic}, tête @{LocalRig.Instance.Head.position}");
            yield return Until(() => scale.Load(mine) == 5, 6f, "Contrepoids : le plateau pèse les lests posés dessus (5 kg)");
            Check(!scale.Objective.Solved.Value, "Contrepoids : charge fausse, pas d'équilibre");
            Move(heavy, home);
            yield return Until(() => scale.Load(mine) == 0, 4f, null);
            // Chaque plateau occupé reçoit un sous-ensemble de lests dont la somme vaut sa cible (lests échangés si besoin).
            // Les plus lourds d'abord : le moins de lests possible par plateau.
            var pool = Field<BallastWeight[]>(scale, "weights").OrderByDescending(w => w.Kilograms).ToList();
            int active = State.ActiveMask.Value;
            for (int q = 0; q < 4; q++)
            {
                if ((active & (1 << q)) == 0) continue;
                List<BallastWeight> chosen = Subset(pool, scale.Target(q));
                Check(chosen != null, $"Contrepoids : combinaison de lests trouvée pour {scale.Target(q)} kg");
                if (chosen == null) yield break;
                pan = scale.Pan(q).bounds.center;
                panFloor = scale.Pan(q).bounds.min.y;
                for (int i = 0; i < chosen.Count; i++)
                {
                    Place(chosen[i], i);
                    pool.Remove(chosen[i]);
                }
            }
            yield return Wait(2f);
            if (!scale.Objective.Solved.Value)
            {
                var report = new StringBuilder("[Abysses] Test contrepoids :");
                for (int q = 0; q < 4; q++) report.Append($" Q{q} {scale.Load(q)}/{scale.Target(q)} kg ;");
                foreach (BallastWeight w in Field<BallastWeight[]>(scale, "weights"))
                    report.Append($" {w.name}@{w.transform.position}");
                Debug.Log(report.ToString());
            }
            yield return Until(() => scale.Objective.Solved.Value, 6f, $"Contrepoids : chaque plateau à sa charge (dont {scale.Target(mine)} kg ici) → ballasts équilibrés");
        }

        static List<BallastWeight> Subset(List<BallastWeight> pool, int target)
        {
            if (target == 0) return new List<BallastWeight>();
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].Kilograms > target) continue;
                List<BallastWeight> rest = Subset(pool.Skip(i + 1).ToList(), target - pool[i].Kilograms);
                if (rest == null) continue;
                rest.Insert(0, pool[i]);
                return rest;
            }
            return null;
        }

        /// <summary>Méta-énigme : fragments affichés, mauvais code refusé, bon code saisi touche par touche.</summary>
        static IEnumerator CabinCode(int floor)
        {
            FloorLock floorLock = Object.FindObjectsByType<FloorLock>(FindObjectsSortMode.None).FirstOrDefault(l => l.Objective.Floor == floor);
            Check(floorLock != null, $"Étage {floor} : verrou de la cabine en place");
            if (floorLock == null) yield break;
            // À plusieurs, le test ne joue que les énigmes de l'hôte : le maître du jeu résout celles des autres plongeurs.
            int remaining = 0;
            foreach (PuzzleObjective objective in PuzzleObjective.Registered.ToArray())
                if (objective.Floor == floor && objective != floorLock.Objective && !objective.Solved.Value)
                {
                    objective.ServerSolve();
                    remaining++;
                }
            if (remaining > 0) Check(true, $"Pupitre : {remaining} énigme(s) des autres plongeurs résolue(s) par le maître du jeu");
            yield return Until(() => floorLock.AllFragments, 6f, $"Étage {floor} : tous les fragments du code révélés");
            int mine = LocalRig.Instance.Quadrant;
            FloorLockPanel panel = Object.FindObjectsByType<FloorLockPanel>(FindObjectsSortMode.None).FirstOrDefault(p => Field<int>(p, "quadrant") == mine);
            if (panel == null) { Check(false, "Verrou de la cabine dans le module du joueur"); yield break; }
            string fragments = Field<TMP_Text>(panel, "fragmentsText").text;
            Check(fragments.Contains(floorLock.Digit(0).ToString()), "Verrou : le fragment confié à ce module s'affiche");
            int wrong = floorLock.Wrong.Value;
            for (int i = 0; i < floorLock.Count; i++) panel.Pressed((floorLock.Digit(i) + 1) % 10);
            panel.Pressed(FloorLockPanel.ValidateIndex);
            yield return Until(() => floorLock.Wrong.Value == wrong + 1, 3f, "Verrou : un mauvais code est refusé");
            for (int i = 0; i < floorLock.Count; i++) panel.Pressed(floorLock.Digit(i));
            panel.Pressed(FloorLockPanel.ValidateIndex);
            // Le plongeur est déjà dans sa cabine : la descente peut partir aussitôt (et décharger l'étage).
            yield return Until(() => floorLock == null || floorLock.Unlocked || State.Phase.Value != GamePhase.Playing, 3f, "Verrou : le bon code ouvre la cabine");
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

            if (checkLostItem) yield return Wiring();
            yield return KnockedCode();

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
            yield return Antidote();
            yield return Relay(2);
            yield return SkipFloorAndDescend(2);
            yield return Until(() => Phase(GamePhase.Playing, 3), 45f, "Descente vers l'étage 3 (ballasts)");
            Check(WatchText().Contains("-2800"), "Montre : -2 800 m");
            Check(Object.FindObjectsByType<RisingWater>(FindObjectsSortMode.None).Length == 4, "Étage 3 : l'eau monte dans les 4 modules");
            yield return Manifold();
            yield return Maze();
            yield return Bulkheads();
            yield return Counterweights();
            yield return CabinCode(3);
            yield return Until(() => State.Phase.Value != GamePhase.Playing || State.Floor.Value != 3, 6f, "Étage 3 : code de la cabine → étage résolu");
            yield return Until(() => Phase(GamePhase.Playing, 4), 45f, "Descente vers l'étage 4 (salle commune)");
            Check(WatchText().Contains("-4000"), "Montre : -4 000 m");
            yield return Triangulation();
            yield return Switchboard();
            State.GmSkipFloorRpc();
            yield return Until(() => State.Phase.Value == GamePhase.Finished, 6f, "Séquence du réacteur + remontée → fin de partie");
            Check(State.EndTime.Value > State.StartTime.Value, "Chrono de fin cohérent");
            Check(Leaderboard.Parse(State.Leaderboard.Value.ToString()).Count > 0, "Temps enregistré au classement : " + Leaderboard.Format(State.Leaderboard.Value.ToString()).Split('\n').ElementAtOrDefault(1));
        }

        // ------------------------------------------------------------------ Scénario solo

        /// <summary>Une manette de jeu branchée sur le Mac (stick qui dérive) déplacerait le plongeur pendant le test.</summary>
        static void DisableGamepad()
        {
            foreach (GamepadLocomotion pad in Object.FindObjectsByType<GamepadLocomotion>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                pad.enabled = false;
        }

        static IEnumerator Solo()
        {
            yield return Wait(2f);
            DisableGamepad();
            ConnectionManager connection = ConnectionManager.Instance;
            connection.PlaySolo();
            yield return Until(() => connection.State == ConnectionState.Online && LocalRig.Instance.Quadrant == 0, 15f, "Partie solo lancée, quadrant ORANGE attribué");
            Check(PhaseSyncedLast(), "Réseau : la phase est reçue après l'étage, les modules actifs et les cabines (affichages croisés à jour chez les clients)");
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
            var info = new ProcessStartInfo(exe, $"-batchmode -nographics -abysses-mute -logFile \"{log}\" {extraArgs}") { UseShellExecute = false };
            Process process = Process.Start(info);
            processes.Add(process);
            return process;
        }

        static NetworkPlayer PlayerIn(int quadrant) => NetworkPlayer.All.FirstOrDefault(p => p.Quadrant.Value == quadrant);

        static IEnumerator Multi()
        {
            yield return Wait(2f);
            DisableGamepad();
            ConnectionManager connection = ConnectionManager.Instance;
            connection.HostLan();
            yield return Until(() => connection.State == ConnectionState.Online, 15f, "Hôte réseau local lancé (plongeur ORANGE)");

            Check(StartClient("client_plongeur", "-abysses-join-lan -abysses-player") != null, "Client Mac lancé (2e plongeur, trouve l'hôte par la découverte réseau local)");
            yield return Until(() => NetworkPlayer.All.Count == 2 && PlayerIn(1) != null, 90f, "2e plongeur connecté, quadrant CYAN");
            yield return Until(() => (State.ReadyMask.Value & 3) == 3, 20f, "Les 2 plongeurs détectés dans leur cabine");

            Check(StartClient("client_pupitre", "-abysses-join 127.0.0.1 -abysses-spectator -abysses-gm-hint") != null, "Pupitre spectateur lancé");
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
