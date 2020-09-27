using System;
using UnityEngine;
using System.IO;
using UnityEngine.UI;

public class HexCell : MonoBehaviour
{
	public HexGridChunk chunk;
	public HexCoordinates coordinates;

	[SerializeField]
	HexCell[] neighbors = default;

	[SerializeField]
	bool[] roads = default;

	private int terrainTypeIndex;
	private int elevation = int.MinValue;
	private int waterLevel;
	private int urbanLevel;
	private int farmLevel;
	private int plantLevel;
	private bool hasIncomingRiver = false;
	private bool hasOutgoingRiver = false;
	private bool walled = false;
	private HexDirection incomingRiver;
	private HexDirection outgoingRiver;
	private int specialIndex;
	private int distance;
	private int visibility = 0;
	private bool explored;

	/// <summary>
	/// The index of this cell in the map's cell list and in the cell shader data.
	/// </summary>
	public int Index { get; set; }

	public RectTransform UiRect { get; set; }

	/// <summary>
	/// Connects the cell with the previous one in a path.
	/// Used to recontruct the path after finding the destination.
	/// </summary>
	public HexCell PathFrom { get; set; }

	/// <summary>
	/// Saves the unmodified direct distance between the cell and the destination.
	/// Used to bias searching and pathfinding towards the destination.
	/// </summary>
	public int SearchHeuristic { get; set; }

	/// <summary>
	/// Points to the next cell with the same priority, forming a linked list.
	/// </summary>
	public HexCell NextWithSamePriority { get; set; }

	/// <summary>
	/// A reference to the unit that is currently occupying the cell.
	/// </summary>
	public HexUnit Unit { get; set; }

	/// <summary>
	/// Holds data that gets passed to shaders for visual representation.
	/// </summary>
	public HexCellShaderData ShaderData { get; set; }

	/// <summary>
	/// Cells marked as not explorable will never be uncovered by a unit's vision.
	/// This is useful to hide the edges of the map from the player, where the mesh looks
	/// ugly because of missing neighbor cells.
	/// TODO: Explorable state could easily be made editable in Edit Mode. Also include it in save data in this case.
	/// </summary>
	public bool Explorable { get; set; }

	/// <summary>
	/// Keeps track of whether this cell has been explored at some point.
	/// </summary>
	public bool IsExplored
	{
		get
		{
			return explored && Explorable;
		}
		private set
		{
			explored = value;
		}
	}

	public bool IsVisible
	{
		get
		{
			return visibility > 0 && Explorable;
		}
	}

	public int SearchPriority
	{
		get
		{
			return distance + SearchHeuristic;
		}
	}

	public int ViewElevation
	{
		get
		{
			return elevation >= waterLevel ? elevation : waterLevel;
		}
	}

	public int Elevation
	{
		get
		{
			return elevation;
		}
		set
		{
			if (elevation == value)
			{
				return;
			}

			int originalViewElevation = ViewElevation;
			elevation = value;

			if (ViewElevation != originalViewElevation)
			{
				ShaderData.ViewElevationChanged();
			}

			RefreshPosition();
			ValidateRivers();

			// Remove any roads if elevetion difference becomes too high
			for (int i = 0; i < roads.Length; i++)
			{
				if (roads[i] && GetElevationDifference((HexDirection)i) > 1)
				{
					SetRoad(i, false);
				}
			}

			Refresh();
		}
	}

	public int WaterLevel
	{
		get
		{
			return waterLevel;
		}
		set
		{
			if (waterLevel == value)
			{
				return;
			}

			int originalViewElevation = ViewElevation;
			waterLevel = value;

			if (ViewElevation != originalViewElevation)
			{
				ShaderData.ViewElevationChanged();
			}

			ValidateRivers();
			Refresh();
		}
	}

	public int UrbanLevel
	{
		get
		{
			return urbanLevel;
		}
		set
		{
			if (urbanLevel != value)
			{
				urbanLevel = value;
				RefreshSelfOnly();
			}
		}
	}

