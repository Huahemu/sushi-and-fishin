using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Bozo.ModularCharacters
{
	public class OutfitHideByTag : MonoBehaviour, IOutfitExtension
	{
		[Serializable]
		private struct HideSettings
		{
			public SkinnedMeshRenderer renderer;

			public string tag;
		}

		private OutfitSystem system;

		[SerializeField]
		private HideSettings[] settings;

		public void Execute(OutfitSystem outfitSystem, Outfit outfit)
		{
		}

		public string GetID()
		{
			return "OutfitHideByTag";
		}

		public object GetValue()
		{
			return null;
		}

		public Type GetValueType()
		{
			return null;
		}

		public void Initalize(OutfitSystem outfitSystem, Outfit outfit)
		{
			system = GetComponentInParent<OutfitSystem>(includeInactive: true);
			OutfitSystem outfitSystem2 = system;
			outfitSystem2.OnTagsChanged = (UnityAction<List<string>>)Delegate.Combine(outfitSystem2.OnTagsChanged, new UnityAction<List<string>>(SetHide));
		}

		public void OnDestroy()
		{
			if ((bool)system)
			{
				OutfitSystem outfitSystem = system;
				outfitSystem.OnTagsChanged = (UnityAction<List<string>>)Delegate.Remove(outfitSystem.OnTagsChanged, new UnityAction<List<string>>(SetHide));
			}
		}

		private void SetHide(List<string> tags)
		{
			HideSettings[] array = settings;
			for (int i = 0; i < array.Length; i++)
			{
				HideSettings hideSettings = array[i];
				if (!(hideSettings.renderer == null))
				{
					if (system.ContainsTag(hideSettings.tag))
					{
						hideSettings.renderer.gameObject.SetActive(value: false);
					}
					else
					{
						hideSettings.renderer.gameObject.SetActive(value: true);
					}
				}
			}
		}
	}
}
