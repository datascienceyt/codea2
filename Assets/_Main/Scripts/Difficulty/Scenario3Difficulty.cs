using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Escenario 3 según la dificultad: si la fila se ve, si se puede reordenar y con qué bloques
/// arranca.
///
/// En básica la fila viene montada, bloqueada y OCULTA (Recoger · Girar al destino · Soltar ·
/// Volver): funciona sola y el niño solo elige repeticiones y tipo, sin ver instrucciones. El
/// rótulo y el fondo de la columna de instrucciones llevan DifficultyOnly de intermedia. En
/// intermedia la fila se ve y viene desordenada con giros
/// literales (Girar Izq / Girar Der) y hay que encontrar el orden. Son bloques distintos, cada
/// uno con DifficultyOnly: un bloque que dice una cosa y hace otra es un bug que ya costó
/// arreglar (RotateTowardsSlot).
/// </summary>
public class Scenario3Difficulty : DifficultyVariant<Scenario3Difficulty.Settings>
{
    [Serializable]
    public class Settings
    {
        [Tooltip("Si el niño puede sacar y reordenar los bloques de la fila.")]
        public bool editable;

        [Tooltip("Un elemento por hueco, en orden. Sin bloque, el hueco queda vacío.")]
        public List<SocketRow.InitialBlock> initialBlocks = new List<SocketRow.InitialBlock>();

        [Tooltip("Desactivado, la fila no se ve pero se ejecuta igual. En básica el niño solo " +
                 "elige tipo y repeticiones: las instrucciones no se le enseñan.")]
        public bool showRow = true;
    }

    [SerializeField] private SocketRow row;

    protected override void Apply(Settings settings)
    {
        if (row == null)
        {
            Debug.LogError($"[Dificultad] Escenario 3: falta la fila en '{name}'.", this);
            return;
        }

        row.SetInitialBlocks(settings.initialBlocks);
        row.SetEditable(settings.editable);
        row.SetVisible(settings.showRow);

        if (!settings.showRow && settings.editable)
            Debug.LogWarning($"[Dificultad] Escenario 3: la fila está oculta pero es editable; " +
                             "se podrían agarrar bloques sin verlos.", this);
    }

    public void SetRow(SocketRow socketRow) => row = socketRow;
}
