using System;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public string levelName;
    public int[][] grid;
    public GameObject[][] objectGrid;

    public event Action OnLevelCompleted;
    bool completed;

    /// <summary>Si el bot ya llegó a la meta. Lo consulta ProgramTrigger para saber si un intento resolvió el reto.</summary>
    public bool IsCompleted => completed;

    public void CompleteLevel()
    {
        if (completed) return;
        completed = true;
        OnLevelCompleted?.Invoke();
    }

    public void ResetLevel()
    {
        completed = false;
    }

    public bool ValidMovementInGrid(Vector2 position)
    {
        int x = (int)position.x;
        int y = (int)position.y;

        if (!IsInsideGrid(x, y))
        {
            Debug.Log($"[Escenario1] Choque en ({x},{y}): fuera del tablero.", this);
            RegisterError(LogicErrorType.ColisionBot);
            return false;
        }

        TileType tile = (TileType)grid[y][x];

        if (tile == TileType.Path)
            return true;

        Debug.Log($"[Escenario1] Choque en ({x},{y}): la casilla es {tile}.", this);
        RegisterError(LogicErrorType.ColisionBot);
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
        if (grid == null)
        {
            Debug.LogWarning("[Escenario1] No hay tablero cargado: entra en Play Mode.", this);
            return;
        }

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
    /// Devuelve lo que el robot puede usar en una casilla. Hoy solo la salida.
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
