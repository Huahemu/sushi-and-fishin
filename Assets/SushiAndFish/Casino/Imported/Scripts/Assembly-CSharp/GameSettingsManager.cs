namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::System.Reflection;
using global::UnityEngine;
using global::UnityEngine.UIElements;

public class GameSettingsManager : MonoBehaviour
{
	[SerializeField]
	private UIDocument uiDocument;

	[Header("Settings")]
	[SerializeField]
	private GameSettings gameSettings;

	[SerializeField]
	private PlayerSettings playerSettings;

	[SerializeField]
	private CameraSettings cameraSettings;

	[SerializeField]
	private SoundSettings soundSettings;

	[SerializeField]
	private UIColorPalette colorPalette;

	[SerializeField]
	private LobbySettings lobbySettings;

	[SerializeField]
	private SpawnableSettings spawnableSettings;

	[SerializeField]
	private ChallengeSettings challengeSettings;

	private Dictionary<string, ScriptableObject> settingsDict;

	private string _selectedSettingType;

	private VisualElement root;

	private bool isVisible;

	public bool IsVisible => isVisible;

	private void Start()
	{
		settingsDict = new Dictionary<string, ScriptableObject>();
		if (gameSettings != null)
		{
			settingsDict["Game"] = gameSettings;
		}
		if (playerSettings != null)
		{
			settingsDict["Player"] = playerSettings;
		}
		if (cameraSettings != null)
		{
			settingsDict["Camera"] = cameraSettings;
		}
		if (soundSettings != null)
		{
			settingsDict["Sound"] = soundSettings;
		}
		if (colorPalette != null)
		{
			settingsDict["Colors"] = colorPalette;
		}
		if (lobbySettings != null)
		{
			settingsDict["Lobby"] = lobbySettings;
		}
		if (spawnableSettings != null)
		{
			settingsDict["Spawnable"] = spawnableSettings;
		}
		if (challengeSettings != null)
		{
			settingsDict["Challenge"] = challengeSettings;
		}
		if (settingsDict.Count > 0)
		{
			_selectedSettingType = new List<string>(settingsDict.Keys)[0];
		}
		else
		{
			_selectedSettingType = null;
		}
		if (uiDocument == null)
		{
			Debug.LogError("UIDocument reference is missing on GameSettingsManager!");
			return;
		}
		VisualTreeAsset visualTreeAsset = SushiAndFish.Casino.Original.CasinoResources.Load<VisualTreeAsset>("GameSettingsManager");
		if (visualTreeAsset == null)
		{
			Debug.LogError("Failed to load GameSettingsManager.uxml from Resources!");
			return;
		}
		StyleSheet styleSheet = SushiAndFish.Casino.Original.CasinoResources.Load<StyleSheet>("GameSettingsManager");
		if (styleSheet == null)
		{
			Debug.LogError("Failed to load GameSettingsManager.uss from Resources!");
			return;
		}
		uiDocument.rootVisualElement.Clear();
		root = visualTreeAsset.CloneTree();
		root.styleSheets.Add(styleSheet);
		uiDocument.rootVisualElement.Add(root);
		root.style.display = DisplayStyle.None;
		CreateGUI();
	}

	private void OnDestroy()
	{
	}

	private void CreateGUI()
	{
		VisualElement chipBar = root.Q<VisualElement>("chipBar");
		ScrollView settingsScroll = root.Q<ScrollView>("settingsScroll");
		Font font = SushiAndFish.Casino.Original.CasinoResources.Load<Font>("Fonts/Bungee-Regular");
		if (font != null)
		{
			root.style.unityFont = font;
		}
		chipBar.Clear();
		chipBar.style.flexDirection = FlexDirection.Row;
		chipBar.style.flexWrap = Wrap.NoWrap;
		foreach (string key in settingsDict.Keys)
		{
			Button chip = null;
			string capturedTab = key;
			chip = new Button(() =>
			{
				_selectedSettingType = capturedTab;
				UpdateSettingsList(settingsScroll);
				foreach (VisualElement item in chipBar.Children())
				{
					item.RemoveFromClassList("chip--selected");
				}
				chip.AddToClassList("chip--selected");
			})
			{
				text = key
			};
			chip.AddToClassList("chip");
			if (key == _selectedSettingType)
			{
				chip.AddToClassList("chip--selected");
			}
			chipBar.Add(chip);
		}
		UpdateSettingsList(settingsScroll);
	}