	public int FarmLevel
	{
		get
		{
			return farmLevel;
		}
		set
		{
			if (farmLevel != value)
			{
				farmLevel = value;
				RefreshSelfOnly();
			}
		}
	}

	public int PlantLevel
	{
		get
		{
			return plantLevel;
		}
		set
		{
			if (plantLevel != value)
			{
				plantLevel = value;
				RefreshSelfOnly();
			}
		}
	}

	public bool Walled
	{
		get
		{
			return walled;
		}
		set
		{
			if (walled != value)
			{
				walled = value;
				Refresh();
			}
		}
	}

	public Vector3 Position
	{
		get
		{
			return transform.localPosition;
		}
	}

	public int TerrainTypeIndex
	{
		get
		{
			return terrainTypeIndex;
		}
		set
		{
			if (terrainTypeIndex != value)
			{
				terrainTypeIndex = value;
				ShaderData.RefreshTerrain(this);
			}
		}
	}

	public bool HasRiver
	{
		get
		{
			return hasIncomingRiver || hasOutgoingRiver;
		}
	}

	public bool HasRiverBeginOrEnd
	{
		get
		{
			return hasIncomingRiver != hasOutgoingRiver;
		}
	}

	public bool HasRiverThroughEdge(HexDirection direction)
	{
		return
			hasIncomingRiver && incomingRiver == direction ||
			hasOutgoingRiver && outgoingRiver == direction;
	}

	public bool HasIncomingRiver
	{
		get
		{
			return hasIncomingRiver;
		}
	}

	public bool HasOutgoingRiver
	{
		get
		{
			return hasOutgoingRiver;
		}
	}

	public HexDirection IncomingRiver
	{
		get
		{
			return incomingRiver;
		}
	}

	public HexDirection OutgoingRiver
	{
		get
		{
			return outgoingRiver;
		}
	}

	public HexDirection RiverBeginOrEndDirection
	{
		get
		{
			return hasIncomingRiver ? incomingRiver : outgoingRiver;
		}
	}

	public float StreamBedY
	{
		get
		{
			return (elevation + HexMetrics.streamBedElevationOffset) * HexMetrics.elevationStep;
		}
	}

	public float RiverSurfaceY
	{
		get
		{
			return (elevation + HexMetrics.waterElevationOffset) * HexMetrics.elevationStep;
		}
	}

	public bool HasRoads
	{
		get
		{
			for (int i = 0; i < roads.Length; i++)
			{
				if (roads[i])
				{
					return true;
				}
			}
			return false;
		}
	}

	public bool IsUnderwater
	{
		get
		{
			return waterLevel > elevation;
		}
	}

	public float WaterSurfaceY
	{
		get
		{
			return (waterLevel + HexMetrics.waterElevationOffset) *	HexMetrics.elevationStep;
		}
	}

	public int SpecialIndex
	{
		get
		{
			return specialIndex;
		}
		set
		{
			if (specialIndex != value && !HasRiver)
			{
				specialIndex = value;

				// Remove roads for now. Maybe some special features will allow roads?
				RemoveRoads();
				RefreshSelfOnly();
			}
		}
	}

	public bool IsSpecial
	{
		get
		{
			return specialIndex > 0;
		}
	}

	public int Distance
	{
		get
		{
			return distance;
		}
		set
		{
			distance = value;
		}
	}

	/// <summary>
	/// Indicates the part of the search phase this cell is in.
	/// A value of 0 indicates that the cell has not yet been visited.
	/// A value of 1 indicates that the cell is currently part of the search frontier.
	/// A value of 2 indicates that the cell has already been taken out of the frontier.
	/// </summary>
	public int SearchPhase { get; set; }

	public void SetLabel(string text)
	{
		Text label = UiRect.GetComponent<Text>();
		label.text = text;
	}

	public void DisableHighlight()
	{
		Image highlight = UiRect.GetChild(0).GetComponent<Image>();
		highlight.enabled = false;
	}

	public void EnableHighlight(Color color)
	{
		Image highlight = UiRect.GetChild(0).GetComponent<Image>();
		highlight.color = color;
		highlight.enabled = true;
	}

