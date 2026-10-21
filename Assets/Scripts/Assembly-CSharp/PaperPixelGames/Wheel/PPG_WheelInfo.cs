using System.Collections.Generic;
using Lean.Pool;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace PaperPixelGames.Wheel
{
	public class PPG_WheelInfo : MonoBehaviour
	{
		[SerializeField]
		private GameObject _hintArea;

		[SerializeField]
		private HintItem _hintPrefab;

		private Dictionary<string, HintItem> _hints = new Dictionary<string, HintItem>();

		public void WheelVisible(int wheelType, bool isEmoteWheelPage)
		{
			switch (wheelType)
			{
			case 1:
				if (isEmoteWheelPage)
				{
					AddHint("RightMouseMechanic", "key_open_expressions", "GameStateTable");
				}
				else
				{
					RemoveHint("RightMouseMechanic");
				}
				break;
			}
			_hintArea.SetActive(value: true);
		}

		public void WheelInvisible(int wheelType)
		{
			switch (wheelType)
			{
			case 1:
				RemoveHint("RightMouseMechanic");
				break;
			}
			_hintArea.SetActive(value: false);
		}

		private void AddHint(string iconName, string localizationKey, string tableKey)
		{
			HintItem hintItem;
			if (_hints.ContainsKey(iconName))
			{
				hintItem = _hints[iconName];
			}
			else
			{
				hintItem = LeanPool.Spawn(_hintPrefab, _hintArea.transform);
				_hints.Add(iconName, hintItem);
			}
			Singleton<HintsManager>.Instance.SetupHint(hintItem, iconName, localizationKey, tableKey);
		}

		private void RemoveHint(string iconName)
		{
			if (_hints.ContainsKey(iconName))
			{
				LeanPool.Despawn(_hints[iconName]);
				_hints.Remove(iconName);
			}
		}
	}
}
