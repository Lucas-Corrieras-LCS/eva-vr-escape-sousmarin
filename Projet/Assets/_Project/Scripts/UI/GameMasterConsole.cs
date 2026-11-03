using System.Collections.Generic;
using System.Globalization;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using Abysses.Core;
using Abysses.Level;
using Abysses.Network;
using Abysses.Player;

namespace Abysses.UI
{
    /// <summary>
    /// Pupitre du maître du jeu (staff EVA) et écran spectateur, sur un PC (build bureau lancé avec -pupitre,
    /// ou F1 dans l'éditeur). Le PC rejoint la partie comme spectateur (sans avatar) ou l'héberge en réseau local.
    /// - Suivi : phase, profondeur, chrono, plongeurs (en cabine ou non), énigmes de l'étage.
    /// - Actions : indice immédiat, message libre, résoudre une énigme, passer l'étage, relancer la partie, mode facile.
    /// - Caméras : surveillance de chaque module, vue derrière chaque plongeur, caméra libre ;
    ///   mode « vitrine » plein écran qui alterne les vues pour le public qui attend.
    /// Interface IMGUI : outil PC, jamais actif dans le casque.
    /// </summary>
    public class GameMasterConsole : MonoBehaviour
    {
        [SerializeField] Camera spectatorCamera;
        [SerializeField, Min(2f)] float showcaseInterval = 8f;

        enum View { Module0, Module1, Module2, Module3, Diver, Free }

        bool active;
        bool showcase;
        bool spectatorJoin = true;
        string address = "192.168.1.10";
        string message = "";
        View view = View.Module0;
        int diverIndex;
        float nextShowcaseSwitch;
        Vector2 scroll;
        readonly List<string> log = new List<string>(8);
        Vector3 freePosition = new Vector3(15.5f, 2.6f, -1f);
        Vector2 freeAngles = new Vector2(25f, 0f);
        GUIStyle title, big, small;

        void Start()
        {
            bool requested = System.Environment.CommandLine.Contains("-pupitre") || System.Environment.CommandLine.Contains("-gm");
            bool desktopWithoutHeadset = !Application.isEditor && !Application.isMobilePlatform && !XRSettings.isDeviceActive;
            if (Application.isMobilePlatform && !Application.isEditor)
            {
                // Dans le casque : jamais de pupitre.
                enabled = false;
                if (spectatorCamera != null) spectatorCamera.gameObject.SetActive(false);
                return;
            }
            SetActive(requested || desktopWithoutHeadset);
        }

        void OnEnable() => HintSystem.MessageReceived += OnMessage;
        void OnDisable() => HintSystem.MessageReceived -= OnMessage;

        void OnMessage(string text)
        {
            if (log.Count == log.Capacity) log.RemoveAt(0);
            log.Add(System.DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture) + "  " + text);
        }

        void SetActive(bool value)
        {
            active = value;
            if (spectatorCamera != null) spectatorCamera.gameObject.SetActive(value);
            // La caméra du rig XR ne rendrait que l'origine (aucun casque) : coupée tant que le pupitre est affiché.
            if (LocalRig.Instance != null && LocalRig.Instance.Head.TryGetComponent(out Camera rigCamera)) rigCamera.enabled = !value;
            ConnectionManager connection = ConnectionManager.Instance;
            if (connection != null && connection.State != ConnectionState.Online) connection.Spectator = value && spectatorJoin;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (Application.isEditor && keyboard != null && keyboard.f1Key.wasPressedThisFrame) SetActive(!active);
            if (!active || spectatorCamera == null) return;

            if (keyboard != null)
            {
                if (keyboard.f2Key.wasPressedThisFrame) showcase = !showcase;
                for (int i = 0; i < 6; i++)
                    if (keyboard[Key.Digit1 + i].wasPressedThisFrame) view = (View)i;
            }
            if (showcase && Time.unscaledTime >= nextShowcaseSwitch)
            {
                nextShowcaseSwitch = Time.unscaledTime + showcaseInterval;
                NextShowcaseView();
            }
            PlaceCamera(keyboard);
        }

