namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::UnityEngine;

public class NPCCosmeticSelector : MonoBehaviour
{
	[Serializable]
	public class CosmeticPreset
	{
		public string presetName;

		public GameObject[] enabledCosmetics;
	}

	[Header("Preset Selection")]
	[SerializeField]
	private CosmeticPreset[] presets;

	[SerializeField]
	private int selectedPresetIndex;

	[SerializeField]
	private bool applyOnAwake = true;

	private void Awake()
	{
		if (applyOnAwake)
		{
			ApplySelectedPreset();
		}
	}

	private void OnValidate()
	{
		ApplySelectedPreset();
	}

	[ContextMenu("Apply Selected Preset")]
	public void ApplySelectedPreset()
	{
		if (presets == null || presets.Length == 0)
		{
			return;
		}
		selectedPresetIndex = Mathf.Clamp(selectedPresetIndex, 0, presets.Length - 1);
		CosmeticPreset cosmeticPreset = presets[selectedPresetIndex];
		if (cosmeticPreset == null || cosmeticPreset.enabledCosmetics == null)
		{
			return;
		}
		HashSet<GameObject> hashSet = new HashSet<GameObject>(cosmeticPreset.enabledCosmetics);
		for (int i = 0; i < presets.Length; i++)
		{
			CosmeticPreset cosmeticPreset2 = presets[i];
			if (cosmeticPreset2 == null || cosmeticPreset2.enabledCosmetics == null)
			{
				continue;
			}
			for (int j = 0; j < cosmeticPreset2.enabledCosmetics.Length; j++)
			{
				GameObject gameObject = cosmeticPreset2.enabledCosmetics[j];
				if (!(gameObject == null))
				{
					gameObject.SetActive(hashSet.Contains(gameObject));
				}
			}
		}
	}

	public void SetSelectedPresetIndex(int presetIndex)
	{
		selectedPresetIndex = presetIndex;
		ApplySelectedPreset();
	}
}

}
