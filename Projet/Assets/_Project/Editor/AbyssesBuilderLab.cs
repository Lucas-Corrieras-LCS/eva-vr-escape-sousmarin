using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using Abysses.Interaction;
using Abysses.Level;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Générateur : antidote du labo (étage 2, <see cref="AntidoteMixer"/>) et code frappé sur la coque (étage 1, <see cref="KnockCode"/>).
    /// </summary>
    public static partial class AbyssesBuilder
    {
        static Material[] reagentMaterials;

        static List<PuzzleObjective> BuildAntidote(Mats m, Transform parent, Module[] modules)
        {
            reagentMaterials = new[]
            {
                Lit("M_Reactif_Rouge", new Color(0.6f, 0.05f, 0.05f), 0f, 0.9f, new Color(1f, 0.15f, 0.1f) * 0.8f),
                Lit("M_Reactif_Bleu", new Color(0.05f, 0.12f, 0.6f), 0f, 0.9f, new Color(0.2f, 0.4f, 1f) * 0.8f),
                Lit("M_Reactif_Vert", new Color(0.05f, 0.5f, 0.12f), 0f, 0.9f, new Color(0.2f, 1f, 0.35f) * 0.8f),
                Lit("M_Reactif_Jaune", new Color(0.7f, 0.6f, 0.05f), 0f, 0.9f, new Color(1f, 0.85f, 0.2f) * 0.8f),
            };
            string[] colorNames = { "ROUGE", "BLEU", "VERT", "JAUNE" };
            string[] colorNamesEn = { "RED", "BLUE", "GREEN", "YELLOW" };
            var mixers = new AntidoteMixer[4];
            var objectives = new List<PuzzleObjective>();

            foreach (Module mod in modules)
            {
                var root = new GameObject("Melangeur_Q" + mod.Q);
                root.transform.SetParent(parent, false);
                root.transform.SetPositionAndRotation(mod.P(2.5f, 1.3f, mod.Vz * 3.62f), mod.Facing(0f, mod.Vz));
                root.AddComponent<NetworkObject>();
                PuzzleObjective objective = Objective(root, 2, mod.Q, "Antidote " + QuadrantNames[mod.Q], "Antidote " + QuadrantNamesEn[mod.Q], "antidote");
                var mixer = root.AddComponent<AntidoteMixer>();
                Transform t = root.transform;
                DeviceMount(m, t, new Vector2(0.86f, 0.92f), 1.3f);
                Prim(PrimitiveType.Cube, "Boitier", t, new Vector3(0f, 0f, -0.02f), Quaternion.identity, new Vector3(0.8f, 0.86f, 0.06f), m.Panel, false);
                Label(t, "MÉLANGEUR D'ANTIDOTE", new Vector3(-0.04f, 0.38f, -0.055f), 0.34f, null, "ANTIDOTE MIXER");
                Renderer lamp = Prim(PrimitiveType.Sphere, "Status", t, new Vector3(0.33f, 0.38f, -0.06f), Quaternion.identity, Vector3.one * 0.045f, m.StatusIdle, false).GetComponent<Renderer>();
                Prim(PrimitiveType.Cube, "Etagere", t, new Vector3(0f, 0.02f, -0.1f), Quaternion.identity, new Vector3(0.74f, 0.02f, 0.14f), m.Frame, false);

                var localOnly = new List<Behaviour>();
                var doses = new TMP_Text[AntidoteMixer.Flasks];
                var liquids = new Transform[AntidoteMixer.Flasks];
                for (int i = 0; i < AntidoteMixer.Flasks; i++)
                {
                    float x = -0.27f + i * 0.18f;
                    // Flacon : verre, liquide (pivot au fond : il baisse à chaque dose), bouchon, étiquette de couleur.
                    var pivot = new GameObject("Liquide_" + i).transform;
                    pivot.SetParent(t, false);
                    pivot.localPosition = new Vector3(x, 0.035f, -0.1f);
                    pivot.localScale = new Vector3(0.075f, 0.1f, 0.075f);
                    Prim(PrimitiveType.Cylinder, "Niveau", pivot, new Vector3(0f, 1f, 0f), Quaternion.identity, Vector3.one, reagentMaterials[i], false);
                    liquids[i] = pivot;
                    Prim(PrimitiveType.Cylinder, "Verre", t, new Vector3(x, 0.15f, -0.1f), Quaternion.identity, new Vector3(0.09f, 0.115f, 0.09f), m.Glass, false);
                    Prim(PrimitiveType.Cylinder, "Bouchon", t, new Vector3(x, 0.275f, -0.1f), Quaternion.identity, new Vector3(0.05f, 0.015f, 0.05f), m.Rubber, false);
                    Label(t, colorNames[i], new Vector3(x, -0.03f, -0.055f), 0.22f, null, colorNamesEn[i]);
                    doses[i] = Text(t, "Doses_" + i, new Vector3(x, -0.09f, -0.056f), Quaternion.identity, new Vector2(0.1f, 0.05f), 0.4f, new Color(1f, 0.85f, 0.5f), false);
                    doses[i].text = "0";
                    PokeButtonObject(m, t, "Dose_" + i, new Vector3(x, -0.16f, -0.03f), new Vector2(0.12f, 0.06f), m.Key, mixer, i, null, null, localOnly);
                    Text(t, "TexteDose_" + i, new Vector3(x, -0.16f, -0.072f), Quaternion.identity, new Vector2(0.12f, 0.05f), 0.26f, Color.white, false).text = "DOSE";
                }
                PokeButtonObject(m, t, "Vider", new Vector3(-0.22f, -0.3f, -0.03f), new Vector2(0.22f, 0.07f), m.PipeRed, mixer, AntidoteMixer.EmptyIndex, null, null, localOnly);
                ButtonText(t, "VIDER", "EMPTY", new Vector3(-0.22f, -0.3f, -0.072f));
                PokeButtonObject(m, t, "Melanger", new Vector3(0.14f, -0.3f, -0.03f), new Vector2(0.36f, 0.07f), m.EmitCyan, mixer, AntidoteMixer.MixIndex, null, null, localOnly);
                ButtonText(t, "MÉLANGER", "MIX", new Vector3(0.14f, -0.3f, -0.072f));
                LocalOnly(root, mod.Q, localOnly);

                ParticleSystem foam = SteamBurst(m, t, new Vector3(0f, 0.2f, -0.12f));
                SetFields(mixer, ("objective", objective), ("legendSource", mod.Q == 0 ? null : mixers[0]), ("statusLamp", lamp), ("lampIdle", m.StatusIdle),
                    ("lampOk", m.StatusOk), ("lampError", m.StatusError), ("doseSound", Sfx(root, fx.Drip, 0.8f)), ("failSound", Sfx(root, fx.SteamHiss, 0.8f)),
                    ("successSound", Sfx(root, fx.Success, 0.9f)), ("foam", foam));
                var so = new UnityEditor.SerializedObject(mixer);
                SetArray(so.FindProperty("doseTexts"), doses);
                SetArray(so.FindProperty("liquids"), liquids);
                so.ApplyModifiedPropertiesWithoutUndo();
                mixers[mod.Q] = mixer;
                objectives.Add(objective);
            }

            // Recettes (chez un autre plongeur) sur le mur du fond, et tableau des réactifs à l'encre UV au labo ORANGE.
            foreach (Module mod in modules)
            {
                Transform card = Group("Recette_Q" + mod.Q, parent, mod.P(-7.16f, 1.4f, -2.0f), mod.Facing(-1f, 0f));
                DeviceMount(m, card, new Vector2(0.56f, 0.56f), 1.4f);
                Prim(PrimitiveType.Cube, "Fiche", card, new Vector3(0f, 0f, -0.02f), Quaternion.identity, new Vector3(0.5f, 0.5f, 0.02f), paperMaterial, false);
                TextMeshPro text = Text(card, "Texte", new Vector3(0f, 0f, -0.032f), Quaternion.identity, new Vector2(0.46f, 0.46f), 0.2f, new Color(0.1f, 0.09f, 0.08f), true);
                text.alignment = TextAlignmentOptions.TopLeft;
                text.enableAutoSizing = true;
                text.fontSizeMin = 0.1f;
                text.fontSizeMax = 0.2f;
                Private(card.gameObject, mod.Q, text.GetComponent<Renderer>());
                var recipe = card.gameObject.AddComponent<AntidoteRecipe>();
                SetFields(recipe, ("quadrant", mod.Q), ("shift", 1), ("text", text));
                var so = new UnityEditor.SerializedObject(recipe);
                SetArray(so.FindProperty("mixers"), mixers);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            Module orange = modules[0];
            Transform chart = Group("TableauReactifs_UV", parent, orange.P(-7.16f, 1.4f, -3.0f), orange.Facing(-1f, 0f));
            Prim(PrimitiveType.Cube, "Plaque", chart, new Vector3(0f, 0f, 0.005f), Quaternion.identity, new Vector3(0.5f, 0.42f, 0.01f), m.HullDark, false);
            Label(chart, "ENCRE UV", new Vector3(0f, 0.24f, -0.006f), 0.24f, null, "UV INK");
            TextMeshPro chartText = Text(chart, "Texte", new Vector3(0f, 0f, -0.008f), Quaternion.identity, new Vector2(0.46f, 0.38f), 0.22f, new Color(0.75f, 0.45f, 1f), true);
            chartText.enableAutoSizing = true;
            chartText.fontSizeMin = 0.1f;
            chartText.fontSizeMax = 0.22f;
            var reveal = chart.gameObject.AddComponent<UVReveal>();
            var rso = new UnityEditor.SerializedObject(reveal);
            SetArray(rso.FindProperty("hiddenRenderers"), chartText.GetComponent<Renderer>());
            rso.ApplyModifiedPropertiesWithoutUndo();
            SetFields(chart.gameObject.AddComponent<AntidoteChart>(), ("reference", mixers[0]), ("text", chartText));
            return objectives;
        }

        /// <summary>Code frappé : deux objectifs (un par frappeur) et un pupitre d'écoute au-dessus de chaque plaque.</summary>
        static PuzzleObjective[] BuildKnockCode(Mats m, Transform parent, KnockWall wall, Module a, Module b)
        {
            var root = new GameObject("CodeFrappe_Q" + a.Q + "_Q" + b.Q);
            root.transform.SetParent(parent, false);
            root.AddComponent<NetworkObject>();
            var code = root.AddComponent<KnockCode>();
            var objectives = new PuzzleObjective[2];
            var texts = new TMP_Text[2];
            var sounds = new AudioSource[2];
            Module[] sides = { a, b };
            for (int side = 0; side < 2; side++)
            {
                Module knocker = sides[side], listener = sides[1 - side];
                var holder = new GameObject("Frappe_Q" + knocker.Q);
                holder.transform.SetParent(root.transform, false);
                objectives[side] = Objective(holder, 1, knocker.Q, "Code frappé par " + QuadrantNames[knocker.Q], "Code knocked by " + QuadrantNamesEn[knocker.Q], "knock");

                // Pupitre d'écoute chez celui qui ÉCOUTE (il lit le rythme et le décrit au frappeur).
                Transform panel = Group("PupitreEcoute_Q" + listener.Q, parent, listener.P(7.16f, 1.8f, 3.1f), listener.Facing(1f, 0f));
                DeviceMount(m, panel, new Vector2(0.62f, 0.34f), 1.8f);
                Prim(PrimitiveType.Cube, "Boitier", panel, new Vector3(0f, 0f, -0.02f), Quaternion.identity, new Vector3(0.56f, 0.28f, 0.06f), m.Panel, false);
                Prim(PrimitiveType.Cube, "Ecran", panel, new Vector3(0f, 0f, -0.052f), Quaternion.identity, new Vector3(0.52f, 0.24f, 0.004f), m.SlotEmpty, false);
                TextMeshPro text = Text(panel, "Texte", new Vector3(0f, 0f, -0.056f), Quaternion.identity, new Vector2(0.5f, 0.22f), 0.2f, new Color(0.55f, 1f, 0.8f), true);
                text.enableAutoSizing = true;
                text.fontSizeMin = 0.1f;
                text.fontSizeMax = 0.2f;
                Private(panel.gameObject, listener.Q, text.GetComponent<Renderer>());
                // Le pupitre du côté A affiche le rythme que B doit frapper : indice de pupitre = côté qui écoute.
                texts[1 - side] = text;
                sounds[side] = Sfx(panel.gameObject, fx.Success, 0.8f);
            }
            SetFields(code, ("wall", wall));
            var so = new UnityEditor.SerializedObject(code);
            SetArray(so.FindProperty("objectives"), objectives);
            SetArray(so.FindProperty("panelTexts"), texts);
            SetArray(so.FindProperty("successSounds"), sounds);
            so.ApplyModifiedPropertiesWithoutUndo();
            return objectives;
        }
    }
}
