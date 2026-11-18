using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Bluff.EditorTools
{
    /// <summary>
    /// Headless build entry point used by the versioned GitHub Actions release workflow.
    /// </summary>
    public static class BuildCommand
    {
        const string DefaultBuildPath = "Builds/StandaloneWindows64/Bluff.exe";

        public static void BuildWindows()
        {
            var version = GetArgument("-buildVersion")
                ?? Environment.GetEnvironmentVariable("BLUFF_VERSION");
            var buildPath = GetArgument("-customBuildPath") ?? GetArgument("-buildPath") ?? DefaultBuildPath;

            if (version == null || !Regex.IsMatch(version, @"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z")
                || !Version.TryParse(version, out var parsed)
                || parsed.Major > 65535 || parsed.Minor > 65535 || parsed.Build > 65535)
                throw new BuildFailedException("Pass -buildVersion MAJOR.MINOR.PATCH (each component 0-65535, no leading zeroes).");

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new BuildFailedException("No enabled scenes are registered in Build Settings.");
            if (scenes[0] != "Assets/Bluff/Scenes/MainMenu.unity" || scenes.Any(scene => !File.Exists(scene)))
                throw new BuildFailedException("Enabled scenes must exist and MainMenu must be first in Build Settings.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new BuildFailedException("Install Windows Build Support for this Unity editor version.");

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var fullBuildPath = Path.GetFullPath(Path.Combine(projectRoot, buildPath));
            if (!string.Equals(Path.GetExtension(fullBuildPath), ".exe", StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("The Windows build path must end in .exe.");
            Directory.CreateDirectory(Path.GetDirectoryName(fullBuildPath));

            var previousVersion = PlayerSettings.bundleVersion;
            var previousBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            try
            {
                PlayerSettings.bundleVersion = version;
                // Mono allows the Linux CI editor to cross-build the Windows player.
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = fullBuildPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                });
                if (report.summary.result != BuildResult.Succeeded || !File.Exists(fullBuildPath))
                    throw new BuildFailedException($"Windows build failed: {report.summary.result} ({report.summary.totalErrors} errors).");

                var info = new BuildInfo
                {
                    version = version,
                    commit = GetArgument("-commitSha") ?? "local",
                    unityVersion = Application.unityVersion,
                    target = BuildTarget.StandaloneWindows64.ToString(),
                    scenes = scenes,
                };
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(fullBuildPath), "build-info.json"), JsonUtility.ToJson(info, true));
                Debug.Log($"[Bluff] Built version {version} at {fullBuildPath}.");
            }
            finally
            {
                PlayerSettings.bundleVersion = previousVersion;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, previousBackend);
                AssetDatabase.SaveAssets();
            }
        }

        [Serializable]
        sealed class BuildInfo
        {
            public string version;
            public string commit;
            public string unityVersion;
            public string target;
            public string[] scenes;
        }

        static string GetArgument(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var i = 0; i < arguments.Length - 1; i++)
            {
                if (string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase))
                    return arguments[i + 1];
            }

            return null;
        }
    }
}
