using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools → Codea → Montar la intermedia del Escenario 4: el panel con las seis letras de una
/// palabra en el alfabeto de glifos cuadrados.
///
///   · Pliego de glifos: nombra los 26 recortes de la A a la Z, en orden de lectura, y los
///     deja al tamaño de las figuras de siempre
///   · Huecos: los que ya había pasan a ser de solo básica, y se crea uno por letra de la
///     palabra, de solo intermedia, en fila desde el primero de la básica
///   · Fichas: las mismas en las dos dificultades. Scenario4Difficulty les cambia el dibujo:
///     en básica el que tienen, en intermedia letras al azar entre las que siempre están las
///     que hacen falta para completar la palabra
///
/// Repetible: lo ya montado no se duplica y las letras sorteadas se conservan mientras sirvan.
/// "Volver a sortear" las cambia.
/// </summary>
public static class CodeaScenario4
{
    private const string GameScene = "Assets/Scenes/Juego.unity";
    private const string GlyphSheet = "Assets/_Main/Scripts/Scenario 4/Figuras/squareGlyphs.png";
    private const string Report = "Logs/Codea_Escenario4.txt";

    private const string Word = "YACHAY";
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    // Los glifos llenan su recorte entero y las figuras de siempre dejan margen dentro del
    // suyo (256 px a 100 px por unidad). Con esto un glifo ocupa unas tres cuartas partes de
    // lo que ocupa el recorte de una figura, y no toca los bordes de la ficha.
    private const float GlyphPixelsPerUnit = 44f;

    // Con seis huecos donde había cuatro, la separación se cierra a esta parte de la original.
    private const float SpacingFactor = 0.8f;

    [MenuItem("Tools/Codea/Escenario 4/Montar la intermedia (YACHAY)")]
    private static void BuildMenu() => RunOnOpenScene(false);

    [MenuItem("Tools/Codea/Escenario 4/Volver a sortear las letras de las fichas")]
    private static void ReshuffleMenu() => RunOnOpenScene(true);

