using UnityEngine;

public class BoardManager : MonoBehaviour
{
    [Header("Board")]
    public int width = 8;
    public int height = 8;
    public float cellSize = 1f;
    public GameObject squarePrefab;

    private Square[,] squares;

    void Awake()
    {
        GenerateBoard();
        CenterCamera();
    }

    void GenerateBoard()
    {
        if (squarePrefab == null) { Debug.LogError("Assigne squarePrefab !"); return; }
        squares = new Square[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pos = new Vector3(x * cellSize, y * cellSize, 0);
                var go = Instantiate(squarePrefab, pos, Quaternion.identity, transform);
                go.name = $"Square_{x}_{y}";

                var sq = go.GetComponent<Square>();
                if (sq == null) sq = go.AddComponent<Square>();
                sq.Init(x, y);

                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.color = ((x + y) % 2 == 0) ? new Color(0.92f, 0.92f, 0.92f)
                                                  : new Color(0.25f, 0.25f, 0.25f);

                squares[x, y] = sq;
            }
        }
    }

    void CenterCamera()
    {
        var cam = Camera.main;
        if (!cam) return;
        cam.orthographic = true;
        var boardW = (width - 1) * cellSize;
        var boardH = (height - 1) * cellSize;
        cam.transform.position = new Vector3(boardW / 2f, boardH / 2f, -10f);
        cam.orthographicSize = Mathf.Max(width, height) * cellSize * 0.7f;
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;
    public Square GetSquare(int x, int y) => squares[x, y];
}

public class Square : MonoBehaviour
{
    public int x, y;
    public void Init(int x, int y) { this.x = x; this.y = y; }
}
