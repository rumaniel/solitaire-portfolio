using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Model.Card;
using Model.Game;
using Service.CardService;
using Service.HintService;

namespace Tests.EditMode
{
    [TestFixture]
    public class HintServiceTests
    {
        private SolitaireCardService _cardService;
        private HintService _hintService;
        private StubDealRule _rule;

        [SetUp]
        public void SetUp()
        {
            _rule = new StubDealRule();
            _cardService = new SolitaireCardService();
            _cardService.Initialize(_rule);
            _hintService = new HintService(_cardService);
            _hintService.Initialize(_rule);
        }

        // --- Helpers ---

        private static PileState EmptyPile(PileType type, int idx = 0) =>
            new PileState(new PileId(type, idx), new List<PlayingCard>(), 0);

        private static PileState Pile(PileType type, int idx, int faceUpFrom, params PlayingCard[] cards) =>
            new PileState(new PileId(type, idx), new List<PlayingCard>(cards), faceUpFrom);

        private static PileState FaceUpPile(PileType type, int idx, params PlayingCard[] cards) =>
            new PileState(new PileId(type, idx), new List<PlayingCard>(cards), 0);

        private TableState BuildState(
            PileState stock = null,
            PileState waste = null,
            PileState[] foundations = null,
            PileState[] tableaus = null)
        {
            stock ??= EmptyPile(PileType.Stock);
            waste ??= EmptyPile(PileType.Waste);

            var fList = new List<PileState>();
            for (int i = 0; i < _rule.FoundationCount; i++)
                fList.Add(foundations != null && i < foundations.Length && foundations[i] != null
                    ? foundations[i]
                    : EmptyPile(PileType.Foundation, i));

            var tList = new List<PileState>();
            for (int i = 0; i < _rule.TableauCount; i++)
                tList.Add(tableaus != null && i < tableaus.Length && tableaus[i] != null
                    ? tableaus[i]
                    : EmptyPile(PileType.Tableau, i));

            return new TableState(stock, waste, fList, tList);
        }

        private static PlayingCard C(Rank rank, Suit suit) => new PlayingCard(rank, suit);

        // ============================================================
        // FindAllMoves / GetHints
        // ============================================================

        [Test]
        public void FindAllMoves_EmptyBoard_ReturnsNoMoves()
        {
            var state = BuildState();
            var moves = MoveEnumerator.FindAllMoves(state, _cardService, _rule);
            Assert.AreEqual(0, moves.Count);
        }

        [Test]
        public void WasteAce_ToEmptyFoundation_Found()
        {
            var state = BuildState(
                waste: FaceUpPile(PileType.Waste, 0, C(Rank.Ace, Suit.Spade)));

            var moves = MoveEnumerator.FindAllMoves(state, _cardService, _rule);
            Assert.IsTrue(moves.Any(m =>
                m.MoveType == HintMoveType.WasteToFoundation
                && m.Request.Card.Rank == Rank.Ace));
        }

        [Test]
        public void WasteCard_ToValidTableau_Found()
        {
            var state = BuildState(
                waste: FaceUpPile(PileType.Waste, 0, C(Rank.Nine, Suit.Heart)),
                tableaus: new[]
                {
                    FaceUpPile(PileType.Tableau, 0, C(Rank.Ten, Suit.Spade))
                });

            var moves = MoveEnumerator.FindAllMoves(state, _cardService, _rule);
            Assert.IsTrue(moves.Any(m =>
                m.MoveType == HintMoveType.WasteToTableau
                && m.Request.Card.Rank == Rank.Nine));
        }

        [Test]
        public void TableauTopAce_ToFoundation_Found()
        {
            var state = BuildState(
                tableaus: new[]
                {
                    FaceUpPile(PileType.Tableau, 0, C(Rank.Ace, Suit.Heart))
                });

            var moves = MoveEnumerator.FindAllMoves(state, _cardService, _rule);
            Assert.IsTrue(moves.Any(m =>
                m.MoveType == HintMoveType.TableauToFoundation
                && m.Request.Card.Rank == Rank.Ace));
        }

