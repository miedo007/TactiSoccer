using System.Collections.Generic;
using UnityEngine;

namespace CozyFramework
{
    public class LeaderboardListTemplate : ScriptableObject
    {
        [System.Serializable]
        public class LeaderboardEntry
        {
            public string LeaderboardId;
            public string LeaderboardName;
        }

        public List<LeaderboardEntry> Leaderboards = new List<LeaderboardEntry>();
    }
}
