using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Bozo.ModularCharacters
{
	public class DecalController : MonoBehaviour
	{
		public CharacterCreator characterCreator;

		public OutfitSystem system;

		public DecalBaker decalBaker;

		public TMP_Dropdown anchorSelect;

		public TMP_Text indexText;

		public Slider XSlider;

		public Slider YSlider;

		public Slider ZSlider;

		public Slider YawSlider;

		public Slider RollSlider;

		public Slider pitchSlider;

		public Slider ScaleSlider;

		public Texture decal;

		public Color color = Color.white;

		public int DecalIndex;

		public int DecalIndexMax = 1;

		private Outfit outfit;

		private void OnEnable()
		{
			CharacterCreator obj = characterCreator;
			obj.onCategoryChanged = (UnityAction<string>)Delegate.Combine(obj.onCategoryChanged, new UnityAction<string>(Init));
			CharacterCreator obj2 = characterCreator;
			obj2.onCharacterChanged = (UnityAction<GameObject>)Delegate.Combine(obj2.onCharacterChanged, new UnityAction<GameObject>(SetCharacter));
			if (!(characterCreator.type == null))
			{
				Init(characterCreator.type.name);
			}
		}

		private void OnDisable()
		{
			CharacterCreator obj = characterCreator;
			obj.onCategoryChanged = (UnityAction<string>)Delegate.Remove(obj.onCategoryChanged, new UnityAction<string>(Init));
			CharacterCreator obj2 = characterCreator;
			obj2.onCharacterChanged = (UnityAction<GameObject>)Delegate.Remove(obj2.onCharacterChanged, new UnityAction<GameObject>(SetCharacter));
		}

		private void SetCharacter(GameObject character)
		{
			if (!(character == null))
			{
				OutfitSystem component = character.GetComponent<OutfitSystem>();
				if (!(component == null))
				{
					system = component;
				}
			}
		}

		private void Init(string type)
		{
			outfit = characterCreator.character.GetOutfit(type);
			if (!outfit)
			{
				return;
			}
			if (outfit.decalDatas.Count == 0)
			{
				decal = null;
				DecalIndexMax = 0;
				indexText.text = "-/-";
				return;
			}
			DecalIndexMax = outfit.decalDatas.Count;
			DecalIndex = Mathf.Clamp(DecalIndex, 0, DecalIndexMax - 1);
			indexText.text = DecalIndex + 1 + "/" + DecalIndexMax;
			DecalData decalData = outfit.decalDatas[DecalIndex];
			color = decalData.color;
			string boneToFind = decalData.parent;
			int num = anchorSelect.options.FindIndex((TMP_Dropdown.OptionData option) => option.text == boneToFind);
			if (num != -1)
			{
				anchorSelect.SetValueWithoutNotify(num);
				anchorSelect.RefreshShownValue();
			}
			XSlider.value = decalData.pos.x;
			YSlider.value = decalData.pos.y;
			ZSlider.value = decalData.pos.z;
			YawSlider.value = decalData.yaw;
			RollSlider.value = decalData.roll;
			pitchSlider.value = decalData.pitch;
			ScaleSlider.value = decalData.scale;
		}

		public void RefreshFromCharacter()
		{
			if (!(characterCreator == null) && !string.IsNullOrEmpty(characterCreator.GetCurrentCatagory()))
			{
				Init(characterCreator.GetCurrentCatagory());
			}
		}

		public void SetDecal(Texture texture)
		{
			if (!(outfit == null))
			{
				decal = texture;
				UpdateDecal();
			}
		}

		public void UpdateDecal()
		{
			if (!(decal == null))
			{
				float value = XSlider.value;
				float value2 = YSlider.value;
				float value3 = ZSlider.value;
				string text = anchorSelect.options[anchorSelect.value].text;
				DecalData data = new DecalData(decal.name, text, new Vector3(value, value2, value3), YawSlider.value, RollSlider.value, pitchSlider.value, ScaleSlider.value, color);
				data.color.a = 1f;
				data.decalTexture = decal;
				outfit.SetDecal(data, DecalIndex);
			}
		}

		public void SetColor(Color color)
		{
			this.color = color;
			UpdateDecal();
		}

		public void AddDecal()
		{
			outfit.AddDecal();
			DecalIndexMax++;
			DecalIndex = Mathf.Clamp(DecalIndex, 0, DecalIndexMax - 1);
			indexText.text = DecalIndex + 1 + "/" + DecalIndexMax;
		}

		public void RemoveDecal()
		{
			outfit.RemoveDecal();
			DecalIndexMax--;
			DecalIndex = Mathf.Clamp(DecalIndex, 0, DecalIndexMax - 1);
			indexText.text = DecalIndex + 1 + "/" + DecalIndexMax;
		}

		public void IndexUp()
		{
			DecalIndex++;
			DecalIndex = Mathf.Clamp(DecalIndex, 0, DecalIndexMax - 1);
			indexText.text = DecalIndex + 1 + "/" + DecalIndexMax;
			Init(outfit.Type.name);
		}

		public void IndexDown()
		{
			DecalIndex--;
			DecalIndex = Mathf.Clamp(DecalIndex, 0, DecalIndexMax - 1);
			indexText.text = DecalIndex + 1 + "/" + DecalIndexMax;
			Init(outfit.Type.name);
		}
	}
}
