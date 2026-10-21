namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Runtime.InteropServices;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;
using global::UnityEngine.Events;

public class Blackjack : GameBase
{
	public enum CardAreaType
	{
		Player = 0,
		PlayerSplit = 1,
		Dealer = 2
	}

	private enum BlackjackGameState
	{
		Waiting = 0,
		PlayerTurn = 1,
		DealerTurn = 2,
		Finished = 3
	}

	[Header("UI References")]
	[Header("Card System")]
	[SerializeField]
	private GameObject cardPrefab;

	[SerializeField]
	private Transform playerCardArea;

	[SerializeField]
	private Transform playerSplitCardArea;

	[SerializeField]
	private Transform dealerCardArea;

	[SerializeField]
	private Transform deckOfCardsTransform;

	[SerializeField]
	private int numberOfDecks = 1;

	[SerializeField]
	private float baseScaleForOneDeck = 1f;

	[SerializeField]
	private float cardSpacing = 0.5f;

	[SerializeField]
	private float cardMoveSpeed = 5f;

	[SerializeField]
	private Vector3 splitHandAreaOffset = new Vector3(2f, 0f, 0f);

	[SerializeField]
	private string splitHandAreaName = "PlayerSplitHand";

	[Header("Game State")]
	[SerializeField]
	private readonly SyncList<CardData> playerHand = new SyncList<CardData>();

	[SerializeField]
	private readonly SyncList<CardData> splitHand = new SyncList<CardData>();

	[SerializeField]
	private readonly SyncList<CardData> dealerHand = new SyncList<CardData>();

	[SerializeField]
	private BlackjackGameState gameState;

	[Header("SFX")]
	[SerializeField]
	private SFXComponent resetCardsSfx;

	private List<CardData> deck = new List<CardData>();

	private List<GameObject> spawnedPlayerCards = new List<GameObject>();

	private List<GameObject> spawnedSplitCards = new List<GameObject>();

	private List<GameObject> spawnedDealerCards = new List<GameObject>();

	private int initialDeckCount;

	private bool deckInitialized;

	[SyncVar(hook = "OnDeckScaleChanged")]
	private float deckScaleY = 1f;

	private int hiddenDealerCardIndex = -1;

	private bool hasSplitThisRound;

	private int activeHandIndex;

	private readonly bool[] handCompleted = new bool[2];

	private readonly bool[] handDoubled = new bool[2];

	private readonly long[] handBets = new long[2];

	[SerializeField]
	private UnityEvent rpcOnStartEvent;

	public Action<float, float> _Mirror_SyncVarHookDelegate_deckScaleY;

