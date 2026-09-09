using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Entrada del PIN del participante, en la pantalla del supervisor.
///
/// Deliberadamente NO conoce Oculus.Interaction: cada tecla llama a AppendDigit(n) desde el
/// WhenSelect de su PokeInteractable. Así se prueba sin visor desde el menú contextual y el
/// script sigue sirviendo si mañana cambia la primitiva de interacción.
///
/// El PIN identifica al participante y da nombre a su archivo de telemetría
/// ({pin}_{sessionId}.json), así que se exige completo: uno a medias no identifica a nadie.
/// </summary>
public class PinEntry : MonoBehaviour
{
    [Header("Formato")]
    [Tooltip("Cuántas cifras tiene el PIN. El JSON lo escribe con este mismo ancho: 4 -> \"0007\".")]
    [SerializeField] private int digits = 4;

    [Header("Pantalla")]
    [Tooltip("Opcional. Muestra lo tecleado.")]
    [SerializeField] private Text display;

    [Tooltip("Igual, pero en TextMeshPro. Rellena solo el que uses.")]
    [SerializeField] private TMP_Text displayTmp;

    [Tooltip("Con qué se dibujan las cifras que faltan por teclear.")]
    [SerializeField] private string emptyChar = "_";

    [Header("Eventos")]
    [Tooltip("Cada vez que cambia lo tecleado: sonido de tecla, refrescar el botón de empezar...")]
    public UnityEvent OnChanged;

    [Tooltip("Al completarse la última cifra.")]
    public UnityEvent OnCompleted;

    private string entered = string.Empty;

    /// <summary>Lo tecleado, como número. 0 si todavía no hay nada.</summary>
    public int Pin => entered.Length == 0 ? 0 : int.Parse(entered);

    /// <summary>Solo cuando están puestas las N cifras.</summary>
    public bool IsComplete => entered.Length == digits;

    public int Digits => digits;

    private void Awake()
    {
        // Tope en 9 cifras: a partir de 10, int.Parse desbordaría.
        digits = Mathf.Clamp(digits, 1, 9);
    }

    private void Start() => Refresh();

    /// <summary>Cablear aquí cada tecla del 0 al 9, con su cifra como argumento.</summary>
    public void AppendDigit(int digit)
    {
        if (digit < 0 || digit > 9) return;
        if (entered.Length >= digits) return;

        entered += digit.ToString();

        Refresh();
        OnChanged?.Invoke();

        if (IsComplete) OnCompleted?.Invoke();
    }

    [ContextMenu("Borrar la última")]
    public void DeleteLast()
    {
        if (entered.Length == 0) return;

        entered = entered.Substring(0, entered.Length - 1);

        Refresh();
        OnChanged?.Invoke();
    }

    [ContextMenu("Limpiar")]
    public void Clear()
    {
        if (entered.Length == 0) return;

        entered = string.Empty;

        Refresh();
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Propone el siguiente PIN correlativo al último usado.
    ///
    /// Es el caso habitual del piloto: los participantes entran uno detrás de otro, y así el
    /// supervisor no tiene que teclear cuatro cifras por niño con el visor en la mano.
    /// </summary>
    [ContextMenu("Siguiente PIN")]
    public void UseNextPin() => SetPin(TelemetryManager.GetLastPin() + 1);

    /// <summary>Fija el PIN de golpe, rellenando con ceros por la izquierda.</summary>
    public void SetPin(int value)
    {
        int max = (int)Mathf.Pow(10f, digits) - 1;
        value = Mathf.Clamp(value, 0, max);

        entered = value.ToString(new string('0', digits));

        Refresh();
        OnChanged?.Invoke();
        OnCompleted?.Invoke();
    }

    private void Refresh()
    {
        if (!UiText.Any(display, displayTmp)) return;

        string shown = entered;

        for (int i = entered.Length; i < digits; i++)
            shown += emptyChar;

        UiText.Set(display, displayTmp, shown);
    }
}
