using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Economy;
using Unity.Services.Economy.Model;
using UnityEngine;

namespace CozyFramework
{
    public partial class CozyAPI : MonoBehaviour
    {
        public static CozyAPI Instance;

        private void Awake()
        {
            SingletonHelper.InitializeSingleton(ref Instance, this);
        }

        public void PurchaseShopProduct(string productId)
        {
            CozyInAppPurchases.Instance.HandleShopPurchase(productId);
        }
        
        public async Task SubmitScoreToLeaderboard(string leaderboardId, int score)
        {
            if (!PlayerManager.Instance.PlayerHasUsername) return;
            await CozyLeaderboards.Instance.AddScoreToLeaderboard(leaderboardId, score);
        }
        
        public async Task GainCurrency(string id, int amount)
        {
            await CozyCloudCode.Instance.GainCurrency(id, amount);
        }
        
        public async Task SpendCurrency(string id, int amount)
        {
            await CozyCloudCode.Instance.SpendCurrency(id, amount);
        }
        
        public int GetCurrencyValue (string currencyId)
        {
            foreach (var currency in CozyEconomy.Instance.CurrencyBalances.Balances)
            {
                if (currency.CurrencyId == currencyId)
                {
                    return (int)currency.Balance;
                }
            }

            return 0;
        }
        
        public async Task SavePlayerData<T>(string key, T value)
        {
            await CozyCloudSave.SaveData(key, value);
        }
        
        public async Task SavePlayerData(Dictionary<string, object> data)
        {
            await CozyCloudSave.SaveData(data);
        }
        
        
        public void SendAnalyticEvent(string eventName, Dictionary<string, object> parameters)
        {
            CozyAnalyticsEvents.Instance.SendAnalyticEvent(eventName, parameters);
        }


        public async Task<MakeVirtualPurchaseResult> MakeVirtualPurchaseAsync(string virtualPurchaseId)
        {
            try
            {
                MakeVirtualPurchaseResult result = await EconomyService.Instance.Purchases.MakeVirtualPurchaseAsync(virtualPurchaseId);
                await CozyEconomy.Instance.RefreshCurrencyBalances();
                CozyEvents.OnVirtualPurchaseSuccessful(virtualPurchaseId, result);
                return result;

            }
            catch (EconomyException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return default;
            }
        }
        
    }
}
