using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools → Codea → Quitar llamadas vacías y muertas: borra de Setup y Juego las llamadas de
/// UnityEvent que no hacen nada, las que "Comprobar escena de juego" marca con ⚠: las vacías
/// (No Function) y las que apuntan a un objeto que ya no existe. Las desactivadas (Off) no se
/// tocan. Informe en Logs/Codea_LlamadasMuertas.txt.
/// Sin abrir Unity: Unity.exe -batchmode -quit -projectPath . -executeMethod CodeaDeadCalls.CleanBatch
/// </summary>
public static class CodeaDeadCalls
{
    private static readonly string[] Scenes = { "Assets/Scenes/Setup.unity", "Assets/Scenes/Juego.unity" };
    private const string ReportPath = "Logs/Codea_LlamadasMuertas.txt";

    [MenuItem("Tools/Codea/Quitar llamadas vacías y muertas", false, 22)]
    private static void CleanMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string opened = SceneManager.GetActiveScene().path;
        Clean();

        if (!string.IsNullOrEmpty(opened) && File.Exists(opened))
            EditorSceneManager.OpenScene(opened, OpenSceneMode.Single);
    }

    public static void CleanBatch() => Clean();

    private static void Clean()
    {
        StringBuilder log = new StringBuilder("[Codea] Llamadas vacías y muertas quitadas:\n");
        int total = 0;

        foreach (string path in Scenes)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int removed = 0;

            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                removed += CleanBehaviour(behaviour, log);

            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            log.AppendLine($"  {path}: {removed} quitada(s)");
            total += removed;
        }

        log.Insert(0, $"[Codea] {total} llamada(s) quitada(s) en total.\n");
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, log.ToString());
        Debug.Log(log.ToString());
    }

    private static int CleanBehaviour(MonoBehaviour behaviour, StringBuilder log)
    {
        if (behaviour == null) return 0;

        SerializedObject serialized = new SerializedObject(behaviour);
        List<string> callLists = new List<string>();

        SerializedProperty iterator = serialized.GetIterator();
        while (iterator.NextVisible(true))
            if (iterator.isArray && iterator.propertyPath.EndsWith("m_PersistentCalls.m_Calls"))
                callLists.Add(iterator.propertyPath);

        int removed = 0;

        foreach (string listPath in callLists)
        {
            SerializedProperty calls = serialized.FindProperty(listPath);

            for (int i = calls.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty call = calls.GetArrayElementAtIndex(i);
                bool off = call.FindPropertyRelative("m_CallState").enumValueIndex == (int)UnityEventCallState.Off;
                Object target = call.FindPropertyRelative("m_Target").objectReferenceValue;
                string method = call.FindPropertyRelative("m_MethodName").stringValue;

                if (off || (target != null && !string.IsNullOrEmpty(method))) continue;

                log.AppendLine($"    {PathOf(behaviour)} · {behaviour.GetType().Name}.{listPath.Replace(".m_PersistentCalls.m_Calls", "")}" +
                               (target == null ? $" · muerta a '{method}'" : " · vacía"));
                calls.DeleteArrayElementAtIndex(i);
                removed++;
            }
        }

        if (removed > 0) serialized.ApplyModifiedPropertiesWithoutUndo();
        return removed;
    }

    private static string PathOf(Component component)
    {
        Transform t = component.transform;
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
