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
        // map user position → cell indices
        Vector2Int idx = gridManager.GetCellIndicesFromPosition(user.transform.position);
        if (idx.x < 0)
            yield break;

        bool isPlayer = user.CompareTag("Player");
        int targetRow = isPlayer ? idx.x + 1 : idx.x - 1;

        if (targetRow >= 0 && targetRow < gridManager.rows)
            SetRowInteractable(targetRow, false);

        yield return new WaitForSeconds(duration);

        if (targetRow >= 0 && targetRow < gridManager.rows)
            SetRowInteractable(targetRow, true);
    }

    private void SetRowInteractable(int row, bool enabled)
    {
        foreach (var cell in gridManager.Row(row))
            cell.GetComponent<Collider2D>().enabled = enabled;
    }

    // ── Focus State ──

    private bool _focusPending   = false;
    private string _focusOwnerTag = "";

    /// <summary>
    /// Called by FocusPowerUp.Activate(user).
    /// Marks that the opponent’s next turn should be filtered.
    /// </summary>
    public void ApplyFocus(GameObject user)
    {
        _focusPending   = true;
        _focusOwnerTag  = user.tag;  // e.g. "Player" or "AI"
        Debug.Log($"[PowerUpManager] Focus picked up by {_focusOwnerTag}");
    }

    /// <summary>
    /// Called from GameManager.HighlightRow to decide which columns to show.
    /// Logs internal state and, if focus is pending and the attacker is not
    /// the focus owner, returns a random subset of 3 columns.
    /// Otherwise returns all columns.
    /// </summary>
    public List<int> GetAllowedColumns(string attackerTag)
    {
        Debug.Log($"[PowerUpManager] GetAllowedColumns: attackerTag={attackerTag}, focusPending={_focusPending}, focusOwnerTag={_focusOwnerTag}");

        var all = Enumerable.Range(0, gridManager.cols).ToList();

        if (_focusPending && attackerTag != _focusOwnerTag)
        {
            _focusPending = false;
            Debug.Log($"[PowerUpManager] Applying Focus filter to {attackerTag}");

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
