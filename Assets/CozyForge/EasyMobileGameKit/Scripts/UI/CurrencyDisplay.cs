using TMPro;
using Unity.Services.Economy.Model;
using UnityEngine;
using UnityEngine.UI;

namespace CozyFramework
{
    public class CurrencyDisplay : MonoBehaviour
    {
        public string ID;
        public bool FormatNumber;

        public Image Icon;
        public TextMeshProUGUI ValueText;
        

        private void Start()
        {
            CozyEvents.CurrencyRefresh += UpdateUI;
        }

        private void OnDisable()
        {
            CozyEvents.CurrencyRefresh -= UpdateUI;
        }

        private void OnEnable()
        {
            if(CozyEconomy.Instance != null) InitCurrencies();
        }

        private void UpdateUI(GetBalancesResult result)
        {
            foreach (var balance in result.Balances)
            {
                if (balance.CurrencyId != ID) continue;
                UpdateText(balance);
            }
        }

        public void InitCurrencies()
        {
            if(CozyEconomy.Instance.CurrencyBalances == null) return;
            foreach (var balance in CozyEconomy.Instance.CurrencyBalances.Balances)
            {
                if (balance.CurrencyId != ID) continue;
                UpdateText(balance);
                Icon.sprite = CozyDatabase.Instance.GetCozyCurrency(ID).Icon;
            }
        }

        private void UpdateText(PlayerBalance balance)
        {
            ValueText.text = FormatNumber ? CozyUtilities.FormatNumber(balance.Balance) : balance.Balance.ToString();
        }
        

    }
}
