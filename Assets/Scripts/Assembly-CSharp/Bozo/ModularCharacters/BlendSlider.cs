using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bozo.ModularCharacters
{
	public class BlendSlider : MonoBehaviour
	{
		private Slider slider;

		[SerializeField]
		private TMP_Text title;

		[SerializeField]
		private TMP_Text sliderValue;

		[SerializeField]
		private OutfitSystem system;

		private CharacterCreator characterCreator;

		public string shape;

		public void Init(OutfitSystem system, string key)
		{
			slider = GetComponentInChildren<Slider>();
			this.system = system;
			shape = key;
			characterCreator = GetComponentInParent<CharacterCreator>();
			RefreshLocalization();
			RefreshValue();
			slider.onValueChanged.AddListener(Apply);
		}

		private void OnEnable()
		{
			RefreshValue();
		}

		public void RefreshValue()
		{
			if (!(system == null) && !(slider == null) && !string.IsNullOrEmpty(shape))
			{
				float shapeValue = system.GetShapeValue(shape);
				slider.SetValueWithoutNotify(shapeValue);
				if (sliderValue != null)
				{
					sliderValue.text = $"{shapeValue}%";
				}
			}
		}

		public void RefreshLocalization()
		{
			if (!(title == null) && !string.IsNullOrWhiteSpace(shape))
			{
				title.text = CustomizationLocalizationUtility.GetAdjustmentLabel(shape, shape);
			}
		}

		public void Apply(float value)
		{
			characterCreator?.RecordContinuousUndoState();
			system.SetShape(shape, value);
			if (sliderValue != null)
			{
				sliderValue.text = $"{slider.value}%";
			}
		}
	}
}
