using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class MatchModifierManager : MonoBehaviour
{
    [Tooltip("All possible Modifiers")]
    public List<MatchModifierDefinition> allModifiers;

   [Header("Draft Settings")]
    [Tooltip("When true, once a modifier is picked it won't be offered again until the match ends")]
    [SerializeField] private bool uniqueDraft = true;

    // --- Define incompatible pairs here ---
    private static readonly Dictionary<
        MatchModifierDefinition.ModifierType,
        List<MatchModifierDefinition.ModifierType>
    > incompatibleMap = new Dictionary<MatchModifierDefinition.ModifierType, List<MatchModifierDefinition.ModifierType>>
    {
        { MatchModifierDefinition.ModifierType.BurnedColumn,
            new List<MatchModifierDefinition.ModifierType> {
                MatchModifierDefinition.ModifierType.ColumnLoyalty,
                MatchModifierDefinition.ModifierType.LockedColumn,
                MatchModifierDefinition.ModifierType.MirrorClash
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
    { MatchModifierDefinition.ModifierType.MirrorClash,
            new List<MatchModifierDefinition.ModifierType> {
                MatchModifierDefinition.ModifierType.BurnedColumn
            }
        },

        // you can add QuitOrDouble incompatibilities here if needed
    };

    // Holds only the two modifiers picked each turn
    private List<MatchModifierDefinition.ModifierType> _turnModifiers = 
    new List<MatchModifierDefinition.ModifierType>();

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

    // ── New: track whether DoubleAdvance is armed ──
    private bool _doubleAdvanceReady = false;

      // ── Blockade state ──
    private bool _blockadeReadyPlayer = false;
    private bool _blockadeReadyAI     = false;

    private bool _sabotageReadyPlayer = false;
    private bool _sabotageReadyAI     = false;

    // ── Add a field at the top:
    private bool _slipstreamReady = false;

    // CounterSurge state
     private bool _counterSurgeReadyPlayer = false;
    private bool _counterSurgeReadyAI     = false;
   
// — Counter Strike state —
    private int  _playerTackleCount           = 0;
    private int  _aiTackleCount               = 0;
    private bool _counterStrikeReadyPlayer    = false;
    private bool _counterStrikeReadyAI        = false;
    
    private bool _edgeBurstReady = false;


   private bool _stallReadyPlayer = false;
    private bool _stallReadyAI     = false;
    
    void Awake()
    {
        _gridManager = Object.FindFirstObjectByType<GridManager>();
        if (_gridManager == null)
            Debug.LogError("MatchModifierManager: No GridManager found in scene.");
    }

    /// <summary>
    /// Toggleable from GameManager.InitializeMatch()
    /// </summary>
    public bool UniqueDraft 
    {
        get => uniqueDraft;
        set => uniqueDraft = value;
    }

    private HashSet<MatchModifierDefinition.ModifierType> _usedModifiers
        = new HashSet<MatchModifierDefinition.ModifierType>();

    public void ResetUsedModifiers()
{
    _usedModifiers.Clear();
}

    /// <summary>
    /// Randomly pick up to 2 modifiers at the start of each match,
    /// reset all counters, and ensure incompatible pairs are never both selected.
    /// </summary>
    
    
    public void PickRandomModifiers()
    {

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
        _playerTackleCount        = 0;
        _aiTackleCount            = 0;
        _counterStrikeReadyPlayer = false;
        _counterStrikeReadyAI     = false;


        for (int i = 0; i < 2 && pool.Count > 0; i++)
        {
            int idx = Random.Range(0, pool.Count);
            var mod = pool[idx];
    
            Debug.Log($"[Modifiers] Added {mod.modifierName} ({mod.type})");

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

    public bool HasModifier(MatchModifierDefinition.ModifierType t) =>
    _turnModifiers.Contains(t);
    

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

    _dribbleCounts.TryGetValue(column, out var cnt);
    cnt++;
    _dribbleCounts[column] = cnt;
    Debug.Log($"[OnDribble] column={column} count={cnt}");
    if (cnt >= 3)
    {
        _gridMasteryReady = true;
        Debug.Log("[OnDribble] GridMastery is now ready!");
    }
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

/// <summary>
/// Call this whenever a tackle succeeds.
/// </summary>
public void OnCounterTackle(GameManager.Actor actor)
{
    if (!HasModifier(MatchModifierDefinition.ModifierType.CounterStrike)) return;

    if (actor == GameManager.Actor.Player)
    {
        _playerTackleCount++;
        if (_playerTackleCount >= 2)    // after 2 tackles, the next one is empowered
            _counterStrikeReadyPlayer = true;
    }
    else
    {
        _aiTackleCount++;
        if (_aiTackleCount >= 2)
            _counterStrikeReadyAI = true;
    }
}

/// <summary>
/// Returns true if the next tackle by this actor is a Counter Strike.
/// </summary>
public bool IsCounterStrikeReady(GameManager.Actor actor)
    => actor == GameManager.Actor.Player
        ? _counterStrikeReadyPlayer
        : _counterStrikeReadyAI;

/// <summary>
/// Consume the Counter Strike bonus and reset the count for that actor.
/// </summary>
public void ConsumeCounterStrike(GameManager.Actor actor)
{
    if (actor == GameManager.Actor.Player)
    {
        _counterStrikeReadyPlayer = false;
        _playerTackleCount        = 0;
    }
    else
    {
        _counterStrikeReadyAI     = false;
        _aiTackleCount            = 0;
    }
}
/// <summary>
    /// Draft `count` modifiers from only the allowed categories.
    /// Respects `uniqueDraft` (never re-offer until Reset) and
    /// will reshuffle the used set if pool exhaustion is hit.
    /// </summary>
    public List<MatchModifierDefinition> Draft(
        int count,
        params MatchModifierDefinition.ModifierCategory[] allowedCategories
    )
    {
        // 1) build & filter pool
        var pool = allModifiers
            .Where(m => allowedCategories.Contains(m.category))
            .ToList();
        if (uniqueDraft)
            pool = pool.Where(m => !_usedModifiers.Contains(m.type)).ToList();

        // 2) exhaustion? reshuffle used & rebuild
        if (uniqueDraft && pool.Count < count)
        {
            _usedModifiers.Clear();
            pool = allModifiers
                .Where(m => allowedCategories.Contains(m.category))
                .ToList();
        }

        // 3) shuffle
        for (int i = 0; i < pool.Count; i++)
        {
            int j = Random.Range(i, pool.Count);
            var tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
        }

        // 4) take up to `count`
        var draft = pool.Take(Mathf.Min(count, pool.Count)).ToList();

        // 5) mark used
        if (uniqueDraft)
            foreach (var m in draft)
                _usedModifiers.Add(m.type);

        return draft;
    }

/// <summary>
/// Apply exactly these two picks for the current turn.
/// </summary>
public void ApplyTurnModifiers(
    MatchModifierDefinition.ModifierType playerPick,
    MatchModifierDefinition.ModifierType aiPick
) {
    _turnModifiers.Clear();
    _turnModifiers.Add(playerPick);
    _turnModifiers.Add(aiPick);

    // 2) Log *every* pick so you can verify PushThrough shows up
    Debug.Log($"[MatchModifierManager] Player picked {playerPick}, AI picked {aiPick}");

    // ←— ADD THIS BLOCK:
    if (playerPick == MatchModifierDefinition.ModifierType.PushThrough)
        Debug.Log("[MatchModifierManager] Player armed PushThrough");
    if (aiPick == MatchModifierDefinition.ModifierType.PushThrough)
        Debug.Log("[MatchModifierManager] AI armed PushThrough");

    // Arm DoubleAdvance if either pick is it
    if (playerPick == MatchModifierDefinition.ModifierType.DoubleAdvance
     || aiPick     == MatchModifierDefinition.ModifierType.DoubleAdvance)
    {
        _doubleAdvanceReady = true;
        Debug.Log("[ApplyTurnModifiers] DoubleAdvance armed!");
    }

    // Arm GridMastery if either pick is it
    if (playerPick == MatchModifierDefinition.ModifierType.GridMastery
     || aiPick     == MatchModifierDefinition.ModifierType.GridMastery)
    {
        _gridMasteryReady = true;
        Debug.Log("[ApplyTurnModifiers] GridMastery armed!");
    }

    // ── Blockade: defender next turn only 2 columns ──
    if (playerPick == MatchModifierDefinition.ModifierType.Blockade)
        _blockadeReadyAI = true;    // Player blocked AI’s defense
    if (aiPick == MatchModifierDefinition.ModifierType.Blockade)
        _blockadeReadyPlayer = true; // AI blocked Player’s defense

    // ── Sabotage: attacker’s next move only 2 columns ──
    if (playerPick == MatchModifierDefinition.ModifierType.Sabotage)
        _sabotageReadyAI = true;
    if (aiPick == MatchModifierDefinition.ModifierType.Sabotage)
        _sabotageReadyPlayer = true;

    // ── Slipstream: next diagonal dribble → +1 extra row ──
    if (playerPick == MatchModifierDefinition.ModifierType.Slipstream
     || aiPick     == MatchModifierDefinition.ModifierType.Slipstream)
    {
        _slipstreamReady = true;
        Debug.Log("[ApplyTurnModifiers] Slipstream armed!");
    }

// ── Edge Burst: attacker next advance from an edge gets +1 row ──
    if (playerPick == MatchModifierDefinition.ModifierType.EdgeBurst
     || aiPick     == MatchModifierDefinition.ModifierType.EdgeBurst)
    {
        _edgeBurstReady = true;
        Debug.Log("[ApplyTurnModifiers] EdgeBurst armed!");
    }
// ── Stall: defender’s next mismatch stalls advance ──
if (playerPick == MatchModifierDefinition.ModifierType.Stall)
{
    _stallReadyPlayer = true;
        Debug.Log("[ApplyTurnModifiers] Stall armed for Player");
}
if (aiPick == MatchModifierDefinition.ModifierType.Stall)
{
    _stallReadyAI = true;
        Debug.Log("[ApplyTurnModifiers] Stall armed for AI");
}


    /// ── Arm CounterSurge only for the defender ──
    if (playerPick == MatchModifierDefinition.ModifierType.CounterSurge)
    {
        _counterSurgeReadyPlayer = true;
        Debug.Log("[ApplyTurnModifiers] CounterSurge armed for Player!");
    }
    if (aiPick == MatchModifierDefinition.ModifierType.CounterSurge)
    {
        _counterSurgeReadyAI = true;
        Debug.Log("[ApplyTurnModifiers] CounterSurge armed for AI!");
    }
}

/// <summary>
/// Clears last turn’s picks; call at the start of each new draft.
/// </summary>
public void ClearTurnModifiers() {
    _turnModifiers.Clear();

    // one-shot flags:
    _slipstreamReady          = false;
    _doubleAdvanceReady       = false;
    _gridMasteryReady         = false;
    _blockadeReadyPlayer      = false;
    _blockadeReadyAI          = false;
    _sabotageReadyPlayer      = false;
    _sabotageReadyAI          = false;
    _counterSurgeReadyPlayer  = false;
    _counterSurgeReadyAI      = false;
    _counterStrikeReadyPlayer = false;
    _counterStrikeReadyAI     = false;
    _edgeBurstReady = false;
    _stallReadyPlayer = false;
    _stallReadyAI     = false;
}


    /// <summary>
    /// Returns true if the next dribble should advance +2 rows.
    /// </summary>
    public bool IsDoubleAdvanceReady() => _doubleAdvanceReady;

    /// <summary>
    /// Call this after consuming the bonus so it only fires once.
    /// </summary>
    public void ConsumeDoubleAdvance() => _doubleAdvanceReady = false;
    /// <summary> Returns true if that defender is under blockade. </summary>
    public bool IsBlockadeReady(GameManager.Actor defender) =>
        defender == GameManager.Actor.Player
            ? _blockadeReadyPlayer
            : _blockadeReadyAI;

    /// <summary> Consume the blockade so it only applies once. </summary>
    public void ConsumeBlockade(GameManager.Actor defender)
    {
        if (defender == GameManager.Actor.Player)
            _blockadeReadyPlayer = false;
        else
            _blockadeReadyAI = false;
    }

    public bool IsSlipstreamReady() => _slipstreamReady;
public void ConsumeSlipstream() => _slipstreamReady = false;

/// <summary>True if that defender’s next tackle surges.</summary>
    public bool IsCounterSurgeReady(GameManager.Actor defender) =>
        defender == GameManager.Actor.Player
            ? _counterSurgeReadyPlayer
            : _counterSurgeReadyAI;

    /// <summary>Consume so CounterSurge only fires once.</summary>
    public void ConsumeCounterSurge(GameManager.Actor defender)
    {
        if (defender == GameManager.Actor.Player)
            _counterSurgeReadyPlayer = false;
        else
            _counterSurgeReadyAI = false;
    }  // ← make sure this closing brace is here

    /// <summary>True if that attacker’s next move is sabotaged (only 2 cols).</summary>
    public bool IsSabotageReady(GameManager.Actor actor)
    {
        return actor == GameManager.Actor.Player
            ? _sabotageReadyPlayer
            : _sabotageReadyAI;
    }

    /// <summary>Consume so Sabotage only fires once.</summary>
    public void ConsumeSabotage(GameManager.Actor actor)
    {
        if (actor == GameManager.Actor.Player)
            _sabotageReadyPlayer = false;
        else
            _sabotageReadyAI = false;
    }

    /// <summary>True if Edge Burst is armed for the next advance.</summary>
public bool IsEdgeBurstReady()
{
    return _edgeBurstReady;
}

/// <summary>Consume so Edge Burst only fires once.</summary>
public void ConsumeEdgeBurst()
{
    _edgeBurstReady = false;
}

// new query & consume:
public bool IsStallReady(GameManager.Actor actor) =>
    actor == GameManager.Actor.Player
      ? _stallReadyPlayer
      : _stallReadyAI;

public void ConsumeStall(GameManager.Actor actor)
{
    if (actor == GameManager.Actor.Player)
        _stallReadyPlayer = false;
    else
        _stallReadyAI = false;
}

} 