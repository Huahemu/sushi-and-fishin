namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using SushiAndFish.Casino.Original.Extensions;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class Bat : ConsumableItem
{
	[Header("References")]
	[SerializeField]
	private Transform spawnPoint;

	[SerializeField]
	private Collider hitPoint;

	[SerializeField]
	private Animator anim;

	[SerializeField]
	private ParticleSystem hitVfx;

	[SerializeField]
	private ParticleSystem hitVfxSmall;

	[Header("Settings")]
	[SerializeField]
	private float hitDuration = 0.5f;

	[SerializeField]
	private float power = 7f;

	[SerializeField]
	private float upPower = 7f;

	[SerializeField]
	private float randomPower = 3f;

	[SerializeField]
	private float torquePower = 10f;

	[SerializeField]
	private float breakChance = 0.15f;

	[Header("SFX")]
	[SerializeField]
	private EventReference sFXBatSwing;

	[SerializeField]
	private EventReference sFXGenericBatHit;

	[SerializeField]
	private EventReference sFXPlayerHit;

	private bool _isUsing;

	protected override void OnDisable()
	{
		base.OnDisable();
		_isUsing = false;
	}

	protected override void OnPickedUp(PlayerInventory playerInventory)
	{
		base.OnPickedUp(playerInventory);
		RpcOnPickedUp();
	}

	[ClientRpc]
	private void RpcOnPickedUp()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Bat::RpcOnPickedUp()", -582666199, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	protected override void OnDropped(PlayerInventory playerInventory)
	{
		base.OnDropped(playerInventory);
		RpcOnDropped();
	}

	[ClientRpc]
	private void RpcOnDropped()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Bat::RpcOnDropped()", 1719686264, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	protected override void OnUseItem(bool isPressed)
	{
		base.OnUseItem(isPressed);
		if (!_isUsing)
		{
			_isUsing = true;
			PlayBatSFX(0);
			anim.SetTrigger("Hit");
			if ((bool)base.NetworkHolder && base.NetworkHolder.isLocalPlayer)
			{
				Hit();
			}
			StartCoroutine(SetIsUsingFalseAfterDelay());
		}
	}

	private IEnumerator SetIsUsingFalseAfterDelay()
	{
		yield return new WaitForSeconds(hitDuration);
		_isUsing = false;
	}

	private void Hit()
	{
		hitPoint.enabled = true;
		StartCoroutine(DisableHitPointAfterDelay());
	}

	private IEnumerator DisableHitPointAfterDelay()
	{
		yield return new WaitForSeconds(0.15f);
		hitPoint.enabled = false;
	}

	private void OnTriggerEnter(Collider hit)
	{
		if (!base.NetworkHolder || !base.NetworkHolder.isLocalPlayer || hit.isTrigger)
		{
			return;
		}
		Vector3 vector = hit.ClosestPointOnBounds(hitPoint.transform.position);
		float sqrMagnitude = (vector - hitPoint.transform.position).sqrMagnitude;
		float falldown = 1f - Mathf.Clamp(sqrMagnitude, 0f, 0.9f);
		if (!hit.attachedRigidbody)
		{
			CmdPlayBatSFX(1);
			HitVfx(isSmall: true, vector);
			return;
		}
		NPC component2;
		Item component3;
		if (hit.attachedRigidbody.TryGetComponent<PlayerController>(out var component))
		{
			if (component.netId == base.NetworkHolder.netId)
			{
				return;
			}
			CmdHitPlayer(component, falldown);
			CmdPlayBatSFX(2);
		}
		else if (hit.attachedRigidbody.TryGetComponent<NPC>(out component2))
		{
			CmdHitNpc(component2, falldown);
			CmdPlayBatSFX(2);
		}
		else if (hit.attachedRigidbody.TryGetComponent<Item>(out component3))
		{
			CmdHitItem(component3, falldown);
			CmdPlayBatSFX(1);
		}
		HitVfx(isSmall: false, hit.ClosestPointOnBounds(hitPoint.transform.position));
	}

	private void HitVfx(bool isSmall, Vector3 position)
	{
		Object.Destroy(Object.Instantiate(isSmall ? hitVfxSmall : hitVfx, position, Quaternion.identity).gameObject, 1f);
		CmdHitVfx(isSmall, position);
	}

	[Command(requiresAuthority = false)]
	private void CmdHitVfx(bool isSmall, Vector3 position)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isSmall);
		writer.WriteVector3(position);
		SendCommandInternal("System.Void Bat::CmdHitVfx(System.Boolean,UnityEngine.Vector3)", 1658169067, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcHitVfx(bool isSmall, Vector3 position)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isSmall);
		writer.WriteVector3(position);
		SendRPCInternal("System.Void Bat::RpcHitVfx(System.Boolean,UnityEngine.Vector3)", -1206476476, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Command(requiresAuthority = false)]
	private void CmdHitPlayer(PlayerController pc, float falldown)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(pc);
		writer.WriteFloat(falldown);
		SendCommandInternal("System.Void Bat::CmdHitPlayer(PlayerController,System.Single)", 972876612, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Command(requiresAuthority = false)]
	private void CmdHitNpc(NPC npc, float falldown)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(npc);
		writer.WriteFloat(falldown);
		SendCommandInternal("System.Void Bat::CmdHitNpc(NPC,System.Single)", 123880106, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Command(requiresAuthority = false)]
	private void CmdHitItem(Item item, float falldown)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(item);
		writer.WriteFloat(falldown);
		SendCommandInternal("System.Void Bat::CmdHitItem(Item,System.Single)", 1971654748, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	private void TryBreak()
	{
		if (NetworkSingleton<GameManager>.Instance.state == GameState.Game && !(breakChance <= Random.value))
		{
			DestroyItem();
		}
	}

	public void LocalSetBatSpawnPoint(Transform point)
	{
		spawnPoint = point;
	}

	[Server]
	public void ServerResetBat()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Bat::ServerResetBat()' called when server was not active");
		}
		else if (!base.NetworkHolder && (bool)spawnPoint && (!NetworkSingleton<ElevatorManager>.Instance || !NetworkSingleton<ElevatorManager>.Instance.IsInElevator(base.transform.position)))
		{
			RpcResetBat();
		}
	}

	[ClientRpc]
	private void RpcResetBat()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Bat::RpcResetBat()", 57258751, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Command(requiresAuthority = false)]
	private void CmdPlayBatSFX(int i)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(i);
		SendCommandInternal("System.Void Bat::CmdPlayBatSFX(System.Int32)", -1271608753, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcPlayBatSFX(int i)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(i);
		SendRPCInternal("System.Void Bat::RpcPlayBatSFX(System.Int32)", 39244016, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void PlayBatSFX(int i)
	{
		EventReference sfxEvent;
		switch (i)
		{
		case 0:
			sfxEvent = sFXBatSwing;
			break;
		case 1:
			sfxEvent = sFXGenericBatHit;
			break;
		case 2:
		{
			SFXParams[] sFXParams = new SFXParams[1]
			{
				new SFXParams("Magnitude", 1f)
			};
			SFXManager.SFXOneShot3DAttachedWithParameters(sFXPlayerHit, sFXParams, base.gameObject);
			return;
		}
		default:
			sfxEvent = sFXBatSwing;
			break;
		}
		SFXManager.SFXOneShot3DAttached(sfxEvent, base.gameObject);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcOnPickedUp()
	{
		anim.SetTrigger("PickUp");
	}

	protected static void InvokeUserCode_RpcOnPickedUp(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnPickedUp called on server.");
		}
		else
		{
			((Bat)obj).UserCode_RpcOnPickedUp();
		}
	}

	protected void UserCode_RpcOnDropped()
	{
		anim.Play("Default", 0, 0f);
		anim.Update(0f);
	}

	protected static void InvokeUserCode_RpcOnDropped(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnDropped called on server.");
		}
		else
		{
			((Bat)obj).UserCode_RpcOnDropped();
		}
	}

	protected void UserCode_CmdHitVfx__Boolean__Vector3(bool isSmall, Vector3 position)
	{
		RpcHitVfx(isSmall, position);
	}

	protected static void InvokeUserCode_CmdHitVfx__Boolean__Vector3(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdHitVfx called on client.");
		}
		else
		{
			((Bat)obj).UserCode_CmdHitVfx__Boolean__Vector3(reader.ReadBool(), reader.ReadVector3());
		}
	}

	protected void UserCode_RpcHitVfx__Boolean__Vector3(bool isSmall, Vector3 position)
	{
		if (!base.NetworkHolder || !base.NetworkHolder.isLocalPlayer)
		{
			Object.Destroy(Object.Instantiate(isSmall ? hitVfxSmall : hitVfx, position, Quaternion.identity).gameObject, 1f);
		}
	}

	protected static void InvokeUserCode_RpcHitVfx__Boolean__Vector3(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcHitVfx called on server.");
		}
		else
		{
			((Bat)obj).UserCode_RpcHitVfx__Boolean__Vector3(reader.ReadBool(), reader.ReadVector3());
		}
	}

	protected void UserCode_CmdHitPlayer__PlayerController__Single(PlayerController pc, float falldown)
	{
		Vector3 force = hitPoint.transform.forward * power + Vector3.up * upPower + hitPoint.transform.right * Random.Range(-1f, 1f) * randomPower;
		force *= falldown;
		Vector3 torque = Vector3.Cross(Vector3.ClampMagnitude(hitPoint.transform.position - pc.GetComponent<Rigidbody>().worldCenterOfMass, 1f), hitPoint.transform.forward) * torquePower * falldown;
		pc.ServerKnockback(force, torque);
		TryBreak();
	}

	protected static void InvokeUserCode_CmdHitPlayer__PlayerController__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdHitPlayer called on client.");
		}
		else
		{
			((Bat)obj).UserCode_CmdHitPlayer__PlayerController__Single(reader.ReadNetworkBehaviour<PlayerController>(), reader.ReadFloat());
		}
	}

	protected void UserCode_CmdHitNpc__NPC__Single(NPC npc, float falldown)
	{
		Vector3 force = hitPoint.transform.forward * power + Vector3.up * upPower + hitPoint.transform.right * Random.Range(-1f, 1f) * randomPower;
		force *= falldown;
		Vector3 torque = Vector3.Cross(Vector3.ClampMagnitude(hitPoint.transform.position - npc.GetComponent<Rigidbody>().worldCenterOfMass, 1f), hitPoint.transform.forward) * torquePower * falldown;
		npc.ServerKnockback(force, torque);
	}

	protected static void InvokeUserCode_CmdHitNpc__NPC__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdHitNpc called on client.");
		}
		else
		{
			((Bat)obj).UserCode_CmdHitNpc__NPC__Single(reader.ReadNetworkBehaviour<NPC>(), reader.ReadFloat());
		}
	}

	protected void UserCode_CmdHitItem__Item__Single(Item item, float falldown)
	{
		Rigidbody component = item.GetComponent<Rigidbody>();
		Vector3 force = hitPoint.transform.forward * power + Vector3.up * upPower + hitPoint.transform.right * Random.Range(-1f, 1f) * randomPower;
		force *= falldown;
		Vector3 torque = Vector3.Cross(Vector3.ClampMagnitude(hitPoint.transform.position - component.worldCenterOfMass, 1f), hitPoint.transform.forward) * torquePower * falldown;
		component.AddForce(force, ForceMode.Impulse);
		component.AddTorque(torque, ForceMode.Impulse);
	}

	protected static void InvokeUserCode_CmdHitItem__Item__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdHitItem called on client.");
		}
		else
		{
			((Bat)obj).UserCode_CmdHitItem__Item__Single(reader.ReadNetworkBehaviour<Item>(), reader.ReadFloat());
		}
	}

	protected void UserCode_RpcResetBat()
	{
		Rb.isKinematic = true;
		base.transform.parent = spawnPoint;
		base.transform.localPosition = Vector3.zero;
		base.transform.localRotation = Quaternion.identity;
	}

	protected static void InvokeUserCode_RpcResetBat(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcResetBat called on server.");
		}
		else
		{
			((Bat)obj).UserCode_RpcResetBat();
		}
	}

	protected void UserCode_CmdPlayBatSFX__Int32(int i)
	{
		RpcPlayBatSFX(i);
	}

	protected static void InvokeUserCode_CmdPlayBatSFX__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdPlayBatSFX called on client.");
		}
		else
		{
			((Bat)obj).UserCode_CmdPlayBatSFX__Int32(reader.ReadVarInt());
		}
	}

	protected void UserCode_RpcPlayBatSFX__Int32(int i)
	{
		PlayBatSFX(i);
	}

	protected static void InvokeUserCode_RpcPlayBatSFX__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPlayBatSFX called on server.");
		}
		else
		{
			((Bat)obj).UserCode_RpcPlayBatSFX__Int32(reader.ReadVarInt());
		}
	}

	static Bat()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(Bat), "System.Void Bat::CmdHitVfx(System.Boolean,UnityEngine.Vector3)", InvokeUserCode_CmdHitVfx__Boolean__Vector3, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(Bat), "System.Void Bat::CmdHitPlayer(PlayerController,System.Single)", InvokeUserCode_CmdHitPlayer__PlayerController__Single, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(Bat), "System.Void Bat::CmdHitNpc(NPC,System.Single)", InvokeUserCode_CmdHitNpc__NPC__Single, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(Bat), "System.Void Bat::CmdHitItem(Item,System.Single)", InvokeUserCode_CmdHitItem__Item__Single, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(Bat), "System.Void Bat::CmdPlayBatSFX(System.Int32)", InvokeUserCode_CmdPlayBatSFX__Int32, requiresAuthority: false);
		RemoteProcedureCalls.RegisterRpc(typeof(Bat), "System.Void Bat::RpcOnPickedUp()", InvokeUserCode_RpcOnPickedUp);
		RemoteProcedureCalls.RegisterRpc(typeof(Bat), "System.Void Bat::RpcOnDropped()", InvokeUserCode_RpcOnDropped);
		RemoteProcedureCalls.RegisterRpc(typeof(Bat), "System.Void Bat::RpcHitVfx(System.Boolean,UnityEngine.Vector3)", InvokeUserCode_RpcHitVfx__Boolean__Vector3);
		RemoteProcedureCalls.RegisterRpc(typeof(Bat), "System.Void Bat::RpcResetBat()", InvokeUserCode_RpcResetBat);
		RemoteProcedureCalls.RegisterRpc(typeof(Bat), "System.Void Bat::RpcPlayBatSFX(System.Int32)", InvokeUserCode_RpcPlayBatSFX__Int32);
	}
}

}
