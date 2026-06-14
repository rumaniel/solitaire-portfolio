using System;
using NUnit.Framework;
using Model.Board;
using Model.Game;
using Service.BoardGameService;
using Service.GameService;

namespace Tests.EditMode
{
    /// <summary>
    /// Correctness tests for <see cref="BoardSolvableSeedResolver"/>.
    /// Covers determinism, proven-result replay, and wrong-game-type guard.
    /// </summary>
    [TestFixture]
    public class BoardSolvableSeedResolverTests
    {
        private BoardLayout triPeaksLayout;
        private BoardLayout pyramidLayout;

        [SetUp]
        public void SetUp()
        {
            triPeaksLayout = TriPeaksLayoutFactory.Create();
            pyramidLayout = PyramidLayoutFactory.Create();
        }

        // ---- determinism ----

        [Test]
        public void Resolve_TriPeaks_SameInputSeed_IsDeterministic()
        {
            var first = BoardSolvableSeedResolver.Resolve(99, GameType.TriPeaks, triPeaksLayout);
            var second = BoardSolvableSeedResolver.Resolve(99, GameType.TriPeaks, triPeaksLayout);

            Assert.AreEqual(first.Seed, second.Seed, "TriPeaks: same input must yield same resolved seed.");
            Assert.AreEqual(first.Attempts, second.Attempts);
            Assert.AreEqual(first.Proven, second.Proven);
        }

        [Test]
        public void Resolve_Pyramid_SameInputSeed_IsDeterministic()
        {
            var first = BoardSolvableSeedResolver.Resolve(77, GameType.Pyramid, pyramidLayout);
            var second = BoardSolvableSeedResolver.Resolve(77, GameType.Pyramid, pyramidLayout);

            Assert.AreEqual(first.Seed, second.Seed, "Pyramid: same input must yield same resolved seed.");
            Assert.AreEqual(first.Attempts, second.Attempts);
            Assert.AreEqual(first.Proven, second.Proven);
        }

        // ---- proven result replays as solvable ----

        [Test]
        public void Resolve_TriPeaks_ProvenResult_IsSolvableOnReplay()
        {
            // 3 distinct seeds; skip if none prove solvable within the test budget.
            int proven = 0;
            for (int inputSeed = 0; inputSeed < 3; inputSeed++)
            {
                var resolved = BoardSolvableSeedResolver.Resolve(inputSeed, GameType.TriPeaks, triPeaksLayout);
                if (!resolved.Proven) continue;
                proven++;

                var state = BoardSolvableSeedResolver.BuildDeal(resolved.Seed, GameType.TriPeaks, triPeaksLayout);
                var replay = TriPeaksSolver.Solve(state, triPeaksLayout, stateBudget: 50_000);

                Assert.IsTrue(replay.Solved,
                    $"TriPeaks resolved seed {resolved.Seed} (input {inputSeed}) must be solvable on replay.");
            }
            // At ~85% proof rate per attempt we expect at least one proven result across 3 inputs.
            Assert.Greater(proven, 0, "Expected at least one TriPeaks deal to be proven solvable across 3 seeds.");
        }

        [Test]
        public void Resolve_Pyramid_ProvenResult_IsSolvableOnReplay()
        {
            int proven = 0;
            for (int inputSeed = 0; inputSeed < 3; inputSeed++)
            {
                var resolved = BoardSolvableSeedResolver.Resolve(inputSeed, GameType.Pyramid, pyramidLayout);
                if (!resolved.Proven) continue;
                proven++;

                var state = BoardSolvableSeedResolver.BuildDeal(resolved.Seed, GameType.Pyramid, pyramidLayout);
                var replay = PyramidSolver.Solve(state, pyramidLayout, maxRecycles: 3, stateBudget: 200_000);

                Assert.IsTrue(replay.Solved,
                    $"Pyramid resolved seed {resolved.Seed} (input {inputSeed}) must be solvable on replay.");
            }
            Assert.Greater(proven, 0, "Expected at least one Pyramid deal to be proven solvable across 3 seeds.");
        }

        // ---- wrong game type throws ----

        [Test]
        public void Resolve_UnsupportedGameType_ThrowsArgumentException()
        {
            // GameType.Klondike is not a board-game type handled by this resolver.
            Assert.Throws<ArgumentException>(() =>
                BoardSolvableSeedResolver.Resolve(0, GameType.Klondike, pyramidLayout));
        }

        [Test]
        public void Resolve_NullLayout_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                BoardSolvableSeedResolver.Resolve(0, GameType.TriPeaks, null));
        }
    }
}
