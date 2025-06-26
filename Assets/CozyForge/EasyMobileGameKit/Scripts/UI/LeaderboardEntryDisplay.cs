using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CozyFramework
{
    public class LeaderboardEntryDisplay : MonoBehaviour
    {
        [Header("UI References")] public TextMeshProUGUI RankText;
        public TextMeshProUGUI PlayerNameText;
        public TextMeshProUGUI ScoreText;
        public Image TopIcon;
        public Image BackgroundImage;

        public void Initialize(int position, string playerName, int score)
        {
            ScoreText.text = score.ToString();
            PlayerNameText.text = playerName;


            if (position >= 0 && position <= 2)
            {
                TopIcon.gameObject.SetActive(true);
                switch (position)
                {
                    case 0:
                        RankText.gameObject.SetActive(false);
                        TopIcon.sprite = LeaderboardView.Instance.Top1Icon;
                        BackgroundImage.color = LeaderboardView.Instance.Top1Color;
                        break;
                    case 1:
                        RankText.gameObject.SetActive(false);
                        TopIcon.sprite = LeaderboardView.Instance.Top2Icon;
                        BackgroundImage.color = LeaderboardView.Instance.Top2Color;
                        break;
                    case 2:
                        RankText.gameObject.SetActive(false);
                        TopIcon.sprite = LeaderboardView.Instance.Top3Icon;
                        BackgroundImage.color = LeaderboardView.Instance.Top3Color;
                        break;
                }
            }
            else
            {
                TopIcon.gameObject.SetActive(false);
                RankText.gameObject.SetActive(true);
                int rank = position + 1;
                RankText.text = rank.ToString();
                BackgroundImage.color = LeaderboardView.Instance.DefaultBackgroundColor;
            }
        }

    }
}
