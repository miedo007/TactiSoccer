using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Services.Economy.Model;

namespace CozyFramework
{
    public class BattlePassView : MonoBehaviour
    {
        [Header("UI References")]
        public List<RectTransform> RectTransforms = new List<RectTransform>();
        public CanvasGroup BattlePassCanvasGroup;
        
        public Transform FreeTiersParent;
        public Transform ProgressTiersParent;
        public Transform PremiumTiersParent;

        [Header("Tier Prefabs")]
        public BattlePassTierDisplay TierDisplayPrefab; 
        public BattlePassTierProgressDisplay TierProgressPrefab;

        [Header("Text")]
        public TextMeshProUGUI MainTimerText;

        [Header("Premium")]
        public GameObject OpenBuyPopupGO;
        public GameObject PremiumActiveGO;
        
        [Header("Buy Popup")]
        public CanvasGroup BuyPopupCanvasGroup;
        public Button ActivateButton;
        public TextMeshProUGUI CostText, PopupTimerText;
        public Image CostCurrencyIcon;
        
        [Header("Colors")]
        public Color FreeClaimableFrameColor;
        public Color FreeClaimedFrameColor;
        public Color FreeLockedFrameColor;

        public Color PremiumClaimableFrameColor;
        public Color PremiumClaimedFrameColor;
        public Color PremiumLockedFrameColor;

        public Color TierFrameClaimed;
        public Color TierFrameClaimable;
        public Color TierFrameLocked;

        [Header("Config Data")]
        public BattlePassConfig CurrentBattlePassConfig;
        public PlayerBattlePassProgress CurrentBattlePassProgress;

        private List<CozyRemoteConfig.ConfigKey> configKeys = new List<CozyRemoteConfig.ConfigKey>();

        // We keep references to the instantiated progress displays to clear them later
        private readonly List<BattlePassTierDisplay> freeTierDisplays = new List<BattlePassTierDisplay>();
        private readonly List<BattlePassTierDisplay> premiumTierDisplays = new List<BattlePassTierDisplay>();
        private readonly List<BattlePassTierProgressDisplay> progressTierDisplays = new List<BattlePassTierProgressDisplay>();

        #region Data Structures

        [Serializable]
        public class BattlePassConfig
        {
            public string passId;
            public string passName;

            public long startTime;
            public long endTime;

            public List<TierData> tiers = new List<TierData>();
        }

        [Serializable]
        public class TierData
        {
            public int requiredXP;                 // XP needed to unlock this tier
            public List<RewardData> freeRewards;   // Rewards for the free track
            public List<RewardData> premiumRewards;// Rewards for the premium track
        }

        [Serializable]
        public class RewardData
        {
            public string type;
            public string id;
            public int quantity;
        }

        [Serializable]
        public class PlayerBattlePassProgress
        {
            public string passId;
            public bool premiumUnlocked;
            public int currentXP;
            public int CurrentTier = 0;

            public List<bool> freeClaimed = new List<bool>();
            public List<bool> premiumClaimed = new List<bool>();
        }


        #endregion

        #region Singleton

        public static BattlePassView Instance;

        private void Awake()
        {
            SingletonHelper.InitializeSingleton(ref Instance, this);
        }
        
        

        private void Start()
        {
            CozyEvents.RemoteConfigsFetched += OnRemoteConfigFetched;
            CozyEvents.VirtualPurchaseSuccessful += ProcessVirtualPurchase;

            if (ActivateButton != null)
            {
                ActivateButton.onClick.AddListener(OnActivateButtonPressed);
            }
        }

        private void OnDisable()
        {
            CozyEvents.RemoteConfigsFetched -= OnRemoteConfigFetched;
            CozyEvents.VirtualPurchaseSuccessful -= ProcessVirtualPurchase;
        }

        #endregion

        #region Remote Config

