using UnityEngine;

[CreateAssetMenu(fileName = "NewPowerUp", menuName = "PowerUps/PowerUpDefinition")]
public abstract class PowerUpDefinition : ScriptableObject
{
    [Header("Basic Info")]
    public string powerUpName;
    public Sprite icon;

    /// <summary>
    /// Called when the power-up is used.
    /// </summary>
    /// <param name="user">The GameObject (Player or AI) that activates it.</param>
    public abstract void Activate(GameObject user);
}
