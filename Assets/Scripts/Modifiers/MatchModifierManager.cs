// Assets/Scripts/MatchModifiers/MatchModifierManager.cs
using System.Collections.Generic;
using UnityEngine;

public class MatchModifierManager : MonoBehaviour
{
    [Tooltip("All possible Modifiers")]
    public List<MatchModifierDefinition> allModifiers;

    [HideInInspector]
    public List<MatchModifierDefinition> activeModifiers = new List<MatchModifierDefinition>();

    // For a momentum limit, count how many back‐to‐back advances we've done:
    private int _consecutiveAdvances = 0;

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
    }

    public bool HasModifier(MatchModifierDefinition.ModifierType t)
    {
        return activeModifiers.Exists(m => m.type == t);
    }

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
        // resets the momentum counter whenever a tackle happens
        _consecutiveAdvances = 0;
    }
}
