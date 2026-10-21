using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace PaperPixelGames.Core.Blackjack
{
	public class BlackjackCardDisplay : MonoBehaviour
	{
		[Header("Card Prefab")]
		[SerializeField]
		private BlackjackCardView _cardPrefab;

		[SerializeField]
		private Texture2D[] _cardFaceTextures = new Texture2D[52];

		[SerializeField]
		private Texture2D _cardBackTexture;

		[Header("Card Anchors")]
		[FormerlySerializedAs("_dealOrigin")]
		[SerializeField]
		private Transform _cardStackOrigin;

		[SerializeField]
		private Transform _dealerAnchor;

		[SerializeField]
		private Transform[] _playerAnchors = new Transform[1];

		[Header("Hand Value Texts")]
		[SerializeField]
		private TMP_Text _dealerHandValueText;

		[SerializeField]
		private TMP_Text[] _playerHandValueTexts = new TMP_Text[1];

		[Header("Card Placement")]
		[SerializeField]
		private Vector3 _cardSpacing = new Vector3(0.085f, 0.002f, 0f);

		[SerializeField]
		private bool _compactLegacyCardSpacing = true;

		[SerializeField]
		private Vector3 _faceUpEuler;

		[SerializeField]
		private Vector3 _faceDownEuler = new Vector3(180f, 0f, 0f);

		[Header("Deal Animation")]
		[SerializeField]
		[Min(0f)]
		private float _dealMoveDuration = 0.3f;

		[SerializeField]
		private Ease _dealMoveEase = Ease.OutQuad;

		private readonly Dictionary<int, BlackjackCardView> _spawnedCards = new Dictionary<int, BlackjackCardView>();

		private readonly HashSet<int> _visibleCardKeys = new HashSet<int>();

		private BlackjackTable _table;

		public void Bind(BlackjackTable table)
		{
			_table = table;
			if (_playerAnchors == null || _playerAnchors.Length != 1)
			{
				Array.Resize(ref _playerAnchors, 1);
			}
			if (_playerHandValueTexts == null || _playerHandValueTexts.Length != 1)
			{
				Array.Resize(ref _playerHandValueTexts, 1);
			}
		}

		public void Refresh()
		{
			if (_table == null || !_table.IsSpawned)
			{
				return;
			}
			RefreshDealerHandValue();
			RefreshPlayerHandValues();
			if (_cardPrefab == null)
			{
				ClearCards();
				return;
			}
			_visibleCardKeys.Clear();
			for (int i = 0; i < _table.Cards.Count; i++)
			{
				RefreshCard(_table.Cards[i]);
			}
			RemoveMissingCards();
		}

		private void OnDestroy()
		{
			ClearCards();
		}

		private void RefreshCard(BlackjackCardState card)
		{
			Transform anchor = GetAnchor(card.SeatIndex);
			if (!(anchor == null))
			{
				int cardKey = GetCardKey(card);
				_visibleCardKeys.Add(cardKey);
				if (_spawnedCards.TryGetValue(cardKey, out var value) && value != null)
				{
					ApplyCardPresentation(value, card, animateDeal: false);
					return;
				}
				BlackjackCardView blackjackCardView = UnityEngine.Object.Instantiate(_cardPrefab, anchor);
				_spawnedCards[cardKey] = blackjackCardView;
				ApplyCardPresentation(blackjackCardView, card, animateDeal: true);
			}
		}

		private void ApplyCardPresentation(BlackjackCardView cardView, BlackjackCardState card, bool animateDeal)
		{
			Vector3 vector = GetEffectiveCardSpacing() * (int)card.Order;
			Quaternion localRotation = Quaternion.Euler(card.Hidden ? _faceDownEuler : _faceUpEuler);
			cardView.name = (card.Hidden ? "Hidden Card" : GetCardLabel(card));
			Texture2D faceTexture = GetFaceTexture(card);
			cardView.Setup(faceTexture, _cardBackTexture);
			Transform transform = cardView.transform;
			if (!animateDeal)
			{
				transform.localRotation = localRotation;
				return;
			}
			transform.DOKill();
			if (_dealMoveDuration <= 0f)
			{
				transform.localPosition = vector;
				transform.localRotation = localRotation;
				return;
			}
			Transform transform2 = ((_cardStackOrigin != null) ? _cardStackOrigin : _dealerAnchor);
			transform.position = ((transform2 != null) ? transform2.position : transform.parent.position);
			transform.localRotation = localRotation;
			transform.DOLocalMove(vector, _dealMoveDuration).SetEase(_dealMoveEase);
		}

		private Transform GetAnchor(int seatIndex)
		{
			if (seatIndex == -1)
			{
				return _dealerAnchor;
			}
			if (seatIndex < 0 || seatIndex >= _playerAnchors.Length)
			{
				return null;
			}
			return _playerAnchors[seatIndex];
		}

		private Texture2D GetFaceTexture(BlackjackCardState card)
		{
			int num = (int)card.Suit * 13 + card.Rank - 1;
			if (_cardFaceTextures == null || num < 0 || num >= _cardFaceTextures.Length)
			{
				return null;
			}
			return _cardFaceTextures[num];
		}

		private void ClearCards()
		{
			foreach (BlackjackCardView value in _spawnedCards.Values)
			{
				if (!(value == null))
				{
					value.transform.DOKill();
					UnityEngine.Object.Destroy(value.gameObject);
				}
			}
			_spawnedCards.Clear();
			_visibleCardKeys.Clear();
		}

		private void RemoveMissingCards()
		{
			if (_spawnedCards.Count == _visibleCardKeys.Count)
			{
				return;
			}
			List<int> list = null;
			foreach (KeyValuePair<int, BlackjackCardView> spawnedCard in _spawnedCards)
			{
				if (!_visibleCardKeys.Contains(spawnedCard.Key))
				{
					if (list == null)
					{
						list = new List<int>();
					}
					list.Add(spawnedCard.Key);
					if (spawnedCard.Value != null)
					{
						spawnedCard.Value.transform.DOKill();
						UnityEngine.Object.Destroy(spawnedCard.Value.gameObject);
					}
				}
			}
			if (list != null)
			{
				for (int i = 0; i < list.Count; i++)
				{
					_spawnedCards.Remove(list[i]);
				}
			}
		}

		private void RefreshPlayerHandValues()
		{
			if (_playerHandValueTexts == null)
			{
				return;
			}
			for (int i = 0; i < _playerHandValueTexts.Length; i++)
			{
				TMP_Text tMP_Text = _playerHandValueTexts[i];
				if (!(tMP_Text == null))
				{
					bool flag = TryGetPlayerAtSeat(i, out var player) && player.HandValue > 0;
					tMP_Text.gameObject.SetActive(flag);
					if (flag)
					{
						tMP_Text.SetText("{0}", player.HandValue);
					}
				}
			}
		}

		private void RefreshDealerHandValue()
		{
			if (!(_dealerHandValueText == null))
			{
				int visibleDealerHandValue = GetVisibleDealerHandValue(out var visibleCardCount);
				bool flag = visibleCardCount > 0;
				_dealerHandValueText.gameObject.SetActive(flag);
				if (flag)
				{
					_dealerHandValueText.SetText("{0}", visibleDealerHandValue);
				}
			}
		}

		private int GetVisibleDealerHandValue(out int visibleCardCount)
		{
			int num = 0;
			int num2 = 0;
			visibleCardCount = 0;
			for (int i = 0; i < _table.Cards.Count; i++)
			{
				BlackjackCardState blackjackCardState = _table.Cards[i];
				if (blackjackCardState.SeatIndex == -1 && !blackjackCardState.Hidden)
				{
					visibleCardCount++;
					if (blackjackCardState.Rank == 1)
					{
						num += 11;
						num2++;
					}
					else
					{
						num += Mathf.Min(blackjackCardState.Rank, 10);
					}
				}
			}
			while (num > 21 && num2 > 0)
			{
				num -= 10;
				num2--;
			}
			return num;
		}

		private bool TryGetPlayerAtSeat(int seatIndex, out BlackjackPlayerState player)
		{
			for (int i = 0; i < _table.Players.Count; i++)
			{
				BlackjackPlayerState blackjackPlayerState = _table.Players[i];
				if (blackjackPlayerState.SeatIndex == seatIndex)
				{
					player = blackjackPlayerState;
					return true;
				}
			}
			player = default;
			return false;
		}

		private static int GetCardKey(BlackjackCardState card)
		{
			return (card.SeatIndex + 1 << 8) | card.Order;
		}

		private static string GetCardLabel(BlackjackCardState card)
		{
			return string.Format("{0} of {1}", card.Rank switch
			{
				1 => "A", 
				11 => "J", 
				12 => "Q", 
				13 => "K", 
				_ => card.Rank.ToString(), 
			}, card.Suit);
		}

		private Vector3 GetEffectiveCardSpacing()
		{
			if (!_compactLegacyCardSpacing)
			{
				return _cardSpacing;
			}
			if (Mathf.Approximately(_cardSpacing.x, 0.11f) && Mathf.Approximately(_cardSpacing.y, 0.002f) && Mathf.Approximately(_cardSpacing.z, 0f))
			{
				return new Vector3(0.085f, 0.002f, 0f);
			}
			return _cardSpacing;
		}
	}
}
