using System;
using Oculus.Interaction;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Añade al menú de click derecho del propio InteractableUnityEventWrapper las entradas para
/// dispararle sus eventos a mano.
///
/// Va sobre el wrapper que el botón ya tiene, así que no hay que añadirle ningún componente
/// extra a nada: click derecho en la cabecera del componente y listo.
/// </summary>
public static class InteractableEventInvoker
{
    [MenuItem("CONTEXT/InteractableUnityEventWrapper/Disparar Hover")]
    private static void Hover(MenuCommand command) => Fire(command, w => w.WhenHover, "WhenHover");

    [MenuItem("CONTEXT/InteractableUnityEventWrapper/Disparar Unhover")]
    private static void Unhover(MenuCommand command) => Fire(command, w => w.WhenUnhover, "WhenUnhover");

    [MenuItem("CONTEXT/InteractableUnityEventWrapper/Disparar Select")]
    private static void Select(MenuCommand command) => Fire(command, w => w.WhenSelect, "WhenSelect");

    [MenuItem("CONTEXT/InteractableUnityEventWrapper/Disparar Unselect")]
    private static void Unselect(MenuCommand command) => Fire(command, w => w.WhenUnselect, "WhenUnselect");

    /// <summary>
    /// Select y Unselect seguidos, que es lo que ocurre de verdad al pulsar y soltar. Hace
    /// falta si el prefab tiene sonidos o visuales colgando de los dos eventos.
    /// </summary>
    [MenuItem("CONTEXT/InteractableUnityEventWrapper/Pulsación completa (Select + Unselect)")]
    private static void FullPress(MenuCommand command)
    {
        Fire(command, w => w.WhenSelect, "WhenSelect");
        Fire(command, w => w.WhenUnselect, "WhenUnselect");
    }

    private static void Fire(MenuCommand command,
                             Func<InteractableUnityEventWrapper, UnityEvent> pick,
                             string eventName)
    {
        InteractableUnityEventWrapper wrapper = command.context as InteractableUnityEventWrapper;

        if (wrapper == null) return;

        pick(wrapper)?.Invoke();

        Debug.Log($"[Invoker] {wrapper.name} -> {eventName}", wrapper);
    }
}
