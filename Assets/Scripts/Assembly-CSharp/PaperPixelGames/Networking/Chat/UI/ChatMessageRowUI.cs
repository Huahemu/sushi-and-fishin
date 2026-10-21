using System;
using System.Collections;
using System.Text.RegularExpressions;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Networking.Chat.UI
{
	public sealed class ChatMessageRowUI : MonoBehaviour
	{
		[SerializeField]
		private TextMeshProUGUI _messageText;

		[SerializeField]
		private Shadow _messageShadow;

		private const float DefaultMoveToOldContentDelay = 5f;

		private const float RestaurantMoveToOldContentDelay = 5f;

		private const float DefaultFontSize = 15f;

		private const float RestaurantFontSize = 16f;

		private Transform _oldContent;

		private Action _onLayoutDirty;

		private Coroutine _moveCoroutine;

		private int _version;

		public bool CanDisable { get; private set; }

		private void Awake()
		{
			if (_messageShadow == null && _messageText != null)
			{
				_messageShadow = _messageText.GetComponent<Shadow>();
			}
			ApplyMessageVisuals(isRestaurant: false);
		}

		public void Set(ChatMessageData data, Transform oldContent, Action onLayoutDirty)
		{
			if (data == null)
			{
				return;
			}
			_version++;
			int currentVersion = _version;
			_oldContent = oldContent;
			_onLayoutDirty = onLayoutDirty;
			CanDisable = false;
			StopMoveCoroutine();
			if (_messageText != null)
			{
				ApplyMessageVisuals(data.Type == ChatMessageType.Restaurant);
				_messageText.richText = true;
				_messageText.text = BuildMessageText(data);
			}
			_onLayoutDirty?.Invoke();
			float num = ((data.Type == ChatMessageType.Restaurant) ? 5f : 5f);
			if (Singleton<DelayHelper>.Instance != null)
			{
				Singleton<DelayHelper>.Instance.DelayAction(num, () =>
				{
					MoveToOldContent(currentVersion);
				});
			}
			else
			{
				_moveCoroutine = StartCoroutine(MoveToOldContentRoutine(currentVersion, num));
			}
		}

		public void PrepareForReuse()
		{
			_version++;
			CanDisable = false;
			StopMoveCoroutine();
			_oldContent = null;
			_onLayoutDirty = null;
			ApplyMessageVisuals(isRestaurant: false);
			if (_messageText != null)
			{
				_messageText.text = string.Empty;
			}
		}

		public void Release()
		{
			_version++;
			CanDisable = true;
			StopMoveCoroutine();
			_oldContent = null;
			_onLayoutDirty = null;
			ApplyMessageVisuals(isRestaurant: false);
			if (_messageText != null)
			{
				_messageText.text = string.Empty;
			}
		}

		public void Invalidate()
		{
			_version++;
			CanDisable = true;
			StopMoveCoroutine();
			_oldContent = null;
			_onLayoutDirty = null;
		}

		private string BuildMessageText(ChatMessageData data)
		{
			bool flag = data.Type == ChatMessageType.System;
			bool flag2 = data.Type == ChatMessageType.Info;
			bool flag3 = data.Type == ChatMessageType.Restaurant;
			bool flag4 = flag2 && string.Equals(data.SenderName, "DevConsole", StringComparison.Ordinal);
			string text = ((!string.IsNullOrWhiteSpace(data.SenderName)) ? data.SenderName : (flag ? "System" : "Info"));
			string message = data.Message;
			string text2 = ((data.Zone != string.Empty) ? ("[" + data.Zone + "] ") : "");
			if (flag)
			{
				return BuildSystemMessageText(text, message, data.RestaurantTone);
			}
			if (flag4)
			{
				return "<color=#D8A8FF>DevConsole</color>: " + message;
			}
			if (flag2)
			{
				return "<color=#5CB8FF>Info</color>: " + message;
			}
			if (flag3)
			{
				return BuildRestaurantMessageText(message, data.RestaurantTone);
			}
			if (!TryGetSenderColorHex(data, out var colorHex))
			{
				colorHex = "000000";
			}
			return "<color=#" + colorHex + ">" + text + "</color> " + text2 + ": " + message;
		}

		private static string BuildSystemMessageText(string senderName, string message, RestaurantChatTone tone)
		{
			string text;
			string text2;
			switch (tone)
			{
			case RestaurantChatTone.Success:
				text = "#74D680";
				text2 = "#DDF8E2";
				break;
			case RestaurantChatTone.Warning:
				text = "#FFBE55";
				text2 = "#FFF0C2";
				break;
			case RestaurantChatTone.Error:
				text = "#FF6B6B";
				text2 = "#FFD8D8";
				break;
			default:
				text = "#5CB8FF";
				text2 = "#DCEEFF";
				break;
			}
			return "<b><color=" + text + ">" + senderName + "</color></b> <color=" + text + ">•</color> <color=" + text2 + ">" + message + "</color>";
		}

		private static string BuildRestaurantMessageText(string message, RestaurantChatTone tone)
		{
			string text = tone switch
			{
				RestaurantChatTone.Success => "#74D680", 
				RestaurantChatTone.Warning => "#FFBE55", 
				RestaurantChatTone.Error => "#FF6B6B", 
				_ => "#A8D8FF", 
			};
			string input = Regex.Replace(message, "\\bTable\\s+\\d+\\b", (Match match) => "<b><color=#FFD166>" + match.Value + "</color></b>", RegexOptions.IgnoreCase);
			input = Regex.Replace(input, "\\bSeat\\s+\\d+\\b", (Match match) => "<b><color=#FFE29A>" + match.Value + "</color></b>", RegexOptions.IgnoreCase);
			input = Regex.Replace(input, "\\+?\\$\\d+", (Match match) => "<b><color=#74D680>" + match.Value + "</color></b>");
			input = Regex.Replace(input, "\\bComplete\\b", (Match match) => "<b><color=#74D680>" + match.Value + "</color></b>", RegexOptions.IgnoreCase);
			input = Regex.Replace(input, "\\bMissing side(?: & beverage)?\\b|\\bMissing beverage\\b", (Match match) => "<b><color=#FFBE55>" + match.Value + "</color></b>", RegexOptions.IgnoreCase);
			input = Regex.Replace(input, "\\b(ready|waiting|payment|perfect|perfectly|wrong)\\b", (Match match) =>
			{
				string text2;
				switch (match.Value.ToLowerInvariant())
				{
				case "perfect":
				case "perfectly":
					text2 = "#74D680";
					break;
				case "wrong":
					text2 = "#FF6B6B";
					break;
				default:
					text2 = "#8ED8FF";
					break;
				}
				string text3 = text2;
				return "<b><color=" + text3 + ">" + match.Value.ToUpperInvariant() + "</color></b>";
			}, RegexOptions.IgnoreCase);
			return "<b><color=#F6A623>RESTAURANT</color></b> <color=" + text + ">•</color> <color=#F2F4F8>" + input + "</color>";
		}

		private void ApplyMessageVisuals(bool isRestaurant)
		{
			if (_messageText != null)
			{
				_messageText.fontSize = (isRestaurant ? 16f : 15f);
			}
			if (_messageShadow != null)
			{
				_messageShadow.enabled = isRestaurant;
			}
		}

		private bool TryGetSenderColorHex(ChatMessageData data, out string colorHex)
		{
			colorHex = string.Empty;
			if (Singleton<PlayersManager>.Instance == null)
			{
				return false;
			}
			if (!Singleton<PlayersManager>.Instance.TryGetPlayerColorBySteamId(data.SenderSteamId, out var color))
			{
				return false;
			}
			colorHex = ColorUtility.ToHtmlStringRGB(color);
			return true;
		}

		private IEnumerator MoveToOldContentRoutine(int version, float delay)
		{
			yield return new WaitForSecondsRealtime(delay);
			_moveCoroutine = null;
			MoveToOldContent(version);
		}

		private void MoveToOldContent(int version)
		{
			if (version != _version)
			{
				return;
			}
			if (_oldContent == null)
			{
				CanDisable = true;
				UtilityMethods.Log("[ChatMessageRowUI] Cannot move row to old content. Old content is missing.", LogType.Warning);
				return;
			}
			if (base.transform.parent != _oldContent)
			{
				base.transform.SetParent(_oldContent, worldPositionStays: false);
				base.transform.SetAsLastSibling();
			}
			CanDisable = true;
			_onLayoutDirty?.Invoke();
		}

		private void StopMoveCoroutine()
		{
			if (_moveCoroutine != null)
			{
				StopCoroutine(_moveCoroutine);
				_moveCoroutine = null;
			}
		}

		private void OnDisable()
		{
			Invalidate();
		}
	}
}
