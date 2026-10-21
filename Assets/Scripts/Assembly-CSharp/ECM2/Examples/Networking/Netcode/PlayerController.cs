using Unity.Netcode;
using UnityEngine;

namespace ECM2.Examples.Networking.Netcode
{
	public class PlayerController : NetworkBehaviour
	{
		private Character _character;

		private void Awake()
		{
			_character = GetComponent<Character>();
		}

		private void Update()
		{
			if (base.IsOwner)
			{
				Vector2 vector = new Vector2
				{
					x = Input.GetAxisRaw("Horizontal"),
					y = Input.GetAxisRaw("Vertical")
				};
				Vector3 zero = Vector3.zero;
				zero += Vector3.forward * vector.y;
				zero += Vector3.right * vector.x;
				_character.SetMovementDirection(zero);
				if (Input.GetButtonDown("Jump"))
				{
					_character.Jump();
				}
				else if (Input.GetButtonUp("Jump"))
				{
					_character.StopJumping();
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
			return "PlayerController";
		}
	}
}
