using System.Threading.Tasks;
using Unity.Services.Leaderboards;
using UnityEngine;

namespace CozyFramework
{
    public class CozyLeaderboards : MonoBehaviour
    {
        public LeaderboardListTemplate LeaderboardListTemplate;
        
        public static CozyLeaderboards Instance;

        private void Awake()
        {
            SingletonHelper.InitializeSingleton(ref Instance, this);
        }
        
        public async Task AddScoreToLeaderboard(string leaderboardID, int score)
        {
            if (!PlayerManager.Instance.PlayerHasUsername) return;
            if (!LeaderboardExists(leaderboardID))
            {
                Debug.LogError($"Leaderboard with ID {leaderboardID} does not exist.");
                return;
            }
            
            await LeaderboardsService.Instance.AddPlayerScoreAsync(leaderboardID, score);
        }
        
        public bool LeaderboardExists(string leaderboardID) => LeaderboardListTemplate.Leaderboards.Exists(x => x.LeaderboardId == leaderboardID);
        
    }
}
