using UnityEngine;

[CreateAssetMenu(menuName = "SoccerBet/Match Modifier")]
public class MatchModifierDefinition : ScriptableObject
{
    public string modifierName;            // e.g. “Momentum Limit”
    [TextArea] public string description;  // one-sentence rule
    public Sprite icon;                    // or an emoji texture

    // New: category to control which pool the modifier belongs to
    public ModifierCategory category;

    // Drives the logic hook
    public ModifierType type;

    /// <summary>
    /// Which phase(s) this modifier applies to.
    /// </summary>
    public enum ModifierCategory
    {
        Offensive,  // when attacking
        Defensive,  // when defending
        Tactical    // always available
    }

    /// <summary>
    /// The identifier used in code to implement the modifier’s effect.
    /// </summary>
    public enum ModifierType
    {
        MomentumLimit,
        BurnedColumn,
        MirrorClash,
        LockedColumn,
        ColumnLoyalty,
        FlightPath,
        GridMastery,
        DynamicCorridor,
        CounterStrike, 
        QuitOrDouble,
        Blockade,
        Slipstream,
        CounterSurge,   
        Sabotage, 
        PushThrough,          
        DoubleAdvance,
        ForcedDiagonal,
        EdgeBurst,
        Stall 
        // (you can add more here later)
    }
}
