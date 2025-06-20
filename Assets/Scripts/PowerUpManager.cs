using System;
using System.Collections.Generic;
using UnityEngine;

public enum PowerUpType { None, Shield, GoldCache, Blockade }

public class PowerUpManager : MonoBehaviour
{
    [Header("References")] 
    public GridManager gridManager;
    public GameManager gameManager;
    public BlockadePowerUp blockadePowerUp;

    [Header("Power-Up Prefabs")]
    public GameObject shieldPrefab;
    public GameObject goldPrefab;
    public GameObject blockadePrefab;

    // Internal tracking of grid power-ups
    private Dictionary<Vector2Int, PowerUpType> _positions =
        new Dictionary<Vector2Int, PowerUpType>();
    private Dictionary<Vector2Int, GameObject> _icons =
        new Dictionary<Vector2Int, GameObject>();

    // Tracks which power-up each actor holds
    private Dictionary<GameManager.Actor, bool> _shieldActive =
        new Dictionary<GameManager.Actor, bool>();
    private Dictionary<GameManager.Actor, bool> _goldActive =
        new Dictionary<GameManager.Actor, bool>();
    private Dictionary<GameManager.Actor, bool> _blockadeActive =
        new Dictionary<GameManager.Actor, bool>();

    private GameObject _equippedIcon;

    void Awake()
    {
        // initialize flags
        foreach (GameManager.Actor a in Enum.GetValues(typeof(GameManager.Actor)))
        {
            _shieldActive[a]   = false;
            _goldActive[a]     = false;
            _blockadeActive[a] = false;
        }
        if (blockadePowerUp != null)
            blockadePowerUp.Initialize(gridManager);
    }

    public void SetupNewMatch()
    {
        // clear old grid icons
        foreach (var icon in _icons.Values) Destroy(icon);
        _icons.Clear();
        _positions.Clear();

        // place three random power-ups
        var candidates = new List<Vector2Int>();
        for (int r = 1; r < gridManager.rows - 1; r++)
            for (int c = 0; c < gridManager.cols; c++)
                candidates.Add(new Vector2Int(r, c));

        for (int i = 0; i < 3; i++)
        {
            int idx = UnityEngine.Random.Range(0, candidates.Count);
            var pos = candidates[idx];
            candidates.RemoveAt(idx);

            var type = (PowerUpType)UnityEngine.Random.Range(1, 4);
            _positions[pos] = type;

            // spawn icon on grid
            Vector3 world = gridManager.GetCellPosition(pos.x, pos.y);
            GameObject prefab = (type == PowerUpType.Shield)
                ? shieldPrefab
                : (type == PowerUpType.GoldCache)
                    ? goldPrefab
                    : blockadePrefab;
            if (prefab != null)
                _icons[pos] = Instantiate(
                    prefab, world, Quaternion.identity, transform
                );
        }

        // reset all equipped flags
        foreach (var actor in new List<GameManager.Actor>(_shieldActive.Keys))
        {
            _shieldActive[actor]   = false;
            _goldActive[actor]     = false;
            _blockadeActive[actor] = false;
        }
        ClearEquippedIcon();
        ClearBlockade();
    }

    public PowerUpType CollectIfAny(
        int row, int col, GameManager.Actor actor
    )
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
            case PowerUpType.Shield:
                _shieldActive[actor] = true;
                break;
            case PowerUpType.GoldCache:
                _goldActive[actor] = true;
                break;
            case PowerUpType.Blockade:
                _blockadeActive[actor] = true;
                break;
        }

        // update icon on ball
        if (gameManager.CurrentBallInstance != null)
            UpdateEquippedIcon(actor,
                gameManager.CurrentBallInstance.transform
            );

        return type;
    }

    public void UpdateEquippedIcon(
        GameManager.Actor actor,
        Transform parent
    )
    {
        ClearEquippedIcon();
        PowerUpType active = PowerUpType.None;
        if (_shieldActive[actor]) active = PowerUpType.Shield;
        else if (_goldActive[actor]) active = PowerUpType.GoldCache;
        else if (_blockadeActive[actor]) active = PowerUpType.Blockade;
        if (active == PowerUpType.None) return;

        GameObject prefab = (active == PowerUpType.Shield)
            ? shieldPrefab
            : (active == PowerUpType.GoldCache)
                ? goldPrefab
                : blockadePrefab;
        if (prefab == null) return;

        _equippedIcon = Instantiate(prefab, parent);
        _equippedIcon.transform.localPosition =
            new Vector3(-0.3f, 0.3f, 0f);
        _equippedIcon.transform.localScale =
            Vector3.one * 0.5f;
    }

    public void ClearEquippedIcon()
    {
        if (_equippedIcon != null)
            Destroy(_equippedIcon);
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
    /// Exposed so GameManager can highlight shield cells.
    /// </summary>
    public bool IsShieldActive(GameManager.Actor actor)
    {
        return _shieldActive.ContainsKey(actor)
            && _shieldActive[actor];
    }

    /// <summary>
    /// Clear any prior blockade from the grid.
    /// </summary>
    public void ClearBlockade()
    {
        if (blockadePowerUp != null)
            blockadePowerUp.Clear();
    }

    /// <summary>
    /// Disable 'count' entire cells on given row.
    /// </summary>
    public void ApplyBlockade(int rowIndex, int count = 2)
    {
        if (blockadePowerUp != null)
            blockadePowerUp.Apply(rowIndex, count);
    }
}
