using UnityEngine;

namespace Service.GameService
{
    /// <summary>Returns the trimmed clipboard contents iff they parse as a valid <see cref="GameCode"/>, else empty.</summary>
    public static class ClipboardGameCodeReader
    {
        public static string ReadOrEmpty()
        {
            var clip = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(clip))
                return string.Empty;

            var trimmed = clip.Trim();
            return GameCode.Decode(trimmed) != null ? trimmed : string.Empty;
        }
    }
}
