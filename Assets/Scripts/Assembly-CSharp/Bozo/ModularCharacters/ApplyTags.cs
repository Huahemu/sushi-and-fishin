using UnityEngine;

namespace Bozo.ModularCharacters
{
	public class ApplyTags : MonoBehaviour
	{
		public string[] tags;

		private OutfitSystem system;

		private Outfit parentOutfit;

		private void Awake()
		{
			system = GetComponentInParent<OutfitSystem>();
			parentOutfit = GetComponentInParent<Outfit>();
		}

		private void OnEnable()
		{
			_ = (bool)system;
		}

		private void OnDisable()
		{
			_ = (bool)system;
		}
	}
}
