using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CozyFramework
{
    public class ShopProductDisplay : MonoBehaviour
    {
        public TextMeshProUGUI QuantityText;
        public TextMeshProUGUI PriceText;
        public Image Icon;

        private string productId;
        
        public void SetProductID (string id)
        {
            productId = id;
        }

        public void Click()
        {
            CozyInAppPurchases.Instance.HandleShopPurchase(productId);
        }
    
    }
}
