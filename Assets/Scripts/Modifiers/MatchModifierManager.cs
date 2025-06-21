// Assets/Scripts/Modifiers/MatchModifierManager.cs
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

    // Mirror Clash needs the grid dimensions
    private GridManager _gridManager;

    void Awake()
    {
        // Use the new API instead of the obsolete FindObjectOfType
        _gridManager = Object.FindFirstObjectByType<GridManager>();
        if (_gridManager == null)
        {
            Debug.LogError("MatchModifierManager: No GridManager found in scene.");
        }
    }

    /// <summary>
    /// Randomly pick up to 2 modifiers at the start of each match
    /// and reset all counters.
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

        // Reset all modifier state
        _consecutiveAdvances = 0;
        _lastPlayerColumn = -1;
        _lastAIColumn     = -1;
    }

    public bool HasModifier(MatchModifierDefinition.ModifierType t)
    {
        return activeModifiers.Exists(m => m.type == t);
    }

    // --- Momentum Limit hooks ---

    public void OnAdvance()
    {
        if (HasModifier(MatchModifierDefinition.ModifierType.MomentumLimit))
        {
            _consecutiveAdvances++;
        }
    }

    public bool CanAdvance()
    {
        if (HasModifier(MatchModifierDefinition.ModifierType.MomentumLimit))
        {
            return _consecutiveAdvances < 3;
        }
        return true;
    }

    public void OnTackle()
    {
        _consecutiveAdvances = 0;
    }

    // --- Burned Column hooks ---

    public void SetLastUsedColumn(GameManager.Actor actor, int column)
    {
        if (actor == GameManager.Actor.Player)
            _lastPlayerColumn = column;
        else
            _lastAIColumn = column;
    }

    public int GetLastUsedColumn(GameManager.Actor actor)
    {
        return (actor == GameManager.Actor.Player)
            ? _lastPlayerColumn
            : _lastAIColumn;
    }

    // --- Mirror Clash hooks ---

    /// <summary>
    /// Returns true if the two chosen columns are mirror-symmetrical.
    /// (i.e. their indices sum to cols-1)
    /// </summary>
    public bool IsMirrorClash(int attackCol, int defendCol)
    {
        if (_gridManager == null) return false;
        return attackCol + defendCol == (_gridManager.cols - 1);
    }

    /// <summary>
    /// When Mirror Clash triggers, pushes the ball back one row.
    /// </summary>
   public void ApplyMirrorClash(ref int ballRow, GameManager.Actor attacker)
    {
        if (_gridManager == null) return;
        // Player “back” is row-1; AI “back” is row+1
        int delta = (attacker == GameManager.Actor.Player) ? -1 : +1;
        ballRow = Mathf.Clamp(ballRow + delta, 0, _gridManager.rows - 1);
    }
}
