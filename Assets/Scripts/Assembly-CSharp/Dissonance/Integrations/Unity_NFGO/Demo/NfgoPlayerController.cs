using Unity.Netcode;
using UnityEngine;

namespace Dissonance.Integrations.Unity_NFGO.Demo
{
	public class NfgoPlayerController : NetworkBehaviour
	{
		private CharacterController _controller;

		private Transform _transform;

		private void OnEnable()
		{
			_controller = GetComponent<CharacterController>();
			_transform = GetComponent<Transform>();
		}

		private void Update()
		{
			if (base.IsLocalPlayer && (bool)_controller)
			{
				float yAngle = Input.GetAxis("Horizontal") * Time.deltaTime * 150f;
				float num = Input.GetAxis("Vertical") * 3f;
				_transform.Rotate(0f, yAngle, 0f);
				_controller.SimpleMove(_transform.TransformDirection(Vector3.forward) * num);
				if (_transform.position.y < -3f)
				{
					_transform.position = Vector3.zero;
					_transform.rotation = Quaternion.identity;
				}
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "NfgoPlayerController";
		}
	}
}
