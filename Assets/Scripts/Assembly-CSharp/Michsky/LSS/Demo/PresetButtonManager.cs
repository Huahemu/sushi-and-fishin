using System.Collections.Generic;
using UnityEngine;

namespace Michsky.LSS.Demo
{
	public class PresetButtonManager : MonoBehaviour
	{
		private int selectedButtonIndex;

		private List<PresetButton> buttons = new List<PresetButton>();

		private void Start()
		{
			foreach (Transform item in base.transform)
			{
				buttons.Add(item.gameObject.GetComponent<PresetButton>());
			}
			for (int i = 0; i < buttons.Count; i++)
			{
				buttons[i].pbManager = this;
				buttons[i].index = i;
			}
			SetSelectedButton(selectedButtonIndex);
			buttons[selectedButtonIndex].SetPresetToManager();
		}

		public void SetSelectedButton(int index)
		{
			selectedButtonIndex = index;
			for (int i = 0; i < buttons.Count; i++)
			{
				if (i != selectedButtonIndex)
				{
					buttons[i].SetSelected(value: false);
				}
			}
		}
	}
}
