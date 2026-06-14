using System.Collections.Generic;
using NUnit.Framework;
using Model.Card;
using Model.Game;
using Service.CardService;

namespace Tests.EditMode
{
    [TestFixture]
    public class SolitaireCardServiceTests
    {
        private SolitaireCardService _service;

        [SetUp]
        public void SetUp()
        {
            _service = new SolitaireCardService();
            _service.Initialize(new StubDealRule());
        }

        // --- Helpers ---

        private static PileState EmptyPile(PileType type, int idx = 0) =>
            new PileState(new PileId(type, idx), new List<PlayingCard>(), 0);

        private static PileState PileWith(PileType type, int idx, params PlayingCard[] cards) =>
            new PileState(new PileId(type, idx), new List<PlayingCard>(cards), 0);

        private static TableState StateWith(PileState targetPile)
        {
            var stock = targetPile.Id.Type == PileType.Stock ? targetPile : EmptyPile(PileType.Stock);
            var waste = targetPile.Id.Type == PileType.Waste ? targetPile : EmptyPile(PileType.Waste);

            var foundations = new List<PileState>();
            for (int i = 0; i < 4; i++)
                foundations.Add(targetPile.Id.Type == PileType.Foundation && targetPile.Id.Index == i
                    ? targetPile
                    : EmptyPile(PileType.Foundation, i));

            var tableaus = new List<PileState>();
            for (int i = 0; i < 7; i++)
                tableaus.Add(targetPile.Id.Type == PileType.Tableau && targetPile.Id.Index == i
                    ? targetPile
                    : EmptyPile(PileType.Tableau, i));

            return new TableState(stock, waste, foundations, tableaus);
        }

        private static MoveCardRequest Req(PlayingCard card, PileType srcType, int srcIdx, PileType tgtType, int tgtIdx) =>
            new MoveCardRequest(card, new PileId(srcType, srcIdx), 0, new PileId(tgtType, tgtIdx));

        // --- Base validation ---

        [Test]
        public void TryMove_NullCard_Fails()
        {
            var state = StateWith(EmptyPile(PileType.Tableau, 0));
            var req = new MoveCardRequest(null, new PileId(PileType.Stock, 0), 0, new PileId(PileType.Tableau, 0));
            Assert.IsFalse(_service.TryMove(req, state).IsSuccess);
        }

        [Test]
        public void TryMove_SamePile_Fails()
        {
            var card = new PlayingCard(Rank.Nine, Suit.Heart);
            var state = StateWith(EmptyPile(PileType.Tableau, 0));
            var req = new MoveCardRequest(card, new PileId(PileType.Tableau, 0), 0, new PileId(PileType.Tableau, 0));
            Assert.IsFalse(_service.TryMove(req, state).IsSuccess);
        }

        // --- Tableau ---

        [Test]
        public void Tableau_RedNine_OnBlackTen_Succeeds()
        {
            var target = PileWith(PileType.Tableau, 0, new PlayingCard(Rank.Ten, Suit.Spade));
            var state = StateWith(target);
            var req = Req(new PlayingCard(Rank.Nine, Suit.Heart), PileType.Stock, 0, PileType.Tableau, 0);
            Assert.IsTrue(_service.TryMove(req, state).IsSuccess);
        }

        [Test]
        public void Tableau_BlackNine_OnRedTen_Succeeds()
        {
            var target = PileWith(PileType.Tableau, 0, new PlayingCard(Rank.Ten, Suit.Heart));
            var state = StateWith(target);
            var req = Req(new PlayingCard(Rank.Nine, Suit.Club), PileType.Stock, 0, PileType.Tableau, 0);
            Assert.IsTrue(_service.TryMove(req, state).IsSuccess);
        }

        [Test]
        public void Tableau_SameColor_Fails()
        {
            var target = PileWith(PileType.Tableau, 0, new PlayingCard(Rank.Ten, Suit.Heart));
            var state = StateWith(target);
            var req = Req(new PlayingCard(Rank.Nine, Suit.Diamond), PileType.Stock, 0, PileType.Tableau, 0);
            Assert.IsFalse(_service.TryMove(req, state).IsSuccess);
        }

