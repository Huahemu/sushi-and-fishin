namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::MoreMountains.Feedbacks;
using global::UnityEngine;

public class MerchantBuyButton : InteractableBase
{
	[Header("References")]
	[Tooltip("The buy zone that tracks items for purchase.")]
	[SerializeField]
	private MerchantBuyZone buyZone;

	[Tooltip("Item price settings. If null, will load from SushiAndFish.Casino.Original.CasinoResources.")]
	[SerializeField]
	private ItemPriceSettings priceSettings;

	[SerializeField]
	private GameSettings gameSettings;

	[Header("Feedbacks")]
	[Tooltip("Feedback to play when purchase is successful.")]
	[SerializeField]
	private MMF_Player purchaseSuccessFeedback;

	[Tooltip("Feedback to play when purchase fails (not enough tickets or no items).")]
	[SerializeField]
	private MMF_Player purchaseFailFeedback;

	[Header("SFX")]
	[SerializeField]
	private EventReference purchaseSuccessSFX;

	[SerializeField]
	private EventReference purchaseFailSFX;

	private void Awake()
	{
		if (buyZone == null)
		{
			buyZone = GetComponentInParent<MerchantBuyZone>();
		}
		if (priceSettings == null)
		{
			priceSettings = SushiAndFish.Casino.Original.CasinoResources.Load<ItemPriceSettings>("ItemPriceSettings");
		}
		if (string.IsNullOrEmpty(TooltipMessage))
		{
			TooltipMessage = "Press [E] to Buy Items";
		}
		if (!gameSettings)
		{
			gameSettings = SushiAndFish.Casino.Original.CasinoResources.Load<GameSettings>("GameSettings");
		}
	}

	public override void ServerOnInteract(PlayerInteract playerInteract)
	{
		base.ServerOnInteract(playerInteract);
		if (buyZone == null)
		{
			Debug.LogWarning("[MerchantBuyButton] Buy zone not assigned!");
			RpcPlayFailFeedback();
			return;
		}
		List<ConsumableItem> itemsInZone = buyZone.GetItemsInZone();
		if (itemsInZone == null || itemsInZone.Count == 0)
		{
			Debug.Log("[MerchantBuyButton] No items in buy zone to purchase.");
			RpcPlayFailFeedback();
			return;
		}
		int num = CalculateTotalCost(itemsInZone);
		if (num <= 0)
		{
			Debug.LogWarning("[MerchantBuyButton] Could not calculate price for items. Price settings may be missing.");
			RpcPlayFailFeedback();
			return;
		}
		if (NetworkSingleton<MoneyManager>.Instance == null)
		{
			Debug.LogError("[MerchantBuyButton] MoneyManager instance not found!");
			RpcPlayFailFeedback();
			return;
		}
		if (!NetworkSingleton<MoneyManager>.Instance.TryChangeTicketBalance(-num))
		{
			Debug.Log($"[MerchantBuyButton] Not enough tickets! Need {num}, have {NetworkSingleton<MoneyManager>.Instance.ticketBalance}.");
			RpcPlayFailFeedback();
			return;
		}
		List<ConsumableItem> list = new List<ConsumableItem>();
		List<GachaSphere> list2 = new List<GachaSphere>();
		foreach (ConsumableItem item in itemsInZone)
		{
			if (!(item == null) && !(item.gameObject == null))
			{
				GachaSphere component = item.GetComponent<GachaSphere>();
				if (component != null)
				{
					list2.Add(component);
				}
				else
				{
					list.Add(item);
				}
			}
		}
		foreach (ConsumableItem item2 in list)
		{
			if (!(item2 == null) && !(item2.gameObject == null))
			{
				if (NetworkSingleton<ItemStampManager>.Instance != null)
				{
					NetworkSingleton<ItemStampManager>.Instance.MarkInstancePurchased(item2.gameObject);
				}
				if (item2.spawnableSo != null && NetworkSingleton<ItemManager>.Instance != null)
				{
					NetworkSingleton<ItemManager>.Instance.ServerAddItem(item2.spawnableSo);
				}
				if (item2.TryGetComponent<NetworkIdentity>(out var component2))
				{
					NetworkServer.Destroy(component2.gameObject);
				}
			}
		}
		foreach (GachaSphere item3 in list2)
		{
			if (!(item3 == null) && !(item3.gameObject == null))
			{
				int cosmeticId = item3.CosmeticId;
				if (cosmeticId > 0)
				{
					RpcUnlockCosmetic(cosmeticId);
					Debug.Log($"[MerchantBuyButton] Unlocking cosmetic {cosmeticId} for all clients.");
				}
				if (item3.TryGetComponent<NetworkIdentity>(out var component3))
				{
					NetworkServer.Destroy(component3.gameObject);
				}
			}
		}
		buyZone.ClearItemsInZone();
		Debug.Log($"[MerchantBuyButton] Successfully purchased {itemsInZone.Count} item(s) for {num} tickets.");
		RpcPlaySuccessFeedback(itemsInZone.Count, num);
	}

