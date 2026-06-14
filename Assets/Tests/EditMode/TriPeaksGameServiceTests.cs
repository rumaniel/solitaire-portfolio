using System.Collections.Generic;
using Model.Board;
using Model.Card;
using Model.Game;
using NUnit.Framework;
using R3;
using Service.BoardGameService;
using Service.GameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class TriPeaksGameServiceTests
    {
        private sealed class FixedShuffle : IShuffleStrategy
        {
            private readonly List<PlayingCard> deck;
            public FixedShuffle(params PlayingCard[] cards) => deck = new List<PlayingCard>(cards);
            public List<PlayingCard> Shuffle(int seed) => new List<PlayingCard>(deck);
        }

        private static PlayingCard Card(Rank r) => new PlayingCard(r, Suit.Spade);

        private static BoardLayout FlatLayout(int n)
        {
            var cells = new List<BoardCell>(n);
            for (int i = 0; i < n; i++) cells.Add(new BoardCell(new CellId(i), null));
            return new BoardLayout(GameType.TriPeaks, 1, cells);
        }

        [Test]
        public void Initialize_FlipsFirstStockCardToWaste()
        {
            // deck = Seven,Eight,Nine; cell0=Seven; stock=[Eight,Nine]; WithStockDrawn pops LAST (Nine).
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Seven), Card(Rank.Eight), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(1), new TriPeaksMatchRule(), seed: 0);

            Assert.AreEqual(1, svc.CurrentState.Stock.Count, "one card left in stock after the deal flip");
            Assert.IsNotNull(svc.CurrentState.WasteTop);
            Assert.AreEqual(Rank.Nine, svc.CurrentState.WasteTop.Rank);
        }

        [Test]
        public void SelectCell_PlayableCard_MovesItToWaste_AndClearsCell()
        {
            // cell0 = Eight; waste-top after deal = Nine (adjacent -> playable).
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Eight), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(1), new TriPeaksMatchRule(), seed: 0);
            Assert.AreEqual(Rank.Nine, svc.CurrentState.WasteTop.Rank);

            svc.SelectCell(new CellId(0)); // 8 plays on 9

            Assert.IsFalse(svc.CurrentState.HasCard(new CellId(0)), "cell cleared");
            Assert.AreEqual(Rank.Eight, svc.CurrentState.WasteTop.Rank, "played card is new waste-top");
            Assert.IsTrue(svc.IsWon(svc.CurrentState), "all cells cleared");
        }

        [Test]
        public void SelectCell_NonPlayableCard_IsIgnored()
        {
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Five), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(1), new TriPeaksMatchRule(), seed: 0);
            svc.SelectCell(new CellId(0));
            Assert.IsTrue(svc.CurrentState.HasCard(new CellId(0)), "non-adjacent card stays");
        }

        [Test]
        public void SelectWasteTop_IsNoOp()
        {
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Eight), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(1), new TriPeaksMatchRule(), seed: 0);
            var before = svc.CurrentState;
            svc.SelectWasteTop();
            Assert.AreSame(before, svc.CurrentState);
        }

        [Test]
        public void CanRecycle_IsAlwaysFalse_NoSecondPass()
        {
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Five), Card(Rank.Nine), Card(Rank.Two)));
            svc.Initialize(FlatLayout(1), new TriPeaksMatchRule(), seed: 0, maxRecycles: 0);
            svc.DrawFromStock(); // exhaust stock onto waste
            Assert.AreEqual(0, svc.CurrentState.Stock.Count);
            Assert.IsFalse(svc.CanRecycle(svc.CurrentState));
        }

        [Test]
        public void HasAnyMove_TrueWhenAFreeCellIsPlayable()
        {
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Eight), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(1), new TriPeaksMatchRule(), seed: 0);
            Assert.AreEqual(0, svc.CurrentState.Stock.Count);
            Assert.IsTrue(svc.HasAnyMove(svc.CurrentState));
        }

        [Test]
        public void HasAnyMove_FalseWhenStuck()
        {
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Five), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(1), new TriPeaksMatchRule(), seed: 0);
            Assert.IsFalse(svc.HasAnyMove(svc.CurrentState));
        }

        [Test]
        public void HasAnyMove_TrueWhenStockRemains()
        {
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Five), Card(Rank.Two), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(1), new TriPeaksMatchRule(), seed: 0);
            Assert.IsTrue(svc.CurrentState.Stock.Count > 0);
            Assert.IsTrue(svc.HasAnyMove(svc.CurrentState));
        }

        [Test]
        public void Undo_RevertsAPlay()
        {
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Eight), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(1), new TriPeaksMatchRule(), seed: 0);
            svc.SelectCell(new CellId(0)); // play 8
            Assert.IsFalse(svc.CurrentState.HasCard(new CellId(0)));

            svc.Undo();
            Assert.IsTrue(svc.CurrentState.HasCard(new CellId(0)), "play reverted");
            Assert.AreEqual(Rank.Nine, svc.CurrentState.WasteTop.Rank, "waste-top restored");
        }

        [Test]
        public void GetHints_PlayableFreeCell_ReturnsMatch()
        {
            // deck = Eight,Two,Nine -> cells=[Eight,Two]; stock=[Nine]; flip -> waste=Nine, stock empty.
            // Eight is adjacent to Nine (playable); Two is not. Expect one Match hint on cell 0.
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Eight), Card(Rank.Two), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(2), new TriPeaksMatchRule(), seed: 0);
            var hints = svc.GetHints(svc.CurrentState);
            Assert.AreEqual(1, hints.Count);
            Assert.AreEqual(BoardHintKind.Match, hints[0].Kind);
            Assert.IsTrue(hints[0].Targets.Contains(new CellId(0)));
        }

        [Test]
        public void GetHints_NoPlayButStock_ReturnsDraw()
        {
            // deck = Five,Two,Nine,King -> cells=[Five,Two]; stock=[Nine,King]; flip pops King -> waste=King, stock=[Nine].
            // Five vs King not adjacent; Two vs King not adjacent -> no play; stock has [Nine] -> Draw.
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Five), Card(Rank.Two), Card(Rank.Nine), Card(Rank.King)));
            svc.Initialize(FlatLayout(2), new TriPeaksMatchRule(), seed: 0);
            var hints = svc.GetHints(svc.CurrentState);
            Assert.AreEqual(1, hints.Count);
            Assert.AreEqual(BoardHintKind.Draw, hints[0].Kind);
        }

        [Test]
        public void GetHints_StuckReturnsEmpty_NeverRecycle()
        {
            // deck = Five,Two,Nine -> cells=[Five,Two]; stock=[Nine]; flip -> waste=Nine, stock empty. No play, no recycle.
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Five), Card(Rank.Two), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(2), new TriPeaksMatchRule(), seed: 0);
            var hints = svc.GetHints(svc.CurrentState);
            Assert.AreEqual(0, hints.Count);
        }

        [Test]
        public void Restore_RoundTripsState()
        {
            var layout = TriPeaksLayoutFactory.Create();
            var rule = new TriPeaksMatchRule();
            var source = new TriPeaksGameService(new FisherYatesShuffleStrategy());
            source.Initialize(layout, rule, seed: 42);
            var snapshotState = source.CurrentState;

            var svc = new TriPeaksGameService(new FisherYatesShuffleStrategy());
            svc.Restore(layout, rule, seed: 42, state: snapshotState, undoHistory: new List<BoardState>());

            Assert.AreEqual(42, svc.CurrentSeed);
            Assert.IsTrue(svc.CurrentState.Equals(snapshotState));
            Assert.IsFalse(svc.CanUndo);
        }

        [Test]
        public void SelectCell_FreeButNotPlayable_EmitsOnInvalidTap()
        {
            // cell0 = Five; waste-top after deal = Nine (not adjacent) → invalid tap.
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Five), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(1), new TriPeaksMatchRule(), seed: 0);

            CellId? fired = null;
            using var _ = svc.OnInvalidTap.Subscribe(id => fired = id);

            svc.SelectCell(new CellId(0));

            Assert.IsTrue(fired.HasValue, "invalid tap should emit");
            Assert.AreEqual(new CellId(0), fired.Value);
            Assert.IsTrue(svc.CurrentState.HasCard(new CellId(0)), "card not played");
        }

        [Test]
        public void SelectCell_PlayableCard_DoesNotEmitOnInvalidTap()
        {
            // cell0 = Eight; waste-top = Nine (adjacent) → valid play, no invalid signal.
            var svc = new TriPeaksGameService(new FixedShuffle(Card(Rank.Eight), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(1), new TriPeaksMatchRule(), seed: 0);

            bool fired = false;
            using var _ = svc.OnInvalidTap.Subscribe(_2 => fired = true);

            svc.SelectCell(new CellId(0));

            Assert.IsFalse(fired, "a playable tap must not emit invalid");
            Assert.IsFalse(svc.CurrentState.HasCard(new CellId(0)), "card was played");
        }

        [Test]
        public void SelectCell_CoveredCell_DoesNotEmitOnInvalidTap()
        {
            // Full deal: only the base row is free; apex 0 is covered → tapping it is a silent no-op.
            var svc = new TriPeaksGameService(new FisherYatesShuffleStrategy());
            svc.Initialize(TriPeaksLayoutFactory.Create(), new TriPeaksMatchRule(), seed: 7);

            bool fired = false;
            using var _ = svc.OnInvalidTap.Subscribe(_2 => fired = true);

            svc.SelectCell(new CellId(0)); // apex 0 is covered at deal

            Assert.IsFalse(fired, "a covered (locked) cell must not emit invalid");
        }
    }
}
