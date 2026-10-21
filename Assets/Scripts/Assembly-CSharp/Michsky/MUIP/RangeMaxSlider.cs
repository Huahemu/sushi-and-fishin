using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	public class RangeMaxSlider : Slider
	{
		public RangeMinSlider minSlider;

		public TextMeshProUGUI label;

		public string numberFormat;

		public float realValue;

		private bool assignedRealValue;

		protected override void Start()
		{
			realValue = base.maxValue;
			base.Start();
		}

		protected override void Set(float input, bool sendCallback)
		{
			if (minSlider == null)
			{
				minSlider = base.transform.parent.Find("Min Slider").GetComponent<RangeMinSlider>();
			}
			if (!assignedRealValue)
			{
				realValue = base.maxValue;
				assignedRealValue = true;
			}
			else
			{
				realValue = base.maxValue - input + base.minValue;
			}
			if (base.wholeNumbers)
			{
				realValue = Mathf.Round(realValue);
			}
			if (!(realValue <= minSlider.value))
			{
				if (label != null)
				{
					label.text = realValue.ToString(numberFormat);
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
