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

    // --- Locked Column state ---
    private int _lockedColumn = -1;

    // --- Column Loyalty state ---
    private int _prevPlayerCol = -1;
    private int _playerStreak  = 0;
    private int _prevAICol     = -1;
    private int _aiStreak      = 0;

    // --- Flight Path state ---
    private int _fastLaneColumn = -1;

    // Mirror Clash needs the grid dimensions
    private GridManager _gridManager;

    void Awake()
    {
        _gridManager = Object.FindFirstObjectByType<GridManager>();
        if (_gridManager == null)
            Debug.LogError("MatchModifierManager: No GridManager found in scene.");
    }

    /// <summary>
    /// Randomly pick up to 2 modifiers at the start of each match
    /// and reset all counters.
    /// </summary>
    public void PickRandomModifiers()
    {
        activeModifiers.Clear();
        var pool = new List<MatchModifierDefinition>(allModifiers);

        // reset all modifier state
        _consecutiveAdvances = 0;
        _lastPlayerColumn    = -1;
        _lastAIColumn        = -1;
        _lockedColumn        = -1;
        _prevPlayerCol       = -1;
        _playerStreak        = 0;
        _prevAICol           = -1;
        _aiStreak            = 0;
        _fastLaneColumn      = -1;

        for (int i = 0; i < 2 && pool.Count > 0; i++)
        {
            int idx = Random.Range(0, pool.Count);
            var mod = pool[idx];
            activeModifiers.Add(mod);
            pool.RemoveAt(idx);

            if (mod.type == MatchModifierDefinition.ModifierType.LockedColumn && _gridManager != null)
            {
                _lockedColumn = Random.Range(0, _gridManager.cols);
            }
        }
    }

    public bool HasModifier(MatchModifierDefinition.ModifierType t)
    {
        return activeModifiers.Exists(m => m.type == t);
    }

    // --- Momentum Limit hooks ---
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
        return actor == GameManager.Actor.Player
            ? _lastPlayerColumn
            : _lastAIColumn;
    }

    // --- Locked Column query ---
    public int GetLockedColumn()
    {
        return HasModifier(MatchModifierDefinition.ModifierType.LockedColumn)
            ? _lockedColumn
            : -1;
    }

    // --- Mirror Clash hooks ---
    public bool IsMirrorClash(int attackCol, int defendCol)
    {
        if (_gridManager == null) return false;
        return attackCol + defendCol == (_gridManager.cols - 1);
    }

    public void ApplyMirrorClash(ref int ballRow, GameManager.Actor attacker)
    {
        if (_gridManager == null) return;
        int delta = attacker == GameManager.Actor.Player ? -1 : +1;
        ballRow = Mathf.Clamp(ballRow + delta, 0, _gridManager.rows - 1);
    }

    // --- Column Loyalty hooks ---
    public int GetLoyaltyBoost(GameManager.Actor actor, int column)
    {
        if (!HasModifier(MatchModifierDefinition.ModifierType.ColumnLoyalty))
            return 0;

        if (actor == GameManager.Actor.Player)
        {
            if (column == _prevPlayerCol)
                _playerStreak++;
            else
            {
                _prevPlayerCol = column;
                _playerStreak = 1;
            }
            return (_playerStreak == 2) ? 1 : 0;
        }
        else
        {
            if (column == _prevAICol)
                _aiStreak++;
            else
            {
                _prevAICol = column;
                _aiStreak = 1;
            }
            return (_aiStreak == 2) ? 1 : 0;
        }
    }

    // --- Flight Path hooks ---
    /// <summary>
    /// Call once per turn to pick a random “fast lane” column.
    /// </summary>
    public void PickFastLaneColumn()
    {
        if (!HasModifier(MatchModifierDefinition.ModifierType.FlightPath) || _gridManager == null)
            return;
        _fastLaneColumn = Random.Range(0, _gridManager.cols);
    }

    /// <summary>
    /// Returns the current fast-lane column, or –1 if inactive.
    /// </summary>
    public int GetFastLaneColumn()
    {
        return HasModifier(MatchModifierDefinition.ModifierType.FlightPath)
            ? _fastLaneColumn
            : -1;
    }
}
