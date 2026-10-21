using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Restaurant.Sign
{
	public class ColorPickerController : MonoBehaviour
	{
		[SerializeField]
		private SVImageControl _sVImageControl;

		[Space]
		[SerializeField]
		private RawImage _hueImage;

		[SerializeField]
		private RawImage _satValImage;

		[SerializeField]
		private RawImage _outpuImage;

		[Space]
		[SerializeField]
		private Slider _hueSlider;

		[Space]
		[SerializeField]
		private TMP_InputField _hexInputField;

		private Texture2D _hueTexture;

		private Texture2D _svTexture;

		private Texture2D _outputTexture;

		private TextMeshProUGUI _storeNameText;

		private TMP_InputField _inputField;

		private float _currentHue;

		private float _currentSat;

		private float _currentVal;

		private bool _isUpdating;

		public void Initialize(TextMeshProUGUI storeNameText, TMP_InputField inputField)
		{
			ReleaseTextures();
			_storeNameText = storeNameText;
			_inputField = inputField;
			CreateHueImage();
			CreateSVImage();
			CreateOutputImage();
			UpdateOutputImage();
			_hueSlider.onValueChanged.RemoveAllListeners();
			_hueSlider.onValueChanged.AddListener(UpdateSVImage);
			_hexInputField.onValueChanged.RemoveAllListeners();
			_hexInputField.onValueChanged.AddListener(OnTextInput);
		}

		private void OnDestroy()
		{
			if (_hueSlider != null)
			{
				_hueSlider.onValueChanged.RemoveListener(UpdateSVImage);
			}
			if (_hexInputField != null)
			{
				_hexInputField.onValueChanged.RemoveListener(OnTextInput);
			}
			ReleaseTextures();
		}

		private void ReleaseTextures()
		{
			ReleaseTexture(ref _hueTexture, _hueImage);
			ReleaseTexture(ref _svTexture, _satValImage);
			ReleaseTexture(ref _outputTexture, _outpuImage);
		}

		private static void ReleaseTexture(ref Texture2D texture, RawImage image)
		{
			if (!(texture == null))
			{
				if (image != null && image.texture == texture)
				{
					image.texture = null;
				}
				if (Application.isPlaying)
				{
					Object.Destroy(texture);
				}
				else
				{
					Object.DestroyImmediate(texture);
				}
				texture = null;
			}
		}

		private void CreateHueImage()
		{
			_hueTexture = new Texture2D(1, 16);
			_hueTexture.wrapMode = TextureWrapMode.Clamp;
			_hueTexture.name = "HueTexture";
			for (int i = 0; i < _hueTexture.height; i++)
			{
				_hueTexture.SetPixel(0, i, Color.HSVToRGB((float)i / (float)_hueTexture.height, 1f, 1f));
			}
			_hueTexture.Apply();
			_currentHue = 0f;
			_hueImage.texture = _hueTexture;
		}

		private void CreateSVImage()
		{
			_svTexture = new Texture2D(16, 16);
			_svTexture.wrapMode = TextureWrapMode.Clamp;
			_svTexture.name = "SatValTexture";
			for (int i = 0; i < _svTexture.height; i++)
			{
				for (int j = 0; j < _svTexture.width; j++)
				{
					_svTexture.SetPixel(j, i, Color.HSVToRGB(_currentHue, (float)j / (float)_svTexture.width, (float)i / (float)_svTexture.height));
				}
			}
			_svTexture.Apply();
			_currentSat = 0f;
			_currentVal = 0f;
			_satValImage.texture = _svTexture;
		}

		private void CreateOutputImage()
		{
			_outputTexture = new Texture2D(1, 16);
			_outputTexture.wrapMode = TextureWrapMode.Clamp;
			_outputTexture.name = "OutputTexture";
			Color color = Color.HSVToRGB(_currentHue, _currentSat, _currentVal);
			for (int i = 0; i < _outputTexture.height; i++)
			{
				_outputTexture.SetPixel(0, i, color);
			}
			_outputTexture.Apply();
			_outpuImage.texture = _outputTexture;
		}

		private void UpdateOutputImage()
		{
			if (!_isUpdating)
			{
				_isUpdating = true;
				Color color = Color.HSVToRGB(_currentHue, _currentSat, _currentVal);
				color.a = 1f;
				for (int i = 0; i < _outputTexture.height; i++)
				{
					_outputTexture.SetPixel(0, i, color);
				}
				_outputTexture.Apply();
				_hexInputField.text = ColorUtility.ToHtmlStringRGB(color);
				if (_storeNameText != null)
				{
					_storeNameText.color = color;
				}
				if (_inputField != null && _inputField.textComponent != null)
				{
					_inputField.textComponent.color = color;
				}
				_isUpdating = false;
			}
		}

		public void SetSV(float s, float v)
		{
			_currentSat = s;
			_currentVal = v;
			UpdateOutputImage();
		}

		private void UpdateSVImage(float value)
		{
			_currentHue = value;
			for (int i = 0; i < _svTexture.height; i++)
			{
				for (int j = 0; j < _svTexture.width; j++)
				{
					_svTexture.SetPixel(j, i, Color.HSVToRGB(_currentHue, (float)j / (float)_svTexture.width, (float)i / (float)_svTexture.height));
				}
			}
			_svTexture.Apply();
			UpdateOutputImage();
		}

		private void OnTextInput(string text)
		{
			if (!_isUpdating && !string.IsNullOrWhiteSpace(text) && text.Length >= 6)
			{
				_isUpdating = true;
				if (ColorUtility.TryParseHtmlString("#" + text, out var color))
				{
					color.a = 1f;
					Color.RGBToHSV(color, out _currentHue, out _currentSat, out _currentVal);
				}
				_hueSlider.value = _currentHue;
				_sVImageControl.UpdatePickerTransformPosition(_currentSat, _currentVal);
				_isUpdating = false;
				UpdateOutputImage();
			}
		}
	}
}
