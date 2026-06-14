using Shared;
using TMPro;
using UnityEngine;

namespace Scene.Ingame.View
{
    /// <summary>
    /// Top Panel HUD: Score, Time, Moves display.
    /// Attach to the Top Panel GameObject in the Ingame scene.
    /// </summary>
    public class IngameHudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text moveText;

        private int lastScore = -1;
        private int lastMoves = -1;
        private int lastDisplayedSecond = -1;

        public void SetScore(int score)
        {
            if (score == lastScore) return;
            lastScore = score;
            scoreText.SetText("{0}", score);
        }

        public void SetTime(float seconds)
        {
            int sec = (int)seconds;
            if (sec == lastDisplayedSecond) return;
            lastDisplayedSecond = sec;
            timeText.text = TimeFormatHelper.Format(seconds);
        }

        public void SetMoves(int moves)
        {
            if (moves == lastMoves) return;
            lastMoves = moves;
            moveText.SetText("{0}", moves);
        }

        public void ResetDisplay()
        {
            lastScore = -1;
            lastMoves = -1;
            lastDisplayedSecond = -1;
            SetScore(0);
            SetTime(0f);
            SetMoves(0);
        }
    }
}
