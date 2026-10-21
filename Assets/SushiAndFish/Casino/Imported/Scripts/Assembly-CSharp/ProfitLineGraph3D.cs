namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::DG.Tweening;
using SushiAndFish.Casino.Original.Extensions;
using global::Shapes;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.UI;

public class ProfitLineGraph3D : MonoBehaviour
{
	[SerializeField]
	private float timeAxisLength;

	[SerializeField]
	private float profitAxisHeight;

	[SerializeField]
	private float playerSpacing;

	[SerializeField]
	private float rightPadding;

	[Header("Line Settings")]
	[SerializeField]
	private float lineThickness = 0.05f;

	[SerializeField]
	private UIColorPalette colorPalette;

	[Header("Endpoint Marker")]
	[SerializeField]
	private bool showEndpointMarkers = true;

	[SerializeField]
	private bool showPlayerNameLeft;

	[SerializeField]
	private float nameOffsetX = 1f;

	[SerializeField]
	private GameObject endpointMarkerPrefab;

	[SerializeField]
	private Vector3 markerScale = Vector3.one;

	[SerializeField]
	private GameObject profitTextPrefab;

	[SerializeField]
	private GameObject playerNameTextPrefab;

	[SerializeField]
	private float textOffsetX = 0.5f;

	[SerializeField]
	private Vector3 textScale = Vector3.one;

	[Header("Corner Marker")]
	[SerializeField]
	private bool showCornerMarkers = true;

	[SerializeField]
	private float cornerMarkerRadius = 0.08f;

	[Header("Graph Display Settings")]
	[SerializeField]
	private long maxProfitDisplay;

	[Header("Graph Display Settings")]
	[SerializeField]
	private long minProfitDisplay = 1000L;

	[Header("Animation Settings")]
	[SerializeField]
	private float animationDuration = 5f;

	[SerializeField]
	private Ease animationEase = Ease.OutQuad;

	[Header("Performance")]
	[SerializeField]
	private int maxPointsPerPlayer = 120;

	private LobbySettings lobbySettings;

	private Color[] playerColors;

	private List<PlayerProfile> playerOrder = new List<PlayerProfile>();

	private List<Polyline> playerPolylines = new List<Polyline>();

	private List<GameObject> playerEndMarkers = new List<GameObject>();

	private List<GameObject> playerEndTexts = new List<GameObject>();

	private List<GameObject> playerNameTexts = new List<GameObject>();

	private readonly List<GameObject> cornerMarkerPool = new List<GameObject>();

	private void OnEnable()
	{
		if (Application.isPlaying)
		{
			NetworkSingleton<PayoutTracker>.TryGetInstance(out var instance);
			if (instance != null)
			{
				instance.OnPayoutRecorded += OnPayoutRecorded;
			}
		}
	}

	public void ResetAndAnimate()
	{
		DOTween.Kill(this);
		foreach (Polyline playerPolyline in playerPolylines)
		{
			if ((bool)playerPolyline)
			{
				playerPolyline.gameObject.SetActive(value: false);
			}
		}
		foreach (GameObject playerEndMarker in playerEndMarkers)
		{
			if ((bool)playerEndMarker)
			{
				playerEndMarker.SetActive(value: false);
			}
		}
		foreach (GameObject playerEndText in playerEndTexts)
		{
			if ((bool)playerEndText)
			{
				playerEndText.SetActive(value: false);
			}
		}
		foreach (GameObject playerNameText in playerNameTexts)
		{
			if ((bool)playerNameText)
			{
				playerNameText.SetActive(value: false);
			}
		}
		if (showCornerMarkers)
		{
			foreach (GameObject item in cornerMarkerPool)
			{
				if ((bool)item)
				{
					item.SetActive(value: false);
				}
			}
		}
		AnimatePolylines();
	}

	private void OnDisable()
	{
		if (Application.isPlaying)
		{
			PayoutTracker instance = NetworkSingleton<PayoutTracker>.Instance;
			if (instance != null)
			{
				instance.OnPayoutRecorded -= OnPayoutRecorded;
			}
		}
		DOTween.Kill(this);
		DestroyAllSpheres();
	}

	private void OnPayoutRecorded(PayoutRecord _)
	{
	}

