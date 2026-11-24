using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Abysses.Interaction;
using Abysses.Level;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Générateur, étage 1 : « Manuel de désamorçage » (<see cref="WiringPanel"/>) — tableau de fils sur le mur du fond,
    /// manuel (règles du tableau d'un autre plongeur) sur le mur extérieur.
    /// </summary>
    public static partial class AbyssesBuilder
    {
        static Material[] wireMaterials;

        static void WiringMaterials()
        {
            wireMaterials = new[]
            {
                Lit("M_Fil_Rouge", new Color(0.8f, 0.07f, 0.05f), 0.1f, 0.6f),
                Lit("M_Fil_Bleu", new Color(0.08f, 0.25f, 0.85f), 0.1f, 0.6f),
                Lit("M_Fil_Jaune", new Color(0.95f, 0.78f, 0.08f), 0.1f, 0.6f),
                Lit("M_Fil_Blanc", new Color(0.88f, 0.88f, 0.86f), 0.1f, 0.6f),
                Lit("M_Fil_Noir", new Color(0.04f, 0.04f, 0.05f), 0.1f, 0.6f),
            };
        }

        static WiringPanel BuildWiringPanel(Mats m, Transform parent, Module mod)
        {
            if (wireMaterials == null || wireMaterials[0] == null) WiringMaterials();
            var root = new GameObject("Derivation_Q" + mod.Q);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(mod.P(-7.16f, 1.3f, -1.6f), mod.Facing(-1f, 0f));
            root.AddComponent<NetworkObject>();
            Objective(root, 1, mod.Q, "Tableau de dérivation " + QuadrantNames[mod.Q], "Bypass panel " + QuadrantNamesEn[mod.Q], "wiring");
            var panel = root.AddComponent<WiringPanel>();
            Transform t = root.transform;

            DeviceMount(m, t, new Vector2(0.98f, 0.92f), 1.3f);
            Prim(PrimitiveType.Cube, "Boitier", t, new Vector3(0f, 0f, -0.02f), Quaternion.identity, new Vector3(0.9f, 0.84f, 0.06f), m.Panel, false);
            Label(t, "TABLEAU DE DÉRIVATION", new Vector3(-0.08f, 0.37f, -0.055f), 0.36f, null, "BYPASS PANEL");
            var progress = new Renderer[WiringPanel.Rounds];
            for (int i = 0; i < progress.Length; i++)
                progress[i] = Prim(PrimitiveType.Sphere, "Etape_" + i, t, new Vector3(0.27f + i * 0.06f, 0.37f, -0.06f), Quaternion.identity, Vector3.one * 0.04f, m.SlotEmpty, false).GetComponent<Renderer>();

            // Voyant et numéro de série.
            Prim(PrimitiveType.Cylinder, "Culot_Voyant", t, new Vector3(-0.33f, 0.26f, -0.055f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.08f, 0.01f, 0.08f), m.Frame, false);
            Renderer indicator = Prim(PrimitiveType.Sphere, "Voyant", t, new Vector3(-0.33f, 0.26f, -0.065f), Quaternion.identity, new Vector3(0.06f, 0.06f, 0.04f), m.SlotEmpty, false).GetComponent<Renderer>();
            Label(t, "VOYANT", new Vector3(-0.33f, 0.2f, -0.055f), 0.22f, null, "LIGHT");
            Prim(PrimitiveType.Cube, "Plaque_Serie", t, new Vector3(0.12f, 0.26f, -0.052f), Quaternion.identity, new Vector3(0.5f, 0.06f, 0.004f), m.Brass, false);
            TextMeshPro serial = Text(t, "Serie", new Vector3(0.12f, 0.26f, -0.056f), Quaternion.identity, new Vector2(0.48f, 0.06f), 0.3f, new Color(0.1f, 0.08f, 0.05f), false);
            serial.fontStyle = FontStyles.Bold;

            // 6 emplacements de fils, entre deux bornes ; on « coupe » un fil en le touchant.
            var localOnly = new List<Behaviour>();
            var wires = new Renderer[WiringPanel.Slots];
            var stubs = new GameObject[WiringPanel.Slots];
            var stubRenderers = new Renderer[WiringPanel.Slots];
            for (int i = 0; i < WiringPanel.Slots; i++)
            {
                float y = 0.12f - i * 0.072f;
                Label(t, (i + 1).ToString(), new Vector3(-0.39f, y, -0.055f), 0.32f);
                foreach (float x in new[] { -0.31f, 0.31f })
                    Prim(PrimitiveType.Cylinder, "Borne", t, new Vector3(x, y, -0.06f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.035f, 0.02f, 0.035f), m.Brass, false);
                GameObject wire = Prim(PrimitiveType.Capsule, "Fil_" + i, t, new Vector3(0f, y, -0.07f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.024f, 0.3f, 0.024f), wireMaterials[i % wireMaterials.Length], false);
                // Zone de toucher plus large que le fil (2,5 cm de diamètre, c'est trop fin pour le doigt).
                wire.AddComponent<BoxCollider>().size = new Vector3(2.6f, 1.9f, 2.6f);
                var interactable = wire.AddComponent<XRSimpleInteractable>();
                wire.AddComponent<XRPokeFilter>();
                Halo(m, wire, wire, 1.4f);
                SetFields(wire.AddComponent<PokeButton>(), ("receiver", panel), ("index", i), ("travel", 0f));
                localOnly.Add(interactable);
                wires[i] = wire.GetComponent<Renderer>();

                var stub = new GameObject("FilCoupe_" + i);
                stub.transform.SetParent(t, false);
                Renderer left = Prim(PrimitiveType.Capsule, "Bout", stub.transform, new Vector3(-0.24f, y, -0.07f), Quaternion.Euler(0f, 0f, 80f), new Vector3(0.024f, 0.08f, 0.024f), wireMaterials[0], false).GetComponent<Renderer>();
                Prim(PrimitiveType.Capsule, "Bout", stub.transform, new Vector3(0.24f, y - 0.01f, -0.07f), Quaternion.Euler(0f, 0f, 100f), new Vector3(0.024f, 0.08f, 0.024f), wireMaterials[0], false);
                stub.SetActive(false);
                stubs[i] = stub;
                stubRenderers[i] = left;
            }
            Label(t, "TOUCHER UN FIL = LE COUPER", new Vector3(0f, -0.37f, -0.055f), 0.24f, null, "TOUCH A WIRE = CUT IT");
            LocalOnly(root, mod.Q, localOnly);

            ParticleSystem sparks = SparkBurst(m, t, new Vector3(0f, -0.05f, -0.09f));
            SetFields(panel, ("objective", root.GetComponent<PuzzleObjective>()), ("indicator", indicator), ("indicatorOn", m.EmitAmber), ("indicatorOff", m.SlotEmpty),
                ("serialText", serial), ("progressOff", m.SlotEmpty), ("progressOn", m.StatusOk),
                ("cutSound", Sfx(root, fx.Click, 0.8f)), ("shortSound", Sfx(root, fx.Spark, 1f)), ("successSound", Sfx(root, fx.Success, 0.9f)), ("sparks", sparks));
            var so = new UnityEditor.SerializedObject(panel);
            SetArray(so.FindProperty("wires"), wires);
            SetArray(so.FindProperty("cutStubs"), stubs);
            SetArray(so.FindProperty("cutStubRenderers"), stubRenderers);
            SetArray(so.FindProperty("wireMaterials"), wireMaterials);
            SetArray(so.FindProperty("progressLamps"), progress);
            so.ApplyModifiedPropertiesWithoutUndo();
            return panel;
        }

        /// <summary>Manuel de désamorçage, mur extérieur : écran texte lisible du seul occupant du module.</summary>
        static void BuildWiringManual(Mats m, Transform parent, Module mod, WiringPanel[] panels, int shift)
        {
            var root = new GameObject("Manuel_Q" + mod.Q);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(mod.P(RelayWallX, 1.35f, mod.Vz * 3.62f), mod.Facing(0f, mod.Vz));
            Transform t = root.transform;
            DeviceMount(m, t, new Vector2(0.84f, 0.98f), 1.35f);
            Prim(PrimitiveType.Cube, "Boitier", t, new Vector3(0f, 0f, -0.02f), Quaternion.identity, new Vector3(0.78f, 0.92f, 0.06f), m.Panel, false);
            Label(t, "MANUEL DU CHEF MÉCANICIEN", new Vector3(0f, 0.41f, -0.055f), 0.3f, null, "CHIEF ENGINEER'S MANUAL");
            Prim(PrimitiveType.Cube, "Page", t, new Vector3(0f, -0.03f, -0.052f), Quaternion.identity, new Vector3(0.72f, 0.8f, 0.004f), paperMaterial, false);
            TextMeshPro text = Text(t, "Texte", new Vector3(0f, -0.03f, -0.056f), Quaternion.identity, new Vector2(0.68f, 0.76f), 0.2f, new Color(0.1f, 0.09f, 0.08f), true);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.12f;
            text.fontSizeMax = 0.2f;
            Private(root, mod.Q, text.GetComponent<Renderer>());
            var manual = root.AddComponent<WiringManual>();
            SetFields(manual, ("quadrant", mod.Q), ("shift", shift), ("text", text));
            var so = new UnityEditor.SerializedObject(manual);
            SetArray(so.FindProperty("panels"), panels);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Gerbe d'étincelles (court-circuit), une salve.</summary>
        static ParticleSystem SparkBurst(Mats m, Transform parent, Vector3 position)
        {
            var go = new GameObject("Etincelles");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var sparks = go.AddComponent<ParticleSystem>();
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
            shape.scale = new Vector3(0.7f, 0.4f, 0.02f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = m.GlowAngler;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return sparks;
        }
    }
}