        private void OnRemoteConfigFetched(Unity.Services.RemoteConfig.RuntimeConfig config)
        {
            string currentPassId = GetCurrentlyActiveBattlePass(config);
            if (string.IsNullOrEmpty(currentPassId))
            {
                Debug.LogError("[BattlePass] No active pass found in remote config.");
                return;
            }
            
            configKeys.Add(new CozyRemoteConfig.ConfigKey(currentPassId, CozyRemoteConfig.ConfigType.Json));
            
            foreach (var configKey in configKeys)
            {
                if (configKey.Type == CozyRemoteConfig.ConfigType.Json && config.HasKey(configKey.Key))
                {
                    string jsonString = config.GetJson(configKey.Key);
                    if (!string.IsNullOrEmpty(jsonString))
                    {
                        CurrentBattlePassConfig = JsonConvert.DeserializeObject<BattlePassConfig>(jsonString);
                    }
                }
            }

            _ = InitializeBattlePass();
        }

        private string GetCurrentlyActiveBattlePass(Unity.Services.RemoteConfig.RuntimeConfig config)
        {
            if (config.HasKey("ACTIVE_BATTLE_PASS"))
            {
                return config.GetString("ACTIVE_BATTLE_PASS");
            }

            return null;
        }

        /// <summary>
        /// Called externally or after remote config is updated.
        /// Ensures pass config is loaded, merges with player's progress, shows pass if needed.
        /// </summary>
        public async Task InitializeBattlePass()
        {
            // 1) Load from Cloud Save
            await CozyAPI.Instance.LoadBattlePass();

            // 2) Merge config with progress
            SyncBattlePassProgress();
        }

        #endregion

        #region Sync Logic

        private void SyncBattlePassProgress()
        {
            if (CurrentBattlePassConfig == null || CurrentBattlePassConfig.tiers.Count == 0)
            {
                Debug.LogWarning("[BattlePass] No pass config found or empty tiers!");
                return;
            }

            // If the passId in progress doesn't match, or we never set it, re-init
            if (CurrentBattlePassProgress == null)
            {
                CurrentBattlePassProgress = new PlayerBattlePassProgress();
            }

            if (string.IsNullOrEmpty(CurrentBattlePassProgress.passId) || CurrentBattlePassProgress.passId != CurrentBattlePassConfig.passId)
            {
                CurrentBattlePassProgress.passId = CurrentBattlePassConfig.passId;
                CurrentBattlePassProgress.currentXP = 0;
                CurrentBattlePassProgress.CurrentTier = 0;
                CurrentBattlePassProgress.premiumUnlocked = false;
                CurrentBattlePassProgress.freeClaimed = new List<bool>(new bool[CurrentBattlePassConfig.tiers.Count]);
                CurrentBattlePassProgress.premiumClaimed = new List<bool>(new bool[CurrentBattlePassConfig.tiers.Count]);
            }
            else
            {
                // Ensure freeClaimed and premiumClaimed lists match the tier count
                int tierCount = CurrentBattlePassConfig.tiers.Count;

                // Adjust freeClaimed
                if (CurrentBattlePassProgress.freeClaimed.Count < tierCount)
                {
                    while (CurrentBattlePassProgress.freeClaimed.Count < tierCount)
                    {
                        CurrentBattlePassProgress.freeClaimed.Add(false);
                    }
                }
                else if (CurrentBattlePassProgress.freeClaimed.Count > tierCount)
                {
                    CurrentBattlePassProgress.freeClaimed.RemoveRange(tierCount, 
                        CurrentBattlePassProgress.freeClaimed.Count - tierCount);
                }

                // Adjust premiumClaimed
                if (CurrentBattlePassProgress.premiumClaimed.Count < tierCount)
                {
                    while (CurrentBattlePassProgress.premiumClaimed.Count < tierCount)
                    {
                        CurrentBattlePassProgress.premiumClaimed.Add(false);
                    }
                }
                else if (CurrentBattlePassProgress.premiumClaimed.Count > tierCount)
                {
                    CurrentBattlePassProgress.premiumClaimed.RemoveRange(tierCount, 
                        CurrentBattlePassProgress.premiumClaimed.Count - tierCount);
                }
            }

            // Save to ensure progress is up to date
            _ = CozyAPI.Instance.SaveBattlePass();
        }

