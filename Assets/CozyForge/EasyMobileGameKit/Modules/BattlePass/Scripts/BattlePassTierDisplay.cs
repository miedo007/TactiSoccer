using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace CozyFramework
{
    public class BattlePassTierDisplay : MonoBehaviour
    {
        public enum TierType
        {
            Free,
            Premium
        }

        [Header("UI References")] public Image RewardIcon;
        public TextMeshProUGUI RewardQuantity;
        public Image Frame;
        public GameObject ClaimedGO;
        public GameObject LockedGO;

        [Header("Claim Button")] public Button ClaimButton;

        // Internal state
        private TierType tierType;
        private int tierIndex;
        private BattlePassView battlePassView;

        public void Init(TierType tierType, int tierIndex, BattlePassView thisView)
        {
            this.tierType = tierType;
            this.tierIndex = tierIndex;
            this.battlePassView = thisView;

            // Hook up the claim button event
            ClaimButton.onClick.RemoveAllListeners();
            if (tierType == TierType.Free)
            {
                ClaimButton.onClick.AddListener(() => { battlePassView.OnClaimFreeTier(tierIndex); });
            }
            else
            {
                ClaimButton.onClick.AddListener(() => { battlePassView.OnClaimPremiumTier(tierIndex); });
            }
        }

        /// <summary>
        /// Called after Init, to fill reward data (icon, quantity, color states).
        /// For simplicity, let's just show the first reward if there's one.
        /// </summary>
        public void SetRewardData(List<BattlePassView.RewardData> rewards)
        {
            if (rewards == null || rewards.Count == 0)
            {
                // no reward
                if (RewardIcon) RewardIcon.gameObject.SetActive(false);
                if (RewardQuantity) RewardQuantity.text = "";
            }
            else
            {
                // Just show the first reward
                var r = rewards[0];
                if (RewardIcon) RewardIcon.gameObject.SetActive(true);
                if (RewardQuantity) RewardQuantity.text = "x" + r.quantity.ToString();

                // If you have an icon DB, e.g. coins
                if (RewardIcon && r.type == "Currency")
                {
                    RewardIcon.sprite = CozyDatabase.Instance.GetCozyCurrency(r.id).Icon;
                }
            }

            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            var config = battlePassView.CurrentBattlePassConfig;
            var progress = battlePassView.CurrentBattlePassProgress;

            if (config == null || progress == null) return;
            if (tierIndex < 0 || tierIndex >= config.tiers.Count) return;
            
            bool canClaim = tierIndex <= progress.CurrentTier;
            bool isClaimed = false;
            
            if (tierType == TierType.Free)
            {
                isClaimed = progress.freeClaimed[tierIndex];
            }
            else
            {
                isClaimed = progress.premiumClaimed[tierIndex];
            }
            
            bool locked = !canClaim;
            if (tierType == TierType.Premium)
            {
                if (!progress.premiumUnlocked) locked = true;
            }

            // Set frame color based on state
            if (tierType == TierType.Free)
            {
                if (isClaimed)
                {
                    Frame.color = battlePassView.FreeClaimedFrameColor;
                }
                else if (!locked)
                {
                    Frame.color = battlePassView.FreeClaimableFrameColor;
                }
                else
                {
                    Frame.color = battlePassView.FreeLockedFrameColor;
                }
            }
            else // Premium
            {
                if (isClaimed)
                {
                    Frame.color = battlePassView.PremiumClaimedFrameColor;
                }
                else if (!locked)
                {
                    Frame.color = battlePassView.PremiumClaimableFrameColor;
                }
                else
                {
                    Frame.color = battlePassView.PremiumLockedFrameColor;
                }
            }

            if (ClaimedGO) ClaimedGO.SetActive(isClaimed);
            
            if (LockedGO)
            {
                LockedGO.SetActive(locked && tierType == TierType.Premium);
            }
            
            if (ClaimButton)
            {
                ClaimButton.interactable = canClaim;
            }
        }

    }
}
