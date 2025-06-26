using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.CloudCode;

namespace CozyFramework
{
    public class CozyCloudCode : MonoBehaviour
    {
        public GameObject LoadingGO;
        
        public static CozyCloudCode Instance;

        private void Awake()
        {
            SingletonHelper.InitializeSingleton(ref Instance, this);
        }
        
        
        public async Task SpendCurrency(string id, int amount)
        {
            LoadingGO.SetActive(true);
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "amount", amount },
                    { "currencyId", id }
                };

                await CloudCodeService.Instance.CallEndpointAsync("SpendCurrency", parameters);
                await CozyEconomy.Instance.RefreshCurrencyBalances();
                LoadingGO.SetActive(false);
            }
            catch (CloudCodeException e)
            {
                LoadingGO.SetActive(false);
                Debug.LogError($"Cloud Code error: {e.Message}");
                throw;
            }
            catch (Exception e)
            {
                LoadingGO.SetActive(false);
                Debug.LogError($"Unexpected error: {e.Message}");
                throw;
            }
        }
        
        public async Task GainCurrency(string id, int amount)
        {
            LoadingGO.SetActive(true);
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "amount", amount },
                    { "currencyId", id }
                };

                await CloudCodeService.Instance.CallEndpointAsync("GainCurrency", parameters);
                await CozyEconomy.Instance.RefreshCurrencyBalances();
                LoadingGO.SetActive(false);
            }
            catch (CloudCodeException e)
            {
                LoadingGO.SetActive(false);
                Debug.LogError($"Cloud Code error: {e.Message}");
                throw;
            }
            catch (Exception e)
            {
                LoadingGO.SetActive(false);
                Debug.LogError($"Unexpected error: {e.Message}");
                throw;
            }
        }
    }
}
