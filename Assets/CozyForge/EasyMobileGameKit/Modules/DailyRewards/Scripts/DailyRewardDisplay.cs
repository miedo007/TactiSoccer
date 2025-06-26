using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CozyFramework
{
    public class DailyRewardDisplay : MonoBehaviour
    {
        public TextMeshProUGUI DayText, AmountText;
        public GameObject LootGO, ClaimedGO;
        public Image LootIcon;
        
        public Button ClaimButton;

        public void Click()
        {
            DailyRewardsView.Instance.OnClaimButtonPressed();
        }
    }
}
