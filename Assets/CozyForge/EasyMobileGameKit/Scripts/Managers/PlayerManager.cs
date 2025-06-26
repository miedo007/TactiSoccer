using System;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Services.CloudSave.Models;
using UnityEngine;

namespace CozyFramework
{
    public class PlayerManager : MonoBehaviour
    {
        [Header("PLAYER DATA")]
        public bool PlayerHasUsername;
        public string PlayerUsername;
        public int PlayerHighScore;
        public int PlayerTotalMatches;

        public static PlayerManager Instance;

        private void Awake()
        {
            SingletonHelper.InitializeSingleton(ref Instance, this);
        }

        private void Start()
        {
            CozyEvents.AuthSuccess += InitializeGame;
        }

        private void OnDisable()
        {
            CozyEvents.AuthSuccess -= InitializeGame;
        }


        private void InitializeGame()
        {
            LoadPlayerData();
        }


        private async void LoadPlayerData()
        {
            var keysToLoad = new HashSet<string>
            {
                "highscore",
                "matches",
                "username"
            };

            var loadedData = await CozyCloudSave.LoadData(keysToLoad);
            if (loadedData == null)
            {
                Debug.LogWarning("LoadedData was null.");
                return;
            }

            if (loadedData.TryGetValue("highscore", out var highScoreItem))
            {
                var highScore = ConvertItem<int>(highScoreItem);
                if (!EqualityComparer<int>.Default.Equals(highScore, default(int)))
                {
                    PlayerHighScore = highScore;
                }
            }

            if (loadedData.TryGetValue("matches", out var totalMatchesItem))
            {
                var totalMatches = ConvertItem<int>(totalMatchesItem);
                if (!EqualityComparer<int>.Default.Equals(totalMatches, default(int)))
                {
                    PlayerTotalMatches = totalMatches;
                }
            }

            if (loadedData.TryGetValue("username", out var usernameItem))
            {
                string username = ConvertItem<string>(usernameItem);
                if (!string.IsNullOrEmpty(username))
                {
                    PlayerHasUsername = true;
                }
            }

            if (PlayerHasUsername)
            {
                PlayerUsername = await AuthenticationService.Instance.GetPlayerNameAsync();
            }
        }

        private T ConvertItem<T>(Item item)
        {
            try
            {
                string jsonString = item.Value.GetAsString();
                
                if (typeof(T).IsPrimitive || typeof(T) == typeof(string))
                {
                    return (T)Convert.ChangeType(jsonString, typeof(T));
                }
                else
                {
                    return JsonUtility.FromJson<T>(jsonString);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to convert value to type {typeof(T)}: {e.Message}");
                return default;
            }
        }


    }
}
