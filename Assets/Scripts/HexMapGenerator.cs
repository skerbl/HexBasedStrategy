using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HexMapGenerator : MonoBehaviour
{
    struct MapRegion
    {
        public int xMin, xMax, zMin, zMax;
    }

    [SerializeField]
    HexGrid grid = default;

    [SerializeField, Tooltip("Use a custom RNG seed for terrain generation?")]
    bool useFixedSeed = default;

    [SerializeField, Tooltip("The RNG seed used for terrain generation.")]
    int seed = default;

    [SerializeField, Range(0f, 0.5f), Tooltip("Amount of randomness in the terrain generation. A value of 0 results in hexagon-like patches.")]
    float jitterProbability = 0.25f;

    [SerializeField, Range(20, 200)]
    int chunkSizeMin = 30;

    [SerializeField, Range(20, 200)]
    int chunkSizeMax = 100;

    [SerializeField, Range(0, 10), Tooltip("Creates a border of water along the horizontal edges of the map.")]
    int mapBorderX = 5;

    [SerializeField, Range(0, 10), Tooltip("Creates a border of water along the horizontal edges of the map.")]
    int mapBorderZ = 5;

    [SerializeField, Range(1, 4), Tooltip("Breaks the map up into continents.")]
    int regionCount = 1;

    [SerializeField, Range(0, 10), Tooltip("Separates the continents with water.")]
    int regionBorder = 5;

    [SerializeField, Range(-4, 0)]
    int elevationMinimum = -2;

    [SerializeField, Range(6, 10)]
    int elevationMaximum = 8;

    [SerializeField, Range(1, 5)]
    int waterLevel = 3;

    [SerializeField, Range(5, 95)]
    int landPercentage = 50;

    [SerializeField, Range(0f, 1f), Tooltip("Probability of cells being raised by more than one step, forming cliffs.")]
    float highRiseProbability = 0.25f;

    [SerializeField, Range(0f, 0.4f), Tooltip("Probability of cells being lowered instead of raised, creating more variations in terrain.")]
    float sinkProbability = 0.2f;

    private int cellCount;
    private int searchFrontierPhase;
    private List<MapRegion> regions;
    private HexCellPriorityQueue searchFrontier;

    public void GenerateMap(int x, int z)
    {
        Random.State originalRandomState = Random.state;

        if (!useFixedSeed)
        {
            // The lower 32 bits of the system time XOR-ed (so it doesn't increase) with the current run time makes a reasonable seed.
            seed = Random.Range(0, int.MaxValue);
            seed ^= (int)System.DateTime.Now.Ticks;
            seed ^= (int)Time.unscaledTime;
            seed &= int.MaxValue;   // Force it to be positive
        }
        Random.InitState(seed);

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

        CreateRegions();
        CreateLand();
        SetTerrainType();

        for (int i = 0; i < cellCount; i++)
        {
            grid.GetCell(i).SearchPhase = 0;
        }

        Random.state = originalRandomState;
    }

    /// <summary>
    /// Breaks the map up into 1-4 separate regions. Currently, they are
    /// split along straight lines, equal in size, and equal in landmass.
    /// Maybe add some more advance splitting methods that produce more organic results?
    /// </summary>
    void CreateRegions()
    {
        if (regions == null)
        {
            regions = new List<MapRegion>();
        }
        else
        {
            regions.Clear();
        }

        switch (regionCount)
        {
            default:
                CreateOneRegion();
                break;
            case 2:
                CreateTwoRegions();
                break;
            case 3:
                CreateThreeRegions();
                break;
            case 4:
                CreateFourRegions();
                break;
        }
    }

    private void CreateOneRegion()
	{
        MapRegion region;
        region.xMin = mapBorderX;
        region.xMax = grid.cellCountX - mapBorderX;
        region.zMin = mapBorderZ;
        region.zMax = grid.cellCountZ - mapBorderZ;
        regions.Add(region);
    }

    private void CreateTwoRegions()
	{
        MapRegion region;
        if (Random.value < 0.5f)
        {
            region.xMin = mapBorderX;
            region.xMax = grid.cellCountX / 2 - regionBorder;
            region.zMin = mapBorderZ;
            region.zMax = grid.cellCountZ - mapBorderZ;
            regions.Add(region);
            region.xMin = grid.cellCountX / 2 + regionBorder;
            region.xMax = grid.cellCountX - mapBorderX;
            regions.Add(region);
        }
        else
        {
            region.xMin = mapBorderX;
            region.xMax = grid.cellCountX - mapBorderX;
            region.zMin = mapBorderZ;
            region.zMax = grid.cellCountZ / 2 - regionBorder;
            regions.Add(region);
            region.zMin = grid.cellCountZ / 2 + regionBorder;
            region.zMax = grid.cellCountZ - mapBorderZ;
            regions.Add(region);
        }
    }

    private void CreateThreeRegions()
	{
        MapRegion region;
        region.xMin = mapBorderX;
        region.xMax = grid.cellCountX / 3 - regionBorder;
        region.zMin = mapBorderZ;
        region.zMax = grid.cellCountZ - mapBorderZ;
        regions.Add(region);
        region.xMin = grid.cellCountX / 3 + regionBorder;
        region.xMax = grid.cellCountX * 2 / 3 - regionBorder;
        regions.Add(region);
        region.xMin = grid.cellCountX * 2 / 3 + regionBorder;
        region.xMax = grid.cellCountX - mapBorderX;
        regions.Add(region);
    }

    private void CreateFourRegions()
	{
        MapRegion region;
        region.xMin = mapBorderX;
        region.xMax = grid.cellCountX / 2 - regionBorder;
        region.zMin = mapBorderZ;
        region.zMax = grid.cellCountZ / 2 - regionBorder;
        regions.Add(region);
        region.xMin = grid.cellCountX / 2 + regionBorder;
        region.xMax = grid.cellCountX - mapBorderX;
        regions.Add(region);
        region.zMin = grid.cellCountZ / 2 + regionBorder;
        region.zMax = grid.cellCountZ - mapBorderZ;
        regions.Add(region);
        region.xMin = mapBorderX;
        region.xMax = grid.cellCountX / 2 - regionBorder;
        regions.Add(region);
    }

    /// <summary>
    /// Raises a random chunk of land by one elevation step (with a chance of raising by 2).
    /// </summary>
    /// <param name="chunkSize">The size of the chunk of land to be raised</param>
    /// <param name="budget">The total number of cells that should be raised above the water level</param>
    /// <returns>The remaining amount of land that wasn't raised above the water level</returns>
    int RaiseTerrain(int chunkSize, int budget, MapRegion region)
    {
        searchFrontierPhase += 1;
        HexCell firstCell = GetRandomCell(region);
        firstCell.SearchPhase = searchFrontierPhase;
        firstCell.Distance = 0;
        firstCell.SearchHeuristic = 0;
        searchFrontier.Enqueue(firstCell);
        HexCoordinates center = firstCell.coordinates;

        int rise = Random.value < highRiseProbability ? 2 : 1;
        int size = 0;
        while (size < chunkSize && searchFrontier.Count > 0)
        {
            HexCell current = searchFrontier.Dequeue();
            int originalElevation = current.Elevation;

            int newElevation = originalElevation + rise;
            if (newElevation > elevationMaximum)
            {
                continue;
            }

            current.Elevation = newElevation;
            if (originalElevation < waterLevel && newElevation >= waterLevel && --budget == 0)
            {
                // Reached the maximum amount of land to raise.
                searchFrontier.Clear();
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

    /// <summary>
    /// Lowers a chunk of land by one elevation step (with a chance of lowering by 2).
    /// </summary>
    /// <param name="chunkSize">The size of the chunk of land to be lowered</param>
    /// <param name="budget">The total number of cells that should be lowered</param>
    /// <returns>The new amount of land that wasn't raised above the water level</returns>
    int SinkTerrain(int chunkSize, int budget, MapRegion region)
    {
        searchFrontierPhase += 1;
        HexCell firstCell = GetRandomCell(region);
        firstCell.SearchPhase = searchFrontierPhase;
        firstCell.Distance = 0;
        firstCell.SearchHeuristic = 0;
        searchFrontier.Enqueue(firstCell);
        HexCoordinates center = firstCell.coordinates;

        int sink = Random.value < highRiseProbability ? 2 : 1;
        int size = 0;
        while (size < chunkSize && searchFrontier.Count > 0)
        {
            HexCell current = searchFrontier.Dequeue();
            int originalElevation = current.Elevation;

            int newElevation = current.Elevation - sink;
            if (newElevation < elevationMinimum)
            {
                continue;
            }

            current.Elevation = newElevation;
            if (originalElevation >= waterLevel && newElevation < waterLevel)
            {
                // Reclaim budget
                budget += 1;
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
        for (int failsafe = 0; failsafe < 10000; failsafe++)
        {
            bool sink = Random.value < sinkProbability;
            for (int i = 0; i < regions.Count; i++)
            {
                MapRegion region = regions[i];
                int chunkSize = Random.Range(chunkSizeMin, chunkSizeMax - 1);
                if (sink)
                {
                    landBudget = SinkTerrain(chunkSize, landBudget, region);
                }
                else
                {
                    landBudget = RaiseTerrain(chunkSize, landBudget, region);
                    if (landBudget == 0)
                    {
                        return;
                    }
                }
            }
        }

        if (landBudget > 0)
        {
            Debug.LogWarning("Failed to use up " + landBudget + " land budget.");
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

    HexCell GetRandomCell(MapRegion region)
    {
        return grid.GetCell(Random.Range(region.xMin, region.xMax), Random.Range(region.zMin, region.zMax));
    }
}
