using System.Collections.Generic;
using PaperPixelGames.Framework.UI;
using UnityEngine;

namespace PaperPixelGames.Framework.States.MainMenu
{
	public class SaveSlotScreen : BaseScreen
	{
		[SerializeField]
		private List<SaveSlot> _saveSlots;

		protected override void OnInitialize()
		{
			foreach (SaveSlot saveSlot in _saveSlots)
			{
				saveSlot.Initialize();
			}
		}
	}
}