	private void DestroyAllSpheres()
	{
		for (int i = 0; i < playerEndMarkers.Count; i++)
		{
			if ((bool)playerEndMarkers[i])
			{
				if (Application.isPlaying)
				{
					UnityEngine.Object.Destroy(playerEndMarkers[i]);
				}
				else
				{
					UnityEngine.Object.DestroyImmediate(playerEndMarkers[i]);
				}
			}
		}
		playerEndMarkers.Clear();
		for (int j = 0; j < playerEndTexts.Count; j++)
		{
			if ((bool)playerEndTexts[j])
			{
				if (Application.isPlaying)
				{
					UnityEngine.Object.Destroy(playerEndTexts[j]);
				}
				else
				{
					UnityEngine.Object.DestroyImmediate(playerEndTexts[j]);
				}
			}
		}
		playerEndTexts.Clear();
		for (int k = 0; k < playerNameTexts.Count; k++)
		{
			if ((bool)playerNameTexts[k])
			{
				if (Application.isPlaying)
				{
					UnityEngine.Object.Destroy(playerNameTexts[k]);
				}
				else
				{
					UnityEngine.Object.DestroyImmediate(playerNameTexts[k]);
				}
			}
		}
		playerNameTexts.Clear();
		for (int l = 0; l < cornerMarkerPool.Count; l++)
		{
			if ((bool)cornerMarkerPool[l])
			{
				if (Application.isPlaying)
				{
					UnityEngine.Object.Destroy(cornerMarkerPool[l]);
				}
				else
				{
					UnityEngine.Object.DestroyImmediate(cornerMarkerPool[l]);
				}
			}
		}
		cornerMarkerPool.Clear();
	}

