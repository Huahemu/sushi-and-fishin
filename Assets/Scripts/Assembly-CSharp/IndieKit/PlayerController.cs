using UnityEngine;

namespace IndieKit
{
	public class PlayerController : MonoBehaviour
	{
		[Header("Camera Settings")]
		[SerializeField]
		private Transform cameraRig;

		[SerializeField]
		public float cameraRotationSpeed = 20f;

		[Header("Damage Settings")]
		[SerializeField]
		private float damageAmount = 10f;

		private void Update()
		{
			if (cameraRig != null)
			{
				cameraRig.eulerAngles = new Vector3(cameraRig.eulerAngles.x, cameraRig.eulerAngles.y - Time.deltaTime * cameraRotationSpeed, cameraRig.eulerAngles.z);
			}
			HandleFireInput();
			HandleQuitInput();
		}

		private void HandleFireInput()
		{
			if (Input.GetMouseButtonDown(0))
			{
				Vector2 vector = Input.mousePosition;
				if (Physics.Raycast(Camera.main.ScreenPointToRay(vector), out var hitInfo))
				{
					hitInfo.collider.GetComponent<IDamageable>()?.ApplyDamage(damageAmount, hitInfo.point);
				}
			}
		}

		private void HandleQuitInput()
		{
			if (Input.GetKeyDown(KeyCode.Escape))
			{
				Application.Quit();
			}
		}
	}
}
