using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Oculus.Interaction;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools → Codea → Escenario 2: botón Probar.
///
/// Pone en los tres prefabs de módulo el botón azul del Escenario 1, rotulado "Probar", debajo
/// de la cuadrícula de opciones y cableado a SystemModule.Try. Desde la reunión del 20/10/2026
/// marcar una opción no comprueba nada: el niño marca, prueba, y si falla se desmarcan todas.
///
/// Repetible: si el módulo ya tiene su Probar, se borra y se vuelve a montar.
/// </summary>
public static class CodeaScenario2
{
    private const string StartButtonPrefab = "Assets/_Main/Prefabs/Buttons/StartButton.prefab";

    // El azul del botón de ejecutar del Escenario 1. Allí es un ajuste de la instancia en la
    // escena, no del prefab: el prefab es gris.
    private const string BlueMaterial = "Assets/_Main/CustomMaterials/Scenario1/Blue.mat";

    // Sonido al reparar un módulo: el de acertar una ficha en el Escenario 4.
    private const string SolvedSound = "Assets/_Main/Sounds/taskCompleted.mp3";

    private static readonly string[] ModulePrefabs =
    {
        "Assets/_Main/Prefabs/Scenario2/CoolingModule.prefab",
        "Assets/_Main/Prefabs/Scenario2/EngineModule.prefab",
        "Assets/_Main/Prefabs/Scenario2/GeneratorsModule.prefab",
    };

    public const string TryButtonName = "Probar";
    private const string LabelName = "Rótulo Probar";
    private const string LabelText = "Probar";

    // El prefab del botón mide 0,11 m de diámetro a su escala 0,5: con 1,4 queda en unos 0,3 m,
    // del orden de los botones de opción (0,42 × 0,85 m).
    private const float ButtonScale = 1.8f;

    // El rótulo copiado de una opción, algo más grande: "Probar" va solo, sin botón que lo enmarque.
    private const float LabelScale = 1.5f;

    // Qué parte de la separación entre filas de opciones baja Probar bajo la última fila.
    private const float RowGapFraction = 0.85f;

    // Separación entre el centro del botón y el del rótulo, a lo ancho del panel.
    private const float LabelOffset = 0.42f;

    // --- Ambientación: que la sala no sean cajas lisas ---

    private const string GameScene = "Assets/Scenes/Juego.unity";
    private const string PropsName = "Ambientación";
    private const string PackMachines = "Assets/Sci-Fi Styled Modular Pack/Prefabs/Machines/";

    /// <summary>Una máquina del pack junto a su módulo, de lo que trata el problema.</summary>
    private struct Prop
    {
        public string Module, Prefab, Name;
        public Vector3 Offset, Euler;
        public float Scale;

        public Prop(string module, string prefab, string name, Vector3 offset, Vector3 euler, float scale)
        {
            Module = module; Prefab = prefab; Name = name; Offset = offset; Euler = euler; Scale = scale;
        }
    }

    // Offset desde el módulo, en metros de mundo: x hacia la pared del fondo, z a lo ancho de la sala.
    private static readonly Prop[] Props =
    {
        new Prop("CoolingModule",    "rotor.prefab",     "Ventilador",  new Vector3(-0.8f, 0.75f, -2.1f), new Vector3(0f, 90f, 0f), 0.6f),
        new Prop("GeneratorsModule", "Battery.prefab",   "Batería A",   new Vector3(-1.2f, 0f, -1.9f),   new Vector3(0f, 0f, 0f),  0.55f),
        new Prop("GeneratorsModule", "Battery.prefab",   "Batería B",   new Vector3(-1.2f, 0f,  1.9f),   new Vector3(0f, 0f, 0f),  0.55f),
        new Prop("EngineModule",     "generator.prefab", "Motor",       new Vector3(-0.9f, 0f,  2.2f),   new Vector3(0f, 0f, 0f),  0.5f),
    };

