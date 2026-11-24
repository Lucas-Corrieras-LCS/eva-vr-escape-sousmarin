using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using Abysses.Interaction;
using Abysses.Level;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Générateur, étage 4 « Cœur » : triangulation du Léviathan (<see cref="SonarHunt"/>, console sur le mur extérieur)
    /// et câblage du standard (<see cref="PatchBoard"/>, poste sur le mur du fond).
    /// </summary>
    public static partial class AbyssesBuilder
    {
        static PuzzleObjective BuildSonarHunt(Mats m, Transform parent, Module[] modules)
        {
            var huntGo = new GameObject("TriangulationLeviathan");
            huntGo.transform.SetParent(parent, false);
            huntGo.AddComponent<NetworkObject>();
            PuzzleObjective objective = Objective(huntGo, CoreFloor, -1, "Triangulation du Léviathan", "Leviathan triangulation", "sonarhunt");
            var hunt = huntGo.AddComponent<SonarHunt>();
            var distances = new TMP_Text[4];
            var entries = new TMP_Text[4];
            var statuses = new TMP_Text[4];

            foreach (Module mod in modules)
            {
                var root = new GameObject("ConsoleSonar_Q" + mod.Q);
                root.transform.SetParent(parent, false);
                root.transform.SetPositionAndRotation(mod.P(RelayWallXCore, 1.3f, mod.Vz * 3.62f), mod.Facing(0f, mod.Vz));
                Transform t = root.transform;
                DeviceMount(m, t, new Vector2(1.04f, 0.94f), 1.3f);
                Prim(PrimitiveType.Cube, "Boitier", t, new Vector3(0f, 0f, -0.02f), Quaternion.identity, new Vector3(0.98f, 0.88f, 0.06f), m.Panel, false);
                Label(t, "SONAR · LANCE-LEURRE", new Vector3(0f, 0.4f, -0.055f), 0.36f, null, "SONAR · DECOY LAUNCHER");

                // Carte 6 x 6 (A-F en colonnes, 1-6 en rangées), sonars aux coins (couleur du module) et au centre (réacteur).
                const float cell = 0.06f;
                var origin = new Vector3(-0.44f, 0.29f, -0.054f);
                Prim(PrimitiveType.Cube, "Carte", t, origin + new Vector3(cell * 3f, -cell * 3f, 0.002f), Quaternion.identity, new Vector3(cell * 6f + 0.02f, cell * 6f + 0.02f, 0.004f), m.SlotEmpty, false);
                for (int r = 0; r < SonarHunt.Size; r++)
                for (int c = 0; c < SonarHunt.Size; c++)
                    Prim(PrimitiveType.Quad, "Case", t, origin + new Vector3((c + 0.5f) * cell, -(r + 0.5f) * cell, -0.001f), Quaternion.identity, new Vector3(cell - 0.006f, cell - 0.006f, 1f), m.Panel, false);
                for (int i = 0; i < SonarHunt.Size; i++)
                {
                    Label(t, ((char)('A' + i)).ToString(), origin + new Vector3((i + 0.5f) * cell, 0.025f, -0.002f), 0.25f);
                    Label(t, (i + 1).ToString(), origin + new Vector3(-0.022f, -(i + 0.5f) * cell, -0.002f), 0.25f);
                }
                for (int q = 0; q < 4; q++)
                {
                    Vector2Int s = SonarHunt.Sonars[q];
                    Prim(PrimitiveType.Sphere, "Sonar_" + QuadrantNames[q], t, origin + new Vector3((s.x + 0.5f) * cell, -(s.y + 0.5f) * cell, -0.008f), Quaternion.identity, Vector3.one * 0.035f, m.Avatar[q], false);
                }
                Vector2Int reactor = SonarHunt.Reactor;
                Prim(PrimitiveType.Sphere, "Sonar_Reacteur", t, origin + new Vector3((reactor.x + 0.5f) * cell, -(reactor.y + 0.5f) * cell, -0.008f), Quaternion.identity, Vector3.one * 0.035f, m.EmitCyan, false);

                // Écran : distance de SON sonar (privé), saisie, état.
                Prim(PrimitiveType.Cube, "Ecran", t, new Vector3(0.24f, 0.2f, -0.052f), Quaternion.identity, new Vector3(0.44f, 0.2f, 0.004f), m.SlotEmpty, false);
                TextMeshPro distance = Text(t, "Distance", new Vector3(0.24f, 0.2f, -0.056f), Quaternion.identity, new Vector2(0.42f, 0.18f), 0.2f, new Color(0.55f, 1f, 0.8f), true);
                distance.enableAutoSizing = true;
                distance.fontSizeMin = 0.12f;
                distance.fontSizeMax = 0.2f;
                Private(root, mod.Q, distance.GetComponent<Renderer>());
                Prim(PrimitiveType.Cube, "Saisie", t, new Vector3(0.24f, 0.04f, -0.052f), Quaternion.identity, new Vector3(0.16f, 0.08f, 0.004f), m.SlotEmpty, false);
                TextMeshPro entry = Text(t, "Case", new Vector3(0.24f, 0.04f, -0.056f), Quaternion.identity, new Vector2(0.16f, 0.08f), 0.6f, new Color(1f, 0.85f, 0.5f), false);
                TextMeshPro status = Text(t, "Etat", new Vector3(0f, -0.36f, -0.055f), Quaternion.identity, new Vector2(0.9f, 0.08f), 0.2f, new Color(0.7f, 0.9f, 1f), true);

                var localOnly = new List<Behaviour>();
                for (int i = 0; i < SonarHunt.Size; i++)
                {
                    float x = -0.4f + i * 0.09f;
                    PokeButtonObject(m, t, "Lettre_" + i, new Vector3(x, -0.14f, -0.03f), new Vector2(0.07f, 0.06f), m.Key, hunt, i, null, null, localOnly);
                    Text(t, "L" + i, new Vector3(x, -0.14f, -0.072f), Quaternion.identity, new Vector2(0.07f, 0.06f), 0.4f, Color.white, false).text = ((char)('A' + i)).ToString();
                    PokeButtonObject(m, t, "Chiffre_" + i, new Vector3(x, -0.24f, -0.03f), new Vector2(0.07f, 0.06f), m.Key, hunt, SonarHunt.Size + i, null, null, localOnly);
                    Text(t, "N" + i, new Vector3(x, -0.24f, -0.072f), Quaternion.identity, new Vector2(0.07f, 0.06f), 0.4f, Color.white, false).text = (i + 1).ToString();
                }
                PokeButtonObject(m, t, "Feu", new Vector3(0.36f, -0.19f, -0.03f), new Vector2(0.14f, 0.16f), m.EmitRed, hunt, SonarHunt.FireIndex, null, null, localOnly);
                TextMeshPro fire = Text(t, "TexteFeu", new Vector3(0.36f, -0.19f, -0.072f), Quaternion.identity, new Vector2(0.14f, 0.08f), 0.4f, new Color(0.05f, 0.01f, 0.01f), false);
                fire.text = "FEU";
                fire.fontStyle = FontStyles.Bold;
                LocalOnly(root, mod.Q, localOnly);
                distances[mod.Q] = distance;
                entries[mod.Q] = entry;
                statuses[mod.Q] = status;
            }

            SetFields(hunt, ("objective", objective), ("fireSound", Sfx(huntGo, fx.Hatch, 0.8f, 0f)), ("missSound", Sfx(huntGo, fx.ReactorFail, 0.9f, 0f)), ("hitSound", Sfx(huntGo, fx.Success, 0.9f, 0f)));
            var so = new UnityEditor.SerializedObject(hunt);
            SetArray(so.FindProperty("distanceTexts"), distances);
            SetArray(so.FindProperty("entryTexts"), entries);
            SetArray(so.FindProperty("statusTexts"), statuses);
            so.ApplyModifiedPropertiesWithoutUndo();
            return objective;
        }

        static PuzzleObjective BuildPatchBoard(Mats m, Transform parent, Module[] modules)
        {
            var boardGo = new GameObject("Standard");
            boardGo.transform.SetParent(parent, false);
            boardGo.AddComponent<NetworkObject>();
            PuzzleObjective objective = Objective(boardGo, CoreFloor, -1, "Câblage du standard", "Switchboard wiring", "patch");
            var board = boardGo.AddComponent<PatchBoard>();
            SetFields(board, ("objective", objective), ("shift", 1), ("plugSound", Sfx(boardGo, fx.FuseInsert, 0.7f, 0f)),
                ("errorSound", Sfx(boardGo, fx.Error, 0.8f, 0f)), ("successSound", Sfx(boardGo, fx.PowerUp, 0.9f, 0f)));

            foreach (Module mod in modules)
            {
                var root = new GameObject("PosteStandard_Q" + mod.Q);
                root.transform.SetParent(parent, false);
                root.transform.SetPositionAndRotation(mod.P(-7.16f, 1.3f, mod.Vz * 1.0f), mod.Facing(-1f, 0f));
                Transform t = root.transform;
                var station = root.AddComponent<PatchStation>();
                DeviceMount(m, t, new Vector2(1.04f, 0.94f), 1.3f);
                Prim(PrimitiveType.Cube, "Boitier", t, new Vector3(0f, 0f, -0.02f), Quaternion.identity, new Vector3(0.98f, 0.88f, 0.06f), m.Panel, false);
                Label(t, "STANDARD DES CIRCUITS", new Vector3(0f, 0.4f, -0.055f), 0.36f, null, "CIRCUIT SWITCHBOARD");
                Prim(PrimitiveType.Cube, "Ecran", t, new Vector3(0f, 0.23f, -0.052f), Quaternion.identity, new Vector3(0.9f, 0.27f, 0.004f), m.SlotEmpty, false);
                TextMeshPro text = Text(t, "Texte", new Vector3(0f, 0.23f, -0.056f), Quaternion.identity, new Vector2(0.88f, 0.25f), 0.18f, new Color(0.55f, 1f, 0.8f), true);
                text.enableAutoSizing = true;
                text.fontSizeMin = 0.1f;
                text.fontSizeMax = 0.18f;
                Private(root, mod.Q, text.GetComponent<Renderer>());

                // Tableau de l'opérateur : 4 fiches à gauche, 8 prises à droite, câbles tendus entre eux.
                var group = new GameObject("Tableau").transform;
                group.SetParent(t, false);
                var localOnly = new List<Behaviour>();
                var sourcePoints = new Transform[PatchBoard.Sources];
                var socketPoints = new Transform[PatchBoard.Sockets];
                var sourceLabels = new TMP_Text[PatchBoard.Sources];
                var socketLabels = new TMP_Text[PatchBoard.Sockets];
                var caps = new Renderer[PatchBoard.Sources];
                for (int i = 0; i < PatchBoard.Sources; i++)
                {
                    var position = new Vector3(-0.3f, 0.04f - i * 0.1f, -0.03f);
                    GameObject plug = PokeButtonObject(m, group, "Fiche_" + i, position, new Vector2(0.07f, 0.07f), m.Brass, station, i, null, null, localOnly);
                    sourcePoints[i] = plug.transform;
                    caps[i] = plug.GetComponent<Renderer>();
                    sourceLabels[i] = Text(group, "NumFiche_" + i, position + new Vector3(-0.1f, 0f, -0.025f), Quaternion.identity, new Vector2(0.08f, 0.05f), 0.36f, Color.white, false);
                }
                for (int i = 0; i < PatchBoard.Sockets; i++)
                {
                    var position = new Vector3(0.3f, 0.07f - i * 0.055f, -0.03f);
                    GameObject socket = PokeButtonObject(m, group, "Prise_" + i, position, new Vector2(0.05f, 0.045f), m.Rubber, station, PatchBoard.Sources + i, null, null, localOnly);
                    socketPoints[i] = socket.transform;
                    socketLabels[i] = Text(group, "NumPrise_" + i, position + new Vector3(0.09f, 0f, -0.025f), Quaternion.identity, new Vector2(0.08f, 0.045f), 0.32f, Color.white, false);
                }
                var cables = new Transform[PatchBoard.Sources];
                for (int i = 0; i < cables.Length; i++)
                {
                    GameObject cable = Prim(PrimitiveType.Cylinder, "Cable_" + i, group, Vector3.zero, Quaternion.identity, Vector3.one * 0.012f, m.PipeRed, false);
                    cable.SetActive(false);
                    cables[i] = cable.transform;
                }
                Label(group, "FICHES", new Vector3(-0.3f, 0.1f, -0.055f), 0.24f, null, "PLUGS");
                Label(group, "PRISES", new Vector3(0.3f, 0.12f, -0.055f), 0.24f, null, "SOCKETS");
                LocalOnly(root, mod.Q, localOnly);

                SetFields(station, ("board", board), ("quadrant", mod.Q), ("text", text), ("boardGroup", group.gameObject),
                    ("capIdle", m.Brass), ("capSelected", m.EmitAmber));
                var so = new UnityEditor.SerializedObject(station);
                SetArray(so.FindProperty("sourceLabels"), sourceLabels);
                SetArray(so.FindProperty("socketLabels"), socketLabels);
                SetArray(so.FindProperty("sourcePoints"), sourcePoints);
                SetArray(so.FindProperty("socketPoints"), socketPoints);
                SetArray(so.FindProperty("cables"), cables);
                SetArray(so.FindProperty("sourceCaps"), caps);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return objective;
        }
    }
}