        void NextShowcaseView()
        {
            // Alterne entre les modules occupés et les plongeurs.
            int occupied = NetworkPlayer.OccupiedQuadrantMask();
            for (int step = 0; step < 8; step++)
            {
                if (view == View.Diver && diverIndex + 1 < NetworkPlayer.All.Count) { diverIndex++; return; }
                view = (View)(((int)view + 1) % 5);
                diverIndex = 0;
                if (view == View.Diver ? NetworkPlayer.All.Count > 0 : occupied == 0 || (occupied & (1 << (int)view)) != 0) return;
            }
        }

        void PlaceCamera(Keyboard keyboard)
        {
            Transform cam = spectatorCamera.transform;
            if (view <= View.Module3)
            {
                // Caméra de surveillance dans le coin extérieur du module, sous le plafond.
                int q = (int)view;
                Rect r = PlayArea.GetRect(q, PlayArea.WallInset + 0.4f);
                var corner = new Vector3(q % 2 == 0 ? r.xMin : r.xMax, 2.75f, q < 2 ? r.yMin : r.yMax);
                Vector3 center = PlayArea.GetCenter(q) + Vector3.up * 0.9f;
                cam.SetPositionAndRotation(corner, Quaternion.LookRotation(center - corner));
                spectatorCamera.fieldOfView = 75f;
            }
            else if (view == View.Diver)
            {
                if (NetworkPlayer.All.Count == 0) { view = View.Module0; return; }
                NetworkPlayer diver = NetworkPlayer.All[Mathf.Clamp(diverIndex, 0, NetworkPlayer.All.Count - 1)];
                Transform head = diver.Head;
                Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
                forward.Normalize();
                Vector3 target = head.position + forward * 0.8f;
                Vector3 position = head.position - forward * 0.9f + Vector3.up * 0.35f;
                cam.SetPositionAndRotation(Vector3.Lerp(cam.position, position, 0.15f), Quaternion.Slerp(cam.rotation, Quaternion.LookRotation(target - position), 0.15f));
                spectatorCamera.fieldOfView = 70f;
            }
            else
            {
                // Caméra libre : ZQSD/WASD + clic droit pour regarder.
                Mouse mouse = Mouse.current;
                if (mouse != null && mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue() * 0.15f;
                    freeAngles += new Vector2(-delta.y, delta.x);
                    freeAngles.x = Mathf.Clamp(freeAngles.x, -85f, 85f);
                }
                Quaternion rotation = Quaternion.Euler(freeAngles.x, freeAngles.y, 0f);
                if (keyboard != null)
                {
                    Vector3 move = Vector3.zero;
                    if (keyboard.wKey.isPressed || keyboard.zKey.isPressed) move += Vector3.forward;
                    if (keyboard.sKey.isPressed) move += Vector3.back;
                    if (keyboard.aKey.isPressed || keyboard.qKey.isPressed) move += Vector3.left;
                    if (keyboard.dKey.isPressed) move += Vector3.right;
                    if (keyboard.eKey.isPressed) move += Vector3.up;
                    if (keyboard.cKey.isPressed) move += Vector3.down;
                    freePosition += rotation * move * (3f * Time.unscaledDeltaTime);
                }
                cam.SetPositionAndRotation(freePosition, rotation);
                spectatorCamera.fieldOfView = 70f;
            }
        }

        // ------------------------------------------------------------------ Interface

