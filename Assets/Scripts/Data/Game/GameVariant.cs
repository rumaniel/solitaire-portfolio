using Model.Game;
using UnityEngine;

namespace Data.Game
{
    /// <summary>A single playable game variant (e.g. "Klondike Draw 1") with a unified (GameType, VariantId) key.</summary>
    [CreateAssetMenu(fileName = "GameVariant", menuName = "Solitaire/Game/Game Variant")]
    public class GameVariant : ScriptableObject
    {
        [SerializeField] private GameType gameType = GameType.Klondike;

        [Tooltip("Game-type-specific variant identifier.\n" +
                 "Klondike: drawCount (1 or 3)\n" +
                 "Spider: suitCount (1, 2, or 4)\n" +
                 "Pyramid/TriPeaks: board variant id")]
        [SerializeField] private int variantId = 1;

        [SerializeField] private string displayName = "Klondike";
        [SerializeField] private DealRuleAsset dealRule;
        [SerializeField] private Sprite previewIcon;

        public GameType GameType => gameType;
        public int VariantId => variantId;
        public string DisplayName => displayName;
        public DealRuleAsset DealRule => dealRule;
        public Sprite PreviewIcon => previewIcon;
    }
}