	public float NetworkdeckScaleY
	{
		get
		{
			return deckScaleY;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref deckScaleY, 8uL, _Mirror_SyncVarHookDelegate_deckScaleY);
		}
	}

	protected override void StartGame()
	{
		base.StartGame();
		rpcOnStartEvent?.Invoke();
		hasSplitThisRound = false;
		activeHandIndex = 0;
		handCompleted[0] = false;
		handCompleted[1] = false;
		handDoubled[0] = false;
		handDoubled[1] = false;
		handBets[0] = currentBet;
		handBets[1] = 0L;
		if (!deckInitialized)
		{
			InitializeDeck();
			initialDeckCount = deck.Count;
			deckInitialized = true;
			if (deckOfCardsTransform != null)
			{
				NetworkdeckScaleY = baseScaleForOneDeck * (float)numberOfDecks;
				deckOfCardsTransform.localScale = new Vector3(deckOfCardsTransform.localScale.x, deckScaleY, deckOfCardsTransform.localScale.z);
			}
		}
		StartCoroutine(DealInitialCardsRoutine());
	}

	private IEnumerator DealInitialCardsRoutine()
	{
		DealCardToPlayer();
		yield return new WaitForSeconds(0.5f);
		DealCardToDealer();
		yield return new WaitForSeconds(0.5f);
		DealCardToPlayer();
		yield return new WaitForSeconds(0.5f);
		DealCardToDealer(isFaceDown: true);
		if (GetHandValue(playerHand) == 21)
		{
			EndGame(BlackjackResult.PlayerBlackjackWin);
		}
		else
		{
			gameState = BlackjackGameState.PlayerTurn;
		}
	}

	[Server]
	public void PlayerHit(PlayerInteract playerInteract)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Blackjack::PlayerHit(PlayerInteract)' called when server was not active");
		}
		else
		{
			if (gameState != BlackjackGameState.PlayerTurn)
			{
				return;
			}
			DealCardToCurrentHand();
			int handValue = GetHandValue(GetCurrentHand());
			if (handValue > 21)
			{
				if (!hasSplitThisRound)
				{
					EndGame(BlackjackResult.PlayerLose);
				}
				else
				{
					CompleteCurrentHandAndAdvance();
				}
			}
			else if (handValue == 21)
			{
				CompleteCurrentHandAndAdvance();
			}
		}
	}

	[Server]
	public void PlayerStand(PlayerInteract playerInteract)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Blackjack::PlayerStand(PlayerInteract)' called when server was not active");
		}
		else if (gameState == BlackjackGameState.PlayerTurn)
		{
			CompleteCurrentHandAndAdvance();
		}
	}

	[Server]
	public void PlayerDouble(PlayerInteract playerInteract)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Blackjack::PlayerDouble(PlayerInteract)' called when server was not active");
		}
		else
		{
			if (gameState != BlackjackGameState.PlayerTurn || handDoubled[activeHandIndex])
			{
				return;
			}
			SyncList<CardData> currentHand = GetCurrentHand();
			if (currentHand.Count == 2 && !(interactingPlayer == null) && NetworkSingleton<MoneyManager>.Instance.TryChangeBalance(-handBets[activeHandIndex], interactingPlayer, ChangeType.Bet))
			{
				handBets[activeHandIndex] *= 2L;
				handDoubled[activeHandIndex] = true;
				base.NetworkcurrentBet = handBets[0] + handBets[1];
				DealCardToCurrentHand();
				if (GetHandValue(currentHand) > 21 && !hasSplitThisRound)
				{
					EndGame(BlackjackResult.PlayerLose);
				}
				else
				{
					CompleteCurrentHandAndAdvance();
				}
			}
		}
	}

	[Server]
	public void PlayerSplit(PlayerInteract playerInteract)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Blackjack::PlayerSplit(PlayerInteract)' called when server was not active");
		}
		else
		{
			if (gameState != BlackjackGameState.PlayerTurn || hasSplitThisRound || playerHand.Count != 2 || playerHand[0].Rank != playerHand[1].Rank || interactingPlayer == null || !NetworkSingleton<MoneyManager>.Instance.TryChangeBalance(-handBets[0], interactingPlayer, ChangeType.Bet))
			{
				return;
			}
			hasSplitThisRound = true;
			handBets[1] = handBets[0];
			base.NetworkcurrentBet = handBets[0] + handBets[1];
			EnsureSplitCardArea();
			CardData item = playerHand[1];
			playerHand.RemoveAt(1);
			splitHand.Add(item);
			if (spawnedPlayerCards.Count > 1)
			{
				GameObject gameObject = spawnedPlayerCards[1];
				spawnedPlayerCards.RemoveAt(1);
				spawnedSplitCards.Add(gameObject);
				Transform cardAreaTransform = GetCardAreaTransform(CardAreaType.PlayerSplit);
				if (cardAreaTransform != null && gameObject != null)
				{
					gameObject.transform.SetParent(cardAreaTransform);
					gameObject.transform.localPosition = CalculateCardPosition(CardAreaType.PlayerSplit, spawnedSplitCards.Count - 1, spawnedSplitCards.Count);
					gameObject.transform.localRotation = Quaternion.identity;
					RpcSetCardParentAndPosition(gameObject, CardAreaType.PlayerSplit, gameObject.transform.localPosition);
				}
			}
			DealCardToHand(playerHand, spawnedPlayerCards, CardAreaType.Player);
			DealCardToHand(splitHand, spawnedSplitCards, CardAreaType.PlayerSplit);
			RepositionAllCards(CardAreaType.Player, spawnedPlayerCards.Count);
			RpcRepositionAllCards(CardAreaType.Player, spawnedPlayerCards.Count);
			RepositionAllCards(CardAreaType.PlayerSplit, spawnedSplitCards.Count);
			RpcRepositionAllCards(CardAreaType.PlayerSplit, spawnedSplitCards.Count);
			activeHandIndex = 0;
			handCompleted[0] = false;
			handCompleted[1] = false;
			UpdateCasinoHelperCounts();
		}
	}

	[Server]
	private void InitializeDeck()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Blackjack::InitializeDeck()' called when server was not active");
			return;
		}
		deck.Clear();
		for (int i = 0; i < numberOfDecks; i++)
		{
			foreach (Suit value in Enum.GetValues(typeof(Suit)))
			{
				foreach (Rank value2 in Enum.GetValues(typeof(Rank)))
				{
					if (value2 != 0)
					{
						deck.Add(new CardData(value, value2));
					}
				}
			}
		}
		ShuffleDeck();
	}

	[Server]
	private void ShuffleDeck()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Blackjack::ShuffleDeck()' called when server was not active");
			return;
		}
		global::System.Random seededRandom = GetSeededRandom(deck.Count * 10000);
		for (int num = deck.Count - 1; num > 0; num--)
		{
			int index = seededRandom.Next(0, num + 1);
			CardData value = deck[num];
			deck[num] = deck[index];
			deck[index] = value;
		}
	}

	[Server]
	private CardData DrawCardFromDeck()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'CardData Blackjack::DrawCardFromDeck()' called when server was not active");
			return default;
		}
		if (deck.Count == 0)
		{
			Debug.LogWarning("[Blackjack] Deck is empty! Reinitializing...");
			InitializeDeck();
			initialDeckCount = deck.Count;
			if (deckOfCardsTransform != null)
			{
				NetworkdeckScaleY = baseScaleForOneDeck * (float)numberOfDecks;
			}
		}
		CardData result = deck[0];
		deck.RemoveAt(0);
		if (deckOfCardsTransform != null)
		{
			if (deck.Count == 0)
			{
				NetworkdeckScaleY = -0.1f;
				return result;
			}
			if (initialDeckCount > 0)
			{
				float num = (float)deck.Count / (float)initialDeckCount;
				NetworkdeckScaleY = baseScaleForOneDeck * (float)numberOfDecks * num;
			}
		}
		return result;
	}

	private void OnDeckScaleChanged(float oldValue, float newValue)
	{
		if (deckOfCardsTransform != null)
		{
			deckOfCardsTransform.localScale = new Vector3(deckOfCardsTransform.localScale.x, newValue, deckOfCardsTransform.localScale.z);
		}
	}

	[Server]
	private void DealCardToPlayer(bool isFaceDown = false)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Blackjack::DealCardToPlayer(System.Boolean)' called when server was not active");
		}
		else
		{
			DealCardToHand(playerHand, spawnedPlayerCards, CardAreaType.Player, isFaceDown);
		}
	}

	[Server]
	private void DealCardToDealer(bool isFaceDown = false)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Blackjack::DealCardToDealer(System.Boolean)' called when server was not active");
			return;
		}
		CardData cardData = DrawCardFromDeck();
		dealerHand.Add(cardData);
		SpawnCard(cardData, CardAreaType.Dealer, spawnedDealerCards, isFaceDown);
		if (isFaceDown)
		{
			hiddenDealerCardIndex = dealerHand.Count - 1;
		}
		UpdateCasinoHelperCounts();
	}

	[Server]
	private void DealCardToHand(SyncList<CardData> hand, List<GameObject> cardList, CardAreaType areaType, bool isFaceDown = false)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Blackjack::DealCardToHand(Mirror.SyncList`1<CardData>,System.Collections.Generic.List`1<UnityEngine.GameObject>,Blackjack/CardAreaType,System.Boolean)' called when server was not active");
			return;
		}
		CardData cardData = DrawCardFromDeck();
		hand.Add(cardData);
		SpawnCard(cardData, areaType, cardList, isFaceDown);
		UpdateCasinoHelperCounts();
	}

	[Server]
	private void SpawnCard(CardData cardData, CardAreaType areaType, List<GameObject> cardList, bool isHidden)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Blackjack::SpawnCard(CardData,Blackjack/CardAreaType,System.Collections.Generic.List`1<UnityEngine.GameObject>,System.Boolean)' called when server was not active");
			return;
		}
		if (SushiAndFish.Casino.Original.CasinoResources.Load<CardDataSO>($"Card_{cardData.Suit}_{GetRankName(cardData.Rank)}") == null)
		{
			Debug.LogWarning($"[Blackjack] Could not load CardDataSO: Card_{cardData.Suit}_{GetRankName(cardData.Rank)}");
		}
		Vector3 vector = CalculateCardPosition(areaType, cardList.Count, cardList.Count + 1);
		GameObject gameObject = UnityEngine.Object.Instantiate(cardPrefab);
		NetworkServer.Spawn(gameObject);
		Card component = gameObject.GetComponent<Card>();
		if (component != null)
		{
			component.ServerSetCardData(cardData);
		}
		Transform cardAreaTransform = GetCardAreaTransform(areaType);
		if (cardAreaTransform != null)
		{
			gameObject.transform.SetParent(cardAreaTransform);
			gameObject.transform.localPosition = vector;
			gameObject.transform.localRotation = Quaternion.identity;
		}
		RpcSetCardParentAndPosition(gameObject, areaType, vector);
		cardList.Add(gameObject);
		RepositionAllCards(areaType, cardList.Count);
		RpcRepositionAllCards(areaType, cardList.Count);
		if (isHidden)
		{
			component?.RpcSetFaceDown(faceDown: true);
		}
		else
		{
			component?.RpcSetFaceDown(faceDown: false);
		}
	}

	[ClientRpc]
	private void RpcSetCardParentAndPosition(GameObject cardObject, CardAreaType areaType, Vector3 cardLocalPosition)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteGameObject(cardObject);
		GeneratedNetworkCode._Write_Blackjack_002FCardAreaType(writer, areaType);
		writer.WriteVector3(cardLocalPosition);
		SendRPCInternal("System.Void Blackjack::RpcSetCardParentAndPosition(UnityEngine.GameObject,Blackjack/CardAreaType,UnityEngine.Vector3)", 691033185, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	private Vector3 CalculateCardPosition(CardAreaType areaType, int cardIndex, int totalCardCount)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'UnityEngine.Vector3 Blackjack::CalculateCardPosition(Blackjack/CardAreaType,System.Int32,System.Int32)' called when server was not active");
			return default;
		}
		if (totalCardCount <= 1)
		{
			return Vector3.zero;
		}
		float num = (0f - (float)(totalCardCount - 1) * cardSpacing) / 2f + (float)cardIndex * cardSpacing;
		return Vector3.right * num;
	}

	[Server]
	private void RepositionAllCards(CardAreaType areaType, int totalCardCount)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Blackjack::RepositionAllCards(Blackjack/CardAreaType,System.Int32)' called when server was not active");
			return;
		}
		List<GameObject> list = ((areaType == CardAreaType.Player) ? spawnedPlayerCards : spawnedDealerCards);
		if (areaType == CardAreaType.PlayerSplit)
		{
			list = spawnedSplitCards;
		}
		Transform cardAreaTransform = GetCardAreaTransform(areaType);
		if (cardAreaTransform == null)
		{
			return;
		}
		List<Transform> list2 = new List<Transform>();
		for (int i = 0; i < list.Count && i < totalCardCount; i++)
		{
			if (list[i] != null)
			{
				list2.Add(list[i].transform);
			}
		}
		StartCoroutine(RepositionCardsSmoothRoutine(list2, cardAreaTransform, areaType, totalCardCount));
	}

	[ClientRpc]
	private void RpcRepositionAllCards(CardAreaType areaType, int totalCardCount)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		GeneratedNetworkCode._Write_Blackjack_002FCardAreaType(writer, areaType);
		writer.WriteVarInt(totalCardCount);
		SendRPCInternal("System.Void Blackjack::RpcRepositionAllCards(Blackjack/CardAreaType,System.Int32)", 1910415189, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private IEnumerator RepositionCardsSmoothRoutine(List<Transform> cardTransforms, Transform cardArea, CardAreaType areaType, int totalCardCount)
	{
		for (int i = 0; i < cardTransforms.Count && i < totalCardCount; i++)
		{
			if (cardTransforms[i] != null)
			{
				Vector3 targetPosition = CalculateCardPositionLocal(cardArea, areaType, i, totalCardCount);
				StartCoroutine(MoveCardSmoothRoutine(cardTransforms[i], targetPosition));
			}
		}
		yield return null;
	}

	private Vector3 CalculateCardPositionLocal(Transform cardArea, CardAreaType areaType, int cardIndex, int totalCardCount)
	{
		if (totalCardCount <= 1)
		{
			return Vector3.zero;
		}
		float num = (0f - (float)(totalCardCount - 1) * cardSpacing) / 2f + (float)cardIndex * cardSpacing;
		return Vector3.right * num;
	}

	private IEnumerator MoveCardSmoothRoutine(Transform cardTransform, Vector3 targetPosition)
	{
		while (cardTransform != null && Vector3.Distance(cardTransform.localPosition, targetPosition) > 0.01f)
		{
			cardTransform.localPosition = Vector3.MoveTowards(cardTransform.localPosition, targetPosition, cardMoveSpeed * Time.deltaTime);
			yield return null;
		}
		if (cardTransform != null) cardTransform.localPosition = targetPosition;
	}

	private string GetRankName(Rank rank)
	{
		switch (rank)
		{
		case Rank.Ace:
			return "Ace";
		case Rank.Jack:
			return "Jack";
		case Rank.Queen:
			return "Queen";
		case Rank.King:
			return "King";
		default:
		{
			int num = (int)rank;
			return num.ToString();
		}
		}
	}

	private IEnumerator DealerPlay()
	{
		yield return new WaitForSeconds(1f);
		if (spawnedDealerCards.Count > 1 && spawnedDealerCards[1] != null)
		{
			Card component = spawnedDealerCards[1].GetComponent<Card>();
			if (component != null)
			{
				component.RpcSetFaceDown(faceDown: false);
			}
		}
		hiddenDealerCardIndex = -1;
		UpdateCasinoHelperCounts();
		yield return new WaitForSeconds(1f);
		while (GetHandValue(dealerHand) < 17)
		{
			DealCardToDealer();
			yield return new WaitForSeconds(1f);
		}
		int handValue = GetHandValue(dealerHand);
		int handValue2 = GetHandValue(playerHand);
		if (hasSplitThisRound)
		{
			long num = 0L;
			num += CalculateHandPayout(playerHand, handValue, handBets[0], allowBlackjackPayout: false);
			num += CalculateHandPayout(splitHand, handValue, handBets[1], allowBlackjackPayout: false);
			EndGameWithPayout(num);
			yield break;
		}
		if (handValue2 > 21)
		{
		}
		BlackjackResult result = ((handValue <= 21) ? ((handValue > handValue2) ? BlackjackResult.PlayerLose : ((handValue2 <= handValue) ? BlackjackResult.Push : BlackjackResult.PlayerWin)) : BlackjackResult.PlayerWin);
		EndGame(result);
	}

	[Server]
	private void EndGame(BlackjackResult result)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Blackjack::EndGame(BlackjackResult)' called when server was not active");
			return;
		}
		gameState = BlackjackGameState.Finished;
		int handValue = GetHandValue(playerHand);
		int handValue2 = GetHandValue(dealerHand);
		Dictionary<string, object> gameSpecificData = new Dictionary<string, object>
		{
			{ "playerHandValue", handValue },
			{ "dealerHandValue", handValue2 }
		};
		switch (result)
		{
		case BlackjackResult.PlayerBlackjackWin:
			Payout(3.0 * base.EstimatedValue, ChangeType.GameResult, gameSpecificData, -1L);
			break;
		case BlackjackResult.PlayerWin:
			Payout(2.0 * base.EstimatedValue, ChangeType.GameResult, gameSpecificData, -1L);
			break;
		case BlackjackResult.PlayerLose:
			Payout(0.0, ChangeType.GameResult, gameSpecificData, -1L);
			break;
		case BlackjackResult.Push:
			Payout(1.0, ChangeType.Misc, gameSpecificData, -1L);
			break;
		}
		StartCoroutine(ResetGameRoutine());
	}

	private IEnumerator ResetGameRoutine()
	{
		yield return new WaitForSeconds(1f);
		ResetGame();
	}

	[Server]
	protected override void ResetGame()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Blackjack::ResetGame()' called when server was not active");
			return;
		}
		base.ResetGame();
		playerHand.Clear();
		splitHand.Clear();
		dealerHand.Clear();
		gameState = BlackjackGameState.Waiting;
		hiddenDealerCardIndex = -1;
		hasSplitThisRound = false;
		activeHandIndex = 0;
		handCompleted[0] = false;
		handCompleted[1] = false;
		handDoubled[0] = false;
		handDoubled[1] = false;
		handBets[0] = 0L;
		handBets[1] = 0L;
		CleanupCards(spawnedPlayerCards);
		CleanupCards(spawnedSplitCards);
		CleanupCards(spawnedDealerCards);
		spawnedPlayerCards.Clear();
		spawnedSplitCards.Clear();
		spawnedDealerCards.Clear();
		RpcClearCasinoHelperTexts();
		resetCardsSfx.RpcPlayOneShotWithCustom3DPos(dealerCardArea.position);
	}

	[Server]
	private void CleanupCards(List<GameObject> cardList)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Blackjack::CleanupCards(System.Collections.Generic.List`1<UnityEngine.GameObject>)' called when server was not active");
			return;
		}
		foreach (GameObject card in cardList)
		{
			if (card != null)
			{
				NetworkServer.Destroy(card);
			}
		}
	}

	private int GetHandValue(SyncList<CardData> hand)
	{
		int num = 0;
		int num2 = 0;
		foreach (CardData item in hand)
		{
			if (item.Rank == Rank.Ace)
			{
				num2++;
				num += 11;
			}
			else
			{
				num += item.GetBlackjackValue();
			}
		}
		while (num > 21 && num2 > 0)
		{
			num -= 10;
			num2--;
		}
		return num;
	}

	[Server]
	private void UpdateCasinoHelperCounts()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Blackjack::UpdateCasinoHelperCounts()' called when server was not active");
			return;
		}
		int handValue = GetHandValue(GetCurrentHand());
		int dealerTotal = ((hiddenDealerCardIndex >= 0) ? GetHandValueExcludingIndex(dealerHand, hiddenDealerCardIndex) : GetHandValue(dealerHand));
		RpcUpdateCasinoHelperCounts(handValue, dealerTotal);
	}

	[ClientRpc]
	private void RpcUpdateCasinoHelperCounts(int playerTotal, int dealerTotal)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(playerTotal);
		writer.WriteVarInt(dealerTotal);
		SendRPCInternal("System.Void Blackjack::RpcUpdateCasinoHelperCounts(System.Int32,System.Int32)", 1511639462, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcClearCasinoHelperTexts()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Blackjack::RpcClearCasinoHelperTexts()", -1603687594, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private int GetHandValueExcludingIndex(SyncList<CardData> hand, int excludedIndex)
	{
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < hand.Count; i++)
		{
			if (i != excludedIndex)
			{
				CardData cardData = hand[i];
				if (cardData.Rank == Rank.Ace)
				{
					num2++;
					num += 11;
				}
				else
				{
					num += cardData.GetBlackjackValue();
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

	private SyncList<CardData> GetCurrentHand()
	{
		if (activeHandIndex != 0)
		{
			return splitHand;
		}
		return playerHand;
	}

	private List<GameObject> GetCurrentHandCards()
	{
		if (activeHandIndex != 0)
		{
			return spawnedSplitCards;
		}
		return spawnedPlayerCards;
	}

	private CardAreaType GetCurrentHandAreaType()
	{
		if (activeHandIndex != 0)
		{
			return CardAreaType.PlayerSplit;
		}
		return CardAreaType.Player;
	}

	[Server]
	private void DealCardToCurrentHand()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Blackjack::DealCardToCurrentHand()' called when server was not active");
		}
		else
		{
			DealCardToHand(GetCurrentHand(), GetCurrentHandCards(), GetCurrentHandAreaType());
		}
	}

	[Server]
	private void CompleteCurrentHandAndAdvance()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Blackjack::CompleteCurrentHandAndAdvance()' called when server was not active");
			return;
		}
		handCompleted[activeHandIndex] = true;
		if (hasSplitThisRound && activeHandIndex == 0)
		{
			activeHandIndex = 1;
			UpdateCasinoHelperCounts();
		}
		else
		{
			gameState = BlackjackGameState.DealerTurn;
			StartCoroutine(DealerPlay());
		}
	}

	private long CalculateHandPayout(SyncList<CardData> hand, int dealerValue, long handBet, bool allowBlackjackPayout)
	{
		if (handBet <= 0)
		{
			return 0L;
		}
		int handValue = GetHandValue(hand);
		if (handValue > 21)
		{
			return 0L;
		}
		bool flag = dealerValue > 21;
		if (allowBlackjackPayout && hand.Count == 2 && handValue == 21)
		{
			return (long)Math.Round((double)(handBet * 3) * base.EstimatedValue);
		}
		if (flag || handValue > dealerValue)
		{
			return (long)Math.Round((double)(handBet * 2) * base.EstimatedValue);
		}
		if (handValue < dealerValue)
		{
			return 0L;
		}
		return handBet;
	}

	[Server]
	private void EndGameWithPayout(long payout)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Blackjack::EndGameWithPayout(System.Int64)' called when server was not active");
			return;
		}
		gameState = BlackjackGameState.Finished;
		int handValue = GetHandValue(playerHand);
		int handValue2 = GetHandValue(splitHand);
		int handValue3 = GetHandValue(dealerHand);
		Dictionary<string, object> gameSpecificData = new Dictionary<string, object>
		{
			{ "playerHandValue", handValue },
			{ "splitHandValue", handValue2 },
			{ "dealerHandValue", handValue3 }
		};
		Payout(payout, ChangeType.GameResult, gameSpecificData, -1L);
		StartCoroutine(ResetGameRoutine());
	}

	private Transform GetCardAreaTransform(CardAreaType areaType)
	{
		switch (areaType)
		{
		case CardAreaType.Player:
			return playerCardArea;
		case CardAreaType.PlayerSplit:
			if (!(playerSplitCardArea != null))
			{
				return playerCardArea;
			}
			return playerSplitCardArea;
		case CardAreaType.Dealer:
			return dealerCardArea;
		default:
			return null;
		}
	}

	[Server]
	private void EnsureSplitCardArea()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Blackjack::EnsureSplitCardArea()' called when server was not active");
		}
		else if (!(playerSplitCardArea != null) && !(playerCardArea == null))
		{
			Transform parent = playerCardArea.parent;
			GameObject gameObject = new GameObject(splitHandAreaName);
			gameObject.transform.SetParent(parent);
			gameObject.transform.localPosition = playerCardArea.localPosition + splitHandAreaOffset;
			gameObject.transform.localRotation = playerCardArea.localRotation;
			gameObject.transform.localScale = playerCardArea.localScale;
			playerSplitCardArea = gameObject.transform;
			RpcEnsureSplitCardArea(splitHandAreaName, playerSplitCardArea.localPosition, playerSplitCardArea.localRotation, playerSplitCardArea.localScale);
		}
	}

	[ClientRpc]
	private void RpcEnsureSplitCardArea(string areaName, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteString(areaName);
		writer.WriteVector3(localPosition);
		writer.WriteQuaternion(localRotation);
		writer.WriteVector3(localScale);
		SendRPCInternal("System.Void Blackjack::RpcEnsureSplitCardArea(System.String,UnityEngine.Vector3,UnityEngine.Quaternion,UnityEngine.Vector3)", 1104917024, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public Blackjack()
	{
		InitSyncObject(playerHand);
		InitSyncObject(splitHand);
		InitSyncObject(dealerHand);
		_Mirror_SyncVarHookDelegate_deckScaleY = OnDeckScaleChanged;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcSetCardParentAndPosition__GameObject__CardAreaType__Vector3(GameObject cardObject, CardAreaType areaType, Vector3 cardLocalPosition)
	{
		Transform cardAreaTransform = GetCardAreaTransform(areaType);
		if (cardAreaTransform != null && cardObject != null)
		{
			cardObject.transform.SetParent(cardAreaTransform);
			cardObject.transform.localPosition = cardLocalPosition;
			cardObject.transform.localRotation = Quaternion.identity;
		}
	}

	protected static void InvokeUserCode_RpcSetCardParentAndPosition__GameObject__CardAreaType__Vector3(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetCardParentAndPosition called on server.");
		}
		else
		{
			((Blackjack)obj).UserCode_RpcSetCardParentAndPosition__GameObject__CardAreaType__Vector3(reader.ReadGameObject(), GeneratedNetworkCode._Read_Blackjack_002FCardAreaType(reader), reader.ReadVector3());
		}
	}

	protected void UserCode_RpcRepositionAllCards__CardAreaType__Int32(CardAreaType areaType, int totalCardCount)
	{
		Transform cardAreaTransform = GetCardAreaTransform(areaType);
		if (cardAreaTransform == null)
		{
			return;
		}
		List<Transform> list = new List<Transform>();
		for (int i = 0; i < cardAreaTransform.childCount; i++)
		{
			Transform child = cardAreaTransform.GetChild(i);
			if (child.GetComponent<Card>() != null)
			{
				list.Add(child);
			}
		}
		list.Sort((Transform a, Transform b) => a.localPosition.x.CompareTo(b.localPosition.x));
		StartCoroutine(RepositionCardsSmoothRoutine(list, cardAreaTransform, areaType, totalCardCount));
	}

	protected static void InvokeUserCode_RpcRepositionAllCards__CardAreaType__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcRepositionAllCards called on server.");
		}
		else
		{
			((Blackjack)obj).UserCode_RpcRepositionAllCards__CardAreaType__Int32(GeneratedNetworkCode._Read_Blackjack_002FCardAreaType(reader), reader.ReadVarInt());
		}
	}

	protected void UserCode_RpcUpdateCasinoHelperCounts__Int32__Int32(int playerTotal, int dealerTotal)
	{
		if (!base.IsCasinoHelperEnabled)
		{
			ClearCasinoHelperTexts();
			return;
		}
		SetCasinoHelperText(0, playerTotal.ToString());
		SetCasinoHelperText(1, dealerTotal.ToString());
	}

	protected static void InvokeUserCode_RpcUpdateCasinoHelperCounts__Int32__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcUpdateCasinoHelperCounts called on server.");
		}
		else
		{
			((Blackjack)obj).UserCode_RpcUpdateCasinoHelperCounts__Int32__Int32(reader.ReadVarInt(), reader.ReadVarInt());
		}
	}

	protected void UserCode_RpcClearCasinoHelperTexts()
	{
		ClearCasinoHelperTexts();
	}

	protected static void InvokeUserCode_RpcClearCasinoHelperTexts(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcClearCasinoHelperTexts called on server.");
		}
		else
		{
			((Blackjack)obj).UserCode_RpcClearCasinoHelperTexts();
		}
	}

	protected void UserCode_RpcEnsureSplitCardArea__String__Vector3__Quaternion__Vector3(string areaName, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
	{
		if (!(playerSplitCardArea != null) && !(playerCardArea == null))
		{
			Transform parent = playerCardArea.parent;
			Transform transform = ((parent != null) ? parent.Find(areaName) : null);
			if (transform != null)
			{
				playerSplitCardArea = transform;
				return;
			}
			GameObject gameObject = new GameObject(areaName);
			gameObject.transform.SetParent(parent);
			gameObject.transform.localPosition = localPosition;
			gameObject.transform.localRotation = localRotation;
			gameObject.transform.localScale = localScale;
			playerSplitCardArea = gameObject.transform;
		}
	}

	protected static void InvokeUserCode_RpcEnsureSplitCardArea__String__Vector3__Quaternion__Vector3(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcEnsureSplitCardArea called on server.");
		}
		else
		{
			((Blackjack)obj).UserCode_RpcEnsureSplitCardArea__String__Vector3__Quaternion__Vector3(reader.ReadString(), reader.ReadVector3(), reader.ReadQuaternion(), reader.ReadVector3());
		}
	}

	static Blackjack()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(Blackjack), "System.Void Blackjack::RpcSetCardParentAndPosition(UnityEngine.GameObject,Blackjack/CardAreaType,UnityEngine.Vector3)", InvokeUserCode_RpcSetCardParentAndPosition__GameObject__CardAreaType__Vector3);
		RemoteProcedureCalls.RegisterRpc(typeof(Blackjack), "System.Void Blackjack::RpcRepositionAllCards(Blackjack/CardAreaType,System.Int32)", InvokeUserCode_RpcRepositionAllCards__CardAreaType__Int32);
		RemoteProcedureCalls.RegisterRpc(typeof(Blackjack), "System.Void Blackjack::RpcUpdateCasinoHelperCounts(System.Int32,System.Int32)", InvokeUserCode_RpcUpdateCasinoHelperCounts__Int32__Int32);
		RemoteProcedureCalls.RegisterRpc(typeof(Blackjack), "System.Void Blackjack::RpcClearCasinoHelperTexts()", InvokeUserCode_RpcClearCasinoHelperTexts);
		RemoteProcedureCalls.RegisterRpc(typeof(Blackjack), "System.Void Blackjack::RpcEnsureSplitCardArea(System.String,UnityEngine.Vector3,UnityEngine.Quaternion,UnityEngine.Vector3)", InvokeUserCode_RpcEnsureSplitCardArea__String__Vector3__Quaternion__Vector3);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteFloat(deckScaleY);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 8L) != 0L)
		{
			writer.WriteFloat(deckScaleY);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref deckScaleY, _Mirror_SyncVarHookDelegate_deckScaleY, reader.ReadFloat());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 8L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref deckScaleY, _Mirror_SyncVarHookDelegate_deckScaleY, reader.ReadFloat());
		}
	}
}

}
