using CableSystem;
using UnityEngine;

namespace CableSystemCutter
{
	public class Cutter : MonoBehaviour
	{
		public GameObject LogSegment;

		private void OnCollisionEnter(Collision collision)
		{
			if (collision.gameObject.name.Contains("CableSegment"))
			{
				LogSegment = collision.gameObject;
				collision.transform.parent.GetComponent<Cable>().CutCable(collision.gameObject);
			}
		}
	}
}