	public HexCell GetNeighbor(HexDirection direction)
	{
		return neighbors[(int)direction];
	}

	public void SetNeighbor(HexDirection direction, HexCell cell)
	{
		neighbors[(int)direction] = cell;
		cell.neighbors[(int)direction.Opposite()] = this;
	}

	public HexEdgeType GetEdgeType(HexDirection direction)
	{
		return HexMetrics.GetEdgeType(
			elevation, neighbors[(int)direction].elevation
		);
	}

	public HexEdgeType GetEdgeType(HexCell otherCell)
	{
		return HexMetrics.GetEdgeType(
			elevation, otherCell.elevation
		);
	}

	void RefreshPosition()
	{
		Vector3 position = transform.localPosition;
		position.y = elevation * HexMetrics.elevationStep;
		position.y +=
			(HexMetrics.SampleNoise(position).y * 2f - 1f) *
			HexMetrics.elevationPerturbStrength;
		transform.localPosition = position;

		Vector3 uiPosition = UiRect.localPosition;
		uiPosition.z = -position.y;
		UiRect.localPosition = uiPosition;
	}

	public int GetElevationDifference(HexDirection direction)
	{
		int difference = elevation - GetNeighbor(direction).elevation;
		return difference >= 0 ? difference : -difference;
	}

	public void SetOutgoingRiver(HexDirection direction)
	{
		if (hasOutgoingRiver && outgoingRiver == direction)
		{
			return;
		}

		HexCell neighbor = GetNeighbor(direction);
		if (!IsValidRiverDestination(neighbor))
		{
			return;
		}

		RemoveOutgoingRiver();
		if (hasIncomingRiver && incomingRiver == direction)
		{
			RemoveIncomingRiver();
		}
		hasOutgoingRiver = true;
		outgoingRiver = direction;

		// A river will also wash away special features
		specialIndex = 0;

		neighbor.RemoveIncomingRiver();
		neighbor.hasIncomingRiver = true;
		neighbor.incomingRiver = direction.Opposite();
		neighbor.specialIndex = 0;

		// This also refreshes the current and neighboring cell, 
		// regardless of whether a road was actually removed.
		SetRoad((int)direction, false);
	}

	public void RemoveOutgoingRiver()
	{
		if (!hasOutgoingRiver)
		{
			return;
		}
		hasOutgoingRiver = false;
		RefreshSelfOnly();

		HexCell neighbor = GetNeighbor(outgoingRiver);
		neighbor.hasIncomingRiver = false;
		neighbor.RefreshSelfOnly();
	}

	public void RemoveIncomingRiver()
	{
		if (!hasIncomingRiver)
		{
			return;
		}
		hasIncomingRiver = false;
		RefreshSelfOnly();

		HexCell neighbor = GetNeighbor(incomingRiver);
		neighbor.hasOutgoingRiver = false;
		neighbor.RefreshSelfOnly();
	}

	public void RemoveRiver()
	{
		RemoveOutgoingRiver();
		RemoveIncomingRiver();
	}

	public bool HasRoadThroughEdge(HexDirection direction)
	{
		return roads[(int)direction];
	}

	public void AddRoad(HexDirection direction)
	{
		if (
			!roads[(int)direction] && !HasRiverThroughEdge(direction) && !IsSpecial && 
			!GetNeighbor(direction).IsSpecial && GetElevationDifference(direction) <= 1
		)
		{
			SetRoad((int)direction, true);
		}
	}

	public void RemoveRoads()
	{
		for (int i = 0; i < neighbors.Length; i++)
		{
			if (roads[i])
			{
				SetRoad(i, false);
			}
		}
	}

	bool IsValidRiverDestination(HexCell neighbor)
	{
		return neighbor && (elevation >= neighbor.elevation || waterLevel == neighbor.elevation);
	}

	void SetRoad(int index, bool state)
	{
		roads[index] = state;
		neighbors[index].roads[(int)((HexDirection)index).Opposite()] = state;
		neighbors[index].RefreshSelfOnly();
		RefreshSelfOnly();
	}

