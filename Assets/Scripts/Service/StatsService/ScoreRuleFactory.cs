using System;
using System.Collections.Generic;
using Model.Game;
using Model.Stats;

namespace Service.StatsService
{
    public class ScoreRuleFactory : IScoreRuleFactory
    {
        private readonly IReadOnlyDictionary<GameType, IScoreRule> rules;

        public ScoreRuleFactory(IReadOnlyDictionary<GameType, IScoreRule> rules)
        {
            this.rules = rules;
        }

        public IScoreRule Create(GameType gameType)
        {
            if (rules.TryGetValue(gameType, out var rule))
                return rule;
            throw new ArgumentOutOfRangeException(nameof(gameType), gameType,
                $"No IScoreRule registered for GameType '{gameType}'.");
        }
    }
}