        #endregion

        #region UI

        public void ShowBattlePass()
        {
            CozyUtilities.EnableCG(BattlePassCanvasGroup);

            // Whenever the pass is opened, re-check the timer text
            UpdateTimerText(MainTimerText);

            RefreshUI();
        }

        public void HideBattlePass()
        {
            CozyUtilities.DisableCG(BattlePassCanvasGroup);
        }

        /// <summary>
        /// Called to refresh the entire UI layout: free column, progress column, premium column.
        /// </summary>
        public void RefreshUI()
        {
            ClearAllDisplays();

            if (CurrentBattlePassConfig == null || CurrentBattlePassConfig.tiers.Count == 0)
            {
                Debug.LogError("[BattlePass] No config or no tiers to display!");
                return;
            }

            bool premiumUnlocked = CurrentBattlePassProgress.premiumUnlocked;
            
            OpenBuyPopupGO.SetActive(!premiumUnlocked);
            PremiumActiveGO.SetActive(premiumUnlocked);

            // For each tier, create 3 displays: free, progress, premium
            for (int i = 0; i < CurrentBattlePassConfig.tiers.Count; i++)
            {
                var tierData = CurrentBattlePassConfig.tiers[i];

                // 1) Free Tier
                var freeDisplay = Instantiate(TierDisplayPrefab, FreeTiersParent);
                freeTierDisplays.Add(freeDisplay);
                freeDisplay.Init(
                    tierType: BattlePassTierDisplay.TierType.Free,
                    tierIndex: i,
                    thisView: this
                );
                freeDisplay.SetRewardData(tierData.freeRewards);

                // 2) Progress Tier
                var progressDisplay = Instantiate(TierProgressPrefab, ProgressTiersParent);
                progressTierDisplays.Add(progressDisplay);
                SetupProgressDisplay(progressDisplay, i);

                // 3) Premium Tier
                var premiumDisplay = Instantiate(TierDisplayPrefab, PremiumTiersParent);
                premiumTierDisplays.Add(premiumDisplay);
                premiumDisplay.Init(
                    tierType: BattlePassTierDisplay.TierType.Premium,
                    tierIndex: i,
                    thisView: this
                );
                premiumDisplay.SetRewardData(tierData.premiumRewards);

                // If premium is not unlocked, that premium display is locked
                premiumDisplay.LockedGO.SetActive(!premiumUnlocked);
            }
            
            RefreshRects();
        }

        private void ClearAllDisplays()
        {
            foreach (var d in freeTierDisplays)
                Destroy(d.gameObject);
            freeTierDisplays.Clear();

            foreach (var d in premiumTierDisplays)
                Destroy(d.gameObject);
            premiumTierDisplays.Clear();

            foreach (var d in progressTierDisplays)
                Destroy(d.gameObject);
            progressTierDisplays.Clear();
        }
        
        private void UpdateTimerText(TextMeshProUGUI text)
        {
            if (text == null) return;
            if (CurrentBattlePassConfig == null) return;

            long endUnix = CurrentBattlePassConfig.endTime;
            DateTime endDate = DateTimeOffset.FromUnixTimeSeconds(endUnix).UtcDateTime;
            DateTime now = DateTime.UtcNow;

            if (now >= endDate)
            {
                // The pass is expired
                text.text = "Ended";
                return;
            }

            TimeSpan remaining = endDate - now;
            // Convert to something like "16d 22h"
            int days = (int)remaining.TotalDays;
            int hours = remaining.Hours;
            int minutes = remaining.Minutes;

            // If you want a more precise format:
            if (days > 0)
            {
                text.text = $"{days}d {hours}h";
            }
            else
            {
                // If less than 1 day left
                text.text = $"{hours}h {minutes}m";
            }
        }
        
