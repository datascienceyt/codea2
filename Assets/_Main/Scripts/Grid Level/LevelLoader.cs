using Unity.VisualScripting;
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
    public bool loadOnStart = true;

    private LevelManager levelManager;
    private Bot bot;

    private void Awake() => levelManager = GetComponent<LevelManager>();

    void Start()
    {
        transform.localScale *= tileSize;
     
        if (loadOnStart)
            LoadLevel(levelJson);
    }

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
                Vector3 pos = new Vector3(x, 0, -y);
                GameObject prefab = null;

                grid[y][x] = data.tiles[y * data.width + x];

                switch (type)
                {
                    case TileType.Path:
                        prefab = pathPrefab;
                        break;
                    case TileType.Wall:
                        prefab = wallPrefab;
                        break;
                    case TileType.Danger:
                        prefab = dangerPrefab;
                        break;
                    case TileType.Point:
                        prefab = Instantiate(pointPrefab, transform);       //Instanciar prefab de punto antes de convertir el grid en camino para que el usuario pueda caminar por ahí
                        prefab.transform.localPosition = pos;
                        prefab.GetComponent<Point>().OnCollected += levelManager.IncreasePoint;

                        grid[y][x] = 0;                                     //Convertir en path en el grid numérico
                        prefab = pathPrefab;
                        break;
                    case TileType.Spawn:
                        grid[y][x] = 0;
                        spawnPos.x = x;
                        spawnPos.y = y;

                        bot = Instantiate(botPrefab, transform).GetComponent<Bot>();
                        bot.transform.localPosition = pos;

                        prefab = pathPrefab;
                        break;
                    case TileType.Exit:
                        prefab = exitPrefab;
                        break;
                }

                if(prefab != null)
                {
                    prefab = Instantiate(prefab, transform);
                    prefab.transform.localPosition = pos;
                    objectGrid[y][x] = prefab;
                }
            }
        }

        ReferenceLevelData(data.name, grid, objectGrid, spawnPos);
        PrintGrid(grid);
    }

    private void ReferenceLevelData(string name, int[][] grid, GameObject[][] objectGrid, Vector2 spawnPos)
    {
        levelManager.levelName = name;
        levelManager.grid = grid;
        levelManager.objectGrid = objectGrid;

        bot.botPos = spawnPos;
        bot.levelManager = levelManager;
    }

    public void ReloadLevel()
    {
        ClearLevel();
        LoadLevel(levelJson);
    }

    void ClearLevel()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        bot = null;
        levelManager.grid = null;
        levelManager.objectGrid = null;
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