using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Abysses.Level;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Captures d'écran des étages (docs/captures) sans lancer le jeu ni porter le casque :
    /// Station + étage ouverts ensemble, ambiance appliquée comme en jeu, particules pré-simulées.
    /// Sert à vérifier la lumière et la DA après un bake, et à illustrer le rapport.
    /// </summary>
    public static class AbyssesCaptures
    {
        const string SceneDir = "Assets/_Project/Scenes";
        const int Width = 1600, Height = 900;

        struct View
        {
            public string Name;
            public Vector3 Eye, Target;
            public View(string name, Vector3 eye, Vector3 target) { Name = name; Eye = eye; Target = target; }
        }

        static readonly Dictionary<string, View[]> Views = new Dictionary<string, View[]>
        {
            {
                "Etage1_Habitat", new[]
                {
                    new View("etage1_module", new Vector3(1.2f, 2.1f, 1.2f), new Vector3(12f, 1f, 6f)),
                    new View("etage1_hublot", new Vector3(6f, 1.6f, 2f), new Vector3(14.7f, 1.6f, 5.5f)),
                    new View("etage1_digicode", new Vector3(9.4f, 1.55f, 1.5f), new Vector3(10f, 1.3f, 0.34f)),
                    // Hublots sur l'océan, aux deux bouts du mur extérieur du module Q0 (x = 1,1 et 13,9).
                    new View("etage1_ocean_bureau", new Vector3(2.4f, 1.55f, 2.9f), new Vector3(1.1f, 1.45f, 0.3f)),
                    new View("etage1_ocean_casiers", new Vector3(13.2f, 1.55f, 2.9f), new Vector3(13.9f, 1.45f, 0.3f)),
                }
            },
            {
                "Etage2_Labo", new[]
                {
                    new View("etage2_module", new Vector3(13.5f, 2f, 1.2f), new Vector3(2f, 1.4f, 6f)),
                    new View("etage2_manometre", new Vector3(5f, 1.6f, 2.3f), new Vector3(4.5f, 1.4f, 0.34f)),
                    new View("etage2_ocean", new Vector3(13.2f, 1.55f, 2.9f), new Vector3(13.9f, 1.45f, 0.3f)),
                    new View("etage2_fuite", new Vector3(4f, 1.7f, 4.2f), new Vector3(0.45f, 2.1f, 1.7f)),
                }
            },
            {
                "Etage3_Coeur", new[]
                {
                    new View("etage3_reacteur", new Vector3(9f, 1.7f, 2f), new Vector3(15.5f, 1.4f, 8.25f)),
                    new View("etage3_salle", new Vector3(1.5f, 2.2f, 1.2f), new Vector3(20f, 0.8f, 12f)),
                    new View("etage3_ocean", new Vector3(2.4f, 1.55f, 2.9f), new Vector3(1.1f, 1.45f, 0.3f)),
                }
            },
        };

        /// <summary>Chaîne complète en une commande (utile en batchmode) : construction, bake, captures.</summary>
        public static void BuildBakeCapture()
        {
            AbyssesBuilder.BuildAllAndBake();
            CaptureAll();
        }

        [MenuItem("Abysses/Captures d'écran des étages", priority = 20)]
        public static void CaptureAll()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/captures"));
            Directory.CreateDirectory(dir);

            foreach (KeyValuePair<string, View[]> floor in Views)
            {
                Scene station = EditorSceneManager.OpenScene(SceneDir + "/Station.unity", OpenSceneMode.Single);
                // On ne garde de la Station que les cabines (pas de rig XR, de menu ni de simulateur dans l'image).
                foreach (GameObject root in station.GetRootGameObjects())
                    if (root.name != "Cabins") root.SetActive(false);

                Scene scene = EditorSceneManager.OpenScene(SceneDir + "/" + floor.Key + ".unity", OpenSceneMode.Additive);
                // Dans l'éditeur, seules les données d'éclairage de la scène active sont affichées.
                SceneManager.SetActiveScene(scene);
                LightProbes.Tetrahedralize();
                Color background = ApplyAmbience(scene);
                foreach (GameObject root in scene.GetRootGameObjects())
                foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>())
                    if (ps.main.playOnAwake) ps.Simulate(12f, true, true);

                foreach (View view in floor.Value)
                    Render(view, background, Path.Combine(dir, view.Name + ".png"));

                if (floor.Key == "Etage3_Coeur")
                {
                    ShowFinale(scene);
                    Render(new View("etage3_final", new Vector3(9f, 1.6f, 2.5f), new Vector3(15.5f, 2.2f, 8.25f)), RenderSettings.fogColor, Path.Combine(dir, "etage3_final.png"));
                }
            }
            EditorSceneManager.OpenScene(SceneDir + "/Station.unity", OpenSceneMode.Single);
            Debug.Log("[Abysses] Captures enregistrées dans " + dir);
        }

        /// <summary>Applique brouillard et lumière ambiante de l'étage (FloorAmbience ne tourne qu'en jeu).</summary>
        static Color ApplyAmbience(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var ambience = root.GetComponentInChildren<FloorAmbience>();
                if (ambience == null) continue;
                var so = new SerializedObject(ambience);
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = so.FindProperty("fogColor").colorValue;
                RenderSettings.fogDensity = so.FindProperty("fogDensity").floatValue;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = so.FindProperty("ambientColor").colorValue;
                return RenderSettings.fogColor;
            }
            return Color.black;
        }

        static void ShowFinale(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var finale = root.GetComponentInChildren<FloorFinale>();
                if (finale == null) continue;
                var so = new SerializedObject(finale);
                var beam = (Light)so.FindProperty("sunbeam").objectReferenceValue;
                if (beam != null) beam.intensity = so.FindProperty("sunbeamIntensity").floatValue;
                var shaft = (Renderer)so.FindProperty("sunShaft").objectReferenceValue;
                if (shaft != null) shaft.enabled = true;
                RenderSettings.fogColor = so.FindProperty("surfaceFog").colorValue;
                RenderSettings.fogDensity = so.FindProperty("surfaceFogDensity").floatValue;
                RenderSettings.ambientLight = so.FindProperty("surfaceAmbient").colorValue;
                foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>())
                    if (!ps.main.playOnAwake) ps.Simulate(3f, true, true);
            }
        }

        static void Render(View view, Color background, string path)
        {
            var go = new GameObject("CaptureCamera");
            var camera = go.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(view.Eye, Quaternion.LookRotation(view.Target - view.Eye));
            camera.fieldOfView = 80f;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 40f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
                RenderPipeline.SubmitRenderRequest(camera, request);
            else
            {
                camera.targetTexture = rt;
                camera.Render();
                camera.targetTexture = null;
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, image.EncodeToPNG());

            Object.DestroyImmediate(image);
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
        }
    }
}
