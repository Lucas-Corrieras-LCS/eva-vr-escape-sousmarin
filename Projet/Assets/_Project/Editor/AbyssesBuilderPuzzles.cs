using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Abysses.Interaction;
using Abysses.Level;
using Abysses.UI;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Générateur, partie « énigmes de module » : une énigme par module et par étage, jouable aussi en solo
    /// (les énigmes coopératives des modules vides sont résolues automatiquement, celles-ci non : le joueur seul a
    /// vraiment de quoi chercher). Inspirées de classiques d'escape game : note à lire, code morse, jeu de mémoire.
    /// Toutes sur le mur du fond du module, à hauteur de main, entre les éléments déjà posés.
    /// </summary>
    public static partial class AbyssesBuilder
    {
        static Material paperMaterial, morseOnMaterial;
        static Material[] simonOff, simonOn;

        static void PuzzleMaterials()
        {
            paperMaterial = Lit("M_Paper", new Color(0.86f, 0.83f, 0.74f), 0f, 0.15f, new Color(0.86f, 0.83f, 0.74f) * 0.05f);
            morseOnMaterial = Lit("M_Morse_On", Color.black, 0f, 0.5f, new Color(1f, 0.92f, 0.65f) * 6f);
            simonOff = new Material[SimonConsole.Pads];
            simonOn = new Material[SimonConsole.Pads];
            for (int i = 0; i < SimonConsole.Pads; i++)
            {
                simonOff[i] = Lit("M_Simon_Off_" + i, SymbolColors[i] * 0.3f, 0.1f, 0.6f);
                simonOn[i] = Lit("M_Simon_On_" + i, SymbolColors[i] * 0.5f, 0f, 0.6f, SymbolColors[i] * 3f);
            }
        }

        // ------------------------------------------------------------------ Outils

        /// <summary>
        /// Bouton physique lisible : collerette sombre, chapeau coloré qui s'enfonce, étiquette au-dessus, halo au survol.
        /// Repère local du parent : +Z vers le mur, la face avant est en -Z.
        /// </summary>
        static GameObject PokeButtonObject(Mats m, Transform parent, string name, Vector3 position, Vector2 size, Material cap,
            MonoBehaviour receiver, int index, string label, string labelEn, List<Behaviour> localOnly, float labelSize = 0.32f)
        {
            Prim(PrimitiveType.Cube, name + "_Bezel", parent, position + new Vector3(0f, 0f, -0.005f), Quaternion.identity,
                new Vector3(size.x + 0.03f, size.y + 0.03f, 0.012f), m.Frame, false);
            GameObject button = Prim(PrimitiveType.Cube, name, parent, position + new Vector3(0f, 0f, -0.025f), Quaternion.identity,
                new Vector3(size.x, size.y, 0.03f), cap, true);
            var interactable = button.AddComponent<XRSimpleInteractable>();
            button.AddComponent<XRPokeFilter>();
            Halo(m, button, button, 1.15f);
            AudioSource click = Sfx(button, fx.Click, 0.5f);
            SetFields(button.AddComponent<PokeButton>(), ("receiver", receiver), ("index", index), ("click", click));
            localOnly?.Add(interactable);
            if (!string.IsNullOrEmpty(label))
                Label(parent, label, position + new Vector3(0f, size.y / 2f + 0.035f, -0.03f), labelSize, null, labelEn);
            return button;
        }

        static TextMeshPro Text(Transform parent, string name, Vector3 position, Quaternion rotation, Vector2 box, float size, Color color, bool wrap)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            ((RectTransform)go.transform).sizeDelta = box;
            return tmp;
        }

        /// <summary>Texte sombre posé sur un bouton lumineux (contraste maximal).</summary>
        static void ButtonText(Transform parent, string french, string english, Vector3 position)
        {
            TextMeshPro text = Text(parent, "Texte_Bouton", position, Quaternion.identity, new Vector2(0.3f, 0.06f), 0.34f, new Color(0.02f, 0.05f, 0.06f), false);
            text.text = french;
            text.fontStyle = FontStyles.Bold;
            SetFields(text.gameObject.AddComponent<LocalizedText>(), ("french", french), ("english", english));
        }

        static void LocalOnly(GameObject root, int quadrant, List<Behaviour> components)
        {
            var localOnly = root.AddComponent<LocalModuleOnly>();
            var so = new UnityEditor.SerializedObject(localOnly);
            so.FindProperty("quadrant").intValue = quadrant;
            SetArray(so.FindProperty("components"), components.ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void StatusLamp(Mats m, Transform parent, Vector3 position, PuzzleObjective objective)
        {
            GameObject lamp = Prim(PrimitiveType.Sphere, "Status", parent, position, Quaternion.identity, Vector3.one * 0.05f, m.StatusIdle, false);
            SetFields(lamp.AddComponent<ObjectiveLamp>(), ("objective", objective), ("lamp", lamp.GetComponent<Renderer>()),
                ("idle", m.StatusIdle), ("solved", m.StatusOk));
        }

        // ------------------------------------------------------------------ Étage 1 : disjoncteurs

        static PuzzleObjective BuildBreakerPanel(Mats m, Transform parent, Module mod)
        {
            var root = new GameObject("BreakerPanel_Q" + mod.Q);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(mod.P(-7.16f, 1.3f, -1.6f), mod.Facing(-1f, 0f));
            root.AddComponent<NetworkObject>();
            PuzzleObjective objective = Objective(root, 1, mod.Q, "Disjoncteurs " + QuadrantNames[mod.Q], "Breakers " + QuadrantNamesEn[mod.Q], "breakers");
            var panel = root.AddComponent<BreakerPanel>();
            Transform t = root.transform;

            DeviceMount(m, t, new Vector2(1.04f, 0.78f), 1.3f);
            Prim(PrimitiveType.Cube, "Boitier", t, new Vector3(0f, 0f, -0.02f), Quaternion.identity, new Vector3(0.94f, 0.64f, 0.06f), m.Panel, false);
            Label(t, "DISJONCTEURS", new Vector3(0f, 0.27f, -0.055f), 0.5f, null, "BREAKERS");
            StatusLamp(m, t, new Vector3(0.4f, 0.27f, -0.06f), objective);

            var localOnly = new List<Behaviour>();
            var levers = new Transform[BreakerPanel.Count];
            var lamps = new Renderer[BreakerPanel.Count];
            string[] fr = { "POMPES", "SONAR", "RADIO", "CHAUFFAGE", "PROJECT." };
            string[] en = { "PUMPS", "SONAR", "RADIO", "HEATING", "FLOODS" };
            for (int i = 0; i < BreakerPanel.Count; i++)
            {
                float x = -0.36f + i * 0.18f;
                Prim(PrimitiveType.Cube, "Fente", t, new Vector3(x, 0f, -0.052f), Quaternion.identity, new Vector3(0.07f, 0.2f, 0.01f), m.Frame, false);
                var pivot = new GameObject("Manette_" + i).transform;
                pivot.SetParent(t, false);
                pivot.localPosition = new Vector3(x, 0f, -0.055f);
                // Manette qui sort du panneau (vers -Z) et bascule vers le haut (armé) ou le bas (coupé).
                GameObject handle = Prim(PrimitiveType.Cube, "Poignee", pivot, new Vector3(0f, 0f, -0.05f), Quaternion.identity, new Vector3(0.045f, 0.04f, 0.1f), m.Key, true);
                Prim(PrimitiveType.Cube, "Bout", handle.transform, new Vector3(0f, 0f, -0.45f), Quaternion.identity, new Vector3(1.05f, 1.05f, 0.2f), m.Symbols[0], false);
                var interactable = handle.AddComponent<XRSimpleInteractable>();
                handle.AddComponent<XRPokeFilter>();
                Halo(m, handle, handle, 1.25f);
                SetFields(handle.AddComponent<PokeButton>(), ("receiver", panel), ("index", i), ("travel", 0f));
                localOnly.Add(interactable);
                levers[i] = pivot;
                lamps[i] = Prim(PrimitiveType.Sphere, "Voyant_" + i, t, new Vector3(x, 0.17f, -0.055f), Quaternion.identity, Vector3.one * 0.035f, m.SlotEmpty, false).GetComponent<Renderer>();
                Label(t, fr[i], new Vector3(x, -0.17f, -0.055f), 0.26f, null, en[i]);
            }
            Label(t, "HAUT = ARMÉ   ·   BAS = COUPÉ", new Vector3(0f, -0.27f, -0.055f), 0.28f, null, "UP = ON   ·   DOWN = OFF");
            LocalOnly(root, mod.Q, localOnly);

            // Étincelles au court-circuit.
            var sparksGo = new GameObject("Etincelles");
            sparksGo.transform.SetParent(t, false);
            sparksGo.transform.localPosition = new Vector3(0f, 0f, -0.08f);
            var sparks = sparksGo.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.025f);
            main.gravityModifier = 1f;
            main.maxParticles = 60;
            var emission = sparks.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 45) });
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.8f, 0.3f, 0.02f);
            var sparkRenderer = sparksGo.GetComponent<ParticleSystemRenderer>();
            sparkRenderer.sharedMaterial = m.GlowAngler;
            sparkRenderer.shadowCastingMode = ShadowCastingMode.Off;

            // Note du chef mécanicien sur le bureau du module, sous une lampe de bureau.
            TextMeshPro note = BuildDeskNote(m, parent, mod);

            SetFields(panel, ("objective", objective), ("note", note), ("lampOff", m.SlotEmpty), ("lampOn", m.StatusOk),
                ("clickSound", Sfx(root, fx.Key, 0.6f)), ("tripSound", Sfx(root, fx.Spark, 1f)), ("successSound", Sfx(root, fx.Success, 0.9f)), ("sparks", sparks));
            var so = new UnityEditor.SerializedObject(panel);
            SetArray(so.FindProperty("levers"), levers);
            SetArray(so.FindProperty("lamps"), lamps);
            so.ApplyModifiedPropertiesWithoutUndo();
            return objective;
        }

        /// <summary>Feuille posée à plat sur le bureau (même repère que le bureau de l'habitat), éclairée par une lampe.</summary>
        static TextMeshPro BuildDeskNote(Mats m, Transform parent, Module mod)
        {
            Transform desk = Group("Note_Q" + mod.Q, parent, mod.P(-5f, 0f, mod.Vz * 3.36f), mod.Facing(0f, mod.Vz));
            // Feuille légèrement de travers, côté gauche du plateau, devant l'écran.
            Quaternion paper = Quaternion.Euler(0f, 8f, 0f);
            Static(Prim(PrimitiveType.Cube, "Feuille", desk, new Vector3(-0.42f, 0.789f, -0.1f), paper, new Vector3(0.3f, 0.002f, 0.38f), paperMaterial, false));
            TextMeshPro text = Text(desk, "Texte", new Vector3(-0.42f, 0.7915f, -0.1f), paper * Quaternion.Euler(90f, 0f, 0f), new Vector2(0.27f, 0.35f), 0.2f, new Color(0.12f, 0.1f, 0.08f), true);
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.1f;
            text.fontSizeMax = 0.2f;

            // Lampe de bureau : socle, bras, abat-jour, ampoule chaude, lumière bakée (présente dans les deux éclairages).
            var lamp = new GameObject("LampeBureau").transform;
            lamp.SetParent(desk, false);
            lamp.localPosition = new Vector3(-0.68f, 0.785f, 0.12f);
            Static(Prim(PrimitiveType.Cylinder, "Socle", lamp, new Vector3(0f, 0.01f, 0f), Quaternion.identity, new Vector3(0.12f, 0.01f, 0.12f), m.Frame, false));
            Static(Prim(PrimitiveType.Cylinder, "Bras", lamp, new Vector3(0.05f, 0.18f, -0.03f), Quaternion.Euler(-20f, 0f, -18f), new Vector3(0.015f, 0.18f, 0.015f), m.Brass, false));
            Static(Prim(PrimitiveType.Cylinder, "AbatJour", lamp, new Vector3(0.14f, 0.34f, -0.1f), Quaternion.Euler(-25f, 0f, -30f), new Vector3(0.12f, 0.05f, 0.12f), m.PipeYellow, false));
            Static(Prim(PrimitiveType.Sphere, "Ampoule", lamp, new Vector3(0.15f, 0.31f, -0.11f), Quaternion.identity, Vector3.one * 0.04f, m.WarmBulb, false));
            var lightGo = new GameObject("Lumiere");
            lightGo.transform.SetParent(lamp, false);
            lightGo.transform.localPosition = new Vector3(0.2f, 0.25f, -0.18f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.5f);
            light.range = 1.4f;
            light.intensity = 1.5f;
            light.lightmapBakeType = LightmapBakeType.Baked;
            light.shadows = LightShadows.Soft;
            return text;
        }

        // ------------------------------------------------------------------ Étage 2 : morse

        static PuzzleObjective BuildMorsePuzzle(Mats m, Transform parent, Module mod)
        {
            float s = mod.Vz;

            // Balise de détresse dehors, 3 m devant le hublot du bout du module (côté partenaire).
            var beaconGo = new GameObject("MorseBeacon_Q" + mod.Q);
            beaconGo.transform.SetParent(parent, false);
            beaconGo.transform.position = mod.P(OceanWindowX[1], 0f, s * 6.8f);
            beaconGo.AddComponent<NetworkObject>();
            var beacon = beaconGo.AddComponent<MorseBeacon>();
            Transform b = beaconGo.transform;
            // Balise de détresse posée sur le fond, à côté de la station.
            Prim(PrimitiveType.Cylinder, "Socle", b, new Vector3(0f, 0.6f, 0f), Quaternion.identity, new Vector3(0.6f, 0.4f, 0.6f), m.PipeRed, false);
            Prim(PrimitiveType.Cylinder, "Mat", b, new Vector3(0f, 1.2f, 0f), Quaternion.identity, new Vector3(0.06f, 0.3f, 0.06f), m.Frame, false);
            Renderer lamp = Prim(PrimitiveType.Sphere, "Feu", b, new Vector3(0f, 1.55f, 0f), Quaternion.identity, Vector3.one * 0.3f, m.SlotEmpty, false).GetComponent<Renderer>();

            // Cadenas à molettes, sur le mur du fond.
            var root = new GameObject("MorseLock_Q" + mod.Q);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(mod.P(-7.16f, 1.3f, -0.75f), mod.Facing(-1f, 0f));
            root.AddComponent<NetworkObject>();
            PuzzleObjective objective = Objective(root, 2, mod.Q, "Cadenas morse " + QuadrantNames[mod.Q], "Morse lock " + QuadrantNamesEn[mod.Q], "morse");
            var combination = root.AddComponent<CombinationLock>();
            Transform t = root.transform;
            DeviceMount(m, t, new Vector2(0.62f, 0.74f), 1.3f);
            Prim(PrimitiveType.Cube, "Boitier", t, new Vector3(0f, 0f, -0.02f), Quaternion.identity, new Vector3(0.52f, 0.6f, 0.06f), m.Panel, false);
            Label(t, "VANNE D'URGENCE", new Vector3(0f, 0.26f, -0.055f), 0.36f, null, "EMERGENCY VALVE");
            Renderer status = Prim(PrimitiveType.Sphere, "Status", t, new Vector3(0.21f, 0.26f, -0.06f), Quaternion.identity, Vector3.one * 0.045f, m.StatusIdle, false).GetComponent<Renderer>();

            var localOnly = new List<Behaviour>();
            var wheels = new TMP_Text[MorseBeacon.Digits];
            for (int w = 0; w < MorseBeacon.Digits; w++)
            {
                float x = -0.15f + w * 0.15f;
                Prim(PrimitiveType.Cylinder, "Molette_" + w, t, new Vector3(x, 0.02f, -0.06f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.11f, 0.05f, 0.11f), m.Brass, false);
                Prim(PrimitiveType.Cube, "Fenetre_" + w, t, new Vector3(x, 0.02f, -0.113f), Quaternion.identity, new Vector3(0.07f, 0.08f, 0.004f), m.SlotEmpty, false);
                wheels[w] = Text(t, "Chiffre_" + w, new Vector3(x, 0.02f, -0.117f), Quaternion.identity, new Vector2(0.07f, 0.08f), 0.7f, new Color(1f, 0.9f, 0.6f), false);
                wheels[w].text = "0";
                PokeButtonObject(m, t, "Plus_" + w, new Vector3(x, 0.135f, -0.03f), new Vector2(0.07f, 0.05f), m.Key, combination, w, "+", "+", localOnly, 0.4f);
                PokeButtonObject(m, t, "Moins_" + w, new Vector3(x, -0.1f, -0.03f), new Vector2(0.07f, 0.05f), m.Key, combination, MorseBeacon.Digits + w, null, null, localOnly);
                Label(t, "-", new Vector3(x, -0.155f, -0.055f), 0.45f);
            }
            PokeButtonObject(m, t, "Valider", new Vector3(0f, -0.235f, -0.03f), new Vector2(0.3f, 0.06f), m.EmitCyan, combination, CombinationLock.ValidateIndex, null, null, localOnly);
            ButtonText(t, "VALIDER", "CONFIRM", new Vector3(0f, -0.235f, -0.072f));
            LocalOnly(root, mod.Q, localOnly);

            // Haut-parleur radio au-dessus du cadenas : il bipe avec la balise.
            Prim(PrimitiveType.Cube, "HautParleur", t, new Vector3(0f, 0.44f, -0.03f), Quaternion.identity, new Vector3(0.26f, 0.12f, 0.06f), m.Frame, false);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cube, "Grille", t, new Vector3(0f, 0.405f + i * 0.025f, -0.061f), Quaternion.identity, new Vector3(0.2f, 0.008f, 0.004f), m.Key, false);
            AudioSource beep = Sfx(root, fx.MorseTone, 0.35f);
            beep.loop = true;

            // Table du morse à côté du cadenas.
            Transform table = Group("TableMorse_Q" + mod.Q, parent, mod.P(-7.17f, 1.42f, 0.15f), mod.Facing(-1f, 0f));
            Static(Prim(PrimitiveType.Cube, "Plaque", table, new Vector3(0f, 0f, 0.005f), Quaternion.identity, new Vector3(0.46f, 0.66f, 0.01f), paperMaterial, false));
            Static(AbyssesArt.MeshObject("Cadre", table, AbyssesArt.FrameMesh(0.5f, 0.7f, 0.03f, 0.03f, 0.02f), Vector3.zero, Quaternion.identity, Vector3.one, m.Frame));
            TextMeshPro rows = Text(table, "Table", new Vector3(0f, -0.02f, -0.002f), Quaternion.identity, new Vector2(0.42f, 0.6f), 0.4f, new Color(0.1f, 0.09f, 0.08f), false);
            rows.alignment = TextAlignmentOptions.Left;
            string[] morse = { "— — — — —", "· — — — —", "· · — — —", "· · · — —", "· · · · —", "· · · · ·", "— · · · ·", "— — · · ·", "— — — · ·", "— — — — ·" };
            var sb = new System.Text.StringBuilder("<b>   CODE MORSE</b>\n");
            for (int d = 0; d < 10; d++) sb.Append("<b>").Append(d).Append("</b>     ").Append(morse[d]).Append('\n');
            rows.text = sb.ToString();

            SetFields(beacon, ("lamp", lamp), ("lampOff", m.SlotEmpty), ("lampOn", morseOnMaterial), ("beep", beep));
            SetFields(combination, ("objective", objective), ("beacon", beacon), ("status", status), ("idleMaterial", m.StatusIdle),
                ("successMaterial", m.StatusOk), ("errorMaterial", m.StatusError), ("errorSound", Sfx(root, fx.Error, 0.8f)), ("successSound", Sfx(root, fx.Success, 0.9f)));
            var so = new UnityEditor.SerializedObject(combination);
            SetArray(so.FindProperty("wheels"), wheels);
            so.ApplyModifiedPropertiesWithoutUndo();
            return objective;
        }

        // ------------------------------------------------------------------ Étage 3 : séquence de mémoire

        static PuzzleObjective BuildSimonConsole(Mats m, Transform parent, Module mod)
        {
            var root = new GameObject("SonarConsole_Q" + mod.Q);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(mod.P(-7.16f, 1.25f, mod.Vz * 0.65f), mod.Facing(-1f, 0f));
            root.AddComponent<NetworkObject>();
            PuzzleObjective objective = Objective(root, 3, mod.Q, "Console sonar " + QuadrantNames[mod.Q], "Sonar console " + QuadrantNamesEn[mod.Q], "simon");
            var console = root.AddComponent<SimonConsole>();
            Transform t = root.transform;
            DeviceMount(m, t, new Vector2(0.84f, 0.84f), 1.25f);
            Prim(PrimitiveType.Cube, "Boitier", t, new Vector3(0f, 0f, -0.02f), Quaternion.identity, new Vector3(0.74f, 0.7f, 0.06f), m.Panel, false);
            Label(t, "CALIBRAGE SONAR", new Vector3(0f, 0.31f, -0.055f), 0.36f, null, "SONAR CALIBRATION");
            StatusLamp(m, t, new Vector3(0.32f, 0.31f, -0.06f), objective);
            Prim(PrimitiveType.Cube, "Ecran", t, new Vector3(0f, 0.2f, -0.052f), Quaternion.identity, new Vector3(0.56f, 0.13f, 0.004f), m.SlotEmpty, false);
            TextMeshPro screen = Text(t, "Texte", new Vector3(0f, 0.2f, -0.056f), Quaternion.identity, new Vector2(0.54f, 0.12f), 0.3f, new Color(0.45f, 0.95f, 1f), true);

            var localOnly = new List<Behaviour>();
            var pads = new Renderer[SimonConsole.Pads];
            for (int i = 0; i < SimonConsole.Pads; i++)
            {
                float x = i % 2 == 0 ? -0.12f : 0.12f;
                float y = i < 2 ? 0.02f : -0.17f;
                GameObject pad = PokeButtonObject(m, t, "Touche_" + i, new Vector3(x, y, -0.03f), new Vector2(0.17f, 0.15f), simonOff[i], console, i, null, null, localOnly);
                // Forme + couleur (lisible sans distinguer les couleurs).
                Prim(PrimitiveType.Quad, "Forme", pad.transform, new Vector3(0f, 0f, -0.51f), Quaternion.identity, new Vector3(0.5f, 0.55f, 1f), m.Glyphs[i], false);
                pads[i] = pad.GetComponent<Renderer>();
            }
            PokeButtonObject(m, t, "Ecouter", new Vector3(0f, -0.29f, -0.03f), new Vector2(0.24f, 0.055f), m.EmitCyan, console, SimonConsole.ListenIndex, null, null, localOnly);
            ButtonText(t, "ÉCOUTER", "LISTEN", new Vector3(0f, -0.29f, -0.072f));
            LocalOnly(root, mod.Q, localOnly);

            AudioSource tone = Sfx(root, fx.SimonTone, 0.8f);
            SetFields(console, ("objective", objective), ("screen", screen), ("tone", tone),
                ("errorSound", Sfx(root, fx.Error, 0.8f)), ("successSound", Sfx(root, fx.Success, 0.9f)));
            var so = new UnityEditor.SerializedObject(console);
            SetArray(so.FindProperty("pads"), pads);
            SetArray(so.FindProperty("padOff"), simonOff);
            SetArray(so.FindProperty("padOn"), simonOn);
            so.ApplyModifiedPropertiesWithoutUndo();
            return objective;
        }
    }
}
