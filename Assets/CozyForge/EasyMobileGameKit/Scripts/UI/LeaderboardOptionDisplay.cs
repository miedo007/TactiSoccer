using TMPro;
using UnityEngine;

namespace CozyFramework
{
    public class LeaderboardOptionDisplay : MonoBehaviour
    {

        public GameObject SelectedImage;
        public TextMeshProUGUI LeaderboardName;


        private string leaderboardID;

        public void Initiliaze(string id)
        {
            leaderboardID = id;
        }

        public void Click()
        {
            LeaderboardView.Instance.SelectLeaderboardID(leaderboardID);
        }


    }
}
