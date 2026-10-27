using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools → Codea → Feedback 27/10: cambios de escena pedidos tras las pruebas del 27/10/2026
/// que no se pueden hacer desde código. Repetible.
///
///   · Esc. 3: los botones + y − de las repeticiones, los dos con el mismo material gris
///     (RepetitionButton.mat). Eran rojo (Red.mat) y azul (Blue 1.mat), materiales que usan
///     también otras piezas de la sala, así que no se repintan: se cambia el de los botones.
///   · Esc. 1 básica: un Girar Derecha de la paleta pasa a Girar Izquierda. El nivel en espejo
///     (el robot empieza mirando arriba) pide 2 Girar Izq y 1 Girar Der; la paleta tenía 1 y 2.
///     Se duplica un Girar Izquierda de la básica en el sitio del Girar Derecha y este se borra,
///     para que el bloque nuevo lleve también su dibujo y su rótulo.
///   · Botones de empezar: el Ejecutar del Esc. 3 y el INICIAR del tutorial pasan a "Probar" y
///     al celeste del Probar del Esc. 1 (Blue.mat, sin el visual de depuración de Meta).
///   · Recorrido de los botones de pulsar a 0,4: la superficie que dispara la pulsación se acerca
///     a la cara del botón. Va en los cuatro prefabs con PokeInteractableVisual; se marca
///     renombrando la superficie, para no acortarlo dos veces.
/// </summary>
public static class CodeaFeedback
{
    private const string GameScene = "Assets/Scenes/Juego.unity";
    private const string RepetitionMaterial = "Assets/_Main/CustomMaterials/Scenario3/RepetitionButton.mat";

    private static readonly string[] RepetitionButtons = { "IncreaseButton", "DecreaseButton" };

    // Los materiales que tenían. Solo se sustituyen estos: el resto del botón (marco, base) no cambia.
    private static readonly string[] OldMaterials = { "Red", "Blue 1" };

    private static string Report => Path.Combine(Path.GetTempPath(), "codea_feedback.txt");

    [MenuItem("Tools/Codea/Feedback 27-10")]
    private static void ApplyMenu()
    {
        if (!EditorUtility.DisplayDialog("Feedback 27/10",
                "Botones + y − del Esc. 3 en gris, un Girar Derecha de la paleta básica del Esc. 1 " +
                "pasa a Girar Izquierda, botones de empezar a 'Probar' en celeste y recorrido de " +
                "los botones a 0,4. Se guardan la escena Juego y los prefabs de botón.", "Aplicar", "Cancelar"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Apply();
    }

    /// <summary>Sin abrir Unity: -executeMethod CodeaFeedback.ApplyBatch</summary>
    public static void ApplyBatch() => Apply();

    private static void Apply()
    {
        StringBuilder log = new StringBuilder("[Codea] Feedback 27/10:\n");

        Material gray = AssetDatabase.LoadAssetAtPath<Material>(RepetitionMaterial);
        if (gray == null)
        {
            log.AppendLine($"  ✗ No encuentro {RepetitionMaterial}");
            Finish(log);
            return;
        }

        ShortenButtonTravel(log);
        RenameTutorialStart(log);

        var scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);

        GameObject room = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .Select(t => t.gameObject)
            .FirstOrDefault(g => g.name == "Escenario (3)");

        if (room == null)
        {
            log.AppendLine("  ✗ No encuentro 'Escenario (3)' en Juego");
            Finish(log);
            return;
        }

        foreach (string buttonName in RepetitionButtons)
        {
            Transform button = room.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == buttonName);
            if (button == null)
            {
                log.AppendLine($"  ✗ No encuentro '{buttonName}' en Escenario (3)");
                continue;
            }

            int changed = 0;
            foreach (Renderer r in button.GetComponentsInChildren<Renderer>(true))
            {
                if (r.sharedMaterial == null || !OldMaterials.Contains(r.sharedMaterial.name)) continue;

                Undo.RecordObject(r, "Material gris de repetir");
                r.sharedMaterial = gray;
                changed++;
            }

            log.AppendLine(changed > 0
                ? $"  ✓ {buttonName}: {changed} renderer(s) en gris"
                : $"  · {buttonName}: nada que cambiar (¿ya estaba en gris?)");
        }

        SwapBasicTurn(scene, log);
        PaintScenario3Start(room, log);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Finish(log);
    }

    private static void SwapBasicTurn(UnityEngine.SceneManagement.Scene scene, StringBuilder log)
    {
        List<GridBlock> basic = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<GridBlock>(true))
            .Where(b => b.GetComponentInParent<TutorialController>(true) == null &&
                        b.TryGetComponent(out DifficultyOnly only) && only.Difficulty == Difficulty.Basica)
            .ToList();

        int left = basic.Count(b => b.action == GridActionType.RotateLeft);
        int right = basic.Count(b => b.action == GridActionType.RotateRight);

        if (left >= 2 && right <= 1)
        {
            log.AppendLine($"  · Esc. 1 básica: ya tiene {left} Girar Izq y {right} Girar Der");
            return;
        }

        GridBlock source = basic.FirstOrDefault(b => b.action == GridActionType.RotateLeft);
        GridBlock target = basic.LastOrDefault(b => b.action == GridActionType.RotateRight);
        if (source == null || target == null)
        {
            log.AppendLine($"  ✗ Esc. 1 básica: no encuentro un Girar Izq y un Girar Der ({basic.Count} bloques de básica)");
            return;
        }

