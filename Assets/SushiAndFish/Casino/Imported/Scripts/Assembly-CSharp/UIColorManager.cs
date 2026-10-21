namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.UI;

public class UIColorManager : MonoBehaviour
{
	public enum Tone
	{
		ProfitGreen = 0,
		LossRed = 1,
		TicketYellow = 2,
		White = 3,
		Black = 4,
		PlayerColor = 5,
		GwyfMainColor = 6,
		GwyfSecondaryColor = 7,
		NPCColor = 8
	}

	[Serializable]
	public class ColorGroup
	{
		public Tone tone;

		public bool useTransparency;

		public bool useEmission;

		public List<Image> images = new List<Image>();

		public List<TextMeshProUGUI> texts = new List<TextMeshProUGUI>();

		public List<TextMeshPro> textMeshPros = new List<TextMeshPro>();

		public List<MeshRenderer> meshRenderers = new List<MeshRenderer>();

		public List<SkinnedMeshRenderer> skinnedMeshRenderers = new List<SkinnedMeshRenderer>();

		public List<Material> decalMaterials = new List<Material>();

		public List<LineRenderer> lineRenderers = new List<LineRenderer>();
	}

	[SerializeField]
	private UIColorPalette palette;

	[SerializeField]
	private List<ColorGroup> colorGroups = new List<ColorGroup>();

	private LobbySettings lobbySettings;

	private Color _lastAppliedPlayerColor = new Color(0f, 0f, 0f, 0f);

	private Dictionary<Renderer, Material> _runtimeMaterials = new Dictionary<Renderer, Material>();

	private Dictionary<LineRenderer, Material> _runtimeLineMaterials = new Dictionary<LineRenderer, Material>();

	private void Awake()
	{
		if (lobbySettings == null)
		{
			lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
		}
		if (palette == null)
		{
			palette = SushiAndFish.Casino.Original.CasinoResources.Load<UIColorPalette>("ColorSettings");
			if (palette == null)
			{
				Debug.LogError("UIColorManager: Failed to load UIColorPalette from Resources/ColorSettings. Please ensure the ColorSettings asset exists in a Resources folder.");
			}
		}
	}

	private void OnEnable()
	{
		UIColorPalette.PaletteChanged += OnPaletteChanged;
		LobbySettings.SettingsChanged += OnLobbySettingsChanged;
		ApplyColors();
	}

	private void OnDisable()
	{
		UIColorPalette.PaletteChanged -= OnPaletteChanged;
		LobbySettings.SettingsChanged -= OnLobbySettingsChanged;
	}

	private void OnPaletteChanged(UIColorPalette changed)
	{
		if (changed == palette)
		{
			ApplyColors();
		}
	}

	private void OnLobbySettingsChanged(LobbySettings settings)
	{
		PlayerProfile component = base.gameObject.GetComponent<PlayerProfile>();
		if (component != null && settings != null && palette != null)
		{
			settings.GetPlayerBySteamId(component.steamId);
		}
		Color playerColor = GetPlayerColor();
		if (_lastAppliedPlayerColor != playerColor)
		{
			_lastAppliedPlayerColor = playerColor;
			ApplyColors();
		}
	}

