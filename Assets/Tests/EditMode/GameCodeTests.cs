using NUnit.Framework;
using Model.Game;
using Service.GameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class GameCodeTests
    {
        // --- Encode ---

        [Test]
        public void Encode_Klondike_CorrectPrefix()
        {
            var code = GameCode.Encode(GameType.Klondike, 0);
            Assert.IsTrue(code.StartsWith("KLO-"), $"Expected 'KLO-' prefix, got: {code}");
        }

        [Test]
        public void Encode_Easthaven_CorrectPrefix()
        {
            var code = GameCode.Encode(GameType.Easthaven, 0);
            Assert.IsTrue(code.StartsWith("EAS-"), $"Expected 'EAS-' prefix, got: {code}");
        }

        [Test]
        public void Encode_PositiveSeed_8CharHex()
        {
            var code = GameCode.Encode(GameType.Klondike, 255);
            Assert.AreEqual("KLO-000000FF", code);
        }

        [Test]
        public void Encode_NegativeSeed_HandledCorrectly()
        {
            var code = GameCode.Encode(GameType.Klondike, -1);
            // -1 as uint = 0xFFFFFFFF
            Assert.AreEqual("KLO-FFFFFFFF", code);
        }

        [Test]
        public void Encode_MaxIntSeed()
        {
            var code = GameCode.Encode(GameType.Klondike, int.MaxValue);
            Assert.AreEqual("KLO-7FFFFFFF", code);
        }

        [Test]
        public void Encode_MinIntSeed()
        {
            var code = GameCode.Encode(GameType.Klondike, int.MinValue);
            Assert.AreEqual("KLO-80000000", code);
        }

        // --- Decode ---

        [Test]
        public void Decode_ValidKlondike_Success()
        {
            var result = GameCode.Decode("KLO-000000FF");
            Assert.IsNotNull(result);
            Assert.AreEqual(GameType.Klondike, result.Value.gameType);
            Assert.AreEqual(255, result.Value.seed);
        }

        [Test]
        public void Decode_ValidEasthaven_Success()
        {
            var result = GameCode.Decode("EAS-00001234");
            Assert.IsNotNull(result);
            Assert.AreEqual(GameType.Easthaven, result.Value.gameType);
            Assert.AreEqual(0x1234, result.Value.seed);
        }

        [Test]
        public void Decode_CaseInsensitive()
        {
            var result = GameCode.Decode("klo-000000ff");
            Assert.IsNotNull(result);
            Assert.AreEqual(GameType.Klondike, result.Value.gameType);
            Assert.AreEqual(255, result.Value.seed);
        }

        [Test]
        public void Decode_WithWhitespace_Trimmed()
        {
            var result = GameCode.Decode("  KLO-000000FF  ");
            Assert.IsNotNull(result);
            Assert.AreEqual(255, result.Value.seed);
        }

        [Test]
        public void Decode_NegativeSeedRoundtrip()
        {
            var result = GameCode.Decode("KLO-FFFFFFFF");
            Assert.IsNotNull(result);
            Assert.AreEqual(-1, result.Value.seed);
        }

        // --- Decode Invalid ---

        [Test]
        public void Decode_Null_ReturnsNull()
        {
            Assert.IsNull(GameCode.Decode(null));
        }

        [Test]
        public void Decode_Empty_ReturnsNull()
        {
            Assert.IsNull(GameCode.Decode(""));
        }

        [Test]
        public void Decode_NoDash_ReturnsNull()
        {
            Assert.IsNull(GameCode.Decode("KLO000000FF"));
        }

        [Test]
        public void Decode_UnknownPrefix_ReturnsNull()
        {
            Assert.IsNull(GameCode.Decode("XYZ-000000FF"));
        }

        [Test]
        public void Decode_InvalidHex_ReturnsNull()
        {
            Assert.IsNull(GameCode.Decode("KLO-GHIJKLMN"));
        }

        [Test]
        public void Decode_EmptyHex_ReturnsNull()
        {
            Assert.IsNull(GameCode.Decode("KLO-"));
        }

        [Test]
        public void Decode_EmptyPrefix_ReturnsNull()
        {
            Assert.IsNull(GameCode.Decode("-000000FF"));
        }

        [Test]
        public void Decode_ShortHex_ReturnsNull()
        {
            // Encode always produces 8 hex chars; shorter input should be rejected
            Assert.IsNull(GameCode.Decode("KLO-FF"));
        }

        [Test]
        public void Decode_LongHex_ReturnsNull()
        {
            Assert.IsNull(GameCode.Decode("KLO-000000FF00"));
        }

        // --- Roundtrip ---

        [Test]
        public void Roundtrip_Klondike_PositiveSeed()
        {
            int seed = 42;
            var code = GameCode.Encode(GameType.Klondike, seed);
            var result = GameCode.Decode(code);
            Assert.IsNotNull(result);
            Assert.AreEqual(GameType.Klondike, result.Value.gameType);
            Assert.AreEqual(seed, result.Value.seed);
        }

        [Test]
        public void Roundtrip_Easthaven_NegativeSeed()
        {
            int seed = -999;
            var code = GameCode.Encode(GameType.Easthaven, seed);
            var result = GameCode.Decode(code);
            Assert.IsNotNull(result);
            Assert.AreEqual(GameType.Easthaven, result.Value.gameType);
            Assert.AreEqual(seed, result.Value.seed);
        }

        [Test]
        public void Roundtrip_CryptoSeed()
        {
            int seed = DeckFactory.CreateRandomSeed();
            var code = GameCode.Encode(GameType.Klondike, seed);
            var result = GameCode.Decode(code);
            Assert.IsNotNull(result);
            Assert.AreEqual(seed, result.Value.seed);
        }
    }
}
