namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class Drink : ConsumableItem
{
	[SerializeField]
	private Animator anim;

	[Header("Settings")]
	[SerializeField]
	private float drinkTime = 1.5f;

	[SerializeField]
	private float radius = 4f;

	[SerializeField]
	private float upgradeAmount = 1f;

	[SerializeField]
	private float upgradeDurationThrow = 10f;

	[SerializeField]
	private float upgradeDurationDrink = 30f;

	[SerializeField]
	private float throwThreshold = 10f;

	[SerializeField]
	private float shatterThreshold = 0.5f;

	private bool _isBreakable;

	private Coroutine _setUnbreakableRoutine;

	private PlayerProfile _lastHolder;

	private Coroutine _drinkRoutine;

	private bool _canDrink;

	[Header("SFX")]
	[SerializeField]
	private SFXLoopComponent drinkSfx;

	[SerializeField]
	private SFXComponent cantDrinkSfx;

	protected override void OnUseItem(bool isPressed)
	{
		if (!_canDrink)
		{
			cantDrinkSfx.PlayOneShotAttached();
			return;
		}
		anim.SetBool("IsDrinking", isPressed);
		drinkSfx.RpcLoopSFX(isPressed);
		if (base.isServer)
		{
			if (_drinkRoutine != null)
			{
				StopCoroutine(_drinkRoutine);
			}
			if (isPressed)
			{
				_drinkRoutine = StartCoroutine(DrinkRoutine());
			}
		}
	}

	private IEnumerator DrinkRoutine()
	{
		yield return new WaitForSeconds(drinkTime);
		OnDrank();
	}

	private void OnDrank()
	{
		if (base.isServer)
		{
			base.NetworkHolder?.GetComponent<PlayerBuff>().ApplyBuff(PlayerBuffType.TipsyFortune, upgradeAmount * NetworkSingleton<UpgradeManager>.Instance.GetUpgradeData(_lastHolder.steamId, PlayerUpgradeType.Stakeholder), upgradeDurationDrink);
			if (base.NetworkHolder.TryGetComponent<PlayerVoiceFX>(out var component))
			{
				component.RpcStartTimedVoiceFX(VoipManipulationManager.VoipFX.Wobble, upgradeDurationDrink, overridable: true);
			}
			DestroyItem();
		}
	}

	private void OnCollisionEnter(Collision other)
	{
		if (base.isServer && !base.NetworkHolder && _isBreakable && !(other.impulse.magnitude < shatterThreshold))
		{
			Shatter();
		}
	}

	private IEnumerator SetUnbreakableRoutine()
	{
		while (_isBreakable)
		{
			if (Rb.linearVelocity.sqrMagnitude < 0.01f)
			{
				_isBreakable = false;
			}
			yield return new WaitForFixedUpdate();
		}
	}

	private void Shatter()
	{
		List<PlayerBuff> list = new List<PlayerBuff>();
		Collider[] array = Physics.OverlapSphere(base.transform.position, radius, SushiAndFish.Casino.CasinoLayers.GetMask("Player"));
		foreach (Collider collider in array)
		{
			if ((bool)collider.attachedRigidbody && collider.attachedRigidbody.TryGetComponent<PlayerBuff>(out var component) && !list.Contains(component))
			{
				list.Add(component);
			}
		}
		foreach (PlayerBuff item in list)
		{
			item.ApplyBuff(PlayerBuffType.TipsyFortune, upgradeAmount * NetworkSingleton<UpgradeManager>.Instance.GetUpgradeData(_lastHolder.steamId, PlayerUpgradeType.Stakeholder), upgradeDurationThrow);
			if (item.TryGetComponent<PlayerVoiceFX>(out var component2))
			{
				component2.RpcStartTimedVoiceFX(VoipManipulationManager.VoipFX.Wobble, upgradeDurationThrow, overridable: true);
			}
		}
		DestroyItem();
	}

	public override void ServerThrow(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
	{
		base.ServerThrow(position, rotation, velocity, angularVelocity);
		if (velocity.magnitude < throwThreshold)
		{
			_isBreakable = false;
			return;
		}
		_isBreakable = true;
		if (_setUnbreakableRoutine != null)
		{
			StopCoroutine(_setUnbreakableRoutine);
		}
		_setUnbreakableRoutine = StartCoroutine(SetUnbreakableRoutine());
	}

	[Server]
	public override void ServerTeleport(Vector3 position)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Drink::ServerTeleport(UnityEngine.Vector3)' called when server was not active");
		}
		else if (!base.NetworkHolder)
		{
			_isBreakable = false;
			Rb.Teleport(position, resetVelocity: true);
		}
	}

	protected override void OnPickedUp(PlayerInventory playerInventory)
	{
		base.OnPickedUp(playerInventory);
		_lastHolder = playerInventory.GetComponent<PlayerProfile>();
		var organs=playerInventory.GetComponent<PlayerOrgans>();
        var organData=organs?NetworkSingleton<OrganManager>.Instance.GetOrganData(organs):null;
        RpcSetCanDrink(organData!=null&&organData.mouth);
	}

	protected override void OnDropped(PlayerInventory playerInventory)
	{
		base.OnDropped(playerInventory);
		if (_drinkRoutine != null)
		{
			StopCoroutine(_drinkRoutine);
		}
		RpcSetCanDrink(canDrink: false);
		RpcOnDropped();
	}

	[ClientRpc]
	private void RpcOnDropped()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Drink::RpcOnDropped()", 1556049145, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcSetCanDrink(bool canDrink)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(canDrink);
		SendRPCInternal("System.Void Drink::RpcSetCanDrink(System.Boolean)", -1915179511, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcOnDropped()
	{
		anim.SetBool("IsDrinking", value: false);
		anim.Play("Default", 0, 0f);
		anim.Update(0f);
		drinkSfx.RpcLoopSFX(play: false);
	}

	protected static void InvokeUserCode_RpcOnDropped(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnDropped called on server.");
		}
		else
		{
			((Drink)obj).UserCode_RpcOnDropped();
		}
	}

	protected void UserCode_RpcSetCanDrink__Boolean(bool canDrink)
	{
		_canDrink = canDrink;
	}

	protected static void InvokeUserCode_RpcSetCanDrink__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetCanDrink called on server.");
		}
		else
		{
			((Drink)obj).UserCode_RpcSetCanDrink__Boolean(reader.ReadBool());
		}
	}

	static Drink()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(Drink), "System.Void Drink::RpcOnDropped()", InvokeUserCode_RpcOnDropped);
		RemoteProcedureCalls.RegisterRpc(typeof(Drink), "System.Void Drink::RpcSetCanDrink(System.Boolean)", InvokeUserCode_RpcSetCanDrink__Boolean);
	}
}

}
