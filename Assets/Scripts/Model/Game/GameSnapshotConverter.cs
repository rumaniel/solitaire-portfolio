using System;
using System.Collections.Generic;
using Model.Card;
using Model.Stats;

namespace Model.Game
{
    public static class GameSnapshotConverter
    {
        public static GameSnapshot ToSnapshot(
            GameType gameType,
            int seed,
            int drawCount,
            TableState currentState,
            IReadOnlyCollection<TableState> undoHistory,
            SessionStats stats)
        {
            var historyArray = new TableStateDto[undoHistory.Count];
            int i = 0;
            foreach (var state in undoHistory)
                historyArray[i++] = ToTableStateDto(state);

            return new GameSnapshot
            {
                GameType = gameType,
                Seed = seed,
                DrawCount = drawCount,
                CurrentState = ToTableStateDto(currentState),
                UndoHistory = historyArray,
                Stats = ToSessionStatsDto(stats),
                SavedAtUtcTicks = DateTime.UtcNow.Ticks
            };
        }

        public static TableState ToTableState(TableStateDto dto)
        {
            var foundations = new List<PileState>(dto.Foundations?.Length ?? 0);
            if (dto.Foundations != null)
                foreach (var f in dto.Foundations)
                    foundations.Add(ToPileState(f));

            var tableaus = new List<PileState>(dto.Tableaus?.Length ?? 0);
            if (dto.Tableaus != null)
                foreach (var t in dto.Tableaus)
                    tableaus.Add(ToPileState(t));

            return new TableState(
                ToPileState(dto.Stock),
                ToPileState(dto.Waste),
                foundations,
                tableaus,
                dto.WasteFanCount);
        }

        public static IReadOnlyList<TableState> ToHistory(TableStateDto[] dtos)
        {
            if (dtos == null) return Array.Empty<TableState>();
            var list = new List<TableState>(dtos.Length);
            foreach (var dto in dtos)
                list.Add(ToTableState(dto));
            return list;
        }

        public static SessionStats ToSessionStats(SessionStatsDto dto)
        {
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

        private static TableStateDto ToTableStateDto(TableState state)
        {
            var foundations = new PileStateDto[state.Foundations.Count];
            for (int i = 0; i < state.Foundations.Count; i++)
                foundations[i] = ToPileStateDto(state.Foundations[i]);

            var tableaus = new PileStateDto[state.Tableaus.Count];
            for (int i = 0; i < state.Tableaus.Count; i++)
                tableaus[i] = ToPileStateDto(state.Tableaus[i]);

            return new TableStateDto
            {
                Stock = ToPileStateDto(state.Stock),
                Waste = ToPileStateDto(state.Waste),
                Foundations = foundations,
                Tableaus = tableaus,
                WasteFanCount = state.WasteFanCount
            };
        }

        private static PileStateDto ToPileStateDto(PileState pile)
        {
            var cards = new CardDto[pile.Cards.Count];
            for (int i = 0; i < pile.Cards.Count; i++)
            {
                cards[i] = new CardDto
                {
                    Rank = pile.Cards[i].Rank,
                    Suit = pile.Cards[i].Suit
                };
            }

            return new PileStateDto
            {
                PileType = pile.Id.Type,
                PileIndex = pile.Id.Index,
                Cards = cards,
                FaceUpFromIndex = pile.FaceUpFromIndex
            };
        }

        private static PileState ToPileState(PileStateDto dto)
        {
            var cards = new List<PlayingCard>(dto.Cards?.Length ?? 0);
            if (dto.Cards != null)
                foreach (var c in dto.Cards)
                    cards.Add(new PlayingCard(c.Rank, c.Suit));

            var id = new PileId(dto.PileType, dto.PileIndex);
            return new PileState(id, cards, dto.FaceUpFromIndex);
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