        void OnGUI()
        {
            if (!active) return;
            EnsureStyles();
            float scale = Mathf.Max(0.75f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale, height = Screen.height / scale;

            GameStateManager state = GameStateManager.Instance;
            if (showcase)
            {
                DrawShowcase(state, width, height);
                return;
            }

            GUILayout.BeginArea(new Rect(10f, 10f, 400f, height - 20f), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label(Loc.T("ABYSSES — Pupitre", "ABYSSES — Game master"), title);
            DrawConnection();
            if (state != null && state.IsSpawned)
            {
                DrawStatus(state);
                DrawActions(state);
                DrawObjectives(state);
                GUILayout.Space(6f);
                GUILayout.Label(Leaderboard.Format(state.Leaderboard.Value.ToString()), small);
            }
            DrawLog();
            DrawViews();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawConnection()
        {
            ConnectionManager connection = ConnectionManager.Instance;
            if (connection == null) return;
            GUILayout.Label(connection.Status, small);
            if (connection.State == ConnectionState.Online)
            {
                if (GUILayout.Button(Loc.T("Se déconnecter", "Disconnect"))) connection.Leave();
                return;
            }
            if (connection.IsBusy) return;
            spectatorJoin = GUILayout.Toggle(spectatorJoin, Loc.T(" Pupitre sans avatar (spectateur)", " Console without avatar (spectator)"));
            connection.Spectator = spectatorJoin;
            if (GUILayout.Button(Loc.T("Héberger la partie (réseau local)", "Host the game (local network)"))) connection.HostLan();
            if (GUILayout.Button(Loc.T("Rejoindre (réseau local, auto)", "Join (local network, auto)"))) connection.JoinLan();
            if (GUILayout.Button(Loc.T("Rejoindre (en ligne)", "Join (online)"))) connection.JoinOnline();
            GUILayout.BeginHorizontal();
            address = GUILayout.TextField(address, GUILayout.Width(200f));
            if (GUILayout.Button(Loc.T("Rejoindre l'IP", "Join IP"))) connection.JoinDirect(address);
            GUILayout.EndHorizontal();
            if (GUILayout.Button(Loc.English ? "Français" : "English")) Loc.Toggle();
        }

        void DrawStatus(GameStateManager state)
        {
            GUILayout.Space(6f);
            string phase = state.Phase.Value switch
            {
                GamePhase.Lobby => Loc.T("Surface (préparation)", "Surface (briefing)"),
                GamePhase.Playing => Loc.T("Énigmes en cours", "Puzzles in progress"),
                GamePhase.FloorSolved => Loc.T("Étage résolu — retour aux cabines", "Floor solved — back to the cabins"),
                GamePhase.Descending => Loc.T("Descente", "Descending"),
                _ => Loc.T("Remontée réussie", "Ascent complete"),
            };
            GUILayout.Label(phase + "   ·   -" + state.DepthOf(state.Floor.Value) + " m", big);
            GUILayout.Label(Loc.T("Temps : ", "Time: ") + Leaderboard.Duration(Elapsed(state)) + (state.EasyMode.Value ? Loc.T("   ·   mode facile", "   ·   easy mode") : ""));
            for (int i = 0; i < NetworkPlayer.All.Count; i++)
            {
                int q = NetworkPlayer.All[i].Quadrant.Value;
                bool inCabin = PlayArea.IsValid(q) && (state.ReadyMask.Value & (1 << q)) != 0;
                GUILayout.Label("  ● " + Loc.T("Module ", "Module ") + QuadrantName(q) + (inCabin ? Loc.T("  — en cabine", "  — in cabin") : ""));
            }
            if (NetworkPlayer.All.Count == 0) GUILayout.Label(Loc.T("  Aucun plongeur connecté", "  No diver connected"));
        }

        void DrawActions(GameStateManager state)
        {
            GUILayout.Space(6f);
            GamePhase phase = state.Phase.Value;
            if (phase == GamePhase.Lobby)
            {
                bool easy = GUILayout.Toggle(state.EasyMode.Value, Loc.T(" Mode facile (enfants, débutants)", " Easy mode (kids, beginners)"));
                if (easy != state.EasyMode.Value) state.SetEasyModeRpc(easy);
                GUI.enabled = state.EveryoneReady;
                if (GUILayout.Button(Loc.T("Lancer la plongée", "Start the dive"))) state.RequestStartRpc();
                GUI.enabled = true;
            }
            if (phase == GamePhase.Playing)
            {
                if (GUILayout.Button(Loc.T("Donner un indice maintenant", "Give a hint now")) && HintSystem.Instance != null) HintSystem.Instance.RequestHintRpc();
                if (GUILayout.Button(Loc.T("Passer l'étage", "Skip this floor"))) state.GmSkipFloorRpc();
            }
            if (phase == GamePhase.Finished)
            {
                if (GUILayout.Button(Loc.T("Nouvelle plongée (même équipe)", "New dive (same team)"))) state.RequestRestartRpc();
            }
            if (phase == GamePhase.Playing || phase == GamePhase.FloorSolved || phase == GamePhase.Finished)
            {
                if (GUILayout.Button(Loc.T("Relancer la partie (retour surface)", "Restart (back to surface)"))) state.GmRestartRpc();
            }
            GUILayout.BeginHorizontal();
            message = GUILayout.TextField(message, 160, GUILayout.Width(280f));
            if (GUILayout.Button(Loc.T("Envoyer", "Send")) && HintSystem.Instance != null && message.Trim().Length > 0)
            {
                HintSystem.Instance.BroadcastMessageRpc(message.Trim());
                message = "";
            }
            GUILayout.EndHorizontal();
        }

        void DrawObjectives(GameStateManager state)
        {
            if (state.Phase.Value != GamePhase.Playing) return;
            GUILayout.Space(6f);
            GUILayout.Label(Loc.T("Énigmes de l'étage", "Puzzles on this floor"), big);
            IReadOnlyList<PuzzleObjective> all = PuzzleObjective.Registered;
            for (int i = 0; i < all.Count; i++)
            {
                PuzzleObjective o = all[i];
                if (o.Floor != state.Floor.Value) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label((o.Solved.Value ? "✔ " : "○ ") + o.Label, GUILayout.Width(280f));
                if (!o.Solved.Value && GUILayout.Button(Loc.T("Résoudre", "Solve"))) state.GmSolveRpc(o);
                GUILayout.EndHorizontal();
            }
        }

        void DrawLog()
        {
            if (log.Count == 0) return;
            GUILayout.Space(6f);
            GUILayout.Label(Loc.T("Messages envoyés aux plongeurs", "Messages sent to divers"), big);
            for (int i = log.Count - 1; i >= 0; i--) GUILayout.Label(log[i], small);
        }

        void DrawViews()
        {
            GUILayout.Space(6f);
            GUILayout.Label(Loc.T("Caméras (touches 1 à 6, F2 = vitrine)", "Cameras (keys 1-6, F2 = showcase)"), big);
            GUILayout.BeginHorizontal();
            for (int q = 0; q < 4; q++)
                if (GUILayout.Button(QuadrantName(q))) view = (View)q;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Loc.T("Plongeur suivant", "Next diver")))
            {
                diverIndex = view == View.Diver ? (diverIndex + 1) % Mathf.Max(1, NetworkPlayer.All.Count) : 0;
                view = View.Diver;
            }
            if (GUILayout.Button(Loc.T("Libre", "Free"))) view = View.Free;
            if (GUILayout.Button(Loc.T("Vitrine", "Showcase"))) showcase = true;
            GUILayout.EndHorizontal();
        }

        void DrawShowcase(GameStateManager state, float width, float height)
        {
            GUI.Label(new Rect(30f, 20f, width - 60f, 80f), "ABYSSES", title);
            if (state != null && state.IsSpawned)
            {
                string line = "-" + state.DepthOf(state.Floor.Value) + " m     " + Leaderboard.Duration(Elapsed(state));
                GUI.Label(new Rect(30f, height - 90f, width - 60f, 70f), line, title);
            }
            if (GUI.Button(new Rect(width - 130f, 20f, 110f, 30f), Loc.T("Quitter (F2)", "Exit (F2)"))) showcase = false;
        }

        static int Elapsed(GameStateManager state)
        {
            if (state.StartTime.Value <= 0d || NetworkManager.Singleton == null) return 0;
            double end = state.Phase.Value == GamePhase.Finished ? state.EndTime.Value : NetworkManager.Singleton.ServerTime.Time;
            return Mathf.Max(0, (int)(end - state.StartTime.Value));
        }

        static string QuadrantName(int q)
        {
            switch (q)
            {
                case 0: return "ORANGE";
                case 1: return "CYAN";
                case 2: return Loc.T("VERT", "GREEN");
                case 3: return "MAGENTA";
                default: return "?";
            }
        }

        void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            title.normal.textColor = new Color(0.4f, 0.95f, 1f);
            big = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
        }
    }
}