        [Test]
        public void Tableau_NonConsecutiveRank_Fails()
        {
            var target = PileWith(PileType.Tableau, 0, new PlayingCard(Rank.Ten, Suit.Spade));
            var state = StateWith(target);
            var req = Req(new PlayingCard(Rank.Eight, Suit.Heart), PileType.Stock, 0, PileType.Tableau, 0);
            Assert.IsFalse(_service.TryMove(req, state).IsSuccess);
        }

        [Test]
        public void Tableau_King_OnEmpty_Succeeds()
        {
            var state = StateWith(EmptyPile(PileType.Tableau, 0));
            var req = Req(new PlayingCard(Rank.King, Suit.Heart), PileType.Stock, 0, PileType.Tableau, 0);
            Assert.IsTrue(_service.TryMove(req, state).IsSuccess);
        }

        [Test]
        public void Tableau_NonKing_OnEmpty_Fails()
        {
            var state = StateWith(EmptyPile(PileType.Tableau, 0));
            var req = Req(new PlayingCard(Rank.Queen, Suit.Heart), PileType.Stock, 0, PileType.Tableau, 0);
            Assert.IsFalse(_service.TryMove(req, state).IsSuccess);
        }

        // --- Foundation ---

        [Test]
        public void Foundation_Ace_OnEmpty_Succeeds()
        {
            var state = StateWith(EmptyPile(PileType.Foundation, 0));
            var req = Req(new PlayingCard(Rank.Ace, Suit.Heart), PileType.Stock, 0, PileType.Foundation, 0);
            Assert.IsTrue(_service.TryMove(req, state).IsSuccess);
        }

        [Test]
        public void Foundation_NonAce_OnEmpty_Fails()
        {
            var state = StateWith(EmptyPile(PileType.Foundation, 0));
            var req = Req(new PlayingCard(Rank.Two, Suit.Heart), PileType.Stock, 0, PileType.Foundation, 0);
            Assert.IsFalse(_service.TryMove(req, state).IsSuccess);
        }

        [Test]
        public void Foundation_SameSuit_AscendingOrder_Succeeds()
        {
            var target = PileWith(PileType.Foundation, 0, new PlayingCard(Rank.Ace, Suit.Heart));
            var state = StateWith(target);
            var req = Req(new PlayingCard(Rank.Two, Suit.Heart), PileType.Stock, 0, PileType.Foundation, 0);
            Assert.IsTrue(_service.TryMove(req, state).IsSuccess);
        }

        [Test]
        public void Foundation_DifferentSuit_Fails()
        {
            var target = PileWith(PileType.Foundation, 0, new PlayingCard(Rank.Ace, Suit.Heart));
            var state = StateWith(target);
            var req = Req(new PlayingCard(Rank.Two, Suit.Diamond), PileType.Stock, 0, PileType.Foundation, 0);
            Assert.IsFalse(_service.TryMove(req, state).IsSuccess);
        }

        [Test]
        public void Foundation_NonAscendingRank_Fails()
        {
            var target = PileWith(PileType.Foundation, 0, new PlayingCard(Rank.Ace, Suit.Heart));
            var state = StateWith(target);
            var req = Req(new PlayingCard(Rank.Three, Suit.Heart), PileType.Stock, 0, PileType.Foundation, 0);
            Assert.IsFalse(_service.TryMove(req, state).IsSuccess);
        }

        // --- EastHaven variant: OnlyKingOnEmptyTableau = false ---

        [Test]
        public void Tableau_NonKing_OnEmpty_WhenKingConstraintDisabled_Succeeds()
        {
            var service = new SolitaireCardService();
            service.Initialize(new StubDealRule { OnlyKingOnEmptyTableau = false });
            var state = StateWith(EmptyPile(PileType.Tableau, 0));
            var req = Req(new PlayingCard(Rank.Five, Suit.Heart), PileType.Stock, 0, PileType.Tableau, 0);
            Assert.IsTrue(service.TryMove(req, state).IsSuccess);
        }

        [Test]
        public void Tableau_Ace_OnEmpty_WhenKingConstraintDisabled_Succeeds()
        {
            var service = new SolitaireCardService();
            service.Initialize(new StubDealRule { OnlyKingOnEmptyTableau = false });
            var state = StateWith(EmptyPile(PileType.Tableau, 0));
            var req = Req(new PlayingCard(Rank.Ace, Suit.Spade), PileType.Stock, 0, PileType.Tableau, 0);
            Assert.IsTrue(service.TryMove(req, state).IsSuccess);
        }

