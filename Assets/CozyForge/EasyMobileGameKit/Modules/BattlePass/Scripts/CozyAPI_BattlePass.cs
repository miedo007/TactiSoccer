using System;
using System.Threading.Tasks;
using UnityEngine;

namespace CozyFramework
{
    public partial class CozyAPI
    {
        public async Task LoadBattlePass()
        {
            try
            {
                var progress = await CozyCloudSave.LoadData<BattlePassView.PlayerBattlePassProgress>("PLAYER_BATTLE_PASS");
                if (progress == null)
                {
                    BattlePassView.Instance.CurrentBattlePassProgress = new BattlePassView.PlayerBattlePassProgress();
                }
                else
                {
                    BattlePassView.Instance.CurrentBattlePassProgress = progress;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[CozyAPI] Error loading battle pass progress: " + e);
                BattlePassView.Instance.CurrentBattlePassProgress = new BattlePassView.PlayerBattlePassProgress();
            }
        }

        public async Task SaveBattlePass()
        {
            try
            {
                await CozyCloudSave.SaveData(
                    "PLAYER_BATTLE_PASS",
                    BattlePassView.Instance.CurrentBattlePassProgress
                );
            }
            catch (Exception e)
            {
                Debug.LogError("[CozyAPI] Error saving battle pass progress: " + e);
            }
        }
        
        public async Task AddBattlePassXP(int amount)
        {
            if (BattlePassView.Instance.CurrentBattlePassConfig == null || BattlePassView.Instance.CurrentBattlePassConfig.tiers.Count == 0)
            {
                Debug.LogError("[BattlePass] No battle pass configuration found.");
                return;
            }

            int lastTierIndex = BattlePassView.Instance.CurrentBattlePassConfig.tiers.Count - 1;

            // Prevent XP gain if already at the final tier
            if (BattlePassView.Instance.CurrentBattlePassProgress.CurrentTier >= lastTierIndex && BattlePassView.Instance.CurrentBattlePassProgress.currentXP >= BattlePassView.Instance.CurrentBattlePassConfig.tiers[lastTierIndex].requiredXP)
            {
                return;
            }

            BattlePassView.Instance.CurrentBattlePassProgress.currentXP += amount;
            
            BattlePassView.Instance.HandleLevelUps();
            await SaveBattlePass();

            // Refresh UI
            BattlePassView.Instance.RefreshUI();
        }
    }
}
