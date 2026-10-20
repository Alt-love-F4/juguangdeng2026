using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>Explicit, opt-in Windows release build. Never changes project build settings.</summary>
    public static class WindowsReleaseBuild
    {
        [MenuItem("Tools/Free Cafe/Build/Windows Release + Clean Distribution")]
        public static void BuildFromMenu()
        {
            string parent = EditorUtility.OpenFolderPanel("Choose parent folder for a NEW release", "", "");
            if (string.IsNullOrEmpty(parent))
                return;

            string destination = Path.Combine(parent, PlayerFileName() + "-Release-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            if (!EditorUtility.DisplayDialog("Windows Release",
                    "Create a new release folder:\n" + destination +
                    "\n\nBuild/ keeps the original output, including debug backups.\n" +
                    "Distribution/ contains the shippable copy. Existing folders will not be overwritten.\n\n" +
                    "Uses the current Windows x64 scripting backend (" + PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) +
                    ") and enabled scenes, with Development disabled and LZ4HC compression.",
                    "Build", "Cancel"))
                return;

            try
            {
                BuildRelease(destination);
                EditorUtility.RevealInFinder(Path.Combine(destination, "Distribution"));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Windows Release failed", exception.Message + "\n\nSee the Console. Any original build output is retained.", "OK");
            }
        }

        /// <summary>
        /// Builds into a NEW directory containing Build/ and Distribution/. Throws on failure.
        /// Callable by verification tools with a fresh Builds/WindowsRelease-* path.
        /// Does not switch target, change backend/API, modify scenes, or delete prior output.
        /// </summary>
        public static BuildReport BuildRelease(string freshReleaseDirectory)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Exit Play Mode and wait for compilation/building to finish first.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                throw new InvalidOperationException("Select the Windows x64 build target first. This utility does not switch targets.");
            ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            if (backend != ScriptingImplementation.Mono2x && backend != ScriptingImplementation.IL2CPP)
                throw new InvalidOperationException("Select Mono or IL2CPP as the Windows scripting backend. Existing backend/API settings are preserved.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Windows build support is not installed for this Editor.");

            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0 || scenes.Any(scene => AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null))
                throw new InvalidOperationException("All enabled Build Settings scenes must exist, and at least one must be enabled.");

            string root = FreshDirectory(freshReleaseDirectory);
            string buildDirectory = Path.Combine(root, "Build");
            string distributionDirectory = Path.Combine(root, "Distribution");
            Directory.CreateDirectory(buildDirectory);
            string executable = Path.Combine(buildDirectory, PlayerFileName() + ".exe");
            // Do not inherit Development, AllowDebugging, AutoRunPlayer, or profiler options from the Editor UI.
            const BuildOptions options = BuildOptions.None | BuildOptions.CompressWithLz4HC;
            Debug.Log("[Windows Release] Building " + executable + "\nScenes (unchanged): " + string.Join(", ", scenes) +
                      "\nBackend (unchanged): " + backend + "; API (unchanged): " + PlayerSettings.GetApiCompatibilityLevel(NamedBuildTarget.Standalone) +
                      "; options: " + options);

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = executable,
                    target = BuildTarget.StandaloneWindows64,
                    options = options
                });
            }
            catch (Exception exception)
            {
                Debug.LogError("[Windows Release] Build threw; no distribution staged. Partial output retained at " + buildDirectory);
                throw new BuildFailedException(exception);
            }

            if (report == null)
                throw new BuildFailedException("BuildPipeline returned no report. No distribution staged; output retained at " + buildDirectory);
            var summary = report.summary;
            Debug.Log("[Windows Release] Result: " + summary.result + "; reported size: " + summary.totalSize +
                      " bytes; elapsed: " + summary.totalTime + "; errors: " + summary.totalErrors + "; warnings: " + summary.totalWarnings);
            if (summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Windows build " + summary.result + ". No distribution staged; output retained at " + buildDirectory);
            if ((summary.options & BuildOptions.Development) != 0 || (summary.options & BuildOptions.CompressWithLz4HC) == 0)
                throw new BuildFailedException("Reported build options are not a non-Development LZ4HC release. No distribution staged.");
            if (!File.Exists(executable) || !Directory.Exists(Path.Combine(buildDirectory, Path.GetFileNameWithoutExtension(executable) + "_Data")))
                throw new BuildFailedException("Build succeeded but expected executable or player data is missing. No distribution staged.");

            StageDistribution(buildDirectory, distributionDirectory);
            return report;
        }

        /// <summary>
        /// Copies a known, clean Windows player output into a NEW, separate directory.
        /// Only top-level Unity backup/debug folders are excluded; nested runtime content is preserved.
        /// On a copy failure, leaves partial output for inspection and throws (never silently succeeds).
        /// </summary>
        public static void StageDistribution(string buildDirectory, string freshDistributionDirectory)
        {
            string source = Path.GetFullPath(buildDirectory);
            if (!Directory.Exists(source))
                throw new DirectoryNotFoundException(source);
            string destination = FreshDirectory(freshDistributionDirectory);
            if (IsWithin(destination, source) || IsWithin(source, destination))
                throw new ArgumentException("Build and distribution directories must be separate, non-nested folders.");
            RejectLinkedDirectory(source);

            long sourceBytes = DirectoryBytes(source);
            long excludedBytes = 0;
            int excludedDirectories = 0;
            Directory.CreateDirectory(destination);
            try
            {
                foreach (string file in Directory.GetFiles(source))
                    File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), false);
                foreach (string directory in Directory.GetDirectories(source))
                {
                    RejectLinkedDirectory(directory);
                    string name = Path.GetFileName(directory);
                    if (name.EndsWith("_BackUpThisFolder_ButDontShipItWithYourGame", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith("_BurstDebugInformation_DoNotShip", StringComparison.OrdinalIgnoreCase))
                    {
                        excludedBytes += DirectoryBytes(directory);
                        excludedDirectories++;
                        Debug.Log("[Windows Release] Retained in original output only: " + directory);
                        continue;
                    }
                    CopyDirectory(directory, Path.Combine(destination, name));
                }
                long distributionBytes = DirectoryBytes(destination);
                Debug.Log("[Windows Release] Staged " + destination + "\nOriginal on-disk files: " + sourceBytes +
                          " bytes; distribution: " + distributionBytes + " bytes; excluded top-level debug folders: " +
                          excludedDirectories + " (" + excludedBytes + " bytes). Original output remains at " + source +
                          ". BuildReport size and on-disk file totals are different measurements.");
            }
            catch
            {
                Debug.LogError("[Windows Release] Staging failed. Do not ship the partial distribution at " + destination +
                               ". Original build output is untouched at " + source);
                throw;
            }
        }

        private static string FreshDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("An explicit new output directory is required.", nameof(path));
            string fullPath = Path.GetFullPath(path);
            if (Directory.Exists(fullPath) || File.Exists(fullPath))
                throw new IOException("Output already exists; choose a NEW folder. Nothing was overwritten: " + fullPath);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            foreach (string protectedFolder in new[] { "Assets", "Packages", "ProjectSettings", "Library", "Temp" })
                if (IsWithin(fullPath, Path.Combine(projectRoot, protectedFolder)))
                    throw new ArgumentException("Build output must not be placed in " + protectedFolder + ".");
            return fullPath;
        }

        private static bool IsWithin(string path, string parent)
        {
            string normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(normalizedPath, normalizedParent, StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.StartsWith(normalizedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static void RejectLinkedDirectory(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked directories are not supported in release output: " + path);
        }

        private static void CopyDirectory(string source, string destination)
        {
            RejectLinkedDirectory(source);
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), false);
            foreach (string directory in Directory.GetDirectories(source))
                CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }

        private static long DirectoryBytes(string path)
        {
            RejectLinkedDirectory(path);
            long bytes = Directory.GetFiles(path).Sum(file => new FileInfo(file).Length);
            foreach (string directory in Directory.GetDirectories(path))
                bytes += DirectoryBytes(directory);
            return bytes;
        }

        private static string PlayerFileName()
        {
            string name = new string(PlayerSettings.productName.Select(character =>
                Array.IndexOf(Path.GetInvalidFileNameChars(), character) >= 0 ? '_' : character).ToArray()).Trim().TrimEnd('.');
            return string.IsNullOrWhiteSpace(name) ? "Player" : name;
        }
    }
}