        [Test]
        public void TableauToTableau_WithReveal_DetectedAsReveal()
        {
            // Tableau[0]: face-down card at 0, face-up 5H at 1
            // Tableau[1]: face-up 6S (black)
            var state = BuildState(
                tableaus: new[]
                {
                    Pile(PileType.Tableau, 0, 1, C(Rank.King, Suit.Club), C(Rank.Five, Suit.Heart)),
                    FaceUpPile(PileType.Tableau, 1, C(Rank.Six, Suit.Spade))
                });

            var moves = MoveEnumerator.FindAllMoves(state, _cardService, _rule);
            Assert.IsTrue(moves.Any(m => m.MoveType == HintMoveType.TableauToTableauReveal));
        }

        [Test]
        public void TableauToTableau_WithoutReveal_DetectedAsPlain()
        {
            // Tableau[0]: all face-up, 5H
            // Tableau[1]: 6S
            var state = BuildState(
                tableaus: new[]
                {
                    FaceUpPile(PileType.Tableau, 0, C(Rank.Five, Suit.Heart)),
                    FaceUpPile(PileType.Tableau, 1, C(Rank.Six, Suit.Spade))
                });

            var moves = MoveEnumerator.FindAllMoves(state, _cardService, _rule);
            Assert.IsTrue(moves.Any(m => m.MoveType == HintMoveType.TableauToTableau));
            Assert.IsFalse(moves.Any(m => m.MoveType == HintMoveType.TableauToTableauReveal));
        }

        [Test]
        public void KingOnlyPile_ToEmptyTableau_IsPruned()
        {
            // Tableau[0]: King only (FaceUpFromIndex=0) — entire pile is the King
            // Tableau[1]: empty
            var state = BuildState(
                tableaus: new[]
                {
                    FaceUpPile(PileType.Tableau, 0, C(Rank.King, Suit.Spade)),
                    EmptyPile(PileType.Tableau, 1)
                });

            var moves = MoveEnumerator.FindAllMoves(state, _cardService, _rule);
            // King-to-empty-tableau should be pruned (pointless move)
            Assert.IsFalse(moves.Any(m =>
                m.MoveType == HintMoveType.TableauToTableau
                && m.Request.TargetPileId.Type == PileType.Tableau
                && m.Request.TargetPileId.Index == 1));
        }

        [Test]
        public void KingOnPileWithFaceDown_ToEmptyTableau_IsNotPruned()
        {
            // Tableau[0]: face-down card + King face-up — moving King reveals!
            // Tableau[1]: empty
            var state = BuildState(
                tableaus: new[]
                {
                    Pile(PileType.Tableau, 0, 1, C(Rank.Two, Suit.Club), C(Rank.King, Suit.Spade)),
                    EmptyPile(PileType.Tableau, 1)
                });

            var moves = MoveEnumerator.FindAllMoves(state, _cardService, _rule);
            Assert.IsTrue(moves.Any(m =>
                m.MoveType == HintMoveType.TableauToTableauReveal
                && m.Request.Card.Rank == Rank.King));
        }

        [Test]
        public void StockDraw_WhenStockHasCards_Included()
        {
            var state = BuildState(
                stock: Pile(PileType.Stock, 0, 1, C(Rank.Two, Suit.Heart)));

            var moves = MoveEnumerator.FindAllMoves(state, _cardService, _rule);
            Assert.IsTrue(moves.Any(m => m.MoveType == HintMoveType.StockDraw));
        }

        [Test]
        public void StockRecycle_WhenCanRecycle_Included()
        {
            var state = BuildState(
                waste: FaceUpPile(PileType.Waste, 0, C(Rank.Two, Suit.Heart)));

            _rule.CanRecycleStock = true;
            var moves = MoveEnumerator.FindAllMoves(state, _cardService, _rule);
            Assert.IsTrue(moves.Any(m => m.MoveType == HintMoveType.StockDraw));
        }

