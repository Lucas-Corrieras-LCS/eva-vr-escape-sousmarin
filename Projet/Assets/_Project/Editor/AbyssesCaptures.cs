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
                    // Face au hublot sur l'océan (module ORANGE, x = 1,1) : comme le visage collé à la vitre.
                    new View("etage1_hublot_ocean", new Vector3(1.1f, 1.55f, 0.95f), new Vector3(1.1f, 1.0f, -4f)),
                    new View("etage1_ocean_bureau", new Vector3(2.4f, 1.55f, 2.9f), new Vector3(1.1f, 1.45f, 0.3f)),
                    new View("etage1_disjoncteurs", new Vector3(1.7f, 1.45f, 2.4f), new Vector3(0.34f, 1.3f, 2.4f)),
                    new View("etage1_note", new Vector3(2.5f, 1.45f, 1.45f), new Vector3(2.1f, 0.79f, 0.62f)),
                    new View("etage1_fusible", new Vector3(5.0f, 1.35f, 1.05f), new Vector3(5.0f, 1.2f, 0.34f)),
                    new View("etage1_ocean_casiers", new Vector3(13.2f, 1.55f, 2.9f), new Vector3(13.9f, 1.45f, 0.3f)),
                    // Relais d'appels du module Q0, sur le mur extérieur (x local 1,55).
                    new View("etage1_manuel", new Vector3(9.05f, 1.55f, 1.6f), new Vector3(9.05f, 1.35f, 0.38f)),
                    new View("etage1_verrou", new Vector3(8.5f, 1.5f, 6.4f), new Vector3(8.5f, 1.3f, 7.62f)),
                    // Pupitre d'écoute du code frappé, côté ORANGE de la cloison commune avec CYAN.
                    new View("etage1_frappe", new Vector3(13.3f, 1.6f, 7.1f), new Vector3(14.66f, 1.7f, 7.1f)),
                }
            },
            {
                "Etage2_Labo", new[]
                {
                    new View("etage2_module", new Vector3(13.5f, 2f, 1.2f), new Vector3(2f, 1.4f, 6f)),
                    new View("etage2_manometre", new Vector3(5f, 1.6f, 2.3f), new Vector3(4.5f, 1.4f, 0.34f)),
                    new View("etage2_morse", new Vector3(1.75f, 1.45f, 3.7f), new Vector3(0.34f, 1.35f, 3.7f)),
                    new View("etage2_balise", new Vector3(13.9f, 1.5f, 1.5f), new Vector3(13.9f, 1.45f, -2.8f)),
                    new View("etage2_hublot_ocean", new Vector3(1.1f, 1.55f, 0.95f), new Vector3(1.1f, 1.0f, -4f)),
                    new View("etage2_ocean", new Vector3(13.2f, 1.55f, 2.9f), new Vector3(13.9f, 1.45f, 0.3f)),
                    new View("etage2_fuite", new Vector3(4f, 1.7f, 4.2f), new Vector3(0.45f, 2.1f, 1.7f)),
                    new View("etage2_relais", new Vector3(9.05f, 1.55f, 1.75f), new Vector3(9.05f, 1.3f, 0.38f)),
                    // Radio de la balise du module CYAN, chez ORANGE (au-dessus du hublot du fond).
                    new View("etage2_radio_balise", new Vector3(7.0f, 1.55f, 1.4f), new Vector3(7.0f, 1.42f, 0.38f)),
                    // Antidote : mélangeur d'ORANGE (mur extérieur), recette et tableau UV (mur du fond).
                    new View("etage2_melangeur", new Vector3(10f, 1.55f, 1.65f), new Vector3(10f, 1.25f, 0.38f)),
                    new View("etage2_recette", new Vector3(1.9f, 1.5f, 1.5f), new Vector3(0.34f, 1.4f, 1.5f)),
                }
            },
            {
                "Etage3_Ballasts", new[]
                {
                    new View("etage3b_hublot_ocean", new Vector3(1.1f, 1.55f, 0.95f), new Vector3(1.1f, 1.0f, -4f)),
                    new View("etage3b_module", new Vector3(13.5f, 2f, 1.2f), new Vector3(2f, 1.2f, 6f)),
                    // Collecteur de vannes du module ORANGE (mur du fond) et schéma du collecteur CYAN, chez ORANGE.
                    new View("etage3b_collecteur", new Vector3(1.75f, 1.4f, 4f), new Vector3(0.34f, 1.15f, 4f)),
                    new View("etage3b_schema", new Vector3(1.7f, 1.4f, 2.3f), new Vector3(0.34f, 1.3f, 2.3f)),
                    new View("etage3b_pompe", new Vector3(4.5f, 1.5f, 2.8f), new Vector3(2.9f, 0.7f, 1.05f)),
                    new View("etage3b_surveillance", new Vector3(9.05f, 1.55f, 1.6f), new Vector3(9.05f, 1.35f, 0.38f)),
                    new View("etage3b_cale", new Vector3(8.4f, 2.3f, 1.6f), new Vector3(11.2f, 0f, 4.2f)),
                    new View("etage3b_cloisons", new Vector3(10.8f, 1.5f, 6.3f), new Vector3(10.8f, 1.3f, 7.62f)),
                    // Contrepoids : plateau de pesée et étagère des lests d'ORANGE, registre sur le mur extérieur.
                    new View("etage3b_balance", new Vector3(3.4f, 1.7f, 5.3f), new Vector3(3.0f, 0.85f, 7.0f)),
                    new View("etage3b_registre", new Vector3(5.1f, 1.55f, 1.6f), new Vector3(5.1f, 1.4f, 0.38f)),
                }
            },
            {
                "Etage4_Coeur", new[]
                {
                    new View("etage3_reacteur", new Vector3(9f, 1.7f, 2f), new Vector3(15.5f, 1.4f, 8.25f)),
                    new View("etage3_salle", new Vector3(1.5f, 2.2f, 1.2f), new Vector3(20f, 0.8f, 12f)),
                    new View("etage4_hublot_ocean", new Vector3(1.1f, 1.55f, 0.95f), new Vector3(1.1f, 1.0f, -4f)),
                    new View("etage3_ocean", new Vector3(2.4f, 1.55f, 2.9f), new Vector3(1.1f, 1.45f, 0.3f)),
                    new View("etage4_sonar", new Vector3(10.76f, 1.5f, 1.5f), new Vector3(10.76f, 1.35f, 0.44f)),
                    new View("etage4_standard", new Vector3(1.6f, 1.5f, 3.0f), new Vector3(0.34f, 1.3f, 3.0f)),
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

                // Image « à blanc » : la première prise de vue part avant que les lightmaps de l'étage soient chargées
                // (image noire ou incomplète). On la jette et on capture ensuite pour de bon.
                if (floor.Value.Length > 0) Render(floor.Value[0], background, Path.Combine(Path.GetTempPath(), "abysses_warmup.png"));
                foreach (View view in floor.Value)
                    Render(view, background, Path.Combine(dir, view.Name + ".png"));

                if (floor.Key == "Etage4_Coeur")
                {
                    ShowFinale(scene);
                    Render(new View("etage3_final", new Vector3(9f, 1.6f, 2.5f), new Vector3(15.5f, 2.2f, 8.25f)), RenderSettings.fogColor, Path.Combine(dir, "etage3_final.png"));
                }
            }
            CaptureAvatar(dir);
            CaptureSurface(dir);
            EditorSceneManager.OpenScene(SceneDir + "/Station.unity", OpenSceneMode.Single);
            Debug.Log("[Abysses] Captures enregistrées dans " + dir);
        }

        /// <summary>Avatar d'un coéquipier (scaphandrier CYAN) posé dans l'étage 1 : de face et de trois quarts.</summary>
        [MenuItem("Abysses/Captures : avatar seulement", priority = 21)]
        public static void CaptureAvatarOnly()
        {
            CaptureAvatar(Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/captures")));
            EditorSceneManager.OpenScene(SceneDir + "/Station.unity", OpenSceneMode.Single);
        }

        static void CaptureAvatar(string dir)
        {
            Scene station = EditorSceneManager.OpenScene(SceneDir + "/Station.unity", OpenSceneMode.Single);
            foreach (GameObject root in station.GetRootGameObjects()) root.SetActive(false);
            Scene scene = EditorSceneManager.OpenScene(SceneDir + "/Etage1_Habitat.unity", OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            LightProbes.Tetrahedralize();
            Color background = ApplyAmbience(scene);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/NetworkPlayer.prefab");
            var avatar = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var player = avatar.GetComponent<Abysses.Network.NetworkPlayer>();
            var tinted = (Renderer[])player.GetType().GetField("tintedRenderers", flags).GetValue(player);
            var materials = (Material[])player.GetType().GetField("quadrantMaterials", flags).GetValue(player);
            foreach (Renderer r in tinted) r.sharedMaterial = materials[1];

            // Pose : debout près de la cabine, regard vers -Z, une main levée (signe), l'autre le long du corps.
            Transform t = avatar.transform;
            var center = new Vector3(5.2f, 0f, 4.6f);
            Quaternion look = Quaternion.LookRotation(Vector3.back);
            t.Find("Head").SetPositionAndRotation(center + new Vector3(0f, 1.65f, 0f), look);
            t.Find("LeftHand").SetPositionAndRotation(center + new Vector3(-0.3f, 0.95f, -0.12f), look * Quaternion.Euler(70f, 0f, 0f));
            t.Find("RightHand").SetPositionAndRotation(center + new Vector3(0.38f, 1.55f, -0.28f), look * Quaternion.Euler(-20f, 0f, 0f));
            var body = avatar.GetComponent<Abysses.Network.AvatarBody>();
            var lateUpdate = body.GetType().GetMethod("LateUpdate", flags);
            for (int i = 0; i < 60; i++) lateUpdate.Invoke(body, null);

            Render(new View("avatar_face", center + new Vector3(0.1f, 1.45f, -1.9f), center + new Vector3(0f, 1.05f, 0f)), background, Path.Combine(dir, "avatar_face.png"));
            Render(new View("avatar_profil", center + new Vector3(1.6f, 1.6f, -1.2f), center + new Vector3(0f, 1.1f, 0f)), background, Path.Combine(dir, "avatar_profil.png"));
            Render(new View("avatar_casque", center + new Vector3(0.25f, 1.7f, -0.6f), center + new Vector3(0f, 1.6f, 0f)), background, Path.Combine(dir, "avatar_casque.png"));
            Object.DestroyImmediate(avatar);
        }

        /// <summary>Surface : plateforme vue du pont, cabine et potence, et vue par le hublot gauche d'une cabine.</summary>
        static void CaptureSurface(string dir)
        {
            Scene station = EditorSceneManager.OpenScene(SceneDir + "/Station.unity", OpenSceneMode.Single);
            foreach (GameObject root in station.GetRootGameObjects())
                if (root.name != "Cabins" && root.name != "Surface") root.SetActive(false);
            var surface = Object.FindFirstObjectByType<SurfaceWorld>();
            var so = new SerializedObject(surface);
            Color sky = so.FindProperty("sky").colorValue;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 80f;
            RenderSettings.fogEndDistance = 520f;
            RenderSettings.fogColor = so.FindProperty("horizon").colorValue;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = so.FindProperty("surfaceAmbient").colorValue;
            var surfacePalette = (Abysses.Core.AbyssPalette)typeof(SurfaceWorld).GetField("surfacePalette",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(surface);
            Abysses.Core.AbyssStyle.Apply(surfacePalette);
            Render(new View("surface_plateforme", new Vector3(3.2f, 1.7f, 1.2f), new Vector3(9f, 2.2f, 4.5f)), sky, Path.Combine(dir, "surface_plateforme.png"), 600f);
            Render(new View("surface_large", new Vector3(14f, 1.7f, 8f), new Vector3(-60f, 6f, 20f)), sky, Path.Combine(dir, "surface_large.png"), 600f);
            Render(new View("surface_hublot", new Vector3(7.5f, 1.55f, 4f), new Vector3(5f, 1.4f, 4f)), sky, Path.Combine(dir, "surface_hublot.png"), 600f);

            // Menu principal (état hors ligne), devant la plateforme.
            foreach (GameObject root in station.GetRootGameObjects())
            {
                var menu = root.GetComponentInChildren<Abysses.UI.MainMenu>(true);
                if (menu == null) continue;
                root.SetActive(true);
                // Seul le menu : pas de voile d'alerte, de montre ni de mains dans l'image.
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                    if (!r.transform.IsChildOf(menu.transform)) r.enabled = false;
                Transform online = menu.transform.Find("Panel/Online");
                Transform board = menu.transform.Find("Panel/Leaderboard");
                if (online != null) online.gameObject.SetActive(false);
                if (board != null) board.gameObject.SetActive(false);
                Canvas.ForceUpdateCanvases();
                Vector3 center = menu.transform.position;
                Render(new View("menu_principal", center - menu.transform.forward * 1.05f, center), sky, Path.Combine(dir, "menu_principal.png"), 600f);
                if (online != null) online.gameObject.SetActive(true);
                Transform offline = menu.transform.Find("Panel/Offline");
                if (offline != null) offline.gameObject.SetActive(false);
                Canvas.ForceUpdateCanvases();
                Render(new View("menu_en_partie", center - menu.transform.forward * 1.05f, center), sky, Path.Combine(dir, "menu_en_partie.png"), 600f);
            }
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
                // Palette de la direction artistique (posée en jeu par FloorAmbience).
                Abysses.Core.AbyssStyle.Apply(ambience.Palette);
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

        static void Render(View view, Color background, string path, float farClip = 40f)
        {
            // Halos lumineux tournés vers la caméra (en jeu, c'est HaloBillboards qui s'en charge à chaque image).
            foreach (HaloBillboards billboards in Object.FindObjectsByType<HaloBillboards>(FindObjectsSortMode.None))
            {
                var halos = (Transform[])typeof(HaloBillboards).GetField("halos", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(billboards);
                foreach (Transform halo in halos)
                    if (halo != null && (halo.position - view.Eye).sqrMagnitude > 0.0001f) halo.rotation = Quaternion.LookRotation(halo.position - view.Eye);
            }
            var go = new GameObject("CaptureCamera");
            var camera = go.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(view.Eye, Quaternion.LookRotation(view.Target - view.Eye));
            camera.fieldOfView = 80f;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = farClip;
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