	[ClientRpc]
	private void RpcPlaySuccessFeedback(int itemCount, int totalCost)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(itemCount);
		writer.WriteVarInt(totalCost);
		SendRPCInternal("System.Void MerchantBuyButton::RpcPlaySuccessFeedback(System.Int32,System.Int32)", 2025179644, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcPlayFailFeedback()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void MerchantBuyButton::RpcPlayFailFeedback()", 633224113, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override void ServerOnHover(PlayerInteract playerInteract)
	{
		base.ServerOnHover(playerInteract);
		if (buyZone != null && base.isServer)
		{
			List<ConsumableItem> itemsInZone = buyZone.GetItemsInZone();
			int count = itemsInZone.Count;
			if (count > 0)
			{
				int num = CalculateTotalCost(itemsInZone);
				string message = $"[E] Buy {count} Item(s) ({num} tickets)";
				RpcUpdateTooltip(message);
			}
			else
			{
				RpcUpdateTooltip("[E] Buy Items");
			}
		}
	}

	[Server]
	private int CalculateTotalCost(List<ConsumableItem> items)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Int32 MerchantBuyButton::CalculateTotalCost(System.Collections.Generic.List`1<ConsumableItem>)' called when server was not active");
			return default;
		}
		if (items == null || items.Count == 0)
		{
			return 0;
		}
		if (priceSettings == null)
		{
			return 0;
		}
		int num = 0;
		int floorIndex = 0;
		if (NetworkSingleton<GameManager>.Instance != null)
		{
			floorIndex = gameSettings.DayToFloor(NetworkSingleton<GameManager>.Instance.daysPassed - 1);
		}
		foreach (ConsumableItem item in items)
		{
			if (item == null)
			{
				continue;
			}
			GachaSphere component = item.GetComponent<GachaSphere>();
			if (component != null)
			{
				CosmeticRarity rarity = CosmeticRarity.Common;
				CosmeticData cosmeticById = CosmeticDataManager.GetCosmeticById(component.CosmeticId);
				if (cosmeticById != null)
				{
					rarity = cosmeticById.rarity;
				}
				num += priceSettings.CalculateCosmeticPrice(rarity, floorIndex);
			}
			else if (!(item.spawnableSo == null))
			{
				int num2 = priceSettings.CalculatePrice(item.spawnableSo, floorIndex);
				num += num2;
			}
		}
		return num;
	}

	[ClientRpc]
	private void RpcUpdateTooltip(string message)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteString(message);
		SendRPCInternal("System.Void MerchantBuyButton::RpcUpdateTooltip(System.String)", -1417971140, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcUnlockCosmetic(int cosmeticId)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(cosmeticId);
		SendRPCInternal("System.Void MerchantBuyButton::RpcUnlockCosmetic(System.Int32)", -1621149138, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcPlaySuccessFeedback__Int32__Int32(int itemCount, int totalCost)
	{
		if (purchaseSuccessFeedback != null)
		{
			purchaseSuccessFeedback.PlayFeedbacks();
		}
		if (!purchaseSuccessSFX.IsNull)
		{
			SFXManager.SFXOneShot(purchaseSuccessSFX, base.transform.position);
		}
	}

	protected static void InvokeUserCode_RpcPlaySuccessFeedback__Int32__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPlaySuccessFeedback called on server.");
		}
		else
		{
			((MerchantBuyButton)obj).UserCode_RpcPlaySuccessFeedback__Int32__Int32(reader.ReadVarInt(), reader.ReadVarInt());
		}
	}

	protected void UserCode_RpcPlayFailFeedback()
	{
		if (purchaseFailFeedback != null)
		{
			purchaseFailFeedback.PlayFeedbacks();
		}
		if (!purchaseFailSFX.IsNull)
		{
			SFXManager.SFXOneShot(purchaseFailSFX, base.transform.position);
		}
	}

	protected static void InvokeUserCode_RpcPlayFailFeedback(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPlayFailFeedback called on server.");
		}
		else
		{
			((MerchantBuyButton)obj).UserCode_RpcPlayFailFeedback();
		}
	}

	protected void UserCode_RpcUpdateTooltip__String(string message)
	{
		TooltipMessage = message;
	}

	protected static void InvokeUserCode_RpcUpdateTooltip__String(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcUpdateTooltip called on server.");
		}
		else
		{
			((MerchantBuyButton)obj).UserCode_RpcUpdateTooltip__String(reader.ReadString());
		}
	}

	protected void UserCode_RpcUnlockCosmetic__Int32(int cosmeticId)
	{
		if (MonoSingleton<CosmeticsUnlockManager>.Instance != null)
		{
			if (MonoSingleton<CosmeticsUnlockManager>.Instance.UnlockCosmetic(cosmeticId))
			{
				CosmeticData cosmeticById = CosmeticDataManager.GetCosmeticById(cosmeticId);
				string arg = ((cosmeticById != null) ? cosmeticById.cosmeticName : $"ID {cosmeticId}");
				Debug.Log($"[MerchantBuyButton] Successfully unlocked cosmetic: {arg} (ID: {cosmeticId})");
			}
		}
		else
		{
			Debug.LogError("[MerchantBuyButton] CosmeticsUnlockManager.Instance is null! Cannot unlock cosmetic.");
		}
	}

	protected static void InvokeUserCode_RpcUnlockCosmetic__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcUnlockCosmetic called on server.");
		}
		else
		{
			((MerchantBuyButton)obj).UserCode_RpcUnlockCosmetic__Int32(reader.ReadVarInt());
		}
	}

	static MerchantBuyButton()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(MerchantBuyButton), "System.Void MerchantBuyButton::RpcPlaySuccessFeedback(System.Int32,System.Int32)", InvokeUserCode_RpcPlaySuccessFeedback__Int32__Int32);
		RemoteProcedureCalls.RegisterRpc(typeof(MerchantBuyButton), "System.Void MerchantBuyButton::RpcPlayFailFeedback()", InvokeUserCode_RpcPlayFailFeedback);
		RemoteProcedureCalls.RegisterRpc(typeof(MerchantBuyButton), "System.Void MerchantBuyButton::RpcUpdateTooltip(System.String)", InvokeUserCode_RpcUpdateTooltip__String);
		RemoteProcedureCalls.RegisterRpc(typeof(MerchantBuyButton), "System.Void MerchantBuyButton::RpcUnlockCosmetic(System.Int32)", InvokeUserCode_RpcUnlockCosmetic__Int32);
	}
}

}
