using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Editor.Tools
{
    /// <summary>
    /// Generates character-set .txt files for use with TextMeshPro Font Asset Creator
    /// ("Characters from File"). Aggregates:
    ///   (1) StringTable entries (per locale)
    ///   (2) Hardcoded TMP_Text literals in prefabs + scenes (catches i18n gaps like icon glyphs)
    ///   (3) Common ASCII + punctuation/symbols
    ///   (4) Locale script ranges (Jamo for ko, Kana for ja, Arabic block, ...)
    ///
    /// To add a new locale's script range: append to <see cref="LocaleScriptRanges"/>. No other change needed.
    /// </summary>
    public static class FontCharSetGenerator
    {
        private const string OutputDir = "Assets/Fonts";
        private const string MenuRoot = "Solitaire/Localization/Font Char Set/";

        /// <summary>Included for every locale and every run.</summary>
        private static readonly (int lo, int hi, string name)[] CommonRanges =
        {
            (0x0020, 0x007E, "ASCII printable"),
            (0x00A0, 0x00FF, "Latin-1 Supplement (middle dot, copyright, currency, accented Latin)"),
            (0x2008, 0x26AC, "Punctuation / Math / Symbols / Arrows / Card Suits"),
        };

        /// <summary>Per-locale script/alphabet ranges added on top of scanned glyphs.</summary>
        /// <remarks>
        /// For CJK ideograph locales the scanner picks up exactly the characters used in
        /// translations — no need to bake the entire 20k+ ideograph block.
        /// </remarks>
        private static readonly Dictionary<string, (int lo, int hi, string name)[]> LocaleScriptRanges = new()
        {
            { "en", System.Array.Empty<(int, int, string)>() },
            { "ko", new[] {
                (0x3131, 0x3163, "Hangul Compatibility Jamo (basic)"),
            }},
            { "ja", new[] {
                (0x3041, 0x3096, "Hiragana"),
                (0x309A, 0x309F, "Hiragana extensions"),
                (0x30A0, 0x30FF, "Katakana"),
            }},
            { "zh-Hans", new[] { (0x3000, 0x303F, "CJK Symbols and Punctuation") } },
            { "zh-Hant", new[] { (0x3000, 0x303F, "CJK Symbols and Punctuation") } },
            { "th",       new[] { (0x0E00, 0x0E7F, "Thai") } },
            { "vi",       new[] {
                (0x0100, 0x017F, "Latin Extended-A"),
                (0x1EA0, 0x1EFF, "Latin Extended Additional (Vietnamese)"),
            }},
            { "ar",       new[] {
                (0x0600, 0x06FF, "Arabic"),
                (0xFB50, 0xFDFF, "Arabic Presentation Forms-A"),
                (0xFE70, 0xFEFF, "Arabic Presentation Forms-B"),
            }},
            { "he",       new[] { (0x0590, 0x05FF, "Hebrew") } },
        };

        [MenuItem(MenuRoot + "Generate Merged (All Locales)", priority = 100)]
        public static void GenerateMerged()
        {
            var locales = DiscoverLocaleCodes();
            if (locales.Count == 0) { Report("No StringTable assets found under Assets/."); return; }

            var merged = new SortedSet<int>();
            var perLocale = new List<string>();
            foreach (var locale in locales)
            {
                var set = ScanStringTable(locale, out int entries);
                int scriptAdded = AddLocaleScriptRanges(set, locale);
                foreach (var cp in set) merged.Add(cp);
                perLocale.Add($"  {locale}: {entries} entries, {set.Count} chars (scan + {scriptAdded} script range)");
            }

            var hardcoded = CollectHardcodedTmpChars(out int componentCount, out int skippedScenes);
            foreach (var cp in hardcoded) merged.Add(cp);

            var sourceChars = ScanCSharpSourceNonAscii(out int fileCount);
            foreach (var cp in sourceChars) merged.Add(cp);

            AddRanges(merged, CommonRanges);

            var outPath = Path.Combine(OutputDir, "font-chars-all.txt").Replace('\\', '/');
            WriteCharFile(outPath, merged);
            AssetDatabase.Refresh();

            var msg = new StringBuilder();
            msg.AppendLine($"Wrote {outPath}");
            msg.AppendLine($"Total unique chars: {merged.Count}");
            msg.AppendLine();
            msg.AppendLine("Per locale:");
            foreach (var s in perLocale) msg.AppendLine(s);
            msg.AppendLine();
            msg.AppendLine($"Hardcoded TMP_Text chars: {hardcoded.Count} (from {componentCount} components)");
            msg.AppendLine($"C# source non-ASCII chars: {sourceChars.Count} (from {fileCount} .cs files)");
            if (skippedScenes > 0) msg.AppendLine($"(Skipped {skippedScenes} scenes — save dirty scene first to include.)");
            Report(msg.ToString());
        }

        [MenuItem(MenuRoot + "Generate Per Locale", priority = 101)]
        public static void GeneratePerLocale()
        {
            var locales = DiscoverLocaleCodes();
            if (locales.Count == 0) { Report("No StringTable assets found under Assets/."); return; }

            var hardcoded = CollectHardcodedTmpChars(out _, out int skippedScenes);
            var sourceChars = ScanCSharpSourceNonAscii(out _);

            var summary = new StringBuilder();
            foreach (var locale in locales)
            {
                var set = ScanStringTable(locale, out int entries);
                AddLocaleScriptRanges(set, locale);
                foreach (var cp in hardcoded) set.Add(cp);
                foreach (var cp in sourceChars) set.Add(cp);
                AddRanges(set, CommonRanges);

                var outPath = Path.Combine(OutputDir, $"font-chars-{locale}.txt").Replace('\\', '/');
                WriteCharFile(outPath, set);
                summary.AppendLine($"  {outPath}: {set.Count} chars ({entries} entries)");
            }
            AssetDatabase.Refresh();

            var msg = "Per-locale files:\n" + summary;
            if (skippedScenes > 0) msg += $"\n(Skipped {skippedScenes} scenes — save dirty scene first to include.)";
            Report(msg);
        }

        [MenuItem(MenuRoot + "Scan Report (no write)", priority = 102)]
        public static void ScanReport()
        {
            var sb = new StringBuilder();
            var locales = DiscoverLocaleCodes();
            sb.AppendLine($"Locales found: {locales.Count}");
            foreach (var locale in locales)
            {
                var set = ScanStringTable(locale, out int entries);
                sb.AppendLine($"  {locale}: {entries} entries, {set.Count} non-ASCII chars");
            }
            var hardcoded = CollectHardcodedTmpChars(out int componentCount, out int skippedScenes);
            sb.AppendLine();
            sb.AppendLine($"Hardcoded TMP_Text non-ASCII chars: {hardcoded.Count} across {componentCount} components");
            if (skippedScenes > 0) sb.AppendLine($"(Skipped {skippedScenes} scenes — save dirty scene first.)");
            if (hardcoded.Count > 0 && hardcoded.Count <= 50)
            {
                sb.Append("  codepoints: ");
                foreach (var cp in hardcoded) sb.Append($"U+{cp:X4}({char.ConvertFromUtf32(cp)}) ");
                sb.AppendLine();
            }

            var sourceChars = ScanCSharpSourceNonAscii(out int fileCount);
            sb.AppendLine();
            sb.AppendLine($"C# source non-ASCII chars: {sourceChars.Count} across {fileCount} .cs files");
            if (sourceChars.Count > 0 && sourceChars.Count <= 50)
            {
                sb.Append("  codepoints: ");
                foreach (var cp in sourceChars) sb.Append($"U+{cp:X4}({char.ConvertFromUtf32(cp)}) ");
                sb.AppendLine();
            }
            Report(sb.ToString());
        }

        [MenuItem(MenuRoot + "Audit Hardcoded TMP_Text (console)", priority = 200)]
        public static void AuditHardcoded()
        {
            var items = CollectHardcodedTmpItems(out int skippedScenes);
            // Filter to items containing any non-ASCII char (those are candidates for i18n).
            var nonAscii = items.Where(x => x.value.Any(c => c > 0x7E)).ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"[FontCharSetGenerator] Hardcoded TMP_Text audit");
            sb.AppendLine($"Total hardcoded components: {items.Count}");
            sb.AppendLine($"Containing non-ASCII (i18n candidates): {nonAscii.Count}");
            if (skippedScenes > 0) sb.AppendLine($"(Skipped {skippedScenes} scenes — save dirty scene first.)");
            sb.AppendLine();
            foreach (var (source, obj, value) in nonAscii.Take(100))
                sb.AppendLine($"  [{source}] {obj}: \"{value}\"");
            if (nonAscii.Count > 100) sb.AppendLine($"  ... +{nonAscii.Count - 100} more");
            Debug.Log(sb.ToString());
        }

        // ---------- scanning helpers ----------

        private static List<string> DiscoverLocaleCodes()
        {
            var set = new SortedSet<string>();
            foreach (var g in AssetDatabase.FindAssets("t:StringTable"))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (!p.StartsWith("Assets/")) continue;
                var tbl = AssetDatabase.LoadAssetAtPath<StringTable>(p);
                if (tbl == null) continue;
                var code = tbl.LocaleIdentifier.Code;
                if (!string.IsNullOrEmpty(code)) set.Add(code);
            }
            return set.ToList();
        }

        private static SortedSet<int> ScanStringTable(string locale, out int entryCount)
        {
            var used = new SortedSet<int>();
            entryCount = 0;
            foreach (var g in AssetDatabase.FindAssets("t:StringTable"))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (!p.StartsWith("Assets/")) continue;
                var tbl = AssetDatabase.LoadAssetAtPath<StringTable>(p);
                if (tbl == null || tbl.LocaleIdentifier.Code != locale) continue;
                foreach (var entry in tbl.Values)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.Value)) continue;
                    entryCount++;
                    AddPrintableChars(entry.Value, used);
                }
            }
            return used;
        }

        private static SortedSet<int> CollectHardcodedTmpChars(out int componentCount, out int skippedScenes)
        {
            var items = CollectHardcodedTmpItems(out skippedScenes);
            componentCount = items.Count;
            var set = new SortedSet<int>();
            foreach (var (_, _, value) in items) AddPrintableChars(value, set);
            return set;
        }

        /// <summary>Scan every .cs file under Assets/Scripts/ for non-ASCII chars (string literals + comments).</summary>
        /// <remarks>Catches format strings like <c>$"Continue · {time}"</c> whose glyphs never appear in prefab/scene defaults.</remarks>
        private static SortedSet<int> ScanCSharpSourceNonAscii(out int fileCount)
        {
            var set = new SortedSet<int>();
            fileCount = 0;
            const string scriptsRoot = "Assets/Scripts";
            if (!Directory.Exists(scriptsRoot)) return set;

            foreach (var path in Directory.EnumerateFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                fileCount++;
                try
                {
                    foreach (var c in File.ReadAllText(path))
                    {
                        if (c < 0x80) continue;                  // ASCII covered by CommonRanges
                        if (char.IsControl(c)) continue;
                        if (char.IsSurrogate(c)) continue;       // astral-plane pairs: ignore, TMP rarely needs emoji
                        set.Add(c);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[FontCharSetGenerator] Cannot read {path}: {e.Message}");
                }
            }
            return set;
        }

        private static List<(string source, string obj, string value)> CollectHardcodedTmpItems(out int skippedScenes)
        {
            skippedScenes = 0;
            var items = new List<(string, string, string)>();

            foreach (var g in AssetDatabase.FindAssets("t:Prefab"))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (!p.StartsWith("Assets/")) continue;
                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(p);
                    foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (string.IsNullOrEmpty(tmp.text)) continue;
                        items.Add((p, tmp.name, tmp.text));
                    }
                }
                finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
            }

            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.isDirty)
            {
                Debug.LogWarning("[FontCharSetGenerator] Active scene is dirty — scene scan skipped. Save first to include scenes.");
                skippedScenes = AssetDatabase.FindAssets("t:Scene").Length;
                return items;
            }

            var originalScenePath = activeScene.path;
            foreach (var g in AssetDatabase.FindAssets("t:Scene"))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (!p.StartsWith("Assets/")) continue;
                try
                {
                    var scene = EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                    foreach (var go in scene.GetRootGameObjects())
                    {
                        foreach (var tmp in go.GetComponentsInChildren<TMP_Text>(true))
                        {
                            if (string.IsNullOrEmpty(tmp.text)) continue;
                            items.Add((p, tmp.name, tmp.text));
                        }
                    }
                }
                catch (System.Exception e) { Debug.LogWarning($"[FontCharSetGenerator] Scene error {p}: {e.Message}"); }
            }
            if (!string.IsNullOrEmpty(originalScenePath))
            {
                try { EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single); } catch { }
            }
            return items;
        }

        // ---------- char-set mutators ----------

        private static void AddPrintableChars(string s, SortedSet<int> target)
        {
            if (string.IsNullOrEmpty(s)) return;
            foreach (var c in s)
            {
                if (c < 0x20 || c == 0x7F) continue;
                if (c <= 0x7E) continue; // ASCII always covered by CommonRanges
                target.Add(c);
            }
        }

        private static int AddLocaleScriptRanges(SortedSet<int> set, string locale)
        {
            if (!LocaleScriptRanges.TryGetValue(locale, out var ranges)) return 0;
            int before = set.Count;
            foreach (var (lo, hi, _) in ranges)
                for (int i = lo; i <= hi; i++) set.Add(i);
            return set.Count - before;
        }

        private static void AddRanges(SortedSet<int> set, (int lo, int hi, string name)[] ranges)
        {
            foreach (var (lo, hi, _) in ranges)
                for (int i = lo; i <= hi; i++) set.Add(i);
        }

        private static void WriteCharFile(string path, SortedSet<int> codepoints)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var sb = new StringBuilder(codepoints.Count);
            foreach (var cp in codepoints) sb.Append(char.ConvertFromUtf32(cp));

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static void Report(string msg)
        {
            Debug.Log("[FontCharSetGenerator]\n" + msg);
            EditorUtility.DisplayDialog("Font Char Set Generator", msg, "OK");
        }
    }
}
