using System.Collections.Generic;
using Model.Game;
using Service.GameService;

namespace Service.HintService
{
    public interface IHintService
    {
        void Initialize(IDealRule dealRule);
        IReadOnlyList<HintMove> GetHints(TableState state);
        bool HasAnyMove(TableState state);
        bool CanAutoComplete(TableState state);
        IReadOnlyList<HintMove> GetAutoCompleteMoves(TableState state);
    }
}
