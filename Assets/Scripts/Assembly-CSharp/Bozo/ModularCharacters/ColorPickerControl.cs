using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Bozo.ModularCharacters
{
	public class ColorPickerControl : MonoBehaviour
	{
		private class OutfitPickerSettings
		{
			public bool maintainColors;

			public int copyIndex;
		}

		[Header("Picker Dependencies")]
		public CharacterCreator creator;

		public SVImageControl svContoller;

		public GameObject visual;

		[Header("Picker")]
		public float currentHue;

		public float currentSat;

		public float currentVal;

		public float currentColor;

		[SerializeField]
		private RawImage hueImage;

		[SerializeField]
		private RawImage satValImage;

		[SerializeField]
		private RawImage outputImage;

		[SerializeField]
		private Slider hueSlider;

		private Texture2D hueTexture;

		private Texture2D svTexture;

		private Texture2D outputTexture;

		public Outfit colorObject;

		public OutfitType outfitType;

		public Material colorMaterial;

		public int MaterialSlot;

		[Header("Editor")]
		[SerializeField]
		private OutfitSystem outfitSystem;

		[SerializeField]
		private TMP_Text objectName;

		[SerializeField]
		private TMP_Text channelText;

		[SerializeField]
		private Image Swatch;

		[SerializeField]
		private TMP_Text CopyCatagoryText;

		public List<string> outfitTypes = new List<string>();

		private int copyIndex;

		[SerializeField]
		private TextureType mode;

		[SerializeField]
		private int currentChannel;

		[SerializeField]
		private int maxChannel;

		[SerializeField]
		private string[] channelNames;

		[Header("Decal Editor")]
		[SerializeField]
		private DecalController decalController;

		[SerializeField]
		private GameObject decalButton;

		[Header("Pattern Editor")]
		[SerializeField]
		private GameObject patternContainer;

		[SerializeField]
		private GameObject patternButton;

		[SerializeField]
		private Slider patternXSlider;

		[SerializeField]
		private Slider patternYSlider;

		[Header("Swatch Editor")]
		[SerializeField]
		private GameObject swatchParentContainer;

		[SerializeField]
		private Transform swatchContainer;

		[SerializeField]
		private SwatchSelector swatchSelectorObject;

		private List<SwatchSelector> swatchSelectors = new List<SwatchSelector>();

		[Header("AdvancedEditor")]
		[SerializeField]
		private TMP_Text hexValueTex;

		[SerializeField]
		private List<Color> HeldColors = new List<Color>();

		[SerializeField]
		private bool maintainColors;

		[SerializeField]
		private Toggle maintainColorsToggle;

		[SerializeField]
		private TMP_InputField inputR;

		[SerializeField]
		private TMP_InputField inputG;

		[SerializeField]
		private TMP_InputField inputB;

		[SerializeField]
		private TMP_InputField inputH;

		[SerializeField]
		private TMP_InputField inputS;

		[SerializeField]
		private TMP_InputField inputV;

		private Dictionary<OutfitType, OutfitPickerSettings> outfitPickerSettings = new Dictionary<OutfitType, OutfitPickerSettings>();

		private bool suppressHistoryCapture;

		private void Awake()
		{
			CreateHueImage();
			CreateSVImage();
			CreateOutputImage();
			UpdateOutputImage();
			OutfitType[] array = creator.outfitTypes;
			foreach (OutfitType outfitType in array)
			{
				outfitTypes.Add(outfitType.name);
			}
			CopyCatagoryText.text = outfitTypes[0];
			if (colorObject == null)
			{
				visual.SetActive(value: false);
			}
		}

		private void OnEnable()
		{
			CharacterCreator characterCreator = creator;
			characterCreator.onCategoryChanged = (UnityAction<string>)Delegate.Combine(characterCreator.onCategoryChanged, new UnityAction<string>(ChangeObject));
			CharacterCreator characterCreator2 = creator;
			characterCreator2.onCharacterChanged = (UnityAction<GameObject>)Delegate.Combine(characterCreator2.onCharacterChanged, new UnityAction<GameObject>(SetCharacter));
		}

		private void OnDisable()
		{
			CharacterCreator characterCreator = creator;
			characterCreator.onCategoryChanged = (UnityAction<string>)Delegate.Remove(characterCreator.onCategoryChanged, new UnityAction<string>(ChangeObject));
			CharacterCreator characterCreator2 = creator;
			characterCreator2.onCharacterChanged = (UnityAction<GameObject>)Delegate.Remove(characterCreator2.onCharacterChanged, new UnityAction<GameObject>(SetCharacter));
		}

		private void SetCharacter(GameObject character)
		{
			if (!(character == null))
			{
				OutfitSystem component = character.GetComponent<OutfitSystem>();
				if (!(component == null))
				{
					outfitSystem = component;
				}
			}
		}

		private void CreateHueImage()
		{
			hueTexture = new Texture2D(1, 16);
			hueTexture.wrapMode = TextureWrapMode.Clamp;
			hueTexture.name = "HueTexture";
			for (int i = 0; i < hueTexture.height; i++)
			{
				hueTexture.SetPixel(0, i, Color.HSVToRGB((float)i / (float)hueTexture.height, 1f, 1f));
			}
			hueTexture.Apply();
			currentHue = 0f;
			hueImage.texture = hueTexture;
		}

		private void CreateSVImage()
		{
			svTexture = new Texture2D(16, 16);
			svTexture.wrapMode = TextureWrapMode.Clamp;
			svTexture.name = "SVTexture";
			for (int i = 0; i < svTexture.height; i++)
			{
				for (int j = 0; j < svTexture.width; j++)
				{
					svTexture.SetPixel(j, i, Color.HSVToRGB(currentHue, (float)j / (float)svTexture.width, (float)i / (float)svTexture.height));
				}
			}
			svTexture.Apply();
			currentSat = 0f;
			currentVal = 0f;
			satValImage.texture = svTexture;
		}

		private void CreateOutputImage()
		{
			outputTexture = new Texture2D(1, 16);
			outputTexture.wrapMode = TextureWrapMode.Clamp;
			outputTexture.name = "OutputTexture";
			Color color = Color.HSVToRGB(currentHue, currentSat, currentVal);
			for (int i = 0; i < hueTexture.height; i++)
			{
				outputTexture.SetPixel(0, 1, color);
			}
			outputTexture.Apply();
			currentHue = 0f;
			outputImage.texture = outputTexture;
		}

		private void UpdateOutputImage()
		{
			Color color = Color.HSVToRGB(currentHue, currentSat, currentVal);
			for (int i = 0; i < outputTexture.height; i++)
			{
				outputTexture.SetPixel(0, i, color);
			}
			inputR.text = ((int)(color.r * 255f)).ToString();
			inputG.text = ((int)(color.g * 255f)).ToString();
			inputB.text = ((int)(color.b * 255f)).ToString();
			inputH.text = currentHue.ToString("F2");
			inputS.text = currentSat.ToString("F2");
			inputV.text = currentVal.ToString("F2");
			string text = ColorUtility.ToHtmlStringRGB(color);
			hexValueTex.text = "#" + text;
			outputTexture.Apply();
			if ((bool)colorObject)
			{
				Swatch.color = color;
				if (!suppressHistoryCapture)
				{
					creator?.RecordContinuousUndoState();
				}
				SetColor(color, currentChannel);
				channelText.color = new Color(1f - currentVal, 1f - currentVal, 1f - currentVal, 1f);
				OutfitPickerSettings outfitPickerSettings = this.outfitPickerSettings[colorObject.Type];
				SetMaintainColors(outfitPickerSettings.maintainColors);
				svContoller.setPickerPosition(currentSat, currentVal);
			}
		}

		public void SetSV(float S, float V)
		{
			currentSat = S;
			currentVal = V;
			UpdateOutputImage();
		}

		public void SetHSV(float H, float S, float V)
		{
			currentHue = H;
			currentSat = S;
			currentVal = V;
			UpdateOutputImage();
		}

		public void SetHSV()
		{
			float.TryParse(inputH.text, out currentHue);
			float.TryParse(inputS.text, out currentSat);
			float.TryParse(inputV.text, out currentVal);
			UpdateOutputImage();
		}

		public void SetRGB()
		{
			byte result = 0;
			byte.TryParse(inputR.text, out result);
			byte result2 = 0;
			byte.TryParse(inputG.text, out result2);
			byte result3 = 0;
			byte.TryParse(inputB.text, out result3);
			Color.RGBToHSV(new Color32(result, result2, result3, byte.MaxValue), out var H, out var S, out var V);
			SetHSV(H, S, V);
		}

		public void UpdateSVImage()
		{
			currentHue = hueSlider.value;
			for (int i = 0; i < svTexture.height; i++)
			{
				for (int j = 0; j < svTexture.width; j++)
				{
					svTexture.SetPixel(j, i, Color.HSVToRGB(currentHue, (float)j / (float)svTexture.width, (float)i / (float)svTexture.height));
				}
			}
			svTexture.Apply();
			UpdateOutputImage();
		}

		private void SetColor(Color color, int channel)
		{
			switch (mode)
			{
			case TextureType.Base:
				colorObject.SetColor(color, channel);
				break;
			case TextureType.Decal:
				maxChannel = 3;
				decalController.SetColor(color);
				break;
			case TextureType.Pattern:
				maxChannel = 3;
				colorObject.SetPatternColor(color, channel);
				break;
			}
		}

		public void SetPattern(Texture texture, Color[] colors, Vector2 maxScale)
		{
			creator?.RecordUndoState();
			SwitchMode("Pattern");
			colorObject.SetPattern(texture);
			for (int i = 0; i < colors.Length; i++)
			{
				SetColor(colors[i], i + 1);
			}
			if (maxScale.x < 0f && (bool)patternXSlider)
			{
				patternXSlider.gameObject.SetActive(value: false);
				patternXSlider.maxValue = 1f;
				patternXSlider.SetValueWithoutNotify(1f);
			}
			else
			{
				patternXSlider.gameObject.SetActive(value: true);
				patternXSlider.maxValue = maxScale.x;
				patternXSlider.SetValueWithoutNotify(1f);
			}
			if (maxScale.y < 0f && (bool)patternYSlider)
			{
				patternYSlider.gameObject.SetActive(value: false);
				patternYSlider.maxValue = 1f;
				patternYSlider.SetValueWithoutNotify(1f);
			}
			else
			{
				patternYSlider.gameObject.SetActive(value: true);
				patternYSlider.maxValue = maxScale.y;
				patternYSlider.SetValueWithoutNotify(1f);
			}
			SetPatternSize();
		}

		public void ChangeSwatch(int value)
		{
			currentChannel += value;
			if (currentChannel > maxChannel)
			{
				currentChannel = 1;
			}
			if (currentChannel < 1)
			{
				currentChannel = maxChannel;
			}
			Color color = Color.black;
			switch (mode)
			{
			case TextureType.Base:
				color = colorObject.GetColor(currentChannel);
				break;
			case TextureType.Decal:
				color = colorObject.GetDecalColor(currentChannel);
				break;
			case TextureType.Pattern:
				color = colorObject.GetPatternColor(currentChannel);
				break;
			}
			Swatch.color = color;
			SetChannelName();
			Color.RGBToHSV(color, out var H, out var S, out var V);
			hueSlider.SetValueWithoutNotify(H);
			SetHSV(H, S, V);
			UpdateSVImage();
		}

		public void ChangeObject(string type)
		{
			if (!(outfitSystem == null))
			{
				Outfit outfit = outfitSystem.GetOutfit(type);
				ChangeObject(outfit);
			}
		}

		public void ChangeObject(Outfit colorOutfit)
		{
			if (!colorOutfit)
			{
				RemoveObject();
				return;
			}
			suppressHistoryCapture = true;
			if (maintainColors && !colorOutfit.customShader && colorObject != null && colorOutfit.Type == outfitType)
			{
				for (int i = 0; i < HeldColors.Count; i++)
				{
					colorOutfit.SetColor(HeldColors[i], i + 1);
				}
			}
			channelNames = new string[1] { "Base" };
			outfitType = colorOutfit.Type;
			mode = TextureType.Base;
			colorObject = colorOutfit;
			if (!this.outfitPickerSettings.ContainsKey(colorOutfit.Type))
			{
				OutfitPickerSettings value = new OutfitPickerSettings();
				this.outfitPickerSettings.Add(colorOutfit.Type, value);
			}
			OutfitPickerSettings outfitPickerSettings = this.outfitPickerSettings[colorOutfit.Type];
			SetMaintainColors(outfitPickerSettings.maintainColors);
			SetCopyIndex(outfitPickerSettings.copyIndex);
			Color color = colorObject.GetColor(1);
			color.a = 1f;
			currentChannel = 1;
			Swatch.color = color;
			channelNames = colorOutfit.ColorChannels;
			if ((bool)colorOutfit)
			{
				maxChannel = colorOutfit.ColorChannels.Length;
			}
			else
			{
				maxChannel = 9;
			}
			Vector4 patternSize = colorObject.GetPatternSize();
			patternXSlider.SetValueWithoutNotify(patternSize.x);
			patternYSlider.SetValueWithoutNotify(patternSize.y);
			SetChannelName();
			ChangeSwatch(0);
			objectName.text = colorObject.OutfitName;
			decalButton.SetActive(colorObject.supportDecals);
			patternButton.SetActive(colorObject.supportPatterns);
			decalController.gameObject.SetActive(value: false);
			patternContainer.gameObject.SetActive(value: false);
			visual.SetActive(value: true);
			suppressHistoryCapture = false;
		}

		public void SetPatternSize()
		{
			if (!suppressHistoryCapture)
			{
				creator?.RecordContinuousUndoState();
			}
			Vector2 patternSize = new Vector2(patternXSlider.value, patternYSlider.value);
			colorObject.SetPatternSize(patternSize);
		}

		public void RemoveObject()
		{
			colorObject = null;
			SetMaintainColors(value: false);
			visual.SetActive(value: false);
		}

		public void SetBaseTexture(int textureIndex)
		{
			creator?.RecordUndoState();
			colorObject.GetComponent<Outfit>().SetSwatch(textureIndex);
		}

		public void SwitchMode(string mode)
		{
			TextureType textureType = (TextureType)Enum.Parse(typeof(TextureType), mode);
			this.mode = textureType;
			switch (this.mode)
			{
			case TextureType.Base:
				ChangeObject(colorObject);
				break;
			case TextureType.Decal:
			{
				maxChannel = 1;
				channelText.text = "Decal";
				Color color = decalController.color;
				color.a = 1f;
				currentChannel = 1;
				Swatch.color = color;
				break;
			}
			case TextureType.Pattern:
			{
				maxChannel = 3;
				channelText.text = "Pattern 1/" + maxChannel;
				Color patternColor = colorObject.GetPatternColor(1);
				patternColor.a = 1f;
				currentChannel = 1;
				Swatch.color = patternColor;
				break;
			}
			}
		}

		public void ChangeCopyIndex(int value)
		{
			copyIndex += value;
			if (copyIndex > outfitTypes.Count - 1)
			{
				copyIndex = 0;
			}
			else if (copyIndex < 0)
			{
				copyIndex = outfitTypes.Count - 1;
			}
			CopyCatagoryText.text = outfitTypes[copyIndex];
			if (outfitType != null)
			{
				outfitPickerSettings[outfitType].copyIndex = copyIndex;
			}
		}

		public void SetCopyIndex(int value)
		{
			copyIndex = value;
			CopyCatagoryText.text = outfitTypes[copyIndex];
			outfitPickerSettings[outfitType].copyIndex = copyIndex;
		}

		public void CopyColor(Outfit copyOutfit)
		{
			creator?.RecordUndoState();
			Outfit outfit = colorObject;
			for (int i = 1; i < 9; i++)
			{
				copyOutfit.SetColor(outfit.GetColor(i), i);
			}
		}

		public void CopyColor()
		{
			creator?.RecordUndoState();
			Outfit outfit = colorObject;
			Outfit outfit2 = outfitSystem.GetOutfit(CopyCatagoryText.text);
			if (!(outfit2 == null))
			{
				for (int i = 1; i < 9; i++)
				{
					outfit2.SetColor(outfit.GetColor(i), i);
				}
			}
		}

		public void SetColorByHex(string hex)
		{
			if (hex[0].ToString() != "#")
			{
				hex = "#" + hex;
			}
			if (ColorUtility.TryParseHtmlString(hex, out var color))
			{
				MonoBehaviour.print(color);
				Color.RGBToHSV(color, out var H, out var S, out var V);
				SetHSV(H, S, V);
			}
			else
			{
				Debug.LogWarning("Invalid hex string!");
			}
		}

		public void CopyHex()
		{
			GUIUtility.systemCopyBuffer = hexValueTex.text;
			MonoBehaviour.print("Copied HEX to Clipboard: " + hexValueTex.text);
		}

		public void SetChannelName()
		{
			string text = "";
			switch (mode)
			{
			case TextureType.Base:
				if (!colorObject.customShader && currentChannel - 1 < channelNames.Length)
				{
					text = channelNames[currentChannel - 1];
				}
				break;
			case TextureType.Decal:
				text = "Decal";
				break;
			case TextureType.Pattern:
				text = "Pattern";
				break;
			}
			channelText.text = text + " " + currentChannel + "/" + maxChannel;
		}

		public void SetMaintainColors(bool value)
		{
			if (value && colorObject != null)
			{
				HeldColors = colorObject.GetColors();
			}
			maintainColors = value;
			maintainColorsToggle.SetIsOnWithoutNotify(maintainColors);
			if (outfitType != null)
			{
				outfitPickerSettings[outfitType].maintainColors = maintainColors;
			}
		}
	}
}
