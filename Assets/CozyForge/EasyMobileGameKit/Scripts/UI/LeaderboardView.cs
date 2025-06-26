using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Leaderboards;
using UnityEngine;

namespace CozyFramework
{
    public class LeaderboardView : MonoBehaviour
    {
        [Header("UI References")]
        public Sprite Top1Icon;
        public Sprite Top2Icon, Top3Icon;
        public Color Top1Color, Top2Color, Top3Color;
        public Color DefaultBackgroundColor = Color.white;

        public Transform LeaderboardEntriesContainer;
        public LeaderboardEntryDisplay LeaderboardEntryDisplay;
        private List<LeaderboardEntryDisplay> currentLeaderboardEntryDisplays = new List<LeaderboardEntryDisplay>();

        public GameObject LeaderboardList;
        public GameObject LeaderboardListContainer;
        public LeaderboardOptionDisplay LeaderboardOptionDisplayPrefab;
        private List<LeaderboardOptionDisplay> leaderboardOptionDisplays = new List<LeaderboardOptionDisplay>();

        public TextMeshProUGUI CurrentlySelectedLeaderboardText;

        private string currentLeaderboard = "";

        public static LeaderboardView Instance;

        private void Awake()
        {
            SingletonHelper.InitializeSingleton(ref Instance, this);
        }

        public void SelectLeaderboardID(string id)
        {
            LeaderboardList.SetActive(false);
            if (currentLeaderboard == id) return;
            currentLeaderboard = id;
            UpdateCurrentLeaderBoardText();
            _ = OpenLeaderboard();
        }


        public async Task OpenLeaderboard()
        {
            try
            {
                ClearSlots();

                if (currentLeaderboard == "")
                {
                    if (CozyLeaderboards.Instance.LeaderboardListTemplate.Leaderboards.Count == 0)
                    {
                        Debug.LogWarning("No leaderboards found in the remote config.");
                        MenuManager.Instance.SelectTabByName("Play");
                        return;
                    }

                    currentLeaderboard = CozyLeaderboards.Instance.LeaderboardListTemplate.Leaderboards[0].LeaderboardId;
                }

                UpdateCurrentLeaderBoardText();

                var scoresResponse = await LeaderboardsService.Instance.GetScoresAsync(currentLeaderboard);

                foreach (var player in scoresResponse.Results)
                {
                    var leaderboardEntry = Instantiate(LeaderboardEntryDisplay, LeaderboardEntriesContainer);
                    leaderboardEntry.Initialize(player.Rank, player.PlayerName, (int)player.Score);
                    currentLeaderboardEntryDisplays.Add(leaderboardEntry);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load the leaderboard: {e.Message}");
            }
        }

        private void UpdateCurrentLeaderBoardText()
        {
            CurrentlySelectedLeaderboardText.text = CozyLeaderboards.Instance.LeaderboardListTemplate.Leaderboards.Find(x => x.LeaderboardId == currentLeaderboard).LeaderboardName;
        }

        public void ShowLeaderboardList()
        {
            LeaderboardList.SetActive(true);

            foreach (var optionDisplay in leaderboardOptionDisplays)
            {
                Destroy(optionDisplay.gameObject);
            }

            leaderboardOptionDisplays.Clear();

            foreach (var leaderboard in CozyLeaderboards.Instance.LeaderboardListTemplate.Leaderboards)
            {
                var leaderboardOption = Instantiate(LeaderboardOptionDisplayPrefab, LeaderboardListContainer.transform);
                leaderboardOption.LeaderboardName.text = leaderboard.LeaderboardName;
                leaderboardOption.SelectedImage.SetActive(currentLeaderboard == leaderboard.LeaderboardId);
                leaderboardOption.Initiliaze(leaderboard.LeaderboardId);

                leaderboardOptionDisplays.Add(leaderboardOption);
            }

        }


        public void ClearSlots()
        {
            foreach (var item in currentLeaderboardEntryDisplays)
            {
                Destroy(item.gameObject);
            }

            currentLeaderboardEntryDisplays.Clear();
        }


    }
}
