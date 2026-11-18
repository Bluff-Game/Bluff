using System;
using System.IO;
using System.Linq;
using UnityEditor;

namespace Bluff.EditorTools
{
    /// <summary>
    /// Headless build entry point used by the versioned GitHub Actions release workflow.
    /// </summary>
    public static class BuildCommand
    {
        const string DefaultBuildPath = "Builds/Bluff.exe";

        public static void BuildWindows()
        {
            var version = GetArgument("-buildVersion")
                ?? Environment.GetEnvironmentVariable("BLUFF_VERSION")
                ?? PlayerSettings.bundleVersion;
            var buildPath = GetArgument("-buildPath") ?? DefaultBuildPath;

            if (!Version.TryParse(version, out _))
                throw new InvalidOperationException($"Build version '{version}' is not a valid semantic version.");

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled scenes are registered in Build Settings.");

            var fullBuildPath = Path.GetFullPath(buildPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullBuildPath));

            PlayerSettings.productName = "Bluff";
            PlayerSettings.bundleVersion = version;
            AssetDatabase.SaveAssets();

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = fullBuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });

            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException($"Windows build failed with result {report.summary.result}.");

            UnityEngine.Debug.Log($"[Bluff] Built version {version} at {fullBuildPath}.");
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