    [MenuItem("Tools/Codea/Escenario 2: ambientación")]
    private static void DressMenu()
    {
        if (!EditorUtility.DisplayDialog("Ambientación del Escenario 2",
                "Se pone junto a cada módulo una máquina del pack Sci-Fi de su sistema " +
                "(ventilador, baterías, motor) y se guarda la escena Juego.", "Vestir", "Cancelar"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Dress();
    }

    /// <summary>Sin abrir Unity: -executeMethod CodeaScenario2.DressBatch</summary>
    public static void DressBatch() => Dress();

    private static void Dress()
    {
        StringBuilder log = new StringBuilder("[Codea] Ambientación del Escenario 2:\n");

        var scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
        GameObject room = GameObject.Find("Escenario (2)");
        if (room == null)
        {
            log.AppendLine("  ✗ No encuentro 'Escenario (2)' en Juego");
            Finish(log);
            return;
        }

        Transform old = room.transform.Find(PropsName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        Transform holder = new GameObject(PropsName).transform;
        holder.SetParent(room.transform, false);

        foreach (Prop prop in Props)
        {
            Transform module = room.GetComponentsInChildren<SystemModule>(true).Select(m => m.transform)
                                   .FirstOrDefault(t => t.name == prop.Module);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackMachines + prop.Prefab);
            if (module == null || prefab == null)
            {
                log.AppendLine($"  ⚠ {prop.Name}: falta el módulo {prop.Module} o {prop.Prefab}");
                continue;
            }

            Transform structure = module.Find("Structure");
            Vector3 anchor = structure != null ? structure.GetComponent<Renderer>().bounds.center : module.position;
            anchor.y = 0f;

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
            go.name = prop.Name;
            go.transform.SetPositionAndRotation(anchor + prop.Offset, Quaternion.Euler(prop.Euler));
            go.transform.localScale = Vector3.one * prop.Scale;

            // Decorado: que no estorbe a las manos ni al teletransporte.
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;

            log.AppendLine($"  ✓ {prop.Name} ({prop.Prefab}) junto a {prop.Module}");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Finish(log);
    }

    private static string Report => Path.Combine(Path.GetTempPath(), "codea_escenario2.txt");

    [MenuItem("Tools/Codea/Escenario 2: botón Probar")]
    private static void BuildMenu()
    {
        if (!EditorUtility.DisplayDialog("Botón Probar",
                "Se pone el botón Probar en los tres prefabs de módulo del Escenario 2 " +
                "(CoolingModule, EngineModule, GeneratorsModule) y se guardan.\n\n" +
                "Si ya lo tienen, se vuelve a montar.", "Montar", "Cancelar"))
            return;

        Build();
    }

    /// <summary>Sin abrir Unity: -executeMethod CodeaScenario2.BuildBatch</summary>
    public static void BuildBatch() => Build();

    private static void Build()
    {
        StringBuilder log = new StringBuilder("[Codea] Botón Probar del Escenario 2:\n");

        GameObject buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StartButtonPrefab);
        if (buttonPrefab == null)
        {
            log.AppendLine($"  ✗ No encuentro {StartButtonPrefab}");
            Finish(log);
            return;
        }

        foreach (string path in ModulePrefabs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                if (BuildIn(root, buttonPrefab, log))
                    PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Finish(log);
    }

    private static bool BuildIn(GameObject root, GameObject buttonPrefab, StringBuilder log)
    {
        SystemModule module = root.GetComponentInChildren<SystemModule>(true);
        if (module == null)
        {
            log.AppendLine($"  ✗ {root.name}: no tiene SystemModule");
            return false;
        }

        List<ModuleOptionButton> options = root.GetComponentsInChildren<ModuleOptionButton>(true).ToList();
        if (options.Count == 0)
        {
            log.AppendLine($"  ✗ {root.name}: no tiene botones de opción");
            return false;
        }

        Transform old = root.transform.Find(TryButtonName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // Las opciones van en filas: se agrupan por altura y Probar va una fila más abajo,
        // centrado y con la misma inclinación que el panel.
        List<List<ModuleOptionButton>> rows = options
            .GroupBy(b => Mathf.Round(b.transform.position.y * 20f))
            .OrderByDescending(g => g.Key)
            .Select(g => g.ToList())
            .ToList();

        Vector3 lastRow = Center(rows[rows.Count - 1]);
        Vector3 step = rows.Count > 1
            ? lastRow - Center(rows[rows.Count - 2])
            : -options[0].transform.up * 0.5f;

        Transform reference = options[0].transform;
        Vector3 across = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;

        // A lo ancho del panel: la dirección en que se reparten los botones de una fila.
        List<ModuleOptionButton> wide = rows.OrderByDescending(r => r.Count).First();
        if (wide.Count > 1)
        {
            Vector3 spread = wide.Last().transform.position - wide.First().transform.position;
            across = spread.normalized;
        }

        Vector3 groupCenter = lastRow + step * RowGapFraction;

        GameObject button = (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab, root.transform);
        button.name = TryButtonName;
        button.transform.SetPositionAndRotation(groupCenter - across * (LabelOffset * 0.5f), reference.rotation);
        button.transform.localScale = Vector3.one * ButtonScale;

        InteractableUnityEventWrapper wrapper = button.GetComponentInChildren<InteractableUnityEventWrapper>(true);
        if (wrapper == null)
        {
            log.AppendLine($"  ✗ {root.name}: el botón azul no tiene InteractableUnityEventWrapper");
            Object.DestroyImmediate(button);
            return false;
        }

        // La primera llamada del prefab está vacía: se quita y se añade la de Probar. Al final,
        // después del sonido, para que suene aunque el módulo apague el botón al repararse.
        for (int i = wrapper.WhenSelect.GetPersistentEventCount() - 1; i >= 0; i--)
            if (string.IsNullOrEmpty(wrapper.WhenSelect.GetPersistentMethodName(i)))
                UnityEventTools.RemovePersistentListener(wrapper.WhenSelect, i);

        UnityEventTools.AddPersistentListener(wrapper.WhenSelect, module.Try);

        // Al soltar, el prefab llama a Restart de un Countdown de las muestras de Meta que no
        // existe aquí. En el Escenario 1 se recortó igual.
        for (int i = wrapper.WhenUnselect.GetPersistentEventCount() - 1; i >= 0; i--)
            if (wrapper.WhenUnselect.GetPersistentTarget(i) == null)
                UnityEventTools.RemovePersistentListener(wrapper.WhenUnselect, i);

        PaintBlue(button);

        TMP_Text label = AddLabel(button.transform, options, groupCenter + across * (LabelOffset * 0.5f));

        SerializedObject so = new SerializedObject(module);
        so.FindProperty("tryButton").objectReferenceValue = button;
        so.FindProperty("solvedSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SolvedSound);
        so.ApplyModifiedPropertiesWithoutUndo();

        log.AppendLine($"  ✓ {root.name}: Probar en {Local(root, button.transform.position)} " +
                       $"bajo {rows.Count} filas de opciones · cableado a SystemModule.Try" +
                       (label != null ? " · con rótulo" : " · SIN rótulo (las opciones no tienen texto que copiar)"));
        return true;
    }

    /// <summary>
    /// El rótulo es una copia del texto de un botón de opción: misma fuente, tamaño y
    /// orientación que el resto del panel. Cuelga del botón para apagarse con él.
    /// </summary>
    private static TMP_Text AddLabel(Transform button, List<ModuleOptionButton> options, Vector3 position)
    {
        TMP_Text source = options
            .Select(o => new SerializedObject(o).FindProperty("label").objectReferenceValue as TMP_Text)
            .FirstOrDefault(t => t != null);

        if (source == null) return null;

        GameObject copy = Object.Instantiate(source.gameObject, button, true);
        copy.name = LabelName;
        copy.transform.position = position;

        copy.transform.localScale *= LabelScale;

        TMP_Text text = copy.GetComponent<TMP_Text>();
        text.text = LabelText;
        return text;
    }

    /// <summary>
    /// Como el del Escenario 1: material azul en la cara, y apagado el visual de depuración de
    /// Meta, que la tiñe con sus colores y taparía el azul.
    /// </summary>
    private static void PaintBlue(GameObject button)
    {
        Material blue = AssetDatabase.LoadAssetAtPath<Material>(BlueMaterial);

        foreach (InteractableDebugVisual visual in button.GetComponentsInChildren<InteractableDebugVisual>(true))
        {
            visual.enabled = false;

            Renderer face = new SerializedObject(visual).FindProperty("_renderer").objectReferenceValue as Renderer;
            if (face != null && blue != null) face.sharedMaterial = blue;
        }
    }

    private static Vector3 Center(List<ModuleOptionButton> row)
    {
        Vector3 sum = Vector3.zero;
        foreach (ModuleOptionButton b in row) sum += b.transform.position;
        return sum / row.Count;
    }

    private static string Local(GameObject root, Vector3 world) =>
        root.transform.InverseTransformPoint(world).ToString("F2");

    private static void Finish(StringBuilder log)
    {
        File.WriteAllText(Report, log.ToString());
        Debug.Log(log.ToString());
    }
}
