using UnityEngine;

namespace CozyFramework
{
    public class OpenDailyRewardsButton : MonoBehaviour
    {
        public void OnClick()
        {
            if (DailyRewardsView.Instance == null)
            {
                Debug.LogError("DailyRewardsView is missing!");
                return;
            }

            DailyRewardsView.Instance.ShowDailyRewards();
        }
    }
}
