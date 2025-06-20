using UnityEngine;

public class PowerUpSpawner : MonoBehaviour
{
    [Tooltip("The generic pickup prefab (must have a PowerUpPickup component)")]
    public PowerUpPickup pickupPrefab;

    [Tooltip("All your concrete PowerUpDefinition assets")]
    public PowerUpDefinition[] possiblePowerUps;

    [Tooltip("How many power-ups to drop per match")]
    public int dropsPerMatch = 3;

    [Tooltip("Reference to your GridManager")]
    public GridManager gridManager;

    /// <summary>
    /// Spawns pickups on random cells, each with a random definition.
    /// </summary>
    public void SpawnDrops()
    {
        Debug.Log($"[PowerUpSpawner] Spawning {dropsPerMatch} pickups");
        for (int i = 0; i < dropsPerMatch; i++)
        {
            int r = Random.Range(0, gridManager.rows);
            int c = Random.Range(0, gridManager.cols);
            Vector3 spawnPos = gridManager.GetCellPosition(r, c);
            Debug.Log($"  → spawn #{i} at cell ({r},{c}) worldPos {spawnPos}");

            // Instantiate the generic pickup
            var instance = Instantiate(pickupPrefab, spawnPos, Quaternion.identity);

            // Assign it a random PowerUpDefinition
            int choice = Random.Range(0, possiblePowerUps.Length);
            instance.definition = possiblePowerUps[choice];
            Debug.Log($"     assigned definition: {instance.definition.powerUpName}");
        }
    }
}
