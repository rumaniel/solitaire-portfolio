using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Editor.Tools
{
    /// <summary>
    /// Testing utilities — wipe local save state (persistentDataPath, PlayerPrefs)
    /// so a subsequent play session looks like a fresh install. Editor-only, each
    /// action prompts for confirmation before deleting.
    /// </summary>
    public static class TestingCleanupMenu
    {
        private const string MenuRoot = "Solitaire/Testing/";

        [MenuItem(MenuRoot + "Clear PersistentDataPath", priority = 100)]
        private static void ClearPersistentDataPath()
        {
            var path = Application.persistentDataPath;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                EditorUtility.DisplayDialog("Persistent Data", $"Nothing to clear at:\n{path}", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Clear PersistentDataPath",
                    $"Delete all files/folders in:\n{path}\n\nThis removes snapshots, daily stats, and lifetime stats for every profile. Continue?",
                    "Delete", "Cancel"))
                return;

            int files = 0, dirs = 0, errors = 0;
            foreach (var file in Directory.GetFiles(path))
            {
                try { File.Delete(file); files++; }
                catch (Exception e) { Debug.LogWarning($"[Testing] Could not delete '{file}': {e.Message}"); errors++; }
            }
            foreach (var dir in Directory.GetDirectories(path))
            {
                try { Directory.Delete(dir, recursive: true); dirs++; }
                catch (Exception e) { Debug.LogWarning($"[Testing] Could not delete '{dir}': {e.Message}"); errors++; }
            }

            Debug.Log($"[Testing] Cleared persistentDataPath: {files} file(s), {dirs} folder(s), {errors} error(s).");
        }

        [MenuItem(MenuRoot + "Clear PlayerPrefs", priority = 101)]
        private static void ClearPlayerPrefs()
        {
            if (!EditorUtility.DisplayDialog(
                    "Clear PlayerPrefs",
                    "Delete ALL PlayerPrefs keys for this Unity project?\n\nThis also clears the locally-persisted user id used by LocalAuthGateway.",
                    "Delete", "Cancel"))
                return;

            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[Testing] Cleared PlayerPrefs.");
        }

        [MenuItem(MenuRoot + "Clear Everything (PersistentDataPath + PlayerPrefs)", priority = 120)]
        private static void ClearAll()
        {
            if (!EditorUtility.DisplayDialog(
                    "Clear Everything",
                    "Delete persistentDataPath AND PlayerPrefs?\n\nThis simulates a fresh install on next play.",
                    "Delete Both", "Cancel"))
                return;

            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            var path = Application.persistentDataPath;
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.GetFiles(path))
                {
                    try { File.Delete(file); } catch (Exception e) { Debug.LogWarning($"[Testing] {e.Message}"); }
                }
                foreach (var dir in Directory.GetDirectories(path))
                {
                    try { Directory.Delete(dir, recursive: true); } catch (Exception e) { Debug.LogWarning($"[Testing] {e.Message}"); }
                }
            }

            Debug.Log("[Testing] Cleared PlayerPrefs + persistentDataPath.");
        }

        [MenuItem(MenuRoot + "Reveal PersistentDataPath in Explorer", priority = 200)]
        private static void RevealPersistentDataPath()
        {
            var path = Application.persistentDataPath;
            if (!Directory.Exists(path))
            {
                Debug.LogWarning($"[Testing] persistentDataPath does not exist: {path}");
                return;
            }
            EditorUtility.RevealInFinder(path);
        }
    }
}
