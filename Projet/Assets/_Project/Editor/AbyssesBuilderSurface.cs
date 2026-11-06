using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Abysses.Core;
using Abysses.Level;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Générateur, partie « surface » : la plateforme offshore d'où part la plongée, au large d'une île, et ce qu'il faut
    /// dans chaque cabine pour l'animation de plongée (puits d'eau, éclaboussures). Voir <see cref="SurfaceWorld"/>.
    ///
    /// Budget Quest 2 (déjà limité par le GPU) : aucune ombre, une seule lumière temps réel (le soleil, qui n'éclaire
    /// que la surface et s'éteint sous l'eau), maillages fusionnés par matériau (une poignée de draw calls).
    /// </summary>
    public static partial class AbyssesBuilder
    {
        const float SeaLevel = -0.8f;
        const uint SurfaceLayer = 1u << 2;
        const float ShroudRadius = 2.95f;

        static Material seaMaterial, sandMaterial, grassMaterial, rockMaterial, cloudMaterial, sunDiscMaterial, whitePaint, shroudMaterial, palmLeaf, palmTrunk, foamMaterial;

        static void SurfaceMaterials()
        {
            Texture2D waves = AbyssesContent.Png("T_Sea_Normal", WaveNormals(256), 256, 256, true, false, true, 256);
            seaMaterial = Lit("M_Sea", new Color(0.05f, 0.27f, 0.36f), 0f, 0.9f);
            seaMaterial.SetTexture("_BumpMap", waves);
            seaMaterial.SetFloat("_BumpScale", 0.6f);
            seaMaterial.EnableKeyword("_NORMALMAP");
            // Vue d'en dessous pendant la plongée : les deux faces.
            seaMaterial.SetFloat("_Cull", (float)CullMode.Off);
            seaMaterial.doubleSidedGI = true;
            EditorUtility.SetDirty(seaMaterial);

            sandMaterial = Lit("M_Sand", new Color(0.86f, 0.78f, 0.58f), 0f, 0.15f);
            grassMaterial = Lit("M_Grass", new Color(0.24f, 0.46f, 0.2f), 0f, 0.2f);
            rockMaterial = Lit("M_Rock", new Color(0.42f, 0.4f, 0.38f), 0f, 0.25f);
            whitePaint = Lit("M_WhitePaint", new Color(0.9f, 0.9f, 0.88f), 0.1f, 0.5f);
            palmLeaf = Lit("M_PalmLeaf", new Color(0.2f, 0.5f, 0.17f), 0f, 0.3f);
            palmTrunk = Lit("M_PalmTrunk", new Color(0.45f, 0.33f, 0.2f), 0f, 0.2f);
            cloudMaterial = Unlit("M_Cloud", new Color(0.97f, 0.98f, 1f));
            sunDiscMaterial = Unlit("M_SunDisc", new Color(1f, 0.97f, 0.85f));
            foamMaterial = Unlit("M_Foam", new Color(0.92f, 0.97f, 1f));

            Texture2D gradient = AbyssesContent.Png("T_WaterGradient", WaterGradient(64), 4, 64, true, true);
            shroudMaterial = Unlit("M_DescentWater", Color.white);
            shroudMaterial.SetTexture("_BaseMap", gradient);
            shroudMaterial.SetFloat("_Cull", (float)CullMode.Off);
            EditorUtility.SetDirty(shroudMaterial);
        }

        // ------------------------------------------------------------------ Cabine : puits d'eau et éclaboussures

        /// <summary>
        /// Puits d'eau (cylindre vu de l'intérieur, couvercle, anneau au sol) qui entoure la cabine pendant la descente :
        /// il masque l'étage qui se décharge / se charge. Gerbes d'eau aux hublots au passage de la surface.
        /// </summary>
        static void CabinDescentParts(Transform cabin)
        {
            GameObject shroud = AbyssesArt.MeshObject("DescentShroud", cabin, ShroudMesh(), Vector3.zero, Quaternion.identity, Vector3.one, shroudMaterial);
            var renderer = shroud.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;

            ParticleSystem left = SplashBurst(cabin, new Vector3(-1.75f, 1.5f, 0f));
            ParticleSystem right = SplashBurst(cabin, new Vector3(1.75f, 1.5f, 0f));
            right.transform.SetParent(left.transform, true);
            left.gameObject.name = "Splash";
        }

        static ParticleSystem SplashBurst(Transform parent, Vector3 position)
        {
            var go = new GameObject("SplashBurst");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.09f);
            main.startColor = new Color(0.9f, 0.97f, 1f, 0.8f);
            main.gravityModifier = -0.3f;
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 70), new ParticleSystem.Burst(0.25f, 40) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.3f, 0.8f, 1.2f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        static Mesh ShroudMesh()
        {
            const int segments = 40;
            const float bottom = 0.03f, top = 3.6f, inner = 1.8f;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices.Add(dir * ShroudRadius + Vector3.up * bottom); uvs.Add(new Vector2(i / (float)segments, 0f));
                vertices.Add(dir * ShroudRadius + Vector3.up * top); uvs.Add(new Vector2(i / (float)segments, 1f));
            }
            for (int i = 0; i < segments; i++)
            {
                int k = i * 2;
                triangles.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 });
            }
            // Couvercle (clair : la lumière vient d'en haut) et anneau au sol (sombre).
            int center = vertices.Count;
            vertices.Add(Vector3.up * top); uvs.Add(new Vector2(0.5f, 1f));
            int ring = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices.Add(dir * ShroudRadius + Vector3.up * top); uvs.Add(new Vector2(0.5f, 1f));
                vertices.Add(dir * ShroudRadius + Vector3.up * bottom); uvs.Add(new Vector2(0.5f, 0f));
                vertices.Add(dir * inner + Vector3.up * bottom); uvs.Add(new Vector2(0.5f, 0f));
            }
            for (int i = 0; i < segments; i++)
            {
                int k = ring + i * 3, n = k + 3;
                triangles.AddRange(new[] { center, k, n });
                triangles.AddRange(new[] { k + 1, n + 1, k + 2, n + 1, n + 2, k + 2 });
            }
            var mesh = new Mesh { name = "DescentShroud" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SurfaceMesh("DescentShroud", mesh);
        }

        // ------------------------------------------------------------------ Surface

        static void BuildSurface(Mats m, Transform stationRoot, Transform[] cabins)
        {
            var surface = new GameObject("Surface");
            surface.transform.SetParent(stationRoot, false);
            Transform world = new GameObject("World").transform;
            world.SetParent(surface.transform, false);
            Transform cablesRoot = new GameObject("Cables").transform;
            cablesRoot.SetParent(surface.transform, false);

            var center = new Vector3(PlayArea.ArenaWidth / 2f, 0f, PlayArea.ArenaDepth / 2f);

            // Mer : un grand plan, les vaguelettes défilent (SurfaceWorld décale la normal map).
            GameObject sea = Prim(PrimitiveType.Quad, "Mer", world, new Vector3(center.x, SeaLevel, center.z), Quaternion.Euler(90f, 0f, 0f), new Vector3(1400f, 1400f, 1f), seaMaterial, false);

            Transform deck = new GameObject("Plateforme").transform;
            deck.SetParent(world, false);
            BuildDeck(m, deck, cabins);
            BuildRailings(m, deck);
            var cableTops = new Transform[cabins.Length];
            var cables = new Transform[cabins.Length];
            var cableBottoms = new Vector3[cabins.Length];
            for (int i = 0; i < cabins.Length; i++)
                BuildDavit(m, deck, cablesRoot, cabins[i].position, out cableTops[i], out cables[i], out cableBottoms[i]);

            Transform scenery = new GameObject("Paysage").transform;
            scenery.SetParent(world, false);
            // Île principale à gauche (vue des hublots gauches), île haute en face, îlot à palmiers à droite, phare.
            Island(scenery, "Ile_Principale", new Vector3(-170f, SeaLevel, 70f), 60f, 32f, 11, 12);
            Island(scenery, "Ile_Haute", new Vector3(60f, SeaLevel, 260f), 85f, 58f, 23, 6);
            Island(scenery, "Ilot", new Vector3(140f, SeaLevel, -20f), 24f, 7f, 37, 6);
            Lighthouse(m, scenery, new Vector3(-60f, SeaLevel, 6f));
            Boat(m, world, new Vector3(48f, SeaLevel, 5f));
            Clouds(scenery, center);
            Transform[] gulls = Gulls(m, world, center);

            // Un seul jeu de maillages par matériau pour tout le décor fixe.
            CombineByMaterial(deck, "Surface_Plateforme");
            CombineByMaterial(scenery, "Surface_Paysage");
            foreach (Renderer r in surface.GetComponentsInChildren<Renderer>(true))
            {
                r.renderingLayerMask |= SurfaceLayer;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            // Collision du pont (déplacement au joystick en debug, gravité active).
            var floor = new GameObject("Sol").AddComponent<BoxCollider>();
            floor.transform.SetParent(world, false);
            floor.center = new Vector3(center.x, -0.15f, center.z);
            floor.size = new Vector3(PlayArea.ArenaWidth + 4f, 0.3f, PlayArea.ArenaDepth + 4f);

            // Soleil : seule lumière temps réel, sans ombres, qui n'éclaire que la surface (couche de rendu dédiée).
            var sunGo = new GameObject("Soleil");
            sunGo.transform.SetParent(surface.transform, false);
            sunGo.transform.rotation = Quaternion.Euler(38f, -140f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.None;
            sun.lightmapBakeType = LightmapBakeType.Realtime;
            if (!sunGo.TryGetComponent(out UniversalAdditionalLightData sunData)) sunData = sunGo.AddComponent<UniversalAdditionalLightData>();
            sunData.renderingLayers = SurfaceLayer;
            // Disque du soleil, dans l'axe de la lumière.
            Vector3 sunDir = -sunGo.transform.forward;
            GameObject disc = Prim(PrimitiveType.Quad, "DisqueSolaire", world, center + sunDir * 480f, Quaternion.LookRotation(-sunDir), Vector3.one * 26f, sunDiscMaterial, false);
            disc.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Sons : ressac et vent, mouettes au-dessus de la plateforme, treuil et éclaboussure de la plongée.
            AudioSource ambience = Sfx(surface, fx.Waves, 0.6f, 0f);
            ambience.loop = true;
            ambience.playOnAwake = true;
            var gullVoice = new GameObject("Mouettes_Cris");
            gullVoice.transform.SetParent(surface.transform, false);
            AudioSource gullSource = Sfx(gullVoice, null, 0.5f);
            gullSource.maxDistance = 60f;
            var shots = gullVoice.AddComponent<RandomOneShots>();
            var shotsSo = new SerializedObject(shots);
            SetArray(shotsSo.FindProperty("clips"), fx.Gull);
            SetArray(shotsSo.FindProperty("emitters"), gulls);
            shotsSo.FindProperty("intervalRange").vector2Value = new Vector2(3f, 9f);
            shotsSo.ApplyModifiedPropertiesWithoutUndo();
            var winchGo = new GameObject("Treuil_Son");
            winchGo.transform.SetParent(surface.transform, false);
            AudioSource winch = Sfx(winchGo, fx.Winch, 0.9f, 0f);
            AudioSource splash = Sfx(winchGo, fx.Splash, 0.9f, 0f);

            var shrouds = new Renderer[cabins.Length];
            var splashes = new ParticleSystem[cabins.Length];
            for (int i = 0; i < cabins.Length; i++)
            {
                shrouds[i] = Find(cabins[i], "DescentShroud").GetComponent<Renderer>();
                splashes[i] = Find(cabins[i], "Splash").GetComponent<ParticleSystem>();
            }

            var controller = surface.AddComponent<SurfaceWorld>();
            SetFields(controller, ("world", world), ("sun", sun), ("sea", sea.GetComponent<Renderer>()), ("ambience", ambience),
                ("winch", winch), ("splash", splash), ("seaLevel", SeaLevel));
            var so = new SerializedObject(controller);
            SetArray(so.FindProperty("cables"), cables);
            SetArray(so.FindProperty("cableTops"), cableTops);
            SerializedProperty bottoms = so.FindProperty("cableBottoms");
            bottoms.arraySize = cableBottoms.Length;
            for (int i = 0; i < cableBottoms.Length; i++) bottoms.GetArrayElementAtIndex(i).vector3Value = cableBottoms[i];
            SetArray(so.FindProperty("shrouds"), shrouds);
            SetArray(so.FindProperty("splashes"), splashes);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Pont en tôle sur toute la piste (+2 m), percé d'un puits autour de chaque cabine, bordé de bandes de danger.</summary>
        static void BuildDeck(Mats m, Transform deck, Transform[] cabins)
        {
            const float hole = 1.6f, margin = 2f, thickness = 0.3f;
            var xs = new List<float> { -margin, PlayArea.ArenaWidth + margin };
            var zs = new List<float> { -margin, PlayArea.ArenaDepth + margin };
            foreach (Transform c in cabins)
            {
                xs.Add(c.position.x - hole); xs.Add(c.position.x + hole);
                zs.Add(c.position.z - hole); zs.Add(c.position.z + hole);
            }
            xs.Sort(); zs.Sort();
            for (int i = 0; i < xs.Count - 1; i++)
            for (int j = 0; j < zs.Count - 1; j++)
            {
                float x0 = xs[i], x1 = xs[i + 1], z0 = zs[j], z1 = zs[j + 1];
                if (x1 - x0 < 0.01f || z1 - z0 < 0.01f) continue;
                var mid = new Vector3((x0 + x1) / 2f, -thickness / 2f, (z0 + z1) / 2f);
                bool isHole = false;
                foreach (Transform c in cabins)
                    isHole |= Mathf.Abs(mid.x - c.position.x) < hole && Mathf.Abs(mid.z - c.position.z) < hole;
                if (isHole) continue;
                AbyssesArt.WorldBox("Pont", deck, mid, Quaternion.identity, new Vector3(x1 - x0, thickness, z1 - z0), m.Floor, 1f, false);
            }

            foreach (Transform c in cabins)
            {
                Vector3 p = c.position;
                // Bandes de danger autour du puits, passerelle devant la porte (+Z de la cabine).
                foreach (Vector2 side in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
                {
                    bool alongX = side.y != 0f;
                    Vector3 pos = p + new Vector3(side.x, 0f, side.y) * (hole + 0.06f) + Vector3.up * 0.004f;
                    Vector3 size = alongX ? new Vector3(hole * 2f + 0.24f, 0.008f, 0.12f) : new Vector3(0.12f, 0.008f, hole * 2f);
                    AbyssesArt.WorldBox("Bande", deck, pos, Quaternion.identity, size, m.Hazard, 0.5f, false);
                }
                AbyssesArt.WorldBox("Passerelle", deck, p + new Vector3(0f, -0.02f, 1.5f), Quaternion.identity, new Vector3(1.3f, 0.04f, 0.3f), m.Floor, 1f, false);
                // Garde-corps du puits sur les côtés et à l'arrière (on n'y passe pas).
                foreach (float x in new[] { -1f, 1f })
                    Rail(m.PipeYellow, deck, p + new Vector3(x * (hole + 0.15f), 0f, -hole), p + new Vector3(x * (hole + 0.15f), 0f, hole - 0.2f));
                Rail(m.PipeYellow, deck, p + new Vector3(-hole - 0.15f, 0f, -hole - 0.15f), p + new Vector3(hole + 0.15f, 0f, -hole - 0.15f));
            }

            // Piliers dans l'eau : on les voit défiler vers le haut par les hublots pendant la plongée.
            foreach (float x in new[] { -1.2f, PlayArea.ArenaWidth / 2f, PlayArea.ArenaWidth + 1.2f })
            foreach (float z in new[] { -1.2f, PlayArea.GetCenter(0).z, PlayArea.ArenaDepth / 2f, PlayArea.GetCenter(2).z, PlayArea.ArenaDepth + 1.2f })
            {
                Prim(PrimitiveType.Cylinder, "Pilier", deck, new Vector3(x, -16f, z), Quaternion.identity, new Vector3(0.9f, 15.7f, 0.9f), m.HullDark, false);
                Prim(PrimitiveType.Cylinder, "Bague", deck, new Vector3(x, SeaLevel - 0.2f, z), Quaternion.identity, new Vector3(1.0f, 0.35f, 1.0f), m.PipeYellow, false);
            }
            // Entretoises sous le pont.
            foreach (float z in new[] { -1.2f, PlayArea.ArenaDepth + 1.2f })
                Prim(PrimitiveType.Cube, "Poutre", deck, new Vector3(PlayArea.ArenaWidth / 2f, -0.6f, z), Quaternion.identity, new Vector3(PlayArea.ArenaWidth + 2.4f, 0.4f, 0.3f), m.HullDark, false);
        }

        /// <summary>Garde-corps le long de chaque zone de jeu (au bord du quadrant) et du pont.</summary>
        static void BuildRailings(Mats m, Transform deck)
        {
            for (int q = 0; q < PlayArea.QuadrantCount; q++)
            {
                Rect r = PlayArea.GetRect(q, PlayArea.WallInset);
                var a = new Vector3(r.xMin, 0f, r.yMin);
                var b = new Vector3(r.xMax, 0f, r.yMin);
                var c = new Vector3(r.xMax, 0f, r.yMax);
                var d = new Vector3(r.xMin, 0f, r.yMax);
                Rail(m.PipeYellow, deck, a, b);
                Rail(m.PipeYellow, deck, b, c);
                Rail(m.PipeYellow, deck, c, d);
                Rail(m.PipeYellow, deck, d, a);
            }
        }

        static void Rail(Material material, Transform parent, Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float length = dir.magnitude;
            if (length < 0.1f) return;
            Quaternion along = Quaternion.FromToRotation(Vector3.up, dir / length);
            foreach (float y in new[] { 1.05f, 0.55f })
                Prim(PrimitiveType.Cylinder, "Lisse", parent, (from + to) / 2f + Vector3.up * y, along, new Vector3(0.05f, length / 2f, 0.05f), material, false);
            int posts = Mathf.Max(1, Mathf.RoundToInt(length / 1.8f));
            for (int i = 0; i <= posts; i++)
                Prim(PrimitiveType.Cylinder, "Poteau", parent, Vector3.Lerp(from, to, i / (float)posts) + Vector3.up * 0.525f, Quaternion.identity, new Vector3(0.05f, 0.525f, 0.05f), material, false);
        }

        /// <summary>Potence derrière la cabine : deux montants, flèche au-dessus du dôme, treuil, poulie. Le câble est à part.</summary>
        static void BuildDavit(Mats m, Transform deck, Transform cablesRoot, Vector3 cabin, out Transform cableTop, out Transform cable, out Vector3 cableBottom)
        {
            const float height = 5.1f, back = 2.1f;
            foreach (float x in new[] { -0.6f, 0.6f })
                Prim(PrimitiveType.Cube, "Montant", deck, cabin + new Vector3(x, height / 2f, -back), Quaternion.identity, new Vector3(0.22f, height, 0.22f), m.PipeYellow, false);
            Prim(PrimitiveType.Cube, "Traverse", deck, cabin + new Vector3(0f, height - 0.1f, -back), Quaternion.identity, new Vector3(1.44f, 0.22f, 0.26f), m.PipeYellow, false);
            Prim(PrimitiveType.Cube, "Fleche", deck, cabin + new Vector3(0f, height + 0.1f, -back / 2f), Quaternion.identity, new Vector3(0.26f, 0.3f, back + 0.4f), m.PipeYellow, false);
            Prim(PrimitiveType.Cube, "Hauban", deck, cabin + new Vector3(0f, height - 0.9f, -back + 0.45f), Quaternion.Euler(-50f, 0f, 0f), new Vector3(0.12f, 1.9f, 0.12f), m.PipeYellow, false);
            Prim(PrimitiveType.Cylinder, "Poulie", deck, cabin + new Vector3(0f, height - 0.15f, 0f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.36f, 0.06f, 0.36f), m.Frame, false);
            Prim(PrimitiveType.Cylinder, "Treuil", deck, cabin + new Vector3(0f, 1.1f, -back - 0.05f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.5f, 0.45f, 0.5f), m.Frame, false);
            Prim(PrimitiveType.Cube, "Moteur", deck, cabin + new Vector3(0f, 0.45f, -back - 0.05f), Quaternion.identity, new Vector3(1.0f, 0.9f, 0.6f), m.HullDark, false);
            Prim(PrimitiveType.Cube, "Bande", deck, cabin + new Vector3(0f, 0.75f, -back + 0.26f), Quaternion.identity, new Vector3(1.0f, 0.12f, 0.01f), m.Hazard, false);

            // Point d'accroche hors de la plateforme (qui est fusionnée puis nettoyée), mais dans le monde qui bouge.
            var top = new GameObject("Cable_Haut").transform;
            top.SetParent(deck.parent, false);
            top.position = cabin + new Vector3(0f, height - 0.33f, 0f);
            cableTop = top;
            cableBottom = cabin + new Vector3(0f, 3.25f, 0f);
            float length = top.position.y - cableBottom.y;
            cable = Prim(PrimitiveType.Cylinder, "Cable", cablesRoot, (top.position + cableBottom) / 2f, Quaternion.identity, new Vector3(0.04f, length / 2f, 0.04f), m.Frame, false).transform;
        }

        // ---------- Paysage

        /// <summary>Île à facettes (low poly) : plage, végétation, roche au sommet, quelques palmiers sur le pourtour.</summary>
        static void Island(Transform parent, string name, Vector3 position, float radius, float height, int seed, int palms)
        {
            var random = new System.Random(seed);
            const int rings = 14, segments = 40;
            var heights = new float[rings + 1, segments];
            var offsets = new float[segments];
            for (int s = 0; s < segments; s++) offsets[s] = 0.75f + 0.45f * (float)random.NextDouble();
            for (int r = 0; r <= rings; r++)
            for (int s = 0; s < segments; s++)
            {
                float u = r / (float)rings;
                float shape = Mathf.Pow(Mathf.Clamp01(1f - u), 1.6f);
                float noise = 1f + 0.35f * ((float)random.NextDouble() - 0.5f);
                heights[r, s] = u >= 0.92f ? -1.5f + (1f - u) * 12f : height * shape * noise + 1.2f;
            }
            Vector3 Point(int r, int s)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                float d = r / (float)rings * radius * offsets[s];
                return new Vector3(Mathf.Cos(a) * d, r == 0 ? height + 1.2f : heights[r, s], Mathf.Sin(a) * d);
            }

            var vertices = new List<Vector3>();
            var sand = new List<int>();
            var grass = new List<int>();
            var rock = new List<int>();
            void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                float y = (a.y + b.y + c.y) / 3f;
                List<int> target = y < 2.2f ? sand : y > height * 0.62f ? rock : grass;
                int i = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                target.Add(i); target.Add(i + 1); target.Add(i + 2);
            }
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < segments; s++)
            {
                int n = (s + 1) % segments;
                Vector3 a = Point(r, s), b = Point(r, n), c = Point(r + 1, s), d = Point(r + 1, n);
                if (r > 0) Triangle(a, b, c);
                Triangle(b, d, c);
            }
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(sand, 0);
            mesh.SetTriangles(grass, 1);
            mesh.SetTriangles(rock, 2);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.AddComponent<MeshFilter>().sharedMesh = SurfaceMesh("Surface_" + name, mesh);
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { sandMaterial, grassMaterial, rockMaterial };

            // Palmiers sur le haut de plage (anneau u ≈ 0,89, juste avant la descente vers l'eau).
            for (int i = 0; i < palms; i++)
            {
                int seg = random.Next(segments);
                float a = seg / (float)segments * Mathf.PI * 2f;
                float d = 0.89f * radius * offsets[seg];
                float y = height * Mathf.Pow(0.11f, 1.6f) + 0.9f;
                Palm(go.transform, new Vector3(Mathf.Cos(a) * d, y, Mathf.Sin(a) * d), (float)random.NextDouble() * 360f, 6f + 3f * (float)random.NextDouble());
            }
        }

        static void Palm(Transform parent, Vector3 position, float yaw, float height)
        {
            var root = new GameObject("Palmier").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Prim(PrimitiveType.Cylinder, "Tronc", root, new Vector3(0.4f, height / 2f, 0f), Quaternion.Euler(0f, 0f, -8f), new Vector3(0.45f, height / 2f, 0.45f), palmTrunk, false);
            Vector3 crown = new Vector3(0.4f + Mathf.Tan(8f * Mathf.Deg2Rad) * height, height, 0f);
            for (int i = 0; i < 6; i++)
            {
                Quaternion leaf = Quaternion.Euler(0f, i * 60f, 0f) * Quaternion.Euler(0f, 0f, -28f);
                Prim(PrimitiveType.Cube, "Palme", root, crown + leaf * new Vector3(1.6f, 0f, 0f), leaf, new Vector3(3.4f, 0.08f, 0.9f), palmLeaf, false);
            }
        }

        static void Lighthouse(Mats m, Transform parent, Vector3 position)
        {
            var root = new GameObject("Phare").transform;
            root.SetParent(parent, false);
            root.position = position;
            Prim(PrimitiveType.Sphere, "Rocher", root, new Vector3(0f, -0.5f, 0f), Quaternion.identity, new Vector3(16f, 5f, 13f), rockMaterial, false);
            Prim(PrimitiveType.Cylinder, "Tour", root, new Vector3(0f, 9f, 0f), Quaternion.identity, new Vector3(3f, 7f, 3f), whitePaint, false);
            foreach (float y in new[] { 5f, 9.5f, 14f })
                Prim(PrimitiveType.Cylinder, "BandeRouge", root, new Vector3(0f, y, 0f), Quaternion.identity, new Vector3(3.05f, 0.9f, 3.05f), m.PipeRed, false);
            Prim(PrimitiveType.Cylinder, "Lanterne", root, new Vector3(0f, 16.8f, 0f), Quaternion.identity, new Vector3(2f, 0.8f, 2f), m.WarmBulb, false);
            Prim(PrimitiveType.Cylinder, "Toit", root, new Vector3(0f, 17.8f, 0f), Quaternion.identity, new Vector3(2.6f, 0.2f, 2.6f), m.PipeRed, false);
            // Faisceau tournant (additif, sans lumière).
            var beam = new GameObject("Faisceau_Tournant").transform;
            beam.SetParent(root, false);
            beam.localPosition = new Vector3(0f, 16.8f, 0f);
            SetFields(beam.gameObject.AddComponent<Spin>(), ("degreesPerSecond", 30f));
            // Le cône du kit est orienté selon Y : basculé à l'horizontale des deux côtés.
            foreach (float side in new[] { 90f, -90f })
            {
                GameObject shaft = AbyssesArt.MeshObject("Faisceau", beam, fx.ShaftCone, Vector3.zero, Quaternion.Euler(0f, 0f, side), new Vector3(3f, 40f, 3f), m.SunShaft);
                shaft.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static void Boat(Mats m, Transform parent, Vector3 position)
        {
            var root = new GameObject("Bateau").transform;
            root.SetParent(parent, false);
            root.position = position;
            root.rotation = Quaternion.Euler(0f, 70f, 0f);
            Prim(PrimitiveType.Cube, "Coque", root, new Vector3(0f, 0.4f, 0f), Quaternion.identity, new Vector3(3.2f, 1.2f, 9f), m.PipeRed, false);
            Prim(PrimitiveType.Cube, "Proue", root, new Vector3(0f, 0.45f, 5f), Quaternion.Euler(0f, 45f, 0f), new Vector3(2.2f, 1.1f, 2.2f), m.PipeRed, false);
            Prim(PrimitiveType.Cube, "Pont", root, new Vector3(0f, 1.05f, 0.5f), Quaternion.identity, new Vector3(3.0f, 0.1f, 9.5f), m.Floor, false);
            Prim(PrimitiveType.Cube, "Cabine", root, new Vector3(0f, 2.1f, -1.5f), Quaternion.identity, new Vector3(2.4f, 2f, 3f), whitePaint, false);
            Prim(PrimitiveType.Cube, "Vitres", root, new Vector3(0f, 2.5f, -0f), Quaternion.identity, new Vector3(2.2f, 0.6f, 0.05f), m.Visor, false);
            Prim(PrimitiveType.Cylinder, "Mat", root, new Vector3(0f, 4.2f, -1.5f), Quaternion.identity, new Vector3(0.12f, 1.2f, 0.12f), m.Frame, false);
            root.gameObject.AddComponent<Bobbing>();
        }

        static void Clouds(Transform parent, Vector3 center)
        {
            var random = new System.Random(5);
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f + (float)random.NextDouble() * 0.3f;
                var basePos = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (360f + 80f * (float)random.NextDouble()) + Vector3.up * (60f + 60f * (float)random.NextDouble());
                for (int k = 0; k < 4; k++)
                {
                    var offset = new Vector3(((float)random.NextDouble() - 0.5f) * 50f, ((float)random.NextDouble() - 0.5f) * 8f, ((float)random.NextDouble() - 0.5f) * 30f);
                    float size = 18f + 22f * (float)random.NextDouble();
                    Prim(PrimitiveType.Sphere, "Nuage", parent, basePos + offset, Quaternion.identity, new Vector3(size * 1.8f, size * 0.5f, size), cloudMaterial, false);
                }
            }
        }

        static Transform[] Gulls(Mats m, Transform parent, Vector3 center)
        {
            var gulls = new Transform[5];
            for (int i = 0; i < gulls.Length; i++)
            {
                var gull = new GameObject("Mouette").transform;
                gull.SetParent(parent, false);
                Prim(PrimitiveType.Cube, "Corps", gull, Vector3.zero, Quaternion.identity, new Vector3(0.14f, 0.12f, 0.45f), whitePaint, false);
                Transform left = new GameObject("AileG").transform;
                left.SetParent(gull, false);
                Prim(PrimitiveType.Cube, "Aile", left, new Vector3(-0.45f, 0f, 0f), Quaternion.identity, new Vector3(0.9f, 0.02f, 0.22f), whitePaint, false);
                Transform right = new GameObject("AileD").transform;
                right.SetParent(gull, false);
                Prim(PrimitiveType.Cube, "Aile", right, new Vector3(0.45f, 0f, 0f), Quaternion.identity, new Vector3(0.9f, 0.02f, 0.22f), whitePaint, false);
                SetFields(gull.gameObject.AddComponent<Orbit>(), ("center", center + Vector3.up * (14f + i * 3f)), ("radius", 18f + i * 6f),
                    ("speed", (i % 2 == 0 ? 1f : -1f) * (10f + i * 2f)), ("phase", i * 70f), ("leftWing", left), ("rightWing", right));
                gulls[i] = gull;
            }
            return gulls;
        }

        // ---------- Outils

        /// <summary>Fusionne tous les maillages simples sous <paramref name="root"/> en un maillage par matériau (assets).</summary>
        static void CombineByMaterial(Transform root, string prefix)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var sources = new List<GameObject>();
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || filter.sharedMesh == null || filter.GetComponents<Component>().Length > 3) continue;
                // Pièces animées (faisceau du phare…) : on n'y touche pas.
                if (filter.GetComponentInParent<Spin>() != null) continue;
                Material[] materials = renderer.sharedMaterials;
                for (int sub = 0; sub < filter.sharedMesh.subMeshCount && sub < materials.Length; sub++)
                {
                    if (!groups.TryGetValue(materials[sub], out List<CombineInstance> list)) groups[materials[sub]] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = sub, transform = toRoot * filter.transform.localToWorldMatrix });
                }
                sources.Add(filter.gameObject);
            }
            int index = 0;
            foreach (KeyValuePair<Material, List<CombineInstance>> group in groups)
            {
                var mesh = new Mesh { name = prefix + "_" + index, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(group.Value.ToArray(), true, true);
                mesh.RecalculateBounds();
                var go = new GameObject(prefix + "_" + group.Key.name);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = SurfaceMesh(prefix + "_" + index, mesh);
                go.AddComponent<MeshRenderer>().sharedMaterial = group.Key;
                index++;
            }
            foreach (GameObject source in sources)
            {
                if (source == null) continue;
                // Les objets animés (bateau, mouettes, faisceau) gardent leurs composants : ils ne passent pas ici.
                Object.DestroyImmediate(source.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(source.GetComponent<MeshFilter>());
            }
            // Groupes vides restants.
            for (int i = root.childCount - 1; i >= 0; i--)
                PruneEmpty(root.GetChild(i));
        }

        static void PruneEmpty(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) PruneEmpty(t.GetChild(i));
            if (t.childCount == 0 && t.GetComponents<Component>().Length == 1) Object.DestroyImmediate(t.gameObject);
        }

        static Mesh SurfaceMesh(string name, Mesh generated)
        {
            const string dir = "Assets/_Project/Art/Meshes";
            EnsureFolder(dir);
            string path = dir + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }
            existing.Clear();
            EditorUtility.CopySerialized(generated, existing);
            existing.name = name;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// <summary>Normal map de vaguelettes, raccordable (somme de sinus entiers sur la tuile).</summary>
        static Color[] WaveNormals(int size)
        {
            var heights = new float[size * size];
            var random = new System.Random(3);
            var waves = new (int kx, int ky, float amp, float phase)[10];
            for (int i = 0; i < waves.Length; i++)
                waves[i] = (random.Next(-6, 7), random.Next(1, 8), 1f / (1 + i), (float)random.NextDouble() * Mathf.PI * 2f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float h = 0f;
                foreach ((int kx, int ky, float amp, float phase) in waves)
                    h += amp * Mathf.Sin((kx * x + ky * y) / (float)size * Mathf.PI * 2f + phase);
                heights[y * size + x] = h;
            }
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = heights[y * size + (x + 1) % size] - heights[y * size + (x + size - 1) % size];
                float dy = heights[((y + 1) % size) * size + x] - heights[((y + size - 1) % size) * size + x];
                Vector3 n = new Vector3(-dx * 0.6f, -dy * 0.6f, 1f).normalized;
                pixels[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
            return pixels;
        }

        /// <summary>Dégradé vertical du puits d'eau : plus clair vers le haut (la lumière vient de la surface).</summary>
        static Color[] WaterGradient(int height)
        {
            var pixels = new Color[4 * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                float k = Mathf.Lerp(0.55f, 1.35f, v * v);
                for (int x = 0; x < 4; x++) pixels[y * 4 + x] = new Color(k / 1.35f, k / 1.35f, k / 1.35f, 1f);
            }
            return pixels;
        }
    }
}
