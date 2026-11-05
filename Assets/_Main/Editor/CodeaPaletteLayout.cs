using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools → Codea → Esc. 1: separar la paleta intermedia. Los 13 bloques estaban sueltos y algunos
/// se superponían (sesiones del 22/10/2026). Quedan en dos filas, 7 y 6, alineados con los huecos
/// y dentro de la zona que ya ocupaban. Repetible; la paleta básica no se toca.
/// </summary>
public static class CodeaPaletteLayout
{
    private const string GameScene = "Assets/Scenes/Juego.unity";
    private const int FirstRowCount = 7;

    private static string Report => Path.Combine("Logs", "Codea_PaletaIntermedia.txt");

    [MenuItem("Tools/Codea/Esc. 1: separar la paleta intermedia")]
    private static void ApplyMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Apply();
    }

    /// <summary>Sin abrir Unity: -executeMethod CodeaPaletteLayout.ApplyBatch</summary>
    public static void ApplyBatch() => Apply();

    private static void Apply()
    {
        StringBuilder log = new StringBuilder("[Codea] Paleta intermedia del Esc. 1:\n");
        var scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);

        Scenario1Controller room = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Scenario1Controller>(true))
            .FirstOrDefault();
        SocketRow row = room != null ? room.GetComponentInChildren<SocketRow>(true) : null;

        List<GridBlock> blocks = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<GridBlock>(true))
            .Where(b => b.GetComponentInParent<TutorialController>(true) == null &&
                        b.TryGetComponent(out DifficultyOnly only) && only.Difficulty == Difficulty.Intermedia)
            .ToList();

        if (row == null || blocks.Count < 2)
        {
            log.AppendLine($"  ✗ Sin fila ({row != null}) o sin bloques ({blocks.Count})");
            Finish(log);
            return;
        }

        SerializedObject serializedRow = new SerializedObject(row);
        Transform start = serializedRow.FindProperty("start").objectReferenceValue as Transform;
        Transform end = serializedRow.FindProperty("end").objectReferenceValue as Transform;
        Transform orientation = serializedRow.FindProperty("socketOrientation").objectReferenceValue as Transform;

        if (start == null || end == null)
        {
            log.AppendLine("  ✗ La fila no tiene start y end");
            Finish(log);
            return;
        }

        // Los ejes de la mesa: a lo largo de la fila, y perpendicular a ella sobre el tablero. Un
        // bloque encajado toma la rotación del hueco, así que esa es la que queda alineada.
        Quaternion rotation = orientation != null ? orientation.rotation : row.transform.rotation;
        Vector3 along = (end.position - start.position).normalized;
        Vector3 normal = rotation * Vector3.forward;
        Vector3 across = Vector3.Cross(normal, along).normalized;

        float U(GridBlock b) => Vector3.Dot(b.transform.position, along);
        float V(GridBlock b) => Vector3.Dot(b.transform.position, across);

        float minU = blocks.Min(U), maxU = blocks.Max(U);
        float minV = blocks.Min(V), maxV = blocks.Max(V);
        float pitch = (maxU - minU) / (FirstRowCount - 1);

        // Se conserva el orden en que estaban: primero la fila más próxima a un lado del tablero.
        List<GridBlock> ordered = blocks.OrderBy(V).ToList();
        List<GridBlock> first = ordered.Take(FirstRowCount).OrderBy(U).ToList();
        List<GridBlock> second = ordered.Skip(FirstRowCount).OrderBy(U).ToList();

        Place(first, minU, pitch, minV, along, across, rotation);

        // Al tresbolillo: 6 bloques en los espacios entre los 7 de la otra fila.
        Place(second, minU + pitch * (FirstRowCount - second.Count) / 2f, pitch, maxV, along, across, rotation);

        float widest = blocks.Max(b => WidthAlong(b, along));
        float gap = maxV - minV;
        log.AppendLine($"  ✓ {first.Count} + {second.Count} bloques. Separación {pitch:0.000} m, bloque más " +
                       $"ancho {widest:0.000} m, entre filas {gap:0.000} m");

        if (widest >= pitch)
            log.AppendLine("  ⚠ Los bloques de una misma fila se tocan: ampliar la zona de la paleta en el editor");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Finish(log);
    }

    private static void Place(List<GridBlock> blocks, float startU, float pitch, float v,
                              Vector3 along, Vector3 across, Quaternion rotation)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            Transform t = blocks[i].transform;
            Undo.RecordObject(t, "Separar paleta");

            Vector3 p = t.position;
            p += along * (startU + pitch * i - Vector3.Dot(p, along));
            p += across * (v - Vector3.Dot(p, across));

            t.SetPositionAndRotation(p, rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }
    }

    private static float WidthAlong(GridBlock block, Vector3 axis)
    {
        float min = float.MaxValue, max = float.MinValue;

        foreach (MeshFilter filter in block.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;

            Bounds b = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                float d = Vector3.Dot(filter.transform.TransformPoint(corner), axis);
                min = Mathf.Min(min, d);
                max = Mathf.Max(max, d);
            }
        }

        return max > min ? max - min : 0f;
    }

    private static void Finish(StringBuilder log)
    {
        Debug.Log(log.ToString());
        File.WriteAllText(Report, log.ToString());
    }
}
