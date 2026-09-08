using System;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [SerializeField] public string levelName;
    public int[][] grid;
    public GameObject[][] objectGrid;

    public event Action OnLevelCompleted;
    bool completed;

    /// <summary>Si el bot ya llegó a la meta. Lo consulta ProgramTrigger para saber si un intento resolvió el reto.</summary>
    public bool IsCompleted => completed;

    // Contador latente: el sistema de puntos ya no condiciona la meta (solo Exit completa
    // el nivel), pero Point/Interactable siguen en el proyecto y necesitan este enganche.
    public int pointCounter;

    public void CompleteLevel()
    {
        if (completed) return;
        completed = true;
        OnLevelCompleted?.Invoke();
    }

    public void IncreasePoint()
    {
        pointCounter++;
    }

    public void ResetLevel()
    {
        completed = false;
        pointCounter = 0;
    }

    public bool ValidMovementInGrid(Vector2 direction)
    {
        int x = (int)direction.x;
        int y = (int)direction.y;

        if (!IsInsideGrid(x, y))
        {
            print("Out of grid...");
            RegisterError(LogicErrorType.ColisionBot);
            return false;
        }

        TileType tile = (TileType)grid[y][x];

        if (tile == TileType.Path)
            return true;

        print("Ilegal move, cant reach [" + grid[y][x] + "]");
        RegisterError(LogicErrorType.ComandoInvalido);
        return false;
    }

    /// <summary>
    /// La telemetría es opcional: esto lo llama el bot desde dentro de una corrutina, y sin
    /// el guardia una escena sin el prefab de telemetría lanzaba NullReferenceException,
    /// que aborta la ejecución del programa a media secuencia.
    /// </summary>
    void RegisterError(LogicErrorType error)
    {
        if (TelemetryManager.Instance != null)
            TelemetryManager.Instance.RegisterLogicError(error);
    }

    public bool IsInsideGrid(int x, int y)
    {
        // grid queda a null entre ClearLevel() y LoadLevel(). Sin esta comprobación,
        // un movimiento en esa ventana también reventaba la corrutina.
        if (grid == null) return false;

        return y >= 0 && y < grid.Length &&
               x >= 0 && x < grid[y].Length;
    }

    [ContextMenu("Print grid")]
    void PrintGrid()
    {
        string output = string.Empty;
        for (int i = 0; i < grid.Length; i++)
        {
            output += "\n";
            for (int j = 0; j < grid[i].Length; j++)
            {
                output += grid[i][j];
                output += "\t";
            }
        }
        Debug.Log(output);
    }

    /// <summary>
    /// Devuelve el interactuable de una casilla, si lo hay.
    ///
    /// OJO: filtra únicamente TileType.Exit. Un tile marcado como TileType.Interactable se
    /// instancia con su prefab pero queda INALCANZABLE para Bot.Use(), porque no pasa este
    /// filtro. Point e Interactable se conservan de una iteración anterior del sistema de
    /// puntos; para reactivarlos basta con añadir su TileType a la condición de abajo.
    /// </summary>
    public bool TryGetInteractable(Vector2 position, out IInteractable interactable)
    {
        interactable = null;

        int x = (int)position.x;
        int y = (int)position.y;

        if (!IsInsideGrid(x, y)) return false;
        if ((TileType)grid[y][x] != TileType.Exit) return false;

        GameObject obj = objectGrid[y][x];
        if (obj == null) return false;

        interactable = obj.GetComponent<IInteractable>();
        return interactable != null;
    }
}
