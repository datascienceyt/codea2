using System;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [SerializeField] public string levelName;
    public int[][] grid;
    public GameObject[][] objectGrid;

    //public event Action OnExit;
    public event Action OnDanger;

    public int pointCounter { get; private set; } = 0;

    public void IncreasePoint()
    {
        pointCounter++;
    }

    public void ResetLevel()
    {
        pointCounter = 0;
    }

    public bool ValidMovementInGrid(Vector2 direction)
    {
        int x = (int)direction.x;
        int y = (int)direction.y;

        if (!IsInsideGrid(x, y))
        {
            print("Out of grid...");
            return false;
        }

        int gridValue = grid[y][x];

        TileType tile = (TileType)gridValue;

        if (tile == TileType.Path || tile == TileType.Exit)
        {
            //print("Valid movement");
            return true;
        }
        else
        {
            print("Ilegal move, cant reach [" + grid[y][x] + "]");
            return false;
        }
    }

    public bool IsInsideGrid(int x, int y)
    {
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
            for(int j = 0; j < grid[i].Length; j++)
            {
                output += grid[i][j];
                output += "\t";
            }
        }

        Debug.Log(output);
    }
}
