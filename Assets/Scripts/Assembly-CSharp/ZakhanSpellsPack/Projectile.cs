using UnityEngine;

namespace ZakhanSpellsPack
{
	public class Projectile : MonoBehaviour
	{
		public GameObject ExplosionPrefab;

		public float DestroyExplosion = 4f;

		public float DestroyChildren = 2f;

		public Vector2 Velocity;

		private Rigidbody rb;

		private void Start()
		{
			rb = base.gameObject.GetComponent<Rigidbody>();
			rb.linearVelocity = Velocity;
		}

		private void OnCollisionEnter(Collision col)
		{
			Object.Destroy(Object.Instantiate(ExplosionPrefab, base.transform.position, ExplosionPrefab.transform.rotation), DestroyExplosion);
			Transform child = base.transform.GetChild(0);
			base.transform.DetachChildren();
			Object.Destroy(child.gameObject, DestroyChildren);
			Object.Destroy(base.gameObject);
		}
	}
}
