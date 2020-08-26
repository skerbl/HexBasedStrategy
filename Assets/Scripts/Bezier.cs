using UnityEngine;

public static class Bezier
{
	/// <summary>
	/// Gets a point on a quadratic Bezier curve:
	/// (1−t)^2 A +2 * (1−t) * t * B + t^2 * C
	/// </summary>
	/// <param name="a">Control point A</param>
	/// <param name="b">Control point B</param>
	/// <param name="c">Control point C</param>
	/// <param name="t">Interpolator</param>
	/// <returns></returns>
	public static Vector3 GetPoint(Vector3 a, Vector3 b, Vector3 c, float t)
	{
		float r = 1f - t;
		return r * r * a + 2f * r * t * b + t * t * c;
	}

	/// <summary>
	/// Gets the derivative of a point on a Bezier curve.
	/// Derivative vector corresponds with the travel direction along the curve.
	/// </summary>
	/// <param name="a">Control point A</param>
	/// <param name="b">Control point B</param>
	/// <param name="c">Control point C</param>
	/// <param name="t">Interpolator. Must not be zero!</param>
	/// <returns></returns>
	public static Vector3 GetDerivative(Vector3 a, Vector3 b, Vector3 c, float t)
	{
		return 2f * ((1f - t) * (b - a) + t * (c - b));
	}
}