        [Test]
        public void StockRecycle_WhenCannotRecycle_NotIncluded()
        {
            var state = BuildState(
                waste: FaceUpPile(PileType.Waste, 0, C(Rank.Two, Suit.Heart)));

            _rule.CanRecycleStock = false;
            var moves = MoveEnumerator.FindAllMoves(state, _cardService, _rule);
            Assert.IsFalse(moves.Any(m => m.MoveType == HintMoveType.StockDraw));
        }

        [Test]
        public void StackMove_QJT_OntoKing_Found()
        {
            // Tableau[0]: Q-J-10 all face-up (red Q, black J, red 10)
            // Tableau[1]: King black
            var state = BuildState(
                tableaus: new[]
                {
                    FaceUpPile(PileType.Tableau, 0,
                        C(Rank.Queen, Suit.Heart),
                        C(Rank.Jack, Suit.Spade),
                        C(Rank.Ten, Suit.Diamond)),
                    FaceUpPile(PileType.Tableau, 1, C(Rank.King, Suit.Club))
                });

            var moves = MoveEnumerator.FindAllMoves(state, _cardService, _rule);
            Assert.IsTrue(moves.Any(m =>
                m.Request.Card.Rank == Rank.Queen
                && m.Request.Count == 3));
        }

        [Test]
        public void FoundationToTableau_Found()
        {
            // Foundation[0]: Ace, Two of hearts
            // Tableau[0]: Three of spades (black)
            var state = BuildState(
                foundations: new[]
                {
                    FaceUpPile(PileType.Foundation, 0, C(Rank.Ace, Suit.Heart), C(Rank.Two, Suit.Heart))
                },
                tableaus: new[]
                {
                    FaceUpPile(PileType.Tableau, 0, C(Rank.Three, Suit.Spade))
                });

            var moves = MoveEnumerator.FindAllMoves(state, _cardService, _rule);
            Assert.IsTrue(moves.Any(m =>
                m.MoveType == HintMoveType.FoundationToTableau
                && m.Request.Card.Rank == Rank.Two));
        }

        [Test]
        public void GetHints_SortedByPriorityDescending()
        {
            // Waste has Ace (→ foundation, priority 90)
            // Tableau has 5H on 6S possible (→ tableau, priority 20)
            var state = BuildState(
                waste: FaceUpPile(PileType.Waste, 0, C(Rank.Ace, Suit.Spade)),
                tableaus: new[]
                {
                    FaceUpPile(PileType.Tableau, 0, C(Rank.Six, Suit.Spade)),
                    FaceUpPile(PileType.Tableau, 1, C(Rank.Five, Suit.Heart))
                });

            var hints = _hintService.GetHints(state);
            Assert.IsTrue(hints.Count >= 2);
            for (int i = 1; i < hints.Count; i++)
                Assert.GreaterOrEqual(hints[i - 1].Priority, hints[i].Priority,
                    $"Hint at index {i - 1} (priority {hints[i - 1].Priority}) should be >= index {i} (priority {hints[i].Priority})");
        }

        [Test]
        public void GetHints_StableAcrossMultipleCalls()
        {
            var state = BuildState(
                waste: FaceUpPile(PileType.Waste, 0, C(Rank.Ace, Suit.Spade)),
                tableaus: new[]
                {
                    FaceUpPile(PileType.Tableau, 0, C(Rank.Six, Suit.Spade)),
                    FaceUpPile(PileType.Tableau, 1, C(Rank.Five, Suit.Heart))
                });

            var hints1 = _hintService.GetHints(state);
            var hints2 = _hintService.GetHints(state);

            Assert.AreEqual(hints1.Count, hints2.Count);
            for (int i = 0; i < hints1.Count; i++)
                Assert.AreEqual(hints1[i].Priority, hints2[i].Priority);
        }

        // ============================================================
        // HasAnyMove
        // ============================================================

