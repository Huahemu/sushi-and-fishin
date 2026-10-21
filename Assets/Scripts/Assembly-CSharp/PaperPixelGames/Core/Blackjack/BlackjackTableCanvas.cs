using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Blackjack
{
	[MovedFrom(true, "PaperPixelGames.Core.Blackjack", null, "BlackjackTableUI")]
	public class BlackjackTableCanvas : MonoBehaviour
	{
		[Header("UI References")]
		[SerializeField]
		private GameObject _uiRoot;

		[SerializeField]
		private Slider _betSlider;

		[SerializeField]
		private TextMeshProUGUI _betText;

		[SerializeField]
		private Button _leaveTableButton;

		[SerializeField]
		private Button _playButton;

		[SerializeField]
		private Button _standButton;

		[SerializeField]
		private Button _hitButton;

		private BlackjackTable _table;

		private int _localSeatIndex = -1;

		private bool _isOpen;

		private bool _suppressBetCallback;

		public void Bind(BlackjackTable table)
		{
			_table = table;
			BindButtons();
			if (_uiRoot != null)
			{
				_uiRoot.SetActive(value: false);
			}
		}

		private void OnDestroy()
		{
			UnbindButtons();
		}

		public void Open(int seatIndex)
		{
			_localSeatIndex = seatIndex;
			_isOpen = true;
			if (_uiRoot != null)
			{
				_uiRoot.SetActive(value: true);
			}
			Refresh();
		}

		public void Close(int seatIndex)
		{
			if (_localSeatIndex == seatIndex)
			{
				_isOpen = false;
				_localSeatIndex = -1;
				if (_uiRoot != null)
				{
					_uiRoot.SetActive(value: false);
				}
			}
		}

		public void Refresh()
		{
			if (_isOpen && !(_table == null))
			{
				bool flag = TryGetLocalPlayer(out var localPlayer);
				bool flag2 = _table.Phase == BlackjackTablePhase.Betting;
				bool visible = flag && _table.Phase == BlackjackTablePhase.PlayerTurns && _table.ActiveSeatIndex == _localSeatIndex && localPlayer.HandState == BlackjackHandState.Playing;
				if (_betSlider != null)
				{
					_suppressBetCallback = true;
					_betSlider.minValue = _table.MinimumBet;
					_betSlider.maxValue = _table.MaximumBet;
					_betSlider.wholeNumbers = true;
					_betSlider.value = (flag ? (localPlayer.BetStored / 100) : _table.MinimumBet);
					_betSlider.gameObject.SetActive(flag2);
					_suppressBetCallback = false;
				}
				if (_betText != null)
				{
					_betText.gameObject.SetActive(flag2);
					_betText.text = $"Bahis: ${(flag ? (localPlayer.BetStored / 100) : _table.MinimumBet)}";
				}
				SetButtonVisible(_leaveTableButton, flag2);
				SetButtonVisible(_playButton, flag2);
				SetButtonVisible(_standButton, visible);
				SetButtonVisible(_hitButton, visible);
			}
		}

		public void ShowMessage(string message, NotificationState state = NotificationState.Error)
		{
			if (!string.IsNullOrEmpty(message))
			{
				if (Singleton<NotifyManager>.Instance != null)
				{
					Singleton<NotifyManager>.Instance.ShowToast(message, state, 1.75f);
				}
				else
				{
					Debug.LogWarning("[Blackjack] " + message);
				}
			}
		}

		private bool TryGetLocalPlayer(out BlackjackPlayerState localPlayer)
		{
			for (int i = 0; i < _table.Players.Count; i++)
			{
				BlackjackPlayerState blackjackPlayerState = _table.Players[i];
				if (blackjackPlayerState.SeatIndex == _localSeatIndex)
				{
					localPlayer = blackjackPlayerState;
					return true;
				}
			}
			localPlayer = default;
			return false;
		}

		private void OnBetChanged(float value)
		{
			if (!_suppressBetCallback && !(_table == null))
			{
				int num = Mathf.RoundToInt(value / (float)_table.BetStep) * _table.BetStep;
				_table.ServerSetBetRpc(num);
				if (_betText != null)
				{
					_betText.text = $"Bahis: ${num}";
				}
			}
		}

		private void OnLeaveTableClicked()
		{
			_table?.RequestLocalLeave(_localSeatIndex);
		}

		private void OnPlayClicked()
		{
			_table?.ServerStartRoundRpc();
		}

		private void OnStandClicked()
		{
			_table?.ServerStandRpc();
		}

		private void OnHitClicked()
		{
			_table?.ServerHitRpc();
		}

		private void BindButtons()
		{
			UnbindButtons();
			_betSlider?.onValueChanged.AddListener(OnBetChanged);
			_leaveTableButton?.onClick.AddListener(OnLeaveTableClicked);
			_playButton?.onClick.AddListener(OnPlayClicked);
			_standButton?.onClick.AddListener(OnStandClicked);
			_hitButton?.onClick.AddListener(OnHitClicked);
		}

		private void UnbindButtons()
		{
			_betSlider?.onValueChanged.RemoveListener(OnBetChanged);
			_leaveTableButton?.onClick.RemoveListener(OnLeaveTableClicked);
			_playButton?.onClick.RemoveListener(OnPlayClicked);
			_standButton?.onClick.RemoveListener(OnStandClicked);
			_hitButton?.onClick.RemoveListener(OnHitClicked);
		}

		private static void SetButtonVisible(Button button, bool visible)
		{
			if (button != null)
			{
				button.gameObject.SetActive(visible);
			}
		}
	}
}