        public void ShowBuyPopup()
        {
            CozyUtilities.EnableCG(BuyPopupCanvasGroup);
            
            VirtualPurchaseDefinition battlePassVirtualPurchase = GetBattlePassVirtualPurchaseDefinition();
            if (battlePassVirtualPurchase == null)
            {
                Debug.LogError("[BattlePass] No virtual purchase definition found for the battle pass.");
                return;
            }
            
            int costAmount = battlePassVirtualPurchase.Costs[0].Amount;
            string currencyId = battlePassVirtualPurchase.Costs[0].Item.GetReferencedConfigurationItem().Id;
            
            if(CozyAPI.Instance.GetCurrencyValue(currencyId) >= costAmount)
            {
                ActivateButton.interactable = true;
            }
            else
            {
                ActivateButton.interactable = false;
            }
            
            if (CostText != null)
            {
                CostText.text = costAmount.ToString();
            }
            
            if (CostCurrencyIcon != null)
            {
                CostCurrencyIcon.sprite = CozyDatabase.Instance.GetCozyCurrency(currencyId).Icon;
            }
            
            UpdateTimerText(PopupTimerText);
        }
        
        public void HideBuyPopup()
        {
            CozyUtilities.DisableCG(BuyPopupCanvasGroup);
        }

        private VirtualPurchaseDefinition GetBattlePassVirtualPurchaseDefinition()
        {
            foreach (var virtualPurchase in CozyEconomy.Instance.m_VirtualPurchaseDefinitions)
            {
                if (virtualPurchase.Id == "PURCHASE_BATTLE_PASS")
                {
                    return virtualPurchase;
                }
            }
            
            return null;
        }

        #endregion

        #region Purchase Premium

        private async void OnActivateButtonPressed()
        {
            ActivateButton.interactable = false;
            await CozyAPI.Instance.MakeVirtualPurchaseAsync("PURCHASE_BATTLE_PASS");
        }
        
        private async void ProcessVirtualPurchase(string productId, MakeVirtualPurchaseResult result)
        {
            if (productId == "PURCHASE_BATTLE_PASS")
            {
                CurrentBattlePassProgress.premiumUnlocked = true;
                await CozyAPI.Instance.SaveBattlePass();
                HideBuyPopup();
                RefreshUI();
            }
        }

        #endregion

        #region Claim Logic

        /// <summary>
        /// Called by a "Free" BattlePassTierDisplay's claim button.
        /// </summary>
        public async void OnClaimFreeTier(int tierIndex)
        {
            if (tierIndex < 0 || tierIndex >= CurrentBattlePassConfig.tiers.Count) return;
            if (CurrentBattlePassProgress.freeClaimed[tierIndex]) return;

            if (tierIndex > CurrentBattlePassProgress.CurrentTier)
            {
                return;
            }

            // Grant free rewards
            var rewards = CurrentBattlePassConfig.tiers[tierIndex].freeRewards;
            GrantRewards(rewards);

            CurrentBattlePassProgress.freeClaimed[tierIndex] = true;
            await CozyAPI.Instance.SaveBattlePass();

            RefreshUI();
        }

        /// <summary>
        /// Called by a "Premium" BattlePassTierDisplay's claim button.
        /// </summary>
        public async void OnClaimPremiumTier(int tierIndex)
        {
            if (tierIndex < 0 || tierIndex >= CurrentBattlePassConfig.tiers.Count) return;
            if (CurrentBattlePassProgress.premiumClaimed[tierIndex]) return;

            if (!CurrentBattlePassProgress.premiumUnlocked)
            {
                return;
            }

            if (tierIndex > CurrentBattlePassProgress.CurrentTier)
            {
                return;
            }

            // Grant premium rewards
            var rewards = CurrentBattlePassConfig.tiers[tierIndex].premiumRewards;
            GrantRewards(rewards);

            CurrentBattlePassProgress.premiumClaimed[tierIndex] = true;
            await CozyAPI.Instance.SaveBattlePass();

            RefreshUI();
        }