        [Test]
        public void HasAnyMove_WhenMoveExists_ReturnsTrue()
        {
            var state = BuildState(
                waste: FaceUpPile(PileType.Waste, 0, C(Rank.Ace, Suit.Spade)));

            Assert.IsTrue(_hintService.HasAnyMove(state));
        }

        [Test]
        public void HasAnyMove_WhenStockDrawAvailable_ReturnsTrue()
        {
            var state = BuildState(
                stock: Pile(PileType.Stock, 0, 1, C(Rank.Two, Suit.Heart)));

            Assert.IsTrue(_hintService.HasAnyMove(state));
        }

        [Test]
        public void HasAnyMove_CompletelyStuck_ReturnsFalse()
        {
            // Construct a state with no valid moves:
            // Stock empty, waste empty, CanRecycleStock=false
            // All tableaus have single face-up cards that can't move anywhere
            // (same-color descending cards so no alternating-color move is possible)
            _rule.CanRecycleStock = false;
            _rule.OnlyKingOnEmptyTableau = true;

            // Red 3 on tableau[0], Red 2 on tableau[1] — same color, can't stack
            // No Aces or Kings, foundations empty
            var state = BuildState(
                tableaus: new[]
                {
                    FaceUpPile(PileType.Tableau, 0, C(Rank.Three, Suit.Heart)),
                    FaceUpPile(PileType.Tableau, 1, C(Rank.Two, Suit.Diamond))
                });

            Assert.IsFalse(_hintService.HasAnyMove(state));
        }

        // ============================================================
        // CanAutoComplete
        // ============================================================

        [Test]
        public void CanAutoComplete_AllFaceUp_StockWasteEmpty_ReturnsTrue()
        {
            var state = BuildState(
                tableaus: new[]
                {
                    FaceUpPile(PileType.Tableau, 0, C(Rank.Ace, Suit.Spade)),
                    FaceUpPile(PileType.Tableau, 1, C(Rank.Two, Suit.Spade))
                });

            Assert.IsTrue(_hintService.CanAutoComplete(state));
        }

        [Test]
        public void CanAutoComplete_FaceDownExists_ReturnsFalse()
        {
            var state = BuildState(
                tableaus: new[]
                {
                    Pile(PileType.Tableau, 0, 1,
                        C(Rank.King, Suit.Club), C(Rank.Ace, Suit.Spade))
                });

            Assert.IsFalse(_hintService.CanAutoComplete(state));
        }

        [Test]
        public void CanAutoComplete_StockHasCards_ReturnsFalse()
        {
            var state = BuildState(
                stock: Pile(PileType.Stock, 0, 1, C(Rank.Two, Suit.Heart)),
                tableaus: new[]
                {
                    FaceUpPile(PileType.Tableau, 0, C(Rank.Ace, Suit.Spade))
                });

            Assert.IsFalse(_hintService.CanAutoComplete(state));
        }

        [Test]
        public void CanAutoComplete_WasteHasCards_ReturnsFalse()
        {
            var state = BuildState(
                waste: FaceUpPile(PileType.Waste, 0, C(Rank.Two, Suit.Heart)),
                tableaus: new[]
                {
                    FaceUpPile(PileType.Tableau, 0, C(Rank.Ace, Suit.Spade))
                });

            Assert.IsFalse(_hintService.CanAutoComplete(state));
        }

        [Test]
        public void CanAutoComplete_AlreadyWon_ReturnsFalse()
        {
            // All tableaus empty, all cards on foundations — nothing to auto-complete
            var state = BuildState();
            Assert.IsFalse(_hintService.CanAutoComplete(state));
        }

        // ============================================================
        // GetAutoCompleteMoves
        // ============================================================

        [Test]
        public void GetAutoCompleteMoves_SimpleScenario_ProducesCorrectSequence()
        {
            // Tableau[0]: Ace of Spades
            // Tableau[1]: Two of Spades
            // Expected: Ace → Foundation, then Two → Foundation
            var state = BuildState(
                tableaus: new[]
                {
                    FaceUpPile(PileType.Tableau, 0, C(Rank.Ace, Suit.Spade)),
                    FaceUpPile(PileType.Tableau, 1, C(Rank.Two, Suit.Spade))
                });

            var moves = _hintService.GetAutoCompleteMoves(state);
            Assert.AreEqual(2, moves.Count);
            Assert.AreEqual(Rank.Ace, moves[0].Request.Card.Rank);
            Assert.AreEqual(Rank.Two, moves[1].Request.Card.Rank);
        }

