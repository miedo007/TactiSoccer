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

        // Offset to center grid on this transform
        Vector2 originOffset = new Vector2(-gridWidth * 0.5f, -gridHeight * 0.5f);

        // Instantiate each cell
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                Vector2 pos = new Vector2(c * stepX, r * stepY) + originOffset;
                var cellGO = Instantiate(cellPrefab, (Vector3)pos, Quaternion.identity, transform);
                cellGO.name = $"Cell_{r}_{c}";
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
}
