using System.Collections.Generic;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using UnityEngine.Purchasing;

namespace CozyFramework
{
    public class CozyInAppPurchases : MonoBehaviour, IStoreListener
    {
        private IStoreController storeController;
        private IExtensionProvider extensionProvider;
        
        [System.Serializable]
        public class CatalogProduct
        {
            public string ProductID;
            public string ProductDescription;
            public PayoutDefinition Payout;
            public float Price;
            public int Quantity;
            public string ISOCurrencyCode;
        }

        
        public List<CatalogProduct> CatalogProducts = new List<CatalogProduct>();
        public static CozyInAppPurchases Instance;
        
        private void Awake()
        {
            SingletonHelper.InitializeSingleton(ref Instance, this);
        }
        
        private void Start()
        {
            CozyEvents.AuthSuccess += InitializeIAP;
        }
        
        private void OnDisable()
        {
            CozyEvents.AuthSuccess -= InitializeIAP;
        }
        
        private void InitializeIAP()
        {
            CatalogProducts.Clear();
            InitializationOptions options = new InitializationOptions();
            
#if UNITY_EDITOR
            options.SetEnvironmentName("development");
#else
            options.SetEnvironmentName("production");
#endif
            
            ResourceRequest operation = Resources.LoadAsync<TextAsset>("IAPProductCatalog");
            operation.completed += CatalogLoaded;
        }
        
        private void CatalogLoaded(AsyncOperation operation)
        {
            ResourceRequest resourceRequest = operation as ResourceRequest;
            TextAsset textAsset = resourceRequest.asset as TextAsset;
            
            if (textAsset == null)
            {
                Debug.LogWarning("IAPProductCatalog not found in Resources.");
                return;
            }
            
            ProductCatalog catalog = JsonUtility.FromJson<ProductCatalog>(textAsset.text);

#if !UNITY_WEBGL
            // Unity IAP is not supported on WebGL builds
#if UNITY_EDITOR || UNITY_ANDROID
            var builder = ConfigurationBuilder.Instance(
                StandardPurchasingModule.Instance(AppStore.GooglePlay));
#elif UNITY_IOS
            var builder = ConfigurationBuilder.Instance(
                StandardPurchasingModule.Instance(AppStore.AppleAppStore));
#endif
            IAPConfigurationHelper.PopulateConfigurationBuilder(ref builder, catalog);
            
            foreach (var item in catalog.allProducts)
            {
                CatalogProducts.Add(new CatalogProduct
                {
                    ProductID = item.id,
                });
            }
            
            UnityPurchasing.Initialize(this, builder);
#else
            // On WebGL, Unity IAP is not supported
            Debug.LogWarning("Unity IAP is not supported on WebGL builds.");
#endif
        }
        
        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            storeController = controller;
            extensionProvider = extensions;
            
            foreach (var catalogProduct in CatalogProducts)
            {
                var product = storeController.products.WithID(catalogProduct.ProductID);

                if (product != null)
                {
                    catalogProduct.Payout = product.definition.payout;
                    catalogProduct.ProductDescription = product.metadata.localizedDescription;
                    catalogProduct.Price = (float)product.metadata.localizedPrice;
                    catalogProduct.ISOCurrencyCode = product.metadata.isoCurrencyCode;
                    
                    if (catalogProduct.Payout != null)
                    {
                        catalogProduct.Quantity = (int)catalogProduct.Payout.quantity;
                    }
                    else
                    {
                        catalogProduct.Quantity = 0;
                    }
                }
                else
                {
                    Debug.LogWarning($"Product with ID '{catalogProduct.ProductID}' not found in store.");
                }
            }
        }
        
        public void OnInitializeFailed(InitializationFailureReason error)
        {
            Debug.LogError("IAP initialization failed: " + error);
        }
        
        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            Debug.LogError("IAP initialization failed: " + error + " - " + message);
        }
        
        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs purchaseEvent)
        {
            var catalogItem = CatalogProducts.Find(p => p.ProductID == purchaseEvent.purchasedProduct.definition.id);
            if (catalogItem != null)
            {
                _ = CozyAPI.Instance.GainCurrency(catalogItem.Payout.subtype, (int)catalogItem.Payout.quantity);
            }
            else
            {
                Debug.LogWarning($"Purchased product '{purchaseEvent.purchasedProduct.definition.id}' not found in catalog.");
            }
            
            return PurchaseProcessingResult.Complete;
        }
        
        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            Debug.LogError("Purchase failed: " + product.definition.id + " - Reason: " + failureReason);
        }
        
        public void HandleShopPurchase(string productId)
        {
            if (storeController == null)
            {
                Debug.LogError("Store is not initialized yet.");
                return;
            }
            
            Product productToBuy = storeController.products.WithID(productId);
            
            if (productToBuy == null || !productToBuy.availableToPurchase)
            {
                Debug.LogError("Product not found or not available for purchase: " + productId);
                return;
            }
            
            storeController.InitiatePurchase(productToBuy);
        }
    }
}
