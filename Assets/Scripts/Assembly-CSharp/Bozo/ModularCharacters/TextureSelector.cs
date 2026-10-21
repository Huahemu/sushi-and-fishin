using UnityEngine;
using UnityEngine.UI;

namespace Bozo.ModularCharacters
{
	public class TextureSelector : MonoBehaviour
	{
		public Image icon;

		private TexturePackage texture;

		private CharacterCreator characterCreator;

		private Button button;

		private Image selectionBorder;

		private static readonly Color SelectedBorderColor = new Color(1f, 0.72f, 0.2f, 1f);

		private static readonly Color UnselectedBorderColor = new Color(1f, 1f, 1f, 0.28f);

		public void Init(TexturePackage texture, CharacterCreator characterCreator)
		{
			button = GetComponentInChildren<Button>();
			button.onClick.AddListener(OnSelect);
			this.texture = texture;
			this.characterCreator = characterCreator;
			icon.overrideSprite = texture.icon;
			selectionBorder = FindSelectionBorder();
			SetSelected(selected: false);
		}

		private void OnSelect()
		{
			characterCreator.RecordUndoState();
			if (texture.type == TextureType.Decal)
			{
				characterCreator.decalController.SetDecal(texture.texture);
			}
			if (texture.type == TextureType.Pattern)
			{
				string currentCatagory = characterCreator.GetCurrentCatagory();
				Outfit outfit = characterCreator.character.GetOutfit(currentCatagory);
				if (outfit != null)
				{
					outfit.SetPattern(texture.texture);
				}
			}
			characterCreator.RefreshCustomizationControls();
		}

		public void RefreshSelection(Outfit currentOutfit, Texture currentDecal)
		{
			Texture texture = ((this.texture.type != TextureType.Pattern) ? currentDecal : currentOutfit?.GetPattern());
			bool selected = texture != null && this.texture.texture != null && ((object)texture == this.texture.texture || texture.name == this.texture.texture.name);
			SetSelected(selected);
		}

		private void SetSelected(bool selected)
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

		public void SetVisable(string type)
		{
			if (texture.catagory == type)
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
