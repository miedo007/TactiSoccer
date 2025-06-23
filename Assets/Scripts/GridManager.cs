using UnityEngine;

// Ensure grid builds before GameManager.Start()
[DefaultExecutionOrder(-100)]
public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [Tooltip("Prefab for each cell; must have a Cell component")]
    public GameObject cellPrefab;

    [Tooltip("Number of rows (vertical)")]
    public int rows = 10;
    [Tooltip("Number of columns (horizontal)")]
    public int cols = 5;

    [Tooltip("World-space diameter of each cell (center-to-center before spacing)")]
    public float cellSize = 1f;
    [Tooltip("Extra horizontal gap between columns")]
    public float columnSpacing = 0.1f;
    [Tooltip("Extra vertical gap between rows")]
    public float rowSpacing    = 0.1f;

    [Header("Grid Offset")]
    [Tooltip("Additional offset in local space to shift the entire grid")]
    public Vector2 gridOriginOffset = Vector2.zero;

    [HideInInspector]
    public GameObject[,] cells;

    void Awake()
    {
        // Prepare storage
        cells = new GameObject[rows, cols];

        // Compute step between centers
        float stepX = cellSize + columnSpacing;
        float stepY = cellSize + rowSpacing;

        // Total spans
        float gridWidth  = (cols - 1) * stepX;
        float gridHeight = (rows - 1) * stepY;

        // Center grid around this transform, then apply custom offset
        Vector2 originOffset = new Vector2(-gridWidth * 0.5f, -gridHeight * 0.5f) + gridOriginOffset;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                // Local position relative to this GameObject
                Vector3 localPos = new Vector3(c * stepX, r * stepY, 0f) + (Vector3)originOffset;

                // Instantiate as child and set local position
                var cellGO = Instantiate(cellPrefab, transform);
                cellGO.name = $"Cell_{r}_{c}";
                cellGO.transform.localPosition = localPos;

                cells[r, c] = cellGO;
            }
        }
    }

    /// <summary>
    /// Returns the world position of the center of cell (r, c).
    /// </summary>
    public Vector3 GetCellPosition(int r, int c)
    {
        if (cells == null) return transform.position;
        var go = cells[r, c];
        return go != null ? go.transform.position : transform.position;
    }

    /// <summary>
    /// Returns the Cell components in row r.
    /// </summary>
    public Cell[] Row(int r)
    {
        var rowCells = new Cell[cols];
        for (int c = 0; c < cols; c++)
            rowCells[c] = cells[r, c].GetComponent<Cell>();
        return rowCells;
    }

    /// <summary>
    /// Returns every Cell in row-major order.
    /// </summary>
    public Cell[] AllCells
    {
        get
        {
            var all = new Cell[rows * cols];
            int i = 0;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    all[i++] = cells[r, c].GetComponent<Cell>();
            return all;
        }
    }

    /// <summary>
    /// Finds the (row, col) of the cell whose center is close to worldPos.
    /// Returns (-1, -1) if none found.
    /// </summary>
    public Vector2Int GetCellIndicesFromPosition(Vector3 worldPos)
    {
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (Vector3.Distance(cells[r, c].transform.position, worldPos) < 0.01f)
                {
                    return new Vector2Int(r, c);
                }
            }
        }
        return new Vector2Int(-1, -1);
    }
}
