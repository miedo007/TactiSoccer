using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Economy;
using Unity.Services.Economy.Model;
using UnityEngine;

namespace CozyFramework
{
    public class CozyEconomy : MonoBehaviour
    {

        public List<CurrencyDefinition> currencyDefinitions { get; private set; }
        public List<VirtualPurchaseDefinition> m_VirtualPurchaseDefinitions { get; private set; }
        public GetBalancesResult CurrencyBalances;

        public static CozyEconomy Instance;
        void Awake()
        {
            SingletonHelper.InitializeSingleton(ref Instance, this);
        }

        public async Task RefreshEconomyConfiguration()
        {
            await EconomyService.Instance.Configuration.SyncConfigurationAsync();
            
            if (this == null)
                return;

            currencyDefinitions = EconomyService.Instance.Configuration.GetCurrencies();
            m_VirtualPurchaseDefinitions = EconomyService.Instance.Configuration.GetVirtualPurchases();
        }

        public async Task RefreshCurrencyBalances()
        {
            GetBalancesResult balanceResult = null;

            try
            {
                balanceResult = await GetEconomyBalances();
            }
            catch (EconomyRateLimitedException e)
            {
                balanceResult = await CozyUGSUtils.RetryEconomyFunction(GetEconomyBalances, e.RetryAfter);
            }
            catch (Exception e)
            {
                Debug.Log("Problem getting Economy currency balances:");
                Debug.LogException(e);
            }
            
            if (this == null)
                return;

            CurrencyBalances = balanceResult;
            CozyEvents.OnCurrencyRefresh(balanceResult);
        }

        static Task<GetBalancesResult> GetEconomyBalances()
        {
            var options = new GetBalancesOptions { ItemsPerFetch = 100 };
            return EconomyService.Instance.PlayerBalances.GetBalancesAsync(options);
        }
        
    }
}
