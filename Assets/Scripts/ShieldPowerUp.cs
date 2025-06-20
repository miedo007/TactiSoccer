using UnityEngine;

public class ShieldPowerUp : PowerUp
{
    protected override void OnPickup()
    {
        // nothing now; GameManager will highlight
    }

    public override bool OnDefenseResolution(ref bool tackleHappening)
    {
        if (!IsActive) return false;
        // cancel the tackle
        tackleHappening = false;
        Consume();
        return true;
    }
}
