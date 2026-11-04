using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Abysses.Core;
using Abysses.Interaction;
using Abysses.Level;
using Abysses.UI;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Générateur, partie « monde vivant » : l'océan derrière les hublots, la station qui grince et fuit,
    /// la panne de courant de l'étage 1, les plaques de coque à frapper et les journaux de l'équipage.
    /// </summary>
    public static partial class AbyssesBuilder
    {
        // ------------------------------------------------------------------ Océan derrière les hublots

        /// <summary>
        /// Derrière le mur extérieur de chaque module : neige marine, et selon la profondeur un banc de poissons (-200 m),
        /// des leurres lumineux de poissons-lanternes (-1 500 m) ou de la bioluminescence (-4 000 m).
        /// Une silhouette géante passe de temps en temps de chaque côté de la station.
        /// </summary>
        static void OceanView(Mats m, Transform root, Module[] modules, int floor)
        {
            Transform ocean = new GameObject("Ocean").transform;
            ocean.SetParent(root, false);
            foreach (Module mod in modules)
            {
                Rect area = PlayArea.GetRect(mod.Q);
                float edge = mod.Vz < 0 ? area.yMin : area.yMax;
                var outside = new Vector3(mod.Center.x, 1.6f, edge + mod.Vz * 3.5f);

                // Neige marine : flocons qui tombent lentement dans le noir.
                Color snowColor = floor == 2 ? new Color(1f, 0.75f, 0.6f, 0.5f) : new Color(0.85f, 0.95f, 1f, 0.5f);
                ParticleSystem snow = Particles("MarineSnow_Q" + mod.Q, ocean, outside, m.Snow, 260, floor == 3 ? 10f : 22f, new Vector2(10f, 16f));
                SetBox(snow, new Vector3(PlayArea.QuadrantWidth, 3.4f, 5.5f));
                var main = snow.main;
                main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
                main.startColor = snowColor;
                Velocity(snow, new Vector2(-0.03f, 0.03f), new Vector2(-0.12f, -0.05f), new Vector2(-0.02f, 0.02f));

                if (floor == 1)
                {
                    // Banc de poissons qui longe la station : silhouettes sombres sur le bleu.
                    ParticleSystem fish = Particles("FishSchool_Q" + mod.Q, ocean, outside + new Vector3(0f, -0.1f, mod.Vz * -0.5f), m.Fish, 40, 1.4f, new Vector2(18f, 24f));
                    SetBox(fish, new Vector3(PlayArea.QuadrantWidth, 1.4f, 3f));
                    var fishMain = fish.main;
                    fishMain.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.28f);
                    Velocity(fish, new Vector2(0.55f, 0.85f), new Vector2(-0.03f, 0.03f), new Vector2(-0.05f, 0.05f));
                    var noise = fish.noise;
                    noise.enabled = true;
                    noise.strength = 0.12f;
                    noise.frequency = 0.4f;
                    // Les poissons regardent vers +x : vu depuis un module sud (on regarde vers -z), +x est à gauche de l'écran.
                    fish.GetComponent<ParticleSystemRenderer>().flip = new Vector3(mod.Vz < 0 ? 1f : 0f, 0f, 0f);
                }
                else
                {
                    // Lumières vivantes : leurres de poissons-lanternes (rares, chauds) ou bioluminescence (cyan / magenta).
                    bool deep = floor == 3;
                    ParticleSystem glow = Particles("Bioluminescence_Q" + mod.Q, ocean, outside, deep ? m.GlowBio : m.GlowAngler,
                        deep ? 120 : 12, deep ? 7f : 0.5f, deep ? new Vector2(6f, 11f) : new Vector2(14f, 22f));
                    SetBox(glow, new Vector3(PlayArea.QuadrantWidth, 3f, 5f));
                    var glowMain = glow.main;
                    glowMain.startSize = new ParticleSystem.MinMaxCurve(deep ? 0.03f : 0.05f, deep ? 0.08f : 0.09f);
                    glowMain.startColor = deep
                        ? new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.95f, 1f), new Color(1f, 0.3f, 0.9f))
                        : new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.45f), new Color(0.6f, 1f, 0.8f));
                    Velocity(glow, new Vector2(-0.15f, 0.15f), new Vector2(-0.05f, 0.08f), new Vector2(-0.1f, 0.1f));
                    // Pulsation : la lumière s'allume et s'éteint au cours de sa vie.
                    var size = glow.sizeOverLifetime;
                    size.enabled = true;
                    var pulse = new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.25f, 1f), new Keyframe(0.5f, 0.35f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0.1f));
                    size.size = new ParticleSystem.MinMaxCurve(1f, pulse);
                }
            }

            // Silhouette géante de chaque côté de la station (raie à -200 m, requin à -1 500 m, léviathan à -4 000 m).
            Vector2 scale = floor == 1 ? new Vector2(7f, 3.5f) : floor == 2 ? new Vector2(5f, 2.5f) : new Vector2(18f, 9f);
            float crossing = floor == 3 ? 75f : floor == 2 ? 28f : 45f;
            foreach (float side in new[] { -1f, 1f })
            {
                float z = side < 0 ? -9f : PlayArea.ArenaDepth + 9f;
                var go = new GameObject(side < 0 ? "Leviathan_South" : "Leviathan_North");
                go.transform.SetParent(ocean, false);
                go.transform.position = new Vector3(-14f, 1.6f, z);
                GameObject visual = Prim(PrimitiveType.Quad, "Silhouette", go.transform, Vector3.zero, Quaternion.identity, new Vector3(scale.x, scale.y, 1f), m.Leviathan, false);
                Renderer renderer = visual.GetComponent<Renderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                SetFields(go.AddComponent<OceanDrifter>(),
                    ("from", new Vector3(-14f, floor == 3 ? 2.2f : 1.6f, z)), ("to", new Vector3(PlayArea.ArenaWidth + 14f, floor == 3 ? 2.2f : 1.6f, z)),
                    ("crossingTime", crossing), ("visual", visual.transform));
                // Les deux côtés ne passent pas en même temps.
                var so = new SerializedObject(go.GetComponent<OceanDrifter>());
                so.FindProperty("pauseRange").vector2Value = side < 0 ? new Vector2(25f, 55f) : new Vector2(40f, 80f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static ParticleSystem Particles(string name, Transform parent, Vector3 position, Material material, int max, float rate, Vector2 lifetime)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.prewarm = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = 0f;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 0.5f;
            return ps;
        }

        static void SetBox(ParticleSystem ps, Vector3 size)
        {
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
        }

        static void Velocity(ParticleSystem ps, Vector2 x, Vector2 y, Vector2 z)
        {
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(x.x, x.y);
            velocity.y = new ParticleSystem.MinMaxCurve(y.x, y.y);
            velocity.z = new ParticleSystem.MinMaxCurve(z.x, z.y);
        }

        // ------------------------------------------------------------------ La station vit

        /// <summary>Réverbération métallique par module, coque qui grince, gouttes (étages 1 et 2), journaux de bord.</summary>
        static void StationLife(Mats m, Transform root, Module[] modules, int floor)
        {
            Transform life = new GameObject("StationLife").transform;
            life.SetParent(root, false);

            var creakPoints = new List<Transform>();
            foreach (Module mod in modules)
            {
                // Réverbération : on entend qu'on est dans une coque métallique (la salle commune résonne davantage).
                var reverb = new GameObject("Reverb_Q" + mod.Q).AddComponent<AudioReverbZone>();
                reverb.transform.SetParent(life, false);
                reverb.transform.position = mod.Center + Vector3.up * 1.5f;
                reverb.minDistance = 6f;
                reverb.maxDistance = 9f;
                reverb.reverbPreset = floor == 3 ? AudioReverbPreset.Cave : AudioReverbPreset.Hallway;

                foreach (float x in new[] { -6f, 6f })
                {
                    var point = new GameObject("CreakPoint").transform;
                    point.SetParent(life, false);
                    point.position = mod.P(x, 2.8f, mod.Vz * 3.5f);
                    creakPoints.Add(point);
                }

                if (floor < 3) BuildDrips(m, life, mod);
                BuildCrewLog(m, root, mod, floor);
            }

            AudioSource creak = Sfx(new GameObject("HullCreaks"), null, 0.8f);
            creak.transform.SetParent(life, false);
            creak.maxDistance = 16f;
            var shots = creak.gameObject.AddComponent<RandomOneShots>();
            var so = new SerializedObject(shots);
            SetArray(so.FindProperty("clips"), fx.CreakLow, fx.CreakHigh);
            SetArray(so.FindProperty("emitters"), creakPoints.ToArray());
            so.FindProperty("intervalRange").vector2Value = floor == 3 ? new Vector2(6f, 14f) : new Vector2(9f, 22f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Gouttes qui tombent du plafond, avec le « ploc » au sol.</summary>
        static void BuildDrips(Mats m, Transform parent, Module mod)
        {
            var points = new List<Transform>();
            foreach (Vector2 spot in new[] { new Vector2(-3.5f, 1.6f), new Vector2(3f, -2.2f) })
            {
                Vector3 top = mod.P(spot.x, WallHeight - 0.05f, spot.y);
                ParticleSystem drip = Particles("Drip_Q" + mod.Q, parent, top, m.Drop, 6, 0.4f, new Vector2(0.78f, 0.78f));
                var main = drip.main;
                main.prewarm = false;
                main.gravityModifier = 1f;
                main.startSize = 0.015f;
                var shape = drip.shape;
                shape.enabled = false;
                var fade = drip.colorOverLifetime;
                fade.enabled = false;

                var point = new GameObject("DripPoint").transform;
                point.SetParent(parent, false);
                point.position = new Vector3(top.x, 0.05f, top.z);
                points.Add(point);
            }
            AudioSource source = Sfx(new GameObject("Drips_Q" + mod.Q), null, 0.6f);
            source.transform.SetParent(parent, false);
            source.maxDistance = 7f;
            var shots = source.gameObject.AddComponent<RandomOneShots>();
            var so = new SerializedObject(shots);
            SetArray(so.FindProperty("clips"), fx.Drip);
            SetArray(so.FindProperty("emitters"), points.ToArray());
            so.FindProperty("intervalRange").vector2Value = new Vector2(1.2f, 3.5f);
            so.FindProperty("volumeRange").vector2Value = new Vector2(0.3f, 0.7f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Terminal du journal de bord, sur le mur du fond (bouton LIRE à hauteur de main).</summary>
        static void BuildCrewLog(Mats m, Transform parent, Module mod, int floor)
        {
            (string fr, string en) entry = AbyssesHints.CrewLogs[floor - 1][mod.Q == 0 || mod.Q == 2 ? 0 : 1];
            var root = new GameObject("CrewLog_Q" + mod.Q);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(mod.P(-7.16f, 1.38f, 2.2f), mod.Facing(-1f, 0f));
            Transform t = root.transform;

            Static(Prim(PrimitiveType.Cube, "Case", t, new Vector3(0f, 0f, 0.025f), Quaternion.identity, new Vector3(0.56f, 0.44f, 0.05f), m.Panel, false));
            Prim(PrimitiveType.Quad, "Screen", t, new Vector3(0f, 0.01f, -0.002f), Quaternion.identity, new Vector3(0.5f, 0.36f, 1f), m.SlotEmpty, false);
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(t, false);
            textGo.transform.localPosition = new Vector3(0f, 0.01f, -0.005f);
            var text = textGo.AddComponent<TextMeshPro>();
            text.fontSize = 0.19f;
            text.color = new Color(0.45f, 1f, 0.75f);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            ((RectTransform)textGo.transform).sizeDelta = new Vector2(0.46f, 0.32f);

            GameObject button = Prim(PrimitiveType.Cube, "ReadButton", t, new Vector3(0f, -0.32f, -0.01f), Quaternion.identity, new Vector3(0.14f, 0.07f, 0.05f), m.EmitCyan, true);
            var interactable = button.AddComponent<XRSimpleInteractable>();
            button.AddComponent<XRPokeFilter>();
            Halo(m, button, button);
            Label(t, "LIRE", new Vector3(0f, -0.26f, -0.01f), 0.35f, null, "READ");

            AudioSource radio = Sfx(root, fx.RadioStatic, 0.35f);
            radio.loop = true;
            SetFields(root.AddComponent<CrewLog>(), ("playButton", interactable), ("screen", text), ("radio", radio),
                ("french", entry.fr), ("english", entry.en));
        }

        // ------------------------------------------------------------------ Coups sur la cloison

        /// <summary>Plaque de coque à frapper sur le mur commun de deux modules partenaires.</summary>
        static void BuildKnockWall(Mats m, Transform parent, Module a, Module b)
        {
            var root = new GameObject("KnockWall_Q" + a.Q + "_Q" + b.Q);
            root.transform.SetParent(parent, false);
            root.AddComponent<NetworkObject>();
            var knock = root.AddComponent<KnockWall>();
            var so = new SerializedObject(knock);
            FillKnockSide(m, root.transform, a, so.FindProperty("sideA"));
            FillKnockSide(m, root.transform, b, so.FindProperty("sideB"));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void FillKnockSide(Mats m, Transform parent, Module mod, SerializedProperty side)
        {
            Quaternion facing = mod.Facing(1f, 0f);
            Vector3 position = mod.P(7.15f, 1.3f, 3.1f);
            GameObject plate = Prim(PrimitiveType.Cylinder, "KnockPlate_Q" + mod.Q, parent, position, facing * Quaternion.Euler(90f, 0f, 0f), new Vector3(0.24f, 0.012f, 0.24f), m.Pipe, true);
            var interactable = plate.AddComponent<XRSimpleInteractable>();
            Halo(m, plate, plate, 1.1f);
            Label(parent, "FRAPPER", position + Vector3.up * 0.2f + facing * Vector3.back * 0.01f, 0.4f, facing, "KNOCK");
            AudioSource sound = Sfx(plate, fx.Knock, 1f);
            sound.maxDistance = 10f;
            side.FindPropertyRelative("quadrant").intValue = mod.Q;
            side.FindPropertyRelative("plate").objectReferenceValue = interactable;
            side.FindPropertyRelative("knock").objectReferenceValue = sound;
        }

        // ------------------------------------------------------------------ Étage 1 : panne de courant

        /// <summary>
        /// Départ en éclairage de secours : néons éteints, lumières des modules coupées, lampes ambre basses.
        /// La scène est enregistrée dans cet état (bake « secours ») ; le bake « courant » est fait par <see cref="BakeFloors"/>.
        /// </summary>
        static void BuildPowerOutage(Mats m, Transform root, Module[] modules, PuzzleObjective trigger)
        {
            var neons = new List<Renderer>();
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name != "CeilingStrip") continue;
                r.sharedMaterial = m.NeonOff;
                neons.Add(r);
            }
            // Ordre d'allumage : module par module, en partant du sas.
            neons.Sort((x, y) => (x.transform.position.x + x.transform.position.z * 40f).CompareTo(y.transform.position.x + y.transform.position.z * 40f));
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
                if (light.name == "Light") light.enabled = false;

            var lamps = new List<Renderer>();
            foreach (Module mod in modules)
            {
                Rect r = PlayArea.GetRect(mod.Q, PlayArea.WallInset);
                float outerFace = mod.Vz < 0 ? r.yMin : r.yMax;
                float backFace = mod.Sx > 0 ? r.xMin : r.xMax;
                var spots = new List<(Vector3 lamp, Vector3 scale)>();
                foreach (float z in new[] { -2.4f, 2.4f })
                    spots.Add((new Vector3(backFace + mod.Sx * 0.05f, 2.45f, mod.Center.z + z), new Vector3(0.1f, 0.12f, 0.32f)));
                foreach (float x in new[] { -2.5f, 2.5f })
                {
                    Vector3 p = mod.P(x, 2.45f, 0f);
                    p.z = outerFace - mod.Vz * 0.05f;
                    spots.Add((p, new Vector3(0.32f, 0.12f, 0.1f)));
                }
                foreach ((Vector3 position, Vector3 scale) in spots)
                {
                    GameObject lamp = Prim(PrimitiveType.Cube, "EmergencyLamp", root, position, Quaternion.identity, scale, m.EmitAmber, false);
                    Static(lamp);
                    lamps.Add(lamp.GetComponent<Renderer>());
                    var lightGo = new GameObject("EmergencyLight");
                    lightGo.transform.SetParent(root, false);
                    lightGo.transform.position = position + (mod.Center - position).normalized * 0.25f - Vector3.up * 0.1f;
                    var light = lightGo.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.55f, 0.2f);
                    light.range = 7f;
                    light.intensity = 2.8f;
                    light.lightmapBakeType = LightmapBakeType.Baked;
                    light.shadows = LightShadows.Soft;
                    light.shadowRadius = 0.15f;
                }
            }

            var power = root.gameObject.AddComponent<PowerRestore>();
            var so = new SerializedObject(power);
            so.FindProperty("trigger").objectReferenceValue = trigger;
            SetArray(so.FindProperty("neons"), neons.ToArray());
            so.FindProperty("neonOff").objectReferenceValue = m.NeonOff;
            so.FindProperty("neonOn").objectReferenceValue = m.EmitCyan;
            SetArray(so.FindProperty("emergencyLamps"), lamps.ToArray());
            so.FindProperty("emergencyOff").objectReferenceValue = m.NeonOff;
            so.FindProperty("powerUpSound").objectReferenceValue = Sfx(new GameObject("PowerUpSound"), fx.PowerUp, 1f, 0f);
            AudioSource buzz = Sfx(new GameObject("NeonSound"), fx.NeonBuzz, 0.7f);
            so.FindProperty("neonSound").objectReferenceValue = buzz;
            so.ApplyModifiedPropertiesWithoutUndo();
            ((AudioSource)so.FindProperty("powerUpSound").objectReferenceValue).transform.SetParent(root, false);
            buzz.transform.SetParent(root, false);
        }

        /// <summary>
        /// Étage 1 : bake « courant rétabli » (néons et lumières des modules), copie de ses lightmaps,
        /// puis bake « secours » qui reste celui de la scène. <see cref="PowerRestore"/> échange les deux en jeu.
        /// </summary>
        static void BakePowerVariants(Scene scene)
        {
            PowerRestore power = null;
            foreach (GameObject go in scene.GetRootGameObjects())
                if (power == null) power = go.GetComponentInChildren<PowerRestore>(true);
            if (power == null)
            {
                Lightmapping.Bake();
                return;
            }

            var so = new SerializedObject(power);
            Material neonOn = (Material)so.FindProperty("neonOn").objectReferenceValue;
            Material neonOff = (Material)so.FindProperty("neonOff").objectReferenceValue;
            var lights = new List<Light>();
            foreach (GameObject go in scene.GetRootGameObjects()) lights.AddRange(go.GetComponentsInChildren<Light>(true));

            void SetPowered(bool powered)
            {
                SerializedProperty neons = so.FindProperty("neons");
                for (int i = 0; i < neons.arraySize; i++)
                    ((Renderer)neons.GetArrayElementAtIndex(i).objectReferenceValue).sharedMaterial = powered ? neonOn : neonOff;
                SerializedProperty lamps = so.FindProperty("emergencyLamps");
                for (int i = 0; i < lamps.arraySize; i++)
                    ((Renderer)lamps.GetArrayElementAtIndex(i).objectReferenceValue).sharedMaterial = powered ? neonOff : amberForBake;
                foreach (Light light in lights)
                {
                    if (light.name == "Light") light.enabled = powered;
                    else if (light.name == "EmergencyLight") light.enabled = !powered;
                }
            }

            amberForBake = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/M_Emit_Amber.mat");
            SetPowered(true);
            Lightmapping.Bake();
            string folder = SceneDir + "/" + scene.name + "_Courant";
            if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
            EnsureFolder(folder);
            LightmapData[] maps = LightmapSettings.lightmaps;
            var copies = new Texture2D[maps.Length];
            for (int i = 0; i < maps.Length; i++)
            {
                string source = AssetDatabase.GetAssetPath(maps[i].lightmapColor);
                string target = folder + "/" + System.IO.Path.GetFileName(source);
                AssetDatabase.CopyAsset(source, target);
                copies[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(target);
            }

            SetPowered(false);
            Lightmapping.Bake();
            SetArray(so.FindProperty("poweredLightmaps"), copies);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Abysses] Étage 1 : {copies.Length} lightmap(s) « courant rétabli » enregistrée(s) dans {folder}.");
        }

        static Material amberForBake;

        // ------------------------------------------------------------------ Étage 2 : fuites

        /// <summary>Jet de vapeur sur le mur du fond, néon défaillant, eau qui monte par la brèche.</summary>
        static void BuildLeaks(Mats m, Transform root, Module mod, PuzzleObjective gaugeObjective)
        {
            Transform leaks = new GameObject("Leaks_Q" + mod.Q).transform;
            leaks.SetParent(root, false);

            // Vapeur : tuyau percé au-dessus des têtes, jet horizontal vers le centre du module.
            Vector3 nozzle = mod.P(-7.05f, 2.45f, -2.9f);
            Quaternion inward = mod.Facing(1f, 0f);
            Static(Prim(PrimitiveType.Cylinder, "SteamPipe", leaks, nozzle, inward * Quaternion.Euler(90f, 0f, 0f), new Vector3(0.09f, 0.1f, 0.09f), m.Pipe, false));
            var jetGo = new GameObject("SteamJet");
            jetGo.transform.SetParent(leaks, false);
            jetGo.transform.SetPositionAndRotation(nozzle + inward * Vector3.forward * 0.1f, inward);
            var jet = jetGo.AddComponent<ParticleSystem>();
            jet.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = jet.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = jet.emission;
            emission.rateOverTime = 70f;
            var shape = jet.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.02f;
            var size = jet.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 4f));
            var fade = jet.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var drag = jet.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 2.5f;
            var jetRenderer = jetGo.GetComponent<ParticleSystemRenderer>();
            jetRenderer.sharedMaterial = m.Steam;
            jetRenderer.shadowCastingMode = ShadowCastingMode.Off;
            jetRenderer.receiveShadows = false;
            AudioSource hiss = Sfx(jetGo, fx.SteamHiss, 0.55f);
            hiss.loop = true;
            SetFields(jetGo.AddComponent<SteamVent>(), ("jet", jet), ("hiss", hiss), ("stopWhenSolved", gaugeObjective));

            // Néon fatigué : le bandeau du côté du partenaire clignote par rafales.
            Transform module = root.Find("Module_Q" + mod.Q);
            Vector3 stripPosition = mod.P(4f, WallHeight - 0.03f, 0f);
            if (module != null)
            {
                foreach (Renderer r in module.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.name != "CeilingStrip" || (r.transform.position - stripPosition).sqrMagnitude > 0.01f) continue;
                    AudioSource buzz = Sfx(r.gameObject, fx.NeonBuzz, 0.5f);
                    SetFields(r.gameObject.AddComponent<NeonFlicker>(), ("neon", r), ("onMaterial", r.sharedMaterial), ("offMaterial", m.NeonOff), ("buzz", buzz));
                }
            }

            // Brèche : l'eau monte lentement jusqu'aux chevilles tant que l'étage n'est pas résolu.
            Rect rect = PlayArea.GetRect(mod.Q, PlayArea.WallInset);
            var water = new GameObject("Water_Q" + mod.Q).transform;
            water.SetParent(leaks, false);
            water.position = new Vector3(rect.center.x, 0f, rect.center.y);
            GameObject surface = Prim(PrimitiveType.Quad, "Surface", water, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new Vector3(rect.width, rect.height, 1f), m.Water, false);
            Renderer waterRenderer = surface.GetComponent<Renderer>();
            waterRenderer.shadowCastingMode = ShadowCastingMode.Off;
            waterRenderer.receiveShadows = false;
            SetFields(water.gameObject.AddComponent<RisingWater>(), ("surface", surface.transform));
        }
    }
}
