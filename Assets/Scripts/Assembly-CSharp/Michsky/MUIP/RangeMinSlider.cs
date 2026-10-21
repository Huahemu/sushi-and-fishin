using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	public class RangeMinSlider : Slider
	{
		[Header("RESOURCES")]
		public RangeMaxSlider maxSlider;

		public TextMeshProUGUI label;

		public string numberFormat;

		protected override void Set(float input, bool sendCallback)
		{
			if (maxSlider == null)
			{
				maxSlider = base.transform.parent.Find("Max Slider").GetComponent<RangeMaxSlider>();
			}
			float num = input;
			if (base.wholeNumbers)
			{
				num = Mathf.Round(num);
			}
			if (!(num >= maxSlider.realValue) || maxSlider.realValue == maxSlider.minValue)
			{
				if (label != null)
				{
					label.text = num.ToString(numberFormat);
				}
				base.Set(input, sendCallback);
			}
		}

		public void Refresh(float input)
		{
			Set(input, sendCallback: false);
		}
	}
}
