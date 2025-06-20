using System.Collections.Generic;
using UnityEngine;

public class BlockadePowerUp : MonoBehaviour
{
    private GridManager _grid;
    private List<GameObject> _disabledCells
        = new List<GameObject>();

    public void Initialize(GridManager grid)
    {
        _grid = grid;
    }

    public void Apply(int rowIndex, int count = 2)
    {
        if (_grid == null) return;
        Clear();

        var cols = new List<int>();
        for (int c = 0; c < _grid.cols; c++)
            cols.Add(c);

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

    public void Clear()
    {
        foreach (var cellGO in _disabledCells)
            if (cellGO != null)
                cellGO.SetActive(true);

        _disabledCells.Clear();
    }
}
