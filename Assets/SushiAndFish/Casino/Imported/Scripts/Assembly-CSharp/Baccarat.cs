namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Runtime.InteropServices;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class Baccarat : GameBase
{
	public enum CardAreaType
	{
		Player = 0,
		Dealer = 1
	}

	private enum BaccaratGameState
	{
		Waiting = 0,
		Dealing = 1,
		Finished = 2
	}

	[Header("UI References")]
	[Header("Card System")]
	[SerializeField]
	private GameObject cardPrefab;

	[SerializeField]
	private Transform playerCardArea;

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

	[Header("Game State")]
	[SerializeField]
	private readonly SyncList<CardData> playerHand = new SyncList<CardData>();

	[SerializeField]
	private readonly SyncList<CardData> dealerHand = new SyncList<CardData>();

	[SerializeField]
	private BaccaratGameState gameState;

	[SyncVar]
	private BaccaratBetType currentBetType;

	[Header("SFX")]
	[SerializeField]
	private SFXComponent resetCardsSfx;

	[SerializeField]
	private SFXComponent dealerSfx;

	private List<CardData> deck = new List<CardData>();

	private List<GameObject> spawnedPlayerCards = new List<GameObject>();

	private List<GameObject> spawnedDealerCards = new List<GameObject>();

	private int initialDeckCount;

	private bool deckInitialized;

	[SyncVar(hook = "OnDeckScaleChanged")]
	private float deckScaleY = 1f;

	public Action<float, float> _Mirror_SyncVarHookDelegate_deckScaleY;

	public BaccaratBetType NetworkcurrentBetType
	{
		get
		{
			return currentBetType;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref currentBetType, 8uL, null);
		}
	}

	public float NetworkdeckScaleY
	{
		get
		{
			return deckScaleY;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref deckScaleY, 16uL, _Mirror_SyncVarHookDelegate_deckScaleY);
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
		StartCoroutine(DealInitialCardsRoutine());
		dealerSfx?.PlayOneShotWith3DPos();
	}

	private IEnumerator DealInitialCardsRoutine()
	{
		gameState = BaccaratGameState.Dealing;
		DealCardToPlayer();
		yield return new WaitForSeconds(0.5f);
		DealCardToDealer();
		yield return new WaitForSeconds(0.5f);
		DealCardToPlayer();
		yield return new WaitForSeconds(0.5f);
		DealCardToDealer();
		yield return new WaitForSeconds(2f);
		int handValue = GetHandValue(playerHand);
		int handValue2 = GetHandValue(dealerHand);
		BaccaratResult result = ((handValue > handValue2) ? BaccaratResult.Player : ((handValue2 <= handValue) ? BaccaratResult.Tie : BaccaratResult.Banker));
		EndGame(result);
	}

	[Server]
	private void InitializeDeck()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Baccarat::InitializeDeck()' called when server was not active");
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
			Debug.LogWarning("[Server] function 'System.Void Baccarat::ShuffleDeck()' called when server was not active");
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
			Debug.LogWarning("[Server] function 'CardData Baccarat::DrawCardFromDeck()' called when server was not active");
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
			Debug.LogWarning("[Server] function 'System.Void Baccarat::DealCardToPlayer(System.Boolean)' called when server was not active");
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
			Debug.LogWarning("[Server] function 'System.Void Baccarat::DealCardToDealer(System.Boolean)' called when server was not active");
			return;
		}
		CardData cardData = DrawCardFromDeck();
		dealerHand.Add(cardData);
		SpawnCard(cardData, CardAreaType.Dealer, spawnedDealerCards, isFaceDown);
		UpdateCasinoHelperCounts();
	}

	[Server]
	private void DealCardToHand(SyncList<CardData> hand, List<GameObject> cardList, CardAreaType areaType, bool isFaceDown = false)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Baccarat::DealCardToHand(Mirror.SyncList`1<CardData>,System.Collections.Generic.List`1<UnityEngine.GameObject>,Baccarat/CardAreaType,System.Boolean)' called when server was not active");
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
			Debug.LogWarning("[Server] function 'System.Void Baccarat::SpawnCard(CardData,Baccarat/CardAreaType,System.Collections.Generic.List`1<UnityEngine.GameObject>,System.Boolean)' called when server was not active");
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
		GeneratedNetworkCode._Write_Baccarat_002FCardAreaType(writer, areaType);
		writer.WriteVector3(cardLocalPosition);
		SendRPCInternal("System.Void Baccarat::RpcSetCardParentAndPosition(UnityEngine.GameObject,Baccarat/CardAreaType,UnityEngine.Vector3)", -69855219, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	private Vector3 CalculateCardPosition(CardAreaType areaType, int cardIndex, int totalCardCount)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'UnityEngine.Vector3 Baccarat::CalculateCardPosition(Baccarat/CardAreaType,System.Int32,System.Int32)' called when server was not active");
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
			Debug.LogWarning("[Server] function 'System.Void Baccarat::RepositionAllCards(Baccarat/CardAreaType,System.Int32)' called when server was not active");
			return;
		}
		List<GameObject> list = ((areaType == CardAreaType.Player) ? spawnedPlayerCards : spawnedDealerCards);
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
		GeneratedNetworkCode._Write_Baccarat_002FCardAreaType(writer, areaType);
		writer.WriteVarInt(totalCardCount);
		SendRPCInternal("System.Void Baccarat::RpcRepositionAllCards(Baccarat/CardAreaType,System.Int32)", -2109413821, writer, 0, includeOwner: true);
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
		while (Vector3.Distance(cardTransform.localPosition, targetPosition) > 0.01f)
		{
			cardTransform.localPosition = Vector3.MoveTowards(cardTransform.localPosition, targetPosition, cardMoveSpeed * Time.deltaTime);
			yield return null;
		}
		cardTransform.localPosition = targetPosition;
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

	[Server]
	private void EndGame(BaccaratResult result)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Baccarat::EndGame(BaccaratResult)' called when server was not active");
			return;
		}
		gameState = BaccaratGameState.Finished;
		double multiplier = 0.0;
		switch (currentBetType)
		{
		case BaccaratBetType.Player:
			if (result == BaccaratResult.Player)
			{
				multiplier = 2.0 * base.EstimatedValue;
			}
			break;
		case BaccaratBetType.Banker:
			if (result == BaccaratResult.Banker)
			{
				multiplier = 2.0 * base.EstimatedValue;
			}
			break;
		case BaccaratBetType.Tie:
			if (result == BaccaratResult.Tie)
			{
				multiplier = 8.0 * base.EstimatedValue;
			}
			break;
		}
		Payout(multiplier, ChangeType.GameResult, null, -1L);
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
			Debug.LogWarning("[Server] function 'System.Void Baccarat::ResetGame()' called when server was not active");
			return;
		}
		base.ResetGame();
		playerHand.Clear();
		dealerHand.Clear();
		gameState = BaccaratGameState.Waiting;
		CleanupCards(spawnedPlayerCards);
		CleanupCards(spawnedDealerCards);
		spawnedPlayerCards.Clear();
		spawnedDealerCards.Clear();
		RpcClearCasinoHelperTexts();
		if (resetCardsSfx != null && dealerCardArea != null)
		{
			resetCardsSfx.RpcPlayOneShotWithCustom3DPos(dealerCardArea.position);
		}
	}

	[Server]
	private void CleanupCards(List<GameObject> cardList)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Baccarat::CleanupCards(System.Collections.Generic.List`1<UnityEngine.GameObject>)' called when server was not active");
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
		if (hand.Count == 0)
		{
			return 0;
		}
		int num = 0;
		foreach (CardData item in hand)
		{
			num += item.GetBaccaratValue();
		}
		return num % 10;
	}

	[Server]
	private void UpdateCasinoHelperCounts()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Baccarat::UpdateCasinoHelperCounts()' called when server was not active");
			return;
		}
		int handValue = GetHandValue(playerHand);
		int handValue2 = GetHandValue(dealerHand);
		RpcUpdateCasinoHelperCounts(handValue, handValue2);
	}

	[ClientRpc]
	private void RpcUpdateCasinoHelperCounts(int playerTotal, int dealerTotal)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(playerTotal);
		writer.WriteVarInt(dealerTotal);
		SendRPCInternal("System.Void Baccarat::RpcUpdateCasinoHelperCounts(System.Int32,System.Int32)", -2084194017, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcClearCasinoHelperTexts()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Baccarat::RpcClearCasinoHelperTexts()", 835280477, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private Transform GetCardAreaTransform(CardAreaType areaType)
	{
		return areaType switch
		{
			CardAreaType.Player => playerCardArea, 
			CardAreaType.Dealer => dealerCardArea, 
			_ => null, 
		};
	}

	[Server]
	public void PlaceBetOnPlayer(PlayerInteract playerInteract)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Baccarat::PlaceBetOnPlayer(PlayerInteract)' called when server was not active");
		}
		else if (gameState == BaccaratGameState.Waiting)
		{
			TryStartGame(playerInteract);
			NetworkcurrentBetType = BaccaratBetType.Player;
		}
	}

	[Server]
	public void PlaceBetOnDealer(PlayerInteract playerInteract)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Baccarat::PlaceBetOnDealer(PlayerInteract)' called when server was not active");
		}
		else if (gameState == BaccaratGameState.Waiting)
		{
			TryStartGame(playerInteract);
			NetworkcurrentBetType = BaccaratBetType.Banker;
		}
	}

	[Server]
	public void PlaceBetOnTie(PlayerInteract playerInteract)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Baccarat::PlaceBetOnTie(PlayerInteract)' called when server was not active");
		}
		else if (gameState == BaccaratGameState.Waiting)
		{
			TryStartGame(playerInteract);
			NetworkcurrentBetType = BaccaratBetType.Tie;
		}
	}

	public Baccarat()
	{
		InitSyncObject(playerHand);
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
			((Baccarat)obj).UserCode_RpcSetCardParentAndPosition__GameObject__CardAreaType__Vector3(reader.ReadGameObject(), GeneratedNetworkCode._Read_Baccarat_002FCardAreaType(reader), reader.ReadVector3());
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
			((Baccarat)obj).UserCode_RpcRepositionAllCards__CardAreaType__Int32(GeneratedNetworkCode._Read_Baccarat_002FCardAreaType(reader), reader.ReadVarInt());
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
			((Baccarat)obj).UserCode_RpcUpdateCasinoHelperCounts__Int32__Int32(reader.ReadVarInt(), reader.ReadVarInt());
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
			((Baccarat)obj).UserCode_RpcClearCasinoHelperTexts();
		}
	}

	static Baccarat()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(Baccarat), "System.Void Baccarat::RpcSetCardParentAndPosition(UnityEngine.GameObject,Baccarat/CardAreaType,UnityEngine.Vector3)", InvokeUserCode_RpcSetCardParentAndPosition__GameObject__CardAreaType__Vector3);
		RemoteProcedureCalls.RegisterRpc(typeof(Baccarat), "System.Void Baccarat::RpcRepositionAllCards(Baccarat/CardAreaType,System.Int32)", InvokeUserCode_RpcRepositionAllCards__CardAreaType__Int32);
		RemoteProcedureCalls.RegisterRpc(typeof(Baccarat), "System.Void Baccarat::RpcUpdateCasinoHelperCounts(System.Int32,System.Int32)", InvokeUserCode_RpcUpdateCasinoHelperCounts__Int32__Int32);
		RemoteProcedureCalls.RegisterRpc(typeof(Baccarat), "System.Void Baccarat::RpcClearCasinoHelperTexts()", InvokeUserCode_RpcClearCasinoHelperTexts);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			GeneratedNetworkCode._Write_BaccaratBetType(writer, currentBetType);
			writer.WriteFloat(deckScaleY);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 8L) != 0L)
		{
			GeneratedNetworkCode._Write_BaccaratBetType(writer, currentBetType);
		}
		if ((syncVarDirtyBits & 0x10L) != 0L)
		{
			writer.WriteFloat(deckScaleY);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref currentBetType, null, GeneratedNetworkCode._Read_BaccaratBetType(reader));
			GeneratedSyncVarDeserialize(ref deckScaleY, _Mirror_SyncVarHookDelegate_deckScaleY, reader.ReadFloat());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 8L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref currentBetType, null, GeneratedNetworkCode._Read_BaccaratBetType(reader));
		}
		if ((num & 0x10L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref deckScaleY, _Mirror_SyncVarHookDelegate_deckScaleY, reader.ReadFloat());
		}
	}
}

}
