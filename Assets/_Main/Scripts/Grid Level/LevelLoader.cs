using UnityEngine;

// Los números son los que llevan los JSON de nivel: no se renumeran. El 4 y el 7 eran del
// sistema de puntos, que se retiró; un nivel que los traiga deja esas casillas vacías.
public enum TileType { Path = 0, Wall = 1, Spawn = 2, Danger = 3, Exit = 5, Void = 6 }

[System.Serializable]
public class LevelData
{
    public string name;
    public int width;
    public int height;
    public int[] tiles;

    // Hacia dónde mira el robot al empezar (Direction: 0 arriba, 1 derecha, 2 abajo, 3 izquierda).
    // Sin el campo en el JSON vale -1 y se queda la del prefab, que es abajo.
    public int spawnDirection = -1;
}

[RequireComponent(typeof(LevelManager))]
public class LevelLoader : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject pathPrefab;
    public GameObject wallPrefab;
    public GameObject dangerPrefab;
    public GameObject botPrefab;
    public GameObject exitPrefab;

    [Header("Config")]
    public float tileSize = 1f;
    public float tileScale = 1f;
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
        if (json == null)
        {
            Debug.LogError($"[LevelLoader] '{name}' no tiene nivel asignado.", this);
            return;
        }

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
                int tile = data.tiles[y * data.width + x];
                Vector3 pos = new Vector3(x, 0, -y);
                GameObject tilePrefab = null;

                grid[y][x] = tile;

                switch ((TileType)tile)
                {
                    case TileType.Path:
                        tilePrefab = pathPrefab;
                        break;

                    case TileType.Wall:
                        tilePrefab = wallPrefab;
                        break;

                    case TileType.Danger:
                        tilePrefab = dangerPrefab;
                        break;

                    // El robot va ENCIMA de una casilla de camino: en el grid numérico su
                    // casilla queda como camino, para que se pueda volver a pisar.
                    case TileType.Spawn:
                        spawnPos = new Vector2(x, y);

                        bot = Instantiate(botPrefab, transform).GetComponent<Bot>();
                        bot.transform.localPosition = pos;
                        if (data.spawnDirection >= 0)
                        {
                            // Girado ya al aparecer: el RotateTo de Bot.Start lo animaría, y se
                            // vería al robot dar media vuelta en cada reintento.
                            bot.direction = (Direction)(data.spawnDirection % 4);
                            bot.transform.localRotation = Quaternion.Euler(0, (int)bot.direction * 90f, 0);
                        }

                        grid[y][x] = (int)TileType.Path;
                        tilePrefab = pathPrefab;
                        break;

                    case TileType.Exit:
                        tilePrefab = exitPrefab;
                        break;
                }

                if (tilePrefab == null) continue;

                GameObject tileObject = Instantiate(tilePrefab, transform);
                tileObject.transform.localPosition = pos;
                tileObject.transform.localScale = Vector3.one * tileScale;
                objectGrid[y][x] = tileObject;

                if (tileObject.TryGetComponent(out Exit exit))
                    exit.levelManager = levelManager;
            }
        }

        ReferenceLevelData(data.name, grid, objectGrid, spawnPos);
    }

    private void ReferenceLevelData(string name, int[][] grid, GameObject[][] objectGrid, Vector2 spawnPos)
    {
        levelManager.levelName = name;
        levelManager.grid = grid;
        levelManager.objectGrid = objectGrid;

        // Un JSON sin tile Spawn (2) deja bot a null. Antes eso reventaba aquí mismo con un
        // NullReferenceException y el nivel se quedaba a medio construir sin explicación.
        if (bot == null)
        {
            Debug.LogError($"[LevelLoader] El nivel '{name}' no tiene tile Spawn (2): no se creó el bot.", this);
            return;
        }

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
        {
            GameObject child = transform.GetChild(i).gameObject;

            // Destroy es diferido al final del frame, y LoadLevel() se ejecuta justo después.
            // Durante ese frame convivían el bot viejo y el nuevo, y FindAnyObjectByType<Bot>()
            // podía devolver el que estaba a punto de morir. Desactivarlo lo saca de esa
            // búsqueda (que por defecto ignora inactivos) y desparentarlo lo saca del conteo
            // de hijos del loader.
            child.SetActive(false);
            child.transform.SetParent(null);

            Destroy(child);
        }

        bot = null;
        levelManager.grid = null;
        levelManager.objectGrid = null;
    }
}