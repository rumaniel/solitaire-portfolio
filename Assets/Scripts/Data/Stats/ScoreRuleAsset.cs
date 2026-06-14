using Model.Stats;
using UnityEngine;

namespace Data.Stats
{
    [CreateAssetMenu(fileName = "ScoreRuleAsset", menuName = "Solitaire/Stats/Score Rule")]
    public class ScoreRuleAsset : ScriptableObject, IScoreRule
    {
        [Header("Card Move Scores")]
        [SerializeField] private int wasteToTableau = 5;
        [SerializeField] private int wasteToFoundation = 10;
        [SerializeField] private int tableauToFoundation = 10;
        [SerializeField] private int foundationToTableau = -15;

        [Header("Other Scores")]
        [SerializeField] private int tableauReveal = 5;
        [SerializeField] private int stockRecycle = -100;

        public int WasteToTableau => wasteToTableau;
        public int WasteToFoundation => wasteToFoundation;
        public int TableauToFoundation => tableauToFoundation;
        public int FoundationToTableau => foundationToTableau;
        public int TableauReveal => tableauReveal;
        public int StockRecycle => stockRecycle;
    }
}
