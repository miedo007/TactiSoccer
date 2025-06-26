using System.Collections.Generic;
using Unity.Services.Economy.Model;
using UnityEngine;

namespace CozyFramework
{
    public class CozyVirtualPurchases : MonoBehaviour
    {
        const int k_EconomyPurchaseCostsNotMetStatusCode = 10504;

        public Dictionary<string, (List<ItemAndAmountSpec> costs, List<ItemAndAmountSpec> rewards)> virtualPurchaseTransactions
        {
            get;
            private set;
        }

        public static CozyVirtualPurchases Instance;

        void Awake()
        {
            SingletonHelper.InitializeSingleton(ref Instance, this);
        }

        private void OnEnable()
        {
            CozyEvents.PlayerInitialized += InitializeVirtualPurchaseLookup;
        }
        
        private void OnDisable()
        {
            CozyEvents.PlayerInitialized -= InitializeVirtualPurchaseLookup;
        }

        
        public void InitializeVirtualPurchaseLookup()
        {
            if (CozyEconomy.Instance.m_VirtualPurchaseDefinitions == null)
            {
                return;
            }
            
            virtualPurchaseTransactions = new Dictionary<string, (List<ItemAndAmountSpec> costs, List<ItemAndAmountSpec> rewards)>();

            foreach (var virtualPurchaseDefinition in CozyEconomy.Instance.m_VirtualPurchaseDefinitions)
            {
                var costs = ParseEconomyItems(virtualPurchaseDefinition.Costs);
                var rewards = ParseEconomyItems(virtualPurchaseDefinition.Rewards);

                virtualPurchaseTransactions[virtualPurchaseDefinition.Id] = (costs, rewards);
            }
        }
        
        public List<VirtualPurchaseDefinition> GetVirtualPurchases()
        {
            return CozyEconomy.Instance.m_VirtualPurchaseDefinitions;
        }

        List<ItemAndAmountSpec> ParseEconomyItems(List<PurchaseItemQuantity> itemQuantities)
        {
            var itemsAndAmountsSpec = new List<ItemAndAmountSpec>();

            foreach (var itemQuantity in itemQuantities)
            {
                var id = itemQuantity.Item.GetReferencedConfigurationItem().Id;
                itemsAndAmountsSpec.Add(new ItemAndAmountSpec(id, itemQuantity.Amount));
            }

            return itemsAndAmountsSpec;
        }


        
    }
}
