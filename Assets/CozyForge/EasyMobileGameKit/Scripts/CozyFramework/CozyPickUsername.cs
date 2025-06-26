using System.Collections.Generic;
using Unity.Services.Authentication;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Text.RegularExpressions;

namespace CozyFramework
{
    public class CozyPickUsername : MonoBehaviour
    {
        public List<RectTransform> ContentRects = new List<RectTransform>();

        public GameObject PickUsernameView;
        public TMP_InputField UsernameInputField;
        public TMP_Text FeedbackText;
        public Button SubmitButton;

        [SerializeField] private int minNameLength = 3;
        [SerializeField] private int maxNameLength = 20;
        [SerializeField] private bool enforceAlphanumeric = true;

        public static CozyPickUsername Instance;

        private void Awake()
        {
            SingletonHelper.InitializeSingleton(ref Instance, this);
        }

        public void ShowPickUsernameView()
        {
            ClearFeedback();
            PickUsernameView.SetActive(true);
            UsernameInputField.text = string.Empty;

            UpdateRects();
        }

        private void UpdateRects()
        {
            foreach (var rect in ContentRects)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }
        }

        public void HidePickUsernameView()
        {
            PickUsernameView.SetActive(false);
        }

        public async void OnSubmitUsername()
        {
            FeedbackText.gameObject.SetActive(false);
            string playerName = UsernameInputField.text.Trim();

            if (!IsUsernameValid(playerName))
            {
                return;
            }
            
            SetInteractionEnabled(false);

            try
            {
                await AuthenticationService.Instance.UpdatePlayerNameAsync(playerName);
                HidePickUsernameView();

                PlayerManager.Instance.PlayerHasUsername = true;
                await CozyAPI.Instance.SavePlayerData("username", AuthenticationService.Instance.PlayerName);

                if (CozyLeaderboards.Instance.LeaderboardExists("highscore")) await CozyAPI.Instance.SubmitScoreToLeaderboard("highscore", PlayerManager.Instance.PlayerHighScore);
                if (CozyLeaderboards.Instance.LeaderboardExists("matches")) await CozyAPI.Instance.SubmitScoreToLeaderboard("matches", PlayerManager.Instance.PlayerTotalMatches);

                await LeaderboardView.Instance.OpenLeaderboard();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to update username: {ex.Message}");
                FeedbackText.gameObject.SetActive(true);
                FeedbackText.text = "Failed to update username. Please try again.";
                UpdateRects();
            }
            finally
            {
                SetInteractionEnabled(true);
            }
        }

        private bool IsUsernameValid(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                FeedbackText.gameObject.SetActive(true);
                FeedbackText.text = $"Username cannot be empty.";
                UpdateRects();
                return false;
            }

            if (username.Length < minNameLength || username.Length > maxNameLength)
            {
                FeedbackText.gameObject.SetActive(true);
                FeedbackText.text = $"Username must be between {minNameLength} and {maxNameLength} characters.";
                UpdateRects();
                return false;
            }

            if (enforceAlphanumeric && !Regex.IsMatch(username, @"^[a-zA-Z0-9_]+$"))
            {
                FeedbackText.gameObject.SetActive(true);
                FeedbackText.text = "Username can only contain letters, numbers, and underscores.";
                UpdateRects();
                return false;
            }

            ClearFeedback();
            return true;
        }

        private void ClearFeedback()
        {
            if (FeedbackText != null)
            {
                FeedbackText.text = string.Empty;
                FeedbackText.gameObject.SetActive(false);
            }
        }

        private void SetInteractionEnabled(bool enabled)
        {
            if (SubmitButton != null)
            {
                SubmitButton.interactable = enabled;
            }
        }

    }
}
