using Service.CardService;

namespace Service.HintService
{
    public readonly struct HintMove
    {
        public MoveCardRequest Request { get; }
        public int Priority { get; }
        public HintMoveType MoveType { get; }

        public HintMove(MoveCardRequest request, int priority, HintMoveType moveType)
        {
            Request = request;
            Priority = priority;
            MoveType = moveType;
        }
    }
}
