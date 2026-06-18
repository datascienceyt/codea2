using UnityEngine;

public enum TileType { Path = 0, Wall = 1, Spawn = 2, Danger = 3, Point = 4, Exit = 5, Void = 6 }

[System.Serializable]
public class LevelData
{
    public string name;
    public int width;
    public int height;
    public int[] tiles;
}

[RequireComponent(typeof(LevelManager))]
public class LevelLoader : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject pathPrefab;
    public GameObject wallPrefab;
    public GameObject dangerPrefab;
    public GameObject pointPrefab;
    public GameObject botPrefab;
    public GameObject exitPrefab;

    [Header("Config")]
    public float tileSize = 1f;
    public TextAsset levelJson;

    private LevelManager levelManager;
    private Bot bot;

    private void Awake() => levelManager = GetComponent<LevelManager>();

    void Start() => LoadLevel(levelJson);

    public void LoadLevel(TextAsset json)
    {
        LevelData data = JsonUtility.FromJson<LevelData>(json.text);
        int[][] grid = new int[data.height][];
        GameObject[][] objectGrid = new GameObject[data.height][];

        Vector2 spawnPos = new Vector2();

        for (int y = 0; y < data.height; y++)
        {
            grid[y] = new int[data.width];
            objectGrid[y] = new GameObject[data.width];

            for (int x = 0; x < data.width; x++)
            {
                TileType type = (TileType)data.tiles[y * data.width + x];
                Vector3 pos = new Vector3(x * tileSize, 0, -y * tileSize);
                GameObject gameObject = null;

                grid[y][x] = data.tiles[y * data.width + x];

                switch (type)
                {
                    case TileType.Path:
                        gameObject = Instantiate(pathPrefab, pos, Quaternion.identity);
                        break;
                    case TileType.Wall:
                        gameObject = Instantiate(wallPrefab, pos, Quaternion.identity);
                        break;
                    case TileType.Danger:
                        gameObject = Instantiate(dangerPrefab, pos, Quaternion.identity);
                        break;
                    case TileType.Point:
                        GameObject aux = Instantiate(pointPrefab, pos, Quaternion.identity);
                        aux.transform.parent = transform;
                        aux.GetComponent<Point>().OnCollected += levelManager.IncreasePoint;
                        gameObject = Instantiate(pathPrefab, pos, Quaternion.identity);
                        grid[y][x] = 0;
                        break;
                    case TileType.Spawn:
                        spawnPos.x = x;
                        spawnPos.y = y;
                        bot = Instantiate(botPrefab, pos, Quaternion.identity).GetComponent<Bot>();
                        bot.transform.parent = transform;
                        gameObject = Instantiate(pathPrefab, pos, Quaternion.identity);
                        grid[y][x] = 0;
                        break;
                    case TileType.Exit:
                        gameObject = Instantiate(exitPrefab, pos, Quaternion.identity);
                        break;
                }

                if(gameObject != null)
                {
                    gameObject.transform.parent = transform;
                    objectGrid[y][x] = gameObject;
                }
            }
        }

        ReferenceLevelData(data.name, grid, objectGrid, spawnPos);
        PrintGrid(grid);
        transform.Translate(-data.width * tileSize / 2, 0, -data.height * tileSize / 2);
        bot.transform.parent = null;
    }

    private void ReferenceLevelData(string name, int[][] grid, GameObject[][] objectGrid, Vector2 spawnPos)
    {
        levelManager.levelName = name;
        levelManager.grid = grid;
        levelManager.objectGrid = objectGrid;

        bot.botPos = spawnPos;
        bot.levelManager = levelManager;
    }

    void PrintGrid(int[][] grid)
    {
        string output = string.Empty;

        for (int i = 0; i < grid.Length; i++)
        {
            output += "\n";
            for (int j = 0; j < grid[i].Length; j++)
            {
                output += grid[i][j];
                output += "\t";
            }
        }

        Debug.Log(output);
    }
}