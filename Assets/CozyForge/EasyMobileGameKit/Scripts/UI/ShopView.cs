using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.UI;

namespace CozyFramework
{
    public class ShopView : MonoBehaviour
    {
        public List<RectTransform> RectTransforms = new List<RectTransform>();

        public ShopProductDisplay ShopProductPrefab;
        public Transform ShopProductContainer;
        private List<ShopProductDisplay> currentShopProducts = new List<ShopProductDisplay>();

        [System.Serializable]
        public class ShopProductEntry
        {
            public string ProductID;
            public string Description;
            public PayoutDefinition Payout;
            public float Price;
            public int Quantity;
            public string ISOCurrencyCode;
        }

        public List<ShopProductEntry> shopProducts = new List<ShopProductEntry>();

        public static ShopView Instance;

        private void Awake()
        {
            SingletonHelper.InitializeSingleton(ref Instance, this);
        }

        private void LoadShopProducts()
        {
            shopProducts.Clear();

            foreach (var catalogProduct in CozyInAppPurchases.Instance.CatalogProducts)
            {
                ShopProductEntry productEntry = new ShopProductEntry
                {
                    ProductID = catalogProduct.ProductID,
                    Description = catalogProduct.ProductDescription,
                    Payout = catalogProduct.Payout,
                    Price = catalogProduct.Price,
                    Quantity = catalogProduct.Quantity,
                    ISOCurrencyCode = catalogProduct.ISOCurrencyCode
                };
                shopProducts.Add(productEntry);
            }
        }

        public void OpenShop()
        {
            ClearProductDisplays();
            LoadShopProducts();

            foreach (var product in shopProducts)
            {
                ShopProductDisplay productDisplay = Instantiate(ShopProductPrefab, ShopProductContainer);
                productDisplay.SetProductID(product.ProductID);
                productDisplay.PriceText.text = product.ISOCurrencyCode + " " + product.Price;
                productDisplay.QuantityText.text = "x" + product.Quantity;
                productDisplay.Icon.sprite = CozyDatabase.Instance.GetCozyShopProduct(product.ProductID).Icon;
                currentShopProducts.Add(productDisplay);
            }

            foreach (var rect in RectTransforms)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }
        }

        private void ClearProductDisplays()
        {
            foreach (var product in currentShopProducts)
            {
                Destroy(product.gameObject);
            }

            currentShopProducts.Clear();
        }
    }
}