using System;
using System.Collections.Generic;
using UnityEngine;

public enum PowerUpType { None, Shield, GoldCache, Blockade }

public class PowerUpManager : MonoBehaviour
{
    [Header("References")] 
    public GridManager gridManager;
    public GameManager gameManager;

    [Header("Power-Up Prefabs")]
    public GameObject shieldPrefab;
    public GameObject goldPrefab;
    public GameObject blockadePrefab;

    // Internal tracking of grid power-ups
    private Dictionary<Vector2Int, PowerUpType> _positions = new Dictionary<Vector2Int, PowerUpType>();
    private Dictionary<Vector2Int, GameObject>   _icons     = new Dictionary<Vector2Int, GameObject>();

    // Tracks which power-up each actor currently holds
    private Dictionary<GameManager.Actor, bool> _shieldActive   = new Dictionary<GameManager.Actor, bool>();
    private Dictionary<GameManager.Actor, bool> _goldActive     = new Dictionary<GameManager.Actor, bool>();
    private Dictionary<GameManager.Actor, bool> _blockadeActive = new Dictionary<GameManager.Actor, bool>();

    // Icon displayed on the character to show equipped power-up
    private GameObject _equippedIcon;

    void Awake()
    {
        // Initialize flags for both Player and AI
        foreach (GameManager.Actor a in Enum.GetValues(typeof(GameManager.Actor)))
        {
            _shieldActive[a]   = false;
            _goldActive[a]     = false;
            _blockadeActive[a] = false;
        }
    }

    /// <summary>
    /// Call this at match start to randomly place three power-ups on the grid.
    /// </summary>
    public void SetupNewMatch()
    {
        // Destroy old icons
        foreach (var icon in _icons.Values) Destroy(icon);
        _icons.Clear();
        _positions.Clear();

        // Candidate cells (avoid goal rows)
        var candidates = new List<Vector2Int>();
        for (int r = 1; r < gridManager.rows - 1; r++)
            for (int c = 0; c < gridManager.cols; c++)
                candidates.Add(new Vector2Int(r, c));

        // Place three
        for (int i = 0; i < 3; i++)
        {
            int idx = UnityEngine.Random.Range(0, candidates.Count);
            var pos = candidates[idx];
            candidates.RemoveAt(idx);

            var type = (PowerUpType)UnityEngine.Random.Range(1, 4);
            _positions[pos] = type;

            // Spawn icon
            Vector3 world = gridManager.GetCellPosition(pos.x, pos.y);
            GameObject prefab = type == PowerUpType.Shield   ? shieldPrefab
                              : type == PowerUpType.GoldCache ? goldPrefab
                              : blockadePrefab;
            if (prefab != null)
                _icons[pos] = Instantiate(prefab, world, Quaternion.identity, transform);
        }

        // Reset equipped flags
        foreach (var actor in new List<GameManager.Actor>(_shieldActive.Keys))
        {
            _shieldActive[actor]   = false;
            _goldActive[actor]     = false;
            _blockadeActive[actor] = false;
        }

        ClearEquippedIcon();
    }

    /// <summary>
    /// If a power-up exists at (row,col), collect it for 'actor' and return its type.
    /// </summary>
    public PowerUpType CollectIfAny(int row, int col, GameManager.Actor actor)
    {
        var key = new Vector2Int(row, col);
        if (!_positions.TryGetValue(key, out var type))
            return PowerUpType.None;

        _positions.Remove(key);
        if (_icons.TryGetValue(key, out var icon))
        {
            Destroy(icon);
            _icons.Remove(key);
        }

        switch (type)
        {
            case PowerUpType.Shield:   _shieldActive[actor]   = true; break;
            case PowerUpType.GoldCache:_goldActive[actor]     = true; break;
            case PowerUpType.Blockade:_blockadeActive[actor] = true; break;
        }

        // Show equipped icon on the ball
        if (gameManager != null && gameManager.CurrentBallInstance != null)
            UpdateEquippedIcon(actor, gameManager.CurrentBallInstance.transform);

        return type;
    }

    /// <summary>
    /// Display the equipped power-up icon at a corner of the character prefab.
    /// </summary>
    public void UpdateEquippedIcon(GameManager.Actor actor, Transform parent)
    {
        ClearEquippedIcon();

        PowerUpType active = PowerUpType.None;
        if (_shieldActive[actor]) active = PowerUpType.Shield;
        else if (_goldActive[actor]) active = PowerUpType.GoldCache;
        else if (_blockadeActive[actor]) active = PowerUpType.Blockade;

        if (active == PowerUpType.None) return;

        GameObject prefab = active == PowerUpType.Shield   ? shieldPrefab
                          : active == PowerUpType.GoldCache ? goldPrefab
                          : blockadePrefab;
        if (prefab == null) return;

        _equippedIcon = Instantiate(prefab, parent);
        _equippedIcon.transform.localPosition = new Vector3(-0.3f, 0.3f, 0f);
        _equippedIcon.transform.localScale = Vector3.one * 0.5f;
    }

    public void ClearEquippedIcon()
    {
        if (_equippedIcon != null) Destroy(_equippedIcon);
        _equippedIcon = null;
    }

    public bool ConsumeShield(GameManager.Actor defender)
    {
        if (!_shieldActive[defender]) return false;
        _shieldActive[defender] = false;
        ClearEquippedIcon();
        return true;
    }

    public int GetAdvanceBonus(GameManager.Actor attacker)
    {
        if (!_goldActive[attacker]) return 0;
        _goldActive[attacker] = false;
        ClearEquippedIcon();
        return 2;
    }

    public bool ShouldBlockade(GameManager.Actor defender)
    {
        if (!_blockadeActive[defender]) return false;
        _blockadeActive[defender] = false;
        ClearEquippedIcon();
        return true;
    }

    /// <summary>
    /// Expose shield status so GameManager can highlight.
    /// </summary>
    public bool IsShieldActive(GameManager.Actor actor)
    {
        return _shieldActive.ContainsKey(actor) && _shieldActive[actor];
    }
}
