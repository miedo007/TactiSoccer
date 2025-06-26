using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CozyFramework
{
    public partial class CozyDatabase : MonoBehaviour
    {
        public static CozyDatabase Instance;

        [Header("Populated Automatically")]
        public List<CozyCurrency> Currencies = new List<CozyCurrency>();
        public List<CozyShopProduct> ShopProducts = new List<CozyShopProduct>();

        private void Awake()
        {
            SingletonHelper.InitializeSingleton(ref Instance, this);
            
            Currencies = new List<CozyCurrency>(Resources.LoadAll<CozyCurrency>(""));
            ShopProducts = new List<CozyShopProduct>(Resources.LoadAll<CozyShopProduct>(""));
        }
        
        public CozyCurrency GetCozyCurrency(string currencyId)
        {
            return Currencies.FirstOrDefault(currency => currency.Id == currencyId);
        }
        
        public CozyShopProduct GetCozyShopProduct(string productId)
        {
            return ShopProducts.FirstOrDefault(product => product.Id == productId);
        }
    }
}