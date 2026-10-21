using SushiAndFish.Casino.Original;
using global::Mirror;
using global::UnityEngine;

namespace Dissonance.Integrations.MirrorIgnorance.Demo
{
	public class MirrorIgnorancePlayerController : NetworkBehaviour
	{
		private void Update()
		{
			if (base.isLocalPlayer)
			{
				CharacterController component = GetComponent<CharacterController>();
				float yAngle = Input.GetAxis("Horizontal") * Time.deltaTime * 150f;
				float num = Input.GetAxis("Vertical") * 3f;
				base.transform.Rotate(0f, yAngle, 0f);
				Vector3 vector = base.transform.TransformDirection(Vector3.forward);
				component.SimpleMove(vector * num);
				if (base.transform.position.y < -3f)
				{
					base.transform.position = Vector3.zero;
					base.transform.rotation = Quaternion.identity;
				}
			}
		}

		public override bool Weaved()
		{
			return true;
		}
	}
}
