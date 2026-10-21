using UnityEngine;

namespace ZakhanSpellsPack
{
	public class CreateProjectile : MonoBehaviour
	{
		public Rigidbody Fireball;

		public float Time = 1f;

		private bool CreateInstances = true;

		private Rigidbody Instance;

		private void Start()
		{
			InvokeRepeating("Create", Time, Time);
		}

		private void Update()
		{
			if (Instance == null)
			{
				CreateInstances = true;
			}
		}

		private void Create()
		{
			if (CreateInstances)
			{
				Instance = Object.Instantiate(Fireball, base.transform.position, base.transform.rotation);
				CreateInstances = false;
				Instance.transform.SetParent(base.transform);
			}
		}
	}
}
