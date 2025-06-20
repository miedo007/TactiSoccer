using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public class PowerUpPickup : MonoBehaviour
{
    [HideInInspector] public PowerUpDefinition definition;
    SpriteRenderer _renderer;

    void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void Start()
    {
        if (definition != null)
            _renderer.sprite = definition.icon;
    }

    /// <summary>
    /// Called by GameManager when the ball lands on this cell.
    /// </summary>
    public void ManualPickup(GameObject picker)
    {
        Debug.Log($"[PowerUpPickup] ManualPickup {definition.powerUpName} by {picker.name}");
        var inv = picker.GetComponent<PowerUpInventory>();
        if (inv != null && definition != null)
        {
            inv.Pickup(definition);
            Destroy(gameObject);
        }
    }
}
