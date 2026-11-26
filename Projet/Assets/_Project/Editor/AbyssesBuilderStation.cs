using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Abysses.Interaction;
using Abysses.Level;
using Abysses.Network;
using Abysses.Player;
using Abysses.UI;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Générateur, partie « joueur et Station » : corps des avatars, tutoriel et traînées de descente de la cabine,
    /// montre de plongée, pupitre du maître du jeu.
    /// </summary>
    public static partial class AbyssesBuilder
    {
        /// <summary>
        /// Fusionne les pièces rigides d'un élément d'avatar (casque, buste, gant, botte) par matériau : ~3 fois moins
        /// d'appels de rendu par coéquipier. Les pièces à la couleur du joueur restent dans la liste des pièces teintées.
        /// </summary>
        static void CombineAvatarPart(Transform group, List<Renderer> tinted, string name)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var sources = new List<GameObject>();
            var tintedMaterials = new HashSet<Material>();
            Matrix4x4 toGroup = group.worldToLocalMatrix;
            foreach (MeshFilter filter in group.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || filter.sharedMesh == null) continue;
                Material material = renderer.sharedMaterial;
                if (tinted.Remove(renderer)) tintedMaterials.Add(material);
                if (!groups.TryGetValue(material, out List<CombineInstance> list)) groups[material] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = filter.sharedMesh, transform = toGroup * filter.transform.localToWorldMatrix });
                sources.Add(filter.gameObject);
            }
            foreach (GameObject source in sources) Object.DestroyImmediate(source);
            int index = 0;
            foreach (KeyValuePair<Material, List<CombineInstance>> entry in groups)
            {
                var mesh = new Mesh { name = name + "_" + index };
                mesh.CombineMeshes(entry.Value.ToArray(), true, true);
                mesh.RecalculateBounds();
                var go = new GameObject(name + "_" + entry.Key.name);
                go.transform.SetParent(group, false);
                go.AddComponent<MeshFilter>().sharedMesh = SurfaceMesh("Avatar_" + name + "_" + index, mesh);
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = entry.Key;
                if (tintedMaterials.Contains(entry.Key)) tinted.Add(renderer);
                index++;
            }
        }

        /// <summary>
        /// Casque de scaphandrier (repère de la tête : +Z = regard) : dôme à la couleur du joueur, hublot avant cerclé de
        /// laiton avec grille, hublots latéraux, collerette, lampe frontale.
        /// </summary>
        static void BuildDiverHelmet(Mats m, Transform head, List<GameObject> hidden, List<Renderer> tinted)
        {
            var helmet = new GameObject("Casque").transform;
            helmet.SetParent(head, false);
            hidden.Add(helmet.gameObject);
            GameObject dome = Prim(PrimitiveType.Sphere, "Dome", helmet, new Vector3(0f, 0.01f, -0.01f), Quaternion.identity, new Vector3(0.3f, 0.31f, 0.3f), m.AvatarPaint[0], false);
            tinted.Add(dome.GetComponent<Renderer>());
            // Visière éclairée de l'intérieur (direction artistique) : on devine le plongeur dans le noir.
            Prim(PrimitiveType.Sphere, "Hublot", helmet, new Vector3(0f, -0.005f, 0.122f), Quaternion.identity, new Vector3(0.19f, 0.16f, 0.07f), styleVisor != null ? styleVisor : m.Visor, false);
            AbyssesArt.MeshObject("Cerclage", helmet, art.Ring, new Vector3(0f, -0.005f, 0.14f), Quaternion.identity, new Vector3(0.21f, 0.18f, 0.025f), m.Brass);
            foreach (float side in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Sphere, "HublotLateral", helmet, new Vector3(side * 0.135f, 0f, 0.02f), Quaternion.identity, new Vector3(0.03f, 0.08f, 0.08f), m.Visor, false);
                AbyssesArt.MeshObject("CerclageLateral", helmet, art.Ring, new Vector3(side * 0.145f, 0f, 0.02f), Quaternion.Euler(0f, 90f, 0f), new Vector3(0.095f, 0.095f, 0.02f), m.Brass);
            }
            AbyssesArt.MeshObject("Collerette", helmet, art.Ring, new Vector3(0f, -0.14f, -0.01f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.31f, 0.31f, 0.05f), m.Brass);
            // Lampe frontale : boîtier sombre et verre lumineux, inclinée vers l'avant.
            Prim(PrimitiveType.Cylinder, "Lampe", helmet, new Vector3(0f, 0.145f, 0.06f), Quaternion.Euler(65f, 0f, 0f), new Vector3(0.06f, 0.04f, 0.06f), m.Frame, false);
            Prim(PrimitiveType.Cylinder, "Verre", helmet, new Vector3(0f, 0.163f, 0.097f), Quaternion.Euler(65f, 0f, 0f), new Vector3(0.05f, 0.004f, 0.05f), m.EmitWarm, false);
            // Faisceau de la lampe frontale, vers l'avant et un peu vers le bas : on voit où regarde le coéquipier.
            if (styleBeam != null)
                AbyssesArt.MeshObject("Faisceau", helmet, fx.ShaftCone, new Vector3(0f, 0.165f, 0.1f), Quaternion.Euler(-75f, 0f, 0f), new Vector3(0.32f, 1.3f, 0.32f), styleBeam);
            CombineAvatarPart(helmet, tinted, "Casque");
        }

        /// <summary>Gant de plongée : moufle, pouce côté intérieur, manchette de laiton (repère de la manette : +Z = avant).</summary>
        static void BuildGlove(Mats m, Transform hand, float side, List<GameObject> hidden, List<Renderer> tinted)
        {
            var glove = new GameObject("Gant").transform;
            glove.SetParent(hand, false);
            hidden.Add(glove.gameObject);
            GameObject mitten = Prim(PrimitiveType.Capsule, "Moufle", glove, new Vector3(0f, -0.01f, 0.01f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.085f, 0.07f, 0.055f), m.AvatarPaint[0], false);
            GameObject thumb = Prim(PrimitiveType.Capsule, "Pouce", glove, new Vector3(-side * 0.045f, 0f, 0.02f), Quaternion.Euler(90f, -side * 30f, 0f), new Vector3(0.03f, 0.035f, 0.03f), m.AvatarPaint[0], false);
            tinted.Add(mitten.GetComponent<Renderer>());
            tinted.Add(thumb.GetComponent<Renderer>());
            AbyssesArt.MeshObject("Manchette", glove, art.Ring, new Vector3(0f, -0.01f, -0.065f), Quaternion.identity, new Vector3(0.1f, 0.09f, 0.03f), m.Brass);
            CombineAvatarPart(glove, tinted, side < 0f ? "GantG" : "GantD");
        }

        /// <summary>
        /// Combinaison du scaphandrier : buste en caoutchouc, plastron et bande à la couleur du joueur, harnais, ceinture de
        /// plombs, deux bouteilles et tuyau d'air vers le casque ; bras et jambes en capsules recalculées par
        /// <see cref="AvatarBody"/>, bottes lestées.
        /// </summary>
        static void BuildAvatarBody(Mats m, GameObject root, Transform head, Transform left, Transform right, List<GameObject> hidden, List<Renderer> tinted)
        {
            var torso = new GameObject("Torso").transform;
            torso.SetParent(root.transform, false);
            Prim(PrimitiveType.Capsule, "Combinaison", torso, new Vector3(0f, -0.08f, 0f), Quaternion.identity, new Vector3(0.38f, 0.3f, 0.26f), m.Suit, false);
            foreach (float side in new[] { -1f, 1f })
            {
                GameObject pad = Prim(PrimitiveType.Sphere, "Epaulette", torso, new Vector3(side * 0.19f, 0.19f, 0f), Quaternion.identity, new Vector3(0.14f, 0.1f, 0.14f), m.AvatarPaint[0], false);
                tinted.Add(pad.GetComponent<Renderer>());
            }
            Prim(PrimitiveType.Cube, "Plastron", torso, new Vector3(0f, 0.06f, 0.122f), Quaternion.identity, new Vector3(0.22f, 0.15f, 0.035f), m.Panel, false);
            GameObject stripe = Prim(PrimitiveType.Cube, "Bande", torso, new Vector3(0f, 0.07f, 0.142f), Quaternion.identity, new Vector3(0.2f, 0.07f, 0.01f), m.AvatarPaint[0], false);
            tinted.Add(stripe.GetComponent<Renderer>());
            // Ceinture lumineuse à la couleur du joueur : silhouette lisible de face comme de dos.
            GameObject belt = Prim(PrimitiveType.Cylinder, "Ceinture", torso, new Vector3(0f, -0.25f, 0f), Quaternion.identity, new Vector3(0.39f, 0.035f, 0.28f), m.AvatarPaint[0], false);
            tinted.Add(belt.GetComponent<Renderer>());
            foreach (float side in new[] { -1f, 1f })
            {
                // Bouteilles à la couleur du joueur : on reconnaît son coéquipier de dos, de loin, dans le noir.
                foreach (GameObject tank in new[]
                {
                    Prim(PrimitiveType.Cylinder, "Bouteille", torso, new Vector3(side * 0.07f, 0f, -0.18f), Quaternion.identity, new Vector3(0.12f, 0.19f, 0.12f), m.AvatarPaint[0], false),
                    Prim(PrimitiveType.Sphere, "Ogive", torso, new Vector3(side * 0.07f, 0.19f, -0.18f), Quaternion.identity, new Vector3(0.12f, 0.07f, 0.12f), m.AvatarPaint[0], false),
                    Prim(PrimitiveType.Sphere, "Fond", torso, new Vector3(side * 0.07f, -0.19f, -0.18f), Quaternion.identity, new Vector3(0.12f, 0.06f, 0.12f), m.AvatarPaint[0], false),
                })
                    tinted.Add(tank.GetComponent<Renderer>());
                Prim(PrimitiveType.Cylinder, "Robinet", torso, new Vector3(side * 0.07f, 0.24f, -0.18f), Quaternion.identity, new Vector3(0.035f, 0.025f, 0.035f), m.Brass, false);
            }
            // Tuyau d'air : du robinet vers l'arrière du casque.
            Vector3 hoseFrom = new Vector3(0.07f, 0.26f, -0.18f), hoseTo = new Vector3(0f, 0.38f, -0.12f);
            Prim(PrimitiveType.Cylinder, "TuyauAir", torso, (hoseFrom + hoseTo) / 2f, Quaternion.FromToRotation(Vector3.up, hoseTo - hoseFrom),
                new Vector3(0.028f, (hoseTo - hoseFrom).magnitude / 2f, 0.028f), m.Rubber, false);
            hidden.Add(torso.gameObject);
            CombineAvatarPart(torso, tinted, "Buste");

            Transform Limb(string name, Material material)
            {
                GameObject limb = Prim(PrimitiveType.Capsule, name, root.transform, Vector3.zero, Quaternion.identity, Vector3.one * 0.08f, material, false);
                hidden.Add(limb);
                // Bande lumineuse à la couleur du joueur sur l'avant-bras et le bas de la jambe (lisible en mouvement).
                if (name.StartsWith("AvantBras") || name.StartsWith("Jambe"))
                {
                    GameObject band = Prim(PrimitiveType.Cylinder, "Bande", limb.transform, new Vector3(0f, -0.35f, 0f), Quaternion.identity, new Vector3(1.1f, 0.12f, 1.1f), m.AvatarPaint[0], false);
                    tinted.Add(band.GetComponent<Renderer>());
                }
                return limb.transform;
            }
            Transform Boot(string name)
            {
                var boot = new GameObject(name).transform;
                boot.SetParent(root.transform, false);
                Prim(PrimitiveType.Cube, "Tige", boot, new Vector3(0f, 0.02f, -0.02f), Quaternion.identity, new Vector3(0.13f, 0.11f, 0.2f), m.Frame, false);
                Prim(PrimitiveType.Cube, "Semelle", boot, new Vector3(0f, -0.045f, 0f), Quaternion.identity, new Vector3(0.145f, 0.035f, 0.28f), m.Rubber, false);
                Prim(PrimitiveType.Cube, "Coque", boot, new Vector3(0f, -0.005f, 0.1f), Quaternion.identity, new Vector3(0.135f, 0.06f, 0.09f), m.Brass, false);
                CombineAvatarPart(boot, tinted, name);
                hidden.Add(boot.gameObject);
                return boot;
            }

            SetFields(root.AddComponent<AvatarBody>(), ("head", head), ("leftHand", left), ("rightHand", right), ("torso", torso),
                ("leftUpperArm", Limb("BrasG", m.Suit)), ("leftForearm", Limb("AvantBrasG", m.Suit)),
                ("rightUpperArm", Limb("BrasD", m.Suit)), ("rightForearm", Limb("AvantBrasD", m.Suit)),
                ("leftThigh", Limb("CuisseG", m.Suit)), ("leftShin", Limb("JambeG", m.Suit)),
                ("rightThigh", Limb("CuisseD", m.Suit)), ("rightShin", Limb("JambeD", m.Suit)),
                ("leftBoot", Boot("BotteG")), ("rightBoot", Boot("BotteD")));
        }

        /// <summary>Entraînement contre le mur du fond de la cabine : bouton, fusible d'entraînement, prise.</summary>
        /// <summary>Aide-mémoire des boutons, collé au-dessus de chaque manette (voir <see cref="Abysses.Player.ControllerHints"/>).</summary>
        static void BuildControllerHints(Transform rig)
        {
            TMP_Text Hint(string hand)
            {
                Transform controller = Find(rig, hand);
                var go = new GameObject("Aide_Boutons", typeof(RectTransform));
                go.transform.SetParent(controller, false);
                go.transform.localPosition = new Vector3(0f, 0.07f, 0.02f);
                go.transform.localRotation = Quaternion.Euler(50f, 0f, 0f);
                var text = go.AddComponent<TextMeshPro>();
                text.fontSize = 0.2f;
                text.alignment = TextAlignmentOptions.Center;
                text.color = new Color(0.8f, 0.95f, 1f);
                text.textWrappingMode = TextWrappingModes.NoWrap;
                ((RectTransform)go.transform).sizeDelta = new Vector2(0.2f, 0.08f);
                return text;
            }
            var hints = rig.gameObject.AddComponent<Abysses.Player.ControllerHints>();
            SetFields(hints, ("left", Hint("Left Controller")), ("right", Hint("Right Controller")));
        }

        static void BuildCabinTutorial(Mats m, Transform cabin, float backZ)
        {
            Transform t = new GameObject("Tutorial").transform;
            t.SetParent(cabin, false);
            // +Z local du tutoriel vers le mur du fond : les faces avant (-Z) regardent l'intérieur de la cabine.
            t.localPosition = new Vector3(0f, 0f, backZ + 0.06f);
            t.localRotation = Quaternion.Euler(0f, 180f, 0f);

            Prim(PrimitiveType.Cube, "Board", t, new Vector3(0f, 1.5f, 0.02f), Quaternion.identity, new Vector3(1.5f, 0.42f, 0.02f), m.SlotEmpty, false);
            var textGo = new GameObject("Instructions", typeof(RectTransform));
            textGo.transform.SetParent(t, false);
            textGo.transform.localPosition = new Vector3(0f, 1.5f, 0.005f);
            var text = textGo.AddComponent<TextMeshPro>();
            text.fontSize = 0.55f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.5f, 0.95f, 1f);
            text.textWrappingMode = TextWrappingModes.Normal;
            ((RectTransform)textGo.transform).sizeDelta = new Vector2(1.4f, 0.38f);

            // 1. Bouton.
            GameObject button = Prim(PrimitiveType.Cube, "TrainingButton", t, new Vector3(-0.5f, 1.12f, 0f), Quaternion.identity, new Vector3(0.12f, 0.12f, 0.06f), m.EmitRed, true);
            var buttonInteractable = button.AddComponent<XRSimpleInteractable>();
            button.AddComponent<XRPokeFilter>();

            // 2. Fusible d'entraînement sur son support.
            Prim(PrimitiveType.Cube, "Cradle", t, new Vector3(0.5f, 0.98f, -0.08f), Quaternion.identity, new Vector3(0.3f, 0.04f, 0.2f), m.Panel, true);
            GameObject fuse = Prim(PrimitiveType.Cylinder, "TrainingFuse", t, new Vector3(0.5f, 1.1f, -0.08f), Quaternion.identity, new Vector3(0.06f, 0.08f, 0.06f), m.Symbols[4], true);
            var body = fuse.AddComponent<Rigidbody>();
            body.mass = 0.3f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var grab = fuse.AddComponent<XRGrabInteractable>();

            // 3. Prise.
            Prim(PrimitiveType.Cylinder, "SocketRing", t, new Vector3(0f, 1.12f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.16f, 0.005f, 0.16f), m.Symbols[4], false);
            var socketGo = new GameObject("TrainingSocket");
            socketGo.transform.SetParent(t, false);
            socketGo.transform.localPosition = new Vector3(0f, 1.12f, -0.08f);
            var trigger = socketGo.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.1f;
            var socket = socketGo.AddComponent<XRSocketInteractor>();

            // Halos des étapes (dans l'ordre : bouton, fusible, prise).
            var markers = new List<Renderer>();
            foreach (Vector3 p in new[] { new Vector3(-0.5f, 1.12f, -0.02f), new Vector3(0.5f, 1.1f, -0.08f), new Vector3(0f, 1.12f, -0.08f) })
            {
                GameObject marker = Prim(PrimitiveType.Sphere, "StepMarker", t, p, Quaternion.identity, Vector3.one, m.Highlight, false);
                // Échelle portée par un parent : CabinTutorial fait pulser l'échelle locale du marqueur.
                var holder = new GameObject("StepMarkerHolder").transform;
                holder.SetParent(t, false);
                holder.localPosition = p;
                holder.localScale = Vector3.one * 0.2f;
                marker.transform.SetParent(holder, false);
                marker.transform.localPosition = Vector3.zero;
                marker.transform.localScale = Vector3.one;
                Renderer renderer = marker.GetComponent<Renderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                markers.Add(renderer);
            }

            AudioSource success = Sfx(t.gameObject, fx.Success, 0.6f, 0.5f);
            var tutorial = t.gameObject.AddComponent<CabinTutorial>();
            var so = new UnityEditor.SerializedObject(tutorial);
            so.FindProperty("instructions").objectReferenceValue = text;
            so.FindProperty("button").objectReferenceValue = buttonInteractable;
            so.FindProperty("fuse").objectReferenceValue = grab;
            so.FindProperty("socket").objectReferenceValue = socket;
            SetArray(so.FindProperty("stepMarkers"), markers.ToArray());
            so.FindProperty("successSound").objectReferenceValue = success;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Pendant la descente, des particules défilent vers le haut derrière les hublots de la cabine (sensation de vitesse).</summary>
        static void DescentStreaks(Mats m, ParticleSystem bubbles, float half)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                var go = new GameObject("DescentStreaks");
                go.transform.SetParent(bubbles.transform, false);
                // Les bulles sont décalées de 0,35 m vers -x : on compense pour centrer chaque côté sur son hublot.
                go.transform.position = bubbles.transform.parent.TransformPoint(new Vector3(side * (half + 0.9f), -0.5f, 0f));
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.playOnAwake = false;
                main.loop = true;
                main.startLifetime = 1.2f;
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.025f);
                main.startColor = new Color(0.7f, 0.95f, 1f, 0.6f);
                main.maxParticles = 150;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                var emission = ps.emission;
                emission.rateOverTime = 90f;
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                // Reste dans le puits d'eau (rayon 2,95 m) qui entoure la cabine pendant la descente.
                shape.scale = new Vector3(1.0f, 0.2f, 2.4f);
                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
                velocity.y = new ParticleSystem.MinMaxCurve(3.5f, 5f);
                velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.08f;
                renderer.lengthScale = 2f;
                renderer.sharedMaterial = m.PlanktonHabitat;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        /// <summary>Montre de plongée au poignet gauche + bulle d'indice au-dessus.</summary>
        static void BuildDiveWatch(Mats m, Transform leftController)
        {
            var watch = new GameObject("DiveWatch").transform;
            watch.SetParent(leftController, false);
            watch.localPosition = new Vector3(0f, 0.03f, -0.11f);
            watch.localRotation = Quaternion.Euler(60f, 0f, 0f);
            Prim(PrimitiveType.Cube, "Strap", watch, new Vector3(0f, 0f, 0.012f), Quaternion.identity, new Vector3(0.05f, 0.065f, 0.012f), m.Hull, false);
            Prim(PrimitiveType.Cylinder, "Bezel", watch, new Vector3(0f, 0f, 0.004f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.056f, 0.006f, 0.056f), m.EmitCyan, false);
            Prim(PrimitiveType.Cylinder, "Face", watch, new Vector3(0f, 0f, 0.0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.05f, 0.004f, 0.05f), m.SlotEmpty, false);
            var faceGo = new GameObject("Display", typeof(RectTransform));
            faceGo.transform.SetParent(watch, false);
            faceGo.transform.localPosition = new Vector3(0f, 0f, -0.005f);
            var face = faceGo.AddComponent<TextMeshPro>();
            face.fontSize = 0.075f;
            face.alignment = TextAlignmentOptions.Center;
            face.textWrappingMode = TextWrappingModes.NoWrap;
            face.text = "SURFACE\nO2 --:--\n--:--";
            ((RectTransform)faceGo.transform).sizeDelta = new Vector2(0.045f, 0.045f);

            var panel = new GameObject("Message").transform;
            panel.SetParent(leftController, false);
            panel.localPosition = new Vector3(0f, 0.13f, -0.06f);
            panel.localRotation = Quaternion.Euler(20f, 0f, 0f);
            Prim(PrimitiveType.Quad, "Backing", panel, new Vector3(0f, 0f, 0.002f), Quaternion.identity, new Vector3(0.32f, 0.12f, 1f), m.SlotEmpty, false);
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(panel, false);
            var message = textGo.AddComponent<TextMeshPro>();
            message.fontSize = 0.12f;
            message.color = new Color(1f, 0.88f, 0.55f);
            message.alignment = TextAlignmentOptions.Center;
            message.textWrappingMode = TextWrappingModes.Normal;
            ((RectTransform)textGo.transform).sizeDelta = new Vector2(0.3f, 0.11f);

            // Bouton « MENU » sur le côté de la montre, appuyé du doigt de la main droite : plus visible que le bouton ☰ de la manette.
            var menu = Object.FindFirstObjectByType<Abysses.UI.MainMenu>(FindObjectsInactive.Include);
            GameObject menuButton = Prim(PrimitiveType.Cube, "MenuButton", watch, new Vector3(0.042f, 0f, -0.002f), Quaternion.identity,
                new Vector3(0.024f, 0.03f, 0.014f), m.EmitAmber, true);
            menuButton.AddComponent<XRSimpleInteractable>();
            menuButton.AddComponent<XRPokeFilter>();
            AudioSource menuClick = Sfx(menuButton, fx.Click, 0.5f);
            SetFields(menuButton.AddComponent<PokeButton>(), ("receiver", menu), ("index", 0), ("click", menuClick), ("travel", 0.006f));
            var menuLabelGo = new GameObject("MenuLabel", typeof(RectTransform));
            menuLabelGo.transform.SetParent(watch, false);
            menuLabelGo.transform.localPosition = new Vector3(0.042f, 0.024f, -0.004f);
            var menuLabel = menuLabelGo.AddComponent<TextMeshPro>();
            menuLabel.fontSize = 0.05f;
            menuLabel.alignment = TextAlignmentOptions.Center;
            menuLabel.textWrappingMode = TextWrappingModes.NoWrap;
            menuLabel.color = new Color(1f, 0.85f, 0.5f);
            menuLabel.text = "MENU";
            ((RectTransform)menuLabelGo.transform).sizeDelta = new Vector2(0.04f, 0.012f);

            AudioSource chime = Sfx(watch.gameObject, fx.HintChime, 0.8f, 0f);
            SetFields(watch.gameObject.AddComponent<DiveWatch>(), ("face", face), ("messagePanel", panel.gameObject), ("messageText", message), ("chime", chime));
        }

        /// <summary>Pupitre du maître du jeu (PC) et sa caméra spectateur, désactivés dans le casque.</summary>
        static void BuildGameMaster()
        {
            var cameraGo = new GameObject("GameMasterCamera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.depth = 10f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 60f;
            camera.fieldOfView = 75f;
            cameraGo.SetActive(false);
            SetFields(new GameObject("GameMaster").AddComponent<GameMasterConsole>(), ("spectatorCamera", camera));
        }
    }
}
