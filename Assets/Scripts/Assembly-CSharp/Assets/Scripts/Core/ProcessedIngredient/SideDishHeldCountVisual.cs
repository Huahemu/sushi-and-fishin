using UnityEngine;

namespace Assets.Scripts.Core.ProcessedIngredient
{
	public sealed class SideDishHeldCountVisual : MonoBehaviour
	{
		[SerializeField]
		private GameObject _normalVisual;

		[SerializeField]
		private GameObject _emptyVisual;

		public void Apply(bool isEmpty)
		{
			if (!(_normalVisual == null) && !(_emptyVisual == null))
			{
				_normalVisual.SetActive(!isEmpty);
				_emptyVisual.SetActive(isEmpty);
			}
		}
	}
}
