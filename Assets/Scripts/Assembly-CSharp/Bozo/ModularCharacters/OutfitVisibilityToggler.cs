using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bozo.ModularCharacters
{
	public class OutfitVisibilityToggler : MonoBehaviour
	{
		private Outfit outfit;

		private CharacterCreator characterCreator;

		[SerializeField]
		private GameObject toggle;

		private List<GameObject> toggles = new List<GameObject>();

		private GameObject[] pieces;

		public void Set(Outfit outfit)
		{
			Set(outfit, null);
		}

		public void Set(Outfit outfit, CharacterCreator characterCreator)
		{
			this.outfit = outfit;
			this.characterCreator = characterCreator;
			foreach (GameObject toggle in toggles)
			{
				Object.Destroy(toggle);
			}
			toggles.Clear();
			pieces = new GameObject[0];
			if (outfit == null)
			{
				base.gameObject.SetActive(value: false);
				return;
			}
			if (outfit.optionalPieces.Length == 0)
			{
				base.gameObject.SetActive(value: false);
				return;
			}
			base.gameObject.SetActive(value: true);
			pieces = outfit.optionalPieces;
			for (int i = 0; i < outfit.optionalPieces.Length; i++)
			{
				if (!(outfit.optionalPieces[i] == null))
				{
					_ = outfit.optionalPieces[i];
					GameObject gameObject = Object.Instantiate(this.toggle, base.transform);
					toggles.Add(gameObject);
					gameObject.GetComponentInChildren<TMP_Text>().text = outfit.optionalPieces[i].name;
					Toggle componentInChildren = gameObject.GetComponentInChildren<Toggle>();
					componentInChildren.SetIsOnWithoutNotify(outfit.optionalPieces[i].activeSelf);
					int index = i;
					componentInChildren.onValueChanged.AddListener((bool isOn) =>
					{
						onToggle(index, isOn);
					});
				}
			}
		}

		private void onToggle(int index, bool value)
		{
			characterCreator?.RecordUndoState();
			outfit.SetPartActive(index, value);
		}
	}
}
