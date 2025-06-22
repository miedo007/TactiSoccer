// Assets/Scripts/MatchModifiers/MatchModifierDefinition.cs
using UnityEngine;

[CreateAssetMenu(menuName = "SoccerBet/Match Modifier")]
public class MatchModifierDefinition : ScriptableObject
{
    public string modifierName;            // e.g. “Momentum Limit”
    [TextArea] public string description;  // one-sentence rule
    public Sprite icon;                    // or an emoji texture
    public ModifierType type;              // drives the logic hook

    public enum ModifierType
    {
        MomentumLimit,
        BurnedColumn,
        MirrorClash,
        LockedColumn,
        ColumnLoyalty,
        FlightPath
        // (you can add more here later)
    }
}
