using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

public static class SteamAvatarCache
{
	private static readonly Dictionary<SteamId, Sprite> _avatarCache = new Dictionary<SteamId, Sprite>();

	private static readonly Dictionary<SteamId, float> _nextAllowedFetchTime = new Dictionary<SteamId, float>();

	private static readonly Dictionary<SteamId, UniTask<Sprite>> _inflight = new Dictionary<SteamId, UniTask<Sprite>>();

	public static async Task<Sprite> GetAvatarSpriteAsync(SteamId steamId, Sprite fallback = null, int maxRetries = 4, int initialDelayMs = 80, int cooldownMsOnFail = 1500)
	{
		await UniTask.SwitchToMainThread();
		if (_avatarCache.TryGetValue(steamId, out var value) && value != null)
		{
			return value;
		}
		if (!SteamClient.IsValid)
		{
			return fallback;
		}
		if (_nextAllowedFetchTime.TryGetValue(steamId, out var value2) && Time.unscaledTime < value2)
		{
			return fallback;
		}
		if (_inflight.TryGetValue(steamId, out var value3))
		{
			Sprite sprite = await value3;
			return (sprite != null) ? sprite : fallback;
		}
		UniTask<Sprite> value4 = FetchWithRetryAsync(steamId, maxRetries, initialDelayMs).Preserve();
		_inflight[steamId] = value4;
		Sprite sprite2 = null;
		try
		{
			sprite2 = await value4;
		}
		finally
		{
			_inflight.Remove(steamId);
		}
		if (sprite2 != null)
		{
			_avatarCache[steamId] = sprite2;
			_nextAllowedFetchTime.Remove(steamId);
			return sprite2;
		}
		_nextAllowedFetchTime[steamId] = Time.unscaledTime + (float)cooldownMsOnFail / 1000f;
		return fallback;
	}

	private static async UniTask<Sprite> FetchWithRetryAsync(SteamId steamId, int maxRetries, int initialDelayMs)
	{
		int delay = initialDelayMs;
		for (int attempt = 0; attempt < maxRetries; attempt++)
		{
			Image? image = await SteamFriends.GetLargeAvatarAsync(steamId);
			if (image.HasValue)
			{
				Image value = image.Value;
				int w = (int)value.Width;
				int h = (int)value.Height;
				byte[] d = value.Data;
				if (d != null && d.Length == w * h * 4 && w > 0 && h > 0)
				{
					VerticalFlipRgba32InPlace(d, w, h);
					await UniTask.SwitchToMainThread();
					Texture2D texture2D = new Texture2D(w, h, TextureFormat.RGBA32, 1, linear: false);
					texture2D.LoadRawTextureData(d);
					texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
					texture2D.wrapMode = TextureWrapMode.Clamp;
					texture2D.filterMode = FilterMode.Bilinear;
					return Sprite.Create(texture2D, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
				}
			}
			await UniTask.Delay(delay, ignoreTimeScale: true);
			delay = Mathf.Min(delay * 2, 500);
		}
		return null;
	}

	private static void VerticalFlipRgba32InPlace(byte[] data, int width, int height)
	{
		int num = width * 4;
		byte[] array = new byte[num];
		int num2 = height / 2;
		for (int i = 0; i < num2; i++)
		{
			int num3 = i * num;
			int num4 = (height - 1 - i) * num;
			Buffer.BlockCopy(data, num3, array, 0, num);
			Buffer.BlockCopy(data, num4, data, num3, num);
			Buffer.BlockCopy(array, 0, data, num4, num);
		}
	}
}