        [Test]
        public void GetAutoCompleteMoves_MultiSuit_InterleavesCorrectly()
        {
            // Tableau[0]: Ace♠ (top, can go to foundation immediately)
            // Tableau[1]: Ace♥ (top, can go to foundation immediately)
            // Tableau[2]: Two♠ (needs Ace♠ on foundation first)
            var state = BuildState(
                tableaus: new[]
                {
                    FaceUpPile(PileType.Tableau, 0, C(Rank.Ace, Suit.Spade)),
                    FaceUpPile(PileType.Tableau, 1, C(Rank.Ace, Suit.Heart)),
                    FaceUpPile(PileType.Tableau, 2, C(Rank.Two, Suit.Spade))
                });

            var moves = _hintService.GetAutoCompleteMoves(state);
            // Should move all 3 cards: both Aces first, then Two♠
            Assert.AreEqual(3, moves.Count);

            // All moved cards should be foundation-bound
            foreach (var move in moves)
                Assert.AreEqual(PileType.Foundation, move.Request.TargetPileId.Type);
        }

        [Test]
        public void GetAutoCompleteMoves_WhenCannotAutoComplete_ReturnsEmpty()
        {
            var state = BuildState(
                stock: Pile(PileType.Stock, 0, 1, C(Rank.Two, Suit.Heart)));

            var moves = _hintService.GetAutoCompleteMoves(state);
            Assert.AreEqual(0, moves.Count);
        }

        // ============================================================
        // Spider-specific hint rules
        // ============================================================

        private StubDealRule SpiderRule() => new StubDealRule
        {
            TableauCount = 3,
            FoundationCount = 2,
            HasWaste = false,
            CanRecycleStock = false,
            StockDealsToTableau = true,
            OnlyKingOnEmptyTableau = false,
            RunRuleOverride = TableauRunRule.SameSuit,
            DropRuleOverride = TableauDropRule.AnySuit,
            StockDealRequiresNoEmptyColumnOverride = true,
            AutoCollectCompletedRunsOverride = true,
        };

        [Test]
        public void Spider_StockHint_SuppressedWhileColumnEmpty()
        {
            var spiderRule = SpiderRule();
            var spiderCardService = new SolitaireCardService();
            spiderCardService.Initialize(spiderRule);
            var spiderHintService = new HintService(spiderCardService);
            spiderHintService.Initialize(spiderRule);

            // t0 = ♠K only, t1 = EMPTY, t2 = ♠K only — equal ranks can't stack under AnySuit,
            // so no tableau-to-tableau moves exist; only the stock draw matters for this test.
            var stateWithEmpty = new TableState(
                Pile(PileType.Stock, 0, 1, C(Rank.Two, Suit.Heart)),
                EmptyPile(PileType.Waste),
                new System.Collections.Generic.List<PileState>
                {
                    EmptyPile(PileType.Foundation, 0),
                    EmptyPile(PileType.Foundation, 1),
                },
                new System.Collections.Generic.List<PileState>
                {
                    FaceUpPile(PileType.Tableau, 0, C(Rank.King, Suit.Spade)),
                    EmptyPile(PileType.Tableau, 1),
                    FaceUpPile(PileType.Tableau, 2, C(Rank.King, Suit.Spade)),
                });

            var hintsWithEmpty = spiderHintService.GetHints(stateWithEmpty);
            // Spider guard: dealing is blocked while any column is empty — no StockDraw hint
            Assert.IsFalse(hintsWithEmpty.Any(m => m.MoveType == HintMoveType.StockDraw),
                "StockDraw must not be offered while a Spider column is empty");

            // Positive guard: stock draw IS offered once all columns are filled
            var stateAllFilled = new TableState(
                Pile(PileType.Stock, 0, 1, C(Rank.Two, Suit.Heart)),
                EmptyPile(PileType.Waste),
                new System.Collections.Generic.List<PileState>
                {
                    EmptyPile(PileType.Foundation, 0),
                    EmptyPile(PileType.Foundation, 1),
                },
                new System.Collections.Generic.List<PileState>
                {
                    FaceUpPile(PileType.Tableau, 0, C(Rank.King, Suit.Spade)),
                    FaceUpPile(PileType.Tableau, 1, C(Rank.Queen, Suit.Spade)),
                    FaceUpPile(PileType.Tableau, 2, C(Rank.King, Suit.Spade)),
                });

            var hintsAllFilled = spiderHintService.GetHints(stateAllFilled);
            Assert.IsTrue(hintsAllFilled.Any(m => m.MoveType == HintMoveType.StockDraw),
                "StockDraw must be offered when all Spider columns are filled");
        }

