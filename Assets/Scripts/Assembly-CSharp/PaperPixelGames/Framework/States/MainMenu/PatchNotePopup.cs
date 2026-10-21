using System.Collections;
using System.Text.RegularExpressions;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace PaperPixelGames.Framework.States.MainMenu
{
	public class PatchNotePopup : MonoBehaviour
	{
		[Header("Patch Note Banner")]
		[SerializeField]
		private BaseButton _patchNoteButton;

		[SerializeField]
		private RawImage _patchNoteImage;

		private static Texture2D _cachedPatchNoteTexture;

		private string _latestPatchUrl = "";

		private const string SteamAppId = "3163790";

		public void Initialize()
		{
			_patchNoteButton.Clicked += OnClickPatchNote;
			if (base.gameObject.activeSelf)
			{
				TryFetchPatchNote();
			}
		}

		private void TryFetchPatchNote()
		{
			StartCoroutine(FetchLatestPatchNote());
		}

		private IEnumerator FetchLatestPatchNote()
		{
			string uri = "https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=3163790&count=1&maxlength=5000&format=json";
			using UnityWebRequest request = UnityWebRequest.Get(uri);
			yield return request.SendWebRequest();
			if (request.result == UnityWebRequest.Result.Success)
			{
				SteamNews steamNews = JsonUtility.FromJson<SteamNews>(request.downloadHandler.text);
				if (steamNews.appnews.newsitems.Length != 0)
				{
					NewsItem newsItem = steamNews.appnews.newsitems[0];
					_latestPatchUrl = newsItem.url;
					StartCoroutine(FetchPatchNoteHeaderImage(_latestPatchUrl));
				}
			}
		}

		private IEnumerator FetchPatchNoteHeaderImage(string patchNoteUrl)
		{
			using UnityWebRequest request = UnityWebRequest.Get(patchNoteUrl);
			yield return request.SendWebRequest();
			if (request.result == UnityWebRequest.Result.Success)
			{
				Match match = Regex.Match(request.downloadHandler.text, "<meta property=\"og:image\" content=\"(.*?)\"");
				if (match.Success)
				{
					string value = match.Groups[1].Value;
					StartCoroutine(LoadPatchNoteImage(value));
				}
				else
				{
					UtilityMethods.Log("Couldn't find Steam Patch Note Image");
				}
			}
			else
			{
				UtilityMethods.Log("Couldn't load Steam Patch Note HTML");
			}
		}

		private IEnumerator LoadPatchNoteImage(string imageUrl)
		{
			if (_cachedPatchNoteTexture != null)
			{
				_patchNoteImage.texture = _cachedPatchNoteTexture;
				yield break;
			}
			using UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl);
			yield return request.SendWebRequest();
			if (request.result == UnityWebRequest.Result.Success)
			{
				_cachedPatchNoteTexture = DownloadHandlerTexture.GetContent(request);
				_patchNoteImage.texture = _cachedPatchNoteTexture;
			}
			else
			{
				UtilityMethods.Log("Couldn't load Steam Patch Note Image");
			}
		}

		private void OnClickPatchNote()
		{
			if (!string.IsNullOrEmpty(_latestPatchUrl))
			{
				Application.OpenURL(_latestPatchUrl);
			}
		}
	}
}
