using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scene.Lobby.View;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace Tests.EditMode
{
    /// <summary>Guards against missed Inspector bindings on Lobby grid tile LocalizeStringEvent overrides.</summary>
    [TestFixture]
    public class LobbyTileLocalizationTests
    {
        private const string UiTableCollection = "UI";
        private const string LobbyScenePath = "Assets/Scenes/Lobby.unity";

        private static readonly Dictionary<string, string> ExpectedKeyByName = new()
        {
            { "Klondike Draw 1", "lobby.game.klondike_1" },
            { "Klondike Draw 3", "lobby.game.klondike_3" },
            { "Easthaven",       "lobby.game.easthaven"  },
            { "Spider",          "lobby.game.spider"     },
            { "Pyramid",         "lobby.game.pyramid"    },
            { "Tripeaks",        "lobby.game.tripeaks"   },
        };

        private UnityScene loadedLobby;
        private bool ownsLoadedLobby;

        [SetUp]
        public void SetUp()
        {
            var existing = SceneManager.GetSceneByPath(LobbyScenePath);
            if (existing.IsValid() && existing.isLoaded)
            {
                loadedLobby = existing;
                ownsLoadedLobby = false;
            }
            else
            {
                loadedLobby = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Additive);
                ownsLoadedLobby = true;
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (ownsLoadedLobby && loadedLobby.IsValid() && loadedLobby.isLoaded)
                EditorSceneManager.CloseScene(loadedLobby, removeScene: true);
        }

        [Test]
        public void EveryLobbyTileBindsItsOwnLocalizationKey()
        {
            var sharedPath = "Assets/Localization/Tables/UI Shared Data.asset";
            var shared = AssetDatabase.LoadAssetAtPath<SharedTableData>(sharedPath);
            Assert.IsNotNull(shared, $"Failed to load {sharedPath}");

            var tiles = new List<GameTileView>();
            foreach (var root in loadedLobby.GetRootGameObjects())
                tiles.AddRange(root.GetComponentsInChildren<GameTileView>(includeInactive: true));

            Assert.That(tiles.Count, Is.EqualTo(ExpectedKeyByName.Count),
                $"Expected {ExpectedKeyByName.Count} GameTileView instances in Lobby, found {tiles.Count}.");

            var titleField = typeof(GameTileView).GetField("titleText",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(titleField, "GameTileView.titleText field not found via reflection.");

            var seenIds = new HashSet<long>();
            foreach (var tile in tiles)
            {
                var name = tile.gameObject.name;
                Assert.That(ExpectedKeyByName.ContainsKey(name), Is.True,
                    $"Unexpected tile GameObject name '{name}' — update test mapping when adding new tiles.");

                var titleText = titleField.GetValue(tile) as TMP_Text;
                Assert.IsNotNull(titleText, $"{name}: titleText reference is null.");

                var lse = titleText.GetComponent<LocalizeStringEvent>();
                Assert.IsNotNull(lse, $"{name}: title TMP_Text is missing a LocalizeStringEvent component.");
                Assert.IsTrue(lse.enabled,
                    $"{name}: title LocalizeStringEvent is disabled — it will not refresh on locale change.");

                Assert.That(lse.StringReference.TableReference.TableCollectionName,
                    Is.EqualTo(UiTableCollection),
                    $"{name}: title points at table collection '{lse.StringReference.TableReference.TableCollectionName}' but expected '{UiTableCollection}'.");

                var entryRef = lse.StringReference.TableEntryReference;
                var entry = shared.GetEntry(entryRef.KeyId);
                Assert.IsNotNull(entry,
                    $"{name}: bound KeyId {entryRef.KeyId} does not exist in UI Shared Data.");
                Assert.That(entry.Key, Is.EqualTo(ExpectedKeyByName[name]),
                    $"{name}: title points at '{entry.Key}' but expected '{ExpectedKeyByName[name]}'.");

                Assert.That(seenIds.Add(entryRef.KeyId), Is.True,
                    $"{name}: KeyId {entryRef.KeyId} is shared with another tile — every tile must bind a unique key.");
            }
        }
    }
}
