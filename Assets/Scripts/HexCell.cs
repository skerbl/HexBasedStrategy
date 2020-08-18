using System;
using UnityEngine;
using System.IO;

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

	public RectTransform UiRect { get; set; }

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
			elevation = value;
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
			waterLevel = value;
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
				Refresh();
			}
		}
	}

	public Color Color
	{
		get
		{
			return HexMetrics.colors[terrainTypeIndex];
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
	}

	void RefreshSelfOnly()
	{
		chunk.Refresh();
	}

	public void Save(BinaryWriter writer)
	{
		writer.Write(terrainTypeIndex);
		writer.Write(elevation);
		writer.Write(waterLevel);
		writer.Write(urbanLevel);
		writer.Write(farmLevel);
		writer.Write(plantLevel);
		writer.Write(specialIndex);
		writer.Write(walled);

		writer.Write(hasIncomingRiver);
		writer.Write((int)incomingRiver);

		writer.Write(hasOutgoingRiver);
		writer.Write((int)outgoingRiver);

		for (int i = 0; i < roads.Length; i++)
		{
			writer.Write(roads[i]);
		}
	}

	public void Load(BinaryReader reader)
	{
		terrainTypeIndex = reader.ReadInt32();
		elevation = reader.ReadInt32();
		RefreshPosition();
		waterLevel = reader.ReadInt32();
		urbanLevel = reader.ReadInt32();
		farmLevel = reader.ReadInt32();
		plantLevel = reader.ReadInt32();
		specialIndex = reader.ReadInt32();
		walled = reader.ReadBoolean();

		hasIncomingRiver = reader.ReadBoolean();
		incomingRiver = (HexDirection)reader.ReadInt32();

		hasOutgoingRiver = reader.ReadBoolean();
		outgoingRiver = (HexDirection)reader.ReadInt32();

		for (int i = 0; i < roads.Length; i++)
		{
			roads[i] = reader.ReadBoolean();
		}
	}
}