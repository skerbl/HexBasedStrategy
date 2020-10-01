using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HexMapGenerator : MonoBehaviour
{
    public enum HemisphereMode
    {
        Both, North, South
    }

    struct MapRegion
    {
        public int xMin, xMax, zMin, zMax;
    }

    struct ClimateData
    {
        public float clouds;
        public float moisture;
        public float temperature;
    }

    struct Biome
    {
        public int terrain;
        public int plant;

        public Biome(int terrain, int plant)
        {
            this.terrain = terrain;
            this.plant = plant;
        }
    }

    static float[] temperatureBands = { 0.1f, 0.3f, 0.6f };
    static float[] moistureBands = { 0.12f, 0.28f, 0.85f };
    
    // The biome matrix used for determining terrain types.
    // X axis represents moisture levels, and Y axis temperature levels.
    static Biome[] biomes = {
        new Biome(0, 0), new Biome(4, 0), new Biome(4, 0), new Biome(4, 0),
        new Biome(0, 0), new Biome(2, 0), new Biome(2, 1), new Biome(2, 2),
        new Biome(0, 0), new Biome(1, 0), new Biome(1, 1), new Biome(1, 2),
        new Biome(0, 0), new Biome(1, 1), new Biome(1, 2), new Biome(1, 3)
    };

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

    [SerializeField, Range(0, 100), Tooltip("Smoothens the terrain after creating landmasses. A value of 100 will remove any cliffs from the map, leaving only smooth, sloped terrain.")]
    int erosionPercentage = 50;

    [SerializeField, Range(0f, 1f), Tooltip("Initial moisture levels for land cells. This can prevent excessively dry climates, especially on big landmasses.")]
    float startingMoisture = 0.1f;

    [SerializeField, Range(0f, 1f), Tooltip("Global evaporation of the map. Controls how much vapour each water cell generates per cycle.")]
    float evaporationFactor = 0.5f;

    [SerializeField, Range(0f, 1f), Tooltip("Global precipitation factor. Controls how much vapour each cloud will lose to rainfall each cycle.")]
    float precipitationFactor = 0.25f;

    [SerializeField, Range(0f, 1f), Tooltip("Controls how much moisture drains away after precipitating, flowing towards cells with lower elevation.")]
    float runoffFactor = 0.25f;

    [SerializeField, Range(0f, 1f), Tooltip("Controls how much moisture spreads across level terrain, and from water to land cells.")]
    float seepageFactor = 0.125f;

    [SerializeField, Tooltip("Controls the direction from which the wind is coming from.")]
    HexDirection windDirection = HexDirection.NW;

    [SerializeField, Range(1f, 10f), Tooltip("Wind pushes moisture in a specific direction. At strength 1, clouds disperse evenly in all directions.")]
    float windStrength = 4f;

    [SerializeField, Range(0, 20)]
    int riverPercentage = 10;

    [SerializeField, Range(0f, 1f)]
    float extraLakeProbability = 0.25f;

    [SerializeField, Range(0f, 1f)]
    float lowTemperature = 0f;

    [SerializeField, Range(0f, 1f)]
    float highTemperature = 1f;

    [SerializeField, Range(0f, 1f)]
    float temperatureJitter = 0.1f;

    [SerializeField]
    HemisphereMode hemisphere = default;

    private int cellCount;
    private int landCells;
    private int searchFrontierPhase;
    private int temperatureJitterChannel;
    private List<MapRegion> regions;
    private List<ClimateData> climate = new List<ClimateData>();
    private List<ClimateData> nextClimate = new List<ClimateData>();
    private List<HexDirection> flowDirections = new List<HexDirection>();
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
        ErodeLand();
        CreateClimate();
        CreateRivers();
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
    private int RaiseTerrain(int chunkSize, int budget, MapRegion region)
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
    private int SinkTerrain(int chunkSize, int budget, MapRegion region)
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

    private void CreateLand()
    {
        int landBudget = Mathf.RoundToInt(cellCount * landPercentage * 0.01f);
        landCells = landBudget;

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
            landCells -= landBudget;
        }
    }

    /// <summary>
    /// Erodes the terrain by removing steep elevation differences. Preserves total
    /// landmass by shifting any lowered terrain to another location.
    /// </summary>
    private void ErodeLand()
    {
        List<HexCell> erodibleCells = ListPool<HexCell>.Get();
        for (int i = 0; i < cellCount; i++)
        {
            HexCell cell = grid.GetCell(i);
            if (IsErodible(cell))
            {
                erodibleCells.Add(cell);
            }
        }

        int targetErodibleCount = (int)(erodibleCells.Count * (100 - erosionPercentage) * 0.01f);

        while (erodibleCells.Count > targetErodibleCount)
        {
            int index = Random.Range(0, erodibleCells.Count);
            HexCell cell = erodibleCells[index];
            HexCell targetCell = GetErosionTarget(cell);

            cell.Elevation -= 1;
            targetCell.Elevation += 1;

            // Only remove the cell from the list if it's been lowered far enough
            if (!IsErodible(cell))
            {
                erodibleCells[index] = erodibleCells[erodibleCells.Count - 1];
                erodibleCells.RemoveAt(erodibleCells.Count - 1);
            }

            // Check if any additional surrounding cells have now become erodible
            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                HexCell neighbor = cell.GetNeighbor(d);
                if (neighbor && neighbor.Elevation == cell.Elevation + 2 && !erodibleCells.Contains(neighbor))
                {
                    erodibleCells.Add(neighbor);
                }
            }

            if (IsErodible(targetCell) && !erodibleCells.Contains(targetCell))
            {
                erodibleCells.Add(targetCell);
            }

            // Check if any neighbors of the target cell have now become no longer erodible
            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                HexCell neighbor = targetCell.GetNeighbor(d);
                if (neighbor && neighbor != cell && neighbor.Elevation == targetCell.Elevation + 1 && !IsErodible(neighbor))
                {
                    erodibleCells.Remove(neighbor);
                }
            }
        }

        ListPool<HexCell>.Add(erodibleCells);
    }

    private bool IsErodible(HexCell cell)
    {
        int erodibleElevation = cell.Elevation - 2;
        for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
        {
            HexCell neighbor = cell.GetNeighbor(d);
            if (neighbor && neighbor.Elevation <= erodibleElevation)
            {
                return true;
            }
        }
        return false;
    }

    private HexCell GetErosionTarget(HexCell cell)
    {
        List<HexCell> candidates = ListPool<HexCell>.Get();
        int erodibleElevation = cell.Elevation - 2;

        for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
        {
            HexCell neighbor = cell.GetNeighbor(d);
            if (neighbor && neighbor.Elevation <= erodibleElevation)
            {
                candidates.Add(neighbor);
            }
        }

        HexCell target = candidates[Random.Range(0, candidates.Count)];
        ListPool<HexCell>.Add(candidates);
        return target;
    }

    private void CreateClimate()
    {
        climate.Clear();
        nextClimate.Clear();
		ClimateData initialData = new ClimateData { moisture = startingMoisture };
		ClimateData clearData = new ClimateData();

        for (int i = 0; i < cellCount; i++)
        {
            climate.Add(initialData);
            nextClimate.Add(clearData);
        }

        for (int cycle = 0; cycle < 40; cycle++)
        {
            for (int i = 0; i < cellCount; i++)
            {
                EvolveClimate(i);
            }
            List<ClimateData> swap = climate;
            climate = nextClimate;
            nextClimate = swap;
        }
    }

    private void EvolveClimate(int cellIndex)
    {
        HexCell cell = grid.GetCell(cellIndex);
        ClimateData cellClimate = climate[cellIndex];

        if (cell.IsUnderwater)
        {
            cellClimate.moisture = 1f;
            cellClimate.clouds += evaporationFactor;
        }
        else
        {
            float evaporation = cellClimate.moisture * evaporationFactor;
            cellClimate.moisture -= evaporation;
            cellClimate.clouds += evaporation;
        }

        float precipitation = cellClimate.clouds * precipitationFactor;
        cellClimate.clouds -= precipitation;
        cellClimate.moisture += precipitation;

        // Higher elevation = lower temperatures = less water that the air can hold
        float cloudMaximum = 1f - cell.ViewElevation / (elevationMaximum + 1f);
        if (cellClimate.clouds > cloudMaximum)
        {
            cellClimate.moisture += cellClimate.clouds - cloudMaximum;
            cellClimate.clouds = cloudMaximum;
        }

        HexDirection mainDispersalDirection = windDirection.Opposite();
        float cloudDispersal = cellClimate.clouds * (1f / (5f + windStrength));
        float runoff = cellClimate.moisture * runoffFactor * (1f / 6f);
        float seepage = cellClimate.moisture * seepageFactor * (1f / 6f);

        for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
        {
            HexCell neighbor = cell.GetNeighbor(d);
            if (!neighbor)
            {
                continue;
            }

            ClimateData neighborClimate = nextClimate[neighbor.Index];
            if (d == mainDispersalDirection)
            {
                neighborClimate.clouds += cloudDispersal * windStrength;
            }
            else
            {
                neighborClimate.clouds += cloudDispersal;
            }

            int elevationDelta = neighbor.ViewElevation - cell.ViewElevation;
            if (elevationDelta < 0)
            {
                cellClimate.moisture -= runoff;
                neighborClimate.moisture += runoff;
            }
            else if (elevationDelta == 0)
            {
                cellClimate.moisture -= seepage;
                neighborClimate.moisture += seepage;
            }

            nextClimate[neighbor.Index] = neighborClimate;
        }

        ClimateData nextCellClimate = nextClimate[cellIndex];
        nextCellClimate.moisture += cellClimate.moisture;
        if (nextCellClimate.moisture > 1f)
        {
            nextCellClimate.moisture = 1f;
        }
        nextClimate[cellIndex] = nextCellClimate;
        climate[cellIndex] = new ClimateData();
    }

    private void CreateRivers()
    {
        List<HexCell> riverOrigins = ListPool<HexCell>.Get();
        for (int i = 0; i < cellCount; i++)
        {
            HexCell cell = grid.GetCell(i);
            if (cell.IsUnderwater)
            {
                continue;
            }
            ClimateData data = climate[i];
            float weight = data.moisture * (cell.Elevation - waterLevel) / (elevationMaximum - waterLevel);

            if (weight > 0.75f)
            {
                riverOrigins.Add(cell);
                riverOrigins.Add(cell);
            }
            if (weight > 0.5f)
            {
                riverOrigins.Add(cell);
            }
            if (weight > 0.25f)
            {
                riverOrigins.Add(cell);
            }
        }

        int riverBudget = Mathf.RoundToInt(landCells * riverPercentage * 0.01f);
        while (riverBudget > 0 && riverOrigins.Count > 0)
        {
            int index = Random.Range(0, riverOrigins.Count);
            int lastIndex = riverOrigins.Count - 1;
            HexCell origin = riverOrigins[index];
            riverOrigins[index] = riverOrigins[lastIndex];
            riverOrigins.RemoveAt(lastIndex);

            if (!origin.HasRiver)
            {
                bool isValidOrigin = true;
                for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                {
                    HexCell neighbor = origin.GetNeighbor(d);
                    if (neighbor && (neighbor.HasRiver || neighbor.IsUnderwater))
                    {
                        isValidOrigin = false;
                        break;
                    }
                }
                if (isValidOrigin)
                {
                    riverBudget -= CreateRiver(origin);
                }
            }
        }

        if (riverBudget > 0)
        {
            Debug.LogWarning("Failed to use up river budget.");
        }

        ListPool<HexCell>.Add(riverOrigins);
    }

    private int CreateRiver(HexCell origin)
    {
        int length = 1;
        HexCell cell = origin;
        HexDirection direction = HexDirection.NE;
        while (!cell.IsUnderwater)
        {
            int minNeighborElevation = int.MaxValue;
            flowDirections.Clear();
            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                HexCell neighbor = cell.GetNeighbor(d);
                if (!neighbor)
                {
                    continue;
                }

                if (neighbor.Elevation < minNeighborElevation)
                {
                    minNeighborElevation = neighbor.Elevation;
                }

                if (neighbor == origin || neighbor.HasIncomingRiver)
                {
                    continue;
                }

                int delta = neighbor.Elevation - cell.Elevation;
                if (delta > 0)
                {
                    // Rivers don't flow uphill
                    continue;
                }

                if (neighbor.HasOutgoingRiver)
                {
                    cell.SetOutgoingRiver(d);
                    return length;
                }

                if (delta < 0)
                {
                    // Strongly prefer downhill directions
                    flowDirections.Add(d);
                    flowDirections.Add(d);
                    flowDirections.Add(d);
                }

                if (length == 1 || (d != direction.Next2() && d != direction.Previous2()))
                {
                    flowDirections.Add(d);
                }

                flowDirections.Add(d);
            }

            if (flowDirections.Count == 0)
            {
                if (length == 1)
                {
                    return 0;
                }

                if (minNeighborElevation >= cell.Elevation)
                {
                    cell.WaterLevel = minNeighborElevation;
                    if (minNeighborElevation == cell.Elevation)
                    {
                        cell.Elevation = minNeighborElevation - 1;
                    }
                }
                break;
            }

            direction = flowDirections[Random.Range(0, flowDirections.Count)];
            cell.SetOutgoingRiver(direction);
            length += 1;

            if (minNeighborElevation >= cell.Elevation && Random.value < extraLakeProbability)
            {
                cell.WaterLevel = cell.Elevation;
                cell.Elevation -= 1;
            }

            cell = cell.GetNeighbor(direction);
        }
        return length;
    }

    private void SetTerrainType()
    {
        temperatureJitterChannel = Random.Range(0, 4);
        int rockDesertElevation = elevationMaximum - (elevationMaximum - waterLevel) / 2;

        for (int i = 0; i < cellCount; i++)
        {
            HexCell cell = grid.GetCell(i);
            ClimateData tempData = climate[i];
            float temperature = DetermineTemperature(cell);
            float moisture = tempData.moisture;

            tempData.temperature = temperature;
            climate[i] = tempData;

            if (!cell.IsUnderwater)
            {
                int t = 0;
                for (; t < temperatureBands.Length; t++)
                {
                    if (temperature < temperatureBands[t])
                    {
                        break;
                    }
                }

                int m = 0;
                for (; m < moistureBands.Length; m++)
                {
                    if (moisture < moistureBands[m])
                    {
                        break;
                    }
                }

                Biome cellBiome = biomes[t * 4 + m];

                // Tweak terrain a bit
                if (cellBiome.terrain == 0)
                {
                    if (cell.Elevation >= rockDesertElevation)
                    {
                        // Higher deserts become rock
                        cellBiome.terrain = 3;
                    }
                }
                else if (cell.Elevation == elevationMaximum)
                {
                    // Highest elevation is always snow-covered
                    cellBiome.terrain = 4;
                }

                // Tweak plants a bit
                if (cellBiome.terrain == 4)
                {
                    // No plants in snow
                    cellBiome.plant = 0;
                }
                else if (cellBiome.plant < 3 && cell.HasRiver)
                {
                    // Increase plant levels along rivers
                    cellBiome.plant += 1;
                }

                cell.TerrainTypeIndex = cellBiome.terrain; 
                cell.PlantLevel = cellBiome.plant;
            }
            else
            {
                int terrain;
                if (cell.Elevation == waterLevel - 1)
                {
                    // Shallow water
                    int cliffs = 0, slopes = 0;
                    for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                    {
                        HexCell neighbor = cell.GetNeighbor(d);
                        if (!neighbor)
                        {
                            continue;
                        }
                        int delta = neighbor.Elevation - cell.WaterLevel;
                        if (delta == 0)
                        {
                            slopes += 1;
                        }
                        else if (delta > 0)
                        {
                            cliffs += 1;
                        }
                    }

                    if (cliffs + slopes > 3)
                    {
                        // Either lake or inlet
                        terrain = 1;
                    }
                    else if (cliffs > 0)
                    {
                        // Cliffs means rock
                        terrain = 3;
                    }
                    else if (slopes > 0)
                    {
                        // Slopes means beach
                        terrain = 0;
                    }
                    else
                    {
                        terrain = 1;
                    }
                }
                else if (cell.Elevation >= waterLevel)
                {
                    // Lakes created by rivers
                    terrain = 1;
                }
                else if (cell.Elevation < 0)
                {
                    // Deep water
                    terrain = 3;
                }
                else
                {
                    // Mud
                    terrain = 2;
                }

                if (terrain == 1 && temperature < temperatureBands[0])
                {
                    // No grass in regions with lowest temperature
                    terrain = 2;
                }

                cell.TerrainTypeIndex = terrain;
            }

            cell.SetMapData(moisture);
        }
    }

    float DetermineTemperature(HexCell cell)
    {
        float latitude = (float)cell.coordinates.Z / grid.cellCountZ;

        if (hemisphere == HemisphereMode.Both)
        {
            latitude *= 2f;
            if (latitude > 1f)
            {
                latitude = 2f - latitude;
            }
        }
        else if (hemisphere == HemisphereMode.North)
        {
            latitude = 1f - latitude;
        }

        float temperature = Mathf.LerpUnclamped(lowTemperature, highTemperature, latitude);
        temperature *= 1f - (cell.ViewElevation - waterLevel) / (elevationMaximum - waterLevel + 1f);
        float jitter = HexMetrics.SampleNoise(cell.Position * 0.1f)[temperatureJitterChannel];
        temperature += (jitter * 2f - 1f) * temperatureJitter;

        return temperature;
    }

    public void SetOverlayMoisture()
    {
        for (int i = 0; i < cellCount; i++)
        {
            HexCell cell = grid.GetCell(i);
            cell.SetMapData(climate[i].moisture);
        }
    }

    public void SetOverlayTemperature()
	{
        for (int i = 0; i < cellCount; i++)
        {
            HexCell cell = grid.GetCell(i);
            cell.SetMapData(climate[i].temperature);
        }
    }

    public void SetOverlayRiverOrigin()
	{
        for (int i = 0; i < cellCount; i++)
        {
            HexCell cell = grid.GetCell(i);
            float data = climate[i].moisture * (cell.Elevation - waterLevel) / (elevationMaximum - waterLevel);

            if (data > 0.75f)
            {
                cell.SetMapData(1f);
            }
            else if (data > 0.5f)
            {
                cell.SetMapData(0.5f);
            }
            else if (data > 0.25f)
            {
                cell.SetMapData(0.25f);
            }
            else
            {
                cell.SetMapData(0f);
            }
        }
    }

    private HexCell GetRandomCell(MapRegion region)
    {
        return grid.GetCell(Random.Range(region.xMin, region.xMax), Random.Range(region.zMin, region.zMax));
    }
}