	private void UpdateSettingsList(ScrollView scroll)
	{
		scroll.Clear();
		if (_selectedSettingType == null || !settingsDict.ContainsKey(_selectedSettingType))
		{
			Label label = new Label("No settings assigned for this tab.");
			label.style.unityFont = SushiAndFish.Casino.Original.CasinoResources.Load<Font>("Fonts/Bungee-Regular");
			scroll.Add(label);
			return;
		}
		ScriptableObject setting = settingsDict[_selectedSettingType];
		if (setting == null)
		{
			Label label2 = new Label(_selectedSettingType + " settings are not assigned!");
			label2.style.unityFont = SushiAndFish.Casino.Original.CasinoResources.Load<Font>("Fonts/Bungee-Regular");
			scroll.Add(label2);
			return;
		}
		VisualElement visualElement = new VisualElement();
		visualElement.style.marginBottom = 8f;
		visualElement.style.paddingBottom = 8f;
		visualElement.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
		FieldInfo[] fields = setting.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
		foreach (FieldInfo field in fields)
		{
			if (!(field.Name == "m_Script"))
			{
				VisualElement visualElement2 = new VisualElement();
				visualElement2.style.flexDirection = FlexDirection.Row;
				visualElement2.style.marginBottom = 4f;
				Label label3 = new Label(field.Name);
				label3.style.width = 150f;
				label3.style.color = Color.white;
				label3.style.unityFont = SushiAndFish.Casino.Original.CasinoResources.Load<Font>("Fonts/Bungee-Regular");
				Label child = label3;
				visualElement2.Add(child);
				object value = field.GetValue(setting);
				VisualElement visualElement3 = CreateFieldForType(field.FieldType, value, (object newValue) =>
				{
					field.SetValue(setting, newValue);
					NotifySettingsChanged(setting);
				});
				if (visualElement3 != null)
				{
					visualElement3.style.flexGrow = 1f;
					visualElement3.style.unityFont = SushiAndFish.Casino.Original.CasinoResources.Load<Font>("Fonts/Bungee-Regular");
					visualElement2.Add(visualElement3);
					visualElement.Add(visualElement2);
				}
			}
		}
		scroll.Add(visualElement);
	}

	private void NotifySettingsChanged(ScriptableObject setting)
	{
		MethodInfo method = setting.GetType().GetMethod("NotifyChanged", BindingFlags.Instance | BindingFlags.Public);
		if (method != null)
		{
			method.Invoke(setting, null);
		}
		if (isVisible)
		{
			ScrollView scroll = root.Q<ScrollView>("settingsScroll");
			UpdateSettingsList(scroll);
		}
	}

