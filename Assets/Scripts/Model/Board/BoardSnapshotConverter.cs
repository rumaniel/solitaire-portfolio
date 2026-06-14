using System;
using System.Collections.Generic;
using Model.Card;
using Model.Game;
using Model.Stats;

namespace Model.Board
{
    /// <summary>Converts board runtime state ⟷ serializable DTOs. Parallels GameSnapshotConverter.</summary>
    public static class BoardSnapshotConverter
    {
        public static BoardSnapshot ToSnapshot(
            GameType gameType, int variant, int seed,
            BoardState currentState, IReadOnlyCollection<BoardState> undoHistory, SessionStats stats)
        {
            if (currentState == null) throw new ArgumentNullException(nameof(currentState));
            if (undoHistory == null) throw new ArgumentNullException(nameof(undoHistory));
            if (stats == null) throw new ArgumentNullException(nameof(stats));

            var history = new BoardStateDto[undoHistory.Count];
            int i = 0;
            foreach (var state in undoHistory)
                history[i++] = ToBoardStateDto(state);

            return new BoardSnapshot
            {
                GameType = gameType,
                Variant = variant,
                Seed = seed,
                CurrentState = ToBoardStateDto(currentState),
                UndoHistory = history,
                Stats = ToSessionStatsDto(stats),
                SavedAtUtcTicks = DateTime.UtcNow.Ticks
            };
        }

        public static BoardState ToBoardState(BoardStateDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto)); // corrupt/partial snapshot → fail fast
            int count = dto.Cells?.Length ?? 0;
            var cells = new PlayingCard[count];
            for (int i = 0; i < count; i++)
            {
                var c = dto.Cells[i];
                cells[i] = c == null ? null : new PlayingCard(c.Rank, c.Suit);
            }
            return new BoardState(cells, ToCards(dto.Stock), ToCards(dto.Waste), dto.RecycleCount);
        }

        public static IReadOnlyList<BoardState> ToHistory(BoardStateDto[] dtos)
        {
            if (dtos == null) return Array.Empty<BoardState>();
            var list = new List<BoardState>(dtos.Length);
            foreach (var dto in dtos)
                list.Add(ToBoardState(dto));
            return list;
        }

        public static SessionStats ToSessionStats(SessionStatsDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto)); // corrupt/partial snapshot → fail fast
            return new SessionStats
            {
                Score = dto.Score,
                MoveCount = dto.MoveCount,
                ElapsedSeconds = dto.ElapsedSeconds,
                UndoUsed = dto.UndoUsed,
                HintUsed = dto.HintUsed,
                HintCount = dto.HintCount
            };
        }

        private static BoardStateDto ToBoardStateDto(BoardState state)
        {
            var cells = new CardDto[state.CellCount];
            for (int i = 0; i < state.CellCount; i++)
            {
                var id = new CellId(i);
                cells[i] = state.HasCard(id) ? ToDto(state.CardAt(id)) : null;
            }
            return new BoardStateDto
            {
                Cells = cells,
                Stock = ToDtos(state.Stock),
                Waste = ToDtos(state.Waste),
                RecycleCount = state.RecycleCount
            };
        }

        private static CardDto ToDto(PlayingCard card) => new CardDto { Rank = card.Rank, Suit = card.Suit };

        private static CardDto[] ToDtos(IReadOnlyList<PlayingCard> cards)
        {
            var arr = new CardDto[cards.Count];
            for (int i = 0; i < cards.Count; i++)
                arr[i] = ToDto(cards[i]);
            return arr;
        }

        private static List<PlayingCard> ToCards(CardDto[] dtos)
        {
            var list = new List<PlayingCard>(dtos?.Length ?? 0);
            if (dtos != null)
                foreach (var c in dtos)
                    list.Add(new PlayingCard(c.Rank, c.Suit));
            return list;
        }

        private static SessionStatsDto ToSessionStatsDto(SessionStats stats)
        {
            return new SessionStatsDto
            {
                Score = stats.Score,
                MoveCount = stats.MoveCount,
                ElapsedSeconds = stats.ElapsedSeconds,
                UndoUsed = stats.UndoUsed,
                HintUsed = stats.HintUsed,
                HintCount = stats.HintCount
            };
        }
    }
}
