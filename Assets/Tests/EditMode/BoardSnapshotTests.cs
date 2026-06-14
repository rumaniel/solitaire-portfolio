using System.Collections.Generic;
using MemoryPack;
using Model.Board;
using Model.Card;
using Model.Game;
using Model.Stats;
using NUnit.Framework;

namespace Tests.EditMode
{
    [TestFixture]
    public class BoardSnapshotTests
    {
        private static PlayingCard C(Rank r, Suit s) => new PlayingCard(r, s);

        // A 3-cell board: cell 0 = 9♠ removed, cell 1 = 4♥, cell 2 = K♣; stock [2♦]; waste [8♠].
        private static BoardState SampleState()
        {
            var cells = new PlayingCard[]
            {
                C(Rank.Nine, Suit.Spade),
                C(Rank.Four, Suit.Heart),
                C(Rank.King, Suit.Club),
            };
            var state = new BoardState(cells,
                stock: new[] { C(Rank.Two, Suit.Diamond) },
                waste: new[] { C(Rank.Eight, Suit.Spade) });
            return state.WithCellsRemoved(new[] { new CellId(0) }); // remove cell 0
        }

        private static void AssertStateEqual(BoardState expected, BoardState actual)
        {
            Assert.AreEqual(expected.CellCount, actual.CellCount);
            for (int i = 0; i < expected.CellCount; i++)
            {
                var id = new CellId(i);
                Assert.AreEqual(expected.HasCard(id), actual.HasCard(id), $"cell {i} presence");
                if (expected.HasCard(id))
                    Assert.IsTrue(expected.CardAt(id).Equals(actual.CardAt(id)), $"cell {i} card");
            }
            CollectionAssert.AreEqual(expected.Stock, actual.Stock);
            CollectionAssert.AreEqual(expected.Waste, actual.Waste);
        }

        [Test]
        public void Converter_RoundTrip_PreservesBoardState_IncludingRemovedCells()
        {
            var original = SampleState();
            var snapshot = BoardSnapshotConverter.ToSnapshot(
                GameType.Pyramid, 1, 42, original, new List<BoardState>(), new SessionStats());
            var restored = BoardSnapshotConverter.ToBoardState(snapshot.CurrentState);

            AssertStateEqual(original, restored);
            Assert.IsFalse(restored.HasCard(new CellId(0)), "removed cell stays removed");
        }

        [Test]
        public void Converter_RoundTrip_PreservesUndoHistory()
        {
            var s0 = SampleState();
            var s1 = s0.WithCellsRemoved(new[] { new CellId(1) });
            var history = new List<BoardState> { s0, s1 };

            var snapshot = BoardSnapshotConverter.ToSnapshot(
                GameType.Pyramid, 1, 42, s1, history, new SessionStats());
            var restored = BoardSnapshotConverter.ToHistory(snapshot.UndoHistory);

            Assert.AreEqual(2, restored.Count);
            AssertStateEqual(s0, restored[0]);
            AssertStateEqual(s1, restored[1]);
        }

        [Test]
        public void Converter_RoundTrip_PreservesStatsAndMetadata()
        {
            var stats = new SessionStats { Score = 150, MoveCount = 42, ElapsedSeconds = 123.5f, HintCount = 3 };
            var snapshot = BoardSnapshotConverter.ToSnapshot(
                GameType.Pyramid, 2, 99, SampleState(), new List<BoardState>(), stats);

            Assert.AreEqual(GameType.Pyramid, snapshot.GameType);
            Assert.AreEqual(2, snapshot.Variant);
            Assert.AreEqual(99, snapshot.Seed);
            Assert.Greater(snapshot.SavedAtUtcTicks, 0);

            var restoredStats = BoardSnapshotConverter.ToSessionStats(snapshot.Stats);
            Assert.AreEqual(150, restoredStats.Score);
            Assert.AreEqual(42, restoredStats.MoveCount);
            Assert.AreEqual(123.5f, restoredStats.ElapsedSeconds, 0.01f);
            Assert.AreEqual(3, restoredStats.HintCount);
        }

        [Test]
        public void Snapshot_MemoryPack_SerializeDeserialize_RoundTrips()
        {
            var snapshot = BoardSnapshotConverter.ToSnapshot(
                GameType.Pyramid, 1, 42, SampleState(), new List<BoardState>(),
                new SessionStats { Score = 10 });

            var bytes = MemoryPackSerializer.Serialize(snapshot);
            var back = MemoryPackSerializer.Deserialize<BoardSnapshot>(bytes);

            Assert.IsNotNull(back);
            Assert.AreEqual(snapshot.Seed, back.Seed);
            Assert.AreEqual(snapshot.Stats.Score, back.Stats.Score);
            AssertStateEqual(
                BoardSnapshotConverter.ToBoardState(snapshot.CurrentState),
                BoardSnapshotConverter.ToBoardState(back.CurrentState));
        }

        [Test]
        public void ToBoardState_NullDto_ThrowsArgumentNull()
        {
            Assert.Throws<System.ArgumentNullException>(() => BoardSnapshotConverter.ToBoardState(null));
        }

        [Test]
        public void ToSessionStats_NullDto_ThrowsArgumentNull()
        {
            Assert.Throws<System.ArgumentNullException>(() => BoardSnapshotConverter.ToSessionStats(null));
        }

        [Test]
        public void ToSnapshot_NullCurrentState_ThrowsArgumentNull()
        {
            Assert.Throws<System.ArgumentNullException>(() => BoardSnapshotConverter.ToSnapshot(
                GameType.Pyramid, 1, 1, currentState: null, undoHistory: new List<BoardState>(), stats: new SessionStats()));
        }
    }
}
