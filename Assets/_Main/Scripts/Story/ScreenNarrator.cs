using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Narración escrita en pantalla, pensada para ir en paralelo con Narrator: mientras la voz
/// suena, el texto se escribe en el HUD (RF-07 pide narración por voz + interfaz visual para
/// releer sin repetir el audio).
///
/// Como IStepAction, bloquea el paso del Director hasta que termina de escribirse la línea,
/// igual que Narrator lo bloquea hasta que acaba el clip.
///
/// El contenido viene de un CSV con cabecera: ID_Texto, Texto_Narrativa.
/// </summary>
public class ScreenNarrator : MonoBehaviour, IStepAction
{
    [Serializable]
    public class NarrativeLine
    {
        public int id;
        [TextArea] public string text;
    }

    [Header("Contenido")]
    [Tooltip("CSV con cabecera: ID_Texto, Texto_Narrativa. Las columnas de más se ignoran.")]
    [SerializeField] private TextAsset csv;

    [Header("Pantalla")]
    [SerializeField] private Text display;

    [Tooltip("Caracteres por segundo. 25-35 es cómodo de leer en VR.")]
    [SerializeField] private float charactersPerSecond = 28f;

    [Tooltip("Escribe el resto en transparente en vez de recortar la cadena. Evita que el " +
             "texto salte de línea mientras se escribe, que en VR marea bastante.")]
    [SerializeField] private bool stableLayout = true;

    [Tooltip("Segundos que la línea permanece visible tras terminar de escribirse, antes de " +
             "dar el paso por cumplido. Da margen para terminar de leer.")]
    [SerializeField] private float holdAfterTyping = 0.5f;

    [Header("Sonido")]
    [Tooltip("Opcional. Clic por carácter, tipo máquina de escribir.")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip typeSound;

    [Tooltip("Un clic cada N caracteres, para que no sature.")]
    [SerializeField] private int soundEveryNChars = 3;

    [Header("Eventos")]
    public UnityEvent OnLineStarted;
    public UnityEvent OnLineFinished;

    private readonly List<NarrativeLine> lines = new List<NarrativeLine>();
    private int index;
    private bool skipRequested;

    public int LineCount => lines.Count;
    public bool IsTyping { get; private set; }

    private void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        LoadCsv();
    }

    private void Start()
    {
        if (display != null) display.text = string.Empty;
    }

    // --- Carga ---

    private void LoadCsv()
    {
        lines.Clear();

        if (csv == null)
        {
            Debug.LogError($"[ScreenNarrator] '{name}' no tiene CSV asignado.", this);
            return;
        }

        List<List<string>> rows = ParseCsv(csv.text);

        // Se salta la cabecera.
        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            if (row.Count == 0) continue;

            // Filas vacías al final del archivo, habituales al exportar desde Excel.
            if (row.Count == 1 && string.IsNullOrWhiteSpace(row[0])) continue;

            int.TryParse(Field(row, 0).Trim(), out int id);

            lines.Add(new NarrativeLine
            {
                id = id,
                text = Field(row, 1)
            });
        }

        Debug.Log($"[ScreenNarrator] {lines.Count} líneas cargadas de '{csv.name}'.", this);
    }

    private static string Field(List<string> row, int i) => i < row.Count ? row[i] : string.Empty;

    /// <summary>
    /// Parser de CSV que respeta campos entrecomillados: admite comas dentro del texto,
    /// comillas escapadas ("") y saltos de línea dentro de un campo. Un Split(',') pelado
    /// partiría por la mitad frases como "Rápido y urgente, como una alarma."
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

    // --- Reproducción ---

    /// <summary>
    /// Escribe la línea siguiente y bloquea hasta terminar. Mismo criterio que Narrator:
    /// avanza sola por la lista, así que si va emparejada con la voz, ambas deben aparecer
    /// en los mismos pasos del Director para no desincronizarse.
    /// </summary>
    public IEnumerator Execute()
    {
        if (lines.Count == 0)
        {
            Debug.LogWarning($"[ScreenNarrator] '{name}' no tiene líneas cargadas.", this);
            yield break;
        }

        if (index >= lines.Count)
        {
            Debug.LogWarning($"[ScreenNarrator] No quedan líneas por mostrar ({lines.Count}).", this);
            yield break;
        }

        yield return TypeLine(lines[index]);
        index++;
    }

    /// <summary>Muestra una línea concreta por su ID_Texto del CSV.</summary>
    public void ShowLineById(int id)
    {
        int found = lines.FindIndex(l => l.id == id);

        if (found == -1)
        {
            Debug.LogWarning($"[ScreenNarrator] No existe la línea con ID {id}.", this);
            return;
        }

        index = found;
        StartCoroutine(Execute());
    }

    /// <summary>Coloca el cursor en una posición de la lista sin mostrar nada.</summary>
    public void SetLineIndex(int value) => index = Mathf.Clamp(value, 0, Mathf.Max(0, lines.Count));

    /// <summary>Termina de escribir la línea de golpe. Para un botón de "saltar".</summary>
    [ContextMenu("Completar de golpe")]
    public void CompleteInstantly() => skipRequested = true;

    [ContextMenu("Limpiar pantalla")]
    public void Clear()
    {
        if (display != null) display.text = string.Empty;
    }

    private IEnumerator TypeLine(NarrativeLine line)
    {
        IsTyping = true;
        skipRequested = false;

        OnLineStarted?.Invoke();

        string full = line.text ?? string.Empty;
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

        OnLineFinished?.Invoke();
    }

    private void Render(string full, int revealed)
    {
        if (display == null) return;

        if (!stableLayout)
        {
            display.text = full.Substring(0, revealed);
            return;
        }

        // El resto se pinta transparente: ocupa el mismo sitio, así que el bloque de texto
        // no cambia de tamaño ni reajusta saltos de línea mientras se escribe.
        display.supportRichText = true;
        display.text = full.Substring(0, revealed) + "<color=#00000000>" + full.Substring(revealed) + "</color>";
    }

    private void PlayTick()
    {
        if (audioSource != null)
            audioSource.PlayOneShot(typeSound);
    }
}
