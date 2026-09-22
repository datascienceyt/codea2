using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Una pantalla más donde el Timer escribe la cuenta atrás.
///
/// El temporizador de sesión es único (RF-06), pero tiene que verse en las cuatro salas y en
/// la muñeca del jugador. En vez de listar las pantallas dentro del Timer, cada pantalla se
/// apunta sola: así un panel puede ser un prefab sin caer en la trampa de que un prefab no
/// puede referenciar un objeto de escena, y añadir una sala nueva no obliga a tocar el Timer.
///
/// Se registra al activarse y se borra al desactivarse: las salas apagadas no cuestan nada, y
/// al encenderse muestran la hora en el acto sin esperar al siguiente frame.
/// </summary>
public class TimerDisplay : MonoBehaviour
{
    [Header("Etiqueta")]
    [Tooltip("Rellena solo la que uses. Lo montado a mano suele ser Text; los botones que " +
             "vienen de los samples de Meta usan TextMeshPro.")]
    [SerializeField] private Text display;

    [Tooltip("Igual, pero en TextMeshPro.")]
    [SerializeField] private TMP_Text displayTmp;

    private Timer _timer;

    private void OnEnable()
    {
        if (!UiText.Any(display, displayTmp))
        {
            Debug.LogWarning($"[Timer] La pantalla '{name}' no tiene etiqueta asignada: " +
                             "no mostrará nada.", this);
            return;
        }

        // La búsqueda va aquí y no en un campo del inspector a propósito: el Timer vive en la
        // escena y estos paneles están pensados para ser prefabs. Se paga una vez por
        // activación de sala, no por frame.
        _timer = FindAnyObjectByType<Timer>();

        if (_timer == null)
        {
            Debug.LogWarning($"[Timer] La pantalla '{name}' no encuentra ningún Timer activo " +
                             "en la escena: se quedará en blanco.", this);
            return;
        }

        _timer.Register(this);
    }

    private void OnDisable()
    {
        if (_timer == null) return;

        _timer.Unregister(this);
        _timer = null;
    }

    /// <summary>
    /// Lo llama el Timer. El texto ya viene formateado desde allí para que todas las pantallas
    /// digan exactamente lo mismo, incluida la de la muñeca.
    /// </summary>
    public void Show(string time) => UiText.Set(display, displayTmp, time);
}
