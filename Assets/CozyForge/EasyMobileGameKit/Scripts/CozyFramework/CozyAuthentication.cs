using System;
using System.Threading.Tasks;
using Unity.Services.Analytics;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace CozyFramework
{
    public class CozyAuthentication : MonoBehaviour
    {
        public enum AuthenticationType
        {
            Anonymous,
        }

        public AuthenticationType AuthType;
        
        public static CozyAuthentication Instance;

        private void Awake()
        {
            SingletonHelper.InitializeSingleton(ref Instance, this);
        }

        async void Start()
        {
            try
            {
                await InitializePlayerAccount();
            }
            catch (AuthenticationException authEx)
            {
                await HandleAuthenticationException(authEx);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private async Task InitializePlayerAccount()
        {
            await UnityServices.InitializeAsync();
            if (this == null) return;

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await SignInAsync();
                if (this == null) return;
            }

            AnalyticsService.Instance.StartDataCollection();
            CozyEvents.OnAuthSuccess();

            var refreshEconomyTask = CozyEconomy.Instance.RefreshEconomyConfiguration();
            var refreshCurrencyTask = CozyEconomy.Instance.RefreshCurrencyBalances();
            
            await Task.WhenAll(
                refreshEconomyTask,
                refreshCurrencyTask
            );
            if (this == null) return;
            
            CozyEvents.OnPlayerInitialized();
            Debug.Log("Authentication: <color=green>SUCCESSFUL</color>");
        }

        private async Task SignInAsync()
        {
            try
            {
                switch (AuthType)
                {
                    case AuthenticationType.Anonymous:
                        await AuthenticationService.Instance.SignInAnonymouslyAsync();
                        break;
                    default:
                        throw new NotImplementedException($"Authentication type {AuthType} is not implemented.");
                }
            }
            catch (AuthenticationException)
            {
                
            }
        }

        private async Task HandleAuthenticationException(AuthenticationException authEx)
        {
            if (authEx.ErrorCode == 10007)
            {
                await InitializePlayerAccount();
            }
            else
            {
                Debug.LogError($"Authentication failed: {authEx.Message}");
            }
        }
    }
}