        #endregion

        #region Public XP Methods

        

        
        public void HandleLevelUps()
        {
            while (CurrentBattlePassProgress.CurrentTier < CurrentBattlePassConfig.tiers.Count)
            {
                int currentTierIndex = CurrentBattlePassProgress.CurrentTier;
                var currentTier = CurrentBattlePassConfig.tiers[currentTierIndex];

                // Check if the player has enough XP for the current tier
                if (CurrentBattlePassProgress.currentXP >= currentTier.requiredXP)
                {
                    // Deduct the XP required for this tier
                    CurrentBattlePassProgress.currentXP -= currentTier.requiredXP;

                    // Move to the next tier
                    CurrentBattlePassProgress.CurrentTier++;
                    // Leveled up to Tier {CurrentBattlePassProgress.CurrentTier + 1}!");

                    // Mark rewards as claimable
                    if (!CurrentBattlePassProgress.freeClaimed[currentTierIndex])
                    {
                        // Free reward for Tier {currentTierIndex + 1} is now claimable.");
                    }

                    if (CurrentBattlePassProgress.premiumUnlocked && !CurrentBattlePassProgress.premiumClaimed[currentTierIndex])
                    {
                        // Premium reward for Tier {currentTierIndex + 1} is now claimable.");
                    }

                    // If we reached the final tier, stop processing
                    if (CurrentBattlePassProgress.CurrentTier >= CurrentBattlePassConfig.tiers.Count)
                    {
                        CurrentBattlePassProgress.currentXP = 0; // Reset XP at the last tier
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        
        private void SetupProgressDisplay(BattlePassTierProgressDisplay progressDisplay, int i)
        {
            progressDisplay.TierAmountText.text = $"{i + 1}"; // Tier number

            bool isCurrentTier = i == CurrentBattlePassProgress.CurrentTier;
            bool isClaimed = CurrentBattlePassProgress.freeClaimed[i] || CurrentBattlePassProgress.premiumClaimed[i];

            // Set frame color based on tier state
            if (isClaimed)
            {
                progressDisplay.Frame.color = TierFrameClaimed;
            }
            else if (isCurrentTier)
            {
                progressDisplay.Frame.color = TierFrameClaimable;
            }
            else
            {
                progressDisplay.Frame.color = TierFrameLocked;
            }

            // Update progress bar fill
            if (i < CurrentBattlePassProgress.CurrentTier)
            {
                progressDisplay.ProgressBarFill.fillAmount = 1f;
            }
            else if (i == CurrentBattlePassProgress.CurrentTier)
            {
                var currentTier = CurrentBattlePassConfig.tiers[i];
                float fillAmount = (float)CurrentBattlePassProgress.currentXP / currentTier.requiredXP;
                progressDisplay.ProgressBarFill.fillAmount = Mathf.Clamp01(fillAmount);
            }
            else
            {
                progressDisplay.ProgressBarFill.fillAmount = 0f;
            }

            // Hide progress bar if it's the last tier
            progressDisplay.ProgressBarGO.SetActive(i < CurrentBattlePassConfig.tiers.Count - 1);
        }

        #endregion

        #region Reward Grant

        private void GrantRewards(List<RewardData> rewards)
        {
            if (rewards == null) return;
            foreach (var reward in rewards)
            {
                switch (reward.type)
                {
                    case "Currency":
                        _ = CozyAPI.Instance.GainCurrency(reward.id, reward.quantity);
                        break;
                    case "Item":
                        // e.g. CozyInventory.Instance.GrantItem(reward.id, reward.quantity);
                        Debug.Log($"Granted {reward.quantity} of item '{reward.id}'.");
                        break;
                    default:
                        Debug.LogWarning($"Unknown reward type: {reward.type}");
                        break;
                }
            }
        }

        #endregion
        
        
        

        private void RefreshRects()
        {
            foreach (var rect in RectTransforms)
            { 
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }
        }
    }
}
