#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class LevelEditorWindow : EditorWindow
{
    private LevelData levelData;
    private int selectedTile = 1;
    private Vector2 scroll;
    private int newWidth = 7;
    private int newHeight = 7;

    [MenuItem("Tools/Level Editor")]
    static void Open()
    {
        var window = GetWindow<LevelEditorWindow>("Level Editor");
        window.InitLevel();
    }

    void InitLevel()
    {
        levelData = new LevelData();
        levelData.width = newWidth;
        levelData.height = newHeight;
        levelData.tiles = new int[levelData.width * levelData.height];
    }

    void ClearGrid()
    {
        int height = levelData.height;
        int width = levelData.width;

        levelData = new LevelData();
        levelData.height = height;        
        levelData.width = width;
        levelData.tiles = new int[levelData.width * levelData.height];
    }

    void OnGUI()
    {
        if (levelData == null || levelData.tiles == null) { InitLevel(); return; }

        // ── Nombre ──────────────────────────────────────────────
        levelData.name = EditorGUILayout.TextField("Name", levelData.name);

        // ── Tamaño ──────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        newWidth = EditorGUILayout.IntField("Width", newWidth);
        newHeight = EditorGUILayout.IntField("Height", newHeight);
        if (GUILayout.Button("Aplicar", GUILayout.Width(60)))
            InitLevel();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // ── Selector de tile ────────────────────────────────────
        GUI.backgroundColor = TileColor((TileType)selectedTile);
        GUILayout.Label("Tile: " + (TileType)selectedTile);
        GUI.backgroundColor = Color.white;
        selectedTile = EditorGUILayout.IntSlider(selectedTile, 0, 6);

        EditorGUILayout.Space();

        // ── Grid ────────────────────────────────────────────────
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int y = 0; y < levelData.height; y++)
        {
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < levelData.width; x++)
            {
                int idx = y * levelData.width + x;
                int current = levelData.tiles[idx];
                GUI.backgroundColor = TileColor((TileType)current);
                if (GUILayout.Button(current.ToString(), GUILayout.Width(28), GUILayout.Height(28)))
                    levelData.tiles[idx] = selectedTile;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("Limpiar", GUILayout.Width(60)))
            ClearGrid();
        EditorGUILayout.EndScrollView();

        // ── Export ──────────────────────────────────────────────
        if (GUILayout.Button("Export JSON"))
            ExportLevel();
    }

    Color TileColor(TileType t) => t switch
    {
        TileType.Wall => Color.gray,
        TileType.Spawn => new Color(0.2f, 0.6f, 1f),
        TileType.Danger => Color.red,
        TileType.Point => Color.yellow,
        TileType.Exit => Color.green,
        TileType.Void => Color.black,
        _ => Color.white
    };

    void ExportLevel()
    {
        string json = JsonUtility.ToJson(levelData, true);
        string path = EditorUtility.SaveFilePanel("Save Level", "Assets/Levels", levelData.name, "json");
        if (!string.IsNullOrEmpty(path))
            System.IO.File.WriteAllText(path, json);
        AssetDatabase.Refresh();
    }
}
#endif