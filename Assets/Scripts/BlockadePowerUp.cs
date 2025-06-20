using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Responsible for applying and clearing blockade effects on the grid.
/// </summary>
public class BlockadePowerUp : MonoBehaviour
{
    private GridManager _grid;
    private List<GameObject> _disabledCells = new List<GameObject>();

    public void Initialize(GridManager grid)
    {
        _grid = grid;
    }

    /// <summary>
    /// Disables 'count' random columns on row 'rowIndex'.
    /// </summary>
    public void Apply(int rowIndex, int count = 2)
    {
        if (_grid == null) return;

        // First clear any old blockade
        Clear();

        var cols = new List<int>();
        for (int c = 0; c < _grid.cols; c++) cols.Add(c);

        for (int i = 0; i < count && cols.Count > 0; i++)
        {
            int idx = Random.Range(0, cols.Count);
            int col = cols[idx];
            cols.RemoveAt(idx);

            var cellGO = _grid.cells[rowIndex, col];
            cellGO.SetActive(false);
            _disabledCells.Add(cellGO);
        }
    }

    /// <summary>
    /// Re-enables any cells previously disabled by this blockade.
    /// </summary>
    public void Clear()
    {
        foreach (var cellGO in _disabledCells)
            if (cellGO != null)
                cellGO.SetActive(true);

        _disabledCells.Clear();
    }
}
