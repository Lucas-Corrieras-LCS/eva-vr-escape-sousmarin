using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Abysses.Core;
using Abysses.Interaction;
using Abysses.Level;
using Abysses.Network;
using Abysses.Player;
using Abysses.UI;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Construction reproductible du projet (menu Abysses) : matériaux, prefabs, scène Station et étages en blockout.
    /// Relançable à volonté : les assets existants sont mis à jour, les scènes régénérées.
    /// Le blockout sert de base : l'habillage artistique se fait ensuite à la main sur ces scènes.
    /// </summary>
    public static partial class AbyssesBuilder
    {
        const string Root = "Assets/_Project";
        const string MaterialDir = Root + "/Materials";
        const string PrefabDir = Root + "/Prefabs";
        const string SceneDir = Root + "/Scenes";
        const string StarterAssets = "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets";
        const string XROriginPath = StarterAssets + "/Prefabs/XR Origin (XR Rig).prefab";
        const string SimulatorPath = "Assets/Samples/XR Interaction Toolkit/3.3.2/XR Interaction Simulator/XR Interaction Simulator.prefab";
        const string ParticleMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

        static readonly string[] FloorScenes = { "Etage1_Habitat", "Etage2_Labo", "Etage3_Coeur" };
        // Couleurs des joueurs : teintes très éloignées pour les distinguer d'un coup d'œil (réacteur, avatars).
        static readonly string[] QuadrantNames = { "ORANGE", "CYAN", "VERT", "MAGENTA" };
        static readonly Color[] QuadrantColors =
        {
            new Color(1f, 0.38f, 0.05f), new Color(0.1f, 0.75f, 1f), new Color(0.25f, 1f, 0.3f), new Color(1f, 0.15f, 0.65f),
        };
        static readonly string[] QuadrantNamesEn = { "ORANGE", "CYAN", "GREEN", "MAGENTA" };
        static readonly string[] SymbolNames = { "ROUGE", "VERT", "BLEU", "JAUNE", "BLANC", "VIOLET" };
        static readonly string[] SymbolNamesEn = { "RED", "GREEN", "BLUE", "YELLOW", "WHITE", "PURPLE" };
        static readonly Color[] SymbolColors =
        {
            new Color(0.9f, 0.12f, 0.08f), new Color(0.15f, 0.85f, 0.25f), new Color(0.15f, 0.4f, 1f),
            new Color(1f, 0.85f, 0.1f), new Color(0.95f, 0.95f, 0.95f), new Color(0.65f, 0.2f, 0.95f),
        };

        const float WallHeight = 3.2f;

        /// <summary>Rendering layer 1 : éclairé par la lampe de cabine (cabines + avatars).</summary>
        const uint CabinLayer = 1u << 1;

        /// <summary>Sons et textures générés (voir <see cref="AbyssesContent"/>).</summary>
        static AbyssesContent.Content fx;
        /// <summary>Textures et maillages de l'habillage (voir <see cref="AbyssesArt"/>).</summary>
        static AbyssesArt.Kit art;
        const float WallThickness = 0.2f;

        // ------------------------------------------------------------------ Menus

        [MenuItem("Abysses/Tout construire", priority = 0)]
        public static void BuildAll()
        {
            // Les scènes doivent être dans les Build Settings AVANT leur sauvegarde (identifiants réseau des objets placés).
            SetBuildScenes();
            fx = AbyssesContent.Build();
            art = AbyssesArt.Build();
            Mats m = BuildMaterials();
            BuildConfig();
            GameObject player = BuildPlayerPrefab(m);
            GameObject cabin = BuildCabinPrefab(m);
            BuildFloor1(m);
            BuildFloor2(m);
            BuildFloor3(m);
            BuildStation(m, player, cabin);
            AssetDatabase.SaveAssets();
            Debug.Log("[Abysses] Construction terminée. Penser à baker l'éclairage (Abysses → Baker l'éclairage des étages).");
        }

        /// <summary>
        /// Régénère uniquement la Station (menu, réseau, rig, cabines) et ses prefabs, sans toucher aux étages :
        /// leur éclairage baké reste valide (évite de recommiter des Mo de lightmaps identiques).
        /// </summary>
        [MenuItem("Abysses/Reconstruire la Station seulement", priority = 3)]
        public static void BuildStationOnly()
        {
            SetBuildScenes();
            fx = AbyssesContent.Build();
            art = AbyssesArt.Build();
            Mats m = BuildMaterials();
            BuildConfig();
            GameObject player = BuildPlayerPrefab(m);
            GameObject cabin = BuildCabinPrefab(m);
            BuildStation(m, player, cabin);
            AssetDatabase.SaveAssets();
            Debug.Log("[Abysses] Station reconstruite (étages et éclairage inchangés).");
        }

        [MenuItem("Abysses/Tout construire + baker l'éclairage", priority = 1)]
        public static void BuildAllAndBake()
        {
            BuildAll();
            BakeFloors();
        }

        /// <summary>Bake des lightmaps, light probes et reflection probes de chaque étage (scènes régénérées = bake à refaire).</summary>
        [MenuItem("Abysses/Baker l'éclairage des étages", priority = 2)]
        public static void BakeFloors()
        {
            foreach (string floor in FloorScenes)
            {
                Scene scene = EditorSceneManager.OpenScene(SceneDir + "/" + floor + ".unity", OpenSceneMode.Single);
                var timer = System.Diagnostics.Stopwatch.StartNew();
                bool ok = true;
                // Étage 1 : deux bakes (courant rétabli puis secours), échangés en jeu par PowerRestore.
                if (floor == FloorScenes[0]) BakePowerVariants(scene);
                else ok = Lightmapping.Bake();
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[Abysses] Bake {floor} : {(ok ? "OK" : "ÉCHEC")} en {timer.Elapsed.TotalSeconds:F0} s");
            }
            EditorSceneManager.OpenScene(SceneDir + "/Station.unity", OpenSceneMode.Single);
        }

        // ------------------------------------------------------------------ Matériaux

        class Mats
        {
            public Material Hull, Panel, Floor, Glass, EmitCyan, EmitWarm, EmitRed, EmitPurple, Visor, SlotEmpty, StatusIdle, StatusOk, StatusError, Warning, Crate;
            public Material Key, ShaftRed, SunShaft, PlanktonHabitat, PlanktonLabo, PlanktonCoeur;
            /// <summary>Néon éteint (panne), lampe de secours ambre, halo de survol, tuyauterie, eau, créatures des hublots.</summary>
            public Material NeonOff, EmitAmber, Highlight, Pipe, Water, Fish, Leviathan, GlowBio, GlowAngler, Snow, Drop;
            // Habillage
            public Material HullDark, Ceiling, Frame, Brass, Rubber, PipeYellow, PipeRed, PipeGrey, Hazard, Locker, Liquid, WarmBulb;
            // Étage 2 (laboratoires) : coque peinte claire, faïence, flaques, éclairage de paillasse froid, vapeur.
            public Material HullLab, LabTile, Puddle, EmitCold, Steam;
            public Material[] Screens = new Material[3];
            public Material[] Avatar = new Material[4];
            /// <summary>Couleur seule (fusibles, anneaux).</summary>
            public Material[] Symbols = new Material[6];
            /// <summary>Forme + couleur (digicode) : lisible sans distinguer les couleurs.</summary>
            public Material[] Glyphs = new Material[6];
        }

        static Mats BuildMaterials()
        {
            EnsureFolder(MaterialDir);
            var m = new Mats
            {
                // Peu métalliques : un métal ne renvoie presque pas de lumière diffuse, les lightmaps n'auraient aucun effet.
                Hull = LitTex("M_Hull", new Color(0.5f, 0.57f, 0.62f), 0.3f, 0.5f, art.HullAlbedo, art.HullNormal, 1f),
                HullDark = LitTex("M_HullDark", new Color(0.2f, 0.23f, 0.27f), 0.4f, 0.45f, art.HullAlbedo, art.HullNormal, 1f),
                Ceiling = LitTex("M_Ceiling", new Color(0.42f, 0.46f, 0.5f), 0.3f, 0.35f, art.CeilingAlbedo, art.CeilingNormal, 1f),
                Floor = LitTex("M_Floor", new Color(0.62f, 0.64f, 0.66f), 0.55f, 0.5f, art.PlateAlbedo, art.PlateNormal, 1f),
                Locker = LitTex("M_Locker", new Color(0.4f, 0.52f, 0.58f), 0.35f, 0.4f, art.LockerAlbedo, art.LockerNormal, 1f),
                Hazard = LitTex("M_Hazard", Color.white, 0.1f, 0.35f, art.Hazard, null, 1f),
                Panel = Lit("M_Panel", new Color(0.3f, 0.34f, 0.38f), 0.35f, 0.45f),
                Frame = Lit("M_Frame", new Color(0.09f, 0.1f, 0.12f), 0.6f, 0.6f),
                Brass = Lit("M_Brass", new Color(0.72f, 0.52f, 0.24f), 0.85f, 0.62f),
                Rubber = Lit("M_Rubber", new Color(0.035f, 0.035f, 0.04f), 0f, 0.3f),
                PipeYellow = Lit("M_Pipe_Yellow", new Color(0.8f, 0.55f, 0.08f), 0.2f, 0.5f),
                PipeRed = Lit("M_Pipe_Red", new Color(0.55f, 0.07f, 0.05f), 0.2f, 0.5f),
                PipeGrey = Lit("M_Pipe_Grey", new Color(0.42f, 0.45f, 0.48f), 0.65f, 0.55f),
                Liquid = Lit("M_Liquid", new Color(0.04f, 0.2f, 0.12f), 0f, 0.9f, new Color(0.25f, 1f, 0.55f) * 1.6f),
                WarmBulb = Lit("M_WarmBulb", new Color(0.3f, 0.2f, 0.1f), 0f, 0.6f, new Color(1f, 0.68f, 0.35f) * 3f),
                Crate = Lit("M_Crate", new Color(0.35f, 0.3f, 0.22f), 0.1f, 0.3f),
                HullLab = LitTex("M_HullLab", new Color(0.8f, 0.78f, 0.73f), 0.15f, 0.5f, art.HullAlbedo, art.HullNormal, 1f),
                LabTile = LitTex("M_LabTile", new Color(0.9f, 0.94f, 0.94f), 0f, 0.7f, art.LabTileAlbedo, art.LabTileNormal, 1f),
                // Sol mouillé plutôt que flaque noire : même tôle, plus sombre, reliefs noyés, très lisse.
                Puddle = LitTex("M_Puddle", new Color(0.3f, 0.31f, 0.32f), 0.3f, 0.94f, art.PlateAlbedo, art.PlateNormal, 0.25f),
                EmitCold = Lit("M_Emit_Cold", new Color(0.2f, 0.22f, 0.24f), 0f, 0.5f, new Color(0.75f, 0.95f, 1f) * 2.2f),
                Steam = Additive("M_Steam", "Universal Render Pipeline/Particles/Unlit", new Color(1f, 0.9f, 0.85f, 0.1f), fx.Dot),
                Glass = Transparent("M_Glass", new Color(0.35f, 0.65f, 0.75f, 0.18f), false),
                EmitCyan = Lit("M_Emit_Cyan", new Color(0.05f, 0.2f, 0.25f), 0f, 0.5f, new Color(0.2f, 0.85f, 1f) * 2.5f),
                EmitWarm = Lit("M_Emit_Warm", new Color(0.25f, 0.15f, 0.05f), 0f, 0.5f, new Color(1f, 0.55f, 0.25f) * 2f),
                EmitRed = Lit("M_Emit_Red", new Color(0.25f, 0.02f, 0.02f), 0f, 0.5f, new Color(1f, 0.1f, 0.05f) * 2f),
                EmitPurple = Lit("M_Emit_Purple", new Color(0.12f, 0.03f, 0.2f), 0f, 0.5f, new Color(0.6f, 0.2f, 1f) * 2.5f),
                Visor = Lit("M_Visor", new Color(0.02f, 0.03f, 0.05f), 0.9f, 0.95f),
                SlotEmpty = Unlit("M_SlotEmpty", new Color(0.05f, 0.06f, 0.07f)),
                StatusIdle = Lit("M_Status_Idle", Color.black, 0f, 0.5f, new Color(1f, 0.6f, 0.1f) * 2f),
                StatusOk = Lit("M_Status_Ok", Color.black, 0f, 0.5f, new Color(0.2f, 1f, 0.35f) * 2.5f),
                StatusError = Lit("M_Status_Error", Color.black, 0f, 0.5f, new Color(1f, 0.08f, 0.05f) * 2.5f),
                Warning = Transparent("M_ZoneWarning", new Color(0.8f, 0.05f, 0.02f, 0f), true),
                Key = Lit("M_Key", new Color(0.07f, 0.08f, 0.09f), 0.2f, 0.6f),
                ShaftRed = Additive("M_Shaft_Red", "Universal Render Pipeline/Unlit", new Color(1f, 0.12f, 0.05f, 0.14f), fx.ShaftGradient),
                SunShaft = Additive("M_Shaft_Sun", "Universal Render Pipeline/Unlit", new Color(0.75f, 0.92f, 1f, 0.35f), fx.ShaftGradient),
                PlanktonHabitat = Additive("M_Plankton_Habitat", "Universal Render Pipeline/Particles/Unlit", new Color(0.6f, 0.95f, 1f, 0.5f), fx.Dot),
                PlanktonLabo = Additive("M_Plankton_Labo", "Universal Render Pipeline/Particles/Unlit", new Color(1f, 0.6f, 0.35f, 0.35f), fx.Dot),
                PlanktonCoeur = Additive("M_Plankton_Coeur", "Universal Render Pipeline/Particles/Unlit", new Color(1f, 1f, 1f, 0.8f), fx.Dot),
                NeonOff = Lit("M_Neon_Off", new Color(0.12f, 0.14f, 0.15f), 0.1f, 0.7f),
                EmitAmber = Lit("M_Emit_Amber", new Color(0.25f, 0.12f, 0.02f), 0f, 0.5f, new Color(1f, 0.5f, 0.08f) * 2.2f),
                Highlight = Additive("M_Highlight", "Universal Render Pipeline/Unlit", new Color(0.35f, 0.9f, 1f, 0.28f), null),
                Pipe = Lit("M_Pipe", new Color(0.3f, 0.32f, 0.33f), 0.6f, 0.55f),
                Water = Transparent("M_Water", new Color(0.12f, 0.3f, 0.35f, 0.55f), false),
                Fish = ParticleAlpha("M_Fish", new Color(0.03f, 0.08f, 0.1f, 0.9f), fx.Fish),
                Leviathan = Transparent("M_Leviathan", new Color(0.01f, 0.02f, 0.03f, 0.85f), true),
                GlowBio = Additive("M_Glow_Bio", "Universal Render Pipeline/Particles/Unlit", new Color(1f, 1f, 1f, 0.9f), fx.Dot),
                GlowAngler = Additive("M_Glow_Angler", "Universal Render Pipeline/Particles/Unlit", new Color(1f, 0.8f, 0.4f, 0.9f), fx.Dot),
                Snow = Additive("M_MarineSnow", "Universal Render Pipeline/Particles/Unlit", new Color(0.85f, 0.95f, 1f, 0.45f), fx.Dot),
                Drop = Additive("M_Drop", "Universal Render Pipeline/Particles/Unlit", new Color(0.7f, 0.9f, 1f, 0.8f), fx.Dot),
            };
            m.Leviathan.SetTexture("_BaseMap", fx.Leviathan);
            // Vue depuis l'intérieur : la silhouette doit rester visible quel que soit le côté.
            m.Leviathan.SetFloat("_Cull", (float)CullMode.Off);
            EditorUtility.SetDirty(m.Leviathan);
            m.Screens[0] = Screen("M_Screen_Wave", art.ScreenWave);
            m.Screens[1] = Screen("M_Screen_Bars", art.ScreenBars);
            m.Screens[2] = Screen("M_Screen_Text", art.ScreenText);
            for (int i = 0; i < 4; i++)
                m.Avatar[i] = Lit("M_Avatar_Q" + i, QuadrantColors[i] * 0.6f, 0.3f, 0.6f, QuadrantColors[i]);
            for (int i = 0; i < 6; i++)
            {
                m.Symbols[i] = Unlit("M_Symbol_" + i, SymbolColors[i]);
                m.Glyphs[i] = Glyph("M_Glyph_" + i, SymbolColors[i], fx.Glyphs[i]);
            }
            AssetDatabase.SaveAssets();
            return m;
        }

        static Material GetOrCreate(string name, string shader)
        {
            string path = MaterialDir + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader.name != shader)
            {
                material.shader = Shader.Find(shader);
            }
            material.enableInstancing = true;
            return material;
        }

        static Material Lit(string name, Color color, float metallic, float smoothness, Color? emission = null)
        {
            Material material = GetOrCreate(name, "Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", color);
            // Ne pas écrire _Color : URP la synchronise lui-même depuis _BaseColor (avec ses arrondis) ;
            // l'écrire ici ferait osciller la valeur à chaque génération / build (faux diffs git).
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Lit avec albédo (et normal map) : la couleur du matériau teinte la texture grise.</summary>
        static Material LitTex(string name, Color color, float metallic, float smoothness, Texture2D albedo, Texture2D normal, float normalScale)
        {
            Material material = Lit(name, color, metallic, smoothness);
            material.SetTexture("_BaseMap", albedo);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", normalScale);
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.SetTexture("_BumpMap", null);
                material.DisableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Écran de console : image en émission, contribue à l'éclairage baké.</summary>
        static Material Screen(string name, Texture2D image)
        {
            Material material = Lit(name, new Color(0.02f, 0.03f, 0.04f), 0f, 0.85f, Color.white * 1.4f);
            material.SetTexture("_EmissionMap", image);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material Unlit(string name, Color color)
        {
            Material material = GetOrCreate(name, "Universal Render Pipeline/Unlit");
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material Transparent(string name, Color color, bool unlit)
        {
            Material material = GetOrCreate(name, unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            if (!unlit)
            {
                material.SetFloat("_Smoothness", 0.9f);
                // Pas de reflets d'environnement : les luminaires captés par les sondes rendaient le verre blanc sous les angles rasants.
                material.SetFloat("_EnvironmentReflections", 0f);
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                // Reflet multiplié par l'alpha : en « preserve specular », le Fresnel rasant blanchissait les vitres en entier.
                material.SetFloat("_BlendModePreserveSpecular", 0f);
            }
            // Valeurs de blend canoniques d'URP : sinon Unity les réécrit à l'ouverture et git voit un faux changement.
            BaseShaderGUI.SetupMaterialBlendMode(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Forme découpée (alpha clip) teintée : le fond sombre de la touche reste visible autour.</summary>
        static Material Glyph(string name, Color color, Texture2D shape)
        {
            Material material = GetOrCreate(name, "Universal Render Pipeline/Unlit");
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", shape);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Particules en alpha classique (silhouettes sombres, vapeur) : visibles sur le fond du brouillard.</summary>
        static Material ParticleAlpha(string name, Color color, Texture2D texture)
        {
            Material material = GetOrCreate(name, "Universal Render Pipeline/Particles/Unlit");
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Additif, double face, sans écriture de profondeur : rayons de lumière et particules qui « brillent » dans le brouillard.</summary>
        static Material Additive(string name, string shader, Color color, Texture2D texture)
        {
            Material material = GetOrCreate(name, shader);
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ------------------------------------------------------------------ Config

        static void BuildConfig()
        {
            EnsureFolder(Root + "/Resources");
            const string path = Root + "/Resources/GameConfig.asset";
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(path) != null) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameConfig>(), path);
        }

        // ------------------------------------------------------------------ Prefab joueur

        static GameObject BuildPlayerPrefab(Mats m)
        {
            EnsureFolder(PrefabDir);
            var root = new GameObject("NetworkPlayer");
            root.AddComponent<NetworkObject>();
            var player = root.AddComponent<NetworkPlayer>();

            Transform head = SyncedPoint(root.transform, "Head");
            Transform left = SyncedPoint(root.transform, "LeftHand");
            Transform right = SyncedPoint(root.transform, "RightHand");

            GameObject helmet = Prim(PrimitiveType.Sphere, "Helmet", head, Vector3.zero, Quaternion.identity, new Vector3(0.26f, 0.28f, 0.28f), m.Avatar[0], false);
            GameObject visor = Prim(PrimitiveType.Sphere, "Visor", head, new Vector3(0f, -0.01f, 0.07f), Quaternion.identity, new Vector3(0.21f, 0.13f, 0.17f), m.Visor, false);
            GameObject lamp = Prim(PrimitiveType.Cylinder, "Lamp", head, new Vector3(0f, 0.14f, 0.05f), Quaternion.Euler(70f, 0f, 0f), new Vector3(0.05f, 0.02f, 0.05f), m.EmitCyan, false);
            GameObject leftGlove = Prim(PrimitiveType.Sphere, "Glove", left, Vector3.zero, Quaternion.identity, new Vector3(0.09f, 0.06f, 0.12f), m.Avatar[0], false);
            GameObject rightGlove = Prim(PrimitiveType.Sphere, "Glove", right, Vector3.zero, Quaternion.identity, new Vector3(0.09f, 0.06f, 0.12f), m.Avatar[0], false);

            var hidden = new List<GameObject> { helmet, visor, lamp, leftGlove, rightGlove };
            var tinted = new List<Renderer> { helmet.GetComponent<Renderer>(), leftGlove.GetComponent<Renderer>(), rightGlove.GetComponent<Renderer>() };
            BuildAvatarBody(m, root, head, left, right, hidden, tinted);

            var so = new SerializedObject(player);
            so.FindProperty("head").objectReferenceValue = head;
            so.FindProperty("leftHand").objectReferenceValue = left;
            so.FindProperty("rightHand").objectReferenceValue = right;
            SetArray(so.FindProperty("tintedRenderers"), tinted.ToArray());
            SetArray(so.FindProperty("quadrantMaterials"), m.Avatar);
            SetArray(so.FindProperty("hiddenForOwner"), hidden.ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true)) r.renderingLayerMask |= CabinLayer;
            return SavePrefab(root, PrefabDir + "/NetworkPlayer.prefab");
        }

        static Transform SyncedPoint(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var networkTransform = go.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            networkTransform.SyncScaleX = networkTransform.SyncScaleY = networkTransform.SyncScaleZ = false;
            networkTransform.UseQuaternionSynchronization = true;
            networkTransform.UseQuaternionCompression = true;
            networkTransform.Interpolate = true;
            networkTransform.InLocalSpace = false;
            return go.transform;
        }

        // ------------------------------------------------------------------ Cabine d'ascenseur

        static GameObject BuildCabinPrefab(Mats m)
        {
            var root = new GameObject("Cabin");
            var cabin = root.AddComponent<ElevatorCabin>();
            Transform t = root.transform;
            const float inner = 2.4f, h = 2.6f, w = 0.1f, half = inner / 2f + w / 2f;
            const float hole = 0.56f, holeY = 1.5f;

            // Socle, plancher et toit
            Static(Prim(PrimitiveType.Cube, "Socle", t, new Vector3(0f, -0.04f, 0f), Quaternion.identity, new Vector3(inner + 0.5f, 0.12f, inner + 0.5f), m.Frame, true));
            Static(Prim(PrimitiveType.Cube, "Floor", t, new Vector3(0f, 0.005f, 0f), Quaternion.identity, new Vector3(inner + 2 * w, 0.03f, inner + 2 * w), m.Floor, true));
            Static(Prim(PrimitiveType.Cube, "Ceiling", t, new Vector3(0f, h + 0.05f, 0f), Quaternion.identity, new Vector3(inner + 0.4f, 0.1f, inner + 0.4f), m.HullDark, true));
            // Dôme : sa moitié basse reste cachée dans l'épaisseur du toit (rien ne dépasse à l'intérieur).
            Static(Prim(PrimitiveType.Sphere, "Dome", t, new Vector3(0f, h + 0.35f, 0f), Quaternion.identity, new Vector3(2.2f, 0.6f, 2.2f), m.HullDark, false));
            Static(AbyssesArt.MeshObject("Anneau_Dome", t, art.Ring, new Vector3(0f, h + 0.12f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(2.35f, 2.35f, 0.08f), m.EmitCyan));
            Static(Prim(PrimitiveType.Cube, "Wall_Back", t, new Vector3(0f, h / 2f, -half), Quaternion.identity, new Vector3(inner + 2 * w, h, w), m.Hull, true));

            // Murs latéraux percés d'un hublot rond (on voit les bulles défiler pendant la descente).
            foreach (float side in new[] { -1f, 1f })
            {
                string n = side < 0 ? "L" : "R";
                float x = side * half;
                float low = holeY - hole / 2f, high = holeY + hole / 2f, jamb = (inner - hole) / 2f;
                Static(Prim(PrimitiveType.Cube, "Wall_" + n + "_Low", t, new Vector3(x, low / 2f, 0f), Quaternion.identity, new Vector3(w, low, inner), m.Hull, true));
                Static(Prim(PrimitiveType.Cube, "Wall_" + n + "_High", t, new Vector3(x, (high + h) / 2f, 0f), Quaternion.identity, new Vector3(w, h - high, inner), m.Hull, true));
                Static(Prim(PrimitiveType.Cube, "Wall_" + n + "_Front", t, new Vector3(x, holeY, hole / 2f + jamb / 2f), Quaternion.identity, new Vector3(w, hole, jamb), m.Hull, true));
                Static(Prim(PrimitiveType.Cube, "Wall_" + n + "_Rear", t, new Vector3(x, holeY, -hole / 2f - jamb / 2f), Quaternion.identity, new Vector3(w, hole, jamb), m.Hull, true));
                Static(Prim(PrimitiveType.Cube, "Window_" + n, t, new Vector3(x, holeY, 0f), Quaternion.identity, new Vector3(0.02f, hole, hole), m.Glass, false));
                foreach (float face in new[] { -1f, 1f })
                    Static(AbyssesArt.MeshObject("Hublot_" + n, t, art.Ring, new Vector3(x + face * (w / 2f + 0.025f), holeY, 0f), Quaternion.Euler(0f, 90f, 0f), new Vector3(0.9f, 0.9f, 0.05f), m.Brass));
            }

            // Poteaux d'angle arrondis : la cabine perd son air de boîte.
            foreach (Vector2 c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            {
                var corner = new Vector3(c.x * half, h / 2f, c.y * half);
                Static(Prim(PrimitiveType.Cylinder, "Poteau", t, corner, Quaternion.identity, new Vector3(0.3f, h / 2f + 0.05f, 0.3f), m.HullDark, false));
                Static(Prim(PrimitiveType.Cube, "Feu", t, corner + new Vector3(c.x * 0.13f, 0.4f, c.y * 0.13f), Quaternion.Euler(0f, 45f, 0f), new Vector3(0.04f, 1.2f, 0.04f), m.EmitCyan, false));
            }

            // Façade avant avec ouverture de porte (1,2 m x 2,2 m), encadrement rayé.
            Static(Prim(PrimitiveType.Cube, "Front_L", t, new Vector3(-0.95f, h / 2f, half), Quaternion.identity, new Vector3(0.7f, h, w), m.Hull, true));
            Static(Prim(PrimitiveType.Cube, "Front_R", t, new Vector3(0.95f, h / 2f, half), Quaternion.identity, new Vector3(0.7f, h, w), m.Hull, true));
            Static(Prim(PrimitiveType.Cube, "Front_Top", t, new Vector3(0f, 2.4f, half), Quaternion.identity, new Vector3(1.2f, 0.4f, w), m.Hull, true));
            Static(AbyssesArt.MeshObject("Cadre_Porte", t, AbyssesArt.FrameMesh(1.42f, 2.42f, 0.12f, 0.11f, w + 0.08f), new Vector3(0f, 1.1f, half), Quaternion.identity, Vector3.one, m.Hazard));
            GameObject leftDoor = Prim(PrimitiveType.Cube, "Door_L", t, new Vector3(-0.3f, 1.1f, half - 0.08f), Quaternion.identity, new Vector3(0.62f, 2.2f, 0.05f), m.Locker, true);
            GameObject rightDoor = Prim(PrimitiveType.Cube, "Door_R", t, new Vector3(0.3f, 1.1f, half - 0.08f), Quaternion.identity, new Vector3(0.62f, 2.2f, 0.05f), m.Locker, true);

            // Intérieur : main courante, pupitre avec écran, repère de calibration au sol, plafonnier.
            // Main courante sous le tutoriel (support du fusible d'entraînement à 0,96 m).
            Static(Prim(PrimitiveType.Cylinder, "MainCourante", t, new Vector3(0f, 0.85f, -half + 0.12f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.05f, 1.0f, 0.05f), m.Brass, false));
            Static(Prim(PrimitiveType.Cube, "Pupitre", t, new Vector3(-half + 0.1f, 1.25f, -0.7f), Quaternion.identity, new Vector3(0.08f, 0.5f, 0.6f), m.Frame, false));
            Static(Prim(PrimitiveType.Cube, "Ecran", t, new Vector3(-half + 0.145f, 1.27f, -0.7f), Quaternion.identity, new Vector3(0.01f, 0.4f, 0.52f), m.Screens[0], false));
            Static(AbyssesArt.MeshObject("CalibrationMark", t, art.Ring, new Vector3(0f, 0.025f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.6f, 0.6f, 0.01f), m.EmitCyan));
            Static(AbyssesArt.MeshObject("Plafonnier", t, art.Ring, new Vector3(0f, h - 0.02f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(1.4f, 1.4f, 0.03f), m.EmitCyan));

            var lightGo = new GameObject("CabinLight");
            lightGo.transform.SetParent(t, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 4f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
            // Sans ombres, elle éclairerait aussi le plafond du module à travers le toit : elle n'éclaire que la cabine et les avatars.
            if (!lightGo.TryGetComponent(out UniversalAdditionalLightData lightData)) lightData = lightGo.AddComponent<UniversalAdditionalLightData>();
            lightData.renderingLayers = CabinLayer;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true)) r.renderingLayerMask |= CabinLayer;

            // Profondeur affichée au-dessus de la porte, lisible depuis l'intérieur.
            var depthGo = new GameObject("DepthDisplay", typeof(RectTransform));
            depthGo.transform.SetParent(t, false);
            depthGo.transform.localPosition = new Vector3(0f, 2.4f, half - w / 2f - 0.01f);
            var depth = depthGo.AddComponent<TextMeshPro>();
            depth.text = "SURFACE";
            depth.fontSize = 1.6f;
            depth.alignment = TextAlignmentOptions.Center;
            depth.color = new Color(0.4f, 0.95f, 1f);
            ((RectTransform)depthGo.transform).sizeDelta = new Vector2(1.1f, 0.35f);

            ParticleSystem bubblesLeft = Bubbles(t, new Vector3(-half - 0.35f, 0.6f, 0f));
            ParticleSystem bubblesRight = Bubbles(t, new Vector3(half + 0.35f, 0.6f, 0f));
            // Une seule référence dans le script : la seconde suit la première.
            bubblesRight.transform.SetParent(bubblesLeft.transform, true);
            DescentStreaks(m, bubblesLeft, half);
            BuildCabinTutorial(m, t, -inner / 2f);
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true)) r.renderingLayerMask |= CabinLayer;

            var rumble = root.AddComponent<AudioSource>();
            rumble.playOnAwake = false;
            rumble.loop = true;
            rumble.spatialBlend = 0f;
            rumble.clip = fx.Rumble;
            rumble.volume = 0.8f;

            var so = new SerializedObject(cabin);
            so.FindProperty("leftDoor").objectReferenceValue = leftDoor.transform;
            so.FindProperty("rightDoor").objectReferenceValue = rightDoor.transform;
            so.FindProperty("cabinLight").objectReferenceValue = light;
            so.FindProperty("bubbles").objectReferenceValue = bubblesLeft;
            so.FindProperty("rumble").objectReferenceValue = rumble;
            so.FindProperty("depthText").objectReferenceValue = depth;
            so.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, PrefabDir + "/Cabin.prefab");
        }

        static ParticleSystem Bubbles(Transform parent, Vector3 localPosition, bool loop = true)
        {
            var go = new GameObject("Bubbles");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = loop;
            main.duration = loop ? 5f : 0.8f;
            main.startLifetime = 1.6f;
            main.startSpeed = 2.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.06f);
            main.startColor = new Color(0.75f, 0.95f, 1f, 0.7f);
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = ps.emission;
            emission.rateOverTime = 40f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.3f, 0.05f, 1.2f);
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        // ------------------------------------------------------------------ Étages

        /// <summary>Repère local d'un module : +x vers le module partenaire (mur commun), +z vers le fond de la piste.</summary>
        struct Module
        {
            public int Q;
            public Vector3 Center;
            public float Sx;
            /// <summary>Côté du long mur NON partagé avec un autre module (-1 = sud pour Q0/Q1, +1 = nord pour Q2/Q3).</summary>
            public float Vz;
            public int Partner => Q ^ 1;

            public Module(int q)
            {
                Q = q;
                Center = PlayArea.GetCenter(q);
                Sx = q % 2 == 0 ? 1f : -1f;
                Vz = q < 2 ? -1f : 1f;
            }

            public Vector3 P(float x, float y, float z) => Center + new Vector3(Sx * x, y, z);
            /// <summary>Rotation dont l'axe +Z pointe vers la direction locale (x, z).</summary>
            public Quaternion Facing(float x, float z) => Quaternion.LookRotation(new Vector3(Sx * x, 0f, z), Vector3.up);
        }

        struct Opening
        {
            public float Along, Width, Bottom, Top;
            /// <summary>Hublot vitré (cadre arrondi, joint) plutôt que sas ouvert. Par défaut : toute ouverture d'au moins 1 m.</summary>
            public bool Window;
            public Opening(float along, float width, float bottom, float top, bool? window = null)
            {
                Along = along; Width = width; Bottom = bottom; Top = top;
                Window = window ?? width >= 1f;
            }
        }

        static Scene NewFloorScene(int floor, out Transform root)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            root = new GameObject("Etage" + floor).transform;
            return scene;
        }

        static void SaveScene(Scene scene, string name)
        {
            EnsureFolder(SceneDir);
            string path = SceneDir + "/" + name + ".unity";
            EditorSceneManager.SaveScene(scene, path);

            // Un objet réseau créé par script n'a pas d'identifiant (GlobalObjectIdHash = 0) tant que la scène
            // n'existe pas sur disque : on le régénère après la première sauvegarde, puis on resauvegarde.
            System.Reflection.MethodInfo validate = typeof(NetworkObject).GetMethod("OnValidate",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (NetworkObject networkObject in root.GetComponentsInChildren<NetworkObject>(true))
                {
                    validate.Invoke(networkObject, null);
                    EditorUtility.SetDirty(networkObject);
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        enum Shell
        {
            /// <summary>Hublot + sas avec le partenaire, hublot avec le voisin du dessus/dessous.</summary>
            Standard,
            /// <summary>Comme Standard + sas avec le voisin du dessus/dessous.</summary>
            VerticalHatches,
            /// <summary>Salle commune : murs partagés entièrement vitrés.</summary>
            OpenHall,
        }

        static readonly string[] FloorTitles = { "", "HABITAT  ·  -200 m", "LABORATOIRES  ·  -1 500 m", "CŒUR  ·  -4 000 m" };

        /// <summary>Intervalle (le long d'un mur, en coordonnées locales du module) à laisser libre : énigme, ouverture…</summary>
        struct Keepout
        {
            public float From, To;
            public Keepout(float center, float halfWidth) { From = center - halfWidth; To = center + halfWidth; }
        }

        // Emplacements occupés sur chaque mur, tous étages confondus (la coque est commune aux 3 étages).
        static readonly Keepout[] OuterWallItems = { new Keepout(2.5f, 0.4f), new Keepout(-2.5f, 0.35f), new Keepout(0.6f, 0.5f), new Keepout(-3f, 0.4f), new Keepout(-1.5f, 0.4f),
            // Hublots sur l'océan (OceanWindowX).
            new Keepout(-6.4f, 0.55f), new Keepout(6.4f, 0.55f) };
        static readonly Keepout[] NeighborWallItems = { new Keepout(-3f, 1.2f), new Keepout(-5.5f, 0.6f), new Keepout(2f, 0.35f), new Keepout(2.6f, 0.2f) };

        static bool IsFree(float x, float halfWidth, Keepout[] items)
        {
            foreach (Keepout k in items)
                if (x + halfWidth > k.From && x - halfWidth < k.To) return false;
            return true;
        }

        /// <summary>Boîte statique à UV monde (texture à l'échelle réelle), avec UV de lightmap.</summary>
        static GameObject Box(string name, Transform parent, Vector3 position, Quaternion rotation, Vector3 size, Material material, float tile = 2f, bool collider = false)
        {
            GameObject go = AbyssesArt.WorldBox(name, parent, position, rotation, size, material, tile, collider);
            Static(go);
            return go;
        }

        static void BuildModuleShell(Mats m, Transform parent, Module mod, Shell shell, Color lightColor, float lightIntensity, Material strip, int floor)
        {
            Transform t = new GameObject("Module_Q" + mod.Q).transform;
            t.SetParent(parent, false);
            Rect r = PlayArea.GetRect(mod.Q, PlayArea.WallInset);
            float half = WallThickness / 2f;
            var outerSize = new Vector3(r.width + 2 * WallThickness, 0.1f, r.height + 2 * WallThickness);

            Box("Floor", t, new Vector3(r.center.x, -0.05f, r.center.y), Quaternion.identity, outerSize, m.Floor, 1f, true);
            Box("Ceiling", t, new Vector3(r.center.x, WallHeight + 0.05f, r.center.y), Quaternion.identity, outerSize, m.Ceiling, 1f, true);

            bool open = shell == Shell.OpenHall;
            // Les laboratoires ont une coque peinte claire : elle accroche l'éclairage de secours rouge au lieu de l'absorber.
            Material hull = floor == 2 ? m.HullLab : m.Hull;

            // Mur commun avec le partenaire (axe Z).
            float partnerX = mod.Sx > 0 ? r.xMax + half : r.xMin - half;
            float backX = mod.Sx > 0 ? r.xMin - half : r.xMax + half;
            List<Opening> partnerOpenings = open ? null : new List<Opening>
            {
                new Opening(mod.Center.z + 1.5f - r.yMin, 2.0f, 1.1f, 2.1f),
                new Opening(mod.Center.z - 1.5f - r.yMin, 0.5f, 0.9f, 1.4f),
            };
            Wall(m, t, "Wall_Partner", new Vector3(partnerX, 0f, r.yMin), new Vector3(partnerX, 0f, r.yMax), partnerOpenings, true, open, hull);
            Wall(m, t, "Wall_Back", new Vector3(backX, 0f, r.yMin), new Vector3(backX, 0f, r.yMax), null, false, false, hull);

            // Long mur partagé avec le voisin du dessus/dessous (axe X).
            float sharedZ = mod.Vz < 0 ? r.yMax + half : r.yMin - half;
            float outerZ = mod.Vz < 0 ? r.yMin - half : r.yMax + half;
            List<Opening> neighborOpenings = null;
            if (!open)
            {
                neighborOpenings = new List<Opening> { new Opening(mod.Center.x - mod.Sx * 3f - r.xMin, 2.0f, 1.1f, 2.1f) };
                if (shell == Shell.VerticalHatches)
                    neighborOpenings.Add(new Opening(mod.Center.x + mod.Sx * 2f - r.xMin, 0.5f, 0.9f, 1.4f));
            }
            Wall(m, t, "Wall_Neighbor", new Vector3(r.xMin, 0f, sharedZ), new Vector3(r.xMax, 0f, sharedZ), neighborOpenings, true, open, hull);
            // Mur extérieur : deux hublots sur l'océan (créatures, neige marine, bioluminescence derrière), aux deux bouts du module.
            var oceanWindows = new List<Opening>();
            foreach (float x in OceanWindowX)
                oceanWindows.Add(new Opening(mod.Center.x + mod.Sx * x - r.xMin, OceanWindowWidth, 1.05f, 1.95f, true));
            Wall(m, t, "Wall_Outer", new Vector3(r.xMin, 0f, outerZ), new Vector3(r.xMax, 0f, outerZ), oceanWindows, true, false, hull);
            // Vitres sur l'océan : hors lightmap et sans sondes, sinon l'applique voisine y est bakée en jaune et on ne voit plus le noir du large.
            foreach (Renderer pane in t.Find("Wall_Outer").GetComponentsInChildren<Renderer>())
            {
                if (pane.name != "Glass") continue;
                GameObjectUtility.SetStaticEditorFlags(pane.gameObject, GameObjectUtility.GetStaticEditorFlags(pane.gameObject) & ~StaticEditorFlags.ContributeGI);
                pane.lightProbeUsage = LightProbeUsage.Off;
            }

            DressShell(m, t, mod, r, open, floor, hull);
            CeilingFixtures(m, t, mod, strip, lightColor, lightIntensity);
        }

        /// <summary>
        /// Structure de sous-marin : plinthes, soubassement sombre, liseré à la couleur du module, membrures et poutres,
        /// chanfreins plafond, poteaux d'angle, tuyauteries, appliques, signalétique, marquage au sol autour de la cabine.
        /// </summary>
        static void DressShell(Mats m, Transform t, Module mod, Rect r, bool open, int floor, Material hull)
        {
            Transform d = new GameObject("Habillage").transform;
            d.SetParent(t, false);
            Material accent = m.Avatar[mod.Q];
            float s = mod.Vz; // côté du long mur extérieur

            // Les 4 faces intérieures : (point de départ, direction, longueur, normale vers la pièce, vitrée ?)
            var faces = new (Vector3 start, Vector3 dir, float length, Vector3 inward, bool glass, string name)[]
            {
                (new Vector3(r.xMin, 0f, r.yMin), Vector3.right, r.width, Vector3.forward, open && mod.Vz > 0, "South"),
                (new Vector3(r.xMin, 0f, r.yMax), Vector3.right, r.width, Vector3.back, open && mod.Vz < 0, "North"),
                (new Vector3(r.xMin, 0f, r.yMin), Vector3.forward, r.height, Vector3.right, open && mod.Sx < 0, "West"),
                (new Vector3(r.xMax, 0f, r.yMin), Vector3.forward, r.height, Vector3.left, open && mod.Sx > 0, "East"),
            };
            foreach (var f in faces)
            {
                Quaternion along = Quaternion.LookRotation(f.inward, Vector3.up);
                Vector3 mid = f.start + f.dir * (f.length / 2f);
                Vector3 alongSize(float height, float depth) => Quaternion.Inverse(along) * (f.dir * f.length) + new Vector3(0f, height, depth);
                // Plinthe
                Box("Plinthe_" + f.name, d, mid + f.inward * 0.025f + Vector3.up * 0.06f, along, Abs(alongSize(0.12f, 0.05f)), m.Frame, 1f);
                if (!f.glass)
                {
                    // Soubassement (sombre, faïence au labo) + liseré lumineux à la couleur du module (repère d'identité, guide le regard).
                    if (floor == 2) Box("Faience_" + f.name, d, mid + f.inward * 0.012f + Vector3.up * 0.43f, along, Abs(alongSize(0.86f, 0.022f)), m.LabTile, 1f);
                    else Box("Soubassement_" + f.name, d, mid + f.inward * 0.012f + Vector3.up * 0.43f, along, Abs(alongSize(0.86f, 0.022f)), m.HullDark, 2f);
                    Box("Lisere_" + f.name, d, mid + f.inward * 0.02f + Vector3.up * 0.875f, along, Abs(alongSize(0.03f, 0.03f)), accent, 1f);
                }
                // Chanfrein mur / plafond : section octogonale de coque.
                Vector3 width = (f.inward + Vector3.up).normalized;
                Quaternion chamfer = Quaternion.LookRotation(Vector3.Cross(f.dir, width), width);
                Box("Chanfrein_" + f.name, d, mid + f.inward * 0.17f + Vector3.up * (WallHeight - 0.17f), chamfer, new Vector3(f.length, 0.5f, 0.05f), hull, 2f);
            }

            // Poteaux d'angle (sauf l'angle intérieur vitré de la salle commune : il masquerait le réacteur).
            var glassCorner = new Vector2(mod.Sx > 0 ? r.xMax : r.xMin, mod.Vz < 0 ? r.yMax : r.yMin);
            foreach (Vector2 c in new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax) })
            {
                if (open && c == glassCorner) continue;
                Vector3 inward = new Vector3(Mathf.Sign(r.center.x - c.x), 0f, Mathf.Sign(r.center.y - c.y)) * 0.11f;
                Box("Poteau", d, new Vector3(c.x, WallHeight / 2f, c.y) + inward, Quaternion.identity, new Vector3(0.26f, WallHeight, 0.26f), m.HullDark, 2f);
            }

            // Membrures (couples) sur les deux longs murs + poutres de plafond : le rythme d'une coque de sous-marin.
            foreach (float x in new[] { -6f, -4f, -2f, 0f, 2f, 4f, 6f })
            {
                if (IsFree(x, 0.1f, OuterWallItems)) Rib(m, d, mod.P(x, 0f, s * (3.7f - 0.07f)));
                if (!open && IsFree(x, 0.1f, NeighborWallItems)) Rib(m, d, mod.P(x, 0f, -s * (3.7f - 0.07f)));
                if (Mathf.Abs(x) > 1.7f)
                    Box("Poutre", d, mod.P(x, WallHeight - 0.08f, 0f), Quaternion.identity, new Vector3(0.2f, 0.16f, r.height), m.HullDark, 2f);
            }

            // Tuyauteries : deux conduites sous le plafond côté voisin, deux le long du mur extérieur, avec colliers.
            Pipe(m.PipeGrey, d, mod.P(0f, 2.93f, -s * 2.2f), Vector3.right, r.width, 0.075f);
            Pipe(m.PipeYellow, d, mod.P(0f, 2.93f, -s * 2.45f), Vector3.right, r.width, 0.05f);
            Pipe(m.PipeRed, d, mod.P(0f, 2.62f, s * (3.7f - 0.13f)), Vector3.right, r.width, 0.055f);
            Pipe(m.PipeGrey, d, mod.P(0f, 2.48f, s * (3.7f - 0.11f)), Vector3.right, r.width, 0.04f);
            for (float x = -6.5f; x <= 6.6f; x += 2.6f)
                Box("Collier", d, mod.P(x, 2.55f, s * (3.7f - 0.08f)), Quaternion.identity, new Vector3(0.06f, 0.26f, 0.16f), m.Frame, 1f);

            // Appliques chaudes sur le mur du fond (contraste avec la lumière froide du plafond).
            foreach (float z in new[] { -3.05f, 2.5f })
                Sconce(m, d, mod.P(-7.2f + 0.02f, 2.15f, z), mod.Facing(1f, 0f), floor == 2 ? new Color(1f, 0.45f, 0.2f) : new Color(1f, 0.72f, 0.45f));

            // Signalétique au pochoir sur le mur du fond.
            Label(d, "MODULE " + QuadrantNames[mod.Q], mod.P(-7.18f, 2.62f, 0f), 3.2f, mod.Facing(-1f, 0f));
            Label(d, FloorTitles[floor], mod.P(-7.18f, 2.38f, 0f), 1.6f, mod.Facing(-1f, 0f));

            // Marquage de sécurité autour de la cabine (zone d'embarquement).
            const float ring = 1.75f, band = 0.12f;
            foreach (Vector2 side in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
            {
                bool alongX = side.y != 0f;
                Vector3 pos = mod.Center + new Vector3(side.x, 0f, side.y) * (ring - band / 2f) + Vector3.up * 0.004f;
                Vector3 size = alongX ? new Vector3(ring * 2f, 0.008f, band) : new Vector3(band, 0.008f, ring * 2f - 2f * band);
                Box("Marquage", d, pos, Quaternion.identity, size, m.Hazard, 0.5f);
            }
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>Membrure verticale : âme + semelle, du sol au chanfrein.</summary>
        static void Rib(Mats m, Transform parent, Vector3 basePosition)
        {
            Box("Membrure", parent, basePosition + Vector3.up * (WallHeight - 0.3f) / 2f, Quaternion.identity, new Vector3(0.14f, WallHeight - 0.3f, 0.14f), m.HullDark, 2f);
            Box("Semelle", parent, basePosition + Vector3.up * 0.08f, Quaternion.identity, new Vector3(0.24f, 0.16f, 0.2f), m.Frame, 1f);
        }

        static void Pipe(Material material, Transform parent, Vector3 center, Vector3 direction, float length, float radius)
        {
            GameObject pipe = Prim(PrimitiveType.Cylinder, "Conduite", parent, center, Quaternion.FromToRotation(Vector3.up, direction), new Vector3(radius * 2f, length / 2f, radius * 2f), material, false);
            Static(pipe);
        }

        /// <summary>Applique murale : platine, ampoule chaude sous grille, petite lumière bakée.</summary>
        static void Sconce(Mats m, Transform parent, Vector3 position, Quaternion facing, Color color)
        {
            Transform root = new GameObject("Applique").transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(position, facing);
            Static(Prim(PrimitiveType.Cube, "Platine", root, new Vector3(0f, 0f, 0.015f), Quaternion.identity, new Vector3(0.16f, 0.22f, 0.03f), m.Frame, false));
            Static(Prim(PrimitiveType.Sphere, "Ampoule", root, new Vector3(0f, 0f, 0.09f), Quaternion.identity, Vector3.one * 0.1f, m.WarmBulb, false));
            foreach (float a in new[] { 0f, 60f, 120f })
                Static(Prim(PrimitiveType.Cylinder, "Grille", root, new Vector3(0f, 0f, 0.09f), Quaternion.Euler(90f, a, 0f), new Vector3(0.13f, 0.004f, 0.13f), m.Frame, false));

            var lightGo = new GameObject("Lumiere");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, 0f, 0.25f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = 4f;
            light.intensity = 1.6f;
            light.lightmapBakeType = LightmapBakeType.Baked;
            light.shadows = LightShadows.Soft;
            light.shadowRadius = 0.1f;
        }

        /// <summary>Luminaires encastrés : caisson sombre + diffuseur lumineux, une lumière bakée sous chacun.</summary>
        static void CeilingFixtures(Mats m, Transform t, Module mod, Material diffuser, Color lightColor, float totalIntensity)
        {
            float[] xs = { -5f, -3f, 3f, 5f };
            foreach (float x in xs)
            {
                Box("Caisson", t, mod.P(x, WallHeight - 0.05f, 0f), Quaternion.identity, new Vector3(0.36f, 0.1f, 2.4f), m.HullDark, 2f);
                Static(Prim(PrimitiveType.Cube, "Diffuseur", t, mod.P(x, WallHeight - 0.105f, 0f), Quaternion.identity, new Vector3(0.24f, 0.02f, 2.25f), diffuser, false));

                var lightGo = new GameObject("Light");
                lightGo.transform.SetParent(t, false);
                lightGo.transform.position = mod.P(x, WallHeight - 0.4f, 0f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = lightColor;
                light.range = 7.5f;
                light.intensity = totalIntensity * 0.55f;
                // Bakée : ombres douces et rebonds dans les lightmaps, zéro coût au rendu sur le Quest.
                light.lightmapBakeType = LightmapBakeType.Baked;
                light.shadows = LightShadows.Soft;
                light.shadowRadius = 0.2f;
            }
        }

        /// <summary>
        /// Position (x local du module) et largeur des hublots du mur extérieur : aux deux bouts, entre le poteau d'angle
        /// et le mobilier (casiers, paillasse, bobines), à l'écart des énigmes. Voir aussi <see cref="OuterWallItems"/>.
        /// </summary>
        static readonly float[] OceanWindowX = { -6.4f, 6.4f };
        const float OceanWindowWidth = 0.8f;

        static void Wall(Mats m, Transform parent, string name, Vector3 a, Vector3 b, List<Opening> openings, bool glass, bool fullGlass, Material hull)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            Vector3 dir = (b - a).normalized;
            float length = Vector3.Distance(a, b);
            Quaternion rot = Quaternion.FromToRotation(Vector3.right, dir);
            // Normale du mur (pour orienter les cadres d'ouverture).
            Vector3 normal = Vector3.Cross(dir, Vector3.up);

            void Piece(string n, float from, float to, float bottom, float top, Material mat, float thickness, bool collider)
            {
                if (to - from < 0.01f || top - bottom < 0.01f) return;
                Vector3 center = a + dir * ((from + to) / 2f) + Vector3.up * ((bottom + top) / 2f);
                var size = new Vector3(to - from, top - bottom, thickness);
                if (mat == m.Glass) Static(Prim(PrimitiveType.Cube, n, t, center, rot, size, mat, collider));
                else Box(n, t, center, rot, size, mat, 2f, collider);
            }

            if (fullGlass)
            {
                // Paroi vitrée toute hauteur : montants tous les 2,5 m, liseré lumineux au sol.
                Piece("Glass", 0f, length, 0.05f, WallHeight - 0.15f, m.Glass, 0.03f, true);
                Piece("Top", 0f, length, WallHeight - 0.15f, WallHeight, m.HullDark, WallThickness, true);
                Piece("Sill", 0f, length, 0f, 0.05f, m.EmitCyan, WallThickness, true);
                // Montants intermédiaires seulement : aux extrémités, les poteaux d'angle (ou l'angle vitré ouvert sur le réacteur) suffisent.
                // Nombre impair de panneaux : aucun montant dans l'axe de la porte de cabine (centre du module).
                int panes = Mathf.Max(1, Mathf.RoundToInt(length / 2.5f));
                if (panes % 2 == 0) panes++;
                float step = length / panes;
                for (float x = step; x < length - 0.01f; x += step)
                    Piece("Mullion", x - 0.07f, x + 0.07f, 0.05f, WallHeight - 0.15f, m.HullDark, WallThickness + 0.06f, true);
                return;
            }

            float cursor = 0f;
            if (openings != null)
            {
                openings.Sort((x, y) => x.Along.CompareTo(y.Along));
                foreach (Opening o in openings)
                {
                    float start = o.Along - o.Width / 2f, end = o.Along + o.Width / 2f;
                    Piece("Segment", cursor, start, 0f, WallHeight, hull, WallThickness, true);
                    Piece("Below", start, end, 0f, o.Bottom, hull, WallThickness, true);
                    Piece("Above", start, end, o.Top, WallHeight, hull, WallThickness, true);
                    if (glass && o.Window) Piece("Glass", start, end, o.Bottom, o.Top, m.Glass, 0.03f, true);

                    // Encadrement arrondi qui mange les angles vifs de l'ouverture (hublot / sas).
                    Vector3 center = a + dir * o.Along + Vector3.up * ((o.Bottom + o.Top) / 2f);
                    bool window = o.Window;
                    float margin = window ? 0.3f : 0.2f;
                    Mesh frame = AbyssesArt.FrameMesh(o.Width + margin, o.Top - o.Bottom + margin, window ? 0.28f : 0.14f, window ? 0.2f : 0.12f, WallThickness + 0.08f);
                    Static(AbyssesArt.MeshObject(window ? "Cadre_Hublot" : "Cadre_Sas", t, frame, center, Quaternion.LookRotation(normal, Vector3.up), Vector3.one, window ? m.Frame : m.Hazard));
                    if (window)
                    {
                        Mesh seal = AbyssesArt.FrameMesh(o.Width + 0.04f, o.Top - o.Bottom + 0.04f, 0.16f, 0.06f, WallThickness + 0.02f);
                        Static(AbyssesArt.MeshObject("Joint_Hublot", t, seal, center, Quaternion.LookRotation(normal, Vector3.up), Vector3.one, m.Rubber));
                    }
                    cursor = end;
                }
            }
            Piece("Segment", cursor, length, 0f, WallHeight, hull, WallThickness, true);
        }

        static Module[] BuildModules(Mats m, Transform root, Shell shell, Color lightColor, float lightIntensity, Material strip, int floor)
        {
            var modules = new Module[4];
            for (int q = 0; q < 4; q++)
            {
                modules[q] = new Module(q);
                BuildModuleShell(m, root, modules[q], shell, lightColor, lightIntensity, strip, floor);
                switch (floor)
                {
                    case 1: DressHabitat(m, root, modules[q]); break;
                    case 2: DressLabo(m, root, modules[q]); break;
                    default: DressCore(m, root, modules[q]); break;
                }
            }
            return modules;
        }

        // ---------- Mobilier (contre les murs, hors des zones d'énigme et du passage)
        // Convention : l'axe +Z local d'un meuble pointe vers le mur (son dos), la face avant est en -Z.

        /// <summary>Étage 1 — quartiers d'habitation : casiers, bureau avec écran, caisses, fûts, colonne technique.</summary>
        static void DressHabitat(Mats m, Transform root, Module mod)
        {
            Transform t = new GameObject("Mobilier_Q" + mod.Q).transform;
            t.SetParent(root, false);
            float s = mod.Vz;
            foreach (float x in new[] { 3.35f, 4.4f, 5.0f, 5.6f })
                Locker(m, t, mod.P(x, 0f, s * 3.42f), mod.Facing(0f, -s));
            Crate(m, t, mod.P(6.5f, 0f, s * 3.38f), mod.Facing(0f, -s), new Vector3(0.6f, 0.55f, 0.5f));
            Desk(m, t, mod.P(-5f, 0f, s * 3.36f), mod.Facing(0f, s), m.Screens[mod.Q % 3]);
            Crate(m, t, mod.P(4.5f, 0f, -s * 3.4f), mod.Facing(0f, s), new Vector3(0.7f, 0.6f, 0.55f));
            Crate(m, t, mod.P(5.3f, 0f, -s * 3.42f), mod.Facing(0f, s), new Vector3(0.6f, 0.5f, 0.5f));
            Crate(m, t, mod.P(4.52f, 0.6f, -s * 3.42f), mod.Facing(0f, s), new Vector3(0.55f, 0.45f, 0.45f));
            Barrel(m, t, mod.P(-1.2f, 0f, -s * 3.38f), m.PipeYellow);
            Barrel(m, t, mod.P(-0.55f, 0f, -s * 3.42f), m.PipeRed);
            PipeColumn(m, t, mod.P(-6.85f, 0f, 3.35f));
        }

        /// <summary>Étage 2 — laboratoires : paillasse, cuves à échantillons lumineuses, baie de serveurs.</summary>
        static void DressLabo(Mats m, Transform root, Module mod)
        {
            Transform t = new GameObject("Mobilier_Q" + mod.Q).transform;
            t.SetParent(root, false);
            float s = mod.Vz;
            LabBench(m, t, mod.P(-5f, 0f, s * 3.36f), mod.Facing(0f, s));
            SpecimenTank(m, t, mod.P(4.5f, 0f, s * 3.3f));
            SpecimenTank(m, t, mod.P(5.5f, 0f, s * 3.3f));
            ServerRack(m, t, mod.P(-1.2f, 0f, -s * 3.42f), mod.Facing(0f, -s));
            ServerRack(m, t, mod.P(-0.55f, 0f, -s * 3.42f), mod.Facing(0f, -s));
            SpecimenTank(m, t, mod.P(4.5f, 0f, -s * 3.3f));
            Crate(m, t, mod.P(5.3f, 0f, -s * 3.35f), mod.Facing(0f, s), new Vector3(0.7f, 0.55f, 0.55f));
            PipeColumn(m, t, mod.P(-6.85f, 0f, 3.35f));
            SampleCabinet(m, t, mod.P(-7.2f + 0.24f, 0f, 1.25f), mod.Facing(-1f, 0f));
            // Fuite de pression : jet de vapeur sur la conduite du plafond, flaques au sol.
            SteamLeak(m, t, mod.P(-1.6f, 2.86f, -s * 2.2f));
            Puddle(m, t, mod.P(-1.6f, 0f, -s * 2.0f), new Vector2(1.0f, 0.65f), -30f);
            Puddle(m, t, mod.P(-6.1f, 0f, 2.6f), new Vector2(0.8f, 0.5f), 25f);
        }

        /// <summary>Étage 3 — cœur : machinerie lourde (bobines, collecteurs) et câbles qui convergent vers le réacteur.</summary>
        static void DressCore(Mats m, Transform root, Module mod)
        {
            Transform t = new GameObject("Mobilier_Q" + mod.Q).transform;
            t.SetParent(root, false);
            float s = mod.Vz;
            Coil(m, t, mod.P(-5f, 0f, s * 3.0f));
            Coil(m, t, mod.P(5f, 0f, s * 3.0f));
            // Armoires de distribution entre les membrures du mur extérieur.
            PowerCabinet(m, t, mod.P(-1f, 0f, s * 3.46f), mod.Facing(0f, s), 0);
            PowerCabinet(m, t, mod.P(1f, 0f, s * 3.46f), mod.Facing(0f, s), 2);
            ServerRack(m, t, mod.P(-6.85f, 0f, -s * 1.5f), mod.Facing(-1f, 0f));
            ServerRack(m, t, mod.P(-6.85f, 0f, -s * 0.85f), mod.Facing(-1f, 0f));
            PipeColumn(m, t, mod.P(-6.85f, 0f, 3.35f));
        }

        static void Locker(Mats m, Transform parent, Vector3 floorPosition, Quaternion facing)
        {
            Transform root = Group("Casier", parent, floorPosition, facing);
            Static(Prim(PrimitiveType.Cube, "Corps", root, new Vector3(0f, 0.95f, 0f), Quaternion.identity, new Vector3(0.58f, 1.9f, 0.48f), m.Locker, false));
            Box("Socle", root, new Vector3(0f, 0.04f, 0f), Quaternion.identity, new Vector3(0.6f, 0.08f, 0.5f), m.Frame, 1f);
            Box("Chapeau", root, new Vector3(0f, 1.92f, 0f), Quaternion.identity, new Vector3(0.6f, 0.04f, 0.5f), m.HullDark, 1f);
        }

        static void Desk(Mats m, Transform parent, Vector3 floorPosition, Quaternion facing, Material screen)
        {
            Transform root = Group("Bureau", parent, floorPosition, facing);
            Box("Plateau", root, new Vector3(0f, 0.76f, 0f), Quaternion.identity, new Vector3(1.6f, 0.05f, 0.62f), m.HullDark, 1f);
            Static(Prim(PrimitiveType.Cube, "Caisson", root, new Vector3(0.55f, 0.37f, 0f), Quaternion.identity, new Vector3(0.45f, 0.74f, 0.55f), m.Locker, false));
            Box("Pied", root, new Vector3(-0.72f, 0.37f, 0f), Quaternion.identity, new Vector3(0.06f, 0.74f, 0.5f), m.Frame, 1f);
            Box("Moniteur", root, new Vector3(0f, 1.1f, 0.16f), Quaternion.Euler(-8f, 0f, 0f), new Vector3(0.72f, 0.46f, 0.05f), m.Frame, 1f);
            Static(Prim(PrimitiveType.Cube, "Ecran", root, new Vector3(0f, 1.1f, 0.13f), Quaternion.Euler(-8f, 0f, 0f), new Vector3(0.66f, 0.4f, 0.01f), screen, false));
            Box("Pied_Ecran", root, new Vector3(0f, 0.86f, 0.2f), Quaternion.identity, new Vector3(0.06f, 0.18f, 0.06f), m.Frame, 1f);
            Box("Clavier", root, new Vector3(0f, 0.795f, -0.08f), Quaternion.Euler(4f, 0f, 0f), new Vector3(0.5f, 0.02f, 0.17f), m.Key, 1f);
        }

        static void Crate(Mats m, Transform parent, Vector3 floorPosition, Quaternion facing, Vector3 size)
        {
            Transform root = Group("Caisse", parent, floorPosition, facing);
            Box("Corps", root, new Vector3(0f, size.y / 2f, 0f), Quaternion.identity, size - new Vector3(0.04f, 0.02f, 0.04f), m.PipeGrey, 0.5f, true);
            Box("Bande", root, new Vector3(0f, size.y * 0.72f, 0f), Quaternion.identity, new Vector3(size.x + 0.005f, 0.07f, size.z + 0.005f), m.Hazard, 0.4f);
            foreach (Vector2 c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
                Box("Arete", root, new Vector3(c.x * (size.x / 2f - 0.02f), size.y / 2f, c.y * (size.z / 2f - 0.02f)), Quaternion.identity, new Vector3(0.05f, size.y, 0.05f), m.Frame, 1f);
        }

        static void Barrel(Mats m, Transform parent, Vector3 floorPosition, Material paint)
        {
            Transform root = Group("Fut", parent, floorPosition, Quaternion.identity);
            Static(Prim(PrimitiveType.Cylinder, "Corps", root, new Vector3(0f, 0.44f, 0f), Quaternion.identity, new Vector3(0.52f, 0.44f, 0.52f), paint, false));
            foreach (float y in new[] { 0.18f, 0.7f })
                Static(Prim(PrimitiveType.Cylinder, "Cerclage", root, new Vector3(0f, y, 0f), Quaternion.identity, new Vector3(0.54f, 0.02f, 0.54f), m.Frame, false));
            Static(Prim(PrimitiveType.Cylinder, "Bonde", root, new Vector3(0.12f, 0.885f, 0f), Quaternion.identity, new Vector3(0.07f, 0.01f, 0.07f), m.Frame, false));
        }

        /// <summary>Faisceau de conduites verticales du sol au plafond, avec une vanne en laiton.</summary>
        static void PipeColumn(Mats m, Transform parent, Vector3 floorPosition)
        {
            Transform root = Group("Colonne", parent, floorPosition, Quaternion.identity);
            Pipe(m.PipeGrey, root, new Vector3(-0.1f, WallHeight / 2f, 0f), Vector3.up, WallHeight, 0.07f);
            Pipe(m.PipeYellow, root, new Vector3(0.1f, WallHeight / 2f, 0.04f), Vector3.up, WallHeight, 0.045f);
            Pipe(m.PipeRed, root, new Vector3(0.02f, WallHeight / 2f, -0.12f), Vector3.up, WallHeight, 0.035f);
            Static(AbyssesArt.MeshObject("Vanne", root, art.Ring, new Vector3(0.1f, 1.35f, 0.16f), Quaternion.identity, new Vector3(0.22f, 0.22f, 0.03f), m.Brass));
            foreach (float y in new[] { 0.6f, 2.2f })
                Box("Collier", root, new Vector3(0f, y, 0f), Quaternion.identity, new Vector3(0.32f, 0.05f, 0.3f), m.Frame, 1f);
        }

        static void LabBench(Mats m, Transform parent, Vector3 floorPosition, Quaternion facing)
        {
            Transform root = Group("Paillasse", parent, floorPosition, facing);
            Box("Plan", root, new Vector3(0f, 0.9f, 0f), Quaternion.identity, new Vector3(1.7f, 0.05f, 0.62f), m.PipeGrey, 1f);
            Static(Prim(PrimitiveType.Cube, "Meuble", root, new Vector3(0f, 0.44f, 0.03f), Quaternion.identity, new Vector3(1.6f, 0.86f, 0.55f), m.Locker, false));
            Box("Etagere", root, new Vector3(0f, 1.45f, 0.24f), Quaternion.identity, new Vector3(1.6f, 0.03f, 0.2f), m.Frame, 1f);
            Static(Prim(PrimitiveType.Cube, "Ecran", root, new Vector3(0.55f, 1.2f, 0.29f), Quaternion.identity, new Vector3(0.5f, 0.32f, 0.02f), m.Screens[1], false));
            // Hotte : caisson suspendu, réglette froide, lumière bakée qui découpe une flaque de lumière blanche dans le rouge.
            Box("Hotte", root, new Vector3(0f, 2.05f, 0.05f), Quaternion.identity, new Vector3(1.7f, 0.08f, 0.5f), m.HullDark, 1f);
            Static(Prim(PrimitiveType.Cube, "Reglette", root, new Vector3(0f, 2.005f, -0.05f), Quaternion.identity, new Vector3(1.5f, 0.012f, 0.08f), m.EmitCold, false));
            foreach (float x in new[] { -0.75f, 0.75f })
                Box("Suspente", root, new Vector3(x, (2.09f + WallHeight) / 2f, 0.05f), Quaternion.identity, new Vector3(0.025f, WallHeight - 2.09f, 0.025f), m.Frame, 1f);
            var lamp = new GameObject("Lumiere");
            lamp.transform.SetParent(root, false);
            lamp.transform.localPosition = new Vector3(0f, 1.75f, -0.2f);
            var lampLight = lamp.AddComponent<Light>();
            lampLight.type = LightType.Point;
            lampLight.color = new Color(0.78f, 0.93f, 1f);
            lampLight.range = 2.8f;
            lampLight.intensity = 1.8f;
            lampLight.lightmapBakeType = LightmapBakeType.Baked;
            lampLight.shadows = LightShadows.Soft;
            lampLight.shadowRadius = 0.15f;
            for (int i = 0; i < 5; i++)
            {
                float x = -0.7f + i * 0.22f;
                float h = 0.1f + 0.06f * (i % 3);
                Static(Prim(PrimitiveType.Cylinder, "Fiole", root, new Vector3(x, 0.925f + h, 0.05f), Quaternion.identity, new Vector3(0.07f, h, 0.07f), m.Glass, false));
                Static(Prim(PrimitiveType.Cylinder, "Liquide", root, new Vector3(x, 0.925f + h * 0.6f, 0.05f), Quaternion.identity, new Vector3(0.055f, h * 0.6f, 0.055f), m.Liquid, false));
            }
        }

        /// <summary>Cuve à échantillons : socle, cylindre de verre, liquide lumineux, couvercle et tuyau vers le plafond.</summary>
        static void SpecimenTank(Mats m, Transform parent, Vector3 floorPosition)
        {
            Transform root = Group("Cuve", parent, floorPosition, Quaternion.identity);
            Static(Prim(PrimitiveType.Cylinder, "Socle", root, new Vector3(0f, 0.12f, 0f), Quaternion.identity, new Vector3(0.66f, 0.12f, 0.66f), m.Frame, false));
            Static(Prim(PrimitiveType.Cylinder, "Liquide", root, new Vector3(0f, 0.9f, 0f), Quaternion.identity, new Vector3(0.44f, 0.62f, 0.44f), m.Liquid, false));
            Static(Prim(PrimitiveType.Cylinder, "Verre", root, new Vector3(0f, 0.92f, 0f), Quaternion.identity, new Vector3(0.54f, 0.7f, 0.54f), m.Glass, false));
            Static(Prim(PrimitiveType.Cylinder, "Couvercle", root, new Vector3(0f, 1.66f, 0f), Quaternion.identity, new Vector3(0.62f, 0.06f, 0.62f), m.Frame, false));
            Static(AbyssesArt.MeshObject("Cerclage", root, art.Ring, new Vector3(0f, 1.58f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.6f, 0.6f, 0.05f), m.Brass));
            Pipe(m.PipeGrey, root, new Vector3(0f, (1.72f + WallHeight) / 2f, 0f), Vector3.up, WallHeight - 1.72f, 0.04f);

            // Halo vert baké : contrepoint froid à l'éclairage de secours rouge du labo.
            var lightGo = new GameObject("Lumiere");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, 1f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.3f, 1f, 0.6f);
            light.range = 2.6f;
            light.intensity = 1.4f;
            light.lightmapBakeType = LightmapBakeType.Baked;
            light.shadows = LightShadows.None;
        }

        static void ServerRack(Mats m, Transform parent, Vector3 floorPosition, Quaternion facing)
        {
            Transform root = Group("Baie", parent, floorPosition, facing);
            Box("Chassis", root, new Vector3(0f, 0.95f, 0f), Quaternion.identity, new Vector3(0.6f, 1.9f, 0.45f), m.Frame, 1f);
            for (int i = 0; i < 6; i++)
            {
                float y = 0.35f + i * 0.26f;
                Box("Tiroir", root, new Vector3(0f, y, -0.226f), Quaternion.identity, new Vector3(0.52f, 0.2f, 0.01f), m.HullDark, 1f);
                Static(Prim(PrimitiveType.Cube, "Voyants", root, new Vector3(0.15f, y + 0.05f, -0.233f), Quaternion.identity, new Vector3(0.14f, 0.02f, 0.005f), i % 2 == 0 ? m.Liquid : m.EmitCyan, false));
            }
        }

        static void Coil(Mats m, Transform parent, Vector3 floorPosition)
        {
            Transform root = Group("Bobine", parent, floorPosition, Quaternion.identity);
            Static(Prim(PrimitiveType.Cylinder, "Socle", root, new Vector3(0f, 0.1f, 0f), Quaternion.identity, new Vector3(0.9f, 0.1f, 0.9f), m.Frame, false));
            Static(Prim(PrimitiveType.Cylinder, "Noyau", root, new Vector3(0f, 0.9f, 0f), Quaternion.identity, new Vector3(0.45f, 0.7f, 0.45f), m.HullDark, false));
            for (int i = 0; i < 5; i++)
                Static(AbyssesArt.MeshObject("Spire", root, art.Ring, new Vector3(0f, 0.45f + i * 0.22f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.7f, 0.7f, 0.08f), m.Brass));
            Static(Prim(PrimitiveType.Cylinder, "Chapeau", root, new Vector3(0f, 1.62f, 0f), Quaternion.identity, new Vector3(0.52f, 0.03f, 0.52f), m.Frame, false));
            Static(AbyssesArt.MeshObject("Lueur", root, art.Ring, new Vector3(0f, 1.655f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.34f, 0.34f, 0.02f), m.EmitPurple));
            Pipe(m.Rubber, root, new Vector3(0f, (1.64f + WallHeight) / 2f, 0f), Vector3.up, WallHeight - 1.64f, 0.05f);
        }

        /// <summary>Armoire à échantillons : meuble bas, bocaux lumineux dessus et sur une étagère murale.</summary>
        static void SampleCabinet(Mats m, Transform parent, Vector3 floorPosition, Quaternion facing)
        {
            Transform root = Group("Armoire_Echantillons", parent, floorPosition, facing);
            Static(Prim(PrimitiveType.Cube, "Corps", root, new Vector3(0f, 0.475f, 0f), Quaternion.identity, new Vector3(1.0f, 0.95f, 0.45f), m.Locker, false));
            Box("Plateau", root, new Vector3(0f, 0.97f, 0f), Quaternion.identity, new Vector3(1.04f, 0.04f, 0.48f), m.Frame, 1f);
            Box("Etagere", root, new Vector3(0f, 1.55f, 0.115f), Quaternion.identity, new Vector3(1.0f, 0.03f, 0.25f), m.Frame, 1f);
            foreach (float x in new[] { -0.42f, 0.42f })
                Box("Console", root, new Vector3(x, 1.49f, 0.2f), Quaternion.identity, new Vector3(0.03f, 0.1f, 0.08f), m.Frame, 1f);

            void Jar(Vector3 basePosition, float h, Material liquid)
            {
                Static(Prim(PrimitiveType.Cylinder, "Bocal", root, basePosition + Vector3.up * h, Quaternion.identity, new Vector3(0.13f, h, 0.13f), m.Glass, false));
                Static(Prim(PrimitiveType.Cylinder, "Contenu", root, basePosition + Vector3.up * h * 0.7f, Quaternion.identity, new Vector3(0.105f, h * 0.7f, 0.105f), liquid, false));
                Static(Prim(PrimitiveType.Cylinder, "Couvercle", root, basePosition + Vector3.up * (h * 2f + 0.01f), Quaternion.identity, new Vector3(0.14f, 0.01f, 0.14f), m.Brass, false));
            }
            for (int i = 0; i < 4; i++)
                Jar(new Vector3(-0.36f + i * 0.24f, 0.99f, -0.05f), 0.1f + 0.03f * (i % 2), i == 2 ? m.EmitCyan : m.Liquid);
            for (int i = 0; i < 3; i++)
                Jar(new Vector3(-0.3f + i * 0.3f, 1.565f, 0.11f), 0.08f, i == 1 ? m.EmitCyan : m.Liquid);
        }

        /// <summary>Flaque d'eau : disque très lisse et sombre, il reflète les luminaires via les reflection probes.</summary>
        static void Puddle(Mats m, Transform parent, Vector3 floorPosition, Vector2 size, float angle)
        {
            Static(Prim(PrimitiveType.Cylinder, "Flaque", parent, floorPosition + Vector3.up * 0.004f, Quaternion.Euler(0f, angle, 0f), new Vector3(size.x, 0.002f, size.y), m.Puddle, false));
        }

        /// <summary>Jet de vapeur sous une conduite qui fuit : quelques dizaines de particules douces, aucune lumière.</summary>
        static void SteamLeak(Mats m, Transform parent, Vector3 position)
        {
            var go = new GameObject("Fuite_Vapeur");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(90f, 0f, 0f));
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 16f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = 0.02f;
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.dampen = 0.08f;
            drag.limit = 0.15f;
            var grow = ps.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 4f));
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.4f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = m.Steam;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 0.4f;
        }

        /// <summary>Armoire électrique : portes, écran, deux cadrans, rangée de voyants, faisceau de câbles vers le plafond.</summary>
        static void PowerCabinet(Mats m, Transform parent, Vector3 floorPosition, Quaternion facing, int screen)
        {
            Transform root = Group("Armoire_Puissance", parent, floorPosition, facing);
            Box("Caisson", root, new Vector3(0f, 1.0f, 0f), Quaternion.identity, new Vector3(1.5f, 2.0f, 0.4f), m.HullDark, 2f);
            Box("Socle", root, new Vector3(0f, 0.05f, 0f), Quaternion.identity, new Vector3(1.54f, 0.1f, 0.44f), m.Frame, 1f);
            foreach (float x in new[] { -0.37f, 0.37f })
                Static(Prim(PrimitiveType.Cube, "Porte", root, new Vector3(x, 0.75f, -0.205f), Quaternion.identity, new Vector3(0.7f, 1.25f, 0.01f), m.Locker, false));
            Static(Prim(PrimitiveType.Cube, "Ecran", root, new Vector3(-0.36f, 1.7f, -0.205f), Quaternion.identity, new Vector3(0.62f, 0.34f, 0.01f), m.Screens[screen], false));
            foreach (float x in new[] { 0.22f, 0.52f })
            {
                Static(AbyssesArt.MeshObject("Cadran", root, art.Ring, new Vector3(x, 1.7f, -0.21f), Quaternion.identity, new Vector3(0.26f, 0.26f, 0.03f), m.Brass));
                Static(Prim(PrimitiveType.Cylinder, "Fond", root, new Vector3(x, 1.7f, -0.203f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.2f, 0.004f, 0.2f), m.Key, false));
                Static(Prim(PrimitiveType.Cube, "Aiguille", root, new Vector3(x + 0.02f, 1.72f, -0.21f), Quaternion.Euler(0f, 0f, x > 0.3f ? -35f : 50f), new Vector3(0.012f, 0.08f, 0.004f), m.StatusIdle, false));
            }
            Material[] leds = { m.StatusOk, m.EmitCyan, m.StatusOk, m.StatusIdle, m.EmitCyan, m.StatusOk };
            for (int i = 0; i < leds.Length; i++)
                Static(Prim(PrimitiveType.Cube, "Voyant", root, new Vector3(-0.6f + i * 0.24f, 1.44f, -0.205f), Quaternion.identity, new Vector3(0.05f, 0.05f, 0.012f), leds[i], false));
            Box("Bande", root, new Vector3(0f, 1.97f, 0f), Quaternion.identity, new Vector3(1.505f, 0.06f, 0.405f), m.Hazard, 0.4f);
            foreach (float x in new[] { -0.3f, 0f, 0.3f })
                Pipe(m.Rubber, root, new Vector3(x, (2f + WallHeight) / 2f, 0.06f), Vector3.up, WallHeight - 2f, 0.03f);
        }

        static Transform Group(string name, Transform parent, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            return go.transform;
        }

        // ---------- Étage 1 : « Rétablir le courant »

        static void BuildFloor1(Mats m)
        {
            Scene scene = NewFloorScene(1, out Transform root);
            FloorEnvironment(root, new Color(0.02f, 0.09f, 0.11f), 0.05f, new Color(0.06f, 0.12f, 0.14f), fx.AmbienceHabitat);

            Module[] modules = BuildModules(m, root, Shell.Standard, new Color(0.55f, 0.88f, 1f), 6f, m.EmitCyan, 1);
            foreach (Module mod in modules)
                Plankton(root, mod, m.PlanktonHabitat, new Color(0.55f, 0.95f, 1f), new Color(0.85f, 1f, 0.95f));
            BuildCaustics(root);
            OceanView(m, root, modules, 1);
            StationLife(m, root, modules, 1);

            var keypads = new CodeKeypad[4];
            var fuses = new NetworkTransform[4];
            var levers = new LeverHandle[4];
            var prerequisites = new List<PuzzleObjective>();

            for (int q = 0; q < 4; q++)
            {
                keypads[q] = BuildKeypad(m, root, modules[q], 1);
                prerequisites.Add(keypads[q].GetComponent<PuzzleObjective>());
            }
            for (int q = 0; q < 4; q++)
            {
                Module mod = modules[q];
                BuildCodeDisplay(m, root, mod.P(-7.16f, 1.6f, 0f), mod.Facing(-1f, 0f), keypads[mod.Partner],
                    "CODE DU MODULE " + QuadrantNames[mod.Partner], true, false, "CodeDisplay_For_Q" + mod.Partner, "CODE FOR MODULE " + QuadrantNamesEn[mod.Partner]);
                prerequisites.Add(BuildFuseSocket(m, root, mod));
                fuses[mod.Partner] = BuildFuse(m, root, mod, mod.Partner);
                levers[q] = BuildLever(m, root, mod.Q, mod.P(5.4f, 0f, 2.6f), mod.Facing(0f, -1f));
            }

            // Repli : module partenaire vide → écran de surveillance avec son propre code, et fusible rapatrié.
            for (int q = 0; q < 4; q++)
            {
                Module mod = modules[q];
                GameObject monitor = BuildFallbackMonitor(m, root, mod, keypads[q]);
                Transform fuseTarget = new GameObject("FallbackFusePoint_Q" + q).transform;
                fuseTarget.SetParent(root, false);
                fuseTarget.position = mod.P(-1.5f, 0.95f, mod.Vz * 3.35f);
                Crate(m, root, mod.P(-1.5f, 0f, mod.Vz * 3.38f), mod.Facing(0f, -mod.Vz), new Vector3(0.6f, 0.8f, 0.5f));
                BuildFallback(root, mod, monitor, fuses[q], fuseTarget);
            }

            PuzzleObjective powerObjective = BuildSyncLevers(root, 1, "Remise sous tension", "Power restore", "levers", levers, prerequisites.ToArray());
            BuildHatch(m, root, modules[0], PartnerHatchSpot(modules[0]), modules[1], PartnerHatchSpot(modules[1]));
            BuildHatch(m, root, modules[2], PartnerHatchSpot(modules[2]), modules[3], PartnerHatchSpot(modules[3]));
            BuildKnockWall(m, root, modules[0], modules[1]);
            BuildKnockWall(m, root, modules[2], modules[3]);
            BuildPowerOutage(m, root, modules, powerObjective);

            BuildProbes(root, modules);
            ConfigureLighting(FloorScenes[0]);
            SaveScene(scene, FloorScenes[0]);
        }

        // ---------- Étage 2 : « Équilibrer la pression »

        static void BuildFloor2(Mats m)
        {
            Scene scene = NewFloorScene(2, out Transform root);
            FloorEnvironment(root, new Color(0.07f, 0.015f, 0.01f), 0.07f, new Color(0.1f, 0.035f, 0.03f), fx.AmbienceLabo);

            Module[] modules = BuildModules(m, root, Shell.VerticalHatches, new Color(1f, 0.4f, 0.24f), 10f, m.EmitRed, 2);
            foreach (Module mod in modules)
            {
                Plankton(root, mod, m.PlanktonLabo, new Color(1f, 0.55f, 0.3f), new Color(1f, 0.8f, 0.6f));
                // Gyrophare de secours : le faisceau tournant balaie le brouillard (rendu additif, aucune lumière temps réel).
                Beacon(m, root, mod.P(-7.05f, 2.75f, 2.2f));
            }
            OceanView(m, root, modules, 2);
            StationLife(m, root, modules, 2);

            var keypads = new CodeKeypad[4];
            var gauges = new PressureGauge[4];
            var valves = new ValveWheel[4];
            for (int q = 0; q < 4; q++)
            {
                Module mod = modules[q];
                keypads[q] = BuildKeypad(m, root, mod, 2);
                gauges[q] = BuildGauge(m, root, mod, 2);
                valves[q] = BuildValve(m, root, mod);
            }

            for (int q = 0; q < 4; q++)
            {
                Module mod = modules[q];
                // Code du partenaire écrit à l'encre UV sur le mur du fond : il faut la lampe pour le lire.
                BuildCodeDisplay(m, root, mod.P(-7.16f, 1.6f, -2f), mod.Facing(-1f, 0f), keypads[mod.Partner],
                    "CODE DU MODULE " + QuadrantNames[mod.Partner] + "  (encre UV)", true, true, "UVCode_For_Q" + mod.Partner,
                    "CODE FOR MODULE " + QuadrantNamesEn[mod.Partner] + "  (UV ink)");
                BuildFallback(root, mod, BuildFallbackMonitor(m, root, mod, keypads[q]), null, null);
            }

            var pressure = new GameObject("PressureNetwork");
            pressure.transform.SetParent(root, false);
            pressure.AddComponent<NetworkObject>();
            var network = pressure.AddComponent<PressureNetwork>();
            var so = new SerializedObject(network);
            SerializedProperty stations = so.FindProperty("stations");
            stations.arraySize = 4;
            for (int q = 0; q < 4; q++)
            {
                SerializedProperty station = stations.GetArrayElementAtIndex(q);
                station.FindPropertyRelative("quadrant").intValue = q;
                station.FindPropertyRelative("valve").objectReferenceValue = valves[q];
                station.FindPropertyRelative("gauge").objectReferenceValue = gauges[q];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // Une seule lampe UV pour toute l'équipe, dans le module orange (toujours occupé) : elle circule par les sas.
            Module first = modules[0];
            float shelfZ = -first.Vz * 3.48f;
            Static(Prim(PrimitiveType.Cube, "LampShelf", root, first.P(-5.5f, 0.85f, shelfZ), Quaternion.identity, new Vector3(1.0f, 0.05f, 0.4f), m.Panel, true));
            BuildUVLamp(m, root, first.P(-5.5f, 0.93f, shelfZ));

            BuildHatch(m, root, modules[0], PartnerHatchSpot(modules[0]), modules[1], PartnerHatchSpot(modules[1]));
            BuildHatch(m, root, modules[2], PartnerHatchSpot(modules[2]), modules[3], PartnerHatchSpot(modules[3]));
            BuildHatch(m, root, modules[0], NeighborHatchSpot(modules[0]), modules[2], NeighborHatchSpot(modules[2]));
            BuildHatch(m, root, modules[1], NeighborHatchSpot(modules[1]), modules[3], NeighborHatchSpot(modules[3]));
            BuildKnockWall(m, root, modules[0], modules[1]);
            BuildKnockWall(m, root, modules[2], modules[3]);
            for (int q = 0; q < 4; q++)
                BuildLeaks(m, root, modules[q], gauges[q].GetComponent<PuzzleObjective>());

            BuildProbes(root, modules);
            ConfigureLighting(FloorScenes[1]);
            SaveScene(scene, FloorScenes[1]);
        }

        // ---------- Étage 3 : « Remontée d'urgence » (salle commune)

        static void BuildFloor3(Mats m)
        {
            Scene scene = NewFloorScene(3, out Transform root);
            FloorEnvironment(root, new Color(0.005f, 0.015f, 0.03f), 0.06f, new Color(0.03f, 0.055f, 0.09f), fx.AmbienceCoeur);

            Module[] modules = BuildModules(m, root, Shell.OpenHall, new Color(0.25f, 0.85f, 1f), 6f, m.EmitCyan, 3);
            Vector3 center = new Vector3(PlayArea.ArenaWidth / 2f, 0f, PlayArea.ArenaDepth / 2f);
            // Bioluminescence : plancton cyan / magenta.
            foreach (Module mod in modules)
                Plankton(root, mod, m.PlanktonCoeur, new Color(0.2f, 0.9f, 1f), new Color(1f, 0.25f, 0.85f));
            OceanView(m, root, modules, 3);
            StationLife(m, root, modules, 3);

            // Plafond continu au-dessus des allées : la salle commune se lit comme un seul grand volume.
            // (Le sol des allées reste ouvert : le gouffre des abysses, bordé de failles lumineuses.)
            Box("Plafond_Allee_X", root, new Vector3(center.x, WallHeight + 0.05f, center.z), Quaternion.identity, new Vector3(1.4f, 0.1f, PlayArea.ArenaDepth), m.Ceiling, 1f);
            Box("Plafond_Allee_Z", root, new Vector3(center.x, WallHeight + 0.05f, center.z), Quaternion.identity, new Vector3(PlayArea.ArenaWidth, 0.1f, 0.9f), m.Ceiling, 1f);

            // Fissures lumineuses dans les allées entre les modules (bioluminescence).
            Static(Prim(PrimitiveType.Cube, "Rift_X", root, new Vector3(center.x, 0.01f, center.z), Quaternion.identity, new Vector3(PlayArea.ArenaWidth, 0.02f, 0.12f), m.EmitPurple, false));
            Static(Prim(PrimitiveType.Cube, "Rift_Z", root, new Vector3(center.x, 0.01f, center.z), Quaternion.identity, new Vector3(0.18f, 0.02f, PlayArea.ArenaDepth), m.EmitPurple, false));

            // Le réacteur, au point de jonction des 4 modules : chacun en voit et en touche un quart.
            Transform reactor = new GameObject("ReactorVisual").transform;
            reactor.SetParent(root, false);
            Static(Prim(PrimitiveType.Cylinder, "Base", reactor, center + Vector3.up * 0.03f, Quaternion.identity, new Vector3(1.3f, 0.03f, 1.3f), m.Hull, false));
            Static(Prim(PrimitiveType.Cylinder, "Shell", reactor, center + Vector3.up * 1.6f, Quaternion.identity, new Vector3(0.9f, 1.6f, 0.9f), m.Glass, false));
            GameObject core = Prim(PrimitiveType.Cylinder, "Core", reactor, center + Vector3.up * 1.5f, Quaternion.identity, new Vector3(0.38f, 1.35f, 0.38f), m.EmitCyan, false);
            // Habillage : socle rayé, cerclages en laiton, montants, chapeau et conduites vers la surface.
            Static(Prim(PrimitiveType.Cylinder, "Socle", reactor, center + Vector3.up * 0.12f, Quaternion.identity, new Vector3(1.05f, 0.12f, 1.05f), m.Frame, false));
            Static(Prim(PrimitiveType.Cylinder, "Socle_Raye", reactor, center + Vector3.up * 0.245f, Quaternion.identity, new Vector3(1.0f, 0.01f, 1.0f), m.Hazard, false));
            foreach (float y in new[] { 0.45f, 1.25f, 2.05f, 2.85f })
                Static(AbyssesArt.MeshObject("Cerclage", reactor, art.Ring, center + Vector3.up * y, Quaternion.Euler(90f, 0f, 0f), new Vector3(1.0f, 1.0f, 0.07f), m.Brass));
            for (int i = 0; i < 4; i++)
            {
                float angle = (45f + 90f * i) * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.5f;
                Static(Prim(PrimitiveType.Cylinder, "Montant", reactor, center + offset + Vector3.up * 1.6f, Quaternion.identity, new Vector3(0.06f, 1.6f, 0.06f), m.HullDark, false));
                Pipe(m.PipeGrey, reactor, center + offset * 0.6f + Vector3.up * 3.6f, Vector3.up, 1.2f, 0.06f);
            }
            Static(Prim(PrimitiveType.Cylinder, "Chapeau", reactor, center + Vector3.up * 3.18f, Quaternion.identity, new Vector3(1.1f, 0.08f, 1.1f), m.Frame, false));
            // Liseré lumineux au pied du socle (le réacteur tient dans l'allée : rayon 0,55 m max côté Z, sinon il traverse les vitres).
            Static(AbyssesArt.MeshObject("Halo", reactor, art.Ring, center + Vector3.up * 0.065f, Quaternion.Euler(90f, 0f, 0f), new Vector3(1.28f, 1.28f, 0.01f), m.EmitCyan));
            // Cage qui tourne lentement autour du cœur, derrière la paroi de verre : le réacteur « vit » sans lumière temps réel.
            var cage = new GameObject("Cage_Tournante").transform;
            cage.SetParent(reactor, false);
            cage.localPosition = center + Vector3.up * 1.55f;
            SetFields(cage.gameObject.AddComponent<Spin>(), ("degreesPerSecond", 30f));
            for (int i = 0; i < 3; i++)
            {
                float angle = 120f * i * Mathf.Deg2Rad;
                Prim(PrimitiveType.Cube, "Barreau", cage, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.31f, Quaternion.Euler(8f, -120f * i, 0f), new Vector3(0.035f, 2.3f, 0.035f), m.Brass, false);
            }
            // Lumière bakée du cœur : éclaire le pied du réacteur, les allées et le bas des vitres des 4 modules.
            var glowGo = new GameObject("Lumiere_Reacteur");
            glowGo.transform.SetParent(reactor, false);
            glowGo.transform.localPosition = center + Vector3.up * 1.4f;
            var glow = glowGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(0.3f, 0.85f, 1f);
            glow.range = 7f;
            glow.intensity = 2.5f;
            glow.lightmapBakeType = LightmapBakeType.Baked;
            glow.shadows = LightShadows.Soft;
            glow.shadowRadius = 0.2f;

            var consoles = new (int quadrant, XRSimpleInteractable button)[4];
            var levers = new LeverHandle[4];
            for (int q = 0; q < 4; q++)
            {
                Rect r = PlayArea.GetRect(q, PlayArea.WallInset);
                var corner = new Vector3(q % 2 == 0 ? r.xMax : r.xMin, 0f, q < 2 ? r.yMax : r.yMin);
                Vector3 toCenter = (center - corner).normalized;
                // Le levier se décale vers l'intérieur du module (jamais vers les vitres).
                var inward = new Vector3(q % 2 == 0 ? -1f : 1f, 0f, 0f);
                Vector3 position = corner - toCenter * 0.55f;
                Quaternion facingReactor = Quaternion.LookRotation(toCenter);

                Transform console = new GameObject("Console_Q" + q).transform;
                console.SetParent(root, false);
                Static(Prim(PrimitiveType.Cube, "Pedestal", console, position + Vector3.up * 0.5f, facingReactor, new Vector3(0.5f, 1.0f, 0.35f), m.Panel, true));
                Static(Prim(PrimitiveType.Cube, "Conduit", console, (position + center) / 2f + Vector3.up * 0.95f, facingReactor, new Vector3(0.06f, 0.06f, Vector3.Distance(position, center) - 0.5f), m.Avatar[q], false));
                GameObject button = Prim(PrimitiveType.Cube, "Button", console, position + Vector3.up * 1.12f, facingReactor, new Vector3(0.18f, 0.18f, 0.06f), m.Avatar[q], true);
                button.AddComponent<XRPokeFilter>();
                consoles[q] = (q, button.AddComponent<XRSimpleInteractable>());
                Halo(m, button, button);
                Label(console, "APPUIE QUAND TA COULEUR S'ALLUME", position + Vector3.up * 1.32f - toCenter * 0.02f, 0.45f, facingReactor, "PRESS WHEN YOUR COLOUR LIGHTS UP");
                levers[q] = BuildLever(m, console, q, position + inward * 0.55f, Quaternion.LookRotation(-toCenter));

                // Balisage au sol à la couleur du joueur, de sa cabine à sa console : guide le regard dans un grand volume sombre.
                Vector3 path = position - modules[q].Center;
                path.y = 0f;
                float pathLength = path.magnitude;
                path /= pathLength;
                for (float dist = 2.1f; dist < pathLength - 0.6f; dist += 0.45f)
                    Static(Prim(PrimitiveType.Cube, "Balise", console, modules[q].Center + path * dist + Vector3.up * 0.004f, Quaternion.LookRotation(path), new Vector3(0.09f, 0.008f, 0.2f), m.Avatar[q], false));
            }

            var reactorGo = new GameObject("Reactor");
            reactorGo.transform.SetParent(root, false);
            reactorGo.transform.position = center + Vector3.up * 1.5f;
            reactorGo.AddComponent<NetworkObject>();
            PuzzleObjective sequenceObjective = Objective(reactorGo, 3, -1, "Séquence du réacteur", "Reactor sequence", "reactor");
            var sequence = reactorGo.AddComponent<ReactorSequence>();
            // Le cœur est au centre de la piste : son à moitié spatialisé pour que tous l'entendent bien.
            AudioSource tone = Sfx(reactorGo, fx.ReactorTone, 0.9f, 0.4f);
            AudioSource reactorFail = Sfx(reactorGo, fx.ReactorFail, 0.8f, 0.4f);
            var rso = new SerializedObject(sequence);
            rso.FindProperty("objective").objectReferenceValue = sequenceObjective;
            SerializedProperty consoleArray = rso.FindProperty("consoles");
            consoleArray.arraySize = 4;
            for (int q = 0; q < 4; q++)
            {
                consoleArray.GetArrayElementAtIndex(q).FindPropertyRelative("quadrant").intValue = consoles[q].quadrant;
                consoleArray.GetArrayElementAtIndex(q).FindPropertyRelative("button").objectReferenceValue = consoles[q].button;
            }
            rso.FindProperty("core").objectReferenceValue = core.GetComponent<Renderer>();
            rso.FindProperty("idleMaterial").objectReferenceValue = m.EmitCyan;
            SetArray(rso.FindProperty("quadrantMaterials"), m.Avatar);
            rso.FindProperty("successMaterial").objectReferenceValue = m.StatusOk;
            rso.FindProperty("errorMaterial").objectReferenceValue = m.StatusError;
            rso.FindProperty("tone").objectReferenceValue = tone;
            rso.FindProperty("failSound").objectReferenceValue = reactorFail;
            rso.ApplyModifiedPropertiesWithoutUndo();

            BuildSyncLevers(root, 3, "Remontée d'urgence", "Emergency ascent", "ascent", levers, new[] { sequenceObjective });

            // Fin : faisceau de lumière venu de la surface + bulles dans les colonnes d'eau.
            var beamGo = new GameObject("Sunbeam");
            beamGo.transform.SetParent(root, false);
            beamGo.transform.SetPositionAndRotation(center + Vector3.up * 3.1f, Quaternion.Euler(90f, 0f, 0f));
            var beam = beamGo.AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.range = 14f;
            beam.spotAngle = 140f;
            beam.color = new Color(0.85f, 0.95f, 1f);
            beam.intensity = 0f;
            beam.shadows = LightShadows.None;

            var bubbles = new List<ParticleSystem>();
            foreach (Vector3 offset in new[] { new Vector3(-7f, 0f, 0f), new Vector3(7f, 0f, 0f), new Vector3(0f, 0f, -4f), new Vector3(0f, 0f, 4f) })
                bubbles.Add(Bubbles(root, center + offset));

            // Rayon visible : cône additif sous le puits de lumière, révélé en fondu par FloorFinale.
            var shaft = new GameObject("SunShaft");
            shaft.transform.SetParent(root, false);
            shaft.transform.position = center + Vector3.up * WallHeight;
            shaft.transform.localScale = new Vector3(4f, WallHeight, 4f);
            shaft.AddComponent<MeshFilter>().sharedMesh = fx.ShaftCone;
            var shaftRenderer = shaft.AddComponent<MeshRenderer>();
            shaftRenderer.sharedMaterial = m.SunShaft;
            shaftRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shaftRenderer.receiveShadows = false;
            shaftRenderer.enabled = false;

            AudioSource finaleSound = Sfx(root.gameObject, fx.Finale, 1f, 0f);

            var finale = root.gameObject.AddComponent<FloorFinale>();
            var fso = new SerializedObject(finale);
            fso.FindProperty("sunbeam").objectReferenceValue = beam;
            SetArray(fso.FindProperty("bubbles"), bubbles.ToArray());
            fso.FindProperty("finaleSound").objectReferenceValue = finaleSound;
            fso.FindProperty("sunShaft").objectReferenceValue = shaftRenderer;
            fso.ApplyModifiedPropertiesWithoutUndo();

            BuildProbes(root, modules);
            ConfigureLighting(FloorScenes[2]);
            SaveScene(scene, FloorScenes[2]);
        }

        // ---------- Éléments d'énigme

        /// <summary>
        /// Plaque de montage d'un dispositif mural : plaque sombre boulonnée, encadrement rayé, chemin de câbles jusqu'au plafond.
        /// Repère local du dispositif : +Z vers le mur, +Y vers le haut. <paramref name="height"/> = hauteur du centre du dispositif.
        /// </summary>
        static void DeviceMount(Mats m, Transform device, Vector2 size, float height)
        {
            Prim(PrimitiveType.Cube, "Plaque", device, new Vector3(0f, 0f, 0.03f), Quaternion.identity, new Vector3(size.x, size.y, 0.02f), m.Frame, false);
            AbyssesArt.MeshObject("Encadrement", device, AbyssesArt.FrameMesh(size.x + 0.04f, size.y + 0.04f, 0.04f, 0.035f, 0.03f), new Vector3(0f, 0f, 0.028f), Quaternion.identity, Vector3.one, m.Hazard);
            foreach (Vector2 c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
                Prim(PrimitiveType.Sphere, "Boulon", device, new Vector3(c.x * (size.x / 2f - 0.05f), c.y * (size.y / 2f - 0.05f), 0.02f), Quaternion.identity, Vector3.one * 0.03f, m.Brass, false);
            float top = size.y / 2f, ceiling = WallHeight - 0.35f - height;
            Prim(PrimitiveType.Cube, "Gaine", device, new Vector3(size.x / 2f - 0.09f, (top + ceiling) / 2f, 0.03f), Quaternion.identity, new Vector3(0.07f, ceiling - top, 0.04f), m.Frame, false);
        }

        static CodeKeypad BuildKeypad(Mats m, Transform parent, Module mod, int floor)
        {
            // Face avant vers -Z local (sens d'appui par défaut du poke), donc +Z vers le mur.
            var root = new GameObject("Keypad_Q" + mod.Q);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(mod.P(2.5f, 1.3f, mod.Vz * 3.66f), mod.Facing(0f, mod.Vz));
            root.AddComponent<NetworkObject>();
            PuzzleObjective objective = Objective(root, floor, mod.Q, "Digicode " + QuadrantNames[mod.Q], "Keypad " + QuadrantNamesEn[mod.Q], floor == 1 ? "keypad" : "keypadUV");
            var keypad = root.AddComponent<CodeKeypad>();
            Transform t = root.transform;

            Prim(PrimitiveType.Cube, "Panel", t, new Vector3(0f, 0f, 0.02f), Quaternion.identity, new Vector3(0.5f, 0.6f, 0.04f), m.Panel, false);
            DeviceMount(m, t, new Vector2(0.66f, 0.8f), 1.3f);
            var slots = new Renderer[CodeKeypad.CodeLength];
            for (int i = 0; i < slots.Length; i++)
            {
                var slotPosition = new Vector3(-0.15f + 0.1f * i, 0.18f, -0.005f);
                Prim(PrimitiveType.Cube, "SlotBack" + i, t, slotPosition, Quaternion.identity, new Vector3(0.08f, 0.08f, 0.01f), m.SlotEmpty, false);
                slots[i] = Prim(PrimitiveType.Quad, "Slot" + i, t, slotPosition + new Vector3(0f, 0f, -0.0065f), Quaternion.identity, new Vector3(0.07f, 0.07f, 1f), m.SlotEmpty, false).GetComponent<Renderer>();
            }
            Renderer status = Prim(PrimitiveType.Sphere, "Status", t, new Vector3(0.2f, 0.25f, -0.01f), Quaternion.identity, Vector3.one * 0.05f, m.StatusIdle, false).GetComponent<Renderer>();

            for (int s = 0; s < CodeKeypad.SymbolCount; s++)
            {
                float x = -0.13f + 0.13f * (s % 3);
                float y = -0.04f - 0.13f * (s / 3);
                // Touche sombre + symbole forme ET couleur (lisible par les daltoniens).
                GameObject button = Prim(PrimitiveType.Cube, "Key" + s, t, new Vector3(x, y, -0.01f), Quaternion.identity, new Vector3(0.1f, 0.1f, 0.04f), m.Key, true);
                GameObject glyph = Prim(PrimitiveType.Quad, "Glyph" + s, t, new Vector3(x, y, -0.0315f), Quaternion.identity, new Vector3(0.075f, 0.075f, 1f), m.Glyphs[s], false);
                button.AddComponent<XRSimpleInteractable>();
                button.AddComponent<XRPokeFilter>();
                Halo(m, button, button);
                var key = button.AddComponent<CodeKeypadButton>();
                SetFields(key, ("keypad", keypad), ("symbol", s), ("symbolRenderer", glyph.GetComponent<Renderer>()));
            }

            Label(t, "DIGICODE", new Vector3(0f, 0.36f, -0.01f), 0.6f, null, "KEYPAD");

            var so = new SerializedObject(keypad);
            so.FindProperty("objective").objectReferenceValue = objective;
            SetArray(so.FindProperty("entrySlots"), slots);
            SetArray(so.FindProperty("symbolMaterials"), m.Glyphs);
            so.FindProperty("emptySlotMaterial").objectReferenceValue = m.SlotEmpty;
            so.FindProperty("keySound").objectReferenceValue = Sfx(root, fx.Key, 0.6f);
            so.FindProperty("errorSound").objectReferenceValue = Sfx(root, fx.Error, 0.8f);
            so.FindProperty("successSound").objectReferenceValue = Sfx(root, fx.Success, 0.9f);
            so.FindProperty("statusLight").objectReferenceValue = status;
            so.FindProperty("idleMaterial").objectReferenceValue = m.StatusIdle;
            so.FindProperty("successMaterial").objectReferenceValue = m.StatusOk;
            so.FindProperty("errorMaterial").objectReferenceValue = m.StatusError;
            so.ApplyModifiedPropertiesWithoutUndo();
            return keypad;
        }

        static GameObject BuildCodeDisplay(Mats m, Transform parent, Vector3 position, Quaternion rotation, CodeKeypad keypad, string title, bool large, bool uvInk, string name, string englishTitle = null)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);
            Transform t = root.transform;
            float size = large ? 0.22f : 0.12f;
            float step = size * 1.3f;

            Static(Prim(PrimitiveType.Cube, "Backing", t, new Vector3(0f, 0f, 0.02f), Quaternion.identity, new Vector3(step * 4f + 0.1f, size + 0.2f, 0.04f), m.Panel, false));
            Static(AbyssesArt.MeshObject("Cadre", t, AbyssesArt.FrameMesh(step * 4f + 0.2f, size + 0.3f, 0.05f, 0.05f, 0.06f), new Vector3(0f, 0f, 0.01f), Quaternion.identity, Vector3.one, m.Frame));
            var slots = new Renderer[CodeKeypad.CodeLength];
            for (int i = 0; i < slots.Length; i++)
            {
                var slotPosition = new Vector3((i - 1.5f) * step, -0.02f, -0.005f);
                Prim(PrimitiveType.Cube, "SymbolBack" + i, t, slotPosition, Quaternion.identity, new Vector3(size, size, 0.01f), m.SlotEmpty, false);
                slots[i] = Prim(PrimitiveType.Quad, "Symbol" + i, t, slotPosition + new Vector3(0f, 0f, -0.0065f), Quaternion.identity, new Vector3(size * 0.88f, size * 0.88f, 1f), m.SlotEmpty, false).GetComponent<Renderer>();
            }
            Label(t, title, new Vector3(0f, size / 2f + 0.06f, -0.01f), large ? 0.9f : 0.5f, null, englishTitle);

            var display = root.AddComponent<CodeDisplay>();
            var so = new SerializedObject(display);
            so.FindProperty("keypad").objectReferenceValue = keypad;
            SetArray(so.FindProperty("slots"), slots);
            so.FindProperty("hiddenMaterial").objectReferenceValue = m.SlotEmpty;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (uvInk)
            {
                var reveal = root.AddComponent<UVReveal>();
                var rso = new SerializedObject(reveal);
                SetArray(rso.FindProperty("hiddenRenderers"), slots);
                rso.ApplyModifiedPropertiesWithoutUndo();
            }
            return root;
        }

        static GameObject BuildFallbackMonitor(Mats m, Transform parent, Module mod, CodeKeypad ownKeypad)
        {
            GameObject monitor = BuildCodeDisplay(m, parent, mod.P(0.6f, 1.6f, mod.Vz * 3.66f), mod.Facing(0f, mod.Vz), ownKeypad,
                "SURVEILLANCE — MODULE " + QuadrantNames[mod.Partner] + " VIDE", false, false, "FallbackMonitor_Q" + mod.Q,
                "MONITOR — MODULE " + QuadrantNamesEn[mod.Partner] + " EMPTY");
            monitor.SetActive(false);
            return monitor;
        }

        static void BuildFallback(Transform parent, Module mod, GameObject monitor, NetworkTransform relocated, Transform target)
        {
            var fallback = new GameObject("Fallback_Q" + mod.Q).AddComponent<QuadrantFallback>();
            fallback.transform.SetParent(parent, false);
            var so = new SerializedObject(fallback);
            so.FindProperty("sourceQuadrant").intValue = mod.Partner;
            SetArray(so.FindProperty("showWhenAbsent"), monitor);
            if (relocated != null)
            {
                SetArray(so.FindProperty("relocate"), relocated);
                SetArray(so.FindProperty("relocateTargets"), target);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static PuzzleObjective BuildFuseSocket(Mats m, Transform parent, Module mod)
        {
            var root = new GameObject("FuseSocket_Q" + mod.Q);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(mod.P(-2.5f, 1.2f, mod.Vz * 3.66f), mod.Facing(0f, mod.Vz));
            root.AddComponent<NetworkObject>();
            PuzzleObjective objective = Objective(root, 1, mod.Q, "Fusible " + QuadrantNames[mod.Q], "Fuse " + QuadrantNamesEn[mod.Q], "fuse");
            var socketBehaviour = root.AddComponent<FuseSocket>();
            Transform t = root.transform;

            Prim(PrimitiveType.Cube, "Panel", t, new Vector3(0f, 0f, 0.02f), Quaternion.identity, new Vector3(0.4f, 0.5f, 0.04f), m.Panel, false);
            DeviceMount(m, t, new Vector2(0.56f, 0.76f), 1.2f);
            Prim(PrimitiveType.Cylinder, "ColorRing", t, new Vector3(0f, 0f, -0.005f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.18f, 0.005f, 0.18f), m.Symbols[mod.Q], false);
            Label(t, "FUSIBLE " + SymbolNames[mod.Q], new Vector3(0f, 0.3f, -0.01f), 0.55f, null, SymbolNamesEn[mod.Q] + " FUSE");

            var socketGo = new GameObject("Socket");
            socketGo.transform.SetParent(t, false);
            socketGo.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            var trigger = socketGo.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.12f;
            var socket = socketGo.AddComponent<FuseSocketInteractor>();
            SetFields(socket, ("requiredFuseId", mod.Q));

            GameObject installed = Prim(PrimitiveType.Cylinder, "InstalledFuse", t, new Vector3(0f, 0f, -0.1f), Quaternion.identity, new Vector3(0.06f, 0.08f, 0.06f), m.Symbols[mod.Q], false);
            installed.SetActive(false);

            var localOnly = root.AddComponent<LocalModuleOnly>();
            var lo = new SerializedObject(localOnly);
            lo.FindProperty("quadrant").intValue = mod.Q;
            SetArray(lo.FindProperty("components"), socket);
            lo.ApplyModifiedPropertiesWithoutUndo();

            SetFields(socketBehaviour, ("socket", socket), ("objective", objective), ("installedVisual", installed), ("insertSound", Sfx(root, fx.FuseInsert, 0.9f)));
            return objective;
        }

        static NetworkTransform BuildFuse(Mats m, Transform parent, Module mod, int fuseId)
        {
            // Étagère contre le long mur partagé, le fusible destiné au partenaire posé dessus.
            float z = -mod.Vz * 3.48f;
            Static(Prim(PrimitiveType.Cube, "Shelf_Q" + mod.Q, parent, mod.P(-5.5f, 0.85f, z), Quaternion.identity, new Vector3(1.0f, 0.05f, 0.4f), m.Panel, true));
            GameObject fuse = Prim(PrimitiveType.Cylinder, "Fuse_" + SymbolNames[fuseId], parent, mod.P(-5.5f, 0.97f, z), Quaternion.identity, new Vector3(0.06f, 0.08f, 0.06f), m.Symbols[fuseId], true);
            NetworkTransform networkTransform = MakeNetworkGrabbable(fuse, 0.3f);
            Halo(m, fuse, fuse);
            SetFields(fuse.AddComponent<FuseItem>(), ("fuseId", fuseId));
            return networkTransform;
        }

        static NetworkTransform MakeNetworkGrabbable(GameObject go, float mass)
        {
            go.AddComponent<NetworkObject>();
            var body = go.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            go.AddComponent<XRGrabInteractable>();
            var networkTransform = go.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            networkTransform.SyncScaleX = networkTransform.SyncScaleY = networkTransform.SyncScaleZ = false;
            networkTransform.UseQuaternionCompression = true;
            go.AddComponent<NetworkGrabbable>();
            // Tombé derrière un mur ou hors zone : revient tout seul (personne ne sort de sa zone pour le ramasser).
            SetFields(go.AddComponent<LostItemReturn>(), ("returnSound", Sfx(go, fx.Bubbles, 0.7f)));
            return networkTransform;
        }

        static void BuildUVLamp(Mats m, Transform parent, Vector3 position)
        {
            // Corps le long de +Z : tenue en main, le faisceau part dans la direction pointée.
            var lamp = new GameObject("UVLamp");
            lamp.transform.SetParent(parent, false);
            lamp.transform.SetPositionAndRotation(position, Quaternion.identity);
            var collider = lamp.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.07f, 0.07f, 0.28f);
            GameObject lampBody = Prim(PrimitiveType.Cylinder, "Body", lamp.transform, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new Vector3(0.045f, 0.11f, 0.045f), m.Hull, false);
            Prim(PrimitiveType.Cylinder, "Head", lamp.transform, new Vector3(0f, 0f, 0.12f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.07f, 0.03f, 0.07f), m.Panel, false);
            Prim(PrimitiveType.Cylinder, "Lens", lamp.transform, new Vector3(0f, 0f, 0.151f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.06f, 0.003f, 0.06f), m.EmitPurple, false);

            var beamGo = new GameObject("Beam");
            beamGo.transform.SetParent(lamp.transform, false);
            beamGo.transform.localPosition = new Vector3(0f, 0f, 0.16f);
            var beam = beamGo.AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.color = new Color(0.55f, 0.2f, 1f);
            beam.range = 3.5f;
            beam.spotAngle = 45f;
            beam.intensity = 3f;
            beam.shadows = LightShadows.None;

            MakeNetworkGrabbable(lamp, 0.5f);
            Halo(m, lamp, lampBody, 1.35f);
            SetFields(lamp.AddComponent<UVLamp>(), ("beam", beam));
        }

        static PressureGauge BuildGauge(Mats m, Transform parent, Module mod, int floor)
        {
            var root = new GameObject("Gauge_Q" + mod.Q);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(mod.P(-3f, 1.5f, mod.Vz * 3.66f), mod.Facing(0f, mod.Vz));
            root.AddComponent<NetworkObject>();
            PuzzleObjective objective = Objective(root, floor, mod.Q, "Manomètre " + QuadrantNames[mod.Q], "Gauge " + QuadrantNamesEn[mod.Q], "gauge");
            var gauge = root.AddComponent<PressureGauge>();
            Transform t = root.transform;

            Prim(PrimitiveType.Cylinder, "Dial", t, new Vector3(0f, 0f, 0.015f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.52f, 0.015f, 0.52f), m.Panel, false);
            AbyssesArt.MeshObject("Cerclage", t, art.Ring, new Vector3(0f, 0f, -0.004f), Quaternion.identity, new Vector3(0.58f, 0.58f, 0.04f), m.Brass);
            DeviceMount(m, t, new Vector2(0.76f, 0.86f), 1.5f);
            Prim(PrimitiveType.Cylinder, "Face", t, new Vector3(0f, 0f, -0.002f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.46f, 0.002f, 0.46f), m.SlotEmpty, false);

            var targetPivot = new GameObject("TargetPivot").transform;
            targetPivot.SetParent(t, false);
            targetPivot.localPosition = new Vector3(0f, 0f, -0.006f);
            Prim(PrimitiveType.Cube, "GreenZone", targetPivot, new Vector3(0f, 0.18f, 0f), Quaternion.identity, new Vector3(0.09f, 0.05f, 0.003f), m.StatusOk, false);

            var needlePivot = new GameObject("NeedlePivot").transform;
            needlePivot.SetParent(t, false);
            needlePivot.localPosition = new Vector3(0f, 0f, -0.012f);
            Prim(PrimitiveType.Cube, "Needle", needlePivot, new Vector3(0f, 0.09f, 0f), Quaternion.identity, new Vector3(0.014f, 0.19f, 0.004f), m.EmitRed, false);
            Prim(PrimitiveType.Sphere, "Hub", t, new Vector3(0f, 0f, -0.016f), Quaternion.identity, Vector3.one * 0.035f, m.Hull, false);

            Renderer status = Prim(PrimitiveType.Sphere, "Status", t, new Vector3(0.3f, 0.2f, -0.01f), Quaternion.identity, Vector3.one * 0.05f, m.StatusIdle, false).GetComponent<Renderer>();
            Label(t, "MANOMÈTRE " + QuadrantNames[mod.Q], new Vector3(0f, 0.34f, -0.01f), 0.6f, null, "GAUGE " + QuadrantNamesEn[mod.Q]);
            Label(t, "Un autre plongeur contrôle ta pression", new Vector3(0f, -0.32f, -0.01f), 0.4f, null, "Another diver controls your pressure");

            SetFields(gauge, ("objective", objective), ("needle", needlePivot), ("targetMarker", targetPivot), ("statusLight", status),
                ("idleMaterial", m.StatusIdle), ("inBandMaterial", m.EmitCyan), ("solvedMaterial", m.StatusOk));
            return gauge;
        }

        static ValveWheel BuildValve(Mats m, Transform parent, Module mod)
        {
            Static(Prim(PrimitiveType.Cube, "ValvePost_Q" + mod.Q, parent, mod.P(5.4f, 0.55f, 2.6f), Quaternion.identity, new Vector3(0.16f, 1.1f, 0.16f), m.Panel, true));
            Static(Prim(PrimitiveType.Cube, "ValveStripe_Q" + mod.Q, parent, mod.P(5.4f, 0.9f, 2.6f), Quaternion.identity, new Vector3(0.17f, 0.05f, 0.17f), m.Avatar[mod.Q], false));

            var root = new GameObject("Valve_Q" + mod.Q);
            root.transform.SetParent(parent, false);
            // Volant incliné vers le joueur (au sud du poteau), comme un volant de voiture.
            root.transform.SetPositionAndRotation(mod.P(5.4f, 1.25f, 2.6f), mod.Facing(0f, -1f) * Quaternion.Euler(-35f, 0f, 0f));
            var wheel = new GameObject("Wheel").transform;
            wheel.SetParent(root.transform, false);
            GameObject rim = Prim(PrimitiveType.Cylinder, "Rim", wheel, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new Vector3(0.4f, 0.015f, 0.4f), m.EmitRed, false);
            Prim(PrimitiveType.Cube, "SpokeA", wheel, new Vector3(0f, 0f, 0.02f), Quaternion.identity, new Vector3(0.38f, 0.035f, 0.03f), m.Hull, false);
            Prim(PrimitiveType.Cube, "SpokeB", wheel, new Vector3(0f, 0f, 0.02f), Quaternion.identity, new Vector3(0.035f, 0.38f, 0.03f), m.Hull, false);
            Prim(PrimitiveType.Sphere, "Handle", wheel, new Vector3(0.17f, 0f, 0.04f), Quaternion.identity, Vector3.one * 0.06f, m.Avatar[mod.Q], false);

            var collider = root.AddComponent<SphereCollider>();
            collider.radius = 0.22f;
            root.AddComponent<XRSimpleInteractable>();
            Halo(m, root, rim, 1.12f);
            var valve = root.AddComponent<ValveWheel>();
            SetFields(valve, ("wheel", wheel));
            Label(parent, "VANNE " + QuadrantNames[mod.Q], mod.P(5.4f, 1.6f, 2.6f), 0.55f, mod.Facing(0f, 1f), "VALVE " + QuadrantNamesEn[mod.Q]);
            return valve;
        }

        static LeverHandle BuildLever(Mats m, Transform parent, int quadrant, Vector3 position, Quaternion rotation)
        {
            // +Z local du levier vers le joueur : il tire le levier vers lui.
            var root = new GameObject("Lever_Q" + quadrant);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);
            Transform t = root.transform;

            Static(Prim(PrimitiveType.Cube, "Base", t, new Vector3(0f, 0.5f, 0f), Quaternion.identity, new Vector3(0.3f, 1.0f, 0.3f), m.Panel, true));
            Static(Prim(PrimitiveType.Cube, "Platine", t, new Vector3(0f, 0.006f, 0f), Quaternion.identity, new Vector3(0.6f, 0.012f, 0.6f), m.Hazard, false));
            Static(Prim(PrimitiveType.Cube, "Stripe", t, new Vector3(0f, 0.85f, 0.151f), Quaternion.identity, new Vector3(0.25f, 0.05f, 0.005f), m.Avatar[quadrant], false));
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(t, false);
            pivot.localPosition = new Vector3(0f, 1.0f, 0f);
            Prim(PrimitiveType.Cylinder, "Arm", pivot, new Vector3(0f, 0.22f, 0f), Quaternion.identity, new Vector3(0.04f, 0.22f, 0.04f), m.Hull, false);
            GameObject knob = Prim(PrimitiveType.Sphere, "Knob", pivot, new Vector3(0f, 0.45f, 0f), Quaternion.identity, Vector3.one * 0.1f, m.EmitRed, true);
            knob.AddComponent<XRSimpleInteractable>();
            Halo(m, knob, knob);
            var lever = knob.AddComponent<LeverHandle>();
            SetFields(lever, ("pivot", pivot));
            return lever;
        }

        static PuzzleObjective BuildSyncLevers(Transform parent, int floor, string label, string english, string hintKey, LeverHandle[] levers, PuzzleObjective[] prerequisites)
        {
            var root = new GameObject("SyncLevers");
            root.transform.SetParent(parent, false);
            root.AddComponent<NetworkObject>();
            PuzzleObjective objective = Objective(root, floor, -1, label, english, hintKey);
            var group = root.AddComponent<SyncLeverGroup>();
            AudioSource fail = Sfx(root, fx.LeverFail, 0.9f, 0f);
            var so = new SerializedObject(group);
            so.FindProperty("objective").objectReferenceValue = objective;
            SetArray(so.FindProperty("prerequisites"), prerequisites);
            so.FindProperty("failSound").objectReferenceValue = fail;
            SerializedProperty entries = so.FindProperty("levers");
            entries.arraySize = levers.Length;
            for (int q = 0; q < levers.Length; q++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(q);
                entry.FindPropertyRelative("quadrant").intValue = q;
                entry.FindPropertyRelative("lever").objectReferenceValue = levers[q];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return objective;
        }

        // ---------- Sas à objets

        struct HatchSpot
        {
            public Module Module;
            public Vector3 Tray;
            public Vector3 Button;
            public Quaternion ButtonFacing;
            public Vector3 Label;
        }

        /// <summary>Sas dans le mur commun avec le partenaire (axe Z).</summary>
        static HatchSpot PartnerHatchSpot(Module mod) => new HatchSpot
        {
            Module = mod,
            Tray = mod.P(6.95f, 0.88f, -1.5f),
            Button = mod.P(7.17f, 1.35f, -2.1f),
            ButtonFacing = mod.Facing(1f, 0f),
            Label = mod.P(7.15f, 1.52f, -2.1f),
        };

        /// <summary>Sas dans le long mur partagé avec le voisin du dessus/dessous (axe X).</summary>
        static HatchSpot NeighborHatchSpot(Module mod) => new HatchSpot
        {
            Module = mod,
            Tray = mod.P(2f, 0.88f, -mod.Vz * 3.45f),
            Button = mod.P(2.6f, 1.35f, -mod.Vz * 3.67f),
            ButtonFacing = mod.Facing(0f, -mod.Vz),
            Label = mod.P(2.6f, 1.52f, -mod.Vz * 3.65f),
        };

        static void BuildHatch(Mats m, Transform parent, Module a, HatchSpot spotA, Module b, HatchSpot spotB)
        {
            var root = new GameObject("Hatch_Q" + a.Q + "_Q" + b.Q);
            root.transform.SetParent(parent, false);
            root.AddComponent<NetworkObject>();
            var hatch = root.AddComponent<TransferHatch>();

            // Tube vitré d'un plateau à l'autre, à travers les murs et l'allée.
            Vector3 from = new Vector3(spotA.Tray.x, 1.15f, spotA.Tray.z);
            Vector3 to = new Vector3(spotB.Tray.x, 1.15f, spotB.Tray.z);
            Static(Prim(PrimitiveType.Cylinder, "Tube", root.transform, (from + to) / 2f, Quaternion.FromToRotation(Vector3.up, (to - from).normalized),
                new Vector3(0.45f, Vector3.Distance(from, to) / 2f, 0.45f), m.Glass, false));

            var so = new SerializedObject(hatch);
            FillEndpoint(m, root.transform, spotA, so.FindProperty("sideA"));
            FillEndpoint(m, root.transform, spotB, so.FindProperty("sideB"));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void FillEndpoint(Mats m, Transform parent, HatchSpot spot, SerializedProperty endpoint)
        {
            int q = spot.Module.Q;
            Static(Prim(PrimitiveType.Cube, "Tray_Q" + q, parent, spot.Tray, Quaternion.identity, new Vector3(0.5f, 0.04f, 0.5f), m.Panel, true));

            var zone = new GameObject("Zone_Q" + q).AddComponent<BoxCollider>();
            zone.transform.SetParent(parent, false);
            zone.transform.position = spot.Tray + Vector3.up * 0.22f;
            zone.size = new Vector3(0.45f, 0.4f, 0.45f);
            zone.isTrigger = true;

            var arrival = new GameObject("Arrival_Q" + q).transform;
            arrival.SetParent(parent, false);
            arrival.position = spot.Tray + Vector3.up * 0.12f;

            // Bouton sur le mur, face avant vers -Z local (vers la pièce).
            GameObject button = Prim(PrimitiveType.Cube, "SendButton_Q" + q, parent, spot.Button, spot.ButtonFacing, new Vector3(0.12f, 0.12f, 0.06f), m.EmitRed, true);
            Static(AbyssesArt.MeshObject("Cadre_Bouton", parent, AbyssesArt.FrameMesh(0.22f, 0.22f, 0.03f, 0.05f, 0.05f), spot.Button + spot.ButtonFacing * new Vector3(0f, 0f, 0.01f), spot.ButtonFacing, Vector3.one, m.Hazard));
            var interactable = button.AddComponent<XRSimpleInteractable>();
            button.AddComponent<XRPokeFilter>();
            Halo(m, button, button);
            Label(parent, "ENVOYER", spot.Label, 0.5f, spot.ButtonFacing, "SEND");

            endpoint.FindPropertyRelative("quadrant").intValue = q;
            endpoint.FindPropertyRelative("zone").objectReferenceValue = zone;
            endpoint.FindPropertyRelative("arrivalPoint").objectReferenceValue = arrival;
            endpoint.FindPropertyRelative("sendButton").objectReferenceValue = interactable;
            endpoint.FindPropertyRelative("bubbles").objectReferenceValue = Bubbles(parent, spot.Tray + Vector3.up * 0.06f, false);
            var soundGo = new GameObject("HatchSound_Q" + q);
            soundGo.transform.SetParent(parent, false);
            soundGo.transform.position = spot.Tray;
            endpoint.FindPropertyRelative("sound").objectReferenceValue = Sfx(soundGo, fx.Hatch, 0.9f);
        }

        // ------------------------------------------------------------------ Scène Station

        static void BuildStation(Mats m, GameObject playerPrefab, GameObject cabinPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.04f, 0.07f, 0.09f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.01f, 0.04f, 0.06f);
            RenderSettings.fogDensity = 0.08f;

            // Réseau
            var network = new GameObject("Network");
            var networkManager = network.AddComponent<NetworkManager>();
            var transport = network.AddComponent<UnityTransport>();
            var discovery = network.AddComponent<LanDiscovery>();
            var connection = network.AddComponent<ConnectionManager>();
            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.PlayerPrefab = playerPrefab;
            networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList> { BuildPrefabList(playerPrefab) };
            SetFields(connection, ("networkManager", networkManager), ("transport", transport), ("lanDiscovery", discovery));
            SetFields(network.AddComponent<DevAutoConnect>(), ("connection", connection));

            var gameState = new GameObject("GameState");
            gameState.AddComponent<NetworkObject>();
            gameState.AddComponent<GameStateManager>();
            gameState.AddComponent<HintSystem>();

            // Fond sonore permanent (bourdonnement de la station, sonar) ; chaque étage ajoute sa propre ambiance.
            var ambience = new GameObject("Ambience").AddComponent<AudioSource>();
            ambience.clip = fx.AmbienceStation;
            ambience.loop = true;
            ambience.playOnAwake = true;
            ambience.spatialBlend = 0f;
            ambience.volume = 0.3f;
            AudioSource phaseSource = Sfx(new GameObject("PhaseAudio"), null, 1f, 0f);
            SetFields(phaseSource.gameObject.AddComponent<PhaseAudio>(), ("source", phaseSource), ("floorSolved", fx.FloorSolved));

            new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<XRUIInputModule>();

            // Rig XR local
            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPath);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            rig.transform.position = PlayArea.GetCenter(0);
            Transform cameraTransform = Find(rig.transform, "Main Camera");
            var camera = cameraTransform.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.farClipPlane = 40f;
            // Oreilles qui se bouchent pendant la descente (filtre sur l'auditeur, actif seulement en descente).
            cameraTransform.gameObject.AddComponent<AudioLowPassFilter>();
            cameraTransform.gameObject.AddComponent<DescentEars>();

            rig.AddComponent<XRRenderSettings>();
            var localRig = rig.AddComponent<LocalRig>();
            SetFields(localRig, ("leftHand", Find(rig.transform, "Left Controller")), ("rightHand", Find(rig.transform, "Right Controller")));

            // Debug : rotation de la vue par crans au stick droit (coupée en build EVA avec le reste du joystick).
            var stickTurn = new GameObject("Stick Turn");
            stickTurn.transform.SetParent(Find(rig.transform, "Locomotion"), false);
            SetFields(stickTurn.AddComponent<StickTurn>(), ("origin", rig.GetComponent<Unity.XR.CoreUtils.XROrigin>()),
                ("moveProvider", rig.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement.ContinuousMoveProvider>(true)));

            var gate = rig.AddComponent<LocomotionGate>();
            var gateSo = new SerializedObject(gate);
            SetArray(gateSo.FindProperty("debugOnly"), Find(rig.transform, "Move").gameObject, Find(rig.transform, "Gravity").gameObject, stickTurn);
            var forbidden = new List<GameObject>();
            foreach (string n in new[] { "Teleportation", "Climb", "Jump", "Grab Move", "Turn" })
            {
                Transform f = Find(rig.transform, n);
                if (f != null) forbidden.Add(f.gameObject);
            }
            SetArray(gateSo.FindProperty("alwaysDisabled"), forbidden.ToArray());
            SetArray(gateSo.FindProperty("controllers"), rig.GetComponentsInChildren<ControllerInputActionManager>(true));
            gateSo.ApplyModifiedPropertiesWithoutUndo();

            // Alarme de sortie de zone : voile devant les yeux + son.
            GameObject overlay = Prim(PrimitiveType.Quad, "ZoneWarningOverlay", cameraTransform, new Vector3(0f, 0f, 0.1f), Quaternion.identity, new Vector3(0.6f, 0.6f, 1f), m.Warning, false);
            var overlayRenderer = overlay.GetComponent<Renderer>();
            overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            overlayRenderer.enabled = false;
            var alarm = cameraTransform.gameObject.AddComponent<AudioSource>();
            alarm.playOnAwake = false;
            alarm.loop = true;
            alarm.clip = fx.ZoneAlarm;
            alarm.spatialBlend = 0f;
            var guard = rig.AddComponent<ZoneGuard>();
            SetFields(guard, ("warningOverlay", overlayRenderer), ("alarm", alarm));

            BuildMenu(rig.transform, camera);
            BuildDiveWatch(m, Find(rig.transform, "Left Controller"));
            BuildGameMaster();

            // Simulateur XR : uniquement dans l'éditeur (tag EditorOnly = retiré des builds).
            var simulatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPath);
            if (simulatorPrefab != null)
            {
                var simulator = (GameObject)PrefabUtility.InstantiatePrefab(simulatorPrefab);
                simulator.tag = "EditorOnly";
            }

            // Les 4 cabines, au centre de chaque quadrant.
            var cabins = new GameObject("Cabins").transform;
            for (int q = 0; q < 4; q++)
            {
                var cabin = (GameObject)PrefabUtility.InstantiatePrefab(cabinPrefab, cabins);
                cabin.name = "Cabin_Q" + q;
                cabin.transform.position = PlayArea.GetCenter(q);
                var cabinSo = new SerializedObject(cabin.GetComponent<ElevatorCabin>());
                cabinSo.FindProperty("quadrant").intValue = q;
                cabinSo.ApplyModifiedPropertiesWithoutUndo();
            }

            SaveScene(scene, "Station");
        }

        static NetworkPrefabsList BuildPrefabList(GameObject playerPrefab)
        {
            const string path = Root + "/Prefabs/NetworkPrefabs.asset";
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(path);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, path);
            }
            if (!list.Contains(playerPrefab)) list.Add(new NetworkPrefab { Prefab = playerPrefab });
            EditorUtility.SetDirty(list);
            return list;
        }

        static void BuildMenu(Transform rig, Camera camera)
        {
            var canvasGo = new GameObject("MainMenu", typeof(RectTransform));
            canvasGo.transform.SetParent(rig, false);
            canvasGo.transform.localPosition = new Vector3(0f, 1.45f, 0.85f);
            canvasGo.transform.localScale = Vector3.one * 0.001f;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();
            ((RectTransform)canvasGo.transform).sizeDelta = new Vector2(820f, 1200f);

            RectTransform panel = UIRect("Panel", canvasGo.transform);
            Stretch(panel);
            panel.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.06f, 0.09f, 0.9f);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(48, 48, 40, 40);
            layout.spacing = 16f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            UIText(panel, "Title", "ABYSSES", 96f, FontStyles.Bold, 110f, new Color(0.4f, 0.95f, 1f));
            UIText(panel, "Subtitle", "Station HADAL-4", 34f, FontStyles.Italic, 44f, new Color(0.7f, 0.85f, 0.9f));
            TextMeshProUGUI status = UIText(panel, "Status", "Hors ligne", 34f, FontStyles.Normal, 96f, Color.white);
            TextMeshProUGUI players = UIText(panel, "Players", "", 30f, FontStyles.Normal, 40f, new Color(0.7f, 0.9f, 1f));
            TextMeshProUGUI hint = UIText(panel, "Hint", "", 28f, FontStyles.Italic, 80f, new Color(1f, 0.85f, 0.5f));

            TextMeshProUGUI leaderboard = UIText(panel, "Leaderboard", "", 26f, FontStyles.Normal, 190f, new Color(0.75f, 0.95f, 1f));

            RectTransform offline = Group(panel, "Offline");
            Button joinOnline = UIButton(offline, "Rejoindre l'équipe (en ligne)", "Join the team (online)");
            Button hostLan = UIButton(offline, "Héberger (réseau local)", "Host (local network)");
            Button joinLan = UIButton(offline, "Rejoindre (réseau local)", "Join (local network)");
            Button solo = UIButton(offline, "Jouer en solo", "Play solo");

            RectTransform online = Group(panel, "Online");
            Button start = UIButton(online, "Lancer la plongée");
            Button easy = UIButton(online, "Mode facile : non");
            Button recenter = UIButton(online, "Recalibrer ma position", "Recalibrate my position");
            Button leave = UIButton(online, "Quitter la partie", "Leave the game");

            Button language = UIButton(panel, "English");

            var menu = canvasGo.AddComponent<MainMenu>();
            SetFields(menu,
                ("panel", panel.gameObject), ("statusText", status), ("playersText", players),
                ("offlineGroup", offline.gameObject), ("joinOnlineButton", joinOnline), ("hostLanButton", hostLan),
                ("joinLanButton", joinLan), ("soloButton", solo), ("onlineGroup", online.gameObject),
                ("startButton", start), ("recenterButton", recenter), ("leaveButton", leave), ("hintText", hint),
                ("languageButton", language), ("easyButton", easy), ("leaderboardText", leaderboard));
        }

        static RectTransform Group(Transform parent, string name)
        {
            RectTransform rect = UIRect(name, parent);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            return rect;
        }

        static RectTransform UIRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static TextMeshProUGUI UIText(Transform parent, string name, string text, float size, FontStyles style, float height, Color color)
        {
            RectTransform rect = UIRect(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return label;
        }

        static Button UIButton(Transform parent, string text, string english = null)
        {
            RectTransform rect = UIRect("Button - " + text, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Color.white;
            var button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.06f, 0.3f, 0.38f, 1f);
            colors.highlightedColor = new Color(0.1f, 0.5f, 0.6f, 1f);
            colors.pressedColor = new Color(0.2f, 0.75f, 0.85f, 1f);
            colors.disabledColor = new Color(0.15f, 0.18f, 0.2f, 0.6f);
            button.colors = colors;
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 84f;

            RectTransform labelRect = UIRect("Label", rect);
            Stretch(labelRect);
            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 36f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            if (english != null) SetFields(labelRect.gameObject.AddComponent<LocalizedText>(), ("french", text), ("english", english));
            return button;
        }

        // ------------------------------------------------------------------ Ambiance et lumière

        /// <summary>Brouillard, lumière ambiante et boucle sonore d'un étage. Valeurs posées aussi sur la scène (utilisées par le bake).</summary>
        static void FloorEnvironment(Transform root, Color fog, float density, Color ambient, AudioClip loop)
        {
            SetFields(root.gameObject.AddComponent<FloorAmbience>(), ("fogColor", fog), ("fogDensity", density), ("ambientColor", ambient));
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = density;

            var audio = root.gameObject.AddComponent<AudioSource>();
            audio.clip = loop;
            audio.loop = true;
            audio.playOnAwake = true;
            audio.spatialBlend = 0f;
            audio.volume = 0.55f;
        }

        /// <summary>Particules en suspension dans un module (plancton, poussière) : donnent du volume au brouillard.</summary>
        static void Plankton(Transform parent, Module mod, Material material, Color a, Color b)
        {
            var go = new GameObject("Plankton_Q" + mod.Q);
            go.transform.SetParent(parent, false);
            go.transform.position = mod.Center + Vector3.up * 1.5f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.prewarm = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.05f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.maxParticles = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 16f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(PlayArea.QuadrantWidth - 1.5f, 2.6f, PlayArea.QuadrantDepth - 1.5f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0.035f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 0.02f;
        }

        /// <summary>Étage 1 : caustiques animées projetées par une directionnelle temps réel sans ombres (cookie).</summary>
        static void BuildCaustics(Transform root)
        {
            var go = new GameObject("Caustics");
            go.transform.SetParent(root, false);
            go.transform.rotation = Quaternion.Euler(72f, 25f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.45f, 0.85f, 1f);
            light.intensity = 0.9f;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.cookie = fx.Caustics[0];
            if (!go.TryGetComponent(out UniversalAdditionalLightData data)) data = go.AddComponent<UniversalAdditionalLightData>();
            data.lightCookieSize = new Vector2(3.5f, 3.5f);
            var animator = go.AddComponent<CausticsAnimator>();
            var so = new SerializedObject(animator);
            SetArray(so.FindProperty("frames"), fx.Caustics);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Beacon(Mats m, Transform parent, Vector3 position)
        {
            Transform root = new GameObject("Beacon").transform;
            root.SetParent(parent, false);
            root.position = position;
            Static(Prim(PrimitiveType.Cylinder, "Base", root, Vector3.zero, Quaternion.identity, new Vector3(0.16f, 0.03f, 0.16f), m.Hull, false));
            Prim(PrimitiveType.Sphere, "Dome", root, new Vector3(0f, 0.07f, 0f), Quaternion.identity, Vector3.one * 0.13f, m.EmitRed, false);
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(0f, 0.07f, 0f);
            SetFields(pivot.gameObject.AddComponent<Spin>(), ("degreesPerSecond", 150f));
            foreach (float side in new[] { 90f, -90f })
            {
                var shaft = new GameObject("Shaft");
                shaft.transform.SetParent(pivot, false);
                shaft.transform.localRotation = Quaternion.Euler(0f, 0f, side);
                shaft.transform.localScale = new Vector3(0.55f, 3.2f, 0.55f);
                shaft.AddComponent<MeshFilter>().sharedMesh = fx.ShaftCone;
                var renderer = shaft.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = m.ShaftRed;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        static readonly float[] ProbeHeights = { 0.4f, 1.5f, 2.8f };

        /// <summary>Light probes (éclairage des objets mobiles : avatars, fusibles, lampe) et une reflection probe par module.</summary>
        static void BuildProbes(Transform root, Module[] modules)
        {
            var group = new GameObject("LightProbes").AddComponent<LightProbeGroup>();
            group.transform.SetParent(root, false);
            var positions = new List<Vector3>();
            foreach (Module mod in modules)
            {
                Rect r = PlayArea.GetRect(mod.Q, PlayArea.WallInset + 0.25f);
                for (int ix = 0; ix <= 6; ix++)
                for (int iz = 0; iz <= 3; iz++)
                foreach (float y in ProbeHeights)
                    positions.Add(new Vector3(Mathf.Lerp(r.xMin, r.xMax, ix / 6f), y, Mathf.Lerp(r.yMin, r.yMax, iz / 3f)));

                Rect inner = PlayArea.GetRect(mod.Q, PlayArea.WallInset);
                var probe = new GameObject("ReflectionProbe_Q" + mod.Q).AddComponent<ReflectionProbe>();
                probe.transform.SetParent(root, false);
                probe.transform.position = mod.Center + Vector3.up * 1.6f;
                probe.mode = ReflectionProbeMode.Baked;
                probe.boxProjection = true;
                probe.size = new Vector3(inner.width, WallHeight, inner.height);
                probe.center = new Vector3(0f, WallHeight / 2f - 1.6f, 0f);
                probe.resolution = 64;
                probe.hdr = false;
            }
            group.probePositions = positions.ToArray();
        }

        /// <summary>Réglages de bake de l'étage (scène active) : lightmaps légères, AO, sans directionnalité (mobile).</summary>
        static void ConfigureLighting(string sceneName)
        {
            string path = SceneDir + "/Lighting_" + sceneName + ".lighting";
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (settings == null)
            {
                EnsureFolder(SceneDir);
                settings = new LightingSettings { name = "Lighting_" + sceneName };
                AssetDatabase.CreateAsset(settings, path);
            }
            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            settings.lightmapResolution = 10f;
            settings.lightmapPadding = 2;
            settings.lightmapMaxSize = 1024;
            settings.directSampleCount = 16;
            settings.indirectSampleCount = 128;
            settings.environmentSampleCount = 64;
            settings.maxBounces = 2;
            settings.ao = true;
            settings.aoMaxDistance = 0.8f;
            settings.aoExponentIndirect = 1f;
            settings.aoExponentDirect = 0.3f;
            settings.directionalityMode = LightmapsMode.NonDirectional;
            settings.lightmapCompression = LightmapCompression.NormalQuality;
            EditorUtility.SetDirty(settings);
            Lightmapping.lightingSettings = settings;
        }

        static AudioSource Sfx(GameObject go, AudioClip clip, float volume, float spatialBlend = 1f)
        {
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.playOnAwake = false;
            source.spatialBlend = spatialBlend;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 0.6f;
            source.maxDistance = 12f;
            return source;
        }

        // ------------------------------------------------------------------ Build Settings

        static void SetBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(SceneDir + "/Station.unity", true) };
            foreach (string floor in FloorScenes)
                scenes.Add(new EditorBuildSettingsScene(SceneDir + "/" + floor + ".unity", true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------------ Utilitaires

        static PuzzleObjective Objective(GameObject go, int floor, int quadrant, string label, string english = null, string hintKey = null)
        {
            var objective = go.AddComponent<PuzzleObjective>();
            SetFields(objective, ("floor", floor), ("quadrant", quadrant), ("label", label), ("labelEnglish", english ?? label));
            if (hintKey != null && AbyssesHints.Table.TryGetValue(hintKey, out (string[] fr, string[] en) hints))
            {
                var so = new SerializedObject(objective);
                SetStrings(so.FindProperty("hints"), hints.fr);
                SetStrings(so.FindProperty("hintsEnglish"), hints.en);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return objective;
        }

        static void SetStrings(SerializedProperty property, string[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).stringValue = values[i];
        }

        /// <summary>Halo de survol (copie additive légèrement agrandie du visuel) piloté par <see cref="HoverHighlight"/>.</summary>
        static void Halo(Mats m, GameObject interactable, GameObject visual, float scale = 1.18f)
        {
            var filter = visual.GetComponent<MeshFilter>();
            if (filter == null) return;
            var halo = new GameObject("Halo");
            halo.transform.SetParent(visual.transform, false);
            halo.transform.localScale = Vector3.one * scale;
            halo.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var renderer = halo.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = m.Highlight;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            SetFields(interactable.AddComponent<HoverHighlight>(), ("halo", renderer));
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale, Material material, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            // Les positions passées sont mondiales pour les objets de niveau, locales pour les sous-objets d'un prefab :
            // on les applique en local, et les appelants de niveau passent des parents à l'origine.
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static void Static(GameObject go)
        {
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
        }

        static void Label(Transform parent, string text, Vector3 localPosition, float size, Quaternion? rotation = null, string english = null)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.75f, 0.92f, 1f);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            ((RectTransform)go.transform).sizeDelta = new Vector2(2f, 0.3f);
            if (english != null) SetFields(go.AddComponent<LocalizedText>(), ("french", text), ("english", english));
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = Find(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        static void SetFields(Object target, params (string name, object value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach ((string name, object value) in fields)
            {
                SerializedProperty p = so.FindProperty(name);
                if (p == null)
                {
                    Debug.LogError($"[Abysses] Champ {name} introuvable sur {target.GetType().Name}");
                    continue;
                }
                switch (value)
                {
                    case Object o: p.objectReferenceValue = o; break;
                    case int i: p.intValue = i; break;
                    case float f: p.floatValue = f; break;
                    case bool b: p.boolValue = b; break;
                    case string s: p.stringValue = s; break;
                    case Color c: p.colorValue = c; break;
                    case Vector3 v3: p.vector3Value = v3; break;
                    case Vector2 v2: p.vector2Value = v2; break;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(SerializedProperty property, params Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
