using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Indicación de la Sala 4 (09/11/2026): mientras el niño tiene una ficha en la mano, parpadean
/// en ámbar suave (HintStyle) TODOS los huecos libres del panel. El hueco del tutorial lo
/// enciende TutorialController, que no lleva Scenario4Controller.
///
/// Todos y no solo el correcto a propósito: el reto de la sala es encontrar la figura (o la letra)
/// que corresponde, y marcar el hueco bueno daría la respuesta. Solo dice "las fichas van ahí".
///
/// Lo añade Scenario4Controller en Start; deja de marcar al completar el panel o agotarse el tiempo.
/// </summary>
public class ShapeSocketHints : MonoBehaviour
{
    private Scenario4Controller controller;
    private readonly List<Socket> sockets = new List<Socket>();
    private ShapeChip[] chips;
    private bool lit;

    private void Start()
    {
        controller = GetComponent<Scenario4Controller>();

        foreach (ShapeSocket shape in controller.Sockets)
            if (shape != null && shape.TryGetComponent(out Socket socket))
                sockets.Add(socket);

        // Las fichas del mismo sitio que el panel: las del tutorial para el del tutorial y las
        // demás para la Sala 4. Así coger una ficha del tutorial no ilumina la sala.
        bool tutorialPanel = GetComponentInParent<TutorialController>(true) != null;
        chips = FindObjectsByType<ShapeChip>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(c => (c.GetComponentInParent<TutorialController>(true) != null) == tutorialPanel)
                .ToArray();
    }

    private void Update()
    {
        bool show = !controller.IsCompleted && !controller.IsTimedOut &&
                    chips.Any(c => c != null && c.IsGrabbed);

        if (!show)
        {
            if (lit) Apply(0f);
            lit = false;
            return;
        }

        lit = true;
        Apply(HintStyle.Pulse);
    }

    private void Apply(float amount)
    {
        // SetHint no toca los huecos con ficha: esos se quedan como estén.
        foreach (Socket socket in sockets)
            if (socket != null) socket.SetHint(amount, HintStyle.Color);
    }

    private void OnDisable()
    {
        if (lit) Apply(0f);
        lit = false;
    }
}
