using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using Abysses.Interaction;
using Abysses.Level;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Générateur, étage 3 « Ballasts » : labyrinthe électrifié dans la cale (<see cref="ShockMaze"/>) avec son écran de
    /// surveillance (<see cref="MazeMonitor"/>), et pupitres des cloisons étanches (<see cref="BulkheadNetwork"/>).
    /// </summary>
    public static partial class AbyssesBuilder
    {
        const float MazeStartX = 2.2f;
        const float BulkheadWallX = 3.3f;

        static ShockMaze BuildShockMaze(Mats m, Transform parent, Module mod)
        {
            var root = new GameObject("Cale_Q" + mod.Q);
            root.transform.SetParent(parent, false);
            root.AddComponent<NetworkObject>();
            PuzzleObjective objective = Objective(root, BallastFloor, mod.Q, "Cale électrifiée " + QuadrantNames[mod.Q], "Electrified hold " + QuadrantNamesEn[mod.Q], "maze");
            var maze = root.AddComponent<ShockMaze>();

            // Repère de la grille : X = colonnes (vers le mur du partenaire), Z = rangées (vers +z monde si Sx > 0).
            var origin = new GameObject("Grille").transform;
            origin.SetParent(root.transform, false);
            origin.SetPositionAndRotation(mod.Center + new Vector3(mod.Sx * MazeStartX, 0f, -mod.Sx * ShockMaze.Rows * ShockMaze.Tile / 2f),
                Quaternion.LookRotation(new Vector3(0f, 0f, mod.Sx), Vector3.up));
            float width = ShockMaze.Columns * ShockMaze.Tile, depth = ShockMaze.Rows * ShockMaze.Tile;
            // Dalles toutes identiques (rien ne trahit celles sous tension), cadre rayé autour de la zone.
            for (int r = 0; r < ShockMaze.Rows; r++)
            for (int c = 0; c < ShockMaze.Columns; c++)
                Static(Prim(PrimitiveType.Cube, "Dalle", origin, new Vector3((c + 0.5f) * ShockMaze.Tile, 0.012f, (r + 0.5f) * ShockMaze.Tile), Quaternion.identity,
                    new Vector3(ShockMaze.Tile - 0.03f, 0.02f, ShockMaze.Tile - 0.03f), m.Frame, false));
            Static(Prim(PrimitiveType.Cube, "Cadre", origin, new Vector3(width / 2f, 0.008f, -0.04f), Quaternion.identity, new Vector3(width + 0.16f, 0.016f, 0.08f), m.Hazard, false));
            Static(Prim(PrimitiveType.Cube, "Cadre", origin, new Vector3(width / 2f, 0.008f, depth + 0.04f), Quaternion.identity, new Vector3(width + 0.16f, 0.016f, 0.08f), m.Hazard, false));
            Static(Prim(PrimitiveType.Cube, "Cadre", origin, new Vector3(-0.04f, 0.008f, depth / 2f), Quaternion.identity, new Vector3(0.08f, 0.016f, depth), m.Hazard, false));
            Static(Prim(PrimitiveType.Cube, "Cadre", origin, new Vector3(width + 0.04f, 0.008f, depth / 2f), Quaternion.identity, new Vector3(0.08f, 0.016f, depth), m.Hazard, false));

            // Marque de départ (posée au tirage) et vanne de purge sur la dalle d'arrivée.
            var start = new GameObject("Depart").transform;
            start.SetParent(root.transform, false);
            Prim(PrimitiveType.Cylinder, "Pastille", start, new Vector3(0f, 0.025f, 0f), Quaternion.identity, new Vector3(0.36f, 0.006f, 0.36f), m.StatusOk, false);
            Label(start, "DÉPART", new Vector3(0f, 0.032f, 0f), 0.5f, Quaternion.Euler(90f, 0f, 0f), "START");
            var goal = new GameObject("VannePurge").transform;
            goal.SetParent(root.transform, false);
            Prim(PrimitiveType.Cylinder, "Colonne", goal, new Vector3(0f, 0.5f, 0f), Quaternion.identity, new Vector3(0.12f, 0.5f, 0.12f), m.PipeYellow, false);
            AbyssesArt.MeshObject("Volant", goal, art.Ring, new Vector3(0f, 1.02f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.34f, 0.34f, 0.03f), m.PipeRed);
            Prim(PrimitiveType.Sphere, "Feu", goal, new Vector3(0f, 1.1f, 0f), Quaternion.identity, Vector3.one * 0.07f, m.EmitAmber, false);

            // Panneau d'avertissement sur le mur du partenaire.
            Transform sign = Group("Alerte_Cale", root.transform, mod.P(7.17f, 1.9f, -2.6f), mod.Facing(1f, 0f));
            Static(Prim(PrimitiveType.Cube, "Plaque", sign, new Vector3(0f, 0f, 0.005f), Quaternion.identity, new Vector3(1.1f, 0.2f, 0.01f), m.Hazard, false));
            Label(sign, "DALLES SOUS TENSION · SUIVEZ LE GUIDAGE", new Vector3(0f, 0f, -0.006f), 0.32f, null, "LIVE TILES · FOLLOW GUIDANCE");

            ParticleSystem sparks = SparkBurst(m, origin, new Vector3(width / 2f, 0.05f, depth / 2f));
            AudioSource hum = Sfx(origin.gameObject, fx.NeonBuzz, 0.25f);
            hum.loop = true;
            hum.playOnAwake = true;
            SetFields(maze, ("objective", objective), ("origin", origin), ("rowsTowardNeighbor", mod.Sx * -mod.Vz > 0f),
                ("startMarker", start), ("goalMarker", goal), ("shockSound", Sfx(root, fx.Spark, 1f)), ("armSound", Sfx(root, fx.Key, 0.6f)), ("sparks", sparks));
            return maze;
        }

        /// <summary>Écran de surveillance d'une cale, mur extérieur : carte, point du plongeur, consignes.</summary>
        static void BuildMazeMonitor(Mats m, Transform parent, Module mod, ShockMaze[] mazes, int shift)
        {
            var root = new GameObject("SurveillanceCale_Q" + mod.Q);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(mod.P(RelayWallX, 1.35f, mod.Vz * 3.62f), mod.Facing(0f, mod.Vz));
            Transform t = root.transform;
            DeviceMount(m, t, new Vector2(0.9f, 0.82f), 1.35f);
            Prim(PrimitiveType.Cube, "Boitier", t, new Vector3(0f, 0f, -0.02f), Quaternion.identity, new Vector3(0.84f, 0.76f, 0.06f), m.Panel, false);
            TextMeshPro title = Text(t, "Titre", new Vector3(0f, 0.33f, -0.055f), Quaternion.identity, new Vector2(0.78f, 0.06f), 0.3f, new Color(1f, 0.85f, 0.55f), false);
            title.fontStyle = FontStyles.Bold;
            Prim(PrimitiveType.Cube, "Ecran", t, new Vector3(0f, 0.04f, -0.052f), Quaternion.identity, new Vector3(0.76f, 0.5f, 0.004f), m.SlotEmpty, false);

            // Carte : 6 x 4 cases de 9,5 cm, origine en bas à gauche.
            var mapRoot = new GameObject("Carte").transform;
            mapRoot.SetParent(t, false);
            var mapSize = new Vector2(0.6f, 0.4f);
            mapRoot.localPosition = new Vector3(-mapSize.x / 2f + 0.02f, 0.04f - mapSize.y / 2f, -0.056f);
            var cells = new Renderer[ShockMaze.Columns * ShockMaze.Rows];
            var privateRenderers = new List<Renderer>();
            for (int r = 0; r < ShockMaze.Rows; r++)
            for (int c = 0; c < ShockMaze.Columns; c++)
            {
                var cell = Prim(PrimitiveType.Quad, "Case", mapRoot, new Vector3((c + 0.5f) * mapSize.x / ShockMaze.Columns, (r + 0.5f) * mapSize.y / ShockMaze.Rows, 0f),
                    Quaternion.identity, new Vector3(mapSize.x / ShockMaze.Columns - 0.008f, mapSize.y / ShockMaze.Rows - 0.008f, 1f), m.SlotEmpty, false).GetComponent<Renderer>();
                cells[r * ShockMaze.Columns + c] = cell;
                privateRenderers.Add(cell);
            }
            GameObject dot = Prim(PrimitiveType.Sphere, "Plongeur", mapRoot, new Vector3(0f, 0f, -0.01f), Quaternion.identity, Vector3.one * 0.035f, m.EmitAmber, false);
            privateRenderers.Add(dot.GetComponent<Renderer>());
            Private(root, mod.Q, privateRenderers.ToArray());
            Label(t, "DÉPART", new Vector3(-0.355f, 0.04f, -0.055f), 0.2f, Quaternion.Euler(0f, 0f, 90f), "START");
            Label(t, "VANNE", new Vector3(0.375f, 0.04f, -0.055f), 0.2f, Quaternion.Euler(0f, 0f, -90f), "VALVE");
            TextMeshPro top = Text(t, "Haut", new Vector3(0.02f, 0.265f, -0.056f), Quaternion.identity, new Vector2(0.5f, 0.04f), 0.18f, new Color(0.7f, 0.85f, 0.9f), false);
            TextMeshPro bottom = Text(t, "Bas", new Vector3(0.02f, -0.185f, -0.056f), Quaternion.identity, new Vector2(0.5f, 0.04f), 0.18f, new Color(0.7f, 0.85f, 0.9f), false);
            TextMeshPro status = Text(t, "Etat", new Vector3(0f, -0.29f, -0.055f), Quaternion.identity, new Vector2(0.78f, 0.1f), 0.2f, new Color(0.55f, 1f, 0.8f), true);

            var monitor = root.AddComponent<MazeMonitor>();
            SetFields(monitor, ("quadrant", mod.Q), ("shift", shift), ("safeMaterial", m.StatusOk), ("dangerMaterial", m.StatusError),
                ("startMaterial", m.EmitCyan), ("goalMaterial", m.EmitAmber), ("dot", dot.transform), ("mapSize", mapSize),
                ("title", title), ("topLabel", top), ("bottomLabel", bottom), ("status", status));
            var so = new UnityEditor.SerializedObject(monitor);
            SetArray(so.FindProperty("mazes"), mazes);
            SetArray(so.FindProperty("cells"), cells);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Cloisons étanches : objectif partagé et un pupitre par module (plan si désigné, commandes de portes).</summary>
        static PuzzleObjective BuildBulkheads(Mats m, Transform parent, Module[] modules, int shift)
        {
            var networkGo = new GameObject("CloisonsEtanches");
            networkGo.transform.SetParent(parent, false);
            networkGo.AddComponent<NetworkObject>();
            PuzzleObjective objective = Objective(networkGo, BallastFloor, -1, "Cloisons étanches", "Watertight doors", "bulkhead");
            var network = networkGo.AddComponent<BulkheadNetwork>();
            SetFields(network, ("objective", objective), ("shift", shift), ("spreadSound", Sfx(networkGo, fx.CreakLow, 0.8f, 0f)),
                ("failSound", Sfx(networkGo, fx.ReactorFail, 0.8f, 0f)), ("doorSound", Sfx(networkGo, fx.Hatch, 0.7f, 0f)));

            foreach (Module mod in modules)
            {
                var root = new GameObject("PupitreCloisons_Q" + mod.Q);
                root.transform.SetParent(parent, false);
                root.transform.SetPositionAndRotation(mod.P(BulkheadWallX, 1.3f, -mod.Vz * 3.62f), mod.Facing(0f, -mod.Vz));
                Transform t = root.transform;
                var display = root.AddComponent<BulkheadDisplay>();
                DeviceMount(m, t, new Vector2(0.96f, 0.9f), 1.3f);
                Prim(PrimitiveType.Cube, "Boitier", t, new Vector3(0f, 0f, -0.02f), Quaternion.identity, new Vector3(0.9f, 0.84f, 0.06f), m.Panel, false);
                Label(t, "CLOISONS ÉTANCHES", new Vector3(0f, 0.37f, -0.055f), 0.36f, null, "WATERTIGHT DOORS");

                // Plan (2 rangées x 3 compartiments, portes entre eux), visible seulement si ce module tient le plan.
                var map = new GameObject("Plan").transform;
                map.SetParent(t, false);
                Prim(PrimitiveType.Cube, "Fond", map, new Vector3(-0.05f, 0.17f, -0.052f), Quaternion.identity, new Vector3(0.64f, 0.3f, 0.004f), m.SlotEmpty, false);
                var compartments = new Renderer[BulkheadNetwork.Compartments];
                var privateRenderers = new List<Renderer>();
                Vector3 CompartmentPos(int index) => new Vector3(-0.26f + (index % 3) * 0.21f, 0.235f - (index / 3) * 0.13f, -0.056f);
                for (int i = 0; i < compartments.Length; i++)
                {
                    compartments[i] = Prim(PrimitiveType.Quad, "Compartiment", map, CompartmentPos(i), Quaternion.identity, new Vector3(0.17f, 0.1f, 1f), m.PipeGrey, false).GetComponent<Renderer>();
                    privateRenderers.Add(compartments[i]);
                }
                var doorMarks = new Renderer[BulkheadNetwork.Doors.Length];
                for (int d = 0; d < doorMarks.Length; d++)
                {
                    Vector2Int door = BulkheadNetwork.Doors[d];
                    Vector3 position = (CompartmentPos(door.x) + CompartmentPos(door.y)) / 2f + new Vector3(0f, 0f, -0.002f);
                    bool horizontal = door.y == door.x + 1;
                    doorMarks[d] = Prim(PrimitiveType.Quad, "Porte", map, position, Quaternion.identity, horizontal ? new Vector3(0.025f, 0.07f, 1f) : new Vector3(0.09f, 0.025f, 1f), m.StatusOk, false).GetComponent<Renderer>();
                    privateRenderers.Add(doorMarks[d]);
                    TextMeshPro label = Text(map, "P" + (d + 1), position + (horizontal ? new Vector3(0f, 0.052f, -0.001f) : new Vector3(0.065f, 0f, -0.001f)), Quaternion.identity, new Vector2(0.05f, 0.03f), 0.2f, Color.white, false);
                    label.text = "P" + (d + 1);
                    privateRenderers.Add(label.GetComponent<Renderer>());
                }
                Private(map.gameObject, mod.Q, privateRenderers.ToArray());

                TextMeshPro status = Text(t, "Etat", new Vector3(0f, -0.06f, -0.055f), Quaternion.identity, new Vector2(0.84f, 0.12f), 0.19f, new Color(0.55f, 1f, 0.8f), true);
                // Commandes : une par porte, affichée seulement dans le module qui la commande.
                var localOnly = new List<Behaviour>();
                var buttons = new GameObject[BulkheadNetwork.Doors.Length];
                for (int d = 0; d < buttons.Length; d++)
                {
                    var group = new GameObject("Commande_P" + (d + 1)).transform;
                    group.SetParent(t, false);
                    float x = -0.33f + d * 0.11f;
                    PokeButtonObject(m, group, "Bouton", new Vector3(x, -0.25f, -0.03f), new Vector2(0.085f, 0.075f), m.PipeRed, display, d, null, null, localOnly);
                    TextMeshPro label = Text(group, "Etiquette", new Vector3(x, -0.17f, -0.055f), Quaternion.identity, new Vector2(0.09f, 0.04f), 0.26f, Color.white, false);
                    label.text = "P" + (d + 1);
                    label.fontStyle = FontStyles.Bold;
                    buttons[d] = group.gameObject;
                }
                Label(t, "FERMER LA PORTE", new Vector3(0f, -0.36f, -0.055f), 0.22f, null, "CLOSE THE DOOR");
                // Déclencheur de l'alerte, à côté du plan (affiché seulement chez celui qui a le plan).
                var startGroup = new GameObject("Alerte").transform;
                startGroup.SetParent(t, false);
                PokeButtonObject(m, startGroup, "Declencher", new Vector3(0.36f, 0.17f, -0.03f), new Vector2(0.1f, 0.18f), m.EmitAmber, display, BulkheadDisplay.StartIndex, null, null, localOnly);
                TextMeshPro alarmLabel = Text(startGroup, "Texte", new Vector3(0.36f, 0.17f, -0.072f), Quaternion.Euler(0f, 0f, 90f), new Vector2(0.18f, 0.08f), 0.22f, new Color(0.05f, 0.03f, 0.01f), false);
                alarmLabel.text = "ALERTE";
                alarmLabel.fontStyle = FontStyles.Bold;
                LocalOnly(root, mod.Q, localOnly);

                SetFields(display, ("network", network), ("quadrant", mod.Q), ("map", map.gameObject), ("dry", m.PipeGrey), ("water", m.EmitCyan),
                    ("pump", m.EmitAmber), ("doorOpen", m.StatusOk), ("doorClosed", m.StatusError), ("status", status), ("startButton", startGroup.gameObject));
                var so = new UnityEditor.SerializedObject(display);
                SetArray(so.FindProperty("compartments"), compartments);
                SetArray(so.FindProperty("doorMarks"), doorMarks);
                SetArray(so.FindProperty("doorButtons"), buttons);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return objective;
        }
    }
}
