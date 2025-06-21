// Assets/Scripts/MatchModifiers/MatchModifierManager.cs
using System.Collections.Generic;
using UnityEngine;

public class MatchModifierManager : MonoBehaviour
{
    [Tooltip("All possible Modifiers")]
    public List<MatchModifierDefinition> allModifiers;

    [HideInInspector]
    public List<MatchModifierDefinition> activeModifiers = new List<MatchModifierDefinition>();

    // --- Momentum Limit state ---
    private int _consecutiveAdvances = 0;

    // --- Burned Column state ---
    private int _lastPlayerColumn = -1;
    private int _lastAIColumn     = -1;

    // We assume GridManager exists in scene
    private int ColumnCount => FindObjectOfType<GridManager>().cols;

    /// <summary>
    /// Randomly picks up to two modifiers for this match
    /// and resets all per-match tracking.
    /// </summary>
    public void PickRandomModifiers()
    {
        activeModifiers.Clear();
        var pool = new List<MatchModifierDefinition>(allModifiers);
        for (int i = 0; i < 2 && pool.Count > 0; i++)
        {
            int idx = Random.Range(0, pool.Count);
            activeModifiers.Add(pool[idx]);
            pool.RemoveAt(idx);
        }

        // reset trackers
        _consecutiveAdvances = 0;
        _lastPlayerColumn = -1;
        _lastAIColumn     = -1;
    }

    public bool HasModifier(MatchModifierDefinition.ModifierType t)
    {
        return activeModifiers.Exists(m => m.type == t);
    }

    // ---------------------------------------------------
    // Momentum Limit
    // ---------------------------------------------------
    public void OnAdvance()
    {
        if (HasModifier(MatchModifierDefinition.ModifierType.MomentumLimit))
            _consecutiveAdvances++;
    }

    public bool CanAdvance()
    {
        if (HasModifier(MatchModifierDefinition.ModifierType.MomentumLimit))
            return _consecutiveAdvances < 3;
        return true;
    }

    public void OnTackle()
    {
        // reset the momentum counter
        _consecutiveAdvances = 0;
    }

    // ---------------------------------------------------
    // Burned Column
    // ---------------------------------------------------
    /// <summary>
    /// Remember which column this actor just used.
    /// </summary>
    public void SetLastUsedColumn(bool isPlayer, int column)
    {
        if (!HasModifier(MatchModifierDefinition.ModifierType.BurnedColumn)) return;
        if (isPlayer) _lastPlayerColumn = column;
        else          _lastAIColumn     = column;
    }

    /// <summary>
    /// Returns the column this actor cannot use this turn (if any).
    /// </summary>
    public int? GetBurnedColumn(bool isPlayer)
    {
        if (!HasModifier(MatchModifierDefinition.ModifierType.BurnedColumn))
            return null;
        return isPlayer ? _lastPlayerColumn : _lastAIColumn;
    }

    // ---------------------------------------------------
    // Mirror Clash
    // ---------------------------------------------------
    /// <summary>
    /// Returns true if the two chosen columns are symmetrically opposite.
    /// </summary>
    public bool IsMirrorClash(int attackerColumn, int defenderColumn)
    {
        if (!HasModifier(MatchModifierDefinition.ModifierType.MirrorClash))
            return false;

        // e.g. for 5 columns (0..4), pairs (0,4), (1,3) sum to 4
        return attackerColumn + defenderColumn == (ColumnCount - 1);
    }

    /// <summary>
    /// If MirrorClash applies, moves the ball back one row and returns true.
    /// Call this at the top of your ResolveTurn coroutine in GameManager.
    /// </summary>
    public bool TryMirrorClash(ref int ballRow, bool attackerIsPlayer)
    {
        if (!HasModifier(MatchModifierDefinition.ModifierType.MirrorClash))
            return false;

        // move ball back one row
        ballRow += attackerIsPlayer ? -1 : +1;
        return true;
    }
}
