using UnityEngine;

/// <summary>
/// Teletransporta este objeto a la pose de otro.
///
/// Pensado para la pantalla de narración: cada vez que el jugador cambia de sala, se la manda
/// al sitio que le toca en la nueva.
///
/// El destino va como argumento y no como campo del inspector a propósito: así un único
/// componente sirve para todos los sitios, y basta con arrastrar un Transform distinto en
/// cada UnityEvent que lo llame. Con un campo haría falta un componente por destino.
/// </summary>
public class ScreenTeleporter : MonoBehaviour
{
    /// <summary>
    /// Cablear desde un UnityEvent, arrastrando al argumento el Transform de destino.
    /// Copia posición y rotación: la pantalla tiene que quedar mirando al jugador, y eso lo
    /// define la rotación del marcador que pongas en la escena.
    /// </summary>
    public void TeleportTo(Transform destination)
    {
        if (destination == null)
        {
            Debug.LogError($"[Pantalla] '{name}' ha recibido un destino vacío: no se mueve.", this);
            return;
        }

        transform.SetPositionAndRotation(destination.position, destination.rotation);
    }
}
