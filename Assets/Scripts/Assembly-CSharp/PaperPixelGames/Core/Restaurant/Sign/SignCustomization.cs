using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Restaurant.Sign
{
	public class SignCustomization : MonoBehaviour
	{
		[Header("Controllers & UI Elements")]
		[SerializeField]
		private ColorPickerController _colorPickerController;

		[SerializeField]
		private Material _letterMaterial;

		[SerializeField]
		private GameObject _customizationObj;

		[SerializeField]
		private TMP_InputField _storeNameInputField;

		[SerializeField]
		private TextMeshProUGUI _storeNameSignText;

		[SerializeField]
		private Slider _fontSizeSlider;

		[SerializeField]
		private Slider _spacingSlider;

		[SerializeField]
		private Button _doneButton;

		[SerializeField]
		private GameObject _spacePrefab;

		[SerializeField]
		private Transform _storeSignTransform;

		public GameObject CustomizationObj => _customizationObj;

		public void Initialize()
		{
			_customizationObj.SetActive(value: false);
			SetUIListeners();
		}

		private void SetUIListeners()
		{
			_fontSizeSlider.onValueChanged.RemoveAllListeners();
			_fontSizeSlider.onValueChanged.AddListener(OnValueChangedFontSizeSlider);
			_spacingSlider.onValueChanged.RemoveAllListeners();
			_spacingSlider.onValueChanged.AddListener(OnValueChangedSpacingSlider);
		}

		private void DisplayTextStoreName(string text)
		{
			_storeNameSignText.text = text;
		}

		private void OnValueChangedFontSizeSlider(float value)
		{
			_storeNameSignText.fontSize = value;
		}

		private void OnValueChangedSpacingSlider(float value)
		{
			_storeNameSignText.characterSpacing = value;
		}
	}
}
