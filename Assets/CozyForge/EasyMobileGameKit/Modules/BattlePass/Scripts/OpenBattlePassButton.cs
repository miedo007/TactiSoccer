using UnityEngine;

namespace CozyFramework
{
    public class OpenBattlePassButton : MonoBehaviour
    {
        public void OnClick()
        {
            if (BattlePassView.Instance == null)
            {
                Debug.LogError("BattlePassView is missing!");
                return;
            }

            BattlePassView.Instance.ShowBattlePass();
        }
    }
}
