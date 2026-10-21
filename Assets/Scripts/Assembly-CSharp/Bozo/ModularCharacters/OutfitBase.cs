using UnityEngine;

namespace Bozo.ModularCharacters
{
	public abstract class OutfitBase : MonoBehaviour
	{
		private void Awake()
		{
		}

		public virtual void SetSwatch(int swatchIndex, bool linkedChange = false)
		{
		}
	}
}
