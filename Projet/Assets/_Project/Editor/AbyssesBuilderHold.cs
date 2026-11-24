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
            // Sous les dalles, un plancher sous tension : les joints de 3 cm luisent et dessinent la grille à compter
            // (« 3e colonne, 2e rangée ») sans trahir quelles dalles sont dangereuses.
            Material seam = Lit("M_Emit_Seam", new Color(0.02f, 0.05f, 0.06f), 0f, 0.4f, new Color(0.12f, 0.55f, 0.7f) * 0.9f);
            Prim(PrimitiveType.Cube, "JointsSousTension", origin, new Vector3(width / 2f, 0.003f, depth / 2f), Quaternion.identity, new Vector3(width, 0.004f, depth), seam, false);
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

        /// <summary>
        /// Contrepoids des ballasts : plateau de pesée sur colonne (côté voisin), étagère avec 4 lests (1, 2, 2, 5 kg),
        /// cadran de charge, et registre (charge cible d'un autre plateau) sur le mur extérieur.
        /// </summary>
        static PuzzleObjective BuildBallastScale(Mats m, Transform parent, Module[] modules, int shift)
        {
            var scaleGo = new GameObject("Contrepoids");
            scaleGo.transform.SetParent(parent, false);
            scaleGo.AddComponent<NetworkObject>();
            PuzzleObjective objective = Objective(scaleGo, BallastFloor, -1, "Contrepoids des ballasts", "Ballast counterweights", "ballastscale");
            var scale = scaleGo.AddComponent<BallastScale>();
            var pans = new BoxCollider[4];
            var dials = new TMP_Text[4];
            var weights = new List<BallastWeight>();
            int[] kilograms = { 1, 2, 2, 5 };

            foreach (Module mod in modules)
            {
                Transform t = Group("Pesee_Q" + mod.Q, parent, mod.P(-4.0f, 0f, -mod.Vz * 2.95f), mod.Facing(0f, -mod.Vz));
                GameObject column = Prim(PrimitiveType.Cube, "Colonne", t, new Vector3(0f, 0.4f, 0f), Quaternion.identity, new Vector3(0.36f, 0.8f, 0.36f), m.PipeGrey, true);
                Static(column);
                GameObject pan = Prim(PrimitiveType.Cylinder, "Plateau", t, new Vector3(0f, 0.83f, 0f), Quaternion.identity, new Vector3(0.52f, 0.015f, 0.52f), m.Brass, false);
                pan.AddComponent<BoxCollider>().size = new Vector3(1f, 2f, 1f);
                AbyssesArt.MeshObject("Rebord", t, art.Ring, new Vector3(0f, 0.85f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.54f, 0.54f, 0.04f), m.Brass);
                var zoneGo = new GameObject("ZonePesee");
                zoneGo.transform.SetParent(t, false);
                zoneGo.transform.localPosition = new Vector3(0f, 0.97f, 0f);
                var zone = zoneGo.AddComponent<BoxCollider>();
                zone.isTrigger = true;
                zone.size = new Vector3(0.5f, 0.26f, 0.5f);
                pans[mod.Q] = zone;
                // Cadran sur la face avant de la colonne.
                Prim(PrimitiveType.Cube, "Cadran", t, new Vector3(0f, 0.62f, -0.185f), Quaternion.identity, new Vector3(0.3f, 0.12f, 0.01f), m.SlotEmpty, false);
                TextMeshPro dial = Text(t, "Charge", new Vector3(0f, 0.62f, -0.192f), Quaternion.identity, new Vector2(0.28f, 0.1f), 0.32f, new Color(1f, 0.85f, 0.5f), false);
                dial.text = "CHARGE : 0 kg";
                dials[mod.Q] = dial;
                Label(t, "PLATEAU DE LEST", new Vector3(0f, 0.72f, -0.19f), 0.22f, null, "BALLAST PAN");

                // Étagère des lests, à côté du plateau.
                Transform shelf = Group("EtagereLests_Q" + mod.Q, parent, mod.P(-5.0f, 0f, -mod.Vz * 3.15f), mod.Facing(0f, -mod.Vz));
                Static(Prim(PrimitiveType.Cube, "Tablette", shelf, new Vector3(0f, 0.8f, 0f), Quaternion.identity, new Vector3(0.7f, 0.04f, 0.32f), m.Panel, true));
                Static(Prim(PrimitiveType.Cube, "Liseré", shelf, new Vector3(0f, 0.8f, -0.165f), Quaternion.identity, new Vector3(0.7f, 0.045f, 0.012f), m.Hazard, false));
                foreach (float x in new[] { -0.3f, 0.3f })
                    Static(Prim(PrimitiveType.Cube, "Pied", shelf, new Vector3(x, 0.39f, 0f), Quaternion.identity, new Vector3(0.04f, 0.78f, 0.28f), m.Frame, false));
                for (int i = 0; i < kilograms.Length; i++)
                    weights.Add(BuildWeight(m, parent, mod, kilograms[i], shelf.TransformPoint(new Vector3(-0.24f + i * 0.16f, 0.88f, 0f))));

                // Registre : charge cible d'un autre plateau (routage), mur extérieur.
                Transform register = Group("RegistreLest_Q" + mod.Q, parent, mod.P(-2.4f, 1.4f, mod.Vz * 3.62f), mod.Facing(0f, mod.Vz));
                DeviceMount(m, register, new Vector2(0.56f, 0.5f), 1.4f);
                Prim(PrimitiveType.Cube, "Fiche", register, new Vector3(0f, 0f, -0.02f), Quaternion.identity, new Vector3(0.5f, 0.44f, 0.02f), paperMaterial, false);
                TextMeshPro text = Text(register, "Texte", new Vector3(0f, 0f, -0.032f), Quaternion.identity, new Vector2(0.46f, 0.4f), 0.2f, new Color(0.1f, 0.09f, 0.08f), true);
                text.enableAutoSizing = true;
                text.fontSizeMin = 0.1f;
                text.fontSizeMax = 0.2f;
                Private(register.gameObject, mod.Q, text.GetComponent<Renderer>());
                SetFields(register.gameObject.AddComponent<BallastRegister>(), ("quadrant", mod.Q), ("scale", scale), ("shift", shift), ("text", text));
            }

            SetFields(scale, ("objective", objective), ("successSound", Sfx(scaleGo, fx.Success, 0.9f, 0f)));
            var so = new UnityEditor.SerializedObject(scale);
            SetArray(so.FindProperty("pans"), pans);
            SetArray(so.FindProperty("weights"), weights.ToArray());
            SetArray(so.FindProperty("dials"), dials);
            so.ApplyModifiedPropertiesWithoutUndo();
            return objective;
        }

        /// <summary>Lest de fonte attrapable : taille selon la masse, poignée de laiton, masse gravée.</summary>
        static BallastWeight BuildWeight(Mats m, Transform parent, Module mod, int kilograms, Vector3 position)
        {
            float size = kilograms >= 5 ? 0.13f : kilograms >= 2 ? 0.1f : 0.08f;
            // Lest plombé gainé de peinture, une couleur par masse (comme les lests de plongée) : lisible de loin
            // sur l'étagère sombre (« passe-moi le rouge de 2 kg »). Masse gravée devant et derrière.
            Material paint = kilograms >= 5 ? Lit("M_Lest_5kg", new Color(0.12f, 0.27f, 0.55f), 0.35f, 0.5f)
                : kilograms >= 2 ? Lit("M_Lest_2kg", new Color(0.72f, 0.18f, 0.08f), 0.35f, 0.5f)
                : Lit("M_Lest_1kg", new Color(0.9f, 0.68f, 0.1f), 0.35f, 0.5f);
            Color ink = kilograms >= 2 ? new Color(0.96f, 0.96f, 0.92f) : new Color(0.08f, 0.08f, 0.08f);
            GameObject body = Prim(PrimitiveType.Cube, "Lest_" + kilograms + "kg_Q" + mod.Q, parent, position, Quaternion.identity, new Vector3(size, size * 0.7f, size), paint, true);
            Prim(PrimitiveType.Cylinder, "Poignee", body.transform, new Vector3(0f, 0.65f, 0f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.18f, 0.35f, 0.18f), m.Brass, false);
            foreach (float side in new[] { -1f, 1f })
            {
                TextMeshPro label = Text(body.transform, "Masse", new Vector3(0f, 0f, side * 0.51f), Quaternion.Euler(0f, side > 0f ? 180f : 0f, 0f), new Vector2(0.9f, 0.5f), 3f, ink, false);
                label.text = kilograms + " kg";
                label.fontStyle = FontStyles.Bold;
                label.transform.localScale = new Vector3(1f, 1f / 0.7f, 1f) * 0.1f;
            }
            MakeNetworkGrabbable(body, 0.2f + kilograms * 0.1f);
            Halo(m, body, body);
            var weight = body.AddComponent<BallastWeight>();
            SetFields(weight, ("kilograms", kilograms));
            return weight;
        }
    }
}