	private void AnimatePolylines()
	{
		PayoutTracker instance = NetworkSingleton<PayoutTracker>.Instance;
		if (instance == null)
		{
			return;
		}
		List<PayoutRecord> allRecords = instance.GetAllRecords();
		playerOrder.Clear();
		Dictionary<PlayerProfile, int> dictionary = new Dictionary<PlayerProfile, int>();
		List<long> list = new List<long>();
		List<List<long>> list2 = new List<List<long>>();
		long num = minProfitDisplay;
		long num2 = maxProfitDisplay;
		bool flag = minProfitDisplay == 0L && maxProfitDisplay == 0;
		if (flag)
		{
			num = 0L;
			num2 = 0L;
		}
		for (int i = 0; i < allRecords.Count; i++)
		{
			PayoutRecord payoutRecord = allRecords[i];
			PlayerProfile playerProfile = payoutRecord.playerProfile;
			if (playerProfile == null)
			{
				continue;
			}
			if (!dictionary.TryGetValue(playerProfile, out var value))
			{
				value = (dictionary[playerProfile] = playerOrder.Count);
				playerOrder.Add(playerProfile);
				list.Add(0L);
				list2.Add(new List<long>());
			}
			if (payoutRecord.bet == 0L && payoutRecord.payout == 0L && payoutRecord.profit == 0L)
			{
				continue;
			}
			long num3 = (list[value] += payoutRecord.profit);
			list2[value].Add(num3);
			if (flag)
			{
				if (num3 < num)
				{
					num = num3;
				}
				if (num3 > num2)
				{
					num2 = num3;
				}
			}
		}
		List<long[]> list3 = new List<long[]>(playerOrder.Count);
		for (int j = 0; j < list2.Count; j++)
		{
			list3.Add(DownsamplePoints(list2[j], maxPointsPerPlayer));
		}
		if (flag)
		{
			if (num == long.MaxValue || num2 == long.MinValue)
			{
				num = 0L;
				num2 = 1L;
			}
			long num4 = Math.Max(100L, (num2 - num) / 10);
			num -= num4;
			num2 += num4;
		}
		float num5 = Mathf.Max(1f, num2 - num);
		if ((object)lobbySettings == null)
		{
			lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
		}
		int count2 = playerOrder.Count;
		playerColors = new Color[count2];
		for (int k = 0; k < count2; k++)
		{
			PlayerProfile playerProfile2 = playerOrder[k];
			if (lobbySettings != null && playerProfile2 != null)
			{
				PlayerInfo playerBySteamId = lobbySettings.GetPlayerBySteamId(playerProfile2.steamId);
				if (playerBySteamId != null)
				{
					playerColors[k] = playerBySteamId.playerColor;
					continue;
				}
			}
			playerColors[k] = GetPlayerColorByIndex(k);
		}
		float duration = Mathf.Max(0.01f, animationDuration);
		int num6 = 0;
		float num7 = timeAxisLength - rightPadding;
		int num8 = 0;
		for (int l = 0; l < count2; l++)
		{
			long[] array = list3[l];
			int num9 = array.Length;
			if (num9 == 0)
			{
				continue;
			}
			int playerIndex = l;
			int vertexCount = ((num9 < 2) ? 2 : num9);
			Vector3[] finalPositions = new Vector3[vertexCount];
			long[] profitValues = new long[vertexCount];
			float num10 = ((vertexCount <= 1) ? 0f : (num7 / (float)(vertexCount - 1)));
			float z = (float)(playerIndex + 2) * playerSpacing;
			for (int m = 0; m < vertexCount; m++)
			{
				long num11 = ((m < num9) ? array[m] : array[0]);
				float num12 = Mathf.Clamp01((float)(num11 - num) / num5);
				finalPositions[m] = new Vector3(num10 * (float)m, num12 * profitAxisHeight, z);
				profitValues[m] = num11;
			}
			int[] cornerIndices = null;
			if (showCornerMarkers && num9 >= 3)
			{
				List<int> list4 = new List<int>(2);
				for (int n = 1; n < num9 - 1; n++)
				{
					long num13 = array[n] - array[n - 1];
					long num14 = array[n + 1] - array[n];
					if (num13 != 0L && num14 != 0L && ((num13 > 0 && num14 < 0) || (num13 < 0 && num14 > 0)))
					{
						list4.Add(n);
					}
				}
				cornerIndices = ((list4.Count > 0) ? list4.ToArray() : null);
			}
			Color c = playerColors[playerIndex];
			Polyline polyline = GetOrCreatePolyline(playerIndex);
			polyline.gameObject.SetActive(value: true);
			polyline.Thickness = lineThickness;
			polyline.ThicknessSpace = ThicknessSpace.Meters;
			polyline.Geometry = PolylineGeometry.Billboard;
			polyline.Joins = PolylineJoins.Round;
			polyline.BlendMode = ShapesBlendMode.Opaque;
			polyline.Closed = false;
			polyline.Color = c;
			GameObject[] cornerGOs = null;
			if (cornerIndices != null)
			{
				cornerGOs = new GameObject[cornerIndices.Length];
				for (int num15 = 0; num15 < cornerIndices.Length; num15++)
				{
					int num16 = cornerIndices[num15];
					GameObject gameObject = ((num8 < cornerMarkerPool.Count) ? cornerMarkerPool[num8] : null);
					if (!gameObject)
					{
						gameObject = new GameObject($"CornerMarker_{playerIndex}_{num15}");
						gameObject.transform.SetParent(base.transform, worldPositionStays: false);
						gameObject.AddComponent<Sphere>().RadiusSpace = ThicknessSpace.Meters;
						cornerMarkerPool.Add(gameObject);
						SetLayerRecursively(gameObject, SushiAndFish.Casino.CasinoLayers.NameToLayer("Graph"));
					}
					Sphere component = gameObject.GetComponent<Sphere>();
					component.Radius = cornerMarkerRadius;
					component.Color = c;
					gameObject.transform.localPosition = finalPositions[num16];
					gameObject.SetActive(value: false);
					cornerGOs[num15] = gameObject;
					num8++;
				}
			}
			List<Vector3> animPositions = new List<Vector3>(vertexCount);
			List<Color> animColors = new List<Color>(vertexCount);
			Vector3 vector = finalPositions[0];
			for (int num17 = 0; num17 < vertexCount; num17++)
			{
				animPositions.Add(vector);
				animColors.Add(c);
			}
			polyline.SetPoints(animPositions, animColors);
			if (showEndpointMarkers)
			{
				UpdateEndpointMarker(playerIndex, vector, c, profitValues[0]);
			}
			float delay = (float)num6 * 0.1f;
			num6++;
			DOVirtual.Float(0f, 1f, duration, (float progress) =>
			{
				float num18 = progress * (float)(vertexCount - 1);
				int num19 = Mathf.Clamp((int)num18, 0, vertexCount - 2);
				float t = Mathf.Clamp01(num18 - (float)num19);
				Vector3 vector2 = Vector3.Lerp(finalPositions[num19], finalPositions[num19 + 1], t);
				long profit = (long)Math.Round(Mathf.Lerp(profitValues[num19], profitValues[num19 + 1], t));
				for (int num20 = 0; num20 <= num19; num20++)
				{
					animPositions[num20] = finalPositions[num20];
				}
				animPositions[num19 + 1] = vector2;
				for (int num21 = num19 + 2; num21 < vertexCount; num21++)
				{
					animPositions[num21] = vector2;
				}
				polyline.SetPoints(animPositions, animColors);
				if (cornerGOs != null)
				{
					for (int num22 = 0; num22 < cornerGOs.Length; num22++)
					{
						cornerGOs[num22].SetActive(num19 >= cornerIndices[num22]);
					}
				}
				if (showEndpointMarkers)
				{
					UpdateEndpointMarker(playerIndex, vector2, c, profit);
				}
			}).SetDelay(delay).SetEase(animationEase)
				.SetTarget(this);
		}
	}

