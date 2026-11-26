using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Abysses.Core;
using Abysses.Level;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Générateur, partie « vie derrière les hublots » : chaque hublot sur l'océan devient une petite fenêtre
    /// d'illustration. Fond d'eau en dégradé, rayons de lumière, colonnes de bulles, jardin de coraux et d'algues devant
    /// chaque hublot, bancs de poissons colorés en 3D, méduses lumineuses. Dosage selon la profondeur : couleurs et
    /// lumière du jour en haut, bioluminescence dans le noir en bas.
    /// Budget Quest 2 : coraux fusionnés par matériau, aucune lumière, animations calculées par un composant par groupe.
    /// </summary>
    public static partial class AbyssesBuilder
    {
        const float SeabedY = -0.95f;

        /// <summary>Ambiance d'un étage vu de l'extérieur (style = paramètre « floor » d'OceanView : 1, 2, 4 ballasts, 3 cœur).</summary>
        struct OceanStyle
        {
            public Color waterTop, rays, tipA, tipB;
            public Color[] coral, fish;
            public bool glowingFish, kelp;
            public int fishPerSchool, jellies;
            public Color jelly;
        }

        static OceanStyle StyleOf(int floor)
        {
            switch (floor)
            {
                case 1: // Habitat, -200 m : encore un peu de lumière du jour, récif coloré.
                    return new OceanStyle
                    {
                        waterTop = new Color(0.1f, 0.38f, 0.52f), rays = new Color(0.55f, 0.9f, 1f, 0.07f),
                        tipA = new Color(0.3f, 0.95f, 1f), tipB = new Color(1f, 0.6f, 0.3f),
                        coral = new[] { new Color(0.85f, 0.38f, 0.45f), new Color(0.95f, 0.55f, 0.28f), new Color(0.5f, 0.32f, 0.75f) },
                        fish = new[] { new Color(1f, 0.5f, 0.12f), new Color(1f, 0.85f, 0.2f), new Color(0.2f, 0.55f, 1f) },
                        glowingFish = false, kelp = true, fishPerSchool = 11, jellies = 1, jelly = new Color(0.6f, 0.85f, 1f, 0.35f),
                    };
                case 2: // Laboratoires, -1 500 m : eau sombre, récif terne aux pointes chaudes, poissons-lanternes.
                    return new OceanStyle
                    {
                        waterTop = new Color(0.05f, 0.12f, 0.15f), rays = new Color(0.4f, 0.7f, 0.8f, 0.025f),
                        tipA = new Color(1f, 0.55f, 0.25f), tipB = new Color(1f, 0.85f, 0.4f),
                        coral = new[] { new Color(0.28f, 0.2f, 0.22f), new Color(0.22f, 0.24f, 0.3f), new Color(0.32f, 0.22f, 0.18f) },
                        fish = new[] { new Color(1f, 0.6f, 0.2f), new Color(1f, 0.85f, 0.4f) },
                        glowingFish = true, kelp = false, fishPerSchool = 5, jellies = 3, jelly = new Color(1f, 0.6f, 0.35f, 0.35f),
                    };
                case 4: // Ballasts, -2 800 m : vert d'eau, algues pâles, poissons turquoise.
                    return new OceanStyle
                    {
                        waterTop = new Color(0.04f, 0.18f, 0.2f), rays = new Color(0.4f, 1f, 0.85f, 0.025f),
                        tipA = new Color(0.3f, 1f, 0.85f), tipB = new Color(0.7f, 1f, 0.5f),
                        coral = new[] { new Color(0.16f, 0.3f, 0.3f), new Color(0.22f, 0.36f, 0.28f), new Color(0.18f, 0.22f, 0.32f) },
                        fish = new[] { new Color(0.3f, 1f, 0.85f), new Color(0.6f, 1f, 0.5f) },
                        glowingFish = true, kelp = true, fishPerSchool = 7, jellies = 3, jelly = new Color(0.35f, 1f, 0.85f, 0.35f),
                    };
                default: // Cœur, -4 000 m : noir, bioluminescence cyan et magenta, méduses violettes.
                    return new OceanStyle
                    {
                        waterTop = new Color(0.03f, 0.02f, 0.07f), rays = new Color(0f, 0f, 0f, 0f),
                        tipA = new Color(0.3f, 0.95f, 1f), tipB = new Color(1f, 0.3f, 0.9f),
                        coral = new[] { new Color(0.1f, 0.1f, 0.18f), new Color(0.14f, 0.08f, 0.18f) },
                        fish = new[] { new Color(0.3f, 0.95f, 1f), new Color(1f, 0.35f, 0.9f) },
                        glowingFish = true, kelp = false, fishPerSchool = 4, jellies = 4, jelly = new Color(0.75f, 0.4f, 1f, 0.4f),
                    };
            }
        }

        static readonly Dictionary<string, Material> oceanMaterials = new Dictionary<string, Material>();

        /// <param name="glow">Lueur propre (fraction de la couleur) : dehors, aucune lumière bakée n'éclaire le récif.</param>
        static Material OceanStylized(string name, Color color, float rim, float glow = 0f)
        {
            if (oceanMaterials.TryGetValue(name, out Material cached) && cached != null) return cached;
            Material material = glow > 0f ? Lit(name, color, 0f, 0.2f, color * glow) : Lit(name, color, 0f, 0.2f);
            Stylize(material, color, rim);
            return oceanMaterials[name] = material;
        }

        static Material OceanGlow(string name, Color color, float strength)
        {
            if (oceanMaterials.TryGetValue(name, out Material cached) && cached != null) return cached;
            return oceanMaterials[name] = Lit(name, color * 0.2f, 0f, 0.5f, color * strength);
        }

        // ------------------------------------------------------------------ Passe d'étage

        static void OceanLife(Mats m, Transform ocean, Module[] modules, int floor)
        {
            OceanStyle style = StyleOf(floor);
            var random = new System.Random(100 + floor);

            Transform garden = new GameObject("Jardins").transform;
            garden.SetParent(ocean, false);
            Transform animated = new GameObject("Vie").transform;
            animated.SetParent(ocean, false);
            var stems = new List<Transform>();
            var jellies = new List<Transform>();

            foreach (float side in new[] { -1f, 1f })
            {
                // Bord extérieur de ce côté de la piste (sud : z = 0, nord : z = profondeur).
                float edge = side < 0 ? 0f : PlayArea.ArenaDepth;
                Quaternion inward = Quaternion.LookRotation(new Vector3(0f, 0f, -side), Vector3.up);

                // Fond marin et fond d'eau en dégradé (plus clair vers le haut).
                GameObject bed = Prim(PrimitiveType.Quad, "FondMarin", garden, new Vector3(PlayArea.ArenaWidth / 2f, SeabedY, edge + side * 8f),
                    Quaternion.Euler(90f, 0f, 0f), new Vector3(PlayArea.ArenaWidth + 30f, 16f, 1f), OceanStylized("M_Ocean_Sable_" + floor, style.coral[0] * 0.45f + new Color(0.06f, 0.07f, 0.08f), 0.1f), false);
                bed.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                Material water = Unlit("M_Ocean_Fond_" + floor, style.waterTop);
                water.SetTexture("_BaseMap", waterGradient);
                EditorUtility.SetDirty(water);
                Prim(PrimitiveType.Quad, "FondEau", animated, new Vector3(PlayArea.ArenaWidth / 2f, 4f, edge + side * 14f), inward,
                    new Vector3(PlayArea.ArenaWidth + 50f, 26f, 1f), water, false).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

                // Rayons de lumière qui descendent de la surface (rien dans le noir du cœur).
                if (style.rays.a > 0f)
                {
                    Material rayMaterial = Additive("M_Ocean_Rayon_" + floor, "Universal Render Pipeline/Unlit", style.rays, fx.ShaftGradient);
                    for (float x = -4f; x <= PlayArea.ArenaWidth + 4f; x += 6.5f)
                    {
                        float z = edge + side * (4.5f + (float)random.NextDouble() * 4f);
                        GameObject ray = AbyssesArt.MeshObject("Rayon", animated, fx.ShaftCone, new Vector3(x, 10f, z),
                            Quaternion.Euler(0f, 0f, ((float)random.NextDouble() - 0.5f) * 14f), new Vector3(2.6f, 11f, 2.6f), rayMaterial);
                        ray.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    }
                }

                // Bancs de poissons : deux par côté, en sens opposés, à hauteur des hublots.
                for (int s = 0; s < 2; s++)
                    FishSchoolGroup(animated, style, floor, side, edge, s, random);

                // Méduses lumineuses, dérivant devant les hublots.
                Material jellyMaterial = Additive("M_Ocean_Meduse_" + floor, "Universal Render Pipeline/Unlit", style.jelly, fx.Dot);
                Material jellyCore = OceanGlow("M_Ocean_MeduseCoeur_" + floor, new Color(style.jelly.r, style.jelly.g, style.jelly.b), 2.2f);
                for (int j = 0; j < style.jellies; j++)
                {
                    float x = 2f + (float)random.NextDouble() * (PlayArea.ArenaWidth - 4f);
                    var position = new Vector3(x, 1.2f + (float)random.NextDouble() * 1.4f, edge + side * (2.2f + (float)random.NextDouble() * 3f));
                    jellies.Add(Jelly(animated, position, 0.6f + (float)random.NextDouble() * 0.5f, jellyMaterial, jellyCore));
                }
            }

            // Jardin de coraux (et d'algues) devant chaque hublot, colonne de bulles par module.
            foreach (Module mod in modules)
            {
                Rect area = PlayArea.GetRect(mod.Q);
                float edge = mod.Vz < 0 ? area.yMin : area.yMax;
                foreach (float wx in OceanWindowX)
                {
                    Vector3 window = mod.P(wx, 0f, 0f);
                    var basePoint = new Vector3(window.x, SeabedY, edge + mod.Vz * 2.6f);
                    CoralGarden(garden, style, floor, basePoint, mod.Vz, random);
                    if (style.kelp)
                        for (int k = 0; k < 4; k++)
                            stems.Add(Kelp(animated, floor, basePoint + new Vector3(((float)random.NextDouble() - 0.5f) * 2.4f, 0f, mod.Vz * (0.6f + (float)random.NextDouble() * 1.6f)), random));
                }
                ParticleSystem bubbles = Particles("Bulles_Q" + mod.Q, animated, new Vector3(mod.P(OceanWindowX[0] * 0.5f, 0f, 0f).x, SeabedY, edge + mod.Vz * 3.2f), m.Drop, 40, 5f, new Vector2(6f, 9f));
                var main = bubbles.main;
                main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
                Velocity(bubbles, new Vector2(-0.04f, 0.04f), new Vector2(0.55f, 0.8f), new Vector2(-0.04f, 0.04f));
                var shape = bubbles.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.15f;
            }

            CombineByMaterial(garden, "Ocean_Jardin_" + floor);
            foreach (Renderer r in ocean.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            if (stems.Count > 0)
            {
                var sway = animated.gameObject.AddComponent<Sway>();
                var so = new SerializedObject(sway);
                SetArray(so.FindProperty("stems"), stems.ToArray());
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (jellies.Count > 0)
            {
                var pulse = animated.gameObject.AddComponent<JellyPulse>();
                var so = new SerializedObject(pulse);
                SetArray(so.FindProperty("jellies"), jellies.ToArray());
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ---------- Coraux

        static void CoralGarden(Transform parent, OceanStyle style, int floor, Vector3 basePoint, float side, System.Random random)
        {
            Material Body(int i) => OceanStylized("M_Ocean_Corail_" + floor + "_" + i, style.coral[i % style.coral.Length], 0.8f, floor == 1 ? 0.35f : 0.25f);
            Material tipA = OceanGlow("M_Ocean_PointeA_" + floor, style.tipA, 1.8f);
            Material tipB = OceanGlow("M_Ocean_PointeB_" + floor, style.tipB, 1.8f);
            Vector3 Spot(float spread) => basePoint + new Vector3(((float)random.NextDouble() - 0.5f) * spread, 0f, side * ((float)random.NextDouble() - 0.3f) * spread * 0.6f);

            // Rochers qui portent le jardin.
            for (int i = 0; i < 2; i++)
                Prim(PrimitiveType.Sphere, "Rocher", parent, Spot(2f) + Vector3.up * 0.2f, Quaternion.Euler(0f, (float)random.NextDouble() * 180f, 0f),
                    new Vector3(1.2f + (float)random.NextDouble(), 0.9f + (float)random.NextDouble() * 0.6f, 1f + (float)random.NextDouble()), OceanStylized("M_Ocean_Rocher_" + floor, style.coral[0] * 0.35f + new Color(0.05f, 0.06f, 0.08f), 0.4f, 0.12f), false);

            // Éponges en tube : pointe lumineuse (anneau).
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = Spot(2.2f);
                float h = 0.9f + (float)random.NextDouble() * 1.5f, r = 0.12f + (float)random.NextDouble() * 0.1f;
                Prim(PrimitiveType.Cylinder, "Eponge", parent, p + Vector3.up * (h / 2f), Quaternion.Euler(((float)random.NextDouble() - 0.5f) * 14f, 0f, ((float)random.NextDouble() - 0.5f) * 14f), new Vector3(r * 2f, h / 2f, r * 2f), Body(i), false);
                AbyssesArt.MeshObject("Lueur", parent, art.Ring, p + Vector3.up * (h + 0.01f), Quaternion.Euler(90f, 0f, 0f), new Vector3(r * 2.1f, r * 2.1f, 0.03f), i % 2 == 0 ? tipA : tipB);
            }

            // Coraux branchus : tronc, branches, boules lumineuses au bout.
            for (int i = 0; i < 2; i++)
            {
                Vector3 p = Spot(2f);
                Material body = Body(i + 1);
                float trunk = 0.6f + (float)random.NextDouble() * 0.6f;
                Prim(PrimitiveType.Cylinder, "Tronc", parent, p + Vector3.up * (trunk / 2f), Quaternion.identity, new Vector3(0.09f, trunk / 2f, 0.09f), body, false);
                for (int b = 0; b < 5; b++)
                {
                    float yaw = b * 72f + (float)random.NextDouble() * 30f, tilt = 25f + (float)random.NextDouble() * 30f;
                    float length = 0.4f + (float)random.NextDouble() * 0.5f;
                    Quaternion rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(tilt, 0f, 0f);
                    Vector3 start = p + Vector3.up * trunk;
                    Vector3 tip = start + rotation * Vector3.up * length;
                    Prim(PrimitiveType.Cylinder, "Branche", parent, (start + tip) / 2f, rotation, new Vector3(0.06f, length / 2f, 0.06f), body, false);
                    Prim(PrimitiveType.Sphere, "Bouton", parent, tip, Quaternion.identity, Vector3.one * 0.08f, b % 2 == 0 ? tipA : tipB, false);
                }
            }

            // Gorgone (éventail) tournée vers le hublot.
            Vector3 fan = Spot(1.6f);
            Prim(PrimitiveType.Sphere, "Gorgone", parent, fan + Vector3.up * 0.75f, Quaternion.LookRotation(new Vector3(0f, 0f, -side)) * Quaternion.Euler(0f, ((float)random.NextDouble() - 0.5f) * 40f, 0f),
                new Vector3(1.3f, 1.1f, 0.05f), Body(2), false);
        }

        static Transform Kelp(Transform parent, int floor, Vector3 basePoint, System.Random random)
        {
            var stem = new GameObject("Algue").transform;
            stem.SetParent(parent, false);
            stem.position = basePoint;
            stem.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 180f, 0f);
            float h = 1.8f + (float)random.NextDouble() * 1.6f;
            Material material = OceanStylized("M_Ocean_Algue_" + floor, floor == 1 ? new Color(0.22f, 0.52f, 0.28f) : new Color(0.24f, 0.48f, 0.4f), 0.9f, 0.3f);
            Prim(PrimitiveType.Cube, "Tige", stem, new Vector3(0f, h / 2f, 0f), Quaternion.identity, new Vector3(0.16f, h, 0.025f), material, false);
            return stem;
        }

        // ---------- Poissons et méduses

        static Mesh fishMesh;

        /// <summary>Poisson low poly (corps en losange aplati, queue en V), tourné vers +Z.</summary>
        static Mesh FishMesh()
        {
            if (fishMesh != null) return fishMesh;
            Vector3 nose = new Vector3(0f, 0f, 0.5f), back = new Vector3(0f, 0f, -0.32f);
            Vector3 top = new Vector3(0f, 0.22f, 0.05f), bottom = new Vector3(0f, -0.18f, 0.05f);
            Vector3 left = new Vector3(-0.08f, 0f, 0.08f), right = new Vector3(0.08f, 0f, 0.08f);
            Vector3 tailTop = new Vector3(0f, 0.2f, -0.55f), tailBottom = new Vector3(0f, -0.2f, -0.55f);
            var triangles = new List<Vector3[]>
            {
                new[] { nose, top, right }, new[] { nose, right, bottom }, new[] { nose, bottom, left }, new[] { nose, left, top },
                new[] { back, right, top }, new[] { back, bottom, right }, new[] { back, left, bottom }, new[] { back, top, left },
                new[] { back, tailTop, tailBottom }, new[] { back, tailBottom, tailTop },
            };
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            foreach (Vector3[] t in triangles)
            {
                indices.Add(vertices.Count); vertices.Add(t[0]);
                indices.Add(vertices.Count); vertices.Add(t[1]);
                indices.Add(vertices.Count); vertices.Add(t[2]);
            }
            var mesh = new Mesh { name = "Poisson" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return fishMesh = SurfaceMesh("Ocean_Poisson", mesh);
        }

        static void FishSchoolGroup(Transform parent, OceanStyle style, int floor, float side, float edge, int index, System.Random random)
        {
            var group = new GameObject("Banc").transform;
            group.SetParent(parent, false);
            group.position = new Vector3(0f, 1.25f + index * 0.45f, edge + side * (2.4f + index * 1.3f));
            var fish = new Transform[style.fishPerSchool];
            var offsets = new Vector3[fish.Length];
            Color color = style.fish[(index + (side > 0 ? 1 : 0)) % style.fish.Length];
            Material material = style.glowingFish
                ? OceanGlow("M_Ocean_Poisson_" + floor + "_" + index, color, 1.6f)
                : OceanStylized("M_Ocean_Poisson_" + floor + "_" + ((index + (side > 0 ? 1 : 0)) % style.fish.Length), color, 1f, 0.35f);
            for (int i = 0; i < fish.Length; i++)
            {
                float size = (style.glowingFish ? 0.3f : 0.4f) * (0.8f + (float)random.NextDouble() * 0.4f);
                fish[i] = AbyssesArt.MeshObject("Poisson", group, FishMesh(), Vector3.zero, Quaternion.identity, Vector3.one * size, material).transform;
                offsets[i] = new Vector3(((float)random.NextDouble() - 0.5f) * 1.6f, ((float)random.NextDouble() - 0.5f) * 0.6f, ((float)random.NextDouble() - 0.5f) * 0.9f);
            }
            var school = group.gameObject.AddComponent<FishSchool>();
            var so = new SerializedObject(school);
            SetArray(so.FindProperty("fish"), fish);
            SerializedProperty offsetProperty = so.FindProperty("offsets");
            offsetProperty.arraySize = offsets.Length;
            for (int i = 0; i < offsets.Length; i++) offsetProperty.GetArrayElementAtIndex(i).vector3Value = offsets[i];
            so.FindProperty("range").vector2Value = new Vector2(-10f, PlayArea.ArenaWidth + 10f);
            so.FindProperty("speed").floatValue = (style.glowingFish ? 0.6f : 1.1f) * (0.85f + index * 0.25f);
            so.FindProperty("direction").floatValue = index == 0 ? 1f : -1f;
            so.FindProperty("phase").floatValue = (float)random.NextDouble();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Méduse : ombrelle translucide, cœur lumineux, tentacules.</summary>
        static Transform Jelly(Transform parent, Vector3 position, float size, Material bell, Material core)
        {
            var root = new GameObject("Meduse").transform;
            root.SetParent(parent, false);
            root.position = position;
            root.localScale = Vector3.one * size;
            Prim(PrimitiveType.Sphere, "Ombrelle", root, Vector3.zero, Quaternion.identity, new Vector3(0.5f, 0.32f, 0.5f), bell, false);
            Prim(PrimitiveType.Sphere, "Coeur", root, new Vector3(0f, -0.02f, 0f), Quaternion.identity, new Vector3(0.18f, 0.12f, 0.18f), core, false);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                Prim(PrimitiveType.Cylinder, "Tentacule", root, new Vector3(Mathf.Cos(a) * 0.14f, -0.38f, Mathf.Sin(a) * 0.14f), Quaternion.identity, new Vector3(0.018f, 0.3f, 0.018f), bell, false);
            }
            return root;
        }
    }
}
