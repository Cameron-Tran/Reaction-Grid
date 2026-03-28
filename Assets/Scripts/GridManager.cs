using UnityEngine;
using System.Collections.Generic;
using static GasTypes;

public class GridManager : MonoBehaviour
{
    // Configure grid size
    public int width = 10;
    public int height = 10;
    public Cell[,] grid;

    void Start()
    {
        // Create the grid of cells
        grid = new Cell[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = new Cell(x, y);
            }
        }

        // Testing gas adding
        grid[3, 2].AddGas(new Gas(Oxygen, 50f), 20f);
        foreach (Gas g in grid[3, 2].gases)
        {
            Debug.Log($"Gas: {g.type.name}, Amount: {g.amount}");
        }
    }

    void OnDrawGizmos()
    {
        if (grid == null) return;

        float size = 1f;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float p = grid[x, y].pressure;

                // Normalize pressure (adjust this scale as needed)
                float normalized = Mathf.Clamp01(p / 1000f);

                // Color from black to red
                Gizmos.color = Color.Lerp(Color.black, Color.red, normalized);

                Vector3 pos = new Vector3(x * size, y * size, 0);
                Gizmos.DrawCube(pos, Vector3.one * 0.9f);
            }
        }
    }

    public float interval = 3f; // seconds
    private float timer = 0f;

    void Update()
    {
        timer += Time.deltaTime; // accumulate time since last frame

        if (timer >= interval)
        {
            timer = 0f; // reset timer
            UpdateGrid(); // run your function
        }
    }

    void UpdateGrid()
    {
        List<(Cell pendingNeighbor, Gas pendingGas)> pendingTransfers = new List<(Cell, Gas)>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                SpreadGas(grid[x, y], pendingTransfers);
            }
        }

        foreach ((Cell pendingNeigbor, Gas pendingGas) in pendingTransfers)
        {
            pendingNeigbor.AddGas(pendingGas, pendingNeigbor.temperature);
        }
    }

    public float diffConst = 2f;
    void SpreadGas(Cell cell, List<(Cell pendingNeighbor, Gas pendingGas)> pendingTransfers)
    {
        // Check neighbors and move gas around
        int[][] directions = new int[][]
        {
            new int[] { 1, 0 },
            new int[] { -1, 0 },
            new int[] { 0, 1 },
            new int[] { 0, -1 }
        };
        int amountValidSides = 0;
        List<Cell> validCells = new List<Cell>();

        foreach (var dir in directions)
        {
            int nx = cell.x + dir[0];
            int ny = cell.y + dir[1];

            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
            {
                Cell neighbor = grid[nx, ny];
                if (cell.pressure > neighbor.pressure)
                {
                    amountValidSides++;
                    validCells.Add(neighbor);
                }
            }
        }

        if (amountValidSides == 0)
        {
            return;
        }

        foreach (Gas g in cell.gases)
        {
            float lossTotal = 0f;
            foreach (Cell neighbor in validCells)
            {
                float differential = (cell.pressure - neighbor.pressure);
                float transfer = (g.amount / ((float)amountValidSides + 1f));    // Tweakable exponent base
                lossTotal += transfer;
                pendingTransfers.Add((neighbor, new Gas(g.type, transfer)));
            }
            //pendingLosses.Add((cell, g, lossTotal));
            g.amount -= lossTotal;
        }
    }
}

public class Cell
{
    public int x;
    public int y;

    public float temperature = 0f;
    public List<Gas> gases;
    public List<CellContent> contents;

    const float R = 8.31446261815324f;
    const float cellVolume = 1f;    // Cubic meters

    public Cell(int x, int y)
    {
        this.x = x;
        this.y = y;
        gases = new List<Gas>();
        contents = new List<CellContent>();
    }

    public float pressure
    {
        get
        {
            float total = 0f;
            foreach (var g in gases)
            {
                total += g.amount * R * temperature / cellVolume;
            }
            return total;
        }
    }

    public float totalGasAmount
    {
        get
        {
            float total = 0f;
            foreach (var g in gases)
            {
                total += g.amount;
            }
            return total;
        }
    }

    public void AddContent(CellContent content)
    {
        contents.Add(content);
    }

    public void AddGas(Gas addedGas, float addedTemperature)
    {
        if (addedGas.amount >= 0.001f)
        {
            temperature = ((totalGasAmount * temperature) + (addedGas.amount * addedTemperature)) / (totalGasAmount + addedGas.amount);

            foreach (var g in gases)
            {
                if (g.type == addedGas.type)
                {
                    g.amount += addedGas.amount;
                    return;
                }
            }
            gases.Add(addedGas);
        }
    }
}

public static class GasTypes
{
    public static readonly GasType Oxygen = new GasType("Oxygen");
    public static readonly GasType Hydrogen = new GasType("Hydrogen");
}

public class GasType
{
    public string name;

    public GasType(string name)
    {
        this.name = name;
    }
}

public class Gas
{
    public GasType type;
    public float amount;

    public Gas(GasType type, float amount)
    {
        this.type = type;
        this.amount = amount;
    }
}

public abstract class CellContent
{
    public string name;
    public virtual void OnInteract()
    {

    }
}

public class Wall : CellContent
{
    public Wall()
    {
        name = "Wall";
    }
}