using System.Collections.Generic;
using Audio;
using Component.Board;
using Data.Audio;
using Data.Game;
using Data.Stats;
using Model.Game;
using Model.Stats;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Service.GameService;
using Service.CardService;
using Service.HintService;
using Service.StatsService;
using Service.SnapshotService;
using Service.BoardGameService;
using Scene.Board;
using Core;

namespace Scene.Ingame
{
    public class IngameScene : SceneBase
    {
        [SerializeField] private IngameComponent component;
        [SerializeField] private IngameShellView shellView;

        [Header("Game Variants")]
        [Tooltip("All playable game variants this scene can host. " +
                 "Each GameVariant carries its (GameType, VariantId) key and DealRuleAsset. " +
                 "The IDealRule dictionary is built automatically from this list.")]
        [SerializeField] private GameVariant[] variants;

        [Header("Score Rules")]
        [SerializeField] private ScoreRuleAsset klondikeScoreRule;
        [SerializeField] private ScoreRuleAsset easthavenScoreRule;
        [SerializeField] private ScoreRuleAsset spiderScoreRule;

        [Header("Audio")]
        [SerializeField] private AudioDatabaseAsset sceneAudioDatabase;

        [Header("Board Views")]
        [SerializeField] private UIBoardController pyramidBoardController;
        [SerializeField] private UIBoardController triPeaksBoardController;

        private AudioSystem audioSystem;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(component);
            builder.RegisterComponent(shellView);

            if (sceneAudioDatabase != null)
            {
                builder.RegisterBuildCallback(container =>
                {
                    audioSystem = container.Resolve<AudioSystem>();
                    audioSystem.AddDatabase(sceneAudioDatabase);
                });
            }

            var ruleMap = BuildDealRuleMap(variants);
            builder.RegisterInstance<IReadOnlyDictionary<(GameType, int), IDealRule>>(ruleMap);
            builder.Register<DealRuleFactory>(Lifetime.Scoped).As<IDealRuleFactory>();

            builder.Register<ShuffleStrategyProvider>(Lifetime.Scoped);
            builder.Register<SolitaireGameService>(Lifetime.Scoped).As<IGameService>();
            builder.Register<SolitaireCardService>(Lifetime.Scoped).As<ICardService>();
            builder.Register<HintService>(Lifetime.Scoped).As<IHintService>();

            // Stats
            var scoreRuleMap = new Dictionary<GameType, IScoreRule>
            {
                { GameType.Klondike, klondikeScoreRule },
                { GameType.Easthaven, easthavenScoreRule },
                { GameType.Spider, spiderScoreRule },
            };
            builder.RegisterInstance<IReadOnlyDictionary<GameType, IScoreRule>>(scoreRuleMap);
            builder.Register<ScoreRuleFactory>(Lifetime.Scoped).As<IScoreRuleFactory>();
            builder.Register<SessionStatsService>(Lifetime.Scoped).As<ISessionStatsService>();

            // Snapshot
            builder.Register<GameSnapshotService>(Lifetime.Scoped).As<IGameSnapshotService>();

            // Board stack — BoardViewSet.All skips nulls, so an unwired scene is safe until the scene-edit commit.
            builder.RegisterInstance(new BoardViewSet(pyramidBoardController, triPeaksBoardController));
            builder.Register<FisherYatesShuffleStrategy>(Lifetime.Scoped).As<IShuffleStrategy>();
            builder.Register<PyramidGameService>(Lifetime.Scoped);
            builder.Register<TriPeaksGameService>(Lifetime.Scoped);
            builder.Register<BoardGameServiceFactory>(Lifetime.Scoped).As<IBoardGameServiceFactory>();
            builder.Register<BoardSnapshotService>(Lifetime.Scoped).As<IBoardSnapshotService>();
            builder.RegisterEntryPoint<BoardPresenter>().As<BoardPresenter>();

            builder.RegisterEntryPoint<IngamePresenter>()
                .As<IngamePresenter>();
        }

        /// <summary>Builds the (GameType, variantId) to IDealRule dictionary. Invalid entries are logged and skipped.</summary>
        private static IReadOnlyDictionary<(GameType, int), IDealRule> BuildDealRuleMap(GameVariant[] variants)
        {
            var map = new Dictionary<(GameType, int), IDealRule>();
            if (variants == null) return map;

            foreach (var v in variants)
            {
                if (v == null)
                {
                    Debug.LogWarning("[IngameScene] Null entry in variants array — skipping.");
                    continue;
                }
                if (v.DealRule == null)
                {
                    Debug.LogWarning($"[IngameScene] GameVariant '{v.name}' has no DealRule — skipping.");
                    continue;
                }
                if (v.GameType == GameType.None)
                {
                    Debug.LogWarning($"[IngameScene] GameVariant '{v.name}' has GameType.None — skipping.");
                    continue;
                }
                if (v.VariantId < 1)
                {
                    Debug.LogWarning(
                        $"[IngameScene] GameVariant '{v.name}' has VariantId {v.VariantId} (must be >= 1) — skipping.");
                    continue;
                }
                var key = (v.GameType, v.VariantId);
                if (map.ContainsKey(key))
                {
                    Debug.LogWarning($"[IngameScene] Duplicate variant key {key} (asset '{v.name}') — first entry wins.");
                    continue;
                }
                map[key] = v.DealRule;
            }
            return map;
        }

        protected override void OnDestroy()
        {
            if (audioSystem != null && sceneAudioDatabase != null)
                audioSystem.RemoveDatabase(sceneAudioDatabase);
            base.OnDestroy();
        }
    }
}