	void ValidateRivers()
	{
		if (
			hasOutgoingRiver &&
			!IsValidRiverDestination(GetNeighbor(outgoingRiver))
		)
		{
			RemoveOutgoingRiver();
		}
		if (
			hasIncomingRiver &&
			!GetNeighbor(incomingRiver).IsValidRiverDestination(this)
		)
		{
			RemoveIncomingRiver();
		}
	}

	void Refresh()
	{
		if (chunk)
		{
			chunk.Refresh();
			for (int i = 0; i < neighbors.Length; i++)
			{
				HexCell neighbor = neighbors[i];
				if (neighbor != null && neighbor.chunk != chunk)
				{
					neighbor.chunk.Refresh();
				}
			}
		}

		if (Unit)
		{
			Unit.ValidateLocation();
		}
	}

	void RefreshSelfOnly()
	{
		chunk.Refresh();

		if (Unit)
		{
			Unit.ValidateLocation();
		}
	}

	public void IncreaseVisibility()
	{
		visibility += 1;
		if (visibility == 1)
		{
			IsExplored = true;
			ShaderData.RefreshVisibility(this);
		}
	}

	public void DecreaseVisibility()
	{
		visibility -= 1;
		if (visibility == 0)
		{
			ShaderData.RefreshVisibility(this);
		}
	}

	public void ResetVisibility()
	{
		if (visibility > 0)
		{
			visibility = 0;
			ShaderData.RefreshVisibility(this);
		}
	}

	public void SetMapData(float data)
	{
		ShaderData.SetMapData(this, data);
	}

	public void Save(BinaryWriter writer)
	{
		writer.Write((byte)terrainTypeIndex);
		writer.Write((byte)(elevation + 127));
		writer.Write((byte)waterLevel);
		writer.Write((byte)urbanLevel);
		writer.Write((byte)farmLevel);
		writer.Write((byte)plantLevel);
		writer.Write((byte)specialIndex);
		writer.Write(walled);

		if (hasIncomingRiver)
		{
			// Use eighth bit (128) to indicate whether a river exists
			writer.Write((byte)(incomingRiver + 128));
		}
		else
		{
			// Write zeroes if it doesn't
			writer.Write((byte)0);
		}

		if (hasOutgoingRiver)
		{
			writer.Write((byte)(outgoingRiver + 128));
		}
		else
		{
			writer.Write((byte)0);
		}

		// Represent the road booleans as a bitmask, one bit per direction
		int roadFlags = 0;
		for (int i = 0; i < roads.Length; i++)
		{
			if (roads[i])
			{
				roadFlags |= 1 << i;
			}
		}
		writer.Write((byte)roadFlags);
		writer.Write(IsExplored);
	}

	public void Load(BinaryReader reader, int header)
	{
		TerrainTypeIndex = reader.ReadByte();
		elevation = reader.ReadByte();
		if (header >= 4)
		{
			elevation -= 127;
		}

		RefreshPosition();
		waterLevel = reader.ReadByte();
		urbanLevel = reader.ReadByte();
		farmLevel = reader.ReadByte();
		plantLevel = reader.ReadByte();
		specialIndex = reader.ReadByte();
		walled = reader.ReadBoolean();

		byte riverData = reader.ReadByte();
		if (riverData >= 128)
		{
			// Subtract 128 to get the direction
			hasIncomingRiver = true;
			incomingRiver = (HexDirection)(riverData - 128);
		}
		else
		{
			hasIncomingRiver = false;
		}

		riverData = reader.ReadByte();
		if (riverData >= 128)
		{
			hasOutgoingRiver = true;
			outgoingRiver = (HexDirection)(riverData - 128);
		}
		else
		{
			hasOutgoingRiver = false;
		}

		int roadFlags = reader.ReadByte();
		for (int i = 0; i < roads.Length; i++)
		{
			// mask all other bits with bitwise AND with the corresponding number
			roads[i] = (roadFlags & (1 << i)) != 0;
		}

		IsExplored = header >= 3 ? reader.ReadBoolean() : false;
		ShaderData.RefreshVisibility(this);
	}
}