using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Guarda la colocación inicial de los bloques que cuelgan de este transform y los
/// devuelve ahí al reiniciar.
///
/// Sustituye al flujo de BlockGenerator: los bloques del reto ya están todos en la
/// escena desde el arranque, así que reiniciar no instancia ni destruye nada, solo
/// mueve de vuelta a su sitio los mismos objetos.
/// </summary>
public class BlockResetter : MonoBehaviour
{
    struct BlockHome
    {
        public Transform block;
        public BlockNode node;
        public Vector3 localPosition;
        public Quaternion localRotation;

        // Estado físico de origen. Se restaura tal cual: las fichas del Escenario 4 empiezan
        // dinámicas y con gravedad, y devolverlas cinemáticas las dejaba sin física para siempre.
        public Rigidbody body;
        public bool kinematic;
        public bool gravity;
    }

    readonly List<BlockHome> homes = new List<BlockHome>();

    void Awake()
    {
        foreach (Transform child in transform)
        {
            Rigidbody body = child.GetComponent<Rigidbody>();

            homes.Add(new BlockHome
            {
                block = child,
                node = child.GetComponent<BlockNode>(),
                localPosition = child.localPosition,
                localRotation = child.localRotation,
                body = body,
                kinematic = body != null && body.isKinematic,
                gravity = body != null && body.useGravity,
            });
        }
    }

    [ContextMenu("Reset Blocks")]
    public void ResetBlocks()
    {
        // Desde código y no por UnityEvent: el botón de reiniciar ya llama aquí, y un segundo
        // cable a la telemetría es justo el que se olvida. TelemetryManager decide si cuenta.
        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.RegisterBlockReset();

        // Lo que el juego bloqueó se queda donde está: una ficha acertada del Escenario 4 o un
        // bloque fijo de la fila. Devolverla dejaba el hueco marcado como resuelto pero vacío,
        // y el escenario ya no se podía terminar. Para un reinicio completo, quien lo pida
        // desbloquea antes (Scenario4Controller.ResetScenario).
        foreach (BlockHome home in homes)
            if (home.node == null || home.node.IsInteractable)
                Restore(home);
    }

    /// <summary>
    /// Devuelve un único bloque a su sitio. Lo usa el Escenario 4 para rechazar un chip mal
    /// colocado sin tocar los que ya están bien puestos.
    /// </summary>
    public bool ReturnBlock(BlockNode block)
    {
        if (block == null) return false;

        foreach (BlockHome home in homes)
        {
            if (home.node != block) continue;

            Restore(home);
            return true;
        }

        return false;
    }

    void Restore(BlockHome home)
    {
        if (home.block == null) return;

        // Al agarrarlo, BlockNode lo desparenta; al soltarlo sobre un socket lo
        // cuelga de este. Por eso hay que reparentarlo antes de la pose local.
        // Una ficha con física puede volver cayendo o girando: se le quita la inercia, o
        // seguiría con la de la caída desde su sitio de origen.
        if (home.body != null && !home.body.isKinematic)
        {
            home.body.linearVelocity = Vector3.zero;
            home.body.angularVelocity = Vector3.zero;
        }

        home.block.SetParent(transform);
        home.block.localPosition = home.localPosition;
        home.block.localRotation = home.localRotation;

        // Desacopla de verdad: con ForgetSocket() el socket se quedaba marcado como ocupado
        // aunque el bloque ya estuviera de vuelta en su sitio, y dejaba de admitir nada.
        if (home.node != null)
            home.node.DetachFromSocket();

        // Después de desacoplar: al encajar, el bloque se congeló, y aquí recupera la física
        // con la que empezó.
        if (home.body != null)
        {
            home.body.isKinematic = home.kinematic;
            home.body.useGravity = home.gravity;
        }

        if (home.node != null)
            home.node.OnReturnedHome();
    }
}
