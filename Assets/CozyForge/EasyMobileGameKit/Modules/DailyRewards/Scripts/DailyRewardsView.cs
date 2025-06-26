using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.CloudCode;
using Unity.Services.RemoteConfig;
using UnityEngine;

namespace CozyFramework
{
    public class DailyRewardsView : MonoBehaviour
    {
        public CanvasGroup DailyRewardsCG;

        public DailyRewardDisplay DailyRewardDisplayPrefab;
        public Transform DailyRewardDisplayParent;
        private List<DailyRewardDisplay> currentDayDisplaySlots = new List<DailyRewardDisplay>();

        public enum DayStatus
        {
            DayClaimed,
            DayClaimable,
            DayUnclaimable
        }

        GetStatusResult m_Status;

        [Serializable]
        public class DailyRewardConfig
        {
            public int totalDays;
            public int secondsPerDay;

            public List<List<DailyRewardEntry>> dailyRewards = new List<List<DailyRewardEntry>>();
        }

        [Serializable]
        public class DailyRewardEntry
        {
            public string id;
            public int quantity;
        }

        public DailyRewardConfig dailyRewardsConfig;



        private List<CozyRemoteConfig.ConfigKey> configKeys = new List<CozyRemoteConfig.ConfigKey>
        {
            new CozyRemoteConfig.ConfigKey("DAILY_REWARDS_CONFIG", CozyRemoteConfig.ConfigType.Json),
        };

        public bool isEventReady => m_Status.success;

        public bool firstVisit => m_Status.firstVisit;

        public int daysClaimed => m_Status.daysClaimed;

        public int daysRemaining => m_Status.daysRemaining;

        public int totalCalendarDays => m_Status.dailyRewards.Count;

        public bool isStarted => m_Status.isStarted;

        public float secondsTillClaimable => m_Status.secondsTillClaimable;

        public bool isClaimableNow => secondsTillClaimable <= 0;

        public float secondsTillNextDay => m_Status.secondsTillNextDay;

        public float secondsPerDay => m_Status.secondsPerDay;

        public List<DailyReward> GetDailyRewards(int dayIndex) => m_Status.dailyRewards[dayIndex];

        public List<DailyReward> rewardsGranted { get; private set; }

        float lastUpdateTime;

        private bool ready;

        public static DailyRewardsView Instance;

        private void Awake()
        {
            SingletonHelper.InitializeSingleton(ref Instance, this);
        }

        private void Start()
        {
            CozyEvents.RemoteConfigsFetched += SaveAllConfigValues;
        }

        private void OnDisable()
        {
            CozyEvents.RemoteConfigsFetched -= SaveAllConfigValues;
        }

        public async Task InitializeDailyRewardsStatus()
        {
            await RefreshDailyRewardsEventStatus();
            if (this == null) return;

            ShowStatus();
        }

        public void ShowDailyRewards()
        {
            CozyUtilities.EnableCG(DailyRewardsCG);
            UpdateUI();
        }

        private void UpdateUI()
        {
            ClearDaySlots();

            for (int i = 0; i < m_Status.dailyRewards.Count; i++)
            {
                DailyRewardDisplay slot = Instantiate(DailyRewardDisplayPrefab, DailyRewardDisplayParent);
                DailyRewardEntry dailyRewardEntry = dailyRewardsConfig.dailyRewards[i][0];
                int day = i + 1;
                slot.DayText.text = "Day " + day;
                slot.AmountText.text = CozyUtilities.FormatNumber(dailyRewardEntry.quantity);
                slot.LootIcon.sprite = CozyDatabase.Instance.GetCozyCurrency(dailyRewardEntry.id).Icon;
                currentDayDisplaySlots.Add(slot);

                var dayStatus = GetDayStatus(day);

                if (dayStatus == DayStatus.DayClaimed)
                {
                    slot.LootGO.SetActive(false);
                    slot.ClaimedGO.SetActive(true);
                    slot.ClaimButton.interactable = false;
                }
                else if (dayStatus == DayStatus.DayClaimable)
                {
                    slot.LootGO.SetActive(true);
                    slot.ClaimedGO.SetActive(false);
                    slot.ClaimButton.interactable = true;
                }
                else if (dayStatus == DayStatus.DayUnclaimable)
                {
                    slot.LootGO.SetActive(true);
                    slot.ClaimedGO.SetActive(false);
                    slot.ClaimButton.interactable = false;
                }
            }
        }

        private void ClearDaySlots()
        {
            foreach (var slot in currentDayDisplaySlots)
            {
                Destroy(slot.gameObject);
            }

            currentDayDisplaySlots.Clear();
        }


        public void HideDailyRewardsWindow()
        {
            CozyUtilities.DisableCG(DailyRewardsCG);
        }



