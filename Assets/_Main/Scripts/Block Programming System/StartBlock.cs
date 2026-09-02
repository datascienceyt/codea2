using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StartBlock : BlockNode
{
    public override string InstructionLabel => "Iniciar";

    public override IEnumerator Execute()
    {
        print("Iniciando.");
        yield break;
    }

    /// <summary>
    /// Destruye los bloques colgados de este.
    ///
    /// Obsoleto en el flujo actual: desde que existe BlockResetter no se destruye nada, los
    /// bloques vuelven a su sitio. Se conserva porque puede seguir cableado en alguna escena.
    /// </summary>
    public void Clear()
    {
        // La versión anterior hacía Destroy(transform), que intenta destruir el COMPONENTE
        // Transform —Unity lo rechaza con un error— en vez del GameObject. Además usaba
        // GetComponentInChildren<Transform>(), que devuelve el propio transform, no los hijos.
        List<GameObject> blocks = new List<GameObject>();

        foreach (Transform child in transform)
        {
            if (child.CompareTag("Block"))
                blocks.Add(child.gameObject);
        }

        // Se recogen primero y se destruyen después: modificar la jerarquía mientras se itera
        // sobre ella se salta hijos.
        foreach (GameObject block in blocks)
            Destroy(block);
    }
}
