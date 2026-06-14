using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Data.Audio
{
    [CreateAssetMenu(fileName = "AudioDatabase", menuName = "Solitaire/Audio/Audio Database")]
    public class AudioDatabaseAsset : ScriptableObject
    {
        [SerializeField] private List<AudioEntry> entries = new List<AudioEntry>();

        private Dictionary<string, AudioEntry> lookup;

        public AudioEntry Get(string key)
        {
            EnsureLookup();
            return lookup.TryGetValue(key, out var entry) ? entry : null;
        }

        private void OnEnable()
        {
            BuildLookup();
        }

        private void OnValidate()
        {
            BuildLookup();
            ValidateDuplicates();
            ValidateEntries();
            ValidateCatalogCoverage();
        }

        private void EnsureLookup()
        {
            if (lookup == null)
                BuildLookup();
        }

        private void BuildLookup()
        {
            lookup = new Dictionary<string, AudioEntry>();
            if (entries == null) return;

            foreach (var entry in entries)
            {
                if (entry == null) continue;
                var key = entry.ClipReference.Key;
                if (string.IsNullOrEmpty(key)) continue;
                lookup[key] = entry;
            }
        }

        private void ValidateDuplicates()
        {
            if (entries == null || entries.Count == 0) return;

            var seen = new HashSet<string>();
            foreach (var entry in entries)
            {
                if (entry == null) continue;
                var key = entry.ClipReference.Key;
                if (string.IsNullOrEmpty(key)) continue;
                if (!seen.Add(key))
                    Debug.LogWarning($"Duplicate audio entry key detected: \"{key}\"", this);
            }
        }

        private void ValidateEntries()
        {
            if (entries == null || entries.Count == 0) return;

            foreach (var entry in entries)
            {
                if (entry == null) continue;
                var key = entry.ClipReference.Key;
                if (string.IsNullOrEmpty(key)) continue;

                switch (entry.AudioType)
                {
                    case AudioType.Sfx:
                        if (entry.DuplicatePolicy != DuplicatePlayPolicy.All && entry.MaxInstances <= 0)
                            Debug.LogWarning($"[{key}] SFX entry has MaxInstances <= 0.", this);
                        if (entry.Loop)
                            Debug.LogWarning($"[{key}] SFX entry has Loop enabled — ignored at playback.", this);
                        break;
                    case AudioType.Music:
                        if (entry.DuplicatePolicy != DuplicatePlayPolicy.All)
                            Debug.LogWarning($"[{key}] Music entry has DuplicatePolicy set — ignored by music player.", this);
                        break;
                }
            }
        }

        // Reflection: surface drift between AudioCatalog (compile-time keys) and the asset (runtime data)
        // as soon as the asset is edited in Inspector, so missing/orphaned entries don't fail silently
        // at runtime as no-op Play calls.
        private void ValidateCatalogCoverage()
        {
            if (lookup == null) return;

            var catalogKeys = CollectCatalogKeys();

            foreach (var key in catalogKeys)
                if (!lookup.ContainsKey(key))
                    Debug.LogWarning($"[AudioDatabase] AudioCatalog defines \"{key}\" but no entry exists in this database.", this);

            foreach (var key in lookup.Keys)
                if (!catalogKeys.Contains(key))
                    Debug.LogWarning($"[AudioDatabase] Entry \"{key}\" has no matching AudioCatalog constant — dead key or typo?", this);
        }

        private static HashSet<string> CollectCatalogKeys()
        {
            var keys = new HashSet<string>();
            Collect(typeof(AudioCatalog));
            return keys;

            void Collect(Type type)
            {
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    if (field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
                    {
                        var value = (string)field.GetRawConstantValue();
                        if (!string.IsNullOrEmpty(value)) keys.Add(value);
                    }
                }
                foreach (var nested in type.GetNestedTypes(BindingFlags.Public))
                    Collect(nested);
            }
        }
    }
}
