using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PowerUpManager : MonoBehaviour
{
    [Header("Grid Reference")]
    public GridManager gridManager;

    // ── Blockade State ──
    // (unchanged from before)
    public void ApplyBlockade(GameObject user, float duration)
    {
        StartCoroutine(BlockadeCoroutine(user, duration));
    }
    private IEnumerator BlockadeCoroutine(GameObject user, float duration)
    {
        Vector2Int idx = gridManager.GetCellIndicesFromPosition(user.transform.position);
        if (idx.x < 0) yield break;
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

    // When the player picks Focus, we want *their opponent’s* next move to be limited.
    private bool  _focusPending    = false;
    private string _focusTargetTag = "";  

    /// <summary>
    /// Called by FocusPowerUp.Activate(user).
    /// Stores the *opponent’s* tag so only they get limited choices.
    /// </summary>
    public void ApplyFocus(GameObject user)
    {
        _focusPending = true;
        // if the picker is "Player", the target is "AI", and vice-versa
        _focusTargetTag = user.CompareTag("Player") ? "AI" : "Player";
    }

    /// <summary>
    /// Called from GameManager.HighlightRow to decide which columns to show.
    /// Only filters if focus is pending *and* this attacker matches the target.
    /// </summary>
    public List<int> GetAllowedColumns(string attackerTag)
    {
        int total = gridManager.cols;
        var all   = Enumerable.Range(0, total).ToList();

        if (_focusPending && attackerTag == _focusTargetTag)
        {
            // consume the focus so it only applies once
            _focusPending = false;

            // pick 3 random columns out of the full set
            var chosen = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                int idx = Random.Range(0, all.Count);
                chosen.Add(all[idx]);
                all.RemoveAt(idx);
            }
            return chosen;
        }

        // otherwise, no filter
        return all;
    }
}