	public void ApplyColors()
	{
		if (palette == null)
		{
			return;
		}
		foreach (ColorGroup colorGroup in colorGroups)
		{
			Color color = colorGroup.tone switch
			{
				Tone.ProfitGreen => palette.profitGreen, 
				Tone.LossRed => palette.lossRed, 
				Tone.TicketYellow => palette.ticketYellow, 
				Tone.White => palette.white, 
				Tone.Black => palette.black, 
				Tone.PlayerColor => GetPlayerColor(), 
				Tone.GwyfMainColor => palette.gwyfMainColor, 
				Tone.GwyfSecondaryColor => palette.gwyfSecondaryColor, 
				Tone.NPCColor => palette.NPCColor, 
				_ => palette.profitGreen, 
			};
			if (colorGroup.useTransparency)
			{
				color.a = 0.5f;
			}
			foreach (Image image in colorGroup.images)
			{
				if ((bool)image)
				{
					image.color = color;
				}
			}
			foreach (TextMeshProUGUI text in colorGroup.texts)
			{
				if ((bool)text)
				{
					text.color = color;
				}
			}
			foreach (TextMeshPro textMeshPro in colorGroup.textMeshPros)
			{
				if ((bool)textMeshPro)
				{
					textMeshPro.color = color;
				}
			}
			foreach (MeshRenderer meshRenderer in colorGroup.meshRenderers)
			{
				if (!meshRenderer)
				{
					continue;
				}
				if (Application.isPlaying)
				{
					Material sharedMaterial = meshRenderer.sharedMaterial;
					if (!(sharedMaterial == null))
					{
						if (!_runtimeMaterials.ContainsKey(meshRenderer))
						{
							_runtimeMaterials[meshRenderer] = new Material(sharedMaterial);
							meshRenderer.material = _runtimeMaterials[meshRenderer];
						}
						Material material = _runtimeMaterials[meshRenderer];
						material.color = new Color(color.r, color.g, color.b, material.color.a);
						if (colorGroup.useEmission && material.HasProperty("_EmissionColor"))
						{
							Color playerColor = GetPlayerColor();
							material.SetColor("_EmissionColor", playerColor);
							material.EnableKeyword("_EMISSION");
						}
						else if (material.HasProperty("_EmissionColor"))
						{
							material.DisableKeyword("_EMISSION");
						}
					}
				}
				else
				{
					Material sharedMaterial2 = meshRenderer.sharedMaterial;
					if (!(sharedMaterial2 == null))
					{
						sharedMaterial2.color = new Color(color.r, color.g, color.b, sharedMaterial2.color.a);
					}
				}
			}
			foreach (SkinnedMeshRenderer skinnedMeshRenderer in colorGroup.skinnedMeshRenderers)
			{
				if (!skinnedMeshRenderer)
				{
					continue;
				}
				if (Application.isPlaying)
				{
					Material sharedMaterial3 = skinnedMeshRenderer.sharedMaterial;
					if (!(sharedMaterial3 == null))
					{
						if (!_runtimeMaterials.ContainsKey(skinnedMeshRenderer))
						{
							_runtimeMaterials[skinnedMeshRenderer] = new Material(sharedMaterial3);
							skinnedMeshRenderer.material = _runtimeMaterials[skinnedMeshRenderer];
						}
						Material material2 = _runtimeMaterials[skinnedMeshRenderer];
						material2.color = new Color(color.r, color.g, color.b, material2.color.a);
					}
				}
				else
				{
					Material sharedMaterial4 = skinnedMeshRenderer.sharedMaterial;
					if (!(sharedMaterial4 == null))
					{
						sharedMaterial4.color = new Color(color.r, color.g, color.b, sharedMaterial4.color.a);
					}
				}
			}
			foreach (Material decalMaterial in colorGroup.decalMaterials)
			{
				if ((bool)decalMaterial)
				{
					decalMaterial.SetColor("_Tint", color);
					if (colorGroup.useEmission && decalMaterial.HasProperty("_EmissionColor"))
					{
						Color playerColor2 = GetPlayerColor();
						decalMaterial.SetColor("_EmissionColor", playerColor2);
						decalMaterial.EnableKeyword("_EMISSION");
					}
					else if (decalMaterial.HasProperty("_EmissionColor"))
					{
						decalMaterial.DisableKeyword("_EMISSION");
					}
				}
			}
			foreach (LineRenderer lineRenderer in colorGroup.lineRenderers)
			{
				if (!lineRenderer)
				{
					continue;
				}
				if (Application.isPlaying)
				{
					if (!_runtimeLineMaterials.ContainsKey(lineRenderer))
					{
						Material sharedMaterial5 = lineRenderer.sharedMaterial;
						if (sharedMaterial5 == null)
						{
							continue;
						}
						_runtimeLineMaterials[lineRenderer] = new Material(sharedMaterial5);
						lineRenderer.material = _runtimeLineMaterials[lineRenderer];
					}
					Material material3 = _runtimeLineMaterials[lineRenderer];
					material3.color = new Color(color.r, color.g, color.b, material3.color.a);
					if (colorGroup.useEmission && material3.HasProperty("_EmissionColor"))
					{
						Color playerColor3 = GetPlayerColor();
						material3.SetColor("_EmissionColor", playerColor3);
						material3.EnableKeyword("_EMISSION");
					}
					else if (material3.HasProperty("_EmissionColor"))
					{
						material3.DisableKeyword("_EMISSION");
					}
				}
				else
				{
					lineRenderer.sharedMaterial.color = new Color(color.r, color.g, color.b, lineRenderer.sharedMaterial.color.a);
				}
			}
		}
	}

	private Color GetPlayerColor()
	{
		if (lobbySettings == null)
		{
			return palette.playerColor;
		}
		if (base.gameObject.TryGetComponent<SteamIdComponent>(out var component))
		{
			return lobbySettings.GetPlayerBySteamId(component.SteamId)?.playerColor ?? palette.playerColor;
		}
		if (base.gameObject.TryGetComponent<PlayerProfile>(out var component2))
		{
			return lobbySettings.GetPlayerBySteamId(component2.steamId)?.playerColor ?? palette.playerColor;
		}
		return palette.playerColor;
	}
}

}
