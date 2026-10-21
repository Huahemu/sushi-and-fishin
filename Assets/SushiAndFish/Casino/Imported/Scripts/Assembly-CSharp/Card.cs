namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Runtime.InteropServices;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class Card : NetworkBehaviour
{
	[Header("Card Data")]
	[SerializeField]
	[SyncVar(hook = "OnCardDataChanged")]
	private CardData cardData;

	[SerializeField]
	private SpriteRenderer spriteRenderer;

	[Header("SFX")]
	[SerializeField]
	private EventReference sFXEventGenericSwipe;

	private CardDataSO cardDataSO;

	private bool isFaceDown;

	public Action<CardData, CardData> _Mirror_SyncVarHookDelegate_cardData;

	public CardData NetworkcardData
	{
		get
		{
			return cardData;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref cardData, 1uL, _Mirror_SyncVarHookDelegate_cardData);
		}
	}

	private void OnCardDataChanged(CardData oldValue, CardData newValue)
	{
		NetworkcardData = newValue;
		LoadCardDataSO();
		UpdateCardSprite();
	}

	private void LoadCardDataSO()
	{
		if (cardData.Suit != Suit.Hearts || cardData.Rank != 0)
		{
			string rankName = GetRankName(cardData.Rank);
			string text = $"Card_{cardData.Suit}_{rankName}";
			cardDataSO = SushiAndFish.Casino.Original.CasinoResources.Load<CardDataSO>(text);
			if (cardDataSO == null)
			{
				Debug.LogWarning("[Card] Could not load CardDataSO: " + text);
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

	private void UpdateCardSprite()
	{
		if (!(spriteRenderer == null))
		{
			if (isFaceDown || cardDataSO == null || cardDataSO.cardSprite == null)
			{
				spriteRenderer.sprite = null;
			}
			else
			{
				spriteRenderer.sprite = cardDataSO.cardSprite;
			}
		}
	}

	protected override void OnValidate()
	{
		base.OnValidate();
		if (cardData.Suit != Suit.Hearts || cardData.Rank != 0)
		{
			LoadCardDataSO();
			UpdateCardSprite();
		}
	}

	[Server]
	public void ServerSetCardData(CardData newCardData)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Card::ServerSetCardData(CardData)' called when server was not active");
		}
		else
		{
			NetworkcardData = newCardData;
		}
	}

	[ClientRpc]
	public void RpcSetFaceDown(bool faceDown)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(faceDown);
		SendRPCInternal("System.Void Card::RpcSetFaceDown(System.Boolean)", -690581388, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public void SetFaceDownDirect(bool faceDown)
	{
		isFaceDown = faceDown;
		UpdateCardSprite();
	}

	public Card()
	{
		_Mirror_SyncVarHookDelegate_cardData = OnCardDataChanged;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcSetFaceDown__Boolean(bool faceDown)
	{
		SFXManager.SFXOneShot(sFXEventGenericSwipe, base.transform.position);
		isFaceDown = faceDown;
		UpdateCardSprite();
	}

	protected static void InvokeUserCode_RpcSetFaceDown__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetFaceDown called on server.");
		}
		else
		{
			((Card)obj).UserCode_RpcSetFaceDown__Boolean(reader.ReadBool());
		}
	}

	static Card()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(Card), "System.Void Card::RpcSetFaceDown(System.Boolean)", InvokeUserCode_RpcSetFaceDown__Boolean);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			GeneratedNetworkCode._Write_CardData(writer, cardData);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			GeneratedNetworkCode._Write_CardData(writer, cardData);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref cardData, _Mirror_SyncVarHookDelegate_cardData, GeneratedNetworkCode._Read_CardData(reader));
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref cardData, _Mirror_SyncVarHookDelegate_cardData, GeneratedNetworkCode._Read_CardData(reader));
		}
	}
}

}
