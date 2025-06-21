// Assets/Scripts/MatchModifiers/MatchModifierManager.cs
using System.Collections.Generic;
using UnityEngine;

public class MatchModifierManager : MonoBehaviour
{
    [Tooltip("All possible Modifiers")]
    public List<MatchModifierDefinition> allModifiers;

    [HideInInspector]
    public List<MatchModifierDefinition> activeModifiers = new List<MatchModifierDefinition>();

    // For a momentum limit, count how many back‐to‐back advances we've done
    private int _consecutiveAdvances = 0;

    // For Burned Column, track the last used column per actor
    private int _lastPlayerColumn = -1;
    private int _lastAIColumn     = -1;

    /// <summary>
    /// Randomly picks 1 or 2 modifiers at the start of a match,
    /// and resets all per‐match state.
    /// </summary>
    public void PickRandomModifiers()
    {
        activeModifiers.Clear();
        _consecutiveAdvances = 0;
        _lastPlayerColumn = -1;
        _lastAIColumn     = -1;

        var pool = new List<MatchModifierDefinition>(allModifiers);
        // pick up to 2 at random
        for (int i = 0; i < 2 && pool.Count > 0; i++)
        {
            int idx = Random.Range(0, pool.Count);
            activeModifiers.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
    }

    /// <summary>
    /// Returns true if the given modifier type is active this match.
    /// </summary>
    public bool HasModifier(MatchModifierDefinition.ModifierType t)
    {
        return activeModifiers.Exists(m => m.type == t);
    }

    /// <summary>
    /// Call when a player or AI successfully advances.
    /// </summary>
    public void OnAdvance()
    {
        if (HasModifier(MatchModifierDefinition.ModifierType.MomentumLimit))
            _consecutiveAdvances++;
    }

    /// <summary>
    /// Returns whether advancing is currently allowed under the MomentumLimit rule.
    /// </summary>
    public bool CanAdvance()
    {
        if (HasModifier(MatchModifierDefinition.ModifierType.MomentumLimit))
            return _consecutiveAdvances < 3; // use 3 as the limit
        return true;
    }

    /// <summary>
    /// Call whenever a tackle occurs to reset momentum.
    /// </summary>
    public void OnTackle()
    {
        _consecutiveAdvances = 0;
    }

    /// <summary>
    /// Record which column was just used by Player (true) or AI (false).
    /// Call this immediately after attackChoice is finalized.
    /// </summary>
    public void SetLastUsedColumn(bool isPlayer, int col)
    {
        if (isPlayer)  _lastPlayerColumn = col;
        else           _lastAIColumn     = col;
    }

    /// <summary>
    /// Retrieve the last used column for Player (true) or AI (false).
    /// Returns -1 if none yet.
    /// </summary>
    public int GetLastUsedColumn(bool isPlayer)
    {
        return isPlayer ? _lastPlayerColumn : _lastAIColumn;
    }
}
