namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.IO;
using global::Newtonsoft.Json;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.Networking;
using global::UnityEngine.UI;

public class FlexibleCreditsScreen : MonoBehaviour
{
	[Serializable]
	private class CreditEntry
	{
		public string type;

		public string text;

		public string path;
	}

	[Serializable]
	private class CreditsData
	{
		public string body;

		public string[] lines;

		public CreditEntry[] entries;

		public bool HasEntries
		{
			get
			{
				if (entries != null)
				{
					return entries.Length != 0;
				}
				return false;
			}
		}

		public string[] ResolveLines()
		{
			if (lines != null && lines.Length != 0)
			{
				return lines;
			}
			if (!string.IsNullOrEmpty(body))
			{
				return body.Split(new string[3] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
			}
			return Array.Empty<string>();
		}
	}

	[Header("Scroll")]
	[SerializeField]
	private ScrollRect scrollRect;

	[Tooltip("Pixels per second. Scroll duration follows content height.")]
	[SerializeField]
	private float scrollSpeedPixelsPerSecond = 48f;

	[Tooltip("Minimum time to scroll when content is shorter than the viewport.")]
	[SerializeField]
	private float minScrollDuration = 4f;

	[Header("Layout")]
	[SerializeField]
	private RectTransform content;

	[SerializeField]
	private GameObject linePrefab;

	[SerializeField]
	private GameObject imagePrefab;

	[Header("Credits JSON")]
	[Tooltip("If set, this text is parsed (Editor and builds).")]
	[SerializeField]
	private TextAsset creditsJson;

	[Tooltip("Optional: file under StreamingAssets, e.g. credits.json. Used when creditsJson is null.")]
	[SerializeField]
	private string streamingAssetsFileName = "credits.json";

	private Coroutine _scrollRoutine;

	private readonly List<Texture2D> _loadedTextures = new List<Texture2D>();

	private readonly List<Sprite> _loadedSprites = new List<Sprite>();

	private void Reset()
	{
		if (scrollRect == null)
		{
			scrollRect = GetComponent<ScrollRect>();
		}
		if (scrollRect != null && content == null)
		{
			content = scrollRect.content;
		}
	}

	private void OnEnable()
	{
		if (scrollRect == null || content == null)
		{
			Debug.LogWarning("[FlexibleCreditsScreen] Assign ScrollRect and Content.");
		}
		else
		{
			_scrollRoutine = StartCoroutine(PlayCreditsRoutine());
		}
	}

	private void OnDisable()
	{
		if (_scrollRoutine != null)
		{
			StopCoroutine(_scrollRoutine);
			_scrollRoutine = null;
		}
	}

	private IEnumerator PlayCreditsRoutine()
	{
		ClearContent();
		CreditsData creditsData = LoadCreditsData();
		if (creditsData.HasEntries)
		{
			yield return StartCoroutine(BuildFromEntries(creditsData.entries));
		}
		else
		{
			if (linePrefab == null)
			{
				Debug.LogWarning("[FlexibleCreditsScreen] Assign Line Prefab for text-only credits.");
				yield break;
			}
			string[] lines = creditsData.ResolveLines();
			SpawnTextLines(lines);
		}
		scrollRect.StopMovement();
		scrollRect.velocity = Vector2.zero;
		scrollRect.verticalNormalizedPosition = 1f;
		Canvas.ForceUpdateCanvases();
		LayoutRebuilder.ForceRebuildLayoutImmediate(content);
		yield return null;
		Canvas.ForceUpdateCanvases();
		LayoutRebuilder.ForceRebuildLayoutImmediate(content);
		scrollRect.verticalNormalizedPosition = 1f;
		float height = ((scrollRect.viewport != null) ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>()).rect.height;
		float height2 = content.rect.height;
		float num = Mathf.Max(0f, height2 - height);
		float duration = ((num > 0f) ? (num / Mathf.Max(0.01f, scrollSpeedPixelsPerSecond)) : minScrollDuration);
		duration = Mathf.Max(duration, minScrollDuration);
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			float num2 = Mathf.Clamp01(elapsed / duration);
			num2 = num2 * num2 * (3f - 2f * num2);
			scrollRect.verticalNormalizedPosition = Mathf.Lerp(1f, 0f, num2);
			yield return null;
		}
		scrollRect.verticalNormalizedPosition = 0f;
	}

	private IEnumerator BuildFromEntries(CreditEntry[] entries)
	{
		foreach (CreditEntry creditEntry in entries)
		{
			if (creditEntry == null)
			{
				continue;
			}
			if ((string.IsNullOrEmpty(creditEntry.type) ? "text" : creditEntry.type.Trim().ToLowerInvariant()) == "image")
			{
				if (imagePrefab == null)
				{
					Debug.LogWarning("[FlexibleCreditsScreen] Image entry in JSON but Image Prefab is not assigned.");
					continue;
				}
				if (string.IsNullOrWhiteSpace(creditEntry.path))
				{
					Debug.LogWarning("[FlexibleCreditsScreen] Image entry missing path.");
					continue;
				}
				Texture2D tex = null;
				yield return LoadTextureAsync(creditEntry.path.Trim(), (Texture2D t) =>
				{
					tex = t;
				});
				if (!(tex == null))
				{
					GameObject instance = UnityEngine.Object.Instantiate(imagePrefab, content);
					ApplyTextureToPrefab(instance, tex);
				}
			}
			else if (linePrefab == null)
			{
				Debug.LogWarning("[FlexibleCreditsScreen] Text entry skipped: Line Prefab not assigned.");
			}
			else if (linePrefab.GetComponentInChildren<TMP_Text>(includeInactive: true) == null)
			{
				Debug.LogWarning("[FlexibleCreditsScreen] Line prefab needs a TMP_Text in children.");
			}
			else
			{
				UnityEngine.Object.Instantiate(linePrefab, content).GetComponentInChildren<TMP_Text>(includeInactive: true).text = creditEntry.text ?? "";
			}
		}
	}

