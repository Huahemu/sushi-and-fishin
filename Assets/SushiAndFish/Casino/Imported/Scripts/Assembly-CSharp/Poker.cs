namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Linq;
using global::System.Runtime.InteropServices;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class Poker : GameBase
{
	private enum PokerGameState
	{
		Waiting = 0,
		Dealing = 1,
		PlayerTurn = 2,
		Replacing = 3,
		Finished = 4
	}

	[Header("Card Interactions")]
	[SerializeField]
	private InteractableBase[] cardInteractions;

	[SerializeField]
	private SpriteRenderer[] lockVisuals;

	[Header("Card System")]
	[SerializeField]
	private GameObject cardPrefab;

	[SerializeField]
	private Transform playerCardArea;

	[SerializeField]
	private Transform discardPileArea;

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
	private float cardDealDelay = 0.3f;

	[Header("Game State")]
	[SerializeField]
	private readonly SyncList<CardData> playerHand = new SyncList<CardData>();

	[SerializeField]
	private readonly SyncList<CardData> cardsToKeep = new SyncList<CardData>();

	private List<CardData> deck = new List<CardData>();

	private List<GameObject> spawnedPlayerCards = new List<GameObject>();

	private List<GameObject> discardPileCards = new List<GameObject>();

	private int initialDeckCount;

	private bool deckInitialized;

	[SyncVar(hook = "OnDeckScaleChanged")]
	private float deckScaleY = 1f;

	[Header("SFX")]
	[SerializeField]
	private SFXComponent dealerSfx;

	[SerializeField]
	private SFXComponent lockCardSfx;

	[SerializeField]
	private SFXComponent resetCardSfx;

	[SerializeField]
	private SFXComponent moveCardSfx;

	private PokerGameState gameState;

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

	[Server]
	public new void TryStartGame(PlayerInteract playerInteract)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Poker::TryStartGame(PlayerInteract)' called when server was not active");
		}
		else if (gameState == PokerGameState.PlayerTurn)
		{
			ConfirmSelection(playerInteract);
		}
		else
		{
			if (!CanGameStart() || isPlaying || currentBet <= 0)
			{
				return;
			}
			if (playerInteract.TryGetComponent<PlayerProfile>(out var component))
			{
				interactingPlayer = component;
			}
			if (interactingPlayer == null)
			{
				return;
			}
			if (!isGoldenChipApplied)
			{
				if (currentBet < base.MinBet || currentBet > base.MaxBet || !NetworkSingleton<MoneyManager>.Instance.TryChangeBalance(-currentBet, interactingPlayer, ChangeType.Bet))
				{
					return;
				}
			}
			else
			{
				isGoldenBet = true;
			}
			isPlaying = true;
			canBet = false;
			interactingPlayer.TryGetComponent<PlayerEnergy>(out var component2);
			if (component2 != null)
			{
				component2.DecreaseEnergy(6.6f);
			}
			StartGame();
			dealerSfx?.RpcPlayOneShotWith3DPos();
		}
	}

	protected override void StartGame()
	{
		base.StartGame();
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
		playerHand.Clear();
		cardsToKeep.Clear();
		gameState = PokerGameState.Dealing;
		StartCoroutine(DealInitialCardsRoutine());
	}

	private IEnumerator DealInitialCardsRoutine()
	{
		WaitForSeconds wfs = new WaitForSeconds(cardDealDelay);
		for (int i = 0; i < 5; i++)
		{
			DealCardToPlayer();
			yield return wfs;
		}
		EnableCardInteractions();
		gameState = PokerGameState.PlayerTurn;
	}

	[Server]
	public void ToggleCardSelection(PlayerInteract playerInteract, int cardIndex)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Poker::ToggleCardSelection(PlayerInteract,System.Int32)' called when server was not active");
		}
		else if (gameState == PokerGameState.PlayerTurn && cardIndex >= 0 && cardIndex < playerHand.Count)
		{
			Debug.Log($"[Poker] ToggleCardSelection: {cardIndex}");
			CardData item = playerHand[cardIndex];
			bool flag = cardsToKeep.Contains(item);
			if (flag)
			{
				cardsToKeep.Remove(item);
				Debug.Log($"[Poker] Player deselected card {cardIndex} ({item.Suit} {item.Rank})");
			}
			else
			{
				cardsToKeep.Add(item);
				Debug.Log($"[Poker] Player selected card {cardIndex} ({item.Suit} {item.Rank}) to keep");
			}
			RpcUpdateCardSelection(cardIndex, !flag);
			RpcUpdateLockVisual(cardIndex, !flag);
			lockCardSfx.RpcPlayOneShotWithCustom3DPos(playerCardArea.transform.position);
		}
	}

	[Server]
	public void ToggleLockVisual(PlayerInteract playerInteract, int cardIndex)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Poker::ToggleLockVisual(PlayerInteract,System.Int32)' called when server was not active");
		}
		else if (cardIndex >= 0 && cardIndex < playerHand.Count && cardInteractions != null && cardIndex < cardInteractions.Length)
		{
			CardData item = playerHand[cardIndex];
			bool flag = cardsToKeep.Contains(item);
			if (cardInteractions[cardIndex] != null)
			{
				cardInteractions[cardIndex].IsInteractable = !flag;
			}
			SetLockVisualAtIndex(cardIndex, flag);
		}
	}

	public void ToggleLockVisualAtIndex(int index)
	{
		if (lockVisuals != null && index >= 0 && index < lockVisuals.Length && lockVisuals[index] != null)
		{
			lockVisuals[index].enabled = !lockVisuals[index].enabled;
		}
	}

	public void SetLockVisualAtIndex(int index, bool enabled)
	{
		if (lockVisuals != null && index >= 0 && index < lockVisuals.Length && lockVisuals[index] != null)
		{
			lockVisuals[index].enabled = enabled;
		}
	}

	[Command(requiresAuthority = false)]
	public void CmdToggleCardSelection(PlayerInteract playerInteract, int cardIndex)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(playerInteract);
		writer.WriteVarInt(cardIndex);
		SendCommandInternal("System.Void Poker::CmdToggleCardSelection(PlayerInteract,System.Int32)", 938464306, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Command(requiresAuthority = false)]
	public void CmdKeepCard0(PlayerInteract playerInteract)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(playerInteract);
		SendCommandInternal("System.Void Poker::CmdKeepCard0(PlayerInteract)", 237579634, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Command(requiresAuthority = false)]
	public void CmdKeepCard1(PlayerInteract playerInteract)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(playerInteract);
		SendCommandInternal("System.Void Poker::CmdKeepCard1(PlayerInteract)", 1706902393, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Command(requiresAuthority = false)]
	public void CmdKeepCard2(PlayerInteract playerInteract)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(playerInteract);
		SendCommandInternal("System.Void Poker::CmdKeepCard2(PlayerInteract)", 589025896, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Command(requiresAuthority = false)]
	public void CmdKeepCard3(PlayerInteract playerInteract)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(playerInteract);
		SendCommandInternal("System.Void Poker::CmdKeepCard3(PlayerInteract)", 1748116687, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Command(requiresAuthority = false)]
	public void CmdKeepCard4(PlayerInteract playerInteract)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(playerInteract);
		SendCommandInternal("System.Void Poker::CmdKeepCard4(PlayerInteract)", 255233166, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void ConfirmSelection(PlayerInteract playerInteract)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Poker::ConfirmSelection(PlayerInteract)' called when server was not active");
		}
		else if (gameState == PokerGameState.PlayerTurn)
		{
			Debug.Log($"[Poker] Player confirmed selection. Keeping {cardsToKeep.Count} cards");
			DisableCardInteractions();
			ResetAllLockVisuals();
			RpcResetAllLockVisuals();
			gameState = PokerGameState.Replacing;
			StartCoroutine(ReplaceCardsRoutine());
		}
	}

	[Command(requiresAuthority = false)]
	public void CmdConfirmSelection(PlayerInteract playerInteract)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(playerInteract);
		SendCommandInternal("System.Void Poker::CmdConfirmSelection(PlayerInteract)", -50022445, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	private IEnumerator ReplaceCardsRoutine()
	{
		WaitForSeconds wfs = new WaitForSeconds(cardDealDelay);
		for (int i = spawnedPlayerCards.Count - 1; i >= 0; i--)
		{
			if (i < playerHand.Count)
			{
				CardData item = playerHand[i];
				if (!cardsToKeep.Contains(item))
				{
					playerHand.RemoveAt(i);
					GameObject gameObject = spawnedPlayerCards[i];
					spawnedPlayerCards.RemoveAt(i);
					discardPileCards.Add(gameObject);
					moveCardSfx.RpcPlayOneShotWithCustom3DPos(playerCardArea.transform.position);
					RpcDiscardCard(gameObject);
					yield return wfs;
				}
			}
		}
		int cardsNeeded = 5 - playerHand.Count;
		for (int i = 0; i < cardsNeeded; i++)
		{
			DealCardToPlayer();
			yield return wfs;
		}
		gameState = PokerGameState.Finished;
		yield return StartCoroutine(EvaluateAndEndGame());
	}

	private IEnumerator EvaluateAndEndGame()
	{
		yield return new WaitForSeconds(1f);
		int num = EvaluatePokerHand(playerHand);
		string handDescription = GetHandDescription(num);
		Debug.Log($"[Poker] Final hand: {handDescription} (Rank: {num})");
		double multiplier = 0.0;
		PokerResult result;
		if (num >= 1)
		{
			result = PokerResult.Win;
			multiplier = (double)GetPayoutMultiplier(num) * base.EstimatedValue;
		}
		else
		{
			result = PokerResult.Lose;
		}
		EndGame(result, handDescription, num, multiplier);
	}

	[Server]
	private void EndGame(PokerResult result, string handDescription, int handRank, double multiplier)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Poker::EndGame(PokerResult,System.String,System.Int32,System.Double)' called when server was not active");
			return;
		}
		gameState = PokerGameState.Finished;
		Dictionary<string, object> gameSpecificData = new Dictionary<string, object>
		{
			{ "handRank", handRank },
			{ "handDescription", handDescription },
			{ "lockedCardsCount", cardsToKeep.Count }
		};
		Payout(multiplier, ChangeType.GameResult, gameSpecificData, -1L);
		StartCoroutine(ResetGameRoutine());
		dealerSfx?.RpcPlayOneShotWith3DPos();
	}

	private IEnumerator ResetGameRoutine()
	{
		yield return new WaitForSeconds(2f);
		ResetGame();
	}

	[Server]
	protected override void ResetGame()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Poker::ResetGame()' called when server was not active");
			return;
		}
		base.ResetGame();
		playerHand.Clear();
		cardsToKeep.Clear();
		gameState = PokerGameState.Waiting;
		CleanupCards(spawnedPlayerCards);
		CleanupCards(discardPileCards);
		spawnedPlayerCards.Clear();
		discardPileCards.Clear();
		ResetAllLockVisuals();
		resetCardSfx.RpcPlayOneShotWithCustom3DPos(playerCardArea.transform.position);
	}

	public void ResetAllLockVisuals()
	{
		if (lockVisuals == null)
		{
			return;
		}
		for (int i = 0; i < lockVisuals.Length; i++)
		{
			if (lockVisuals[i] != null)
			{
				lockVisuals[i].enabled = false;
			}
		}
	}

	[Server]
	private void CleanupCards(List<GameObject> cardList)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Poker::CleanupCards(System.Collections.Generic.List`1<UnityEngine.GameObject>)' called when server was not active");
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

	[Server]
	private void InitializeDeck()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Poker::InitializeDeck()' called when server was not active");
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
			Debug.LogWarning("[Server] function 'System.Void Poker::ShuffleDeck()' called when server was not active");
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
			Debug.LogWarning("[Server] function 'CardData Poker::DrawCardFromDeck()' called when server was not active");
			return default;
		}
		if (deck.Count == 0)
		{
			Debug.LogWarning("[Poker] Deck is empty! Reinitializing...");
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
			Debug.LogWarning("[Server] function 'System.Void Poker::DealCardToPlayer(System.Boolean)' called when server was not active");
			return;
		}
		CardData cardData = DrawCardFromDeck();
		playerHand.Add(cardData);
		SpawnCard(cardData, spawnedPlayerCards, isFaceDown);
	}

	[Server]
	private void SpawnCard(CardData cardData, List<GameObject> cardList, bool isHidden)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Poker::SpawnCard(CardData,System.Collections.Generic.List`1<UnityEngine.GameObject>,System.Boolean)' called when server was not active");
			return;
		}
		if (SushiAndFish.Casino.Original.CasinoResources.Load<CardDataSO>($"Card_{cardData.Suit}_{GetRankName(cardData.Rank)}") == null)
		{
			Debug.LogWarning($"[Poker] Could not load CardDataSO: Card_{cardData.Suit}_{GetRankName(cardData.Rank)}");
		}
		Vector3 vector = CalculateCardPosition(cardList.Count, cardList.Count + 1);
		GameObject gameObject = UnityEngine.Object.Instantiate(cardPrefab);
		NetworkServer.Spawn(gameObject);
		Card component = gameObject.GetComponent<Card>();
		if (component != null)
		{
			component.ServerSetCardData(cardData);
		}
		if (playerCardArea != null)
		{
			gameObject.transform.SetParent(playerCardArea);
			gameObject.transform.localPosition = vector;
			gameObject.transform.localRotation = Quaternion.identity;
		}
		RpcSetCardParentAndPosition(gameObject, vector);
		cardList.Add(gameObject);
		RepositionAllCards(cardList.Count);
		RpcRepositionAllCards(cardList.Count);
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
	private void RpcSetCardParentAndPosition(GameObject cardObject, Vector3 cardLocalPosition)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteGameObject(cardObject);
		writer.WriteVector3(cardLocalPosition);
		SendRPCInternal("System.Void Poker::RpcSetCardParentAndPosition(UnityEngine.GameObject,UnityEngine.Vector3)", 1362776838, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	private Vector3 CalculateCardPosition(int cardIndex, int totalCardCount)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'UnityEngine.Vector3 Poker::CalculateCardPosition(System.Int32,System.Int32)' called when server was not active");
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
	private void RepositionAllCards(int totalCardCount)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Poker::RepositionAllCards(System.Int32)' called when server was not active");
		}
		else
		{
			if (playerCardArea == null)
			{
				return;
			}
			List<Transform> list = new List<Transform>();
			for (int i = 0; i < spawnedPlayerCards.Count && i < totalCardCount; i++)
			{
				if (spawnedPlayerCards[i] != null)
				{
					list.Add(spawnedPlayerCards[i].transform);
				}
			}
			StartCoroutine(RepositionCardsSmoothRoutine(list, playerCardArea, totalCardCount));
		}
	}

	[ClientRpc]
	private void RpcRepositionAllCards(int totalCardCount)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(totalCardCount);
		SendRPCInternal("System.Void Poker::RpcRepositionAllCards(System.Int32)", -1431656758, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private IEnumerator RepositionCardsSmoothRoutine(List<Transform> cardTransforms, Transform cardArea, int totalCardCount)
	{
		for (int i = 0; i < cardTransforms.Count && i < totalCardCount; i++)
		{
			if (cardTransforms[i] != null)
			{
				Vector3 targetPosition = CalculateCardPositionLocal(cardArea, i, totalCardCount);
				StartCoroutine(MoveCardSmoothRoutine(cardTransforms[i], targetPosition));
			}
		}
		yield return null;
	}

	private Vector3 CalculateCardPositionLocal(Transform cardArea, int cardIndex, int totalCardCount)
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
		while (cardTransform != null)
		{
			Vector3 localPosition = cardTransform.localPosition;
			if (Vector3.Distance(localPosition, targetPosition) <= 0.01f)
			{
				break;
			}
			cardTransform.localPosition = Vector3.MoveTowards(localPosition, targetPosition, cardMoveSpeed * Time.deltaTime);
			yield return null;
		}
		if (cardTransform != null)
		{
			cardTransform.localPosition = targetPosition;
		}
	}

	[ClientRpc]
	private void RpcDiscardCard(GameObject cardObject)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteGameObject(cardObject);
		SendRPCInternal("System.Void Poker::RpcDiscardCard(UnityEngine.GameObject)", -494646251, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcUpdateCardSelection(int cardIndex, bool isSelected)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(cardIndex);
		writer.WriteBool(isSelected);
		SendRPCInternal("System.Void Poker::RpcUpdateCardSelection(System.Int32,System.Boolean)", 1129187464, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcUpdateLockVisual(int index, bool enabled)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(index);
		writer.WriteBool(enabled);
		SendRPCInternal("System.Void Poker::RpcUpdateLockVisual(System.Int32,System.Boolean)", 1919071727, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcResetAllLockVisuals()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Poker::RpcResetAllLockVisuals()", -155673675, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void EnableCardInteractions()
	{
		if (cardInteractions == null || cardInteractions.Length == 0)
		{
			return;
		}
		InteractableBase[] array = cardInteractions;
		foreach (InteractableBase interactableBase in array)
		{
			if (interactableBase != null)
			{
				interactableBase.IsInteractable = true;
				interactableBase.TooltipMessage = "Press [E] to select";
			}
		}
	}

	private void DisableCardInteractions()
	{
		if (cardInteractions == null || cardInteractions.Length == 0)
		{
			return;
		}
		InteractableBase[] array = cardInteractions;
		foreach (InteractableBase interactableBase in array)
		{
			if (interactableBase != null)
			{
				interactableBase.IsInteractable = false;
			}
		}
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

	private int EvaluatePokerHand(SyncList<CardData> hand)
	{
		if (hand.Count != 5)
		{
			return 0;
		}
		List<int> values = hand.Select((CardData card) => (int)card.Rank).ToList();
		List<Suit> suits = hand.Select((CardData card) => card.Suit).ToList();
		if (IsStraight(values) && IsFlush(suits))
		{
			return 8;
		}
		if (HasFourOfAKind(values))
		{
			return 7;
		}
		if (HasFullHouse(values))
		{
			return 6;
		}
		if (IsFlush(suits))
		{
			return 5;
		}
		if (IsStraight(values))
		{
			return 4;
		}
		if (HasThreeOfAKind(values))
		{
			return 3;
		}
		if (HasTwoPair(values))
		{
			return 2;
		}
		if (HasOnePair(values))
		{
			return 1;
		}
		return 0;
	}

	private bool IsStraight(List<int> values)
	{
		List<int> list = values.OrderBy((int v) => v).ToList();
		for (int num = 1; num < list.Count; num++)
		{
			if (list[num] != list[num - 1] + 1)
			{
				return false;
			}
		}
		return true;
	}

	private bool IsFlush(List<Suit> suits)
	{
		return suits.All((Suit s) => s == suits[0]);
	}

	private bool HasFourOfAKind(List<int> values)
	{
		return (from v in values
			group v by v).Any((IGrouping<int, int> g) => g.Count() == 4);
	}

	private bool HasFullHouse(List<int> values)
	{
		List<IGrouping<int, int>> list = (from v in values
			group v by v).ToList();
		if (list.Count == 2 && list.Any((IGrouping<int, int> g) => g.Count() == 3))
		{
			return list.Any((IGrouping<int, int> g) => g.Count() == 2);
		}
		return false;
	}

	private bool HasThreeOfAKind(List<int> values)
	{
		return (from v in values
			group v by v).Any((IGrouping<int, int> g) => g.Count() == 3);
	}

	private bool HasTwoPair(List<int> values)
	{
		return (from v in values
			group v by v into g
			where g.Count() == 2
			select g).Count() == 2;
	}

	private bool HasOnePair(List<int> values)
	{
		return (from v in values
			group v by v).Any((IGrouping<int, int> g) => g.Count() == 2);
	}

	private int GetPayoutMultiplier(int handRank)
	{
		return handRank switch
		{
			8 => 50, 
			7 => 25, 
			6 => 9, 
			5 => 6, 
			4 => 4, 
			3 => 3, 
			2 => 2, 
			1 => 1, 
			_ => 1, 
		};
	}

	private string GetHandDescription(int handRank)
	{
		return handRank switch
		{
			8 => "Straight Flush", 
			7 => "Four of a Kind", 
			6 => "Full House", 
			5 => "Flush", 
			4 => "Straight", 
			3 => "Three of a Kind", 
			2 => "Two Pair", 
			1 => "One Pair", 
			_ => "High Card", 
		};
	}

	public Poker()
	{
		InitSyncObject(playerHand);
		InitSyncObject(cardsToKeep);
		_Mirror_SyncVarHookDelegate_deckScaleY = OnDeckScaleChanged;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_CmdToggleCardSelection__PlayerInteract__Int32(PlayerInteract playerInteract, int cardIndex)
	{
		ToggleCardSelection(playerInteract, cardIndex);
	}

	protected static void InvokeUserCode_CmdToggleCardSelection__PlayerInteract__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdToggleCardSelection called on client.");
		}
		else
		{
			((Poker)obj).UserCode_CmdToggleCardSelection__PlayerInteract__Int32(reader.ReadNetworkBehaviour<PlayerInteract>(), reader.ReadVarInt());
		}
	}

	protected void UserCode_CmdKeepCard0__PlayerInteract(PlayerInteract playerInteract)
	{
		ToggleCardSelection(playerInteract, 0);
		ToggleLockVisual(playerInteract, 0);
	}

	protected static void InvokeUserCode_CmdKeepCard0__PlayerInteract(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdKeepCard0 called on client.");
		}
		else
		{
			((Poker)obj).UserCode_CmdKeepCard0__PlayerInteract(reader.ReadNetworkBehaviour<PlayerInteract>());
		}
	}

	protected void UserCode_CmdKeepCard1__PlayerInteract(PlayerInteract playerInteract)
	{
		ToggleCardSelection(playerInteract, 1);
		ToggleLockVisual(playerInteract, 1);
	}

	protected static void InvokeUserCode_CmdKeepCard1__PlayerInteract(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdKeepCard1 called on client.");
		}
		else
		{
			((Poker)obj).UserCode_CmdKeepCard1__PlayerInteract(reader.ReadNetworkBehaviour<PlayerInteract>());
		}
	}

	protected void UserCode_CmdKeepCard2__PlayerInteract(PlayerInteract playerInteract)
	{
		ToggleCardSelection(playerInteract, 2);
		ToggleLockVisual(playerInteract, 2);
	}

	protected static void InvokeUserCode_CmdKeepCard2__PlayerInteract(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdKeepCard2 called on client.");
		}
		else
		{
			((Poker)obj).UserCode_CmdKeepCard2__PlayerInteract(reader.ReadNetworkBehaviour<PlayerInteract>());
		}
	}

	protected void UserCode_CmdKeepCard3__PlayerInteract(PlayerInteract playerInteract)
	{
		ToggleCardSelection(playerInteract, 3);
		ToggleLockVisual(playerInteract, 3);
	}

	protected static void InvokeUserCode_CmdKeepCard3__PlayerInteract(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdKeepCard3 called on client.");
		}
		else
		{
			((Poker)obj).UserCode_CmdKeepCard3__PlayerInteract(reader.ReadNetworkBehaviour<PlayerInteract>());
		}
	}

	protected void UserCode_CmdKeepCard4__PlayerInteract(PlayerInteract playerInteract)
	{
		ToggleCardSelection(playerInteract, 4);
		ToggleLockVisual(playerInteract, 4);
	}

	protected static void InvokeUserCode_CmdKeepCard4__PlayerInteract(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdKeepCard4 called on client.");
		}
		else
		{
			((Poker)obj).UserCode_CmdKeepCard4__PlayerInteract(reader.ReadNetworkBehaviour<PlayerInteract>());
		}
	}

	protected void UserCode_CmdConfirmSelection__PlayerInteract(PlayerInteract playerInteract)
	{
		ConfirmSelection(playerInteract);
	}

	protected static void InvokeUserCode_CmdConfirmSelection__PlayerInteract(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdConfirmSelection called on client.");
		}
		else
		{
			((Poker)obj).UserCode_CmdConfirmSelection__PlayerInteract(reader.ReadNetworkBehaviour<PlayerInteract>());
		}
	}

	protected void UserCode_RpcSetCardParentAndPosition__GameObject__Vector3(GameObject cardObject, Vector3 cardLocalPosition)
	{
		if (playerCardArea != null && cardObject != null)
		{
			cardObject.transform.SetParent(playerCardArea);
			cardObject.transform.localPosition = cardLocalPosition;
			cardObject.transform.localRotation = Quaternion.identity;
		}
	}

	protected static void InvokeUserCode_RpcSetCardParentAndPosition__GameObject__Vector3(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetCardParentAndPosition called on server.");
		}
		else
		{
			((Poker)obj).UserCode_RpcSetCardParentAndPosition__GameObject__Vector3(reader.ReadGameObject(), reader.ReadVector3());
		}
	}

	protected void UserCode_RpcRepositionAllCards__Int32(int totalCardCount)
	{
		if (playerCardArea == null)
		{
			return;
		}
		List<Transform> list = new List<Transform>();
		for (int i = 0; i < playerCardArea.childCount; i++)
		{
			Transform child = playerCardArea.GetChild(i);
			if (child.GetComponent<Card>() != null)
			{
				list.Add(child);
			}
		}
		list.Sort((Transform a, Transform b) => a.localPosition.x.CompareTo(b.localPosition.x));
		StartCoroutine(RepositionCardsSmoothRoutine(list, playerCardArea, totalCardCount));
	}

	protected static void InvokeUserCode_RpcRepositionAllCards__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcRepositionAllCards called on server.");
		}
		else
		{
			((Poker)obj).UserCode_RpcRepositionAllCards__Int32(reader.ReadVarInt());
		}
	}

	protected void UserCode_RpcDiscardCard__GameObject(GameObject cardObject)
	{
		if (cardObject != null && discardPileArea != null)
		{
			cardObject.transform.SetParent(discardPileArea);
			cardObject.transform.localPosition = Vector3.zero;
		}
	}

	protected static void InvokeUserCode_RpcDiscardCard__GameObject(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcDiscardCard called on server.");
		}
		else
		{
			((Poker)obj).UserCode_RpcDiscardCard__GameObject(reader.ReadGameObject());
		}
	}

	protected void UserCode_RpcUpdateCardSelection__Int32__Boolean(int cardIndex, bool isSelected)
	{
		if (cardIndex < cardInteractions.Length && cardInteractions[cardIndex] != null)
		{
			if (isSelected)
			{
				cardInteractions[cardIndex].TooltipMessage = "Press [E] to deselect";
			}
			else
			{
				cardInteractions[cardIndex].TooltipMessage = "Press [E] to select";
			}
		}
	}

	protected static void InvokeUserCode_RpcUpdateCardSelection__Int32__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcUpdateCardSelection called on server.");
		}
		else
		{
			((Poker)obj).UserCode_RpcUpdateCardSelection__Int32__Boolean(reader.ReadVarInt(), reader.ReadBool());
		}
	}

	protected void UserCode_RpcUpdateLockVisual__Int32__Boolean(int index, bool enabled)
	{
		SetLockVisualAtIndex(index, enabled);
	}

	protected static void InvokeUserCode_RpcUpdateLockVisual__Int32__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcUpdateLockVisual called on server.");
		}
		else
		{
			((Poker)obj).UserCode_RpcUpdateLockVisual__Int32__Boolean(reader.ReadVarInt(), reader.ReadBool());
		}
	}

	protected void UserCode_RpcResetAllLockVisuals()
	{
		ResetAllLockVisuals();
	}

	protected static void InvokeUserCode_RpcResetAllLockVisuals(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcResetAllLockVisuals called on server.");
		}
		else
		{
			((Poker)obj).UserCode_RpcResetAllLockVisuals();
		}
	}

	static Poker()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(Poker), "System.Void Poker::CmdToggleCardSelection(PlayerInteract,System.Int32)", InvokeUserCode_CmdToggleCardSelection__PlayerInteract__Int32, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(Poker), "System.Void Poker::CmdKeepCard0(PlayerInteract)", InvokeUserCode_CmdKeepCard0__PlayerInteract, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(Poker), "System.Void Poker::CmdKeepCard1(PlayerInteract)", InvokeUserCode_CmdKeepCard1__PlayerInteract, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(Poker), "System.Void Poker::CmdKeepCard2(PlayerInteract)", InvokeUserCode_CmdKeepCard2__PlayerInteract, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(Poker), "System.Void Poker::CmdKeepCard3(PlayerInteract)", InvokeUserCode_CmdKeepCard3__PlayerInteract, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(Poker), "System.Void Poker::CmdKeepCard4(PlayerInteract)", InvokeUserCode_CmdKeepCard4__PlayerInteract, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(Poker), "System.Void Poker::CmdConfirmSelection(PlayerInteract)", InvokeUserCode_CmdConfirmSelection__PlayerInteract, requiresAuthority: false);
		RemoteProcedureCalls.RegisterRpc(typeof(Poker), "System.Void Poker::RpcSetCardParentAndPosition(UnityEngine.GameObject,UnityEngine.Vector3)", InvokeUserCode_RpcSetCardParentAndPosition__GameObject__Vector3);
		RemoteProcedureCalls.RegisterRpc(typeof(Poker), "System.Void Poker::RpcRepositionAllCards(System.Int32)", InvokeUserCode_RpcRepositionAllCards__Int32);
		RemoteProcedureCalls.RegisterRpc(typeof(Poker), "System.Void Poker::RpcDiscardCard(UnityEngine.GameObject)", InvokeUserCode_RpcDiscardCard__GameObject);
		RemoteProcedureCalls.RegisterRpc(typeof(Poker), "System.Void Poker::RpcUpdateCardSelection(System.Int32,System.Boolean)", InvokeUserCode_RpcUpdateCardSelection__Int32__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(Poker), "System.Void Poker::RpcUpdateLockVisual(System.Int32,System.Boolean)", InvokeUserCode_RpcUpdateLockVisual__Int32__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(Poker), "System.Void Poker::RpcResetAllLockVisuals()", InvokeUserCode_RpcResetAllLockVisuals);
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
