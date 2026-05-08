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
        grid[3, 2].AddGas(new Gas(Oxygen, 4464f), 293f);  // Temp in kelvin (very important or low pressure)
        grid[7, 2].AddGas(new Gas(Hydrogen, 2000f), 260f);
        foreach (Gas g in grid[3, 2].gases)
        {
            Debug.Log($"Gas: {g.type.name}, Amount: {g.amount}, Pressure: {grid[3, 2].pressure}");
        }
    }

    public float debugGizmoConst = 1000f;
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
                float normalized = Mathf.Clamp01(p / debugGizmoConst);

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
        List<(Cell pendingGainingCell, Gas pendingGas, float sentTemperature)> pendingTransfers = new List<(Cell, Gas, float)>();
        List<(Cell pendingCell, Gas pendingGas)> pendingLosses = new List<(Cell, Gas)>();

        float systemGasAmount = 0f;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                SpreadGas(grid[x, y], pendingTransfers, pendingLosses);
                systemGasAmount += grid[x, y].totalGasAmount;
            }
        }
        
        // Activate the pending transfers or losses
        foreach ((Cell pendingCell, Gas pendingGas, float sentTemperature) in pendingTransfers)
        {
            pendingCell.AddGas(pendingGas, sentTemperature);
        }
        foreach ((Cell pendingCell, Gas pendingGas) in pendingLosses)
        {
            pendingCell.RemoveGas(pendingGas);
        }

        // DEBUG BELOW
        string text = "";
        foreach (Gas g in grid[3, 2].gases)
        {
            text += $"INITIAL:    Gas: {g.type.name}, Amount: {g.amount}, Temp: {grid[3, 2].temperature}, Pressure: {grid[3, 2].pressure}\n";
        }
        Debug.Log(text);
        text = "";
        foreach (Gas g in grid[4, 2].gases)
        {
            text += $"NEIGHBOR:   Gas: {g.type.name}, Amount: {g.amount}, Temp: {grid[4, 2].temperature}, Pressure: {grid[4, 2].pressure}\n";
        }
        Debug.Log(text);

        Debug.Log($"TOTAL SYSTEM N: {systemGasAmount}");
    }

    public float overspill = 0.1f;

    void SpreadGas(Cell cell, List<(Cell pendingNeighbor, Gas pendingGas, float sentTemperature)> pendingTransfers, List<(Cell pendingCell, Gas pendingGas)> pendingLosses)
    {
        // Check neighbors and move gas around
        int[][] directions = new int[][]
        {
            new int[] { 1, 0 },
            new int[] { -1, 0 },
            new int[] { 0, 1 },
            new int[] { 0, -1 }
        };
        List<Cell> validCells = new List<Cell>();

        // Check each direction and determine neighboring cells with lower pressure
        foreach (var dir in directions)
        {
            int nx = cell.x + dir[0];
            int ny = cell.y + dir[1];

            if ((nx >= 0 && nx < width) && (ny >= 0 && ny < height))
            {
                Cell neighbor = grid[nx, ny];
                if (cell.pressure > neighbor.pressure)
                {
                    validCells.Add(neighbor);
                }
            }
        }

        if (validCells.Count == 0)
        {
            return;
        }

        // Do diffusion for each gas separately
        foreach (Gas g in cell.gases)
        {
            foreach (Cell neighbor in validCells)
            {
                float cellPartialPressure = g.amount / cell.totalGasAmount * cell.pressure;
                var neighborGas = neighbor.GetGas(g.type);
                float neighborGasAmt;
                if (neighborGas == null)
                {
                    neighborGasAmt = 0;
                }
                else
                {
                    neighborGasAmt = neighborGas.amount;
                }
                float neighborPartialPressure = neighborGasAmt / neighbor.totalGasAmount * neighbor.pressure;
                if (float.IsNaN(neighborPartialPressure)) neighborPartialPressure = 0;
                float transferWeight = 1f/(validCells.Count + 1) + (overspill / validCells.Count * Random.value);
                float pressureDiff = cellPartialPressure - neighborPartialPressure;
                float transferPressure = pressureDiff * transferWeight;
                float transfer = transferPressure / cellPartialPressure * g.amount;
                if (float.IsNaN(transfer)) transfer = 0;
                transfer = Mathf.Clamp(transfer, 0f, g.amount);


                pendingLosses.Add((cell, new Gas(g.type, transfer)));
                pendingTransfers.Add((neighbor, new Gas(g.type, transfer), cell.temperature));
            }
        }
    }
}

public class Cell
{
    public int x;
    public int y;

    public float temperature;
    public List<Gas> gases;
    public List<CellContent> contents;

    const float R = 8.31446261815324f;
    const float cellVolume = 1f;    // Cubic meters

    public Cell(int x, int y)
    {
        this.x = x;
        this.y = y;
        temperature = 0f;
        gases = new List<Gas>();
        contents = new List<CellContent>();
    }

    public float pressure
    {
        get
        {
            return totalGasAmount * R * temperature / cellVolume;
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

    public Gas GetGas(GasType type)
    {
        foreach (Gas g in gases)
        {
            if (g.type == type)
            {
                return g;
            }
        }
        return null;
    }

    public void AddGas(Gas addedGas, float addedTemperature)
    {
        if (totalGasAmount == 0f)
        {
            temperature = addedTemperature;
        }
        else
        {
            temperature = ((totalGasAmount * temperature) + (addedGas.amount * addedTemperature)) / (totalGasAmount + addedGas.amount);
        }

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

    public void RemoveGas(Gas removedGas)
    {
        for (int i = gases.Count - 1; i >= 0; i--)
        {
            if (removedGas.type == gases[i].type)
            {
                gases[i].amount -= Mathf.Min(removedGas.amount, gases[i].amount);
                if (gases[i].amount < 0.00001f)
                {
                    gases.RemoveAt(i);
                }
            }
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