        public async Task RefreshDailyRewardsEventStatus()
        {
            try
            {
                m_Status = await CallGetStatusEndpoint();
                if (this == null) return;
                ready = true;
                lastUpdateTime = Time.time;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void Update()
        {
            if (ready && UpdateRewardsStatus())
            {
                UpdateUI();
            }
        }

        public bool UpdateRewardsStatus()
        {
            var time = Time.time;
            var timePassed = time - lastUpdateTime;
            lastUpdateTime = time;

            m_Status.secondsTillNextDay -= timePassed;

            if (m_Status.secondsTillNextDay <= 0)
            {
                m_Status.secondsTillNextDay += m_Status.secondsPerDay;

                if (m_Status.secondsTillClaimable > 0)
                {
                    m_Status.secondsTillClaimable = 0;
                }

                else
                {
                    m_Status.daysRemaining--;

                    if (m_Status.daysRemaining <= 0)
                    {
                        m_Status.daysRemaining = 0;
                    }
                }

                return true;
            }

            if (m_Status.secondsTillClaimable > 0)
            {
                m_Status.secondsTillClaimable = secondsTillNextDay;
            }

            return false;
        }


        public async void OnClaimButtonPressed()
        {
            try
            {
                await ClaimDailyReward();
                if (this == null) return;

                ShowStatus();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void ShowStatus()
        {
            if (firstVisit)
            {
                MarkFirstVisitComplete();
            }
        }

        public async Task ClaimDailyReward()
        {
            try
            {
                var claimResult = await CallClaimEndpoint();
                CozyCloudCode.Instance.LoadingGO.SetActive(false);
                if (this == null) return;

                lastUpdateTime = Time.time;

                SaveStatus(claimResult);
                UpdateUI();
                await CozyEconomy.Instance.RefreshCurrencyBalances();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void MarkFirstVisitComplete()
        {
            m_Status.firstVisit = false;
        }

        public DayStatus GetDayStatus(int dayIndex)
        {
            if (dayIndex <= daysClaimed)
            {
                if (dayIndex > totalCalendarDays && isClaimableNow)
                {
                    return DayStatus.DayClaimable;
                }

                return DayStatus.DayClaimed;
            }

            if (dayIndex > daysClaimed + 1)
            {
                return DayStatus.DayUnclaimable;
            }

            return isClaimableNow ? DayStatus.DayClaimable : DayStatus.DayUnclaimable;
        }

        void SaveStatus(ClaimResult claimResult)
        {
            m_Status.success = claimResult.success;
            m_Status.firstVisit = claimResult.firstVisit;
            m_Status.daysClaimed = claimResult.daysClaimed;
            m_Status.daysRemaining = claimResult.daysRemaining;
            m_Status.totalDays = claimResult.totalDays;
            m_Status.isStarted = claimResult.isStarted;
            m_Status.secondsTillClaimable = claimResult.secondsTillClaimable;
            m_Status.secondsTillNextDay = claimResult.secondsTillNextDay;
            m_Status.secondsPerDay = claimResult.secondsPerDay;
            m_Status.dailyRewards = claimResult.dailyRewards;

            rewardsGranted = claimResult.rewardsGranted;
        }

        public struct GetStatusResult
        {
            public bool success;
            public bool firstVisit;
            public int daysClaimed;
            public int daysRemaining;
            public int totalDays;
            public bool isStarted;
            public float secondsTillClaimable;
            public float secondsTillNextDay;
            public float secondsPerDay;
            public List<List<DailyReward>> dailyRewards;

            public override string ToString()
            {
                return $"success:{success} days:{daysClaimed}/{totalDays} started:{isStarted} " +
                       $"dailyRewardsCount:{dailyRewards.Count} ";
            }
        }

        public struct ClaimResult
        {
            public bool success;
            public bool firstVisit;
            public int daysClaimed;
            public int daysRemaining;
            public int totalDays;
            public bool isStarted;
            public float secondsTillClaimable;
            public float secondsTillNextDay;
            public float secondsPerDay;
            public List<List<DailyReward>> dailyRewards;
            public List<DailyReward> rewardsGranted;

            public override string ToString()
            {
                var outputString = $"success:{success} days:{daysClaimed}/{totalDays} started:{isStarted} " +
                                   $"dailyRewardsCount:{dailyRewards.Count} ";

                if (rewardsGranted == null)
                {
                    return outputString;
                }

                return outputString + $" rewardsGranted:{string.Join(",", rewardsGranted.Select(reward => reward.ToString()).ToArray())}";
            }
        }

        public struct DailyReward
        {
            public string id;
            public int quantity;

            public override string ToString()
            {
                return $"({id} {quantity})";
            }
        }

        private void SaveAllConfigValues(RuntimeConfig config)
        {
            foreach (var configKey in configKeys)
            {
                string key = configKey.Key;
                try
                {
                    switch (configKey.Type)
                    {
                        case CozyRemoteConfig.ConfigType.Json:
                            if (config.HasKey(key))
                            {
                                string jsonString = config.GetJson(key);
                                if (!string.IsNullOrEmpty(jsonString))
                                {
                                    if (key == "DAILY_REWARDS_CONFIG")
                                    {
                                        dailyRewardsConfig = JsonConvert.DeserializeObject<DailyRewardConfig>(jsonString);
                                    }
                                }
                            }

                            break;

                        default:
                            Debug.LogWarning($"Unsupported config type for key: {key}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error fetching key '{key}': {ex.Message}");
                }
            }

            _ = InitializeDailyRewardsStatus();
        }

        public async Task<GetStatusResult> CallGetStatusEndpoint()
        {
            try
            {
                return await CloudCodeService.Instance.CallEndpointAsync<GetStatusResult>("DailyRewards_GetStatus", new Dictionary<string, object>());
            }
            catch (CloudCodeException)
            {
                return new GetStatusResult();
            }
        }

        public async Task<ClaimResult> CallClaimEndpoint()
        {
            try
            {
                CozyCloudCode.Instance.LoadingGO.SetActive(true);
                return await CloudCodeService.Instance.CallEndpointAsync<ClaimResult>("DailyRewards_Claim", new Dictionary<string, object>());
            }
            catch (CloudCodeException)
            {
                return new ClaimResult();
            }
        }
    }
}


