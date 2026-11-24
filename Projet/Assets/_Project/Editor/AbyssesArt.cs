using System.IO;
using UnityEditor;
using UnityEngine;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Habillage artistique procédural (aucun asset externe) :
    /// - textures PBR générées par code (albédo + normal map) : plaques rivetées, tôle larmée, plafond nervuré,
    ///   casiers, bandes de danger, écrans ;
    /// - maillages : boîtes à UV « monde » (la texture garde la même échelle quelle que soit la taille de la pièce),
    ///   cadres arrondis pour les hublots.
    /// Tout est reproductible (graines fixes) et dimensionné pour le Quest 2 (512 px max, compression ASTC).
    /// </summary>
    public static class AbyssesArt
    {
        const string ArtDir = "Assets/_Project/Art";
        const string MeshDir = ArtDir + "/Meshes";

        public class Kit
        {
            public Texture2D HullAlbedo, HullNormal, PlateAlbedo, PlateNormal, CeilingAlbedo, CeilingNormal;
            public Texture2D LockerAlbedo, LockerNormal, Hazard, ScreenWave, ScreenBars, ScreenText;
            public Texture2D LabTileAlbedo, LabTileNormal;
            /// <summary>Peinture usée (objets à UV 0..1 par face) : grain, rayures, arêtes écaillées, crasse.</summary>
            public Texture2D WornAlbedo, WornNormal;
            public Texture2D RubberAlbedo, RubberNormal, WoodAlbedo, WoodNormal;
            /// <summary>Cadre arrondi 1 x 1 (à mettre à l'échelle), bord de 0,1, profondeur 1.</summary>
            public Mesh RoundedFrame;
            /// <summary>Anneau de hublot (cadre circulaire) de diamètre 1, profondeur 1.</summary>
            public Mesh Ring;
        }

        public static Kit Build()
        {
            EnsureFolder(ArtDir);
            EnsureFolder(MeshDir);
            FrameCache.Clear();
            BevelCache.Clear();
            var kit = new Kit();

            float[] hull = HullHeight(512, out Color[] hullAlbedo);
            kit.HullAlbedo = Png("T_Hull_Albedo", hullAlbedo, 512, false);
            kit.HullNormal = Png("T_Hull_Normal", NormalFromHeight(hull, 512, 6f), 512, true);

            float[] plate = PlateHeight(512, out Color[] plateAlbedo);
            kit.PlateAlbedo = Png("T_Plate_Albedo", plateAlbedo, 512, false);
            kit.PlateNormal = Png("T_Plate_Normal", NormalFromHeight(plate, 512, 5f), 512, true);

            float[] ceiling = CeilingHeight(256, out Color[] ceilingAlbedo);
            kit.CeilingAlbedo = Png("T_Ceiling_Albedo", ceilingAlbedo, 256, false);
            kit.CeilingNormal = Png("T_Ceiling_Normal", NormalFromHeight(ceiling, 256, 4f), 256, true);

            float[] locker = LockerHeight(256, out Color[] lockerAlbedo);
            kit.LockerAlbedo = Png("T_Locker_Albedo", lockerAlbedo, 256, false);
            kit.LockerNormal = Png("T_Locker_Normal", NormalFromHeight(locker, 256, 5f), 256, true);

            float[] labTile = LabTileHeight(256, out Color[] labTileAlbedo);
            kit.LabTileAlbedo = Png("T_LabTile_Albedo", labTileAlbedo, 256, false);
            kit.LabTileNormal = Png("T_LabTile_Normal", NormalFromHeight(labTile, 256, 5f), 256, true);

            float[] worn = WornPaintHeight(256, out Color[] wornAlbedo);
            kit.WornAlbedo = Png("T_Worn_Albedo", wornAlbedo, 256, false);
            kit.WornNormal = Png("T_Worn_Normal", NormalFromHeight(worn, 256, 3f), 256, true);

            float[] rubber = RubberHeight(128, out Color[] rubberAlbedo);
            kit.RubberAlbedo = Png("T_Rubber_Albedo", rubberAlbedo, 128, false);
            kit.RubberNormal = Png("T_Rubber_Normal", NormalFromHeight(rubber, 128, 2.5f), 128, true);

            float[] wood = WoodHeight(256, out Color[] woodAlbedo);
            kit.WoodAlbedo = Png("T_Wood_Albedo", woodAlbedo, 256, false);
            kit.WoodNormal = Png("T_Wood_Normal", NormalFromHeight(wood, 256, 4f), 256, true);

            kit.Hazard = Png("T_Hazard", Hazard(128), 128, false);
            kit.ScreenWave = Png("T_Screen_Wave", Screen(256, 0), 256, false);
            kit.ScreenBars = Png("T_Screen_Bars", Screen(256, 1), 256, false);
            kit.ScreenText = Png("T_Screen_Text", Screen(256, 2), 256, false);

            kit.RoundedFrame = SaveMesh("RoundedFrame", RoundedFrameMesh(1f, 1f, 0.18f, 0.1f, 1f));
            kit.Ring = SaveMesh("Ring", RoundedFrameMesh(1f, 1f, 0.5f, 0.14f, 1f));
            return kit;
        }

        // ================================================================== Maillages

        /// <summary>
        /// Boîte dont les UV sont projetées en coordonnées monde (mètres / <paramref name="tile"/>) :
        /// deux murs voisins ont des textures raccord et à la même échelle. UV2 générées pour les lightmaps.
        /// Le maillage est enregistré dans la scène (pas d'asset) : une boîte = un maillage, fusionnés ensuite par le static batching.
        /// </summary>
        public static GameObject WorldBox(string name, Transform parent, Vector3 position, Quaternion rotation, Vector3 size, Material material, float tile, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(name, size, go.transform.localToWorldMatrix, tile);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collider) go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        static readonly System.Collections.Generic.Dictionary<string, Mesh> FrameCache = new System.Collections.Generic.Dictionary<string, Mesh>();

        /// <summary>
        /// Cadre à coins arrondis aux dimensions exactes (pas de mise à l'échelle non uniforme qui déformerait les bords).
        /// Enregistré comme asset (nom stable = GUID stable) et réutilisé par toutes les ouvertures de même taille.
        /// </summary>
        public static Mesh FrameMesh(float width, float height, float radius, float border, float depth)
        {
            string name = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Frame_{0:0.00}x{1:0.00}_r{2:0.00}_b{3:0.00}_d{4:0.00}", width, height, radius, border, depth);
            if (FrameCache.TryGetValue(name, out Mesh cached) && cached != null) return cached;
            Mesh mesh = SaveMesh(name, RoundedFrameMesh(width, height, radius, border, depth));
            FrameCache[name] = mesh;
            return mesh;
        }

        static readonly System.Collections.Generic.Dictionary<string, Mesh> BevelCache = new System.Collections.Generic.Dictionary<string, Mesh>();

        /// <summary>
        /// Cube unité (-0,5..0,5) aux arêtes arrondies, à mettre à l'échelle <paramref name="size"/> : le rayon de l'arrondi
        /// est fixé en mètres (proportionnel au plus petit côté, 2,5 cm max), donc les arrondis restent ronds quelle que soit
        /// l'échelle. Les normales tiennent compte de l'échelle. Asset partagé par toutes les pièces de mêmes proportions.
        /// Renvoie null pour une pièce trop fine (feuille, joint) : le cube standard suffit.
        /// </summary>
        public static Mesh BeveledCube(Vector3 size)
        {
            float smallest = Mathf.Min(Mathf.Abs(size.x), Mathf.Min(Mathf.Abs(size.y), Mathf.Abs(size.z)));
            if (smallest < 0.006f) return null;
            float radius = Mathf.Clamp(smallest * 0.2f, 0.0015f, 0.025f);
            // Rayons en espace unité, arrondis pour partager les maillages entre pièces de proportions voisines.
            var r = new Vector3(Quantize(radius / Mathf.Abs(size.x)), Quantize(radius / Mathf.Abs(size.y)), Quantize(radius / Mathf.Abs(size.z)));
            string name = string.Format(System.Globalization.CultureInfo.InvariantCulture, "Bevel_{0:0.000}_{1:0.000}_{2:0.000}", r.x, r.y, r.z);
            if (BevelCache.TryGetValue(name, out Mesh cached) && cached != null) return cached;
            EnsureFolder(MeshDir + "/Bevel");
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/Bevel/" + name + ".asset");
            if (mesh == null)
            {
                mesh = BeveledCubeMesh(r);
                mesh.name = name;
                AssetDatabase.CreateAsset(mesh, MeshDir + "/Bevel/" + name + ".asset");
            }
            BevelCache[name] = mesh;
            return mesh;
        }

        static float Quantize(float value) => Mathf.Clamp(Mathf.Round(value / 0.005f) * 0.005f, 0.005f, 0.25f);

        static Mesh BeveledCubeMesh(Vector3 r)
        {
            // 6 coordonnées par axe : face plane au centre, 2 segments d'arrondi de chaque côté.
            float[] Coords(float radius) => new[] { -0.5f, -0.5f + radius * 0.35f, -0.5f + radius, 0.5f - radius, 0.5f - radius * 0.35f, 0.5f };
            float[][] coords = { Coords(r.x), Coords(r.y), Coords(r.z) };
            Vector3 inner = new Vector3(0.5f, 0.5f, 0.5f) - r;

            var vertices = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var triangles = new System.Collections.Generic.List<int>();
            for (int f = 0; f < 6; f++)
            {
                Vector3 n = FaceNormals[f];
                int axis = Mathf.Abs(n.x) > 0.5f ? 0 : Mathf.Abs(n.y) > 0.5f ? 1 : 2;
                int ua = (axis + 1) % 3, va = (axis + 2) % 3;
                float side = n[axis];
                // Ordre des sommets : face extérieure dans le sens horaire vu de dehors (convention Unity).
                bool flip = side < 0f;
                int start = vertices.Count;
                float[] cu = coords[ua], cv = coords[va];
                for (int j = 0; j < cv.Length; j++)
                for (int i = 0; i < cu.Length; i++)
                {
                    var p = new Vector3();
                    p[axis] = 0.5f * side;
                    p[ua] = cu[i];
                    p[va] = cv[j];
                    // Arrondi : on ramène le point sur une « sphère » d'ellipsoïde autour du coeur du cube.
                    Vector3 core = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                    Vector3 d = new Vector3((p.x - core.x) / r.x, (p.y - core.y) / r.y, (p.z - core.z) / r.z);
                    Vector3 dir = d.sqrMagnitude > 1e-8f ? d.normalized : n;
                    vertices.Add(core + Vector3.Scale(dir, r));
                    // Normale en espace objet compensant l'échelle non uniforme (transformée par l'inverse transposée).
                    normals.Add(new Vector3(dir.x / r.x, dir.y / r.y, dir.z / r.z).normalized);
                    uvs.Add(new Vector2(cu[i] + 0.5f, cv[j] + 0.5f));
                }
                int w = cu.Length;
                for (int j = 0; j < cv.Length - 1; j++)
                for (int i = 0; i < w - 1; i++)
                {
                    int a = start + j * w + i, b = a + 1, c = a + w, e = c + 1;
                    if (!flip) triangles.AddRange(new[] { a, b, c, b, e, c });
                    else triangles.AddRange(new[] { a, c, b, b, c, e });
                }
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            Unwrapping.GenerateSecondaryUVSet(mesh);
            return mesh;
        }

        /// <summary>Objet utilisant un maillage du kit (cadre, anneau), sans collider.</summary>
        public static GameObject MeshObject(string name, Transform parent, Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static readonly Vector3[] FaceNormals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

        static Mesh BoxMesh(string name, Vector3 size, Matrix4x4 toWorld, float tile)
        {
            var vertices = new Vector3[24];
            var normals = new Vector3[24];
            var uvs = new Vector2[24];
            var triangles = new int[36];
            Vector3 h = size * 0.5f;

            for (int f = 0; f < 6; f++)
            {
                Vector3 n = FaceNormals[f];
                Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, n);
                Vector3 v = Vector3.Cross(n, u);
                Vector3 center = Vector3.Scale(n, h);
                Vector3 du = Vector3.Scale(u, h);
                Vector3 dv = Vector3.Scale(v, h);
                Vector3 worldNormal = toWorld.MultiplyVector(n).normalized;

                for (int i = 0; i < 4; i++)
                {
                    float su = i == 0 || i == 3 ? -1f : 1f;
                    float sv = i < 2 ? -1f : 1f;
                    Vector3 p = center + du * su + dv * sv;
                    int index = f * 4 + i;
                    vertices[index] = p;
                    normals[index] = n;
                    uvs[index] = WorldUV(toWorld.MultiplyPoint3x4(p), worldNormal) / tile;
                }
                int t = f * 6, b = f * 4;
                triangles[t] = b; triangles[t + 1] = b + 2; triangles[t + 2] = b + 1;
                triangles[t + 3] = b; triangles[t + 4] = b + 3; triangles[t + 5] = b + 2;
            }

            var mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            // Les faces doivent regarder vers l'extérieur : on vérifie l'orientation avec la normale et on corrige si besoin.
            FixWinding(mesh);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            Unwrapping.GenerateSecondaryUVSet(mesh);
            return mesh;
        }

        /// <summary>Projection planaire selon l'axe dominant de la normale (« box mapping »).</summary>
        static Vector2 WorldUV(Vector3 p, Vector3 n)
        {
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            if (ay >= ax && ay >= az) return new Vector2(p.x, p.z);
            if (ax >= az) return new Vector2(n.x > 0 ? -p.z : p.z, p.y);
            return new Vector2(n.z > 0 ? p.x : -p.x, p.y);
        }

        static void FixWinding(Mesh mesh)
        {
            Vector3[] v = mesh.vertices;
            Vector3[] n = mesh.normals;
            int[] t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 faceNormal = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (Vector3.Dot(faceNormal, n[t[i]]) < 0f) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            }
            mesh.triangles = t;
        }

        /// <summary>
        /// Cadre rectangulaire à coins arrondis (plan XY, épaisseur selon Z, centré) : extérieur w x h, rayon r, largeur du bord b.
        /// Avec w = h et r = w/2 on obtient un anneau (hublot).
        /// </summary>
        static Mesh RoundedFrameMesh(float w, float h, float r, float b, float depth)
        {
            const int cornerSegments = 8;
            int ringCount = 4 * (cornerSegments + 1);
            Vector2[] outer = RoundedRect(w, h, r, cornerSegments);
            Vector2[] inner = RoundedRect(w - 2f * b, h - 2f * b, Mathf.Max(0.001f, r - b), cornerSegments);
            float zf = -depth / 2f, zb = depth / 2f;

            var vertices = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var triangles = new System.Collections.Generic.List<int>();

            // Chaque quad reçoit la normale attendue : on corrige l'ordre des sommets pour que la face regarde dans ce sens.
            void Quad(Vector3 a, Vector3 c, Vector3 d, Vector3 e, Vector3 expected)
            {
                if (Vector3.Dot(Vector3.Cross(c - a, d - a), expected) < 0f) (c, e) = (e, c);
                int s = vertices.Count;
                vertices.Add(a); vertices.Add(c); vertices.Add(d); vertices.Add(e);
                uvs.Add(new Vector2(a.x + a.z, a.y)); uvs.Add(new Vector2(c.x + c.z, c.y));
                uvs.Add(new Vector2(d.x + d.z, d.y)); uvs.Add(new Vector2(e.x + e.z, e.y));
                triangles.Add(s); triangles.Add(s + 1); triangles.Add(s + 2);
                triangles.Add(s); triangles.Add(s + 2); triangles.Add(s + 3);
            }

            // Les contours sont parcourus dans le sens trigonométrique : la normale sortante d'un segment est (dy, -dx).
            for (int i = 0; i < ringCount; i++)
            {
                int j = (i + 1) % ringCount;
                Vector2 o0 = outer[i], o1 = outer[j], i0 = inner[i], i1 = inner[j];
                Vector2 outerEdge = o1 - o0, innerEdge = i1 - i0;
                var outward = new Vector3(outerEdge.y, -outerEdge.x, 0f);
                var inward = new Vector3(-innerEdge.y, innerEdge.x, 0f);
                Quad(new Vector3(o0.x, o0.y, zf), new Vector3(i0.x, i0.y, zf), new Vector3(i1.x, i1.y, zf), new Vector3(o1.x, o1.y, zf), Vector3.back);
                Quad(new Vector3(o0.x, o0.y, zb), new Vector3(i0.x, i0.y, zb), new Vector3(i1.x, i1.y, zb), new Vector3(o1.x, o1.y, zb), Vector3.forward);
                Quad(new Vector3(o0.x, o0.y, zb), new Vector3(o0.x, o0.y, zf), new Vector3(o1.x, o1.y, zf), new Vector3(o1.x, o1.y, zb), outward);
                Quad(new Vector3(i0.x, i0.y, zb), new Vector3(i0.x, i0.y, zf), new Vector3(i1.x, i1.y, zf), new Vector3(i1.x, i1.y, zb), inward);
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            Unwrapping.GenerateSecondaryUVSet(mesh);
            return mesh;
        }

        static Vector2[] RoundedRect(float w, float h, float r, int segments)
        {
            r = Mathf.Min(r, Mathf.Min(w, h) / 2f);
            var points = new Vector2[4 * (segments + 1)];
            Vector2[] centers =
            {
                new Vector2(w / 2f - r, h / 2f - r), new Vector2(-w / 2f + r, h / 2f - r),
                new Vector2(-w / 2f + r, -h / 2f + r), new Vector2(w / 2f - r, -h / 2f + r),
            };
            int k = 0;
            for (int c = 0; c < 4; c++)
            {
                for (int s = 0; s <= segments; s++)
                {
                    float angle = (c * 90f + 90f * s / segments) * Mathf.Deg2Rad;
                    points[k++] = centers[c] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                }
            }
            return points;
        }

        static Mesh SaveMesh(string name, Mesh generated)
        {
            string path = MeshDir + "/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                generated.name = name;
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }
            // Mise à jour en place : le GUID ne change pas, les scènes et prefabs restent liés.
            mesh.Clear();
            mesh.SetVertices(generated.vertices);
            mesh.SetNormals(generated.normals);
            mesh.SetTangents(generated.tangents);
            mesh.SetUVs(0, generated.uv);
            mesh.SetUVs(1, generated.uv2);
            mesh.SetTriangles(generated.triangles, 0);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            Object.DestroyImmediate(generated);
            return mesh;
        }

        // ================================================================== Bruit

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        /// <summary>Bruit de valeur périodique (raccord sur les bords de la texture).</summary>
        static float ValueNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int xa = Mod(x0, period), xb = Mod(x0 + 1, period), ya = Mod(y0, period), yb = Mod(y0 + 1, period);
            float a = Mathf.Lerp(Hash(xa, ya, seed), Hash(xb, ya, seed), fx);
            float b = Mathf.Lerp(Hash(xa, yb, seed), Hash(xb, yb, seed), fx);
            return Mathf.Lerp(a, b, fy);
        }

        static float Fbm(float u, float v, int baseFrequency, int octaves, int seed)
        {
            float sum = 0f, amplitude = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                int frequency = baseFrequency << o;
                sum += amplitude * ValueNoise(u * frequency, v * frequency, frequency, seed + o * 31);
                norm += amplitude;
                amplitude *= 0.5f;
            }
            return sum / norm;
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;

        static Color[] NormalFromHeight(float[] height, int size, float strength)
        {
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = height[y * size + Mod(x + 1, size)] - height[y * size + Mod(x - 1, size)];
                float dy = height[Mod(y + 1, size) * size + x] - height[Mod(y - 1, size) * size + x];
                var n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                pixels[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
            return pixels;
        }

        // ================================================================== Textures

        /// <summary>
        /// Coque : plaques de 1 m x 0,5 m (texture = 2 m x 2 m), joints creusés, lignes de rivets, léger bombé,
        /// salissures qui coulent vers le bas et variations de teinte d'une plaque à l'autre. Gris neutre : la couleur vient du matériau.
        /// </summary>
        static float[] HullHeight(int size, out Color[] albedo)
        {
            var height = new float[size * size];
            albedo = new Color[size * size];
            int pw = size / 2, ph = size / 4;
            const int rivetInset = 9, rivetSpacing = 21;
            const float rivetRadius = 3.2f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int px = x % pw, py = y % ph;
                int panelX = x / pw, panelY = y / ph;
                float u = x / (float)size, v = y / (float)size;
                int edge = Mathf.Min(Mathf.Min(px, pw - 1 - px), Mathf.Min(py, ph - 1 - py));

                float h = 0.5f + 0.035f * Mathf.Sin(Mathf.PI * px / pw) * Mathf.Sin(Mathf.PI * py / ph);
                h += 0.025f * (Fbm(u, v, 16, 3, 7) - 0.5f);
                float seam = edge < 2 ? 1f : edge < 6 ? 1f - (edge - 2) / 4f : 0f;
                h = Mathf.Lerp(h, 0.12f, seam);

                float rivet = Rivet(px, py, pw, ph, rivetInset, rivetSpacing, rivetRadius);
                h += 0.3f * rivet;
                height[y * size + x] = h;

                float tint = 0.86f + 0.2f * Hash(panelX, panelY, 3);
                float streak = 0.82f + 0.18f * Fbm(u * 1f, v * 0.15f + 0.37f, 24, 3, 11);
                float grime = 1f - 0.3f * Mathf.Pow(1f - py / (float)ph, 4f);
                float dirt = 0.9f + 0.1f * Fbm(u, v, 8, 4, 19);
                float value = 0.66f * tint * streak * grime * dirt;
                value = Mathf.Lerp(value, 0.22f, seam * 0.85f);
                value += 0.12f * rivet;
                albedo[y * size + x] = new Color(value, value * 1.01f, value * 1.03f, 1f);
            }
            return height;
        }

        static float Rivet(int px, int py, int pw, int ph, int inset, int spacing, float radius)
        {
            float best = float.MaxValue;
            // Lignes horizontales (haut / bas de la plaque)
            foreach (int ry in new[] { inset, ph - 1 - inset })
            {
                float rx = Mathf.Round((px - inset) / (float)spacing) * spacing + inset;
                rx = Mathf.Clamp(rx, inset, pw - 1 - inset);
                best = Mathf.Min(best, new Vector2(px - rx, py - ry).magnitude);
            }
            foreach (int rx in new[] { inset, pw - 1 - inset })
            {
                float ry = Mathf.Round((py - inset) / (float)spacing) * spacing + inset;
                ry = Mathf.Clamp(ry, inset, ph - 1 - inset);
                best = Mathf.Min(best, new Vector2(px - rx, py - ry).magnitude);
            }
            return best < radius ? Mathf.Sqrt(1f - (best / radius) * (best / radius)) : 0f;
        }

        /// <summary>Sol : tôle larmée (motifs en losange alternés, 6 cm), plaques de 1 m boulonnées, usure claire sur les reliefs.</summary>
        static float[] PlateHeight(int size, out Color[] albedo)
        {
            var height = new float[size * size];
            albedo = new Color[size * size];
            const int cell = 32;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                int cx = x / cell, cy = y / cell;
                float lx = (x % cell) - cell / 2f + 0.5f, ly = (y % cell) - cell / 2f + 0.5f;
                float angle = ((cx + cy) % 2 == 0 ? 45f : -45f) * Mathf.Deg2Rad;
                float ra = lx * Mathf.Cos(angle) + ly * Mathf.Sin(angle);
                float rb = -lx * Mathf.Sin(angle) + ly * Mathf.Cos(angle);
                float d = (ra * ra) / (11f * 11f) + (rb * rb) / (2.6f * 2.6f);
                float bump = d < 1f ? Mathf.Sqrt(1f - d) : 0f;

                int edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                float seam = edge < 2 ? 1f : 0f;
                float bolt = 0f;
                foreach (Vector2 c in new[] { new Vector2(14, 14), new Vector2(size - 15, 14), new Vector2(14, size - 15), new Vector2(size - 15, size - 15) })
                {
                    float bd = new Vector2(x - c.x, y - c.y).magnitude;
                    if (bd < 6f) bolt = Mathf.Max(bolt, Mathf.Sqrt(1f - (bd / 6f) * (bd / 6f)));
                }

                float h = 0.4f + 0.25f * bump + 0.3f * bolt - 0.3f * seam + 0.02f * Fbm(u, v, 16, 3, 23);
                height[y * size + x] = h;

                float dirt = 0.75f + 0.25f * Fbm(u, v, 4, 4, 29);
                float value = (0.42f + 0.22f * bump + 0.15f * bolt) * dirt;
                value = Mathf.Lerp(value, 0.15f, seam);
                albedo[y * size + x] = new Color(value, value, value * 1.02f, 1f);
            }
            return height;
        }

        /// <summary>Plafond : tôle ondulée (8 ondes / m) et joints de panneaux de 1 m.</summary>
        static float[] CeilingHeight(int size, out Color[] albedo)
        {
            var height = new float[size * size];
            albedo = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float wave = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * x * 8f / size);
                int edge = Mathf.Min(y, size - 1 - y);
                float seam = edge < 2 ? 1f : 0f;
                height[y * size + x] = 0.3f + 0.4f * wave - 0.3f * seam;
                float value = (0.5f + 0.12f * wave) * (0.85f + 0.15f * Fbm(u, v, 8, 3, 37));
                value = Mathf.Lerp(value, 0.18f, seam);
                albedo[y * size + x] = new Color(value, value, value, 1f);
            }
            return height;
        }

        /// <summary>Casier (UV 0..1 par face) : contour de porte, aérations, poignée.</summary>
        static float[] LockerHeight(int size, out Color[] albedo)
        {
            var height = new float[size * size];
            albedo = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                int edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                bool door = edge == 10 || edge == 11;
                bool vent = (y > size * 0.78f && y < size * 0.92f || y > size * 0.08f && y < size * 0.2f)
                            && x > size * 0.25f && x < size * 0.75f && (y / 5) % 2 == 0;
                bool handle = x > size * 0.78f && x < size * 0.84f && y > size * 0.42f && y < size * 0.6f;
                float h = 0.5f;
                if (door || vent) h = 0.15f;
                if (handle) h = 0.85f;
                h += 0.015f * (Fbm(u, v, 8, 3, 41) - 0.5f);
                height[y * size + x] = h;
                float value = 0.6f * (0.88f + 0.12f * Fbm(u, v, 4, 3, 43));
                if (door || vent) value = 0.2f;
                if (handle) value = 0.8f;
                albedo[y * size + x] = new Color(value, value, value, 1f);
            }
            return height;
        }

        /// <summary>
        /// Peinture industrielle usée, pour les pièces à UV 0..1 par face (cubes biseautés) : grain fin, taches de crasse,
        /// fines rayures, et peinture écaillée près des bords de chaque face (là où les mains et les chocs usent le métal).
        /// </summary>
        static float[] WornPaintHeight(int size, out Color[] albedo)
        {
            var height = new float[size * size];
            albedo = new Color[size * size];
            var scratch = new float[size * size];
            var random = new System.Random(71);
            for (int i = 0; i < 46; i++)
            {
                float x0 = (float)random.NextDouble() * size, y0 = (float)random.NextDouble() * size;
                float angle = (float)random.NextDouble() * Mathf.PI, length = 8f + (float)random.NextDouble() * 40f;
                for (float t = 0f; t < length; t += 0.5f)
                {
                    int x = Mod(Mathf.RoundToInt(x0 + Mathf.Cos(angle) * t), size), y = Mod(Mathf.RoundToInt(y0 + Mathf.Sin(angle) * t), size);
                    scratch[y * size + x] = 1f;
                }
            }
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                // Écaillures : seuil de bruit qui ronge la peinture près des bords.
                float chip = Fbm(u, v, 12, 3, 73);
                bool bare = chip > 0.5f + edge * 6f;
                float grime = Fbm(u, v, 3, 4, 79);
                float grain = Fbm(u, v, 32, 2, 83);
                float s = scratch[y * size + x];
                float h = 0.5f + 0.02f * (grain - 0.5f) - (bare ? 0.06f : 0f) - 0.03f * s;
                height[y * size + x] = h;
                float value = 0.9f + 0.1f * grain;
                value *= Mathf.Lerp(0.72f, 1.05f, grime);
                if (bare) value = 1.3f;
                if (s > 0f) value = Mathf.Max(value, 1.1f);
                value = Mathf.Clamp01(value / 1.35f);
                albedo[y * size + x] = new Color(value, value, value, 1f);
            }
            return height;
        }

        /// <summary>Caoutchouc / plastique grainé (touches, poignées).</summary>
        static float[] RubberHeight(int size, out Color[] albedo)
        {
            var height = new float[size * size];
            albedo = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float grain = Fbm(u, v, 24, 2, 89);
                height[y * size + x] = 0.5f + 0.05f * grain;
                float value = 0.85f + 0.15f * Fbm(u, v, 4, 3, 97);
                albedo[y * size + x] = new Color(value, value, value, 1f);
            }
            return height;
        }

        /// <summary>Planches de bois (caisses) : 4 planches, joints creusés, veinage, têtes de clous.</summary>
        static float[] WoodHeight(int size, out Color[] albedo)
        {
            var height = new float[size * size];
            albedo = new Color[size * size];
            int plank = size / 4;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                int index = y / plank, py = y % plank;
                bool joint = py < 2 || py > plank - 3;
                float grain = Fbm(u * 0.25f + index * 0.37f, v * 4f, 8, 4, 101 + index);
                float rings = 0.5f + 0.5f * Mathf.Sin((grain * 9f + u * 3f) * Mathf.PI);
                bool nail = (x % (size / 2) is > 10 and < 16) && (py > plank / 2 - 3 && py < plank / 2 + 3);
                float h = joint ? 0.2f : 0.5f + 0.04f * rings;
                if (nail) h = 0.6f;
                height[y * size + x] = h;
                float value = joint ? 0.35f : 0.75f + 0.25f * rings * (0.8f + 0.2f * Hash(index, 0, 7));
                if (nail) value = 0.45f;
                albedo[y * size + x] = new Color(value, value * 0.92f, value * 0.82f, 1f);
            }
            return height;
        }

        /// <summary>Faïence de laboratoire : carreaux de 25 cm (texture = 1 m), joints creusés, carreaux biseautés, quelques taches.</summary>
        static float[] LabTileHeight(int size, out Color[] albedo)
        {
            var height = new float[size * size];
            albedo = new Color[size * size];
            int cell = size / 4;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                int px = x % cell, py = y % cell;
                int edge = Mathf.Min(Mathf.Min(px, cell - 1 - px), Mathf.Min(py, cell - 1 - py));
                float grout = edge < 2 ? 1f : 0f;
                float bevel = edge < 2 ? 0f : edge < 5 ? (edge - 2) / 3f : 1f;
                height[y * size + x] = 0.2f + 0.35f * bevel + 0.01f * Fbm(u, v, 16, 2, 53);

                float tint = 0.93f + 0.07f * Hash(x / cell, y / cell, 59);
                float stain = Mathf.Clamp01((Fbm(u, v, 4, 4, 61) - 0.58f) * 4f);
                float value = 0.86f * tint * (1f - 0.25f * stain);
                value = Mathf.Lerp(value, 0.38f, grout);
                albedo[y * size + x] = new Color(value, value * 1.01f, value * 1.01f, 1f);
            }
            return height;
        }

        /// <summary>Bandes de danger jaunes et noires en diagonale, usées par endroits.</summary>
        static Color[] Hazard(int size)
        {
            var pixels = new Color[size * size];
            var yellow = new Color(0.95f, 0.72f, 0.08f);
            var black = new Color(0.04f, 0.04f, 0.045f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                bool stripe = ((x + y) / (size / 4)) % 2 == 0;
                Color c = stripe ? yellow : black;
                float wear = Fbm(u, v, 8, 4, 47);
                if (wear > 0.68f) c = Color.Lerp(c, new Color(0.35f, 0.36f, 0.38f), (wear - 0.68f) * 3f);
                pixels[y * size + x] = c * (0.85f + 0.15f * Fbm(u, v, 16, 2, 53));
            }
            return pixels;
        }

        /// <summary>Écrans de console (utilisés en émission) : courbe d'onde, histogramme, lignes de texte.</summary>
        static Color[] Screen(int size, int variant)
        {
            var pixels = new Color[size * size];
            var background = new Color(0.01f, 0.03f, 0.05f);
            var cyan = new Color(0.25f, 0.9f, 1f);
            var amber = new Color(1f, 0.65f, 0.15f);
            var green = new Color(0.35f, 1f, 0.5f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Color c = background;
                bool border = x < 6 || y < 6 || x > size - 7 || y > size - 7;
                bool grid = x % 32 == 0 || y % 32 == 0;
                if (grid) c += cyan * 0.07f;
                if (y > size - 36 && y < size - 12 && x > 12 && x < size * 0.6f) c = cyan * 0.55f; // bandeau titre
                switch (variant)
                {
                    case 0:
                        float wave = size * 0.42f + size * 0.16f * Mathf.Sin(x * 0.07f) * Mathf.Sin(x * 0.013f + 1f);
                        if (Mathf.Abs(y - wave) < 2.2f) c = cyan;
                        float wave2 = size * 0.25f + size * 0.06f * Mathf.Sin(x * 0.19f + 2f);
                        if (Mathf.Abs(y - wave2) < 1.5f) c = amber * 0.9f;
                        break;
                    case 1:
                        int bar = (x - 16) / 22;
                        float barHeight = 30f + 120f * Hash(bar, 0, 59);
                        if (x > 16 && x < size - 16 && (x - 16) % 22 < 15 && y > 20 && y < 20 + barHeight) c = bar % 3 == 0 ? amber : green * 0.85f;
                        break;
                    default:
                        int line = (size - 50 - y) / 14;
                        float length = 40f + (size - 80) * Hash(line, 1, 61);
                        if (y < size - 50 && y > 14 && (size - 50 - y) % 14 < 7 && x > 16 && x < 16 + length && Hash(x / 6, line, 67) > 0.2f)
                            c = line % 5 == 0 ? amber : green * 0.9f;
                        break;
                }
                if (border) c = new Color(0.05f, 0.06f, 0.07f);
                pixels[y * size + x] = c;
            }
            return pixels;
        }

        static Texture2D Png(string name, Color[] pixels, int size, bool normalMap)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            string path = ArtDir + "/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normalMap;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 2;
            importer.maxTextureSize = size;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
