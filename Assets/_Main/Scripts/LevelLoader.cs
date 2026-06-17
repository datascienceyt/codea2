using UnityEngine;

public enum TileType { Empty = 0, Wall = 1, Spawn = 2, Danger = 3, Point = 4, Exit = 5 }

[System.Serializable]
public class LevelData
{
    public string name;
    public int width;
    public int height;
    public int[] tiles;
}

public class LevelLoader : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject wallPrefab;
    public GameObject dangerPrefab;
    public GameObject pointPrefab;
    public GameObject botPrefab;
    public GameObject exitPrefab;

    [Header("Config")]
    public float tileSize = 1f;
    public TextAsset levelJson;

    void Start() => LoadLevel(levelJson);

    public void LoadLevel(TextAsset json)
    {
        LevelData data = JsonUtility.FromJson<LevelData>(json.text);

        for (int y = 0; y < data.height; y++)
        {
            for (int x = 0; x < data.width; x++)
            {
                TileType type = (TileType)data.tiles[y * data.width + x]; // ✅
                Vector3 pos = new Vector3(x * tileSize, 0, -y * tileSize);

                switch (type)
                {
                    case TileType.Wall:
                        Instantiate(wallPrefab, pos, Quaternion.identity);
                        break;
                    case TileType.Danger:
                        Instantiate(dangerPrefab, pos, Quaternion.identity);
                        break;
                    case TileType.Point:
                        Instantiate(pointPrefab, pos, Quaternion.identity);
                        break;
                    case TileType.Spawn:
                        Instantiate(botPrefab, pos, Quaternion.identity);
                        break;
                    case TileType.Exit:
                        Instantiate(exitPrefab, pos, Quaternion.identity);
                        break;
                }
            }
        }
    }
}