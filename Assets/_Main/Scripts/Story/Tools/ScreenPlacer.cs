using System.Collections;
using UnityEngine;

/// <summary>
/// Mueve un objeto a la pose de otro cuando el Director llega a su paso.
///
/// Pensado para la pantalla de narración: en vez de perseguir al jugador todo el rato, se
/// coloca una sola vez por paso, en el sitio que le corresponde a esa sala. Como es un
/// IStepAction, el Director la reposiciona antes de seguir.
///
/// Se puede montar de dos maneras, según cuántos destinos necesites:
///
///   Un solo destino, en el propio objeto que se mueve. Deja 'target' vacío y apunta
///   'destination' a donde quieras llevarlo.
///
///   Varios destinos, uno por sala. Pon este componente en cada marcador de destino, deja
///   'destination' vacío para que use su propia pose, y apunta 'target' a la pantalla. Luego
///   arrastra cada marcador a los waitActions del paso que toque. Es la forma que querrás si
///   la pantalla cambia de sitio varias veces.
/// </summary>
public class ScreenPlacer : MonoBehaviour, IStepAction
{
    [Tooltip("Qué se mueve. Si queda vacío se mueve este mismo objeto.")]
    [SerializeField] private Transform target;

    [Tooltip("A dónde se mueve, copiando su posición y su rotación. Si queda vacío se usa la " +
             "pose de este mismo objeto.")]
    [SerializeField] private Transform destination;

    [Tooltip("Segundos que tarda en llegar. 0 lo coloca de golpe, que es lo que suele " +
             "interesar si el paso ocurre con la pantalla fundida a negro.")]
    [SerializeField] private float moveTime = 0f;

    /// <summary>Coloca la pantalla y devuelve el control al Director.</summary>
    public IEnumerator Execute()
    {
        Transform moved = target != null ? target : transform;
        Transform to = destination != null ? destination : transform;

        if (moved == to)
        {
            Debug.LogError($"[Pantalla] '{name}' no tiene ni objeto que mover ni destino: " +
                           "rellena al menos uno de los dos o el paso no hará nada.", this);
            yield break;
        }

        if (moveTime <= 0f)
        {
            moved.SetPositionAndRotation(to.position, to.rotation);
            yield break;
        }

        Vector3 fromPosition = moved.position;
        Quaternion fromRotation = moved.rotation;

        for (float t = 0f; t < 1f; t += Time.deltaTime / moveTime)
        {
            moved.SetPositionAndRotation(Vector3.Lerp(fromPosition, to.position, t),
                                         Quaternion.Slerp(fromRotation, to.rotation, t));
            yield return null;
        }

        // El bucle sale con t por encima de 1, así que la última pose quedaría a medio camino
        // del destino. Se fija exacta al terminar.
        moved.SetPositionAndRotation(to.position, to.rotation);
    }

    /// <summary>Lo mismo, para cablear desde un UnityEvent en vez de esperar al Director.</summary>
    [ContextMenu("Colocar")]
    public void Place() => StartCoroutine(Execute());
}
