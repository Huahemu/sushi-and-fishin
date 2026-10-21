namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::System.Reflection;
using global::UnityEngine;

[Serializable]
public class TooltipKeySetup : MonoBehaviour
{
	[Header("Key Button Configuration")]
	[SerializeField]
	private GameObject keyButtonPrefab;

	[SerializeField]
	private KeyButtonManager keyButtonManager;

	[Header("Auto Setup")]
	[SerializeField]
	private bool autoSetupOnStart = true;

	private void Start()
	{
		if (autoSetupOnStart)
		{
			SetupTooltipKeySystem();
		}
	}

	[ContextMenu("Setup Tooltip Key System")]
	public void SetupTooltipKeySystem()
	{
		if (keyButtonManager == null)
		{
			keyButtonManager = GetComponent<KeyButtonManager>();
			if (keyButtonManager == null)
			{
				keyButtonManager = base.gameObject.AddComponent<KeyButtonManager>();
			}
		}
		if (keyButtonPrefab == null)
		{
			keyButtonPrefab = SushiAndFish.Casino.Original.CasinoResources.Load<GameObject>("KeyButton");
			if (keyButtonPrefab == null)
			{
				keyButtonPrefab = SushiAndFish.Casino.Original.CasinoResources.Load<GameObject>("UI/GameplayUI/Prefabs/KeyButton");
			}
		}
		if (keyButtonManager != null && keyButtonPrefab != null)
		{
			FieldInfo field = typeof(KeyButtonManager).GetField("keyButtonPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
			if (field != null)
			{
				field.SetValue(keyButtonManager, keyButtonPrefab);
			}
			Debug.Log("TooltipKeySetup: Successfully assigned KeyButton prefab to KeyButtonManager");
		}
		else
		{
			Debug.LogWarning("TooltipKeySetup: Could not find KeyButton prefab. Please assign it manually in the inspector.");
		}
	}

	public void SetKeyButtonPrefab(GameObject prefab)
	{
		keyButtonPrefab = prefab;
		SetupTooltipKeySystem();
	}

	[ContextMenu("Test Tooltip System")]
	public void TestTooltipSystem()
	{
		if (keyButtonManager == null)
		{
			Debug.LogError("TooltipKeySetup: KeyButtonManager not found. Run Setup first.");
			return;
		}
		string text = "hold [E] to pick up";
		List<TooltipElement> list = TooltipKeyParser.ParseTooltip(text);
		Debug.Log("Test Tooltip: '" + text + "'");
		Debug.Log($"Has Keys: {TooltipKeyParser.HasKeys(text)}");
		Debug.Log($"Elements Count: {list.Count}");
		for (int i = 0; i < list.Count; i++)
		{
			Debug.Log($"Element {i}: Type={list[i].Type}, Content='{list[i].Content}'");
		}
	}
}

}
