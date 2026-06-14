namespace Data.Audio
{
    /// <summary>
    /// Central registry of all audio keys used throughout the game.
    /// Each key maps to an AudioEntry in AudioDatabaseAsset (ScriptableObject).
    ///
    /// To add a new sound:
    ///   1. Add the key constant here
    ///   2. Place the .ogg clip in Assets/Audio/Clips/
    ///   3. Add the entry in AudioDatabase asset (ScriptableObjects/Audio/AudioDatabase)
    ///   4. Use via AudioService.Play(AudioCatalog.Xxx.Yyy)
    /// </summary>
    public static class AudioCatalog
    {
        /// <summary>
        /// Game-level events (win, lose, new game, undo, hint).
        /// Typically UI-feedback sounds — short, non-positional.
        /// </summary>
        public static class Game
        {
            /// <summary>Undo move executed.</summary>
            public const string Undo = "game.undo";

            /// <summary>New game started (deal).</summary>
            public const string New = "game.new";

            /// <summary>Game won — celebratory fanfare.</summary>
            public const string Win = "game.win";

            /// <summary>Hint arrow shown — subtle "ding" or chime.</summary>
            public const string Hint = "game.hint";

            /// <summary>Hint requested but no moves available — negative buzz.</summary>
            public const string NoHint = "game.no_hint";

            /// <summary>Stuck/lost panel shown — soft negative tone.</summary>
            public const string Stuck = "game.stuck";
        }

        /// <summary>
        /// Card interaction sounds — drag, drop, flip, place.
        /// Should feel tactile and responsive.
        /// </summary>
        public static class Card
        {
            /// <summary>Card flipped face-up (reveal).</summary>
            public const string Flip = "card.flip";

            /// <summary>Card drag started — light pickup sound.</summary>
            public const string DragStart = "card.drag_start";

            /// <summary>Card drag canceled — card snaps back.</summary>
            public const string DragCancel = "card.drag_cancel";

            /// <summary>Invalid move attempt — soft error thud.</summary>
            public const string MoveRejected = "card.move_rejected";

            /// <summary>Card placed on foundation — satisfying "lock" or chime.</summary>
            public const string FoundationPlace = "card.foundation_place";

            /// <summary>Card placed on tableau — generic soft drop.</summary>
            public const string Place = "card.place";

            /// <summary>Card stack refreshed — visual update sound.</summary>
            public const string Refresh = "card.refresh";
        }

        /// <summary>
        /// General UI interaction sounds (buttons, panels).
        /// </summary>
        public static class UI
        {
            /// <summary>Button / UI element tapped.</summary>
            public const string Click = "ui.click";
            /// <summary>Panel or overlay opened.</summary>
            public const string Open = "ui.open";
            /// <summary>Panel or overlay closed.</summary>
            public const string Close = "ui.close";
            /// <summary>Toggle switch flipped (on/off).</summary>
            public const string Toggle = "ui.toggle";
        }

        /// <summary>
        /// Background music tracks.
        /// </summary>
        public static class Music
        {
            /// <summary>Main BGM loop.</summary>
            public const string Main = "music.main";
            /// <summary>Auto-complete started BGM Loop.</summary>
            public const string AutoComplete = "music.auto_complete";
        }
    }
}
