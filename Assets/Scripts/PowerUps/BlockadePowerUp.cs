using UnityEngine;

[CreateAssetMenu(fileName = "BlockadePU", menuName = "PowerUps/Blockade")]
public class BlockadePowerUp : PowerUpDefinition
{
    [Tooltip("How long the blockade lasts (seconds)")]
    public float blockadeDuration = 2f;

    public override void Activate(GameObject user)
    {
        // Use the new faster API
        var manager = Object.FindFirstObjectByType<PowerUpManager>();
        if (manager == null)
        {
            Debug.LogError("BlockadePowerUp: no PowerUpManager found in scene!");
            return;
        }
        manager.ApplyBlockade(user, blockadeDuration);
    }
}
