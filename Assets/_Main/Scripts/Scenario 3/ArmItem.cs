using UnityEngine;

// Los valores nuevos van SIEMPRE al final: el índice se serializa en cada barril, caja, pila
// y ficha de tipo ya colocados en la escena, e insertar en medio los reasignaría en silencio.
public enum ArmItemType
{
    Barril,
    Caja
}

/// <summary>
/// Marca un objeto del Escenario 3 con su tipo, para que el brazo sepa de qué pila cogerlo y
/// a qué destino pertenece.
///
/// Va en el propio barril o caja, no en una lista del slot: un objeto se mueve entre pilas
/// durante la partida, y su tipo tiene que viajar con él.
/// </summary>
public class ArmItem : MonoBehaviour
{
    [Tooltip("Barriles rojos a la izquierda, cajas azules a la derecha.")]
    public ArmItemType type = ArmItemType.Barril;
}
