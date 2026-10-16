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
    public static class AbyssesBuilder
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
        static readonly string[] SymbolNames = { "ROUGE", "VERT", "BLEU", "JAUNE", "BLANC", "VIOLET" };
        static readonly Color[] SymbolColors =
        {
            new Color(0.9f, 0.12f, 0.08f), new Color(0.15f, 0.85f, 0.25f), new Color(0.15f, 0.4f, 1f),
            new Color(1f, 0.85f, 0.1f), new Color(0.95f, 0.95f, 0.95f), new Color(0.65f, 0.2f, 0.95f),
        };

        const float WallHeight = 3.2f;
        const float WallThickness = 0.2f;

        // ------------------------------------------------------------------ Menus

        [MenuItem("Abysses/Tout construire", priority = 0)]
        public static void BuildAll()
        {
            // Les scènes doivent être dans les Build Settings AVANT leur sauvegarde (identifiants réseau des objets placés).
            SetBuildScenes();
            Mats m = BuildMaterials();
            BuildConfig();
            GameObject player = BuildPlayerPrefab(m);
            GameObject cabin = BuildCabinPrefab(m);
            BuildFloor1(m);
            BuildFloor2(m);
            BuildFloor3(m);
            BuildStation(m, player, cabin);
            AssetDatabase.SaveAssets();
            Debug.Log("[Abysses] Construction terminée.");
        }

        // ------------------------------------------------------------------ Matériaux

        class Mats
        {
            public Material Hull, Panel, Floor, Glass, EmitCyan, EmitWarm, EmitRed, EmitPurple, Visor, SlotEmpty, StatusIdle, StatusOk, StatusError, Warning, Crate;
            public Material[] Avatar = new Material[4];
            public Material[] Symbols = new Material[6];
        }

        static Mats BuildMaterials()
        {
            EnsureFolder(MaterialDir);
            var m = new Mats
            {
                Hull = Lit("M_Hull", new Color(0.16f, 0.19f, 0.23f), 0.7f, 0.45f),
                Panel = Lit("M_Panel", new Color(0.32f, 0.36f, 0.4f), 0.5f, 0.4f),
                Floor = Lit("M_Floor", new Color(0.1f, 0.12f, 0.14f), 0.4f, 0.55f),
                Crate = Lit("M_Crate", new Color(0.35f, 0.3f, 0.22f), 0.1f, 0.3f),
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
            };
            for (int i = 0; i < 4; i++)
                m.Avatar[i] = Lit("M_Avatar_Q" + i, QuadrantColors[i] * 0.6f, 0.3f, 0.6f, QuadrantColors[i]);
            for (int i = 0; i < 6; i++)
                m.Symbols[i] = Unlit("M_Symbol_" + i, SymbolColors[i]);
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
            if (!unlit) material.SetFloat("_Smoothness", 0.95f);
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

            var so = new SerializedObject(player);
            so.FindProperty("head").objectReferenceValue = head;
            so.FindProperty("leftHand").objectReferenceValue = left;
            so.FindProperty("rightHand").objectReferenceValue = right;
            SetArray(so.FindProperty("tintedRenderers"), helmet.GetComponent<Renderer>(), leftGlove.GetComponent<Renderer>(), rightGlove.GetComponent<Renderer>());
            SetArray(so.FindProperty("quadrantMaterials"), m.Avatar);
            SetArray(so.FindProperty("hiddenForOwner"), helmet, visor, lamp, leftGlove, rightGlove);
            so.ApplyModifiedPropertiesWithoutUndo();

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

            Static(Prim(PrimitiveType.Cube, "Floor", t, new Vector3(0f, -0.03f, 0f), Quaternion.identity, new Vector3(inner + 2 * w, 0.1f, inner + 2 * w), m.Floor, true));
            Static(Prim(PrimitiveType.Cube, "Ceiling", t, new Vector3(0f, h + 0.05f, 0f), Quaternion.identity, new Vector3(inner + 2 * w, 0.1f, inner + 2 * w), m.Hull, true));
            Static(Prim(PrimitiveType.Cube, "Wall_Back", t, new Vector3(0f, h / 2f, -half), Quaternion.identity, new Vector3(inner + 2 * w, h, w), m.Hull, true));

            // Murs latéraux avec un hublot (on voit les bulles défiler pendant la descente).
            foreach (float side in new[] { -1f, 1f })
            {
                string n = side < 0 ? "L" : "R";
                float x = side * half;
                Static(Prim(PrimitiveType.Cube, "Wall_" + n + "_Low", t, new Vector3(x, 0.6f, 0f), Quaternion.identity, new Vector3(w, 1.2f, inner), m.Hull, true));
                Static(Prim(PrimitiveType.Cube, "Wall_" + n + "_High", t, new Vector3(x, 2.2f, 0f), Quaternion.identity, new Vector3(w, 0.8f, inner), m.Hull, true));
                Static(Prim(PrimitiveType.Cube, "Wall_" + n + "_Front", t, new Vector3(x, 1.5f, 0.8f), Quaternion.identity, new Vector3(w, 0.6f, 0.8f), m.Hull, true));
                Static(Prim(PrimitiveType.Cube, "Wall_" + n + "_Rear", t, new Vector3(x, 1.5f, -0.8f), Quaternion.identity, new Vector3(w, 0.6f, 0.8f), m.Hull, true));
                Static(Prim(PrimitiveType.Cube, "Window_" + n, t, new Vector3(x, 1.5f, 0f), Quaternion.identity, new Vector3(0.02f, 0.6f, 0.8f), m.Glass, false));
            }

            // Façade avant avec ouverture de porte (1,2 m x 2,2 m).
            Static(Prim(PrimitiveType.Cube, "Front_L", t, new Vector3(-0.95f, h / 2f, half), Quaternion.identity, new Vector3(0.7f, h, w), m.Hull, true));
            Static(Prim(PrimitiveType.Cube, "Front_R", t, new Vector3(0.95f, h / 2f, half), Quaternion.identity, new Vector3(0.7f, h, w), m.Hull, true));
            Static(Prim(PrimitiveType.Cube, "Front_Top", t, new Vector3(0f, 2.4f, half), Quaternion.identity, new Vector3(1.2f, 0.4f, w), m.Hull, true));
            GameObject leftDoor = Prim(PrimitiveType.Cube, "Door_L", t, new Vector3(-0.3f, 1.1f, half - 0.08f), Quaternion.identity, new Vector3(0.62f, 2.2f, 0.05f), m.Panel, true);
            GameObject rightDoor = Prim(PrimitiveType.Cube, "Door_R", t, new Vector3(0.3f, 1.1f, half - 0.08f), Quaternion.identity, new Vector3(0.62f, 2.2f, 0.05f), m.Panel, true);

            // Repère de calibration au sol + bandeau lumineux.
            Static(Prim(PrimitiveType.Cylinder, "CalibrationMark", t, new Vector3(0f, 0.025f, 0f), Quaternion.identity, new Vector3(0.5f, 0.003f, 0.5f), m.EmitCyan, false));
            Static(Prim(PrimitiveType.Cube, "LightStrip", t, new Vector3(0f, h - 0.02f, 0f), Quaternion.identity, new Vector3(1.6f, 0.03f, 0.12f), m.EmitCyan, false));

            var lightGo = new GameObject("CabinLight");
            lightGo.transform.SetParent(t, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 4f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;

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

            var rumble = root.AddComponent<AudioSource>();
            rumble.playOnAwake = false;
            rumble.loop = true;
            rumble.spatialBlend = 0f;

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

        static ParticleSystem Bubbles(Transform parent, Vector3 localPosition)
        {
            var go = new GameObject("Bubbles");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
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
            public Opening(float along, float width, float bottom, float top) { Along = along; Width = width; Bottom = bottom; Top = top; }
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

        static void BuildModuleShell(Mats m, Transform parent, Module mod, Shell shell, Color lightColor, float lightIntensity, Material strip)
        {
            Transform t = new GameObject("Module_Q" + mod.Q).transform;
            t.SetParent(parent, false);
            Rect r = PlayArea.GetRect(mod.Q, PlayArea.WallInset);
            float half = WallThickness / 2f;

            Static(Prim(PrimitiveType.Cube, "Floor", t, new Vector3(r.center.x, -0.05f, r.center.y), Quaternion.identity, new Vector3(r.width + 2 * WallThickness, 0.1f, r.height + 2 * WallThickness), m.Floor, true));
            Static(Prim(PrimitiveType.Cube, "Ceiling", t, new Vector3(r.center.x, WallHeight + 0.05f, r.center.y), Quaternion.identity, new Vector3(r.width + 2 * WallThickness, 0.1f, r.height + 2 * WallThickness), m.Hull, true));

            bool open = shell == Shell.OpenHall;

            // Mur commun avec le partenaire (axe Z).
            float partnerX = mod.Sx > 0 ? r.xMax + half : r.xMin - half;
            float backX = mod.Sx > 0 ? r.xMin - half : r.xMax + half;
            List<Opening> partnerOpenings = open ? null : new List<Opening>
            {
                new Opening(mod.Center.z + 1.5f - r.yMin, 2.0f, 1.1f, 2.1f),
                new Opening(mod.Center.z - 1.5f - r.yMin, 0.5f, 0.9f, 1.4f),
            };
            Wall(m, t, "Wall_Partner", new Vector3(partnerX, 0f, r.yMin), new Vector3(partnerX, 0f, r.yMax), partnerOpenings, true, open);
            Wall(m, t, "Wall_Back", new Vector3(backX, 0f, r.yMin), new Vector3(backX, 0f, r.yMax), null, false, false);

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
            Wall(m, t, "Wall_Neighbor", new Vector3(r.xMin, 0f, sharedZ), new Vector3(r.xMax, 0f, sharedZ), neighborOpenings, true, open);
            Wall(m, t, "Wall_Outer", new Vector3(r.xMin, 0f, outerZ), new Vector3(r.xMax, 0f, outerZ), null, false, false);

            // Bandeaux lumineux au plafond + deux lumières ponctuelles.
            foreach (float x in new[] { -4f, 4f })
            {
                Static(Prim(PrimitiveType.Cube, "CeilingStrip", t, mod.P(x, WallHeight - 0.03f, 0f), Quaternion.identity, new Vector3(4f, 0.04f, 0.15f), strip, false));
                var lightGo = new GameObject("Light");
                lightGo.transform.SetParent(t, false);
                lightGo.transform.position = mod.P(x, WallHeight - 0.4f, 0f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = lightColor;
                light.range = 7f;
                light.intensity = lightIntensity;
                light.shadows = LightShadows.None;
            }
        }

        static void Wall(Mats m, Transform parent, string name, Vector3 a, Vector3 b, List<Opening> openings, bool glass, bool fullGlass)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            Vector3 dir = (b - a).normalized;
            float length = Vector3.Distance(a, b);
            Quaternion rot = Quaternion.FromToRotation(Vector3.right, dir);

            void Piece(string n, float from, float to, float bottom, float top, Material mat, float thickness, bool collider)
            {
                if (to - from < 0.01f || top - bottom < 0.01f) return;
                Vector3 center = a + dir * ((from + to) / 2f) + Vector3.up * ((bottom + top) / 2f);
                Static(Prim(PrimitiveType.Cube, n, t, center, rot, new Vector3(to - from, top - bottom, thickness), mat, collider));
            }

            if (fullGlass)
            {
                // Paroi vitrée toute hauteur : montants tous les 2,5 m, liseré lumineux au sol.
                Piece("Glass", 0f, length, 0.05f, WallHeight - 0.15f, m.Glass, 0.03f, true);
                Piece("Top", 0f, length, WallHeight - 0.15f, WallHeight, m.Hull, WallThickness, true);
                Piece("Sill", 0f, length, 0f, 0.05f, m.EmitCyan, WallThickness, true);
                for (float x = 0f; x <= length + 0.01f; x += length / Mathf.Max(1, Mathf.Round(length / 2.5f)))
                    Piece("Mullion", Mathf.Max(0f, x - 0.05f), Mathf.Min(length, x + 0.05f), 0.05f, WallHeight - 0.15f, m.Hull, WallThickness, true);
                return;
            }

            float cursor = 0f;
            if (openings != null)
            {
                openings.Sort((x, y) => x.Along.CompareTo(y.Along));
                foreach (Opening o in openings)
                {
                    float start = o.Along - o.Width / 2f, end = o.Along + o.Width / 2f;
                    Piece("Segment", cursor, start, 0f, WallHeight, m.Hull, WallThickness, true);
                    Piece("Below", start, end, 0f, o.Bottom, m.Hull, WallThickness, true);
                    Piece("Above", start, end, o.Top, WallHeight, m.Hull, WallThickness, true);
                    if (glass && o.Width >= 1f) Piece("Glass", start, end, o.Bottom, o.Top, m.Glass, 0.03f, true);
                    if (o.Width >= 1f)
                    {
                        // Cadre lumineux du hublot.
                        Piece("Frame", start - 0.05f, end + 0.05f, o.Top, o.Top + 0.05f, m.EmitCyan, WallThickness + 0.02f, false);
                        Piece("Frame", start - 0.05f, end + 0.05f, o.Bottom - 0.05f, o.Bottom, m.EmitCyan, WallThickness + 0.02f, false);
                    }
                    cursor = end;
                }
            }
            Piece("Segment", cursor, length, 0f, WallHeight, m.Hull, WallThickness, true);
        }

        static Module[] BuildModules(Mats m, Transform root, Shell shell, Color lightColor, float lightIntensity, Material strip)
        {
            var modules = new Module[4];
            for (int q = 0; q < 4; q++)
            {
                modules[q] = new Module(q);
                BuildModuleShell(m, root, modules[q], shell, lightColor, lightIntensity, strip);
            }
            return modules;
        }

        // ---------- Étage 1 : « Rétablir le courant »

        static void BuildFloor1(Mats m)
        {
            Scene scene = NewFloorScene(1, out Transform root);
            var ambience = root.gameObject.AddComponent<FloorAmbience>();
            SetFields(ambience, ("fogColor", new Color(0.02f, 0.09f, 0.11f)), ("fogDensity", 0.05f), ("ambientColor", new Color(0.06f, 0.12f, 0.14f)));

            Module[] modules = BuildModules(m, root, Shell.Standard, new Color(0.6f, 0.9f, 1f), 1.3f, m.EmitCyan);

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
                    "CODE DU MODULE " + QuadrantNames[mod.Partner], true, false, "CodeDisplay_For_Q" + mod.Partner);
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
                Static(Prim(PrimitiveType.Cube, "Crate", root, mod.P(-1.5f, 0.4f, mod.Vz * 3.35f), Quaternion.identity, new Vector3(0.6f, 0.8f, 0.5f), m.Crate, true));
                BuildFallback(root, mod, monitor, fuses[q], fuseTarget);
            }

            BuildSyncLevers(root, 1, "Remise sous tension", levers, prerequisites.ToArray());
            BuildHatch(m, root, modules[0], PartnerHatchSpot(modules[0]), modules[1], PartnerHatchSpot(modules[1]));
            BuildHatch(m, root, modules[2], PartnerHatchSpot(modules[2]), modules[3], PartnerHatchSpot(modules[3]));

            SaveScene(scene, FloorScenes[0]);
        }

        // ---------- Étage 2 : « Équilibrer la pression »

        static void BuildFloor2(Mats m)
        {
            Scene scene = NewFloorScene(2, out Transform root);
            var ambience = root.gameObject.AddComponent<FloorAmbience>();
            SetFields(ambience, ("fogColor", new Color(0.07f, 0.015f, 0.01f)), ("fogDensity", 0.07f), ("ambientColor", new Color(0.09f, 0.025f, 0.02f)));

            Module[] modules = BuildModules(m, root, Shell.VerticalHatches, new Color(1f, 0.3f, 0.12f), 0.9f, m.EmitRed);

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
                    "CODE DU MODULE " + QuadrantNames[mod.Partner] + "  (encre UV)", true, true, "UVCode_For_Q" + mod.Partner);
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

            SaveScene(scene, FloorScenes[1]);
        }

        // ---------- Étage 3 : « Remontée d'urgence » (salle commune)

        static void BuildFloor3(Mats m)
        {
            Scene scene = NewFloorScene(3, out Transform root);
            var ambience = root.gameObject.AddComponent<FloorAmbience>();
            SetFields(ambience, ("fogColor", new Color(0.005f, 0.015f, 0.03f)), ("fogDensity", 0.06f), ("ambientColor", new Color(0.02f, 0.04f, 0.07f)));

            Module[] modules = BuildModules(m, root, Shell.OpenHall, new Color(0.25f, 0.85f, 1f), 0.7f, m.EmitCyan);
            Vector3 center = new Vector3(PlayArea.ArenaWidth / 2f, 0f, PlayArea.ArenaDepth / 2f);

            // Fissures lumineuses dans les allées entre les modules (bioluminescence).
            Static(Prim(PrimitiveType.Cube, "Rift_X", root, new Vector3(center.x, 0.01f, center.z), Quaternion.identity, new Vector3(PlayArea.ArenaWidth, 0.02f, 0.12f), m.EmitPurple, false));
            Static(Prim(PrimitiveType.Cube, "Rift_Z", root, new Vector3(center.x, 0.01f, center.z), Quaternion.identity, new Vector3(0.18f, 0.02f, PlayArea.ArenaDepth), m.EmitPurple, false));

            // Le réacteur, au point de jonction des 4 modules : chacun en voit et en touche un quart.
            Transform reactor = new GameObject("ReactorVisual").transform;
            reactor.SetParent(root, false);
            Static(Prim(PrimitiveType.Cylinder, "Base", reactor, center + Vector3.up * 0.03f, Quaternion.identity, new Vector3(1.3f, 0.03f, 1.3f), m.Hull, false));
            Static(Prim(PrimitiveType.Cylinder, "Shell", reactor, center + Vector3.up * 1.6f, Quaternion.identity, new Vector3(0.9f, 1.6f, 0.9f), m.Glass, false));
            GameObject core = Prim(PrimitiveType.Cylinder, "Core", reactor, center + Vector3.up * 1.5f, Quaternion.identity, new Vector3(0.38f, 1.35f, 0.38f), m.EmitCyan, false);

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
                Label(console, "APPUIE QUAND TA COULEUR S'ALLUME", position + Vector3.up * 1.32f - toCenter * 0.02f, 0.45f, facingReactor);
                levers[q] = BuildLever(m, console, q, position + inward * 0.55f, Quaternion.LookRotation(-toCenter));
            }

            var reactorGo = new GameObject("Reactor");
            reactorGo.transform.SetParent(root, false);
            reactorGo.AddComponent<NetworkObject>();
            PuzzleObjective sequenceObjective = Objective(reactorGo, 3, -1, "Séquence du réacteur");
            var sequence = reactorGo.AddComponent<ReactorSequence>();
            var tone = reactorGo.AddComponent<AudioSource>();
            tone.playOnAwake = false;
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
            rso.ApplyModifiedPropertiesWithoutUndo();

            BuildSyncLevers(root, 3, "Remontée d'urgence", levers, new[] { sequenceObjective });

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

            var finale = root.gameObject.AddComponent<FloorFinale>();
            var fso = new SerializedObject(finale);
            fso.FindProperty("sunbeam").objectReferenceValue = beam;
            SetArray(fso.FindProperty("bubbles"), bubbles.ToArray());
            fso.ApplyModifiedPropertiesWithoutUndo();

            SaveScene(scene, FloorScenes[2]);
        }

        // ---------- Éléments d'énigme

        static CodeKeypad BuildKeypad(Mats m, Transform parent, Module mod, int floor)
        {
            // Face avant vers -Z local (sens d'appui par défaut du poke), donc +Z vers le mur.
            var root = new GameObject("Keypad_Q" + mod.Q);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(mod.P(2.5f, 1.3f, mod.Vz * 3.66f), mod.Facing(0f, mod.Vz));
            root.AddComponent<NetworkObject>();
            PuzzleObjective objective = Objective(root, floor, mod.Q, "Digicode " + QuadrantNames[mod.Q]);
            var keypad = root.AddComponent<CodeKeypad>();
            Transform t = root.transform;

            Prim(PrimitiveType.Cube, "Panel", t, new Vector3(0f, 0f, 0.02f), Quaternion.identity, new Vector3(0.5f, 0.6f, 0.04f), m.Panel, false);
            var slots = new Renderer[CodeKeypad.CodeLength];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = Prim(PrimitiveType.Cube, "Slot" + i, t, new Vector3(-0.15f + 0.1f * i, 0.18f, -0.005f), Quaternion.identity, new Vector3(0.08f, 0.08f, 0.01f), m.SlotEmpty, false).GetComponent<Renderer>();
            Renderer status = Prim(PrimitiveType.Sphere, "Status", t, new Vector3(0.2f, 0.25f, -0.01f), Quaternion.identity, Vector3.one * 0.05f, m.StatusIdle, false).GetComponent<Renderer>();

            for (int s = 0; s < CodeKeypad.SymbolCount; s++)
            {
                float x = -0.13f + 0.13f * (s % 3);
                float y = -0.04f - 0.13f * (s / 3);
                GameObject button = Prim(PrimitiveType.Cube, "Key" + s, t, new Vector3(x, y, -0.01f), Quaternion.identity, new Vector3(0.1f, 0.1f, 0.04f), m.Symbols[s], true);
                button.AddComponent<XRSimpleInteractable>();
                button.AddComponent<XRPokeFilter>();
                var key = button.AddComponent<CodeKeypadButton>();
                SetFields(key, ("keypad", keypad), ("symbol", s), ("symbolRenderer", button.GetComponent<Renderer>()));
            }

            Label(t, "DIGICODE", new Vector3(0f, 0.36f, -0.01f), 0.6f);

            var so = new SerializedObject(keypad);
            so.FindProperty("objective").objectReferenceValue = objective;
            SetArray(so.FindProperty("entrySlots"), slots);
            SetArray(so.FindProperty("symbolMaterials"), m.Symbols);
            so.FindProperty("emptySlotMaterial").objectReferenceValue = m.SlotEmpty;
            so.FindProperty("statusLight").objectReferenceValue = status;
            so.FindProperty("idleMaterial").objectReferenceValue = m.StatusIdle;
            so.FindProperty("successMaterial").objectReferenceValue = m.StatusOk;
            so.FindProperty("errorMaterial").objectReferenceValue = m.StatusError;
            so.ApplyModifiedPropertiesWithoutUndo();
            return keypad;
        }

        static GameObject BuildCodeDisplay(Mats m, Transform parent, Vector3 position, Quaternion rotation, CodeKeypad keypad, string title, bool large, bool uvInk, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);
            Transform t = root.transform;
            float size = large ? 0.22f : 0.12f;
            float step = size * 1.3f;

            Static(Prim(PrimitiveType.Cube, "Backing", t, new Vector3(0f, 0f, 0.02f), Quaternion.identity, new Vector3(step * 4f + 0.1f, size + 0.2f, 0.04f), m.Panel, false));
            var slots = new Renderer[CodeKeypad.CodeLength];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = Prim(PrimitiveType.Cube, "Symbol" + i, t, new Vector3((i - 1.5f) * step, -0.02f, -0.005f), Quaternion.identity, new Vector3(size, size, 0.01f), m.SlotEmpty, false).GetComponent<Renderer>();
            Label(t, title, new Vector3(0f, size / 2f + 0.06f, -0.01f), large ? 0.9f : 0.5f);

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
                "SURVEILLANCE — MODULE " + QuadrantNames[mod.Partner] + " VIDE", false, false, "FallbackMonitor_Q" + mod.Q);
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
            PuzzleObjective objective = Objective(root, 1, mod.Q, "Fusible " + QuadrantNames[mod.Q]);
            var socketBehaviour = root.AddComponent<FuseSocket>();
            Transform t = root.transform;

            Prim(PrimitiveType.Cube, "Panel", t, new Vector3(0f, 0f, 0.02f), Quaternion.identity, new Vector3(0.4f, 0.5f, 0.04f), m.Panel, false);
            Prim(PrimitiveType.Cylinder, "ColorRing", t, new Vector3(0f, 0f, -0.005f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.18f, 0.005f, 0.18f), m.Symbols[mod.Q], false);
            Label(t, "FUSIBLE " + SymbolNames[mod.Q], new Vector3(0f, 0.3f, -0.01f), 0.55f);

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

            SetFields(socketBehaviour, ("socket", socket), ("objective", objective), ("installedVisual", installed));
            return objective;
        }

        static NetworkTransform BuildFuse(Mats m, Transform parent, Module mod, int fuseId)
        {
            // Étagère contre le long mur partagé, le fusible destiné au partenaire posé dessus.
            float z = -mod.Vz * 3.48f;
            Static(Prim(PrimitiveType.Cube, "Shelf_Q" + mod.Q, parent, mod.P(-5.5f, 0.85f, z), Quaternion.identity, new Vector3(1.0f, 0.05f, 0.4f), m.Panel, true));
            GameObject fuse = Prim(PrimitiveType.Cylinder, "Fuse_" + SymbolNames[fuseId], parent, mod.P(-5.5f, 0.97f, z), Quaternion.identity, new Vector3(0.06f, 0.08f, 0.06f), m.Symbols[fuseId], true);
            NetworkTransform networkTransform = MakeNetworkGrabbable(fuse, 0.3f);
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
            Prim(PrimitiveType.Cylinder, "Body", lamp.transform, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new Vector3(0.045f, 0.11f, 0.045f), m.Hull, false);
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
            SetFields(lamp.AddComponent<UVLamp>(), ("beam", beam));
        }

        static PressureGauge BuildGauge(Mats m, Transform parent, Module mod, int floor)
        {
            var root = new GameObject("Gauge_Q" + mod.Q);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(mod.P(-3f, 1.5f, mod.Vz * 3.66f), mod.Facing(0f, mod.Vz));
            root.AddComponent<NetworkObject>();
            PuzzleObjective objective = Objective(root, floor, mod.Q, "Manomètre " + QuadrantNames[mod.Q]);
            var gauge = root.AddComponent<PressureGauge>();
            Transform t = root.transform;

            Prim(PrimitiveType.Cylinder, "Dial", t, new Vector3(0f, 0f, 0.015f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.52f, 0.015f, 0.52f), m.Panel, false);
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
            Label(t, "MANOMÈTRE " + QuadrantNames[mod.Q], new Vector3(0f, 0.34f, -0.01f), 0.6f);
            Label(t, "Une autre équipe contrôle ta pression", new Vector3(0f, -0.32f, -0.01f), 0.4f);

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
            Prim(PrimitiveType.Cylinder, "Rim", wheel, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new Vector3(0.4f, 0.015f, 0.4f), m.EmitRed, false);
            Prim(PrimitiveType.Cube, "SpokeA", wheel, new Vector3(0f, 0f, 0.02f), Quaternion.identity, new Vector3(0.38f, 0.035f, 0.03f), m.Hull, false);
            Prim(PrimitiveType.Cube, "SpokeB", wheel, new Vector3(0f, 0f, 0.02f), Quaternion.identity, new Vector3(0.035f, 0.38f, 0.03f), m.Hull, false);
            Prim(PrimitiveType.Sphere, "Handle", wheel, new Vector3(0.17f, 0f, 0.04f), Quaternion.identity, Vector3.one * 0.06f, m.Avatar[mod.Q], false);

            var collider = root.AddComponent<SphereCollider>();
            collider.radius = 0.22f;
            root.AddComponent<XRSimpleInteractable>();
            var valve = root.AddComponent<ValveWheel>();
            SetFields(valve, ("wheel", wheel));
            Label(parent, "VANNE " + QuadrantNames[mod.Q], mod.P(5.4f, 1.6f, 2.6f), 0.55f, mod.Facing(0f, 1f));
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
            Static(Prim(PrimitiveType.Cube, "Stripe", t, new Vector3(0f, 0.85f, 0.151f), Quaternion.identity, new Vector3(0.25f, 0.05f, 0.005f), m.Avatar[quadrant], false));
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(t, false);
            pivot.localPosition = new Vector3(0f, 1.0f, 0f);
            Prim(PrimitiveType.Cylinder, "Arm", pivot, new Vector3(0f, 0.22f, 0f), Quaternion.identity, new Vector3(0.04f, 0.22f, 0.04f), m.Hull, false);
            GameObject knob = Prim(PrimitiveType.Sphere, "Knob", pivot, new Vector3(0f, 0.45f, 0f), Quaternion.identity, Vector3.one * 0.1f, m.EmitRed, true);
            knob.AddComponent<XRSimpleInteractable>();
            var lever = knob.AddComponent<LeverHandle>();
            SetFields(lever, ("pivot", pivot));
            return lever;
        }

        static void BuildSyncLevers(Transform parent, int floor, string label, LeverHandle[] levers, PuzzleObjective[] prerequisites)
        {
            var root = new GameObject("SyncLevers");
            root.transform.SetParent(parent, false);
            root.AddComponent<NetworkObject>();
            PuzzleObjective objective = Objective(root, floor, -1, label);
            var group = root.AddComponent<SyncLeverGroup>();
            var fail = root.AddComponent<AudioSource>();
            fail.playOnAwake = false;
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
            var interactable = button.AddComponent<XRSimpleInteractable>();
            button.AddComponent<XRPokeFilter>();
            Label(parent, "ENVOYER", spot.Label, 0.5f, spot.ButtonFacing);

            endpoint.FindPropertyRelative("quadrant").intValue = q;
            endpoint.FindPropertyRelative("zone").objectReferenceValue = zone;
            endpoint.FindPropertyRelative("arrivalPoint").objectReferenceValue = arrival;
            endpoint.FindPropertyRelative("sendButton").objectReferenceValue = interactable;
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

            var gameState = new GameObject("GameState");
            gameState.AddComponent<NetworkObject>();
            gameState.AddComponent<GameStateManager>();

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

            var localRig = rig.AddComponent<LocalRig>();
            SetFields(localRig, ("leftHand", Find(rig.transform, "Left Controller")), ("rightHand", Find(rig.transform, "Right Controller")));

            var gate = rig.AddComponent<LocomotionGate>();
            var gateSo = new SerializedObject(gate);
            SetArray(gateSo.FindProperty("debugOnly"), Find(rig.transform, "Move").gameObject, Find(rig.transform, "Gravity").gameObject);
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
            var guard = rig.AddComponent<ZoneGuard>();
            SetFields(guard, ("warningOverlay", overlayRenderer), ("alarm", alarm));

            BuildMenu(rig.transform, camera);

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
            ((RectTransform)canvasGo.transform).sizeDelta = new Vector2(820f, 900f);

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

            RectTransform offline = Group(panel, "Offline");
            Button joinOnline = UIButton(offline, "Rejoindre l'équipe (en ligne)");
            Button hostLan = UIButton(offline, "Héberger (réseau local)");
            Button joinLan = UIButton(offline, "Rejoindre (réseau local)");
            Button solo = UIButton(offline, "Jouer en solo");

            RectTransform online = Group(panel, "Online");
            Button start = UIButton(online, "Lancer la plongée");
            Button recenter = UIButton(online, "Recalibrer ma position");
            Button leave = UIButton(online, "Quitter la partie");

            var menu = canvasGo.AddComponent<MainMenu>();
            SetFields(menu,
                ("panel", panel.gameObject), ("statusText", status), ("playersText", players),
                ("offlineGroup", offline.gameObject), ("joinOnlineButton", joinOnline), ("hostLanButton", hostLan),
                ("joinLanButton", joinLan), ("soloButton", solo), ("onlineGroup", online.gameObject),
                ("startButton", start), ("recenterButton", recenter), ("leaveButton", leave));
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

        static Button UIButton(Transform parent, string text)
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
            return button;
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

        static PuzzleObjective Objective(GameObject go, int floor, int quadrant, string label)
        {
            var objective = go.AddComponent<PuzzleObjective>();
            SetFields(objective, ("floor", floor), ("quadrant", quadrant), ("label", label));
            return objective;
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

        static void Label(Transform parent, string text, Vector3 localPosition, float size, Quaternion? rotation = null)
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
