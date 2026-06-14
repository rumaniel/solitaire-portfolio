using System.Collections.Generic;
using Model.Game;
using R3;
using Service.CardService;

namespace Service.GameService
{
    public interface IGameService
    {
        IDealRule DealRule { get; }

        /// <summary>
        /// The seed used to shuffle the current deck. Allows replaying the same deal.
        /// <br/>Null before <see cref="Initialize"/> is called.
        /// </summary>
        int? CurrentSeed { get; }

        /// <summary>The most recently published board state. Null before Initialize() is called.</summary>
        TableState CurrentState { get; }

        Observable<TableState> OnTableStateChanged { get; }

        /// <summary>
        /// Initializes the game with the given deal rule.
        /// If <paramref name="seed"/> is null, a cryptographically random seed is generated.
        /// Otherwise, the given seed is used for deterministic shuffling (replay / shared deal).
        /// </summary>
        void Initialize(IDealRule dealRule, int? seed = null);

        // [Gateway] 향후 UniTask<MoveCardResult> ExecuteMoveAsync(MoveCardRequest) 로 교체 예정.
        // 서버 전환 시: Presenter의 TryMove + ExecuteMove 두 호출이 gateway 단일 호출로 통합됨.
        // 현재는 순수 state mutation — 검증은 호출 전 CardService.TryMove()로 완료됐음을 전제.
        MoveCardResult ExecuteMove(MoveCardRequest request);

        // [Gateway] 향후 IGameGateway.DrawFromStockAsync() 로 교체 예정.
        // Stock이 비어있고 CanRecycleStock=true이면 Waste를 뒤집어 Stock으로 복원.
        void DrawFromStock();
        bool IsWon(TableState state);

        bool CanUndo { get; }
        void Undo();

        /// <summary>
        /// False when tapping the stock would be illegal or a no-op. Tableau-dealing rules:
        /// stock empty, or the classic Spider guard (no deal while any column is empty).
        /// Waste-drawing rules: stock empty with no recycle available.
        /// </summary>
        bool CanDealStock { get; }

        IReadOnlyCollection<TableState> UndoHistory { get; }
        void Restore(IDealRule dealRule, int seed, TableState state, IReadOnlyList<TableState> undoHistory);
    }
}
