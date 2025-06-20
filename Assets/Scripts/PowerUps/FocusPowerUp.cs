using UnityEngine;

[CreateAssetMenu(fileName = "FocusPU", menuName = "PowerUps/Focus")]
public class FocusPowerUp : PowerUpDefinition
{
    public override void Activate(GameObject user)
    {
        var manager = Object.FindFirstObjectByType<PowerUpManager>();
        if (manager == null)
        {
            Debug.LogError("No PowerUpManager in scene!");
            return;
        }
        manager.ApplyFocus(user);
    }
}
