using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class HexUnit : MonoBehaviour
{
	const float travelSpeed = 4f;
	const float rotationSpeed = 180f;
	const int visionRange = 3;

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
				Grid.DecreaseVisibility(location, visionRange);
				location.Unit = null;
			}
			location = value;
			value.Unit = this;
			Grid.IncreaseVisibility(value, visionRange);
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

	public string Name { get; } = "Dummy Unit";
	public int MovementPoints { get; } = 24;
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
				Grid.IncreaseVisibility(location, visionRange);
				Grid.DecreaseVisibility(currentTravelLocation, visionRange);
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
		return !cell.IsUnderwater && !cell.Unit;
	}

	/// <summary>
	/// Causes the unit to die and be removed from the playing field.
	/// </summary>
	public void Die()
	{
		if (location)
		{
			Grid.DecreaseVisibility(location, visionRange);
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
		Grid.DecreaseVisibility(currentTravelLocation ? currentTravelLocation : pathToTravel[0], visionRange);

		float t = Time.deltaTime * travelSpeed;		// Don't wait for next frame, start moving immediately
		for (int i = 1; i < pathToTravel.Count; i++)
		{
			currentTravelLocation = pathToTravel[i];
			a = c;
			b = pathToTravel[i - 1].Position;
			c = (b + currentTravelLocation.Position) * 0.5f;
			Grid.IncreaseVisibility(pathToTravel[i], visionRange);

			for (; t < 1f; t += Time.deltaTime * travelSpeed)
			{
				transform.localPosition = Bezier.GetPoint(a, b, c, t);
				Vector3 d = Bezier.GetDerivative(a, b, c, t);
				d.y = 0f;
				transform.localRotation = Quaternion.LookRotation(d);
				yield return null;
			}

			Grid.DecreaseVisibility(pathToTravel[i], visionRange);
			t -= 1f;
		}

		currentTravelLocation = null;
		a = c;
		b = location.Position;
		c = b;
		Grid.IncreaseVisibility(location, visionRange);

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