    /// <summary>Sin abrir Unity: -executeMethod CodeaScenario4.BuildBatch. Guarda la escena.</summary>
    public static void BuildBatch()
    {
        Scene scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
        Build(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void RunOnOpenScene(bool reshuffle)
    {
        Scene scene = SceneManager.GetActiveScene();

        if (scene.path != GameScene)
        {
            EditorUtility.DisplayDialog("Escenario 4", $"Abre {GameScene} y vuelve a pasar la herramienta.", "Vale");
            return;
        }

        Build(reshuffle);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void Build(bool reshuffle)
    {
        StringBuilder log = new StringBuilder($"[Codea] Escenario 4, intermedia con '{Word}':\n");

        Dictionary<char, Sprite> glyphs = PrepareGlyphs(log);
        Scenario4Controller controller = Object.FindObjectsByType<Scenario4Controller>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                               .FirstOrDefault(c => c.ChallengeId == TelemetryManager.Scenario4Id);

        if (glyphs == null || controller == null)
        {
            if (controller == null) log.AppendLine("  ⚠ No hay Scenario4Controller con el reto 'escenario4' en la escena");
        }
        else
        {
            BuildSockets(controller, glyphs, log);
            BuildChips(controller, glyphs, reshuffle, log);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Report));
        File.WriteAllText(Report, log.ToString());
        Debug.Log(log.ToString(), controller);
    }

    // --- Pliego de glifos ---

    /// <summary>Deja el pliego nombrado de la A a la Z y devuelve sus recortes por letra.</summary>
    private static Dictionary<char, Sprite> PrepareGlyphs(StringBuilder log)
    {
        TextureImporter importer = AssetImporter.GetAtPath(GlyphSheet) as TextureImporter;

        if (importer == null)
        {
            log.AppendLine($"  ⚠ No encuentro {GlyphSheet}");
            return null;
        }

        SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
        factory.Init();

        ISpriteEditorDataProvider data = factory.GetSpriteEditorDataProviderFromObject(importer);
        data.InitSpriteEditorDataProvider();

        // Orden de lectura: de arriba abajo (en la textura, 'y' crece hacia arriba) y de
        // izquierda a derecha.
        SpriteRect[] rects = data.GetSpriteRects()
                                 .OrderByDescending(r => Mathf.RoundToInt(r.rect.y))
                                 .ThenBy(r => Mathf.RoundToInt(r.rect.x))
                                 .ToArray();

        if (rects.Length != Alphabet.Length)
        {
            log.AppendLine($"  ⚠ El pliego tiene {rects.Length} recortes y hacen falta {Alphabet.Length}: " +
                           "recórtalo en el Sprite Editor (rejilla de 5 × 6) y vuelve a pasar la herramienta");
            return null;
        }

        bool renamed = false;

        for (int i = 0; i < rects.Length; i++)
        {
            string letter = Alphabet[i].ToString();
            if (rects[i].name == letter) continue;

            rects[i].name = letter;
            renamed = true;
        }

        bool resized = !Mathf.Approximately(importer.spritePixelsPerUnit, GlyphPixelsPerUnit) ||
                       importer.filterMode != FilterMode.Point ||
                       importer.textureCompression != TextureImporterCompression.Uncompressed;

        if (renamed)
        {
            data.SetSpriteRects(rects);
            data.GetDataProvider<ISpriteNameFileIdDataProvider>()
                .SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            data.Apply();
        }

        if (renamed || resized)
        {
            // Trazos rectos de pocos píxeles: sin filtro ni compresión, o salen borrosos.
            importer.spritePixelsPerUnit = GlyphPixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            log.AppendLine($"  pliego de glifos: {rects.Length} recortes nombrados de la A a la Z, en orden de lectura");
        }
        else
        {
            log.AppendLine("  pliego de glifos: ya estaba nombrado de la A a la Z");
        }

        Dictionary<char, Sprite> glyphs = AssetDatabase.LoadAllAssetsAtPath(GlyphSheet).OfType<Sprite>()
                                                       .Where(s => s.name.Length == 1)
                                                       .ToDictionary(s => s.name[0]);

        if (Alphabet.Any(letter => !glyphs.ContainsKey(letter)))
        {
            log.AppendLine("  ⚠ Tras nombrar el pliego no aparecen las 26 letras: revisa el recorte");
            return null;
        }

        return glyphs;
    }

    // --- Huecos ---

    private static void BuildSockets(Scenario4Controller controller, Dictionary<char, Sprite> glyphs, StringBuilder log)
    {
        List<ShapeSocket> all = controller.GetComponentsInChildren<ShapeSocket>(true).ToList();
        List<ShapeSocket> intermediate = all.Where(s => OnlyIn(s) == Difficulty.Intermedia).ToList();

        // De izquierda a derecha según los ve el jugador, que mira el panel de frente.
        List<ShapeSocket> basic = all.Except(intermediate).OrderBy(s => s.transform.position.x).ToList();

        if (basic.Count < 2)
        {
            log.AppendLine($"  ⚠ El panel tiene {basic.Count} hueco(s) de básica: hacen falta al menos dos para alinear los nuevos");
            return;
        }

        foreach (ShapeSocket socket in basic)
            if (OnlyIn(socket) == null) MarkOnly(socket.gameObject, Difficulty.Basica);

        if (intermediate.Count == 0)
        {
            Transform first = basic[0].transform;
            Vector3 step = (basic[basic.Count - 1].transform.localPosition - first.localPosition) / (basic.Count - 1) * SpacingFactor;

            for (int i = 0; i < Word.Length; i++)
            {
                GameObject copy = Duplicate(first.gameObject);
                copy.name = $"Hueco {i + 1} ({Word[i]})";
                copy.transform.localPosition = first.localPosition + step * i;
                copy.transform.SetSiblingIndex(first.GetSiblingIndex() + basic.Count + i);

                MarkOnly(copy, Difficulty.Intermedia);
                intermediate.Add(copy.GetComponent<ShapeSocket>());
            }

            log.AppendLine($"  huecos: {basic.Count} de solo básica y {Word.Length} nuevos de solo intermedia, " +
                           $"separados {step.magnitude:0.###} (los de básica, {step.magnitude / SpacingFactor:0.###})");
        }
        else
        {
            intermediate = intermediate.OrderBy(s => s.transform.position.x).ToList();
            log.AppendLine($"  huecos: ya había {intermediate.Count} de solo intermedia; solo se les repasa la letra");
        }

        if (intermediate.Count != Word.Length)
            log.AppendLine($"  ⚠ Hay {intermediate.Count} huecos de intermedia y la palabra tiene {Word.Length} letras");

        for (int i = 0; i < intermediate.Count && i < Word.Length; i++)
        {
            SerializedObject so = new SerializedObject(intermediate[i]);
            so.FindProperty("expectedShape").objectReferenceValue = glyphs[Word[i]];

            // Con la posición por delante: la palabra repite letras, y dos huecos con el mismo
            // id serían indistinguibles en la telemetría.
            so.FindProperty("socketId").stringValue = $"{i + 1}_{Word[i]}";
            so.ApplyModifiedProperties();

            intermediate[i].ApplyShapeToChild();
            PrefabUtility.RecordPrefabInstancePropertyModifications(intermediate[i]);
        }

        // El controlador con todos: descarta él solo los de la otra dificultad al arrancar.
        SerializedObject controllerSo = new SerializedObject(controller);
        SerializedProperty list = controllerSo.FindProperty("sockets");
        List<ShapeSocket> wanted = basic.Concat(intermediate).ToList();

        list.arraySize = wanted.Count;
        for (int i = 0; i < wanted.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = wanted[i];
        controllerSo.ApplyModifiedProperties();

        log.AppendLine("  intermedia, de izquierda a derecha: " +
                       string.Join(" · ", intermediate.Select(s => new SerializedObject(s).FindProperty("socketId").stringValue)));
    }

    // --- Fichas ---

    private static void BuildChips(Scenario4Controller controller, Dictionary<char, Sprite> glyphs, bool reshuffle, StringBuilder log)
    {
        BlockResetter resetter = new SerializedObject(controller).FindProperty("chipResetter").objectReferenceValue as BlockResetter;

        if (resetter == null)
        {
            log.AppendLine("  ⚠ El Scenario4Controller no tiene Chip Resetter: no sé cuáles son las fichas de la mesa");
            return;
        }

        if (!controller.TryGetComponent(out Scenario4Difficulty variant))
        {
            variant = Undo.AddComponent<Scenario4Difficulty>(controller.gameObject);
            log.AppendLine($"  añadido Scenario4Difficulty en '{controller.name}'");
        }

        // Las de siempre, en su orden de la jerarquía. Si la lista ya existe se respeta.
        List<ShapeChip> chips = variant.Chips.Count > 0 && variant.Chips.All(c => c != null)
            ? variant.Chips.ToList()
            : resetter.GetComponentsInChildren<ShapeChip>(true).ToList();

        if (chips.Count < Word.Length)
        {
            log.AppendLine($"  ⚠ En la mesa hay {chips.Count} fichas y la palabra necesita {Word.Length}");
            return;
        }

        // Básica: el dibujo que tienen hoy. Solo la primera vez: después de montar, "hoy"
        // podría ser el de intermedia si alguien las tocó a mano.
        List<Sprite> basic = variant.Basica.chipShapes.Count == chips.Count
            ? variant.Basica.chipShapes
            : chips.Select(c => c.Shape).ToList();

        List<Sprite> intermediate = variant.Intermedia.chipShapes;
        bool keep = !reshuffle && intermediate.Count == chips.Count && Spells(intermediate);

        if (!keep) intermediate = DrawLetters(chips.Count, glyphs);

        Undo.RecordObject(variant, "Escenario 4");
        variant.SetChips(chips);
        variant.SetSettings(new Scenario4Difficulty.Settings { chipShapes = basic },
                            new Scenario4Difficulty.Settings { chipShapes = intermediate });
        EditorUtility.SetDirty(variant);

        log.AppendLine($"  fichas: {chips.Count}, las mismas en las dos dificultades");
        log.AppendLine($"  letras en intermedia ({(keep ? "las que ya estaban" : "recién sorteadas")}): " +
                       string.Join(" ", intermediate.Select(s => s.name)));
    }

    /// <summary>Si con esas fichas se puede completar la palabra, contando las letras repetidas.</summary>
    private static bool Spells(List<Sprite> shapes) =>
        shapes.All(s => s != null) &&
        Word.GroupBy(letter => letter).All(g => shapes.Count(s => s.name == g.Key.ToString()) >= g.Count());

    /// <summary>
    /// Las letras de la palabra, una ficha por cada vez que aparece, y el resto al azar entre
    /// las demás del alfabeto, sin repetir. Todo barajado: las que sirven no quedan juntas.
    /// </summary>
    private static List<Sprite> DrawLetters(int count, Dictionary<char, Sprite> glyphs)
    {
        List<char> letters = Word.ToList();
        List<char> others = Alphabet.Where(letter => !Word.Contains(letter)).ToList();

        Shuffle(others);

        // Si algún día hubiera más fichas que letras libres, se repiten desde el principio.
        for (int i = 0; letters.Count < count; i++)
            letters.Add(others[i % others.Count]);

        Shuffle(letters);

        return letters.Select(letter => glyphs[letter]).ToList();
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // --- Utilidades ---

    private static Difficulty? OnlyIn(Component component) =>
        component.TryGetComponent(out DifficultyOnly only) ? only.Difficulty : (Difficulty?)null;

    private static void MarkOnly(GameObject target, Difficulty difficulty)
    {
        if (!target.TryGetComponent(out DifficultyOnly only)) only = Undo.AddComponent<DifficultyOnly>(target);

        Undo.RecordObject(only, "Dificultad");
        only.Difficulty = difficulty;
        EditorUtility.SetDirty(only);
    }

    /// <summary>
    /// Copia de un objeto de la escena junto a él, conservando el enlace a su prefab y los
    /// ajustes de la instancia.
    /// </summary>
    private static GameObject Duplicate(GameObject source)
    {
        GameObject copy;

        if (PrefabUtility.IsOutermostPrefabInstanceRoot(source))
        {
            GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(source);
            copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, source.transform.parent);
            PrefabUtility.SetPropertyModifications(copy, PrefabUtility.GetPropertyModifications(source));
        }
        else
        {
            copy = Object.Instantiate(source, source.transform.parent);
        }

        Undo.RegisterCreatedObjectUndo(copy, "Duplicar");
        return copy;
    }
}
