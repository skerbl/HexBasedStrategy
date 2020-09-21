using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public class HexGrid : MonoBehaviour
{
	public int cellCountX = 20;
	public int cellCountZ = 15;

	[SerializeField]
	HexCell cellPrefab = default;

	[SerializeField]
	Text cellLabelPrefab = default;

	[SerializeField]
	HexGridChunk chunkPrefab = default;

	[SerializeField]
	HexUnit unitPrefab = default;

	[SerializeField]
	Texture2D noiseSource = default;

	public int seed;

	private HexCell[] cells;
	private HexGridChunk[] chunks;
	private int chunkCountX;
	private int chunkCountZ;

	private HexCellPriorityQueue searchFrontier;
	private int searchFrontierPhase;
	private HexCell currentPathFrom;
	private HexCell currentPathTo;
	private bool currentPathExists;

	private HexCellShaderData cellShaderData;

	// TODO: Move this somewhere else. 
	private List<HexUnit> units = new List<HexUnit>();

	public bool HasPath
	{
		get
		{
			return currentPathExists;
		}
	}

	void Awake()
	{
		HexMetrics.noiseSource = noiseSource;
		HexMetrics.InitializeHashGrid(seed);
		HexUnit.unitPrefab = unitPrefab;

		cellShaderData = gameObject.AddComponent<HexCellShaderData>();

		CreateMap(cellCountX, cellCountZ);
	}

	void OnEnable()
	{
		if (!HexMetrics.noiseSource)
		{
			HexMetrics.noiseSource = noiseSource;
			HexMetrics.InitializeHashGrid(seed);
			HexUnit.unitPrefab = unitPrefab;
		}
	}

	public bool CreateMap(int x, int z)
	{
		if (x <= 0 || x % HexMetrics.chunkSizeX != 0 ||
			z <= 0 || z % HexMetrics.chunkSizeZ != 0)
		{
			Debug.LogError("Unsupported map size.");
			return false;
		}

		ClearPath();
		ClearUnits();

		if (chunks != null)
		{
			for (int i = 0; i < chunks.Length; i++)
			{
				Destroy(chunks[i].gameObject);
			}
		}

		cellCountX = x;
		cellCountZ = z;
		chunkCountX = cellCountX / HexMetrics.chunkSizeX;
		chunkCountZ = cellCountZ / HexMetrics.chunkSizeZ;

		cellShaderData.Initialize(cellCountX, cellCountZ);
		CreateChunks();
		CreateCells();

		return true;
	}

		void CreateChunks()
	{
		chunks = new HexGridChunk[chunkCountX * chunkCountZ];

		for (int z = 0, i = 0; z < chunkCountZ; z++)
		{
			for (int x = 0; x < chunkCountX; x++)
			{
				HexGridChunk chunk = chunks[i++] = Instantiate(chunkPrefab);
				chunk.transform.SetParent(transform);
			}
		}
	}

	void CreateCells()
	{
		cells = new HexCell[cellCountZ * cellCountX];
		for (int z = 0, i = 0; z < cellCountZ; z++)
		{
			for (int x = 0; x < cellCountX; x++)
			{
				CreateCell(x, z, i++);
			}
		}
	}

	void CreateCell(int x, int z, int i)
	{
		Vector3 position;
		position.x = (x + z * 0.5f - z / 2) * (HexMetrics.innerRadius * 2f);
		position.y = 0f;
		position.z = z * (HexMetrics.outerRadius * 1.5f);

		HexCell cell = cells[i] = Instantiate<HexCell>(cellPrefab);
		cell.transform.localPosition = position;
		cell.coordinates = HexCoordinates.FromOffsetCoordinates(x, z);
		cell.Index = i;
		cell.ShaderData = cellShaderData;

		if (x > 0)
		{
			cell.SetNeighbor(HexDirection.W, cells[i - 1]);
		}
		if (z > 0)
		{
			if ((z & 1) == 0)	// Even rows
			{
				cell.SetNeighbor(HexDirection.SE, cells[i - cellCountX]);
				if (x > 0)
				{
					cell.SetNeighbor(HexDirection.SW, cells[i - cellCountX - 1]);
				}
			}
			else
			{
				cell.SetNeighbor(HexDirection.SW, cells[i - cellCountX]);
				if (x < cellCountX - 1)
				{
					cell.SetNeighbor(HexDirection.SE, cells[i - cellCountX + 1]);
				}
			}
		}

		Text label = Instantiate<Text>(cellLabelPrefab);
		label.rectTransform.anchoredPosition = new Vector2(position.x, position.z);

		cell.UiRect = label.rectTransform;
		cell.Elevation = 0;

		AddCellToChunk(x, z, cell);
	}

	void AddCellToChunk(int x, int z, HexCell cell)
	{
		int chunkX = x / HexMetrics.chunkSizeX;
		int chunkZ = z / HexMetrics.chunkSizeZ;
		HexGridChunk chunk = chunks[chunkX + chunkZ * chunkCountX];

		int localX = x - chunkX * HexMetrics.chunkSizeX;
		int localZ = z - chunkZ * HexMetrics.chunkSizeZ;
		chunk.AddCell(localX + localZ * HexMetrics.chunkSizeX, cell);
	}

	public void ShowUI(bool visible)
	{
		for (int i = 0; i < chunks?.Length; i++)
		{
			chunks[i].ShowUI(visible);
		}
	}

	public HexCell GetCell(Ray ray)
	{
		RaycastHit hit;
		if (Physics.Raycast(ray, out hit))
		{
			return GetCell(hit.point);
		}
		return null;
	}

	public HexCell GetCell(Vector3 position)
	{
		position = transform.InverseTransformPoint(position);
		HexCoordinates coordinates = HexCoordinates.FromPosition(position);
		int index = coordinates.X + coordinates.Z * cellCountX + coordinates.Z / 2;
		return cells[index];
	}

	public HexCell GetCell(HexCoordinates coordinates)
	{
		int z = coordinates.Z;
		if (z < 0 || z >= cellCountZ)
		{
			return null;
		}

		int x = coordinates.X + z / 2;
		if (x < 0 || x >= cellCountX)
		{
			return null;
		}

		return cells[x + z * cellCountX];
	}

	public List<HexCell> GetPath()
	{
		if (!currentPathExists)
		{
			return null;
		}

		List<HexCell> path = ListPool<HexCell>.Get();
		for (HexCell c = currentPathTo; c != currentPathFrom; c = c.PathFrom)
		{
			path.Add(c);
		}
		path.Add(currentPathFrom);
		path.Reverse();

		return path;
	}

	/// <summary>
	/// Initiates a search for the shortest path between two cells.
	/// </summary>
	/// <param name="fromCell">The starting cell</param>
	/// <param name="toCell">The destination cell</param>
	/// <param name="speed">The unit's movement speed. Since the default movement cost is 5, a number *not* divisible by 5 should be preferred.</param>
	public void FindPath(HexCell fromCell, HexCell toCell, int speed)
	{
		ClearPath();
		currentPathFrom = fromCell;
		currentPathTo = toCell;
		currentPathExists = Search(fromCell, toCell, speed);
		if (currentPathExists)
		{
			ShowPath(speed);
		}
	}

	/// <summary>
	/// Reconstruct the shortest path by stepping through it backwards.
	/// Shows labels along the path, displaying the required turns
	/// </summary>
	/// <param name="speed">The movement speed of the unit</param>
	void ShowPath(int speed)
	{
		if (currentPathExists)
		{
			HexCell current = currentPathTo;
			while (current != currentPathFrom)
			{
				int turn = (current.Distance - 1) / speed;
				current.SetLabel(turn.ToString());
				current.EnableHighlight(Color.white);
				current = current.PathFrom;
			}
		}
		currentPathFrom.EnableHighlight(Color.blue);
		currentPathTo.EnableHighlight(Color.red);
	}

	public void ClearPath()
	{
		if (currentPathExists)
		{
			HexCell current = currentPathTo;
			while (current != currentPathFrom)
			{
				current.SetLabel(null);
				current.DisableHighlight();
				current = current.PathFrom;
			}
			current.DisableHighlight();
			currentPathExists = false;
		}
		else if (currentPathFrom)
		{
			currentPathFrom.DisableHighlight();
			currentPathTo.DisableHighlight();
		}
		currentPathFrom = currentPathTo = null;
	}

	/// <summary>
	/// Searches for the optimal (shortest) path between two cells. Uses the A* algorithm
	/// for pathfinding, taking into account varying movement costs depending on terrain.
	/// Displays the number of turns required along the found path.
	/// </summary>
	/// <param name="fromCell">The starting cell</param>
	/// <param name="toCell">The destination cell</param>
	/// <param name="speed">The unit's movement speed. Since the default movement cost is 5, a number *not* divisible by 5 should be preferred.</param>
	/// <returns>Success or failure</returns>
	bool Search(HexCell fromCell, HexCell toCell, int speed)
	{
		// Ensure that new search frontier is always larger than the previous one
		searchFrontierPhase += 2;

		if (searchFrontier == null)
		{
			searchFrontier = new HexCellPriorityQueue();
		}
		else
		{
			searchFrontier.Clear();
		}

		fromCell.SearchPhase = searchFrontierPhase;
		fromCell.Distance = 0;
		searchFrontier.Enqueue(fromCell);

		while (searchFrontier.Count > 0)
		{
			// Cells taken out of the frontier will have a larger phase than the existing 
			// frontier, but smaller than the fontier of the next search will have.
			HexCell current = searchFrontier.Dequeue();
			current.SearchPhase += 1;

			if (current == toCell)
			{
				// Found the shortest path
				return true;
			}

			int currentTurn = (current.Distance - 1) / speed;

			for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
			{
				HexCell neighbor = current.GetNeighbor(d);

				// Cells that were already taken out of the frontier will be skipped
				if (neighbor == null || neighbor.SearchPhase > searchFrontierPhase)
				{
					continue;
				}
				if (neighbor.IsUnderwater || neighbor.Unit)		// Units of the same faction might be allowed to move past each other
				{
					continue;
				}
				HexEdgeType edgeType = current.GetEdgeType(neighbor);
				if (edgeType == HexEdgeType.Cliff)
				{
					continue;
				}

				// TODO: Find a clean way to implement different movement rules (e.g. flying, aquatic, amphibious, etc.)

				int moveCost;
				if (current.HasRoadThroughEdge(d))
				{
					moveCost = 1;
				}
				else if (current.Walled != neighbor.Walled)
				{
					continue;
				}
				else
				{
					moveCost = edgeType == HexEdgeType.Flat ? 5 : 10;

					// Features without roads slow down movement.
					moveCost += neighbor.UrbanLevel + neighbor.FarmLevel + neighbor.PlantLevel;
				}

				// This will discard leftover movement points and add them to the total distance.
				// The most efficient path will waste as few points as possible.
				int distance = current.Distance + moveCost;
				int turn = (current.Distance - 1) / speed;
				if (turn > currentTurn)
				{
					distance = turn * speed + moveCost;
				}

				if (neighbor.SearchPhase < searchFrontierPhase)
				{
					neighbor.SearchPhase = searchFrontierPhase;
					neighbor.Distance = distance;
					neighbor.PathFrom = current;
					neighbor.SearchHeuristic = neighbor.coordinates.DistanceTo(toCell.coordinates);
					searchFrontier.Enqueue(neighbor);
				}
				else if (distance < neighbor.Distance)
				{
					int oldPriority = neighbor.SearchPriority;
					neighbor.Distance = distance;
					neighbor.PathFrom = current;
					searchFrontier.Change(neighbor, oldPriority);
				}
			}
		}

		return false;
	}

	/// <summary>
	/// Finds all cells that are visible from the starting cell.
	/// Doesn't use search heuristics, so effectively it's Dijkstra's Shortest Path.
	/// </summary>
	/// <param name="fromCell">The starting cell for the visibility search</param>
	/// <param name="range">The vision range (could be a unit, a spell, or a fixed installation).</param>
	/// <returns>A list of visible cells.</returns>
	List<HexCell> GetVisibleCells(HexCell fromCell, int range)
	{
		List<HexCell> visibleCells = ListPool<HexCell>.Get();

		// Ensure that new search frontier is always larger than the previous one
		searchFrontierPhase += 2;

		if (searchFrontier == null)
		{
			searchFrontier = new HexCellPriorityQueue();
		}
		else
		{
			searchFrontier.Clear();
		}

		fromCell.SearchPhase = searchFrontierPhase;
		fromCell.Distance = 0;
		searchFrontier.Enqueue(fromCell);

		while (searchFrontier.Count > 0)
		{
			// Cells taken out of the frontier will have a larger phase than the existing 
			// frontier, but smaller than the fontier of the next search will have.
			HexCell current = searchFrontier.Dequeue();
			current.SearchPhase += 1;
			visibleCells.Add(current);

			for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
			{
				HexCell neighbor = current.GetNeighbor(d);

				// Cells that were already taken out of the frontier will be skipped
				if (neighbor == null || neighbor.SearchPhase > searchFrontierPhase)
				{
					continue;
				}

				int distance = current.Distance + 1;
				if (distance > range)
				{
					// Skip all cells that exceed the vision range
					continue;
				}

				if (neighbor.SearchPhase < searchFrontierPhase)
				{
					neighbor.SearchPhase = searchFrontierPhase;
					neighbor.Distance = distance;
					neighbor.SearchHeuristic = 0;
					searchFrontier.Enqueue(neighbor);
				}
				else if (distance < neighbor.Distance)
				{
					int oldPriority = neighbor.SearchPriority;
					neighbor.Distance = distance;
					searchFrontier.Change(neighbor, oldPriority);
				}
			}
		}

		return visibleCells;
	}

	/// <summary>
	/// Increases the visibility in all cells around a center.
	/// </summary>
	/// <param name="fromCell">The center cell of the visible area.</param>
	/// <param name="range">The radius of the visible area.</param>
	public void IncreaseVisibility(HexCell fromCell, int range)
	{
		List<HexCell> cells = GetVisibleCells(fromCell, range);
		for (int i = 0; i < cells.Count; i++)
		{
			cells[i].IncreaseVisibility();
		}
		ListPool<HexCell>.Add(cells);
	}

	/// <summary>
	/// Decreases the visibility in all cells around a center.
	/// </summary>
	/// <param name="fromCell">The center cell of the visible area.</param>
	/// <param name="range">The radius of the visible area.</param>
	public void DecreaseVisibility(HexCell fromCell, int range)
	{
		List<HexCell> cells = GetVisibleCells(fromCell, range);
		for (int i = 0; i < cells.Count; i++)
		{
			cells[i].DecreaseVisibility();
		}
		ListPool<HexCell>.Add(cells);
	}

	void ClearUnits()
	{
		for (int i = 0; i < units.Count; i++)
		{
			units[i].Die();
		}
		units.Clear();
	}

	public void AddUnit(HexUnit unit, HexCell location, float orientation)
	{
		units.Add(unit);
		unit.Grid = this;
		unit.transform.SetParent(transform, false);
		unit.Location = location;
		unit.Orientation = orientation;
	}

	public void RemoveUnit(HexUnit unit)
	{
		units.Remove(unit);
		unit.Die();
	}

	public void Save(BinaryWriter writer)
	{
		writer.Write(cellCountX);
		writer.Write(cellCountZ);

		for (int i = 0; i < cells.Length; i++)
		{
			cells[i].Save(writer);
		}

		writer.Write(units.Count);
		for (int i = 0; i < units.Count; i++)
		{
			units[i].Save(writer);
		}
	}

	public void Load(BinaryReader reader, int header)
	{
		ClearPath();
		ClearUnits();

		int x = 20, z = 15;
		if (header >= 1)
		{
			x = reader.ReadInt32();
			z = reader.ReadInt32();
		}

		if (x != cellCountX || z != cellCountZ)
		{
			if (!CreateMap(x, z))
			{
				return;
			}
		}

		for (int i = 0; i < cells.Length; i++)
		{
			cells[i].Load(reader, header);
		}

		for (int i = 0; i < chunks.Length; i++)
		{
			chunks[i].Refresh();
		}

		if (header >= 2)
		{
			int unitCount = reader.ReadInt32();
			for (int i = 0; i < unitCount; i++)
			{
				HexUnit.Load(reader, this);
			}
		}
	}

	
}