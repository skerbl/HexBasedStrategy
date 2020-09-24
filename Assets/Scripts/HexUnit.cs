using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class HexUnit : MonoBehaviour
{
	const float travelSpeed = 4f;
	const float rotationSpeed = 180f;

	public static HexUnit unitPrefab;

	private List<HexCell> pathToTravel;

	public HexCell Location
	{
		get
		{
			return location;
		}
		set
		{
			if (location)
			{
				Grid.DecreaseVisibility(location, VisionRange);
				location.Unit = null;
			}
			location = value;
			value.Unit = this;
			Grid.IncreaseVisibility(value, VisionRange);
			transform.localPosition = value.Position;
		}
	}

	public float Orientation
	{
		get
		{
			return orientation;
		}
		set
		{
			orientation = value;
			transform.localRotation = Quaternion.Euler(0f, value, 0f);
		}
	}

	/// <summary>
	/// The movement speed of the unit. For now, this is fixed to 24.
	/// </summary>
	public int Speed
	{
		get
		{
			return 24;
		}
	}

	public int VisionRange 
	{ 
		get 
		{
			return 3;
		} 
	}

	public string Name { get; } = "Dummy Unit";
	public HexGrid Grid { get; set; }

	private float orientation;
	private HexCell location;
	private HexCell currentTravelLocation;

	void OnEnable()
	{
		// Recover from a recompile in editor play mode
		if (location)
		{
			transform.localPosition = location.Position;
			if (currentTravelLocation)
			{
				Grid.IncreaseVisibility(location, VisionRange);
				Grid.DecreaseVisibility(currentTravelLocation, VisionRange);
				currentTravelLocation = null;
			}
		}
	}

	public void ValidateLocation()
	{
		transform.localPosition = location.Position;
	}

	/// <summary>
	/// Validates a move order. For now, this blocks movement to underwater 
	/// cells and cells occupied by another unit. The rules for this will
	/// most likely change with the introduction of certain unit types (e.g. 
	/// flying, aquatic, or amphibious units.
	/// </summary>
	/// <param name="cell">The destination cell</param>
	/// <returns>Validity</returns>
	public bool IsValidDestination(HexCell cell)
	{
		// TODO: Units of the same faction might be allowed to move past each other
		// TODO: Find a good way to allow "non-clairvoyant" pathfinding into unexplored territory
		if (Grid.moveToUnexploredTerritory)
		{
			return !cell.IsUnderwater && !cell.Unit;
		}
		else
		{
			return cell.IsExplored && !cell.IsUnderwater && !cell.Unit;
		}
	}

	/// <summary>
	/// Determines the movement cost for this unit to move from one cell to another.
	/// </summary>
	/// <param name="fromCell">The cell the unit is coming from</param>
	/// <param name="toCell">The cell the unit is moving to</param>
	/// <param name="direction">The direction of the movement</param>
	/// <returns>The movement cost, or -1 if movement is impossible.</returns>
	public int GetMoveCost(HexCell fromCell, HexCell toCell, HexDirection direction)
	{
		// TODO: Find a clean way to implement different movement rules (e.g. flying, aquatic, amphibious, etc.)
		HexEdgeType edgeType = fromCell.GetEdgeType(toCell);
		if (edgeType == HexEdgeType.Cliff)
		{
			return -1;
		}

		int moveCost;
		if (fromCell.HasRoadThroughEdge(direction))
		{
			moveCost = 1;
		}
		else if (fromCell.Walled != toCell.Walled)
		{
			return -1;
		}
		else
		{
			moveCost = edgeType == HexEdgeType.Flat ? 5 : 10;

			// Features without roads slow down movement.
			moveCost += toCell.UrbanLevel + toCell.FarmLevel + toCell.PlantLevel;
		}

		return moveCost;
	}

	/// <summary>
	/// Causes the unit to die and be removed from the playing field.
	/// </summary>
	public void Die()
	{
		if (location)
		{
			Grid.DecreaseVisibility(location, VisionRange);
		}
		location.Unit = null;
		Destroy(gameObject);
	}

	/// <summary>
	/// Causes the unit to move along a path.
	/// </summary>
	/// <param name="path">The path</param>
	public void Travel(List<HexCell> path)
	{
		location.Unit = null;
		location = path[path.Count - 1];
		location.Unit = this;
		pathToTravel = path;
		StopAllCoroutines();
		StartCoroutine(TravelPath());
	}

	IEnumerator TravelPath()
	{
		Vector3 a, b, c = pathToTravel[0].Position;
		yield return LookAt(pathToTravel[1].Position);
		Grid.DecreaseVisibility(currentTravelLocation ? currentTravelLocation : pathToTravel[0], VisionRange);

		float t = Time.deltaTime * travelSpeed;		// Don't wait for next frame, start moving immediately
		for (int i = 1; i < pathToTravel.Count; i++)
		{
			currentTravelLocation = pathToTravel[i];
			a = c;
			b = pathToTravel[i - 1].Position;
			c = (b + currentTravelLocation.Position) * 0.5f;
			Grid.IncreaseVisibility(pathToTravel[i], VisionRange);

			for (; t < 1f; t += Time.deltaTime * travelSpeed)
			{
				transform.localPosition = Bezier.GetPoint(a, b, c, t);
				Vector3 d = Bezier.GetDerivative(a, b, c, t);
				d.y = 0f;
				transform.localRotation = Quaternion.LookRotation(d);
				yield return null;
			}

			Grid.DecreaseVisibility(pathToTravel[i], VisionRange);
			t -= 1f;
		}

		currentTravelLocation = null;
		a = c;
		b = location.Position;
		c = b;
		Grid.IncreaseVisibility(location, VisionRange);

		for (; t < 1f; t += Time.deltaTime * travelSpeed)
		{
			transform.localPosition = Bezier.GetPoint(a, b, c, t);
			Vector3 d = Bezier.GetDerivative(a, b, c, t);
			d.y = 0f;
			transform.localRotation = Quaternion.LookRotation(d);
			yield return null;
		}

		transform.localPosition = location.Position;
		orientation = transform.localRotation.eulerAngles.y;

		ListPool<HexCell>.Add(pathToTravel);
		pathToTravel = null;
	}

	IEnumerator LookAt(Vector3 point)
	{
		point.y = transform.localPosition.y;

		Quaternion fromRotation = transform.localRotation;
		Quaternion toRotation = Quaternion.LookRotation(point - transform.localPosition);
		float angle = Quaternion.Angle(fromRotation, toRotation);

		if (angle > 0f)
		{
			float speed = rotationSpeed / angle;
			for (float t = Time.deltaTime * speed; t < 1f; t += Time.deltaTime * speed)
			{
				transform.localRotation = Quaternion.Slerp(fromRotation, toRotation, t);
				yield return null;
			}
		}

		transform.LookAt(point);
		orientation = transform.localRotation.eulerAngles.y;
	}

	public void Save(BinaryWriter writer)
	{
		location.coordinates.Save(writer);
		writer.Write(orientation);
	}

	public static void Load(BinaryReader reader, HexGrid grid)
	{
		HexCoordinates coordinates = HexCoordinates.Load(reader);
		float orientation = reader.ReadSingle();
		grid.AddUnit(Instantiate(unitPrefab), grid.GetCell(coordinates), orientation);
	}
}