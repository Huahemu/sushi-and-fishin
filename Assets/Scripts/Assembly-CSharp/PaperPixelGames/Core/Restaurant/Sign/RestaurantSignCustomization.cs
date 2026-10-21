using PaperPixelGames.Framework.UI;
using TMPro;
using Unity.Collections;
using UnityEngine;

namespace PaperPixelGames.Core.Restaurant.Sign
{
	public class RestaurantSignCustomization : MonoBehaviour
	{
		[SerializeField]
		private ColorPickerController _colorPickerController;

		[SerializeField]
		private GameObject _customizationParentGO;

		[SerializeField]
		private TMP_InputField _inputField;

		[SerializeField]
		private TextMeshProUGUI _tableNameText;

		[SerializeField]
		private BaseButton _okButton;

		private RestaurantSignManager _restaurantTableManager;

		public Color32 TablegSignColor
		{
			get
			{
				Color32 result = _tableNameText.color;
				result.a = byte.MaxValue;
				return result;
			}
		}

		public void Initialize(RestaurantSignManager restaurantTableManager)
		{
			_restaurantTableManager = restaurantTableManager;
			_colorPickerController.Initialize(_tableNameText, _inputField);
			_inputField.onSelect.AddListener(OnSelect);
			_inputField.onDeselect.AddListener(OnDeselect);
			_inputField.onEndEdit.AddListener(OnEndEdit);
			_okButton.Clicked += OnOkButton;
			Deactive();
		}

		public void Active()
		{
			Cursor.lockState = CursorLockMode.None;
			_customizationParentGO.SetActive(value: true);
			_inputField.ActivateInputField();
			_tableNameText.gameObject.SetActive(value: false);
			_inputField.text = _tableNameText.text;
		}

		public void Deactive()
		{
			_customizationParentGO.SetActive(value: false);
			_tableNameText.gameObject.SetActive(value: true);
		}

		public void TableNameChanged(FixedString128Bytes text)
		{
			_tableNameText.text = text.ToString();
		}

		public void TableNameColorChanged(Color color)
		{
			color.a = 1f;
			_tableNameText.color = color;
		}

		private void OnSelect(string _)
		{
		}

		private void OnDeselect(string _)
		{
		}

		private void OnEndEdit(string value)
		{
			_restaurantTableManager.ServerSetTableNameRpc(value);
			_restaurantTableManager.ServerSetTableNameColorRpc(_tableNameText.color);
		}

		private void OnOkButton()
		{
			_restaurantTableManager.ServerSetTableNameRpc(_inputField.text);
			_restaurantTableManager.ServerSetTableNameColorRpc(_tableNameText.color);
			_restaurantTableManager.CurrentInteractor.ServerUseExitRpc();
		}

		public void SetColor(Color color)
		{
			color.a = 1f;
			_tableNameText.color = color;
		}
	}
}
