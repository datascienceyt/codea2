using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Una línea de narración: su voz y su texto.
///
/// El emparejamiento es por ID explícito y no por posición, porque el CSV cubre toda la
/// narrativa del juego con IDs correlativos mientras que las listas de audio son parciales.
/// Emparejar por índice se rompería en cuanto se grabe más audio o se inserte una línea.
/// </summary>
[Serializable]
public class NarrationEntry
{
    [Tooltip("Opcional. Sin clip, la línea solo se escribe en pantalla.")]
    public AudioClip clip;

    [Tooltip("ID_Texto del CSV. 0 = esta línea no tiene texto, solo voz.")]
    public int textId;
}

/// <summary>Un tramo de narración, por ejemplo el de un escenario.</summary>
[Serializable]
public class NarrationList
{
    [Tooltip("Solo para identificarla en el inspector.")]
    public string name;

    public List<NarrationEntry> entries = new List<NarrationEntry>();
}

/// <summary>
/// Narración de la historia: reproduce la voz y escribe el texto en pantalla a la vez.
///
/// Antes esto eran dos componentes (Narrator y ScreenNarrator), cada uno con su propio índice
/// y su propio Execute(). Si alguien olvidaba ponerlos en el mismo Step del Director, voz y
/// texto se desincronizaban en silencio y solo se descubría con el visor puesto. Fusionados,
/// esa desincronización es imposible por construcción.
///
/// Como IStepAction, bloquea el paso del Director hasta que terminan AMBAS mitades.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class Narrator : MonoBehaviour, IStepAction
{
    [Serializable]
    private class NarrativeLine
    {
        public int id;
        public string text;
    }

    [Header("Contenido")]
    [Tooltip("CSV con cabecera: ID_Texto, Texto_Narrativa. Las columnas de más se ignoran.")]
    [SerializeField] private TextAsset csv;

    [SerializeField] private List<NarrationList> lists = new List<NarrationList>();
    [SerializeField] private int listIndex = 0;

    [Header("Voz")]
    [SerializeField] private AudioSource audioSource;

    [Header("Pantalla")]
    [Tooltip("Opcional. Sin Text asignado, la narración es solo de voz.")]
    [SerializeField] private Text display;

    [Tooltip("Caracteres por segundo. 25-35 se lee cómodo en VR.")]
    [SerializeField] private float charactersPerSecond = 28f;

    [Tooltip("Escribe el resto en transparente en vez de recortar la cadena. Evita que el " +
             "texto reajuste sus saltos de línea mientras aparece, cosa que en VR marea.")]
    [SerializeField] private bool stableLayout = true;

    [Tooltip("Segundos que la línea permanece tras escribirse, para dar margen a terminar de leer.")]
    [SerializeField] private float holdAfterTyping = 0.5f;

    [Header("Sonido de escritura")]
    [Tooltip("AudioSource aparte del de la voz. Si el clic sonara por el mismo, la espera de " +
             "la voz se alargaría con cada tecleo.")]
    [SerializeField] private AudioSource typeAudioSource;
    [SerializeField] private AudioClip typeSound;

    [Tooltip("Un clic cada N caracteres, para que no sature.")]
    [SerializeField] private int soundEveryNChars = 3;

    [Header("Eventos")]
    public UnityEvent OnLineStarted;
    public UnityEvent OnLineFinished;

    private readonly List<NarrativeLine> lines = new List<NarrativeLine>();
    private NarrationList currentList;
    private int index;
    private bool skipRequested;

    public bool IsTyping { get; private set; }
    public int LineCount => lines.Count;

    private void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        LoadCsv();
        SelectList(listIndex);
    }

    private void Start()
    {
        if (display != null) display.text = string.Empty;
    }

    // --- Selección de tramo ---

    /// <summary>Cambia de tramo de narración y reinicia el recorrido.</summary>
    public void SetAudioListIndex(int value)
    {
        SelectList(value);
        index = 0;
    }

    private void SelectList(int value)
    {
        if (lists == null || lists.Count == 0)
        {
            currentList = null;
            return;
        }

        if (value < 0 || value >= lists.Count)
        {
            Debug.LogWarning($"[Narrator] Tramo {value} fuera de rango (hay {lists.Count}).", this);
            return;
        }

        listIndex = value;
        currentList = lists[value];
    }

    // --- Reproducción ---

    /// <summary>
    /// Reproduce la línea siguiente del tramo actual y bloquea hasta que terminen voz y texto.
    /// </summary>
    [ContextMenu("Execute")]
    public IEnumerator Execute()
    {
        if (!HasEntries())
        {
            Debug.LogWarning($"[Narrator] '{name}' no tiene entradas en el tramo {listIndex}.", this);
            yield break;
        }

        if (index >= currentList.entries.Count)
        {
            Debug.LogWarning($"[Narrator] No quedan líneas en el tramo {listIndex}.", this);
            yield break;
        }

        NarrationEntry entry = currentList.entries[index];
        index++;

        yield return PlayEntry(entry);
    }

    /// <summary>Reproduce una entrada concreta, sin tocar el recorrido.</summary>
    public IEnumerator PlayEntry(NarrationEntry entry)
    {
        if (entry == null) yield break;

        OnLineStarted?.Invoke();

        // Las dos mitades corren en paralelo y se espera a las DOS. Así el paso dura lo que
        // dure la más larga, y la ausencia de una de ellas simplemente la salta.
        bool voiceDone = false;
        bool textDone = false;

        StartCoroutine(PlayVoice(entry.clip, () => voiceDone = true));
        StartCoroutine(TypeText(FindText(entry.textId), () => textDone = true));

        yield return new WaitUntil(() => voiceDone && textDone);

        OnLineFinished?.Invoke();
    }

    private IEnumerator PlayVoice(AudioClip clip, Action done)
    {
        if (clip == null || audioSource == null) { done(); yield break; }

        audioSource.clip = clip;
        audioSource.Play();

        Debug.Log($"[Narrator] Reproduciendo: {clip.name}", this);

        yield return null;
        yield return new WaitWhile(() => audioSource.isPlaying);

        done();
    }

    private IEnumerator TypeText(string full, Action done)
    {
        if (display == null || string.IsNullOrEmpty(full)) { done(); yield break; }

        IsTyping = true;
        skipRequested = false;

        float delay = charactersPerSecond > 0f ? 1f / charactersPerSecond : 0f;

        for (int i = 1; i <= full.Length; i++)
        {
            if (skipRequested) break;

            Render(full, i);

            if (typeSound != null && i % Mathf.Max(1, soundEveryNChars) == 0)
                PlayTick();

            if (delay > 0f) yield return new WaitForSeconds(delay);
        }

        Render(full, full.Length);
        IsTyping = false;

        if (holdAfterTyping > 0f)
            yield return new WaitForSeconds(holdAfterTyping);

        done();
    }

    private void Render(string full, int revealed)
    {
        if (display == null) return;

        if (!stableLayout)
        {
            display.text = full.Substring(0, revealed);
            return;
        }

        // El resto se pinta transparente: ocupa el mismo sitio, así el bloque de texto no
        // cambia de tamaño ni reajusta saltos de línea mientras se escribe.
        display.supportRichText = true;
        display.text = full.Substring(0, revealed) + "<color=#00000000>" + full.Substring(revealed) + "</color>";
    }

    private void PlayTick()
    {
        if (typeAudioSource != null)
            typeAudioSource.PlayOneShot(typeSound);
    }

    // --- Control manual ---

    /// <summary>Termina de escribir la línea de golpe. Para un botón de "saltar".</summary>
    [ContextMenu("Completar texto de golpe")]
    public void CompleteInstantly() => skipRequested = true;

    /// <summary>Muestra una línea del CSV por su ID_Texto, sin audio.</summary>
    public void ShowLineById(int id)
    {
        string text = FindText(id);

        if (text == null)
        {
            Debug.LogWarning($"[Narrator] No existe la línea con ID {id}.", this);
            return;
        }

        StartCoroutine(TypeText(text, () => { }));
    }

    /// <summary>Reproduce una entrada del tramo actual por posición.</summary>
    public void PlayAudio(int entryIndex)
    {
        if (!HasEntries()) return;

        if (entryIndex < 0 || entryIndex >= currentList.entries.Count)
        {
            Debug.LogWarning($"[Narrator] Entrada {entryIndex} fuera de rango.", this);
            return;
        }

        index = entryIndex;
        StartCoroutine(PlayEntry(currentList.entries[entryIndex]));
    }

    /// <summary>Reproduce la entrada cuyo clip se llame así.</summary>
    public void PlayAudio(string clipName)
    {
        if (!HasEntries()) return;

        int found = currentList.entries.FindIndex(e => e != null && e.clip != null && e.clip.name == clipName);

        if (found == -1)
        {
            Debug.LogWarning($"[Narrator] Audio con nombre '{clipName}' no encontrado.", this);
            return;
        }

        PlayAudio(found);
    }

    /// <summary>
    /// Detiene la voz. NO anula el AudioSource: hacerlo dejaba el componente inservible y
    /// cualquier llamada posterior lanzaba NullReferenceException.
    /// </summary>
    public void StopAudio()
    {
        if (audioSource != null) audioSource.Stop();
    }

    [ContextMenu("Limpiar pantalla")]
    public void Clear()
    {
        if (display != null) display.text = string.Empty;
    }

    private bool HasEntries() =>
        currentList != null && currentList.entries != null && currentList.entries.Count > 0;

    // --- CSV ---

    private string FindText(int id)
    {
        if (id <= 0) return null;

        NarrativeLine line = lines.Find(l => l.id == id);
        return line?.text;
    }

    private void LoadCsv()
    {
        lines.Clear();

        if (csv == null) return;

        List<List<string>> rows = ParseCsv(csv.text);

        // Se salta la cabecera.
        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            if (row.Count == 0) continue;

            // Filas vacías al final del archivo, habituales al exportar desde Excel.
            if (row.Count == 1 && string.IsNullOrWhiteSpace(row[0])) continue;

            if (!int.TryParse(Field(row, 0).Trim(), out int id)) continue;

            lines.Add(new NarrativeLine { id = id, text = Field(row, 1) });
        }

        Debug.Log($"[Narrator] {lines.Count} líneas de texto cargadas de '{csv.name}'.", this);
    }

    private static string Field(List<string> row, int i) => i < row.Count ? row[i] : string.Empty;

    /// <summary>
    /// Parser de CSV que respeta campos entrecomillados: admite comas dentro del texto,
    /// comillas escapadas ("") y saltos de línea dentro de un campo. Un Split(',') pelado
    /// partiría por la mitad frases como "Tus compañeros lograron llegar, y están a salvo".
    /// </summary>
    private static List<List<string>> ParseCsv(string text)
    {
        List<List<string>> rows = new List<List<string>>();
        List<string> row = new List<string>();
        StringBuilder field = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    bool escapedQuote = i + 1 < text.Length && text[i + 1] == '"';

                    if (escapedQuote) { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;

                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;

                case '\r':
                    break;

                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                    break;

                default:
                    field.Append(c);
                    break;
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }
}
