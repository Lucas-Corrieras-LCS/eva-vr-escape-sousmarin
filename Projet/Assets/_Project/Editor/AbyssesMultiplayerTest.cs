using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Test multijoueur dans l'éditeur (Multiplayer Play Mode 2.0) :
    /// un scénario « 2 joueurs » où l'éditeur principal porte le tag Host et un éditeur virtuel le tag Client.
    /// Au Play, <see cref="Abysses.Network.DevAutoConnect"/> fait héberger l'un et rejoindre l'autre automatiquement.
    ///
    /// MPPM 2.0 n'expose pas encore d'API publique pour créer un scénario : on passe par la réflexion.
    /// En cas d'échec après une mise à jour d'Unity, créer le scénario à la main :
    /// Window → Multiplayer → Play Mode Scenarios → éditeur principal tag « Host » + 1 éditeur additionnel tag « Client ».
    /// </summary>
    public static class AbyssesMultiplayerTest
    {
        const string ScenarioPath = "Assets/_Project/Editor/MPPM_2Joueurs.asset";
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [MenuItem("Abysses/Test multijoueur/Activer le scénario 2 joueurs (Host + Client)", priority = 60)]
        public static void EnableTwoPlayers()
        {
            AssetDatabase.DeleteAsset(ScenarioPath);
            ScriptableObject scenario = CreateScenario();
            SetActiveConfig(scenario);
            Debug.Log("[Abysses] Scénario multijoueur actif : au Play, l'éditeur héberge et un second éditeur rejoint (premier lancement : ~1 min).");
        }

        [MenuItem("Abysses/Test multijoueur/Revenir au Play normal (solo)", priority = 61)]
        public static void DisableScenario()
        {
            SetActiveConfig(null);
            Debug.Log("[Abysses] Play normal rétabli (un seul éditeur, connexion manuelle via le menu).");
        }

        /// <summary>Un tag MPPM doit être déclaré dans le projet (ProjectSettings/VirtualProjectsConfig.json) avant d'être attribué.</summary>
        static void EnsureTagsDeclared(Assembly module)
        {
            Type playmode = module.GetType("Unity.Multiplayer.PlayMode.Editor.MultiplayerPlaymode", true);
            object tags = playmode.GetProperty("PlayerTags", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
            Type tagsType = tags.GetType();
            MethodInfo contains = tagsType.GetMethod("Contains", Any);
            MethodInfo add = tagsType.GetMethod("Add", Any);
            foreach (string tag in new[] { Abysses.Network.DevAutoConnect.HostTag, Abysses.Network.DevAutoConnect.ClientTag })
            {
                if ((bool)contains.Invoke(tags, new object[] { tag })) continue;
                var args = new object[] { tag, null };
                if (!(bool)add.Invoke(tags, args)) Debug.LogWarning("[Abysses] Tag MPPM « " + tag + " » non ajouté : " + args[1]);
            }
        }

        static ScriptableObject CreateScenario()
        {
            Assembly module = FindAssembly("UnityEditor.MultiplayerModule");
            EnsureTagsDeclared(module);
            Type configType = module.GetType("Unity.Multiplayer.PlayMode.Editor.ScenarioConfig", true);
            Type mainType = module.GetType("Unity.Multiplayer.PlayMode.Editor.MainEditorInstanceDescription", true);
            Type virtualType = module.GetType("Unity.Multiplayer.PlayMode.Editor.VirtualEditorInstanceDescription", true);

            var scenario = ScriptableObject.CreateInstance(configType);
            scenario.name = "Abysses — 2 joueurs";
            configType.GetField("m_EnableEditors", Any).SetValue(scenario, true);

            object main = Activator.CreateInstance(mainType, true);
            Describe(main, "Player 1", Abysses.Network.DevAutoConnect.HostTag);
            configType.GetField("m_MainEditorInstance", Any).SetValue(scenario, main);

            object client = Activator.CreateInstance(virtualType, true);
            Describe(client, "Player 2", Abysses.Network.DevAutoConnect.ClientTag);
            FieldInfo advanced = virtualType.GetField("m_AdvancedConfiguration", Any);
            if (advanced != null && advanced.GetValue(client) == null)
                advanced.SetValue(client, Activator.CreateInstance(advanced.FieldType, true));
            var editors = (IList)configType.GetField("m_EditorInstances", Any).GetValue(scenario);
            editors.Add(client);

            AssetDatabase.CreateAsset(scenario, ScenarioPath);
            AssetDatabase.SaveAssets();
            return scenario;
        }

        static void Describe(object instance, string name, string tag)
        {
            Type type = instance.GetType();
            FieldInfo nameField = null, tagField = null;
            for (Type t = type; t != null; t = t.BaseType)
            {
                nameField ??= t.GetField("Name", Any);
                tagField ??= t.GetField("m_PlayerTag", Any);
            }
            nameField?.SetValue(instance, name);
            tagField?.SetValue(instance, tag);
        }

        static void SetActiveConfig(ScriptableObject config)
        {
            Type managerType = FindAssembly("UnityEditor.PlayModeModule").GetType("Unity.PlayMode.Editor.PlayModeManager", true);
            object manager = managerType.GetProperty("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy).GetValue(null);
            managerType.GetProperty("ActivePlayModeConfig", Any).SetValue(manager, config);
        }

        static Assembly FindAssembly(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetName().Name == name) return assembly;
            throw new InvalidOperationException("Assembly introuvable : " + name);
        }
    }
}
