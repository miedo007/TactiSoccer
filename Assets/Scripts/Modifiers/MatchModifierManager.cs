using System.Collections.Generic;
using UnityEngine;

public class MatchModifierManager : MonoBehaviour
{
    [Tooltip("All possible Modifiers")]
    public List<MatchModifierDefinition> allModifiers;

    [HideInInspector]
    public List<MatchModifierDefinition> activeModifiers = new List<MatchModifierDefinition>();

    // --- Define incompatible pairs here ---
    private static readonly Dictionary<
        MatchModifierDefinition.ModifierType,
        List<MatchModifierDefinition.ModifierType>
    > incompatibleMap = new Dictionary<MatchModifierDefinition.ModifierType, List<MatchModifierDefinition.ModifierType>>
    {
        { MatchModifierDefinition.ModifierType.BurnedColumn,
            new List<MatchModifierDefinition.ModifierType> {
                MatchModifierDefinition.ModifierType.ColumnLoyalty,
                MatchModifierDefinition.ModifierType.LockedColumn
            }
        },
        { MatchModifierDefinition.ModifierType.ColumnLoyalty,
            new List<MatchModifierDefinition.ModifierType> {
                MatchModifierDefinition.ModifierType.BurnedColumn
            }
        },

        { MatchModifierDefinition.ModifierType.LockedColumn,
            new List<MatchModifierDefinition.ModifierType> {
                MatchModifierDefinition.ModifierType.BurnedColumn
            }
        },
        // you can add QuitOrDouble incompatibilities here if needed
    };

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

    // --- Quit or Double state ---
    private int _quitOrDoubleColumn = -1;

    // Mirror Clash needs the grid dimensions
    private GridManager _gridManager;

    // — Grid Mastery state —
   private Dictionary<int,int> _dribbleCounts = new Dictionary<int,int>();
   private bool _gridMasteryReady = false;

   // — Dynamic Corridor  state —
   private int _playerDiagonalCount = 0;
    private int _aiDiagonalCount     = 0;
    private bool _dynamicCorridorReadyPlayer = false;
    private bool _dynamicCorridorReadyAI     = false;
   

    void Awake()
    {
        _gridManager = Object.FindFirstObjectByType<GridManager>();
        if (_gridManager == null)
            Debug.LogError("MatchModifierManager: No GridManager found in scene.");
    }

    /// <summary>
    /// Randomly pick up to 2 modifiers at the start of each match,
    /// reset all counters, and ensure incompatible pairs are never both selected.
    /// </summary>
    public void PickRandomModifiers()
    {
        activeModifiers.Clear();
        var pool = new List<MatchModifierDefinition>(allModifiers);

        // reset all modifier state
        _consecutiveAdvances   = 0;
        _lastPlayerColumn      = -1;
        _lastAIColumn          = -1;
        _lockedColumn          = -1;
        _prevPlayerCol         = -1;
        _playerStreak          = 0;
        _prevAICol             = -1;
        _aiStreak              = 0;
        _quitOrDoubleColumn    = -1;
        _dribbleCounts.Clear();
        _gridMasteryReady = false;
        _playerDiagonalCount          = 0;
    _aiDiagonalCount              = 0;
    _dynamicCorridorReadyPlayer   = false;
    _dynamicCorridorReadyAI       = false;


        for (int i = 0; i < 2 && pool.Count > 0; i++)
        {
            int idx = Random.Range(0, pool.Count);
            var mod = pool[idx];
            activeModifiers.Add(mod);

            // remove that modifier from pool
            pool.RemoveAt(idx);

            // remove any incompatible modifiers
            if (incompatibleMap.TryGetValue(mod.type, out var badTypes))
                pool.RemoveAll(m => badTypes.Contains(m.type));

            // special LockedColumn logic
            if (mod.type == MatchModifierDefinition.ModifierType.LockedColumn && _gridManager != null)
                _lockedColumn = Random.Range(0, _gridManager.cols);

            // special QuitOrDouble logic
            if (mod.type == MatchModifierDefinition.ModifierType.QuitOrDouble && _gridManager != null)
                _quitOrDoubleColumn = Random.Range(0, _gridManager.cols);
        }
    }

    public bool HasModifier(MatchModifierDefinition.ModifierType t)
        => activeModifiers.Exists(m => m.type == t);

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