        // --- Multi-card sequence validation ---

        private static TableState StateWithSourceAndTarget(PileState source, PileState target)
        {
            var stock = EmptyPile(PileType.Stock);
            var waste = EmptyPile(PileType.Waste);

            var foundations = new List<PileState>();
            for (int i = 0; i < 4; i++)
                foundations.Add(target.Id.Type == PileType.Foundation && target.Id.Index == i
                    ? target
                    : EmptyPile(PileType.Foundation, i));

            var tableaus = new List<PileState>();
            for (int i = 0; i < 7; i++)
            {
                if (source.Id.Type == PileType.Tableau && source.Id.Index == i)
                    tableaus.Add(source);
                else if (target.Id.Type == PileType.Tableau && target.Id.Index == i)
                    tableaus.Add(target);
                else
                    tableaus.Add(EmptyPile(PileType.Tableau, i));
            }

            return new TableState(stock, waste, foundations, tableaus);
        }

        [Test]
        public void Tableau_MultiCardMove_ValidSequence_Succeeds()
        {
            var source = PileWith(PileType.Tableau, 0,
                new PlayingCard(Rank.Seven, Suit.Club),  // black 7
                new PlayingCard(Rank.Six, Suit.Heart),   // red 6
                new PlayingCard(Rank.Five, Suit.Spade)); // black 5
            var target = PileWith(PileType.Tableau, 1, new PlayingCard(Rank.Eight, Suit.Diamond)); // red 8
            var state = StateWithSourceAndTarget(source, target);

            var req = new MoveCardRequest(source.Cards[0], new PileId(PileType.Tableau, 0), 0,
                new PileId(PileType.Tableau, 1), 3);
            Assert.IsTrue(_service.TryMove(req, state).IsSuccess);
        }

        [Test]
        public void Tableau_MultiCardMove_NonAlternatingColor_Fails()
        {
            var source = PileWith(PileType.Tableau, 0,
                new PlayingCard(Rank.Seven, Suit.Club),    // black 7
                new PlayingCard(Rank.Six, Suit.Spade),     // black 6 — same color, invalid
                new PlayingCard(Rank.Five, Suit.Diamond)); // red 5
            var target = PileWith(PileType.Tableau, 1, new PlayingCard(Rank.Eight, Suit.Heart));
            var state = StateWithSourceAndTarget(source, target);

            var req = new MoveCardRequest(source.Cards[0], new PileId(PileType.Tableau, 0), 0,
                new PileId(PileType.Tableau, 1), 3);
            Assert.IsFalse(_service.TryMove(req, state).IsSuccess);
        }

        [Test]
        public void Tableau_MultiCardMove_NonDescendingRank_Fails()
        {
            var source = PileWith(PileType.Tableau, 0,
                new PlayingCard(Rank.Seven, Suit.Club),    // black 7
                new PlayingCard(Rank.Six, Suit.Heart),     // red 6
                new PlayingCard(Rank.Four, Suit.Spade));   // black 4 — skipped 5, invalid
            var target = PileWith(PileType.Tableau, 1, new PlayingCard(Rank.Eight, Suit.Diamond));
            var state = StateWithSourceAndTarget(source, target);

            var req = new MoveCardRequest(source.Cards[0], new PileId(PileType.Tableau, 0), 0,
                new PileId(PileType.Tableau, 1), 3);
            Assert.IsFalse(_service.TryMove(req, state).IsSuccess);
        }

