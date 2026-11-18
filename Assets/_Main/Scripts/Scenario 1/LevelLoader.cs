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

    [Tooltip("Altura de los obstáculos (muros y peligros) respecto a la de sus prefabs: 1 = como " +
             "están, 0,5 = la mitad. Solo se aplasta lo que hay sobre el suelo, que no se mueve. " +
             "A su altura original tapaban las casillas de detrás y desde la mesa no se veía el " +
             "camino (16/11/2026).")]
    [Range(0.2f, 1f)] public float obstacleHeight = 0.45f;

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

                if (tilePrefab == wallPrefab || tilePrefab == dangerPrefab)
                    LowerObstacle(tileObject.transform);

                if (tileObject.TryGetComponent(out Exit exit))
                    exit.levelManager = levelManager;
            }
        }

        ReferenceLevelData(data.name, grid, objectGrid, spawnPos);
    }

    // Cara de arriba del suelo de una casilla, en su espacio local (Path.prefab: una losa de
    // 0,05 centrada en y = -0,5).
    private const float FloorTop = -0.475f;

    /// <summary>
    /// Aplasta en vertical lo que hay sobre el suelo de una casilla de obstáculo, hacia el suelo.
    /// Todo va dentro de un contenedor nuevo, que es el que se escala: así da igual cómo esté
    /// girada cada caja, y el suelo de la casilla (el hijo "Path" del peligro) queda fuera.
    /// </summary>
    private void LowerObstacle(Transform tile)
    {
        if (obstacleHeight >= 0.999f) return;

        Transform holder = new GameObject("Obstáculo").transform;
        holder.SetParent(tile, false);
        holder.localPosition = Vector3.up * FloorTop;

        for (int i = tile.childCount - 1; i >= 0; i--)
        {
            Transform child = tile.GetChild(i);

            if (child == holder || child.name.StartsWith("Path")) continue;

            child.SetParent(holder, true);
        }

        holder.localScale = new Vector3(1f, obstacleHeight, 1f);
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