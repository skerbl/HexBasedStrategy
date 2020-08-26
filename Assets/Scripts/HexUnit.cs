using UnityEngine;

public class HexUnit : MonoBehaviour
{
	public HexCell Location
	{
		get
		{
			return location;
		}
		set
		{
			location = value;
			value.Unit = this;
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

	private float orientation;
	private HexCell location;

	public void ValidateLocation()
	{
		transform.localPosition = location.Position;
	}

	public void Die()
	{
		location.Unit = null;
		Destroy(gameObject);
	}
}