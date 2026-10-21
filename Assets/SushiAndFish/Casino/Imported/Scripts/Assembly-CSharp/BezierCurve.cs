namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

public class BezierCurve
{
	public Vector3 StartPoint;

	public Vector3 EndPoint;

	public Vector3 BendOffset;

	public BezierCurve(Vector3 startPoint, Vector3 endPoint, Vector3 bendOffset)
	{
		StartPoint = startPoint;
		EndPoint = endPoint;
		BendOffset = bendOffset;
	}

	public Vector3 GetPoint(float t)
	{
		Vector3 vector = Vector3.Lerp(StartPoint, EndPoint, 0.5f) + BendOffset;
		Vector3 a = Vector3.Lerp(StartPoint, vector, t);
		Vector3 b = Vector3.Lerp(vector, EndPoint, t);
		return Vector3.Lerp(a, b, t);
	}

	public Vector3 GetDirection(float t)
	{
		Vector3 vector = Vector3.Lerp(StartPoint, EndPoint, 0.5f) + BendOffset;
		Vector3 startPoint = StartPoint;
		Vector3 vector2 = vector;
		Vector3 endPoint = EndPoint;
		return (2f * (1f - t) * (vector2 - startPoint) + 2f * t * (endPoint - vector2)).normalized;
	}
}

}
