namespace SushiAndFish.Casino.Original
{
using global::Mirror;
using global::UnityEngine;

public class PrimitiveRbNetworkPlayer : NetworkBehaviour
{
	private Rigidbody _rb;

	[SerializeField]
	private int blueChips;

	[SerializeField]
	private int redChips;

	[SerializeField]
	private int greenChips;

	private void Awake()
	{
		_rb = GetComponent<Rigidbody>();
	}

	private void Start()
	{
		if (base.isLocalPlayer)
		{
			Cursor.lockState = CursorLockMode.Locked;
		}
	}

	private void Update()
	{
		if (base.isLocalPlayer)
		{
			HandleMovement();
			HandleRotation();
		}
	}

	private void HandleMovement()
	{
		float axis = Input.GetAxis("Horizontal");
		float axis2 = Input.GetAxis("Vertical");
		Vector3 direction = new Vector3(axis, 0f, axis2);
		direction = base.transform.TransformDirection(direction);
		_rb.AddForce(direction * 5f);
	}

	private void HandleRotation()
	{
		float axis = Input.GetAxis("Mouse X");
		float axis2 = Input.GetAxis("Mouse Y");
		Vector3 localEulerAngles = base.transform.localEulerAngles;
		localEulerAngles.y += axis * 2f;
		localEulerAngles.x -= axis2 * 2f;
		localEulerAngles.z = 0f;
		base.transform.localEulerAngles = localEulerAngles;
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
