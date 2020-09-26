using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HexMapGenerator : MonoBehaviour
{
    [SerializeField]
    HexGrid grid = default;

    [SerializeField, Range(0f, 0.5f), Tooltip("Amount of randomness in the terrain generation. A value of 0 results in hexagon-like patches.")]
    float jitterProbability = 0.25f;

    [SerializeField, Range(20, 200)]
    int chunkSizeMin = 30;

    [SerializeField, Range(20, 200)]
    int chunkSizeMax = 100;

    [SerializeField, Range(1, 5)]
    int waterLevel = 3;

    [SerializeField, Range(5, 95)]
    int landPercentage = 50;

    private int cellCount;
    private int searchFrontierPhase;
    private HexCellPriorityQueue searchFrontier;

    public void GenerateMap(int x, int z)
    {
        cellCount = x * z;
        grid.CreateMap(x, z);
        if (searchFrontier == null)
        {
            searchFrontier = new HexCellPriorityQueue();
        }

        for (int i = 0; i < cellCount; i++)
        {
            grid.GetCell(i).WaterLevel = waterLevel;
        }

        CreateLand();
        SetTerrainType();

        for (int i = 0; i < cellCount; i++)
        {
            grid.GetCell(i).SearchPhase = 0;
        }
    }

    /// <summary>
    /// Raises a random chunk of land by one elevation step. 
    /// </summary>
    /// <param name="chunkSize">The size of the chunks of land to be raised</param>
    /// <param name="budget">The total number of cells that should be raised above the water level</param>
    /// <returns>The remaining amount of land that wasn't raised above the water level</returns>
    int RaiseTerrain(int chunkSize, int budget)
    {
        searchFrontierPhase += 1;
        HexCell firstCell = GetRandomCell();
        firstCell.SearchPhase = searchFrontierPhase;
        firstCell.Distance = 0;
        firstCell.SearchHeuristic = 0;
        searchFrontier.Enqueue(firstCell);
        HexCoordinates center = firstCell.coordinates;

        int size = 0;
        while (size < chunkSize && searchFrontier.Count > 0)
        {
            HexCell current = searchFrontier.Dequeue();
            current.Elevation += 1;
            if (current.Elevation == waterLevel && --budget == 0)
            {
                // Reached the maximum amount of land to raise.
                break;
            }
            size += 1;

            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                HexCell neighbor = current.GetNeighbor(d);
                if (neighbor && neighbor.SearchPhase < searchFrontierPhase)
                {
                    neighbor.SearchPhase = searchFrontierPhase;

                    // By prioritizing the distance to the center cell, we make the chunk grow around its center.
                    neighbor.Distance = neighbor.coordinates.DistanceTo(center);
                    neighbor.SearchHeuristic = Random.value < jitterProbability ? 1 : 0;
                    searchFrontier.Enqueue(neighbor);
                }
            }
        }
        searchFrontier.Clear();

        return budget;
    }

    void CreateLand()
    {
        int landBudget = Mathf.RoundToInt(cellCount * landPercentage * 0.01f);
        while (landBudget > 0)
        {
            landBudget = RaiseTerrain(Random.Range(chunkSizeMin, chunkSizeMax + 1), landBudget);
        }
    }

    void SetTerrainType()
    {
        for (int i = 0; i < cellCount; i++)
        {
            HexCell cell = grid.GetCell(i);
            if (!cell.IsUnderwater)
            {
                cell.TerrainTypeIndex = cell.Elevation - cell.WaterLevel;
            }
        }
    }

    HexCell GetRandomCell()
    {
        return grid.GetCell(Random.Range(0, cellCount));
    }
}
