using Model.Game;
using UnityEngine;

namespace Data.Game
{
    [CreateAssetMenu(fileName = "DealRuleAsset", menuName = "Solitaire/Game/Deal Rule")]
    public class DealRuleAsset : ScriptableObject, IDealRule
    {
        [Header("Table Layout")]
        [SerializeField] private int tableauCount = 7;
        [SerializeField] private int foundationCount = 4;
        [SerializeField] private int perSuitCardCount = 13;

        [Header("Stock Rules")]
        [SerializeField] private bool hasWaste = true;
        [SerializeField] private bool canRecycleStock = true;
        [SerializeField] private int stockDrawCount = 1;
        [SerializeField] private bool stockDealsToTableau;

        [Header("Initial Deal")]
        [SerializeField] private int[] initialCardCounts = { 1, 2, 3, 4, 5, 6, 7 };
        [SerializeField] private int initialFaceUpPerColumn = 1;

        [Header("Placement Rules")]
        [SerializeField] private bool onlyKingOnEmptyTableau = true;

        [Header("Deck Composition")]
        [Tooltip("Number of 52-card decks. Spider uses 2 (104 cards).")]
        [SerializeField] private int deckCount = 1;
        [Tooltip("Suits in play (1/2/4). The deck is remapped onto the first N suits, ranks preserved.")]
        [SerializeField] private int suitCount = 4;

        [Header("Spider Rules")]
        [SerializeField] private TableauRunRule runRule = TableauRunRule.AlternatingColor;
        [SerializeField] private TableauDropRule dropRule = TableauDropRule.AlternatingColor;
        [SerializeField] private bool stockDealRequiresNoEmptyColumn;
        [SerializeField] private bool autoCollectCompletedRuns;

        public int TableauCount => tableauCount;
        public int FoundationCount => foundationCount;
        public int PerSuitCardCount => perSuitCardCount;
        public bool HasWaste => hasWaste;
        public bool CanRecycleStock => canRecycleStock;
        public int StockDrawCount => stockDrawCount;
        public bool StockDealsToTableau => stockDealsToTableau;
        public int[] InitialCardCounts => initialCardCounts;
        public int InitialFaceUpPerColumn => initialFaceUpPerColumn;
        public bool OnlyKingOnEmptyTableau => onlyKingOnEmptyTableau;
        public int DeckCount => deckCount;
        public int SuitCount => suitCount;
        public TableauRunRule RunRule => runRule;
        public TableauDropRule DropRule => dropRule;
        public bool StockDealRequiresNoEmptyColumn => stockDealRequiresNoEmptyColumn;
        public bool AutoCollectCompletedRuns => autoCollectCompletedRuns;
    }
}
