using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Builds Quest 2 (Android, APK) :
    /// - debug : déplacement au joystick autorisé, development build (profilage à distance, logs) ;
    /// - EVA : define <c>ABYSSES_PROD</c> → déplacement au joystick coupé (consigne C3), build optimisé.
    /// Les APK sont écrits dans <c>Projet/Builds/</c> (ignoré par git) ; les defines du projet sont restaurés après le build.
    /// </summary>
    public static class AbyssesBuild
    {
        const string ProdDefine = "ABYSSES_PROD";
        const string OutputDir = "Builds";

        [MenuItem("Abysses/Build/APK debug (joystick, profilage)", priority = 40)]
        public static void BuildDebug() => Build(false);

        [MenuItem("Abysses/Build/APK EVA (production, joystick coupé)", priority = 41)]
        public static void BuildEva() => Build(true);

        public static BuildReport Build(bool production)
        {
            NamedBuildTarget target = NamedBuildTarget.Android;
            string previousDefines = PlayerSettings.GetScriptingDefineSymbols(target);
            var defines = previousDefines.Split(';').Where(d => d.Length > 0 && d != ProdDefine).ToList();
            if (production) defines.Add(ProdDefine);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));

            try
            {
                Directory.CreateDirectory(OutputDir);
                var options = new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    locationPathName = Path.Combine(OutputDir, production ? "Abysses_EVA.apk" : "Abysses_debug.apk"),
                    options = production ? BuildOptions.None : BuildOptions.Development,
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;
                Debug.Log($"[Abysses] Build {(production ? "EVA" : "debug")} : {summary.result}, " +
                          $"{summary.totalSize / (1024f * 1024f):F1} Mo, {summary.totalTime.TotalSeconds:F0} s, " +
                          $"{summary.totalErrors} erreur(s), {summary.totalWarnings} avertissement(s) → {options.locationPathName}");
                return report;
            }
            finally
            {
                PlayerSettings.SetScriptingDefineSymbols(target, previousDefines);
            }
        }
    }
}
