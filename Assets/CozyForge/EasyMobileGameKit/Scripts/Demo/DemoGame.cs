using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace CozyFramework
{
    public class DemoGame : MonoBehaviour
    {
        public TextMeshProUGUI ScoreText;

        private int currentScore;

        public void ClickPlay()
        {
            MenuManager.Instance.StartGame();
            currentScore = 0;
            UpdateScoreText();
        }


        public async void ClickGameOver()
        {
            MenuManager.Instance.EndGame();
            PlayerManager.Instance.PlayerTotalMatches++;
            await IncreaseTotalMatches();
            await CheckHighScore();
            currentScore = 0;
        }

        public void ClickAddScore()
        {
            currentScore += 10;
            UpdateScoreText();
        }

        private void UpdateScoreText()
        {
            ScoreText.text = currentScore.ToString();
        }

        private async Task IncreaseTotalMatches()
        {
            await CozyAPI.Instance.SavePlayerData("matches", PlayerManager.Instance.PlayerTotalMatches);
            await CozyAPI.Instance.SubmitScoreToLeaderboard("matches", PlayerManager.Instance.PlayerTotalMatches);
        }

        private async Task CheckHighScore()
        {
            if (currentScore > PlayerManager.Instance.PlayerHighScore)
            {
                PlayerManager.Instance.PlayerHighScore = currentScore;
                await CozyAPI.Instance.SavePlayerData("highscore", PlayerManager.Instance.PlayerHighScore);
                await CozyAPI.Instance.SubmitScoreToLeaderboard("highscore", PlayerManager.Instance.PlayerHighScore);
            }
        }

    }
}