        // Duplicar por el portapapeles conserva el enlace con el prefab y los ajustes de la
        // instancia (dibujo, rótulo, DifficultyOnly).
        Selection.activeGameObject = source.gameObject;
        Unsupported.DuplicateGameObjectsUsingPasteboard();
        GameObject copy = Selection.activeGameObject;

        if (copy == null || copy == source.gameObject)
        {
            log.AppendLine("  ✗ Esc. 1 básica: no se pudo duplicar el Girar Izquierda");
            return;
        }

        Transform t = target.transform;
        copy.transform.SetParent(t.parent, false);
        copy.transform.SetSiblingIndex(t.GetSiblingIndex());
        copy.transform.localPosition = t.localPosition;
        copy.transform.localRotation = t.localRotation;
        copy.transform.localScale = t.localScale;
        copy.name = target.name.Replace("Derecha", "Izquierda");

        Undo.RegisterCreatedObjectUndo(copy, "Girar Izquierda en la paleta");
        Undo.DestroyObjectImmediate(target.gameObject);
        Selection.activeGameObject = null;

        log.AppendLine($"  ✓ Esc. 1 básica: '{copy.name}' en el sitio del Girar Derecha (ahora {left + 1} Izq y {right - 1} Der)");
    }

    private const float TravelFactor = 0.4f;
    private const string ShortenedSuffix = " (recorrido 0,4)";

    private static readonly string[] PokeButtonPrefabs =
    {
        "Assets/_Main/Prefabs/Buttons/StartButton.prefab",
        "Assets/_Main/Prefabs/Buttons/TextButton.prefab",
        "Assets/_Main/Prefabs/Buttons/RestartButton.prefab",
        "Assets/_Main/Prefabs/Scenario2/Button.prefab",
    };

    /// <summary>
    /// PokeInteractableVisual lleva la cara del botón entre su sitio de reposo y la superficie
    /// (_buttonBaseTransform), y la pulsación salta al cruzar esa superficie: acercarla a la cara
    /// acorta el recorrido sin mover nada de lo que se ve en reposo.
    /// </summary>
    private static void ShortenButtonTravel(StringBuilder log)
    {
        foreach (string path in PokeButtonPrefabs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;

            try
            {
                foreach (Oculus.Interaction.PokeInteractableVisual visual in
                         root.GetComponentsInChildren<Oculus.Interaction.PokeInteractableVisual>(true))
                {
                    Transform surface = new SerializedObject(visual).FindProperty("_buttonBaseTransform")
                        .objectReferenceValue as Transform;
                    if (surface == null) continue;

                    if (surface.name.EndsWith(ShortenedSuffix))
                    {
                        log.AppendLine($"  · {Path.GetFileName(path)}: el recorrido ya estaba acortado");
                        continue;
                    }

                    Vector3 face = visual.transform.position;
                    float before = Vector3.Distance(face, surface.position);
                    surface.position = face + (surface.position - face) * TravelFactor;
                    surface.name += ShortenedSuffix;
                    changed = true;

                    log.AppendLine($"  ✓ {Path.GetFileName(path)}: recorrido {before * 1000f:0.#} → " +
                                   $"{before * TravelFactor * 1000f:0.#} mm (a escala del prefab)");
                }

                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private const string TutorialPrefab = "Assets/_Main/Prefabs/Tutorial.prefab";
    private const string ProbarText = "Probar";

    /// <summary>El botón INICIAR del tutorial: rótulo "Probar" y celeste.</summary>
    private static void RenameTutorialStart(StringBuilder log)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(TutorialPrefab);

        try
        {
            TMPro.TMP_Text label = root.GetComponentsInChildren<TMPro.TMP_Text>(true)
                .FirstOrDefault(t => t.text.Trim().ToUpperInvariant() == "INICIAR" || t.text.Trim() == ProbarText);

            if (label == null)
            {
                log.AppendLine("  ✗ Tutorial: no encuentro el botón INICIAR");
                return;
            }

            label.text = ProbarText;

            Oculus.Interaction.InteractableUnityEventWrapper wrapper =
                label.GetComponentInParent<Oculus.Interaction.InteractableUnityEventWrapper>(true);
            GameObject instance = PrefabUtility.GetOutermostPrefabInstanceRoot(wrapper != null ? wrapper.gameObject : label.gameObject);
            Transform button = instance != null ? instance.transform : label.transform.parent;
            CodeaScenario2.PaintBlue(button.gameObject);

            PrefabUtility.SaveAsPrefabAsset(root, TutorialPrefab);
            log.AppendLine($"  ✓ Tutorial: '{button.name}' dice Probar y es celeste");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>El Ejecutar del Esc. 3: rótulo "Probar" y celeste, como el del Esc. 1.</summary>
    private static void PaintScenario3Start(GameObject room, StringBuilder log)
    {
        Transform button = room.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "StartButton");
        if (button == null)
        {
            log.AppendLine("  ✗ Esc. 3: no encuentro 'StartButton'");
            return;
        }

        foreach (TMPro.TMP_Text text in button.GetComponentsInChildren<TMPro.TMP_Text>(true))
        {
            Undo.RecordObject(text, "Probar");
            text.text = ProbarText;
        }

        CodeaScenario2.PaintBlue(button.gameObject);
        log.AppendLine("  ✓ Esc. 3: StartButton dice Probar y es celeste");
    }

    private static void Finish(StringBuilder log)
    {
        File.WriteAllText(Report, log.ToString());
        Debug.Log(log.ToString());
    }
}
