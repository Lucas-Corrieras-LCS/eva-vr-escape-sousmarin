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
        static readonly string[] QuadrantNames = { "ORANGE", "CYAN", "JAUNE", "MAGENTA" };
        static readonly Color[] QuadrantColors =
        {
            new Color(1f, 0.45f, 0.1f), new Color(0.1f, 0.8f, 1f), new Color(1f, 0.85f, 0.1f), new Color(1f, 0.2f, 0.7f),
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
            BuildPlaceholderFloor(m, 2, new Color(0.9f, 0.25f, 0.1f), new Color(0.06f, 0.015f, 0.01f), 0.08f, "LABORATOIRES — à construire");
            BuildPlaceholderFloor(m, 3, new Color(0.2f, 0.9f, 1f), new Color(0.005f, 0.01f, 0.02f), 0.1f, "CŒUR DE LA STATION — à construire");
            BuildStation(m, player, cabin);
            AssetDatabase.SaveAssets();
            Debug.Log("[Abysses] Construction terminée.");
        }

        // ------------------------------------------------------------------ Matériaux

        class Mats
        {
            public Material Hull, Panel, Floor, Glass, EmitCyan, EmitWarm, EmitRed, Visor, SlotEmpty, StatusIdle, StatusOk, StatusError, Warning, Crate;
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
                Visor = Lit("M_Visor", new Color(0.02f, 0.03f, 0.05f), 0.9f, 0.95f),
                SlotEmpty = Unlit("M_SlotEmpty", new Color(0.05f, 0.06f, 0.07f)),
                StatusIdle = Lit("M_Status_Idle", Color.black, 0f, 0.5f, new Color(1f, 0.6f, 0.1f) * 2f),
                StatusOk = Lit("M_Status_Ok", Color.black, 0f, 0.5f, new Color(0.2f, 1f, 0.35f) * 2.5f),
                StatusError = Lit("M_Status_Error", Color.black, 0f, 0.5f, new Color(1f, 0.08f, 0.05f) * 2.5f),
                Warning = Transparent("M_ZoneWarning", new Color(0.8f, 0.05f, 0.02f, 0f), true),
            };
            for (int i = 0; i < 4; i++)
                m.Avatar[i] = Lit("M_Avatar_Q" + i, QuadrantColors[i] * 0.6f, 0.3f, 0.6f, QuadrantColors[i] * 1.5f);
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

        static void BuildModuleShell(Mats m, Transform parent, Module mod, Color lightColor)
        {
            Transform t = new GameObject("Module_Q" + mod.Q).transform;
            t.SetParent(parent, false);
            Rect r = PlayArea.GetRect(mod.Q, PlayArea.WallInset);
            float half = WallThickness / 2f;

            Static(Prim(PrimitiveType.Cube, "Floor", t, new Vector3(r.center.x, -0.05f, r.center.y), Quaternion.identity, new Vector3(r.width + 2 * WallThickness, 0.1f, r.height + 2 * WallThickness), m.Floor, true));
            Static(Prim(PrimitiveType.Cube, "Ceiling", t, new Vector3(r.center.x, WallHeight + 0.05f, r.center.y), Quaternion.identity, new Vector3(r.width + 2 * WallThickness, 0.1f, r.height + 2 * WallThickness), m.Hull, true));

            // Mur commun avec le partenaire (axe Z) : grand hublot + ouverture du sas.
            float partnerX = mod.Sx > 0 ? r.xMax + half : r.xMin - half;
            float backX = mod.Sx > 0 ? r.xMin - half : r.xMax + half;
            var sharedOpenings = new List<Opening>
            {
                new Opening(mod.Center.z + 1.5f - r.yMin, 2.0f, 1.1f, 2.1f),
                new Opening(mod.Center.z - 1.5f - r.yMin, 0.5f, 0.9f, 1.4f),
            };
            Wall(m, t, "Wall_Partner", new Vector3(partnerX, 0f, r.yMin), new Vector3(partnerX, 0f, r.yMax), sharedOpenings, true);
            Wall(m, t, "Wall_Back", new Vector3(backX, 0f, r.yMin), new Vector3(backX, 0f, r.yMax), null, false);

            // Long mur partagé avec le module voisin du dessus/dessous (axe X) : hublot.
            float sharedZ = mod.Vz < 0 ? r.yMax + half : r.yMin - half;
            float outerZ = mod.Vz < 0 ? r.yMin - half : r.yMax + half;
            var verticalOpenings = new List<Opening> { new Opening(mod.Center.x - mod.Sx * 3f - r.xMin, 2.0f, 1.1f, 2.1f) };
            Wall(m, t, "Wall_Neighbor", new Vector3(r.xMin, 0f, sharedZ), new Vector3(r.xMax, 0f, sharedZ), verticalOpenings, true);
            Wall(m, t, "Wall_Outer", new Vector3(r.xMin, 0f, outerZ), new Vector3(r.xMax, 0f, outerZ), null, false);

            // Bandeaux lumineux au plafond + deux lumières ponctuelles.
            foreach (float x in new[] { -4f, 4f })
            {
                Static(Prim(PrimitiveType.Cube, "CeilingStrip", t, mod.P(x, WallHeight - 0.03f, 0f), Quaternion.identity, new Vector3(4f, 0.04f, 0.15f), m.EmitCyan, false));
                var lightGo = new GameObject("Light");
                lightGo.transform.SetParent(t, false);
                lightGo.transform.position = mod.P(x, WallHeight - 0.4f, 0f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = lightColor;
                light.range = 7f;
                light.intensity = 1.3f;
                light.shadows = LightShadows.None;
            }
        }

        static void Wall(Mats m, Transform parent, string name, Vector3 a, Vector3 b, List<Opening> openings, bool glass)
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

        static void BuildFloor1(Mats m)
        {
            Scene scene = NewFloorScene(1, out Transform root);
            var ambience = root.gameObject.AddComponent<FloorAmbience>();
            SetFields(ambience, ("fogColor", new Color(0.02f, 0.09f, 0.11f)), ("fogDensity", 0.05f), ("ambientColor", new Color(0.06f, 0.12f, 0.14f)));

            var modules = new Module[4];
            for (int q = 0; q < 4; q++)
            {
                modules[q] = new Module(q);
                BuildModuleShell(m, root, modules[q], new Color(0.6f, 0.9f, 1f));
            }

            var keypads = new CodeKeypad[4];
            var fuses = new NetworkTransform[4];
            var levers = new LeverHandle[4];

            for (int q = 0; q < 4; q++) keypads[q] = BuildKeypad(m, root, modules[q]);
            for (int q = 0; q < 4; q++)
            {
                Module mod = modules[q];
                BuildCodeDisplay(m, root, mod.P(-7.16f, 1.6f, 0f), mod.Facing(-1f, 0f), keypads[mod.Partner],
                    "CODE DU MODULE " + QuadrantNames[mod.Partner], true, "CodeDisplay_For_Q" + mod.Partner);
                BuildFuseSocket(m, root, mod);
                fuses[mod.Partner] = BuildFuse(m, root, mod, mod.Partner);
                levers[q] = BuildLever(m, root, mod);
            }

            // Repli : module partenaire vide → écran de surveillance avec son propre code, et fusible rapatrié.
            for (int q = 0; q < 4; q++)
            {
                Module mod = modules[q];
                GameObject monitor = BuildCodeDisplay(m, root, mod.P(0.6f, 1.6f, mod.Vz * 3.66f), mod.Facing(0f, mod.Vz), keypads[q],
                    "SURVEILLANCE — MODULE " + QuadrantNames[mod.Partner] + " VIDE", false, "FallbackMonitor_Q" + q);
                monitor.SetActive(false);
                Transform fuseTarget = new GameObject("FallbackFusePoint_Q" + q).transform;
                fuseTarget.SetParent(root, false);
                fuseTarget.position = mod.P(-1.5f, 0.95f, mod.Vz * 3.35f);
                Static(Prim(PrimitiveType.Cube, "Crate", root, mod.P(-1.5f, 0.4f, mod.Vz * 3.35f), Quaternion.identity, new Vector3(0.6f, 0.8f, 0.5f), m.Crate, true));

                var fallback = new GameObject("Fallback_Q" + q).AddComponent<QuadrantFallback>();
                fallback.transform.SetParent(root, false);
                var so = new SerializedObject(fallback);
                so.FindProperty("sourceQuadrant").intValue = mod.Partner;
                SetArray(so.FindProperty("showWhenAbsent"), monitor);
                SetArray(so.FindProperty("relocate"), fuses[q]);
                SetArray(so.FindProperty("relocateTargets"), fuseTarget);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            BuildSyncLevers(root, levers);
            BuildHatch(m, root, modules[0], modules[1]);
            BuildHatch(m, root, modules[2], modules[3]);

            SaveScene(scene, FloorScenes[0]);
        }

        static CodeKeypad BuildKeypad(Mats m, Transform parent, Module mod)
        {
            // Face avant vers -Z local (sens d'appui par défaut du poke), donc +Z vers le mur.
            var root = new GameObject("Keypad_Q" + mod.Q);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(mod.P(2.5f, 1.3f, mod.Vz * 3.66f), mod.Facing(0f, mod.Vz));
            root.AddComponent<NetworkObject>();
            PuzzleObjective objective = Objective(root, 1, mod.Q, "Digicode " + QuadrantNames[mod.Q]);
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

        static GameObject BuildCodeDisplay(Mats m, Transform parent, Vector3 position, Quaternion rotation, CodeKeypad keypad, string title, bool large, string name)
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
            return root;
        }

        static void BuildFuseSocket(Mats m, Transform parent, Module mod)
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
        }

        static NetworkTransform BuildFuse(Mats m, Transform parent, Module mod, int fuseId)
        {
            // Étagère contre le long mur partagé, le fusible destiné au partenaire posé dessus.
            float z = -mod.Vz * 3.48f;
            Static(Prim(PrimitiveType.Cube, "Shelf_Q" + mod.Q, parent, mod.P(-5.5f, 0.85f, z), Quaternion.identity, new Vector3(1.0f, 0.05f, 0.4f), m.Panel, true));

            GameObject fuse = Prim(PrimitiveType.Cylinder, "Fuse_" + SymbolNames[fuseId], parent, mod.P(-5.5f, 0.97f, z), Quaternion.identity, new Vector3(0.06f, 0.08f, 0.06f), m.Symbols[fuseId], true);
            fuse.AddComponent<NetworkObject>();
            var body = fuse.AddComponent<Rigidbody>();
            body.mass = 0.3f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            fuse.AddComponent<XRGrabInteractable>();
            var networkTransform = fuse.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            networkTransform.SyncScaleX = networkTransform.SyncScaleY = networkTransform.SyncScaleZ = false;
            networkTransform.UseQuaternionCompression = true;
            fuse.AddComponent<NetworkGrabbable>();
            var item = fuse.AddComponent<FuseItem>();
            SetFields(item, ("fuseId", fuseId));
            return networkTransform;
        }

        static LeverHandle BuildLever(Mats m, Transform parent, Module mod)
        {
            // +Z local du levier vers l'intérieur du module (le joueur tire le levier vers lui).
            var root = new GameObject("Lever_Q" + mod.Q);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(mod.P(5.4f, 0f, 2.6f), mod.Facing(0f, -1f));
            Transform t = root.transform;

            Static(Prim(PrimitiveType.Cube, "Base", t, new Vector3(0f, 0.5f, 0f), Quaternion.identity, new Vector3(0.3f, 1.0f, 0.3f), m.Panel, true));
            Static(Prim(PrimitiveType.Cube, "Stripe", t, new Vector3(0f, 0.85f, 0.151f), Quaternion.identity, new Vector3(0.25f, 0.05f, 0.005f), m.Avatar[mod.Q], false));
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

        static void BuildSyncLevers(Transform parent, LeverHandle[] levers)
        {
            var root = new GameObject("SyncLevers");
            root.transform.SetParent(parent, false);
            root.AddComponent<NetworkObject>();
            PuzzleObjective objective = Objective(root, 1, -1, "Remise sous tension");
            var group = root.AddComponent<SyncLeverGroup>();
            var so = new SerializedObject(group);
            so.FindProperty("objective").objectReferenceValue = objective;
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

        static void BuildHatch(Mats m, Transform parent, Module a, Module b)
        {
            var root = new GameObject("Hatch_Q" + a.Q + "_Q" + b.Q);
            root.transform.SetParent(parent, false);
            root.AddComponent<NetworkObject>();
            var hatch = root.AddComponent<TransferHatch>();

            // Tube vitré traversant l'allée entre les deux modules.
            Vector3 middle = (a.P(7.5f, 1.15f, -1.5f) + b.P(7.5f, 1.15f, -1.5f)) / 2f;
            float gap = Mathf.Abs(a.P(7.2f, 0f, 0f).x - b.P(7.2f, 0f, 0f).x);
            Static(Prim(PrimitiveType.Cylinder, "Tube", root.transform, middle, Quaternion.Euler(0f, 0f, 90f), new Vector3(0.45f, gap / 2f, 0.45f), m.Glass, false));

            var so = new SerializedObject(hatch);
            FillEndpoint(m, root.transform, a, so.FindProperty("sideA"));
            FillEndpoint(m, root.transform, b, so.FindProperty("sideB"));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void FillEndpoint(Mats m, Transform parent, Module mod, SerializedProperty endpoint)
        {
            float x = 7.2f - 0.25f;
            Static(Prim(PrimitiveType.Cube, "Tray_Q" + mod.Q, parent, mod.P(x, 0.88f, -1.5f), Quaternion.identity, new Vector3(0.5f, 0.04f, 0.5f), m.Panel, true));

            var zone = new GameObject("Zone_Q" + mod.Q).AddComponent<BoxCollider>();
            zone.transform.SetParent(parent, false);
            zone.transform.position = mod.P(x, 1.1f, -1.5f);
            zone.size = new Vector3(0.45f, 0.4f, 0.45f);
            zone.isTrigger = true;

            var arrival = new GameObject("Arrival_Q" + mod.Q).transform;
            arrival.SetParent(parent, false);
            arrival.position = mod.P(x, 1.0f, -1.5f);

            // Bouton sur le mur, face avant vers -Z local (vers la pièce).
            GameObject button = Prim(PrimitiveType.Cube, "SendButton_Q" + mod.Q, parent, mod.P(7.17f, 1.35f, -2.1f), mod.Facing(1f, 0f), new Vector3(0.12f, 0.12f, 0.06f), m.EmitRed, true);
            var interactable = button.AddComponent<XRSimpleInteractable>();
            button.AddComponent<XRPokeFilter>();
            Label(parent, "ENVOYER", mod.P(7.15f, 1.52f, -2.1f), 0.5f, mod.Facing(1f, 0f));

            endpoint.FindPropertyRelative("quadrant").intValue = mod.Q;
            endpoint.FindPropertyRelative("zone").objectReferenceValue = zone;
            endpoint.FindPropertyRelative("arrivalPoint").objectReferenceValue = arrival;
            endpoint.FindPropertyRelative("sendButton").objectReferenceValue = interactable;
        }

        static void BuildPlaceholderFloor(Mats m, int floor, Color lightColor, Color fog, float density, string title)
        {
            Scene scene = NewFloorScene(floor, out Transform root);
            var ambience = root.gameObject.AddComponent<FloorAmbience>();
            SetFields(ambience, ("fogColor", fog), ("fogDensity", density), ("ambientColor", fog * 3f));
            for (int q = 0; q < 4; q++)
            {
                var mod = new Module(q);
                BuildModuleShell(m, root, mod, lightColor);
                Label(root, title, mod.P(-7.18f, 2.0f, 0f), 2f, mod.Facing(-1f, 0f));
            }
            SaveScene(scene, FloorScenes[floor - 1]);
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