    /// <summary>
    /// Called when a tackle happens. Resets momentum, clears burned-column state,
    /// and (if ColumnLoyalty is active) wipes out the loyalty streak for that actor.
    /// </summary>
    public void OnTackle(GameManager.Actor actor)
    {
        // reset momentum
        _consecutiveAdvances = 0;

        // reset burned-column
        _lastPlayerColumn = -1;
        _lastAIColumn     = -1;

        // reset loyalty streak
        if (HasModifier(MatchModifierDefinition.ModifierType.ColumnLoyalty))
        {
            if (actor == GameManager.Actor.Player)
            {
                _prevPlayerCol = -1;
                _playerStreak  = 0;
            }
            else
            {
                _prevAICol = -1;
                _aiStreak  = 0;
            }
        }
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
        => actor == GameManager.Actor.Player
            ? _lastPlayerColumn
            : _lastAIColumn;

    // --- Locked Column query ---
    public int GetLockedColumn()
        => HasModifier(MatchModifierDefinition.ModifierType.LockedColumn)
            ? _lockedColumn
            : -1;

    // --- Mirror Clash hooks ---
    public bool IsMirrorClash(int attackCol, int defendCol, int centerColumn)
 {
     // mirror around the ball’s column
     return attackCol + defendCol == centerColumn * 2;
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
                _playerStreak  = 1;
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
                _aiStreak  = 1;
            }
            return (_aiStreak == 2) ? 1 : 0;
        }
    }

    // --- Flight Path state ---
    private int _fastLaneColumn = -1;

    public void PickFastLaneColumn()
    {
        if (!HasModifier(MatchModifierDefinition.ModifierType.FlightPath) || _gridManager == null)
            return;
        _fastLaneColumn = Random.Range(0, _gridManager.cols);
    }

    public int GetFastLaneColumn()
        => HasModifier(MatchModifierDefinition.ModifierType.FlightPath)
            ? _fastLaneColumn
            : -1;

    public void SetFastLaneColumn(int col)
        => _fastLaneColumn = col;

    // --- Quit or Double state accessors ---
    public int GetQuitOrDoubleColumn()
        => HasModifier(MatchModifierDefinition.ModifierType.QuitOrDouble)
            ? _quitOrDoubleColumn
            : -1;

    public void SetQuitOrDoubleColumn(int col)
        => _quitOrDoubleColumn = col;

         // Grid Mastery methods (add here)
    /// <summary>
    /// Call this whenever the player completes a dribble (not a tackle).
    /// </summary>
    public void OnDribble(int column)
    {
        if (!HasModifier(MatchModifierDefinition.ModifierType.GridMastery))
            return;

        // increment the counter for that column
        if (_dribbleCounts.ContainsKey(column))
            _dribbleCounts[column]++;
        else
            _dribbleCounts[column] = 1;

        // once any column reaches 3 dribbles, flag the next move
        if (_dribbleCounts[column] >= 3)
            _gridMasteryReady = true;
    }

    /// <summary>
    /// Returns true if the next dribble should ignore adjacency.
    /// </summary>
    public bool IsGridMasteryReady()
        => _gridMasteryReady;

    /// <summary>
    /// Consume the free-move and reset counters.
    /// </summary>
    public void ConsumeGridMastery()
    {
        _gridMasteryReady = false;
        _dribbleCounts.Clear();
    
    }
    /// <summary>
/// Call this whenever a dribble succeeds from a different column than the last ball position.
/// </summary>
public void OnDiagonalDribble(GameManager.Actor actor)
{
    if (!HasModifier(MatchModifierDefinition.ModifierType.DynamicCorridor)) return;
    if (actor == GameManager.Actor.Player)
    {
        _playerDiagonalCount++;
        if (_playerDiagonalCount >= 3) _dynamicCorridorReadyPlayer = true;
    }
    else
    {
        _aiDiagonalCount++;
        if (_aiDiagonalCount >= 3) _dynamicCorridorReadyAI = true;
    }
}

public bool IsDynamicCorridorReady(GameManager.Actor actor)
    => actor == GameManager.Actor.Player
         ? _dynamicCorridorReadyPlayer
         : _dynamicCorridorReadyAI;

public void ConsumeDynamicCorridor(GameManager.Actor actor)
{
    if (actor == GameManager.Actor.Player)
    {
        _dynamicCorridorReadyPlayer = false;
        _playerDiagonalCount = 0;
    }
    else
    {
        _dynamicCorridorReadyAI = false;
        _aiDiagonalCount = 0;
    }
}
}