        [Test]
        public void Spider_CanAutoComplete_AlwaysFalse()
        {
            var spiderRule = SpiderRule();
            var spiderCardService = new SolitaireCardService();
            spiderCardService.Initialize(spiderRule);
            var spiderHintService = new HintService(spiderCardService);
            spiderHintService.Initialize(spiderRule);

            // State satisfies every Klondike auto-complete precondition:
            // stock empty, waste empty, all tableau cards face-up, at least one card present.
            var state = new TableState(
                EmptyPile(PileType.Stock),
                EmptyPile(PileType.Waste),
                new System.Collections.Generic.List<PileState>
                {
                    EmptyPile(PileType.Foundation, 0),
                    EmptyPile(PileType.Foundation, 1),
                },
                new System.Collections.Generic.List<PileState>
                {
                    FaceUpPile(PileType.Tableau, 0, C(Rank.Ace, Suit.Spade)),
                    FaceUpPile(PileType.Tableau, 1, C(Rank.Two, Suit.Spade)),
                    EmptyPile(PileType.Tableau, 2),
                });

            // Spider: auto-complete must always return false (runs self-collect; Klondike endgame N/A)
            Assert.IsFalse(spiderHintService.CanAutoComplete(state),
                "Spider should never trigger Klondike auto-complete");

            // Contrast: the same state under the default Klondike rule returns true
            Assert.IsTrue(_hintService.CanAutoComplete(state),
                "Klondike should return true to confirm the state is otherwise valid");
        }

        [Test]
        public void Spider_FoundationToTableau_NeverOffered()
        {
            var spiderRule = SpiderRule();
            var spiderCardService = new SolitaireCardService();
            spiderCardService.Initialize(spiderRule);
            var spiderHintService = new HintService(spiderCardService);
            spiderHintService.Initialize(spiderRule);

            // Foundation0 holds a full collected ♠K..A run (13 cards — indices 0..12)
            var fullRun = new System.Collections.Generic.List<PlayingCard>();
            for (int r = 1; r <= 13; r++)
                fullRun.Add(C((Rank)r, Suit.Spade));

            var state = new TableState(
                EmptyPile(PileType.Stock),
                EmptyPile(PileType.Waste),
                new System.Collections.Generic.List<PileState>
                {
                    new PileState(new PileId(PileType.Foundation, 0), fullRun, 0),
                    EmptyPile(PileType.Foundation, 1),
                },
                new System.Collections.Generic.List<PileState>
                {
                    FaceUpPile(PileType.Tableau, 0, C(Rank.King, Suit.Heart)),
                    FaceUpPile(PileType.Tableau, 1, C(Rank.Queen, Suit.Heart)),
                    EmptyPile(PileType.Tableau, 2),
                });

            var hints = spiderHintService.GetHints(state);
            Assert.IsFalse(hints.Any(m => m.MoveType == HintMoveType.FoundationToTableau),
                "Spider completed runs on foundation must never be offered back to tableau");
        }
    }
}
