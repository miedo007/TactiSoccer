using System.Collections.Generic;
using UnityEngine;

public class PowerUpInventory : MonoBehaviour
{
    [Tooltip("Max number of power-ups this actor can hold")]
    public int capacity = 3;

    private readonly List<PowerUpDefinition> _bag = new List<PowerUpDefinition>();

    [Header("Icon Display")]
    public Transform iconAnchor;    // e.g. empty child above sprite
    public GameObject iconPrefab;   // simple UI/Image prefab for equipped icons

    public void Pickup(PowerUpDefinition def)
    {
        Debug.Log($"[Inventory:{gameObject.name}] Attempting to pick up {def.powerUpName}");
        if (_bag.Count >= capacity)
        {
            Debug.Log($"  → FAILED: capacity {capacity} reached");
            return;
        }
        _bag.Add(def);
        Debug.Log($"  → SUCCESS: bag now has {_bag.Count} items");
        SpawnIcon(def.icon);
        AutoUse(def);
    }

    private void SpawnIcon(Sprite icon)
    {
        var go = Instantiate(iconPrefab, iconAnchor);
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.sprite = icon;
        // You could log here if needed:
        Debug.Log($"[Inventory:{gameObject.name}] Spawned icon for power-up");
    }

    private void AutoUse(PowerUpDefinition def)
    {
        Debug.Log($"[Inventory:{gameObject.name}] Auto-using {def.powerUpName}");
        def.Activate(gameObject);
        Remove(def);
    }

    private void Remove(PowerUpDefinition def)
    {
        _bag.Remove(def);
        Debug.Log($"[Inventory:{gameObject.name}] Removed {def.powerUpName}, {_bag.Count} left");
        // Optionally destroy the corresponding icon GameObject if you tracked it
    }
}
