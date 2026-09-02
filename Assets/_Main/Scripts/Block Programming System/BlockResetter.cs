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
    }

    readonly List<BlockHome> homes = new List<BlockHome>();

    void Awake()
    {
        foreach (Transform child in transform)
        {
            homes.Add(new BlockHome
            {
                block = child,
                node = child.GetComponent<BlockNode>(),
                localPosition = child.localPosition,
                localRotation = child.localRotation
            });
        }
    }

    [ContextMenu("Reset Blocks")]
    public void ResetBlocks()
    {
        foreach (BlockHome home in homes)
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
        home.block.SetParent(transform);
        home.block.localPosition = home.localPosition;
        home.block.localRotation = home.localRotation;

        // Desacopla de verdad: con ForgetSocket() el socket se quedaba marcado como ocupado
        // aunque el bloque ya estuviera de vuelta en su sitio, y dejaba de admitir nada.
        if (home.node != null)
            home.node.DetachFromSocket();
    }
}
