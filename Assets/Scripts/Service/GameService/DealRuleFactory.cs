using System.Collections.Generic;
using Model.Game;

namespace Service.GameService
{
    /// <summary>Resolves (GameType, variant) pairs to IDealRule instances.</summary>
    public class DealRuleFactory : IDealRuleFactory
    {
        private readonly IReadOnlyDictionary<(GameType, int), IDealRule> _rules;

        public DealRuleFactory(IReadOnlyDictionary<(GameType, int), IDealRule> rules)
        {
            _rules = rules;
        }

        /// <inheritdoc/>
        public IDealRule Create(GameType gameType, int variant = 1)
        {
            bool found = _rules.TryGetValue((gameType, variant), out var rule);

            if (found && rule != null)
                return rule;

            var message = found
                ? $"IDealRule for (GameType '{gameType}', Variant {variant}) is registered but null. " +
                  $"Make sure a GameVariant asset with its DealRule field populated is present in IngameScene.variants."
                : $"No IDealRule registered for (GameType '{gameType}', Variant {variant}). " +
                  $"Add a GameVariant asset for this combination to IngameScene.variants.";

            throw new KeyNotFoundException(message);
        }
    }
}
