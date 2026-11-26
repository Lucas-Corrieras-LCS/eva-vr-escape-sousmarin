using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Abysses.Core;
using Abysses.Level;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Générateur, partie « direction artistique » : illustration sous-marine sombre, lisse et lumineuse, pensée pour le Quest 2.
    /// - Matériaux de décor et de personnages passés sur le shader Abysses/Stylized : aplats doux (texture réduite à un
    ///   grain), dégradé vertical, contour lumineux ; plus de normal maps ni de reflets (moins coûteux que le Lit).
    /// - Une palette sombre par étage (bleu-vert, rouge sourd, vert d'eau, violet bioluminescent) : seuls les éléments
    ///   importants brillent.
    /// - « Faux bloom » : halos additifs tournés vers le joueur autour des lampes (aucun post-traitement).
    /// - Rayons de lumière qui entrent par les hublots sur l'océan.
    /// Tout est appliqué en une passe à la sauvegarde de chaque étage (voir <see cref="SaveScene"/>).
    /// </summary>
    public static partial class AbyssesBuilder
    {
        const string StylizedShader = "Abysses/Stylized";

        static Material styleVisor, styleBeam;
        static readonly Dictionary<Color, Material> haloMaterials = new Dictionary<Color, Material>();

        // Palettes : teinte au sol / en hauteur, contour lumineux. Sombres : le noir domine, la lumière raconte.
        static readonly AbyssPalette[] FloorPalettes =
        {
            // Étage 1 — Habitat : bleu-vert profond, contours cyan.
            new AbyssPalette { low = new Color(0.28f, 0.36f, 0.45f), high = new Color(0.92f, 1f, 1.08f), rim = new Color(0.3f, 0.85f, 1f),
                rimStrength = 0.55f, grain = 0.7f, gradientRange = new Vector2(0f, 2.8f), rimPower = 3f },
            // Étage 2 — Laboratoires : rouge sourd, contours orangés.
            new AbyssPalette { low = new Color(0.28f, 0.18f, 0.18f), high = new Color(0.9f, 0.78f, 0.76f), rim = new Color(1f, 0.42f, 0.28f),
                rimStrength = 0.45f, grain = 0.7f, gradientRange = new Vector2(0f, 2.8f), rimPower = 3f },
            // Étage 3 — Ballasts : vert d'eau, contours turquoise.
            new AbyssPalette { low = new Color(0.22f, 0.36f, 0.38f), high = new Color(0.9f, 1.02f, 1f), rim = new Color(0.3f, 1f, 0.82f),
                rimStrength = 0.5f, grain = 0.7f, gradientRange = new Vector2(0f, 2.8f), rimPower = 3f },
            // Étage 4 — Cœur : presque noir, bioluminescence violette et cyan.
            new AbyssPalette { low = new Color(0.16f, 0.13f, 0.28f), high = new Color(0.7f, 0.74f, 0.95f), rim = new Color(0.75f, 0.35f, 1f),
                rimStrength = 0.65f, grain = 0.65f, gradientRange = new Vector2(0f, 3.2f), rimPower = 2.6f },
        };

        static readonly Color[] ShaftColors =
        {
            new Color(0.35f, 0.8f, 1f, 0.06f), new Color(1f, 0.45f, 0.3f, 0.04f), new Color(0.35f, 1f, 0.85f, 0.05f), new Color(0.6f, 0.4f, 1f, 0.05f),
        };

        // ------------------------------------------------------------------ Matériaux

        /// <summary>Passe un matériau de décor sur le shader stylisé, avec une couleur d'aplat retravaillée.</summary>
        /// <param name="rim">Force du contour lumineux (0 = aucun, 1 = personnages).</param>
        static void Stylize(Material material, Color color, float rim = 0.3f)
        {
            if (material == null) return;
            Shader shader = Shader.Find(StylizedShader);
            if (shader == null)
            {
                Debug.LogError("[Abysses] Shader " + StylizedShader + " introuvable.");
                return;
            }
            bool emissive = material.IsKeywordEnabled("_EMISSION");
            Color emission = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            // Pas de reflets (aucun mot-clé spéculaire) : l'alpha de la couleur spéculaire dose le contour lumineux.
            material.SetColor("_SpecColor", new Color(0f, 0f, 0f, rim));
            material.SetTexture("_BumpMap", null);
            foreach (string keyword in new[] { "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_SPECULAR_SETUP", "_SPECULAR_COLOR", "_SPECGLOSSMAP" })
                material.DisableKeyword(keyword);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            EditorUtility.SetDirty(material);
        }

        static void StylizeMaterials(Mats m)
        {
            // Décor : aplats doux et sombres (contour discret), accents de couleur francs sur les objets (contour marqué).
            Stylize(m.Hull, new Color(0.32f, 0.4f, 0.48f), 0.2f);
            Stylize(m.HullDark, new Color(0.12f, 0.15f, 0.2f), 0.25f);
            Stylize(m.Ceiling, new Color(0.16f, 0.2f, 0.26f), 0.1f);
            Stylize(m.Floor, new Color(0.2f, 0.26f, 0.32f), 0.1f);
            Stylize(m.HullLab, new Color(0.46f, 0.42f, 0.4f), 0.2f);
            Stylize(m.LabTile, new Color(0.5f, 0.58f, 0.6f), 0.15f);
            Stylize(m.Locker, new Color(0.22f, 0.42f, 0.52f), 0.55f);
            Stylize(m.Panel, new Color(0.24f, 0.29f, 0.35f), 0.55f);
            Stylize(m.Frame, new Color(0.07f, 0.09f, 0.12f), 0.5f);
            Stylize(m.Crate, new Color(0.5f, 0.38f, 0.26f), 0.6f);
            Stylize(m.Key, new Color(0.07f, 0.08f, 0.1f), 0.5f);
            Stylize(m.Rubber, new Color(0.035f, 0.04f, 0.055f), 0.5f);
            Stylize(m.Pipe, new Color(0.28f, 0.32f, 0.36f), 0.6f);
            Stylize(m.PipeGrey, new Color(0.34f, 0.4f, 0.45f), 0.6f);
            Stylize(m.PipeYellow, new Color(0.95f, 0.66f, 0.12f), 0.6f);
            Stylize(m.PipeRed, new Color(0.62f, 0.1f, 0.08f), 0.6f);
            Stylize(m.Brass, new Color(0.88f, 0.62f, 0.3f), 0.8f);

            // Personnages : combinaison bleu nuit, lisse, que le contour lumineux détache du fond ; peinture du joueur lumineuse.
            Stylize(m.Suit, new Color(0.08f, 0.1f, 0.16f), 1f);
            for (int i = 0; i < m.AvatarPaint.Length; i++)
            {
                Stylize(m.AvatarPaint[i], QuadrantColors[i], 1f);
                m.AvatarPaint[i].EnableKeyword("_EMISSION");
                m.AvatarPaint[i].SetColor("_EmissionColor", QuadrantColors[i] * 0.55f);
                EditorUtility.SetDirty(m.AvatarPaint[i]);
            }
            // Visière éclairée de l'intérieur : on devine le plongeur dans le noir.
            styleVisor = Lit("M_VisorGlow", new Color(0.02f, 0.05f, 0.07f), 0f, 0.9f, new Color(0.2f, 0.65f, 0.75f) * 0.4f);
            // Faisceau de la lampe frontale (additif, très léger), visible par les coéquipiers.
            styleBeam = Additive("M_HeadlampBeam", "Universal Render Pipeline/Unlit", new Color(1f, 0.9f, 0.7f, 0.07f), fx.ShaftGradient);
        }

        // ------------------------------------------------------------------ Passe d'étage

        /// <summary>Palette, halos et rayons d'un étage, juste avant sa sauvegarde.</summary>
        static void StyleFloor(Scene scene, int floorIndex)
        {
            Transform root = null;
            FloorAmbience ambience = null;
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                ambience = go.GetComponentInChildren<FloorAmbience>(true);
                if (ambience != null)
                {
                    root = go.transform;
                    break;
                }
            }
            if (root == null) return;

            AbyssPalette palette = FloorPalettes[Mathf.Clamp(floorIndex, 0, FloorPalettes.Length - 1)];
            var so = new SerializedObject(ambience);
            SerializedProperty p = so.FindProperty("palette");
            p.FindPropertyRelative("low").colorValue = palette.low;
            p.FindPropertyRelative("high").colorValue = palette.high;
            p.FindPropertyRelative("rim").colorValue = palette.rim;
            p.FindPropertyRelative("rimStrength").floatValue = palette.rimStrength;
            p.FindPropertyRelative("grain").floatValue = palette.grain;
            p.FindPropertyRelative("gradientRange").vector2Value = palette.gradientRange;
            p.FindPropertyRelative("rimPower").floatValue = palette.rimPower;
            so.ApplyModifiedPropertiesWithoutUndo();

            GlowHalos(root, floorIndex);
            WindowShafts(root, floorIndex);
        }

        /// <summary>
        /// Halos additifs autour des lampes (appliques, diffuseurs du plafond), toujours tournés vers le joueur.
        /// Étage 1 : les diffuseurs sont éteints pendant la panne, seules les appliques ont un halo.
        /// </summary>
        static void GlowHalos(Transform root, int floorIndex)
        {
            var halos = new List<Transform>();
            Transform group = new GameObject("Halos").transform;
            group.SetParent(root, false);
            foreach (Renderer source in root.GetComponentsInChildren<Renderer>(true))
            {
                bool bulb = source.name == "Ampoule";
                bool diffuser = source.name == "Diffuseur" && floorIndex != 0 && source.GetComponent<NeonFlicker>() == null;
                if (!bulb && !diffuser) continue;
                Material material = source.sharedMaterial;
                Color glow = material != null && material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.white;
                float peak = Mathf.Max(glow.r, Mathf.Max(glow.g, glow.b));
                if (peak <= 0.01f) continue;
                glow /= peak;
                if (bulb) halos.Add(Halo(group, source.bounds.center, 0.55f, glow, 0.35f));
                else
                {
                    // Diffuseur long : deux halos le long de la réglette, légèrement sous le plafond.
                    Bounds b = source.bounds;
                    Vector3 axis = b.size.x > b.size.z ? Vector3.right : Vector3.forward;
                    float half = Mathf.Max(b.size.x, b.size.z) * 0.3f;
                    foreach (float s in new[] { -1f, 1f })
                        halos.Add(Halo(group, b.center + axis * (s * half) + Vector3.down * 0.05f, 1.1f, glow, 0.22f));
                }
            }
            if (halos.Count == 0)
            {
                Object.DestroyImmediate(group.gameObject);
                return;
            }
            var billboards = group.gameObject.AddComponent<HaloBillboards>();
            var so = new SerializedObject(billboards);
            SetArray(so.FindProperty("halos"), halos.ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Transform Halo(Transform parent, Vector3 position, float size, Color color, float alpha)
        {
            color.a = alpha;
            if (!haloMaterials.TryGetValue(color, out Material material) || material == null)
            {
                string name = string.Format(System.Globalization.CultureInfo.InvariantCulture, "M_Halo_{0:0.00}_{1:0.00}_{2:0.00}_{3:0.00}", color.r, color.g, color.b, color.a);
                material = Additive(name, "Universal Render Pipeline/Particles/Unlit", color, fx.Dot);
                haloMaterials[color] = material;
            }
            GameObject quad = Prim(PrimitiveType.Quad, "Halo", parent, position, Quaternion.identity, Vector3.one * size, material, false);
            var renderer = quad.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            return quad.transform;
        }

        /// <summary>Rayons de lumière qui entrent par les hublots sur l'océan et tombent vers le sol du module.</summary>
        static void WindowShafts(Transform root, int floorIndex)
        {
            Color color = ShaftColors[Mathf.Clamp(floorIndex, 0, ShaftColors.Length - 1)];
            Material material = Additive("M_WindowShaft_" + floorIndex, "Universal Render Pipeline/Unlit", color, fx.ShaftGradient);
            Transform group = new GameObject("Rayons_Hublots").transform;
            group.SetParent(root, false);
            for (int q = 0; q < PlayArea.QuadrantCount; q++)
            {
                var mod = new Module(q);
                foreach (float x in OceanWindowX)
                {
                    Vector3 window = mod.P(x, 1.75f, mod.Vz * 3.9f);
                    // Le cône du kit s'ouvre vers -Y : on le couche vers l'intérieur du module, incliné vers le sol.
                    // Très incliné : le rayon tombe au sol près du hublot sans traverser la pièce (ni la tête du joueur).
                    Vector3 inward = new Vector3(0f, -1.1f, -mod.Vz).normalized;
                    GameObject shaft = AbyssesArt.MeshObject("Rayon", group, fx.ShaftCone, window, Quaternion.FromToRotation(Vector3.down, inward),
                        new Vector3(0.65f, 1.9f, 0.55f), material);
                    var renderer = shaft.GetComponent<Renderer>();
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                }
            }
        }
    }
}
