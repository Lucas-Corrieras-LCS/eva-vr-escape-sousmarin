using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Abysses.Core;
using Abysses.Interaction;
using Abysses.Level;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Générateur, étage 3 « Ballasts » (-2 800 m) : salle des pompes qui prend l'eau.
    /// - Collecteur de vannes croisé : les vannes sont ici, le schéma d'ouverture est chez le partenaire (privé).
    ///   Mauvaise combinaison = coup de bélier, l'eau monte d'un coup. Bonne = la pompe vide le module.
    /// - Relais d'appels « purge des ballasts » (3 manches).
    /// - Leviers de chasse des ballasts, à abaisser ensemble : fin de l'étage.
    /// L'eau monte lentement dans chaque module tant que sa pompe n'est pas amorcée (jamais au-dessus des mollets).
    /// </summary>
    public static partial class AbyssesBuilder
    {
        /// <summary>Numéro de l'étage dans la partie (objectifs, profondeur).</summary>
        const int BallastFloor = 3;
        /// <summary>Style de l'étage pour l'habillage (titre, mobilier) : distinct des 3 autres.</summary>
        const int BallastStyle = 4;
        /// <summary>Le cœur du réacteur est désormais le 4e étage.</summary>
        const int CoreFloor = 4;

        static void BuildFloorBallasts(Mats m)
        {
            Scene scene = NewFloorScene(BallastFloor, out Transform root);
            FloorEnvironment(root, new Color(0.02f, 0.075f, 0.065f), 0.065f, new Color(0.035f, 0.09f, 0.08f), fx.AmbienceLabo);

            Module[] modules = BuildModules(m, root, Shell.Standard, new Color(0.5f, 0.95f, 0.85f), 7f, m.EmitCyan, BallastStyle);
            foreach (Module mod in modules)
            {
                Plankton(root, mod, m.PlanktonHabitat, new Color(0.5f, 1f, 0.85f), new Color(0.8f, 1f, 0.9f));
                // Gyrophare d'alerte inondation, comme au labo.
                Beacon(m, root, mod.P(-7.05f, 2.75f, -2.2f));
            }
            OceanView(m, root, modules, BallastStyle);
            StationLife(m, root, modules, BallastStyle);

            var water = new RisingWater[4];
            for (int q = 0; q < 4; q++) water[q] = BuildFloodWater(m, root, modules[q]);

            var prerequisites = new List<PuzzleObjective>();
            var levers = new LeverHandle[4];
            for (int q = 0; q < 4; q++)
            {
                Module mod = modules[q];
                var partner = new Module(mod.Partner);
                // Schéma de ce collecteur, chez le partenaire (lui seul le voit) + copie de repli chez soi.
                // Mur du fond, côté z < 0 : le terminal du journal de bord occupe z = 2,2.
                (GameObject diagram, Renderer[] icons, TextMeshPro text) = BuildManifoldDiagram(m, root, mod, partner,
                    partner.P(-7.16f, 1.3f, -1.7f), partner.Facing(-1f, 0f), "Schema_Pour_Q" + q);
                (GameObject fallback, Renderer[] fallbackIcons, TextMeshPro fallbackText) = BuildManifoldDiagram(m, root, mod, mod,
                    mod.P(-7.16f, 1.3f, -2.6f), mod.Facing(-1f, 0f), "SchemaRepli_Q" + q);
                ShowWhenPartnerAbsent(root, mod, "Repli_Schema_Q" + q, fallback);

                var allIcons = new List<Renderer>(icons);
                allIcons.AddRange(fallbackIcons);
                prerequisites.Add(BuildValveManifold(m, root, mod, water[q], allIcons.ToArray(), new[] { text, fallbackText }));
                levers[q] = BuildLever(m, root, mod.Q, mod.P(5.4f, 0f, 2.6f), mod.Facing(0f, -1f));
            }

            prerequisites.Add(BuildCallOutRelay(m, root, modules, BallastFloor, RelayWallX, 3, "Purge des ballasts", "Ballast purge",
                "PURGE DES BALLASTS · DÉLESTAGE", "BALLAST PURGE · DUMPING"));
            BuildSyncLevers(root, BallastFloor, "Chasse des ballasts", "Ballast blow", "ballast", levers, prerequisites.ToArray());

            BuildHatch(m, root, modules[0], PartnerHatchSpot(modules[0]), modules[1], PartnerHatchSpot(modules[1]));
            BuildHatch(m, root, modules[2], PartnerHatchSpot(modules[2]), modules[3], PartnerHatchSpot(modules[3]));
            BuildKnockWall(m, root, modules[0], modules[1]);
            BuildKnockWall(m, root, modules[2], modules[3]);

            BuildProbes(root, modules);
            ConfigureLighting(FloorScenes[BallastFloor - 1]);
            SaveScene(scene, FloorScenes[BallastFloor - 1]);
        }

        /// <summary>Eau du module : monte lentement jusqu'aux mollets (7 min), vidée par la pompe du module.</summary>
        static RisingWater BuildFloodWater(Mats m, Transform root, Module mod)
        {
            Rect rect = PlayArea.GetRect(mod.Q, PlayArea.WallInset);
            var water = new GameObject("Inondation_Q" + mod.Q).transform;
            water.SetParent(root, false);
            water.position = new Vector3(rect.center.x, 0f, rect.center.y);
            GameObject surface = Prim(PrimitiveType.Quad, "Surface", water, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new Vector3(rect.width, rect.height, 1f), m.Water, false);
            Renderer renderer = surface.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var rising = water.gameObject.AddComponent<RisingWater>();
            SetFields(rising, ("surface", surface.transform), ("startHeight", 0.03f), ("maxHeight", 0.32f), ("riseTime", 420f), ("drainTime", 8f));
            return rising;
        }

        /// <summary>
        /// Collecteur de vannes, mur du fond : tuyau principal, 5 vannes quart de tour de couleur (poignée parallèle au
        /// tuyau = ouverte), bouton « AMORCER LA POMPE ». Repère local : face avant en -Z.
        /// </summary>
        static PuzzleObjective BuildValveManifold(Mats m, Transform parent, Module mod, RisingWater water, Renderer[] icons, TextMeshPro[] texts)
        {
            var root = new GameObject("Collecteur_Q" + mod.Q);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(mod.P(-7.16f, 1.15f, 0f), mod.Facing(-1f, 0f));
            root.AddComponent<NetworkObject>();
            PuzzleObjective objective = Objective(root, BallastFloor, mod.Q, "Collecteur " + QuadrantNames[mod.Q], "Manifold " + QuadrantNamesEn[mod.Q], "manifold");
            var manifold = root.AddComponent<ValveManifold>();
            Transform t = root.transform;

            DeviceMount(m, t, new Vector2(1.5f, 0.98f), 1.15f);
            Prim(PrimitiveType.Cube, "Boitier", t, new Vector3(0f, 0f, -0.02f), Quaternion.identity, new Vector3(1.4f, 0.88f, 0.06f), m.Panel, false);
            Label(t, "COLLECTEUR · POMPE DE CALE", new Vector3(-0.05f, 0.37f, -0.055f), 0.42f, null, "MANIFOLD · BILGE PUMP");
            Renderer lamp = Prim(PrimitiveType.Sphere, "Status", t, new Vector3(0.62f, 0.37f, -0.06f), Quaternion.identity, Vector3.one * 0.05f, m.StatusIdle, false).GetComponent<Renderer>();
            Label(t, "POIGNÉE PARALLÈLE AU TUYAU = OUVERTE", new Vector3(0f, -0.2f, -0.055f), 0.24f, null, "HANDLE ALONG THE PIPE = OPEN");

            // Tuyau principal, brides aux extrémités et bagues de couleur sous chaque vanne.
            const float pipeY = 0.06f, pipeZ = -0.1f;
            Prim(PrimitiveType.Cylinder, "Tuyau", t, new Vector3(0f, pipeY, pipeZ), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.1f, 0.64f, 0.1f), m.PipeGrey, false);
            foreach (float x in new[] { -0.64f, 0.64f })
            {
                Prim(PrimitiveType.Cylinder, "Bride", t, new Vector3(x, pipeY, pipeZ), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.16f, 0.015f, 0.16f), m.Frame, false);
                Prim(PrimitiveType.Cylinder, "Coude", t, new Vector3(x, pipeY, pipeZ / 2f + 0.01f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.1f, 0.05f, 0.1f), m.PipeGrey, false);
            }

            var localOnly = new List<Behaviour>();
            var handles = new Transform[ValveManifold.Count];
            for (int i = 0; i < ValveManifold.Count; i++)
            {
                float x = -0.48f + i * 0.24f;
                Prim(PrimitiveType.Cylinder, "Bague_" + i, t, new Vector3(x, pipeY, pipeZ), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.118f, 0.022f, 0.118f), m.Symbols[i], false);
                Prim(PrimitiveType.Cube, "Corps_" + i, t, new Vector3(x, pipeY, pipeZ), Quaternion.identity, new Vector3(0.08f, 0.13f, 0.12f), m.Brass, false);
                Prim(PrimitiveType.Cylinder, "Tige_" + i, t, new Vector3(x, pipeY, pipeZ - 0.08f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.025f, 0.03f, 0.025f), m.Frame, false);

                var pivot = new GameObject("Vanne_" + i).transform;
                pivot.SetParent(t, false);
                pivot.localPosition = new Vector3(x, pipeY, pipeZ - 0.115f);
                // Poignée en papillon : bras long (côté prise) et contrepoids court, gaine de la couleur de la vanne.
                GameObject grip = Prim(PrimitiveType.Cube, "Poignee", pivot, new Vector3(0.055f, 0f, 0f), Quaternion.identity, new Vector3(0.15f, 0.034f, 0.026f), m.Symbols[i], true);
                Prim(PrimitiveType.Cylinder, "Moyeu", pivot, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new Vector3(0.045f, 0.016f, 0.045f), m.Frame, false);
                Prim(PrimitiveType.Cube, "Talon", pivot, new Vector3(-0.03f, 0f, 0f), Quaternion.identity, new Vector3(0.04f, 0.026f, 0.02f), m.Frame, false);
                var interactable = grip.AddComponent<XRSimpleInteractable>();
                grip.AddComponent<XRPokeFilter>();
                Halo(m, grip, grip, 1.3f);
                SetFields(grip.AddComponent<PokeButton>(), ("receiver", manifold), ("index", i), ("travel", 0f));
                localOnly.Add(interactable);
                handles[i] = pivot;
                Label(t, (i + 1).ToString(), new Vector3(x, -0.1f, -0.055f), 0.36f);
            }

            PokeButtonObject(m, t, "Amorcer", new Vector3(0f, -0.32f, -0.03f), new Vector2(0.46f, 0.08f), m.EmitAmber, manifold, ValveManifold.PrimeIndex, null, null, localOnly);
            ButtonText(t, "AMORCER LA POMPE", "PRIME THE PUMP", new Vector3(0f, -0.32f, -0.072f));
            LocalOnly(root, mod.Q, localOnly);

            ParticleSystem steam = SteamBurst(m, t, new Vector3(0f, pipeY + 0.05f, pipeZ - 0.05f));
            AudioSource turn = Sfx(root, fx.Key, 0.7f);
            AudioSource surge = Sfx(root, fx.ReactorFail, 0.9f);
            AudioSource success = Sfx(root, fx.PowerUp, 0.9f);
            SetFields(manifold, ("objective", objective), ("iconOpen", m.StatusOk), ("iconClosed", m.StatusError), ("statusLamp", lamp),
                ("lampIdle", m.StatusIdle), ("lampOk", m.StatusOk), ("lampError", m.StatusError), ("turnSound", turn),
                ("surgeSound", surge), ("successSound", success), ("steam", steam), ("water", water));
            var so = new UnityEditor.SerializedObject(manifold);
            SetArray(so.FindProperty("handles"), handles);
            SetArray(so.FindProperty("diagramIcons"), icons);
            SetArray(so.FindProperty("diagramTexts"), texts);
            so.ApplyModifiedPropertiesWithoutUndo();
            return objective;
        }

        /// <summary>
        /// Schéma hydraulique du collecteur de <paramref name="forModule"/>, posé dans le module <paramref name="viewer"/> et
        /// lisible de lui seul : une ligne par vanne (pastille de couleur + voyant vert ouverte / rouge fermée + texte).
        /// </summary>
        static (GameObject root, Renderer[] icons, TextMeshPro text) BuildManifoldDiagram(Mats m, Transform parent, Module forModule, Module viewer,
            Vector3 position, Quaternion rotation, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);
            Transform t = root.transform;
            DeviceMount(m, t, new Vector2(0.66f, 0.86f), 1.3f);
            Prim(PrimitiveType.Cube, "Boitier", t, new Vector3(0f, 0f, -0.02f), Quaternion.identity, new Vector3(0.58f, 0.78f, 0.06f), m.Panel, false);
            Label(t, "SCHÉMA HYDRAULIQUE", new Vector3(0f, 0.33f, -0.055f), 0.3f, null, "HYDRAULIC DIAGRAM");
            Label(t, "COLLECTEUR " + QuadrantNames[forModule.Q], new Vector3(0f, 0.27f, -0.055f), 0.3f, null, QuadrantNamesEn[forModule.Q] + " MANIFOLD");
            Prim(PrimitiveType.Cube, "Ecran", t, new Vector3(0.04f, -0.06f, -0.052f), Quaternion.identity, new Vector3(0.46f, 0.56f, 0.004f), m.SlotEmpty, false);

            var icons = new Renderer[ValveManifold.Count];
            var privateRenderers = new List<Renderer>();
            for (int v = 0; v < ValveManifold.Count; v++)
            {
                float y = 0.16f - v * 0.105f;
                Renderer chip = Prim(PrimitiveType.Cube, "Pastille_" + v, t, new Vector3(-0.22f, y, -0.06f), Quaternion.identity, new Vector3(0.05f, 0.05f, 0.012f), m.Symbols[v], false).GetComponent<Renderer>();
                icons[v] = Prim(PrimitiveType.Cube, "Voyant_" + v, t, new Vector3(-0.14f, y, -0.06f), Quaternion.identity, new Vector3(0.05f, 0.05f, 0.012f), m.SlotEmpty, false).GetComponent<Renderer>();
                privateRenderers.Add(chip);
                privateRenderers.Add(icons[v]);
            }
            TextMeshPro text = Text(t, "Texte", new Vector3(0.09f, -0.05f, -0.056f), Quaternion.identity, new Vector2(0.32f, 0.54f), 0.32f, new Color(0.75f, 1f, 0.9f), false);
            text.alignment = TextAlignmentOptions.Left;
            text.lineSpacing = 18f;
            privateRenderers.Add(text.GetComponent<Renderer>());
            Private(root, viewer.Q, privateRenderers.ToArray());
            return (root, icons, text);
        }

        /// <summary>Bouffée de vapeur (coup de bélier) : une salve, pas de boucle.</summary>
        static ParticleSystem SteamBurst(Mats m, Transform parent, Vector3 position)
        {
            var go = new GameObject("Vapeur");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.gravityModifier = -0.05f;
            main.maxParticles = 50;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.05f;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = m.Steam;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        // ---------- Mobilier : salle des pompes

        /// <summary>Étage 3 — ballasts : deux grosses pompes, fûts, bouche d'évacuation, flaques, panneau d'alerte.</summary>
        static void DressBallast(Mats m, Transform root, Module mod)
        {
            Transform t = new GameObject("Mobilier_Q" + mod.Q).transform;
            t.SetParent(root, false);
            float s = mod.Vz;
            BallastPump(m, t, mod.P(-4.6f, 0f, s * 2.95f), mod.Facing(0f, s));
            BallastPump(m, t, mod.P(4.3f, 0f, s * 2.95f), mod.Facing(0f, s));
            Barrel(m, t, mod.P(0.5f, 0f, -s * 3.38f), m.PipeYellow);
            Barrel(m, t, mod.P(1.15f, 0f, -s * 3.4f), m.PipeYellow);
            Crate(m, t, mod.P(5.5f, 0f, -s * 3.38f), mod.Facing(0f, s), new Vector3(0.65f, 0.55f, 0.5f));
            PipeColumn(m, t, mod.P(-6.85f, 0f, 3.35f));
            // Bouche d'évacuation au sol : grille de caoutchouc sur un puisard sombre, flaques autour.
            Vector3 sump = mod.P(-3.6f, 0f, -s * 1.9f);
            Static(Prim(PrimitiveType.Cube, "Puisard", t, sump + Vector3.up * 0.006f, Quaternion.identity, new Vector3(0.9f, 0.012f, 0.6f), m.Frame, false));
            for (int i = 0; i < 6; i++)
                Static(Prim(PrimitiveType.Cube, "Grille", t, sump + new Vector3(-0.36f + i * 0.144f, 0.016f, 0f), Quaternion.identity, new Vector3(0.04f, 0.012f, 0.56f), m.Rubber, false));
            Puddle(m, t, sump + new Vector3(0.6f, 0f, 0.3f), new Vector2(1.1f, 0.7f), 20f);
            Puddle(m, t, mod.P(2.6f, 0f, s * 2.2f), new Vector2(0.9f, 0.6f), -15f);
            // Panneau d'alerte au-dessus du collecteur.
            Transform sign = Group("Alerte_Inondation", t, mod.P(-7.17f, 2.0f, 0f), mod.Facing(-1f, 0f));
            Static(Prim(PrimitiveType.Cube, "Plaque", sign, new Vector3(0f, 0f, 0.005f), Quaternion.identity, new Vector3(0.9f, 0.16f, 0.01f), m.Hazard, false));
            Label(sign, "DANGER · RISQUE D'INONDATION", new Vector3(0f, 0f, -0.006f), 0.36f, null, "DANGER · FLOOD RISK");
            SteamLeak(m, t, mod.P(1.8f, 2.86f, -s * 2.2f));
        }

        /// <summary>Pompe de cale : socle, moteur horizontal à ailettes, volute jaune, conduite de refoulement vers le plafond.</summary>
        static void BallastPump(Mats m, Transform parent, Vector3 floorPosition, Quaternion facing)
        {
            Transform root = Group("Pompe", parent, floorPosition, facing);
            Static(Prim(PrimitiveType.Cube, "Socle", root, new Vector3(0f, 0.08f, 0f), Quaternion.identity, new Vector3(1.3f, 0.16f, 0.7f), m.Frame, false));
            Static(Prim(PrimitiveType.Cube, "Bande", root, new Vector3(0f, 0.12f, -0.352f), Quaternion.identity, new Vector3(1.25f, 0.05f, 0.01f), m.Hazard, false));
            // Moteur : cylindre couché, ailettes de refroidissement, boîte à bornes.
            Static(Prim(PrimitiveType.Cylinder, "Moteur", root, new Vector3(-0.25f, 0.46f, 0f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.48f, 0.34f, 0.48f), m.PipeGrey, false));
            for (int i = 0; i < 6; i++)
                Static(Prim(PrimitiveType.Cylinder, "Ailette", root, new Vector3(-0.52f + i * 0.1f, 0.46f, 0f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.53f, 0.008f, 0.53f), m.Frame, false));
            Static(Prim(PrimitiveType.Cube, "BoiteBornes", root, new Vector3(-0.25f, 0.74f, 0f), Quaternion.identity, new Vector3(0.18f, 0.12f, 0.16f), m.Panel, false));
            // Volute et accouplement.
            Static(Prim(PrimitiveType.Cylinder, "Accouplement", root, new Vector3(0.16f, 0.46f, 0f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.18f, 0.08f, 0.18f), m.Brass, false));
            Static(Prim(PrimitiveType.Cylinder, "Volute", root, new Vector3(0.4f, 0.46f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.52f, 0.14f, 0.52f), m.PipeYellow, false));
            Static(AbyssesArt.MeshObject("Bride_Volute", root, art.Ring, new Vector3(0.4f, 0.46f, -0.145f), Quaternion.identity, new Vector3(0.56f, 0.56f, 0.03f), m.Frame));
            // Refoulement : conduite verticale jusqu'au plafond, vanne à volant.
            Static(Prim(PrimitiveType.Cylinder, "Refoulement", root, new Vector3(0.4f, 1.75f, 0.1f), Quaternion.identity, new Vector3(0.16f, 1.05f, 0.16f), m.PipeYellow, false));
            Static(AbyssesArt.MeshObject("Volant", root, art.Ring, new Vector3(0.4f, 1.3f, -0.03f), Quaternion.identity, new Vector3(0.3f, 0.3f, 0.025f), m.PipeRed));
            Static(Prim(PrimitiveType.Cube, "Rayon", root, new Vector3(0.4f, 1.3f, -0.03f), Quaternion.identity, new Vector3(0.28f, 0.02f, 0.02f), m.PipeRed, false));
            Static(Prim(PrimitiveType.Cube, "Rayon", root, new Vector3(0.4f, 1.3f, -0.03f), Quaternion.identity, new Vector3(0.02f, 0.28f, 0.02f), m.PipeRed, false));
            // Manomètre de refoulement.
            Static(Prim(PrimitiveType.Cylinder, "Manometre", root, new Vector3(0.4f, 1.0f, -0.1f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.12f, 0.02f, 0.12f), m.Frame, false));
            Static(Prim(PrimitiveType.Cylinder, "Cadran", root, new Vector3(0.4f, 1.0f, -0.122f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.1f, 0.002f, 0.1f), m.EmitCold, false));
        }
    }
}