	private static long[] DownsamplePoints(List<long> source, int maxPoints)
	{
		if (source == null || source.Count == 0)
		{
			return Array.Empty<long>();
		}
		if (maxPoints < 2 || source.Count <= maxPoints)
		{
			return source.ToArray();
		}
		int count = source.Count;
		long[] array = new long[maxPoints];
		array[0] = source[0];
		array[maxPoints - 1] = source[count - 1];
		float num = ((float)count - 1f) / ((float)maxPoints - 1f);
		for (int i = 1; i < maxPoints - 1; i++)
		{
			int index = Mathf.Clamp(Mathf.RoundToInt((float)i * num), 0, count - 1);
			array[i] = source[index];
		}
		return array;
	}

	private Polyline GetOrCreatePolyline(int playerIndex)
	{
		while (playerPolylines.Count <= playerIndex)
		{
			playerPolylines.Add(null);
		}
		if (playerPolylines[playerIndex] == null)
		{
			GameObject gameObject = new GameObject($"PlayerLine_{playerIndex}");
			gameObject.transform.SetParent(base.transform);
			gameObject.transform.localPosition = Vector3.zero;
			gameObject.transform.localRotation = Quaternion.identity;
			gameObject.transform.localScale = Vector3.one;
			SetLayerRecursively(gameObject, SushiAndFish.Casino.CasinoLayers.NameToLayer("Graph"));
			Polyline value = gameObject.AddComponent<Polyline>();
			playerPolylines[playerIndex] = value;
		}
		return playerPolylines[playerIndex];
	}

