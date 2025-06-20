using UnityEngine;

public abstract class PowerUp : MonoBehaviour
{
    public enum Owner { Player, AI }
    public Owner owner { get; private set; }
    public bool IsActive { get; protected set; } = true;

    public void Assign(Owner who)
    {
        owner = who;
        OnPickup();
    }

    // Called immediately on pickup
    protected abstract void OnPickup();

    // Called at start of the owner's next defense turn:
    // return true to block that tackle
    public virtual bool OnDefenseResolution(ref bool tackleHappening) { return false; }

    // Called at start of the owner's next advance turn:
    // gives extra bonus
    public virtual int OnAdvanceResolution() { return 0; }

    // Called at start of the opponent’s defense choice:
    // return a list of columns to disable
    public virtual int[] OnDefenseChoices() { return null; }

    // Once any hook consumes it, deactivate
    protected void Consume() { IsActive = false; Destroy(this); }
}
