using UnityEngine;

public class GoldCachePowerUp : PowerUp
{
    protected override void OnPickup() { }

    public override int OnAdvanceResolution()
    {
        if (!IsActive) return 0;
        Consume();
        return 2; // +2 pot bonus
    }
}
