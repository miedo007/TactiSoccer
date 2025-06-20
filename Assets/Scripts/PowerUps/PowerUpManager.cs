using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PowerUpManager : MonoBehaviour
{
    [Header("Grid Reference")]
    public GridManager gridManager;

    // ── Blockade State ──

    /// <summary>
    /// Called by BlockadePowerUp.Activate(user) to disable the row in front of the user
    /// for the given duration.
    /// </summary>
    public void ApplyBlockade(GameObject user, float duration)
    {
        StartCoroutine(BlockadeCoroutine(user, duration));
    }

    private IEnumerator BlockadeCoroutine(GameObject user, float duration)
    {
        // 1) map user world position → grid indices
        Vector2Int idx = gridManager.GetCellIndicesFromPosition(user.transform.position);
        if (idx.x < 0)
        {
            Debug.LogError("PowerUpManager: couldn't find cell for user position");
            yield break;
        }

        // 2) choose row to block
        bool isPlayer = user.CompareTag("Player");
        int targetRow = isPlayer ? idx.x + 1 : idx.x - 1;

        if (targetRow >= 0 && targetRow < gridManager.rows)
            SetRowInteractable(targetRow, false);

        // 3) wait
        yield return new WaitForSeconds(duration);

        // 4) re-enable
        if (targetRow >= 0 && targetRow < gridManager.rows)
            SetRowInteractable(targetRow, true);
    }

    private void SetRowInteractable(int row, bool enabled)
    {
        foreach (var cell in gridManager.Row(row))
            cell.GetComponent<Collider2D>().enabled = enabled;
    }

    // ── Focus State ──

    private bool  _focusPending   = false;
    private string _focusOwnerTag = "";

    /// <summary>
    /// Called by FocusPowerUp.Activate(user) to flag that the next turn
    /// the opponent’s move choices should be limited.
    /// </summary>
    public void ApplyFocus(GameObject user)
    {
        _focusPending   = true;
        _focusOwnerTag  = user.tag; // expects "Player" or "AI"
    }

    /// <summary>
    /// Called from GameManager.HighlightRow to decide which columns to show.
    /// If focus was pending and attacker != focusOwner, returns 3 random columns;
    /// otherwise returns all columns [0..cols-1].
    /// </summary>
    public List<int> GetAllowedColumns(string attackerTag)
    {
        int total = gridManager.cols;
        var all   = Enumerable.Range(0, total).ToList();

        if (_focusPending && attackerTag != _focusOwnerTag)
        {
            _focusPending = false;
            var chosen = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                int idx = Random.Range(0, all.Count);
                chosen.Add(all[idx]);
                all.RemoveAt(idx);
            }
            return chosen;
        }

        return all;
    }
}
