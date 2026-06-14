using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Editor.Tools
{
    /// <summary>Headless build entry points invoked via Unity's -executeMethod.</summary>
    public static class CIBuilder
    {
        private const string OutputRoot = "build";

        public static void BuildAndroid()
        {
            ConfigureAndroidSigning();
            EditorUserBuildSettings.buildAppBundle = true;
            BuildPlayer(BuildTarget.Android, Path.Combine(OutputRoot, "Android", "solitaire.aab"));
        }

        public static void BuildAndroidRelease()
        {
            OverrideAndroidVersionCode();
            ConfigureAndroidSigning();
            EditorUserBuildSettings.buildAppBundle = true;
            BuildPlayer(BuildTarget.Android, Path.Combine(OutputRoot, "Android", "solitaire.aab"));
        }

        public static void BuildWebGL()
        {
            BuildPlayer(BuildTarget.WebGL, Path.Combine(OutputRoot, "WebGL"));
        }

        private static void OverrideAndroidVersionCode()
        {
            var raw = Environment.GetEnvironmentVariable("ANDROID_VERSION_CODE");
            if (!int.TryParse(raw, out var code) || code <= 0)
            {
                Debug.LogError($"[CI] ANDROID_VERSION_CODE missing or invalid: '{raw}'");
                EditorApplication.Exit(2);
                return;
            }
            var current = PlayerSettings.Android.bundleVersionCode;
            PlayerSettings.Android.bundleVersionCode = code;
            AssetDatabase.SaveAssets();
            Debug.Log($"[CI] Android versionCode: {current} -> {code}");
        }

        // PlayerSettings persists keystore path/alias but not passwords; without
        // env-injected passwords we fall back to the auto-generated debug keystore.
        private static void ConfigureAndroidSigning()
        {
            var keystorePath = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PATH");
            var keystorePass = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASSWORD");
            var keyAlias = Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_NAME");
            var keyAliasPass = Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_PASSWORD");

            if (string.IsNullOrEmpty(keystorePath) || string.IsNullOrEmpty(keystorePass)
                || string.IsNullOrEmpty(keyAlias) || string.IsNullOrEmpty(keyAliasPass))
            {
                Debug.Log("[CI] Android signing env not set; using debug keystore.");
                PlayerSettings.Android.useCustomKeystore = false;
                return;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystorePath;
            PlayerSettings.Android.keystorePass = keystorePass;
            PlayerSettings.Android.keyaliasName = keyAlias;
            PlayerSettings.Android.keyaliasPass = keyAliasPass;
            Debug.Log($"[CI] Android signing: keystore={keystorePath} alias={keyAlias}");
        }

        private static void BuildPlayer(BuildTarget target, string outputPath)
        {
            // Reconcile asset DB with disk after NuGet restore retouches files in
            // Assets/Packages/, otherwise BuildPipeline aborts with "Build asset
            // version error".
            AssetDatabase.Refresh(ImportAssetOptions.Default);
            AssetDatabase.SaveAssets();

            var scenes = EnabledScenePaths();
            if (scenes.Length == 0)
            {
                Debug.LogError($"[CI] No enabled scenes; aborting {target} build.");
                EditorApplication.Exit(2);
                return;
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.None,
            });

            var s = report.summary;
            Debug.Log($"[CI] {target} build {s.result}: size={s.totalSize}B duration={s.totalTime.TotalSeconds:F1}s errors={s.totalErrors} warnings={s.totalWarnings}");
            EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        private static string[] EnabledScenePaths()
        {
            var list = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled) list.Add(scene.path);
            return list.ToArray();
        }
    }
}
