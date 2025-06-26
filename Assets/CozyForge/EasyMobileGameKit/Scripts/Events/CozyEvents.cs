using Unity.Services.RemoteConfig;
using Unity.Services.Economy.Model;
using UnityEngine;

namespace CozyFramework
{
    public class CozyEvents : MonoBehaviour
    {
        public static event System.Action AuthSuccess;
        public static event System.Action PlayerInitialized;
        public static event System.Action<GetBalancesResult> CurrencyRefresh;
        public static event System.Action<RuntimeConfig> RemoteConfigsFetched;
        public static event System.Action<string, MakeVirtualPurchaseResult> VirtualPurchaseSuccessful;
        

        public static CozyEvents Instance;
        private void Awake()
        {
            SingletonHelper.InitializeSingleton(ref Instance, this);
        }
    
        public static void OnAuthSuccess()
        {
            AuthSuccess?.Invoke();
        }
        
        public static void OnPlayerInitialized()
        {
            PlayerInitialized?.Invoke();
        }
    
        public static void OnCurrencyRefresh(GetBalancesResult result)
        {
            CurrencyRefresh?.Invoke(result);
        }
        
        public static void OnRemoteConfigsFetched(RuntimeConfig config)
        {
            RemoteConfigsFetched?.Invoke(config);
        }
        
        public static void OnVirtualPurchaseSuccessful(string virtualPurchaseId, MakeVirtualPurchaseResult result)
        {
            VirtualPurchaseSuccessful?.Invoke(virtualPurchaseId, result);
        }
    
    }
}