        [Test]
        public void Tableau_MultiCardMove_OnEmpty_WhenKingConstraintDisabled_ValidatesSequence()
        {
            var service = new SolitaireCardService();
            service.Initialize(new StubDealRule { OnlyKingOnEmptyTableau = false });

            var validSource = PileWith(PileType.Tableau, 0,
                new PlayingCard(Rank.Five, Suit.Club),
                new PlayingCard(Rank.Four, Suit.Heart),
                new PlayingCard(Rank.Three, Suit.Spade));
            var emptyTarget = EmptyPile(PileType.Tableau, 1);
            var state = StateWithSourceAndTarget(validSource, emptyTarget);
            var validReq = new MoveCardRequest(validSource.Cards[0], new PileId(PileType.Tableau, 0), 0,
                new PileId(PileType.Tableau, 1), 3);
            Assert.IsTrue(service.TryMove(validReq, state).IsSuccess,
                "Valid sequence to empty tableau should pass when King constraint disabled.");

            var invalidSource = PileWith(PileType.Tableau, 0,
                new PlayingCard(Rank.Five, Suit.Club),
                new PlayingCard(Rank.Four, Suit.Spade),   // same color — invalid
                new PlayingCard(Rank.Three, Suit.Heart));
            var state2 = StateWithSourceAndTarget(invalidSource, emptyTarget);
            var invalidReq = new MoveCardRequest(invalidSource.Cards[0], new PileId(PileType.Tableau, 0), 0,
                new PileId(PileType.Tableau, 1), 3);
            Assert.IsFalse(service.TryMove(invalidReq, state2).IsSuccess,
                "Invalid sequence must be rejected even when target is an unrestricted empty tableau.");
        }

        // --- Spider variant ---

        private static StubDealRule SpiderRule() => new StubDealRule
        {
            HasWaste = false,
            CanRecycleStock = false,
            StockDealsToTableau = true,
            OnlyKingOnEmptyTableau = false,
            RunRuleOverride = TableauRunRule.SameSuit,
            DropRuleOverride = TableauDropRule.AnySuit,
            AutoCollectCompletedRunsOverride = true,
        };

        [Test]
        public void Spider_SameSuitDescendingRun_PickupAllowed()
        {
            var service = new SolitaireCardService();
            service.Initialize(SpiderRule());

            var source = PileWith(PileType.Tableau, 0,
                new PlayingCard(Rank.Seven, Suit.Spade),
                new PlayingCard(Rank.Six, Suit.Spade),
                new PlayingCard(Rank.Five, Suit.Spade));
            var target = PileWith(PileType.Tableau, 1, new PlayingCard(Rank.Eight, Suit.Spade));
            var state = StateWithSourceAndTarget(source, target);

            var req = new MoveCardRequest(source.Cards[0], new PileId(PileType.Tableau, 0), 0,
                new PileId(PileType.Tableau, 1), 3);
            Assert.IsTrue(service.TryMove(req, state).IsSuccess);
        }

        [Test]
        public void Spider_MixedSuitRun_PickupRejected()
        {
            var service = new SolitaireCardService();
            service.Initialize(SpiderRule());

            var source = PileWith(PileType.Tableau, 0,
                new PlayingCard(Rank.Seven, Suit.Spade),
                new PlayingCard(Rank.Six, Suit.Heart),   // wrong suit — breaks same-suit run
                new PlayingCard(Rank.Five, Suit.Spade));
            var target = PileWith(PileType.Tableau, 1, new PlayingCard(Rank.Eight, Suit.Spade));
            var state = StateWithSourceAndTarget(source, target);

            var req = new MoveCardRequest(source.Cards[0], new PileId(PileType.Tableau, 0), 0,
                new PileId(PileType.Tableau, 1), 3);
            Assert.IsFalse(service.TryMove(req, state).IsSuccess);
        }

        [Test]
        public void Spider_DropOnSameColor_OneRankLower_Allowed()
        {
            // AnySuit drop rule: ♠6 onto ♠7 is legal even though same color/suit.
            var spiderService = new SolitaireCardService();
            spiderService.Initialize(SpiderRule());

            var spiderSource = PileWith(PileType.Tableau, 0, new PlayingCard(Rank.Six, Suit.Spade));
            var spiderTarget = PileWith(PileType.Tableau, 1, new PlayingCard(Rank.Seven, Suit.Spade));
            var spiderState = StateWithSourceAndTarget(spiderSource, spiderTarget);
            var spiderReq = new MoveCardRequest(
                new PlayingCard(Rank.Six, Suit.Spade),
                new PileId(PileType.Tableau, 0), 0,
                new PileId(PileType.Tableau, 1));
            Assert.IsTrue(spiderService.TryMove(spiderReq, spiderState).IsSuccess,
                "AnySuit drop should allow same-suit placement.");

            // Same move must fail under the default AlternatingColor rule (regression contrast).
            var klondikeService = new SolitaireCardService();
            klondikeService.Initialize(new StubDealRule());
            var klondikeSource = PileWith(PileType.Tableau, 0, new PlayingCard(Rank.Six, Suit.Spade));
            var klondikeTarget = PileWith(PileType.Tableau, 1, new PlayingCard(Rank.Seven, Suit.Spade));
            var klondikeState = StateWithSourceAndTarget(klondikeSource, klondikeTarget);
            var klondikeReq = new MoveCardRequest(
                new PlayingCard(Rank.Six, Suit.Spade),
                new PileId(PileType.Tableau, 0), 0,
                new PileId(PileType.Tableau, 1));
            Assert.IsFalse(klondikeService.TryMove(klondikeReq, klondikeState).IsSuccess,
                "AlternatingColor drop must still reject same-color placement.");
        }