	private void UpdateEndpointMarker(int playerIndex, Vector3 endPosition, Color playerColor, long profit)
	{
		if (endpointMarkerPrefab == null)
		{
			Debug.LogWarning("ProfitLineGraph3D: Endpoint marker prefab is not assigned!");
			return;
		}
		while (playerEndMarkers.Count <= playerIndex)
		{
			playerEndMarkers.Add(null);
		}
		if (playerEndMarkers[playerIndex] == null)
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(endpointMarkerPrefab, base.transform);
			gameObject.name = $"PlayerEndMarker_{playerIndex}";
			gameObject.transform.localPosition = Vector3.zero;
			gameObject.transform.localRotation = Quaternion.identity;
			gameObject.transform.localScale = markerScale;
			SetLayerRecursively(gameObject, SushiAndFish.Casino.CasinoLayers.NameToLayer("Graph"));
			playerEndMarkers[playerIndex] = gameObject;
		}
		playerEndMarkers[playerIndex].transform.localPosition = endPosition;
		playerEndMarkers[playerIndex].transform.localScale = markerScale;
		playerEndMarkers[playerIndex].SetActive(value: true);
		MeshRenderer componentInChildren = playerEndMarkers[playerIndex].GetComponentInChildren<MeshRenderer>();
		if (componentInChildren != null && componentInChildren.material != null)
		{
			componentInChildren.material.color = playerColor;
		}
		UpdateProfitText(playerIndex, endPosition, profit);
		if (showPlayerNameLeft)
		{
			UpdatePlayerNameText(playerIndex, endPosition);
		}
		else if (playerIndex < playerNameTexts.Count && playerNameTexts[playerIndex] != null)
		{
			playerNameTexts[playerIndex].SetActive(value: false);
		}
	}

	private void UpdatePlayerNameText(int playerIndex, Vector3 markerPosition)
	{
		while (playerNameTexts.Count <= playerIndex)
		{
			playerNameTexts.Add(null);
		}
		if (playerNameTexts[playerIndex] == null)
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(playerNameTextPrefab, base.transform);
			gameObject.name = $"PlayerNameText_{playerIndex}";
			gameObject.transform.localPosition = Vector3.zero;
			gameObject.transform.localRotation = Quaternion.identity;
			gameObject.transform.localScale = textScale;
			SetLayerRecursively(gameObject, SushiAndFish.Casino.CasinoLayers.NameToLayer("Graph"));
			playerNameTexts[playerIndex] = gameObject;
		}
		float z = 2f * playerSpacing;
		Vector3 localPosition = new Vector3(markerPosition.x - nameOffsetX, markerPosition.y, z);
		playerNameTexts[playerIndex].transform.localPosition = localPosition;
		playerNameTexts[playerIndex].transform.localScale = textScale;
		playerNameTexts[playerIndex].SetActive(value: true);
		string text = ((playerIndex < playerOrder.Count && playerOrder[playerIndex] != null) ? playerOrder[playerIndex].playerName : $"Player {playerIndex}");
		TextMeshPro component = playerNameTexts[playerIndex].GetComponent<TextMeshPro>();
		if (component != null)
		{
			component.text = text;
			return;
		}
		Text component2 = playerNameTexts[playerIndex].GetComponent<Text>();
		if (component2 != null)
		{
			component2.text = text;
		}
	}

	private void UpdateProfitText(int playerIndex, Vector3 markerPosition, long profit)
	{
		while (playerEndTexts.Count <= playerIndex)
		{
			playerEndTexts.Add(null);
		}
		if (playerEndTexts[playerIndex] == null)
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(profitTextPrefab, base.transform);
			gameObject.name = $"PlayerEndText_{playerIndex}";
			gameObject.transform.localPosition = Vector3.zero;
			gameObject.transform.localRotation = Quaternion.identity;
			gameObject.transform.localScale = textScale;
			SetLayerRecursively(gameObject, SushiAndFish.Casino.CasinoLayers.NameToLayer("Graph"));
			playerEndTexts[playerIndex] = gameObject;
		}
		float z = 2f * playerSpacing;
		Vector3 localPosition = new Vector3(markerPosition.x + textOffsetX, markerPosition.y, z);
		playerEndTexts[playerIndex].transform.localPosition = localPosition;
		playerEndTexts[playerIndex].transform.localScale = textScale;
		playerEndTexts[playerIndex].SetActive(value: true);
		string text = ((profit >= 0) ? ("+" + MoneyFormatter.FormatWithDollar(profit)) : MoneyFormatter.FormatWithDollar(profit));
		TextMeshPro component = playerEndTexts[playerIndex].GetComponent<TextMeshPro>();
		if (component != null)
		{
			component.text = text;
			return;
		}
		Text component2 = playerEndTexts[playerIndex].GetComponent<Text>();
		if (component2 != null)
		{
			component2.text = text;
		}
	}

	private Color GetPlayerColorByIndex(int playerIndex)
	{
		if (colorPalette != null && colorPalette.playerColors != null && colorPalette.playerColors.Length != 0)
		{
			return colorPalette.playerColors[playerIndex % colorPalette.playerColors.Length];
		}
		Color[] array = new Color[6]
		{
			new Color(1f, 0.3f, 0.3f),
			new Color(0.3f, 1f, 0.3f),
			new Color(0.3f, 0.5f, 1f),
			new Color(1f, 0.8f, 0.2f),
			new Color(0.2f, 1f, 1f),
			new Color(1f, 0.3f, 1f)
		};
		return array[playerIndex % array.Length];
	}

	private void SetLayerRecursively(GameObject obj, int layer)
	{
		if (obj == null)
		{
			return;
		}
		obj.layer = layer;
		foreach (Transform item in obj.transform)
		{
			SetLayerRecursively(item.gameObject, layer);
		}
	}
}

}