	private VisualElement CreateFieldForType(Type type, object value, Action<object> onValueChanged)
	{
		Font font = SushiAndFish.Casino.Original.CasinoResources.Load<Font>("Fonts/Bungee-Regular");
		if (type == typeof(float))
		{
			FloatField floatField = new FloatField();
			floatField.value = (float)value;
			floatField.style.unityFont = font;
			floatField.RegisterValueChangedCallback((ChangeEvent<float> evt) =>
			{
				onValueChanged(evt.newValue);
			});
			return floatField;
		}
		if (type == typeof(int))
		{
			IntegerField integerField = new IntegerField();
			integerField.value = (int)value;
			integerField.style.unityFont = font;
			integerField.RegisterValueChangedCallback((ChangeEvent<int> evt) =>
			{
				onValueChanged(evt.newValue);
			});
			return integerField;
		}
		if (type == typeof(bool))
		{
			Toggle toggle = new Toggle();
			toggle.value = (bool)value;
			toggle.style.unityFont = font;
			toggle.RegisterValueChangedCallback((ChangeEvent<bool> evt) =>
			{
				onValueChanged(evt.newValue);
			});
			return toggle;
		}
		if (type == typeof(string))
		{
			TextField textField = new TextField();
			textField.value = (string)value;
			textField.style.unityFont = font;
			textField.RegisterValueChangedCallback((ChangeEvent<string> evt) =>
			{
				onValueChanged(evt.newValue);
			});
			return textField;
		}
		if (type == typeof(Vector2))
		{
			VisualElement visualElement = new VisualElement();
			visualElement.style.flexDirection = FlexDirection.Row;
			FloatField floatField2 = new FloatField
			{
				value = ((Vector2)value).x
			};
			FloatField floatField3 = new FloatField
			{
				value = ((Vector2)value).y
			};
			floatField2.style.unityFont = font;
			floatField3.style.unityFont = font;
			floatField2.RegisterValueChangedCallback((ChangeEvent<float> evt) =>
			{
				Vector2 vector = (Vector2)value;
				vector.x = evt.newValue;
				onValueChanged(vector);
			});
			floatField3.RegisterValueChangedCallback((ChangeEvent<float> evt) =>
			{
				Vector2 vector = (Vector2)value;
				vector.y = evt.newValue;
				onValueChanged(vector);
			});
			Label label = new Label("X:");
			label.style.width = 20f;
			label.style.unityFont = font;
			visualElement.Add(label);
			visualElement.Add(floatField2);
			Label label2 = new Label("Y:");
			label2.style.width = 20f;
			label2.style.marginLeft = 8f;
			label2.style.unityFont = font;
			visualElement.Add(label2);
			visualElement.Add(floatField3);
			return visualElement;
		}
		if (type == typeof(Vector3))
		{
			VisualElement visualElement2 = new VisualElement();
			visualElement2.style.flexDirection = FlexDirection.Row;
			FloatField floatField4 = new FloatField
			{
				value = ((Vector3)value).x
			};
			FloatField floatField5 = new FloatField
			{
				value = ((Vector3)value).y
			};
			FloatField floatField6 = new FloatField
			{
				value = ((Vector3)value).z
			};
			floatField4.style.unityFont = font;
			floatField5.style.unityFont = font;
			floatField6.style.unityFont = font;
			floatField4.RegisterValueChangedCallback((ChangeEvent<float> evt) =>
			{
				Vector3 vector = (Vector3)value;
				vector.x = evt.newValue;
				onValueChanged(vector);
			});
			floatField5.RegisterValueChangedCallback((ChangeEvent<float> evt) =>
			{
				Vector3 vector = (Vector3)value;
				vector.y = evt.newValue;
				onValueChanged(vector);
			});
			floatField6.RegisterValueChangedCallback((ChangeEvent<float> evt) =>
			{
				Vector3 vector = (Vector3)value;
				vector.z = evt.newValue;
				onValueChanged(vector);
			});
			Label label3 = new Label("X:");
			label3.style.width = 20f;
			label3.style.unityFont = font;
			visualElement2.Add(label3);
			visualElement2.Add(floatField4);
			Label label4 = new Label("Y:");
			label4.style.width = 20f;
			label4.style.marginLeft = 8f;
			label4.style.unityFont = font;
			visualElement2.Add(label4);
			visualElement2.Add(floatField5);
			Label label5 = new Label("Z:");
			label5.style.width = 20f;
			label5.style.marginLeft = 8f;
			label5.style.unityFont = font;
			visualElement2.Add(label5);
			visualElement2.Add(floatField6);
			return visualElement2;
		}
		if (type == typeof(Color))
		{
			VisualElement visualElement3 = new VisualElement();
			visualElement3.style.flexDirection = FlexDirection.Row;
			FloatField floatField7 = new FloatField
			{
				value = ((Color)value).r
			};
			FloatField floatField8 = new FloatField
			{
				value = ((Color)value).g
			};
			FloatField floatField9 = new FloatField
			{
				value = ((Color)value).b
			};
			FloatField floatField10 = new FloatField
			{
				value = ((Color)value).a
			};
			floatField7.style.unityFont = font;
			floatField8.style.unityFont = font;
			floatField9.style.unityFont = font;
			floatField10.style.unityFont = font;
			floatField7.RegisterValueChangedCallback((ChangeEvent<float> evt) =>
			{
				Color color = (Color)value;
				color.r = evt.newValue;
				onValueChanged(color);
			});
			floatField8.RegisterValueChangedCallback((ChangeEvent<float> evt) =>
			{
				Color color = (Color)value;
				color.g = evt.newValue;
				onValueChanged(color);
			});
			floatField9.RegisterValueChangedCallback((ChangeEvent<float> evt) =>
			{
				Color color = (Color)value;
				color.b = evt.newValue;
				onValueChanged(color);
			});
			floatField10.RegisterValueChangedCallback((ChangeEvent<float> evt) =>
			{
				Color color = (Color)value;
				color.a = evt.newValue;
				onValueChanged(color);
			});
			Label label6 = new Label("R:");
			label6.style.width = 20f;
			label6.style.unityFont = font;
			visualElement3.Add(label6);
			visualElement3.Add(floatField7);
			Label label7 = new Label("G:");
			label7.style.width = 20f;
			label7.style.marginLeft = 8f;
			label7.style.unityFont = font;
			visualElement3.Add(label7);
			visualElement3.Add(floatField8);
			Label label8 = new Label("B:");
			label8.style.width = 20f;
			label8.style.marginLeft = 8f;
			label8.style.unityFont = font;
			visualElement3.Add(label8);
			visualElement3.Add(floatField9);
			Label label9 = new Label("A:");
			label9.style.width = 20f;
			label9.style.marginLeft = 8f;
			label9.style.unityFont = font;
			visualElement3.Add(label9);
			visualElement3.Add(floatField10);
			return visualElement3;
		}
		if (type == typeof(AnimationCurve))
		{
			VisualElement visualElement4 = new VisualElement();
			visualElement4.style.flexDirection = FlexDirection.Row;
			Label label10 = new Label("AnimationCurve (read-only)");
			label10.style.unityFont = font;
			visualElement4.Add(label10);
			return visualElement4;
		}
		if (type.IsEnum)
		{
			VisualElement visualElement5 = new VisualElement();
			visualElement5.style.flexDirection = FlexDirection.Row;
			PopupField<string> popupField = new PopupField<string>(new List<string>(Enum.GetNames(type)), Enum.GetName(type, value));
			popupField.style.unityFont = font;
			popupField.RegisterValueChangedCallback((ChangeEvent<string> evt) =>
			{
				object obj = Enum.Parse(type, evt.newValue);
				onValueChanged(obj);
			});
			visualElement5.Add(popupField);
			return visualElement5;
		}
		if (type == typeof(LayerMask))
		{
			IntegerField integerField2 = new IntegerField();
			integerField2.value = ((LayerMask)value).value;
			integerField2.style.unityFont = font;
			integerField2.RegisterValueChangedCallback((ChangeEvent<int> evt) =>
			{
				onValueChanged(new LayerMask
				{
					value = evt.newValue
				});
			});
			return integerField2;
		}
		Label label11 = new Label(value?.ToString() ?? "null");
		label11.style.unityFont = font;
		return label11;
	}

	private void ToggleUI()
	{
		isVisible = !isVisible;
		root.style.display = ((!isVisible) ? DisplayStyle.None : DisplayStyle.Flex);
	}
}

}
