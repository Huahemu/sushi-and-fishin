namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

public class Rotator : MonoBehaviour
{
	public enum Axis
	{
		X = 0,
		Y = 1,
		Z = 2
	}

	public float speed = 50f;

	public Axis axis = Axis.Y;

	private Vector3 _axisVector;

	private void Start()
	{
		_axisVector = ((axis == Axis.X) ? Vector3.right : ((axis == Axis.Y) ? Vector3.up : Vector3.forward));
	}

	private void Update()
	{
		base.transform.Rotate(_axisVector * (speed * Time.deltaTime), Space.Self);
	}
}

}
