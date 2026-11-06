using System;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEditor;
using UnityEngine;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Diagnostic du mode « en ligne » (menu Abysses) : services Unity joignables ? connexion anonyme ? Relay activé ?
    /// sessions (Lobby) activées ? Résultat dans la console et dans docs/diagnostic-en-ligne.md.
    /// En batchmode : -executeMethod Abysses.EditorTools.AbyssesNetworkCheck.RunAndExit
    /// </summary>
    public static class AbyssesNetworkCheck
    {
        [MenuItem("Abysses/Tests/Diagnostic du mode en ligne (Relay, sessions)", priority = 83)]
        public static async void Run() => await Check();

        const string PendingKey = "Abysses.NetworkCheck.Pending";

        /// <summary>Batchmode : passe en Play Mode (les services Unity ne s'initialisent qu'en jeu), diagnostique, quitte.</summary>
        public static void RunAndExit()
        {
            UnityEditor.SessionState.SetBool(PendingKey, true);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        static void ResumeAfterReload()
        {
            if (!UnityEditor.SessionState.GetBool(PendingKey, false)) return;
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (EditorApplication.isPlaying) Start();
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode) Start();
        }

        static void Start()
        {
            if (!UnityEditor.SessionState.GetBool(PendingKey, false)) return;
            UnityEditor.SessionState.SetBool(PendingKey, false);
            Task<string> task = Check();
            EditorApplication.update += Wait;
            void Wait()
            {
                if (!task.IsCompleted) return;
                EditorApplication.update -= Wait;
                EditorApplication.Exit(0);
            }
        }

        static async Task<string> Check()
        {
            var report = new StringBuilder("# Diagnostic du mode en ligne\n\n");
            report.AppendLine($"> {DateTime.Now:yyyy-MM-dd HH:mm} · projet cloud `{Application.cloudProjectId}`\n");
            async Task<bool> Step(string label, Func<Task> action)
            {
                try
                {
                    await action();
                    report.AppendLine("- ✅ " + label);
                    return true;
                }
                catch (Exception e)
                {
                    report.AppendLine("- ❌ " + label + " — `" + e.GetType().Name + ": " + e.Message.Replace("\n", " ") + "`");
                    return false;
                }
            }

            bool ok = await Step("Initialisation des services Unity", () => UnityServices.InitializeAsync());
            if (ok && !AuthenticationService.Instance.IsSignedIn)
                ok = await Step("Connexion anonyme (Authentication)", () => AuthenticationService.Instance.SignInAnonymouslyAsync());
            if (ok)
            {
                ISession session = null;
                bool lobby = await Step("Session sans réseau (service Lobby)", async () =>
                    session = await MultiplayerService.Instance.CreateSessionAsync(new SessionOptions { MaxPlayers = 2, IsPrivate = true }));
                if (session != null) await Step("Fermeture de la session de test", () => session.AsHost().DeleteAsync());
                if (lobby)
                    await Step("Allocation Relay (service Relay)", () => Unity.Services.Relay.RelayService.Instance.CreateAllocationAsync(2));
            }
            string text = report.ToString();
            System.IO.File.WriteAllText("../docs/diagnostic-en-ligne.md", text);
            Debug.Log("[Abysses] " + text);
            return text;
        }
    }
}