	private void SpawnTextLines(string[] lines)
	{
		foreach (string text in lines)
		{
			UnityEngine.Object.Instantiate(linePrefab, content).GetComponentInChildren<TMP_Text>(includeInactive: true).text = text;
		}
	}

	private void ApplyTextureToPrefab(GameObject instance, Texture2D tex)
	{
		RawImage componentInChildren = instance.GetComponentInChildren<RawImage>(includeInactive: true);
		if (componentInChildren != null)
		{
			componentInChildren.texture = tex;
			ApplyAspectRatio(componentInChildren.rectTransform, tex);
			_loadedTextures.Add(tex);
			return;
		}
		Image componentInChildren2 = instance.GetComponentInChildren<Image>(includeInactive: true);
		if (componentInChildren2 != null)
		{
			Sprite item = (componentInChildren2.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f));
			componentInChildren2.preserveAspect = true;
			ApplyAspectRatio(componentInChildren2.rectTransform, tex);
			_loadedTextures.Add(tex);
			_loadedSprites.Add(item);
		}
		else
		{
			Debug.LogWarning("[FlexibleCreditsScreen] Image prefab needs RawImage or Image.");
			UnityEngine.Object.Destroy(tex);
		}
	}

	private static void ApplyAspectRatio(RectTransform target, Texture2D tex)
	{
		if (!(target == null) && !(tex == null) && tex.height != 0)
		{
			float aspectRatio = (float)tex.width / (float)tex.height;
			AspectRatioFitter aspectRatioFitter = target.GetComponent<AspectRatioFitter>();
			if (aspectRatioFitter == null)
			{
				aspectRatioFitter = target.gameObject.AddComponent<AspectRatioFitter>();
			}
			aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
			aspectRatioFitter.aspectRatio = aspectRatio;
		}
	}

	private IEnumerator LoadTextureAsync(string relativePathOrUrl, Action<Texture2D> done)
	{
		string url = BuildTextureRequestUrl(relativePathOrUrl);
		using UnityWebRequest req = UnityWebRequestTexture.GetTexture(url);
		yield return req.SendWebRequest();
		if (req.result != UnityWebRequest.Result.Success)
		{
			Debug.LogWarning("[FlexibleCreditsScreen] Image failed: " + url + " (" + req.error + ")");
			done?.Invoke(null);
		}
		else
		{
			Texture2D obj = DownloadHandlerTexture.GetContent(req);
			done?.Invoke(obj);
		}
	}

	private static string BuildTextureRequestUrl(string relativePathOrUrl)
	{
		string text = relativePathOrUrl.Trim();
		if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
		{
			return text;
		}
		string text2 = Path.Combine(SushiAndFish.Casino.Original.CasinoPaths.StreamingAssetsPath, text).Replace('\\', '/');
		if (!text2.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
		{
			return "file://" + text2;
		}
		return text2;
	}

	private void ClearContent()
	{
		foreach (Sprite loadedSprite in _loadedSprites)
		{
			if (loadedSprite != null)
			{
				UnityEngine.Object.Destroy(loadedSprite);
			}
		}
		_loadedSprites.Clear();
		foreach (Texture2D loadedTexture in _loadedTextures)
		{
			if (loadedTexture != null)
			{
				UnityEngine.Object.Destroy(loadedTexture);
			}
		}
		_loadedTextures.Clear();
		for (int num = content.childCount - 1; num >= 0; num--)
		{
			UnityEngine.Object.Destroy(content.GetChild(num).gameObject);
		}
	}

	private CreditsData LoadCreditsData()
	{
		string text = null;
		if (creditsJson != null)
		{
			text = creditsJson.text;
		}
		else if (!string.IsNullOrWhiteSpace(streamingAssetsFileName))
		{
			string text2 = Path.Combine(SushiAndFish.Casino.Original.CasinoPaths.StreamingAssetsPath, streamingAssetsFileName);
			if (File.Exists(text2))
			{
				text = File.ReadAllText(text2);
			}
			else
			{
				Debug.LogWarning("[FlexibleCreditsScreen] Missing file: " + text2);
			}
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			Debug.LogWarning("[FlexibleCreditsScreen] No credits JSON; assign TextAsset or StreamingAssets file.");
			return new CreditsData();
		}
		text = StripUtf8Bom(text);
		try
		{
			CreditsFileJson creditsFileJson = JsonConvert.DeserializeObject<CreditsFileJson>(text);
			if (creditsFileJson == null)
			{
				return new CreditsData();
			}
			CreditsData creditsData = new CreditsData
			{
				body = creditsFileJson.body,
				lines = creditsFileJson.lines
			};
			if (creditsFileJson.entries != null && creditsFileJson.entries.Length != 0)
			{
				creditsData.entries = new CreditEntry[creditsFileJson.entries.Length];
				for (int i = 0; i < creditsFileJson.entries.Length; i++)
				{
					CreditEntryJson creditEntryJson = creditsFileJson.entries[i];
					creditsData.entries[i] = new CreditEntry
					{
						type = creditEntryJson?.type,
						text = creditEntryJson?.text,
						path = creditEntryJson?.path
					};
				}
			}
			return creditsData;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[FlexibleCreditsScreen] JSON parse failed: " + ex.Message);
			return new CreditsData();
		}
	}

	private static string StripUtf8Bom(string s)
	{
		if (string.IsNullOrEmpty(s) || s[0] != '\ufeff')
		{
			return s;
		}
		return s.Substring(1);
	}
}

}