        [Test]
        public void Spider_FoundationDirectDrop_Rejected()
        {
            // AutoCollectCompletedRuns: foundations are write-only — manual drops in are illegal.
            var service = new SolitaireCardService();
            service.Initialize(SpiderRule());

            var state = StateWith(EmptyPile(PileType.Foundation, 0));
            var req = Req(new PlayingCard(Rank.Ace, Suit.Spade), PileType.Stock, 0, PileType.Foundation, 0);
            Assert.IsFalse(service.TryMove(req, state).IsSuccess);
        }

        [Test]
        public void Spider_FoundationSource_Rejected()
        {
            // AutoCollectCompletedRuns: pulling a card out of a foundation is also illegal.
            var service = new SolitaireCardService();
            service.Initialize(SpiderRule());

            // Build state with the Ace genuinely seated in Foundation(0) as source,
            // so the veto fires for the right reason rather than "pile not found".
            var foundationSource = PileWith(PileType.Foundation, 0, new PlayingCard(Rank.Ace, Suit.Spade));
            var foundations = new List<PileState>();
            for (int i = 0; i < 4; i++)
                foundations.Add(i == 0 ? foundationSource : EmptyPile(PileType.Foundation, i));
            var tableaus = new List<PileState>();
            for (int i = 0; i < 7; i++)
                tableaus.Add(EmptyPile(PileType.Tableau, i));
            var state = new TableState(EmptyPile(PileType.Stock), EmptyPile(PileType.Waste), foundations, tableaus);

            var req = new MoveCardRequest(
                new PlayingCard(Rank.Ace, Suit.Spade),
                new PileId(PileType.Foundation, 0), 0,
                new PileId(PileType.Tableau, 0));
            Assert.IsFalse(service.TryMove(req, state).IsSuccess,
                "foundation veto must fire");
        }

        // --- IsValidRunPickup ---

        [Test]
        public void IsValidRunPickup_SpiderRule_SameSuitDescending()
        {
            var svc = new SolitaireCardService();
            svc.Initialize(SpiderRule());

            Assert.IsTrue(svc.IsValidRunPickup(new[]
            {
                new PlayingCard(Rank.Seven, Suit.Spade),
                new PlayingCard(Rank.Six, Suit.Spade),
                new PlayingCard(Rank.Five, Suit.Spade),
            }), "same-suit descending run");

            Assert.IsFalse(svc.IsValidRunPickup(new[]
            {
                new PlayingCard(Rank.Seven, Suit.Spade),
                new PlayingCard(Rank.Six, Suit.Heart),
                new PlayingCard(Rank.Five, Suit.Spade),
            }), "mixed suit rejected");
        }

        [Test]
        public void IsValidRunPickup_KlondikeRule_AlternatingDescending()
        {
            var svc = new SolitaireCardService();
            svc.Initialize(new StubDealRule());

            Assert.IsTrue(svc.IsValidRunPickup(new[]
            {
                new PlayingCard(Rank.Seven, Suit.Spade),
                new PlayingCard(Rank.Six, Suit.Heart),
                new PlayingCard(Rank.Five, Suit.Spade),
            }), "alternating descending run");

            Assert.IsFalse(svc.IsValidRunPickup(new[]
            {
                new PlayingCard(Rank.Seven, Suit.Spade),
                new PlayingCard(Rank.Six, Suit.Club),
            }), "same color rejected under Klondike");
        }
    }
}
