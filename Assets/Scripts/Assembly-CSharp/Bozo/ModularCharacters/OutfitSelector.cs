using UnityEngine;
using UnityEngine.UI;

namespace Bozo.ModularCharacters
{
	public class OutfitSelector : MonoBehaviour
	{
		public Image icon;

		public Outfit outfit;

		private CharacterCreator characterCreator;

		private Button button;

		private Image selectionBorder;

		private static readonly Color SelectedBorderColor = new Color(1f, 0.72f, 0.2f, 1f);

		private static readonly Color UnselectedBorderColor = new Color(1f, 1f, 1f, 0.28f);

		public void Init(Outfit outfit, CharacterCreator characterCreator)
		{
			button = GetComponentInChildren<Button>();
			button.onClick.AddListener(OnSelect);
			this.outfit = outfit;
			this.characterCreator = characterCreator;
			icon.overrideSprite = this.outfit.OutfitIcon;
			selectionBorder = FindSelectionBorder();
			SetSelected(selected: false);
		}

		public void SetSelected(bool selected)
		{
			if (selectionBorder == null)
			{
				selectionBorder = FindSelectionBorder();
			}
			if (selectionBorder != null)
			{
				selectionBorder.color = (selected ? SelectedBorderColor : UnselectedBorderColor);
			}
		}

		private Image FindSelectionBorder()
		{
			Image[] componentsInChildren = GetComponentsInChildren<Image>(includeInactive: true);
			foreach (Image image in componentsInChildren)
			{
				if (image != null && image.gameObject.name == "Boarder")
				{
					return image;
				}
			}
			return null;
		}

		private void OnSelect()
		{
			characterCreator.RecordUndoState();
			characterCreator.SetOutfit(outfit);
		}

		public void SetVisable(string type)
		{
			if (outfit.Type == null)
			{
				base.gameObject.SetActive(value: false);
				Debug.LogWarning(outfit.name + " is missing an outfitType and will not show in the character creator");
			}
			else if (outfit.Type.name == type)
			{
				base.gameObject.SetActive(value: true);
			}
			else
			{
				base.gameObject.SetActive(value: false);
			}
		}
	}
}
