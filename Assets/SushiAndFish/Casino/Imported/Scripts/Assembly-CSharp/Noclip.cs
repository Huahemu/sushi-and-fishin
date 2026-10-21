namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::Mirror;
using global::UnityEngine;

public class Noclip : NetworkBehaviour
{
	[Header("Noclip Settings")]
	[SerializeField]
	private float noclipSpeed = 600f;

	[SerializeField]
	private float noclipSprintSpeed = 1000f;

	[SerializeField]
	private float smoothness = 0.3f;

	private bool _isNoclipActive;

	private PlayerController _pc;

	private Rigidbody _rb;

	private Transform _head;

	private readonly List<Collider> _colliders = new List<Collider>();

	private Vector2 _horizontalInput;

	private Vector3 _currentVelocity;

	private void Awake()
	{
		_pc = GetComponent<PlayerController>();
		_rb = GetComponent<Rigidbody>();
		_head = _pc.head.transform;
		Collider[] componentsInChildren = GetComponentsInChildren<Collider>();
		foreach (Collider item in componentsInChildren)
		{
			_colliders.Add(item);
		}
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		if (!base.isLocalPlayer)
		{
			base.enabled = false;
		}
	}

	private void OnEnable()
	{
		InputEvents.OnMoveEvent = (Action<Vector2>)Delegate.Combine(InputEvents.OnMoveEvent, new Action<Vector2>(OnMove));
	}

	private void OnDisable()
	{
		InputEvents.OnMoveEvent = (Action<Vector2>)Delegate.Remove(InputEvents.OnMoveEvent, new Action<Vector2>(OnMove));
	}

	private void OnMove(Vector2 input)
	{
		_horizontalInput = input;
	}

	private void Update()
	{
		Move();
	}

	private void Move()
	{
		if (_isNoclipActive)
		{
			int num = 0;
			if (InputEvents.IsJumpPressed)
			{
				num++;
			}
			if (InputEvents.IsCrouchPressed)
			{
				num--;
			}
			Vector3 normalized = Vector3.ProjectOnPlane(_head.transform.forward, Vector3.up).normalized;
			Vector3 normalized2 = Vector3.ProjectOnPlane(_head.transform.right, Vector3.up).normalized;
			Vector3 normalized3 = (normalized * _horizontalInput.y + normalized2 * _horizontalInput.x + Vector3.up * num).normalized;
			float num2 = (InputEvents.IsSprintPressed ? noclipSprintSpeed : noclipSpeed);
			Vector3 vector = Vector3.SmoothDamp(Vector3.zero, normalized3, ref _currentVelocity, smoothness);
			base.transform.position += vector * (num2 * Time.unscaledDeltaTime);
		}
	}

	public void ToggleNoclip()
	{
		SetNoclipActive(!_isNoclipActive);
	}

	private void SetNoclipActive(bool active)
	{
		_pc.enabled = !active;
		_rb.isKinematic = active;
		_rb.interpolation = ((!active && base.isLocalPlayer) ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None);
		_isNoclipActive = active;
	}

	private void SetCollidersActive(bool active)
	{
		foreach (Collider collider in _colliders)
		{
			collider.enabled = active;
		}
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
