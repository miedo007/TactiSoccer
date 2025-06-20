// BlockadePowerUp.cs
using System.Collections.Generic;
using UnityEngine;

public class BlockadePowerUp : MonoBehaviour
{
    public GridManager gridManager;
    public PowerUpManager powerUpManager;

    // This could move the logic out of GameManager if you like:
    public void ApplyBlockade(GameManager.Actor defender, int ballRow)
    {
        if (!powerUpManager.ShouldBlockade(defender)) return;

        var cols = new List<int>();
        for (int c = 0; c < gridManager.cols; c++)
            cols.Add(c);
        // pick two to disable
        for (int i = 0; i < 2; i++)
        {
            int idx = Random.Range(0, cols.Count);
            int blockCol = cols[idx];
            cols.RemoveAt(idx);
            var cellGO = gridManager.cells[ballRow, blockCol];
            cellGO.GetComponent<Collider2D>().enabled = false;
        }
        // you can bubble up a message or trigger UI here
        Debug.Log("Blockade applied: 2 columns disabled");
    }
}
