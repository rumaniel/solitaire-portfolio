using NUnit.Framework;
using UnityEditor;
using Data.Achievement;
using System.Linq;

namespace Tests.EditMode
{
    /// <summary>Guards against missed Inspector bindings on AchievementDefinitionAsset SOs.</summary>
    [TestFixture]
    public class AchievementDefinitionLocalizationTests
    {
        [Test]
        public void EveryAchievementHasBoundTitleAndDescriptionEntries()
        {
            var guids = AssetDatabase.FindAssets("t:AchievementDefinitionAsset");
            Assert.That(guids.Length, Is.GreaterThanOrEqualTo(1),
                "Expected at least one AchievementDefinitionAsset under Assets/.");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<AchievementDefinitionAsset>(path);
                Assert.IsNotNull(asset, $"Failed to load {path}");

                Assert.IsFalse(asset.TitleEntry.IsEmpty,
                    $"{asset.Id} ({path}): titleEntry is unbound — set it in the Inspector.");
                Assert.IsFalse(asset.DescriptionEntry.IsEmpty,
                    $"{asset.Id} ({path}): descriptionEntry is unbound — set it in the Inspector.");

                Assert.That(asset.TitleEntry.TableReference.TableCollectionName,
                    Is.EqualTo("Achievements"),
                    $"{asset.Id}: titleEntry must point at the Achievements table.");
                Assert.That(asset.DescriptionEntry.TableReference.TableCollectionName,
                    Is.EqualTo("Achievements"),
                    $"{asset.Id}: descriptionEntry must point at the Achievements table.");
            }
        }

        [Test]
        public void EveryAchievementIdMatchesEntryKeyConvention()
        {
            // Convention: "achievement.{id}.title" / ".description".
            var guids = AssetDatabase.FindAssets("t:AchievementDefinitionAsset");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<AchievementDefinitionAsset>(path);
                if (asset == null || string.IsNullOrEmpty(asset.Id)) continue;

                var expectedTitleKey = $"achievement.{asset.Id}.title";
                var expectedDescKey = $"achievement.{asset.Id}.description";
                Assert.That(asset.TitleKey, Is.EqualTo(expectedTitleKey),
                    $"{asset.Id}: titleEntry key '{asset.TitleKey}' deviates from convention '{expectedTitleKey}'.");
                Assert.That(asset.DescriptionKey, Is.EqualTo(expectedDescKey),
                    $"{asset.Id}: descriptionEntry key '{asset.DescriptionKey}' deviates from convention '{expectedDescKey}'.");
            }
        }
    }
}
