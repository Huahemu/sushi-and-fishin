namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class QuotaGun : ConsumableItem
{
	[Header("References")]
	[SerializeField]
	private GameObject hitVFX;

	[SerializeField]
	private ParticleSystem muzzleVFX;

	[SerializeField]
	private LineRenderer lineRenderer;

	[SerializeField]
	private Animator anim;

	[SerializeField]
	private List<MeshRenderer> organIndicator;

	[Header("Settings")]
	[SerializeField]
	private float power = 7f;

	[SerializeField]
	private float upPower = 7f;

	[SerializeField]
	private float randomPower = 3f;

	[SerializeField]
	private float torquePower = 10f;

	[SerializeField]
	private float shootCooldown = 0.5f;

	[SerializeField]
	private int removableOrganCount = 3;

	[SerializeField]
	private float raycastDistance = 50f;

	[SerializeField]
	private LayerMask rayMask;

	[Header("SFX")]
	[SerializeField]
	private EventReference shootSfx;

	[SerializeField]
	private EventReference hitPlayerSfx;

	[SerializeField]
	private EventReference hitRandomSfx;

	private readonly RaycastHit[] _raycastHits = new RaycastHit[8];

	private float _lastShootTime;

	private int _removedOrganCount;

	private Color _defaultIndicatorColor;

	private void Start()
	{
		_defaultIndicatorColor = organIndicator[0].material.GetColor("_EmissionColor");
	}

	protected override void OnUseItem(bool isPressed)
	{
		base.OnUseItem(isPressed);
		if (isPressed && (bool)base.NetworkHolder && base.NetworkHolder.isLocalPlayer && !(Time.time - _lastShootTime < shootCooldown))
		{
			_lastShootTime = Time.time;
			Shoot();
		}
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
		SendRPCInternal("System.Void QuotaGun::RpcOnDropped()", -12772959, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void Shoot()
	{
		Camera mainCamera = MonoSingleton<LocalManager>.Instance.mainCamera;
		Vector3 position = mainCamera.transform.position;
		Vector3 forward = mainCamera.transform.forward;
		int num = Physics.RaycastNonAlloc(new Ray(position, forward), _raycastHits, raycastDistance, rayMask, QueryTriggerInteraction.Ignore);
		Vector3 vector = position + forward * raycastDistance;
		if (num > 0)
		{
			RaycastHit raycastHit = _raycastHits[0];
			for (int i = 1; i < num; i++)
			{
				if (_raycastHits[i].distance < raycastHit.distance)
				{
					raycastHit = _raycastHits[i];
				}
			}
			bool hitPlayer = false;
			vector = raycastHit.point;
			Rigidbody attachedRigidbody = raycastHit.collider.attachedRigidbody;
			if ((bool)attachedRigidbody)
			{
				NPC component2;
				Item component3;
				if (attachedRigidbody.TryGetComponent<PlayerController>(out var component))
				{
					CmdShootPlayer(component, forward, vector);
					hitPlayer = true;
				}
				else if (attachedRigidbody.TryGetComponent<NPC>(out component2))
				{
					CmdShootNpc(component2, forward, vector);
				}
				else if (attachedRigidbody.TryGetComponent<Item>(out component3))
				{
					CmdShootItem(component3, forward, vector);
				}
			}
			PlayHitEffects(hitPlayer, vector);
			CmdPlayHitEffects(hitPlayer, vector);
		}
		PlayShootEffects(vector);
		CmdPlayShootEffects(vector);
	}

	private Vector3 CalculateKnockbackVector(Vector3 direction)
	{
		return direction * power + Vector3.up * upPower + Vector3.Cross(direction, Vector3.up) * UnityEngine.Random.Range(-1f, 1f) * randomPower;
	}

	private Vector3 CalculateTorque(Vector3 position, Vector3 centerOfMass, Vector3 direction)
	{
		return Vector3.Cross(Vector3.ClampMagnitude(position - centerOfMass, 1f), direction) * torquePower;
	}

	[Command(requiresAuthority = false)]
	private void CmdShootPlayer(PlayerController pc, Vector3 dir, Vector3 pos)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(pc);
		writer.WriteVector3(dir);
		writer.WriteVector3(pos);
		SendCommandInternal("System.Void QuotaGun::CmdShootPlayer(PlayerController,UnityEngine.Vector3,UnityEngine.Vector3)", 182095588, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Command(requiresAuthority = false)]
	private void CmdShootNpc(NPC npc, Vector3 dir, Vector3 pos)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(npc);
		writer.WriteVector3(dir);
		writer.WriteVector3(pos);
		SendCommandInternal("System.Void QuotaGun::CmdShootNpc(NPC,UnityEngine.Vector3,UnityEngine.Vector3)", -1272076522, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[Command(requiresAuthority = false)]
	private void CmdShootItem(Item item, Vector3 dir, Vector3 pos)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(item);
		writer.WriteVector3(dir);
		writer.WriteVector3(pos);
		SendCommandInternal("System.Void QuotaGun::CmdShootItem(Item,UnityEngine.Vector3,UnityEngine.Vector3)", 534524124, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	private void TryRemoveRandomOrgan(PlayerController pc)
	{
		if (_removedOrganCount >= removableOrganCount || NetworkSingleton<GameManager>.Instance.state != GameState.Game || NetworkSingleton<MoneyManager>.Instance.balance >= NetworkSingleton<GameManager>.Instance.currentQuota)
		{
			return;
		}
		PlayerOrgans component = pc.GetComponent<PlayerOrgans>();
		PlayerOrganData organData = NetworkSingleton<OrganManager>.Instance.GetOrganData(component);
		if (organData != null)
		{
			List<OrganType> list = new List<OrganType>();
			if (organData.body)
			{
				list.Add(OrganType.Body);
			}
			if (organData.mouth)
			{
				list.Add(OrganType.Mouth);
			}
			if (organData.leftEye && organData.rightEye)
			{
				list.Add((!(UnityEngine.Random.Range(0f, 1f) > 0.5f)) ? OrganType.RightEye : OrganType.LeftEye);
			}
			if (list.Count > 0)
			{
				NetworkSingleton<OrganManager>.Instance.ServerToggleOrgan(component, list.GetRandomElement(), isEnabled: false);
				_removedOrganCount++;
				RpcSetRemovedOrganIndicator(_removedOrganCount);
				AwardQuotaFraction();
			}
		}
	}

	[Server]
	private void AwardQuotaFraction()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void QuotaGun::AwardQuotaFraction()' called when server was not active");
			return;
		}
		long currentQuota = NetworkSingleton<GameManager>.Instance.currentQuota;
		long num = (long)Math.Ceiling((double)currentQuota / (double)removableOrganCount);
		long num2 = currentQuota - NetworkSingleton<MoneyManager>.Instance.balance;
		if (num > num2)
		{
			num = num2;
		}
		if (num > 0)
		{
			NetworkSingleton<MoneyManager>.Instance.TryChangeBalance(num, null, ChangeType.Item);
		}
	}

	[ClientRpc]
	private void RpcSetRemovedOrganIndicator(int removedOrganCount)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(removedOrganCount);
		SendRPCInternal("System.Void QuotaGun::RpcSetRemovedOrganIndicator(System.Int32)", 1515992013, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Command(requiresAuthority = false)]
	private void CmdPlayShootEffects(Vector3 hitPos)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVector3(hitPos);
		SendCommandInternal("System.Void QuotaGun::CmdPlayShootEffects(UnityEngine.Vector3)", -562226375, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcPlayShootEffects(Vector3 hitPos)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVector3(hitPos);
		SendRPCInternal("System.Void QuotaGun::RpcPlayShootEffects(UnityEngine.Vector3)", 1557493596, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void PlayShootEffects(Vector3 hitPos)
	{
		anim.Play("Shoot", 0, 0f);
		muzzleVFX.Play();
		LineRenderer obj = UnityEngine.Object.Instantiate(lineRenderer);
		obj.SetPosition(0, muzzleVFX.transform.position);
		obj.SetPosition(1, hitPos);
		UnityEngine.Object.Destroy(obj.gameObject, 0.05f);
		SFXManager.SFXOneShot3DAttached(shootSfx, base.gameObject);
	}

	[Command(requiresAuthority = false)]
	private void CmdPlayHitEffects(bool hitPlayer, Vector3 hitPos)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(hitPlayer);
		writer.WriteVector3(hitPos);
		SendCommandInternal("System.Void QuotaGun::CmdPlayHitEffects(System.Boolean,UnityEngine.Vector3)", 960346420, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcPlayHitEffects(bool hitPlayer, Vector3 hitPos)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(hitPlayer);
		writer.WriteVector3(hitPos);
		SendRPCInternal("System.Void QuotaGun::RpcPlayHitEffects(System.Boolean,UnityEngine.Vector3)", -584027577, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void PlayHitEffects(bool hitPlayer, Vector3 hitPos)
	{
		UnityEngine.Object.Destroy(UnityEngine.Object.Instantiate(hitVFX, hitPos, Quaternion.identity), 1f);
		SFXManager.SFXOneShot(hitPlayer ? hitPlayerSfx : hitRandomSfx, hitPos);
	}

	public override bool Weaved()
	{
		return true;
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
			((QuotaGun)obj).UserCode_RpcOnDropped();
		}
	}

	protected void UserCode_CmdShootPlayer__PlayerController__Vector3__Vector3(PlayerController pc, Vector3 dir, Vector3 pos)
	{
		TryRemoveRandomOrgan(pc);
		Rigidbody component = pc.GetComponent<Rigidbody>();
		Vector3 force = CalculateKnockbackVector(dir);
		Vector3 torque = CalculateTorque(pos, component.worldCenterOfMass, dir);
		pc.ServerKnockback(force, torque);
	}

	protected static void InvokeUserCode_CmdShootPlayer__PlayerController__Vector3__Vector3(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdShootPlayer called on client.");
		}
		else
		{
			((QuotaGun)obj).UserCode_CmdShootPlayer__PlayerController__Vector3__Vector3(reader.ReadNetworkBehaviour<PlayerController>(), reader.ReadVector3(), reader.ReadVector3());
		}
	}

	protected void UserCode_CmdShootNpc__NPC__Vector3__Vector3(NPC npc, Vector3 dir, Vector3 pos)
	{
		Rigidbody component = npc.GetComponent<Rigidbody>();
		Vector3 force = CalculateKnockbackVector(dir);
		Vector3 torque = CalculateTorque(pos, component.worldCenterOfMass, dir);
		npc.ServerKnockback(force, torque);
	}

	protected static void InvokeUserCode_CmdShootNpc__NPC__Vector3__Vector3(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdShootNpc called on client.");
		}
		else
		{
			((QuotaGun)obj).UserCode_CmdShootNpc__NPC__Vector3__Vector3(reader.ReadNetworkBehaviour<NPC>(), reader.ReadVector3(), reader.ReadVector3());
		}
	}

	protected void UserCode_CmdShootItem__Item__Vector3__Vector3(Item item, Vector3 dir, Vector3 pos)
	{
		Rigidbody component = item.GetComponent<Rigidbody>();
		Vector3 force = CalculateKnockbackVector(dir);
		Vector3 torque = CalculateTorque(pos, component.worldCenterOfMass, dir);
		component.AddForce(force, ForceMode.Impulse);
		component.AddTorque(torque, ForceMode.Impulse);
	}

	protected static void InvokeUserCode_CmdShootItem__Item__Vector3__Vector3(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdShootItem called on client.");
		}
		else
		{
			((QuotaGun)obj).UserCode_CmdShootItem__Item__Vector3__Vector3(reader.ReadNetworkBehaviour<Item>(), reader.ReadVector3(), reader.ReadVector3());
		}
	}

	protected void UserCode_RpcSetRemovedOrganIndicator__Int32(int removedOrganCount)
	{
		for (int i = 0; i < organIndicator.Count; i++)
		{
			MeshRenderer meshRenderer = organIndicator[i];
			if (i < removedOrganCount)
			{
				meshRenderer.material.SetColor("_EmissionColor", Color.gold * 2f);
			}
			else
			{
				meshRenderer.material.SetColor("_EmissionColor", _defaultIndicatorColor);
			}
		}
	}

	protected static void InvokeUserCode_RpcSetRemovedOrganIndicator__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetRemovedOrganIndicator called on server.");
		}
		else
		{
			((QuotaGun)obj).UserCode_RpcSetRemovedOrganIndicator__Int32(reader.ReadVarInt());
		}
	}

	protected void UserCode_CmdPlayShootEffects__Vector3(Vector3 hitPos)
	{
		RpcPlayShootEffects(hitPos);
	}

	protected static void InvokeUserCode_CmdPlayShootEffects__Vector3(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdPlayShootEffects called on client.");
		}
		else
		{
			((QuotaGun)obj).UserCode_CmdPlayShootEffects__Vector3(reader.ReadVector3());
		}
	}

	protected void UserCode_RpcPlayShootEffects__Vector3(Vector3 hitPos)
	{
		if ((bool)base.NetworkHolder && !base.NetworkHolder.isLocalPlayer)
		{
			PlayShootEffects(hitPos);
		}
	}

	protected static void InvokeUserCode_RpcPlayShootEffects__Vector3(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPlayShootEffects called on server.");
		}
		else
		{
			((QuotaGun)obj).UserCode_RpcPlayShootEffects__Vector3(reader.ReadVector3());
		}
	}

	protected void UserCode_CmdPlayHitEffects__Boolean__Vector3(bool hitPlayer, Vector3 hitPos)
	{
		RpcPlayHitEffects(hitPlayer, hitPos);
	}

	protected static void InvokeUserCode_CmdPlayHitEffects__Boolean__Vector3(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdPlayHitEffects called on client.");
		}
		else
		{
			((QuotaGun)obj).UserCode_CmdPlayHitEffects__Boolean__Vector3(reader.ReadBool(), reader.ReadVector3());
		}
	}

	protected void UserCode_RpcPlayHitEffects__Boolean__Vector3(bool hitPlayer, Vector3 hitPos)
	{
		if ((bool)base.NetworkHolder && !base.NetworkHolder.isLocalPlayer)
		{
			PlayHitEffects(hitPlayer, hitPos);
		}
	}

	protected static void InvokeUserCode_RpcPlayHitEffects__Boolean__Vector3(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPlayHitEffects called on server.");
		}
		else
		{
			((QuotaGun)obj).UserCode_RpcPlayHitEffects__Boolean__Vector3(reader.ReadBool(), reader.ReadVector3());
		}
	}

	static QuotaGun()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(QuotaGun), "System.Void QuotaGun::CmdShootPlayer(PlayerController,UnityEngine.Vector3,UnityEngine.Vector3)", InvokeUserCode_CmdShootPlayer__PlayerController__Vector3__Vector3, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(QuotaGun), "System.Void QuotaGun::CmdShootNpc(NPC,UnityEngine.Vector3,UnityEngine.Vector3)", InvokeUserCode_CmdShootNpc__NPC__Vector3__Vector3, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(QuotaGun), "System.Void QuotaGun::CmdShootItem(Item,UnityEngine.Vector3,UnityEngine.Vector3)", InvokeUserCode_CmdShootItem__Item__Vector3__Vector3, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(QuotaGun), "System.Void QuotaGun::CmdPlayShootEffects(UnityEngine.Vector3)", InvokeUserCode_CmdPlayShootEffects__Vector3, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(QuotaGun), "System.Void QuotaGun::CmdPlayHitEffects(System.Boolean,UnityEngine.Vector3)", InvokeUserCode_CmdPlayHitEffects__Boolean__Vector3, requiresAuthority: false);
		RemoteProcedureCalls.RegisterRpc(typeof(QuotaGun), "System.Void QuotaGun::RpcOnDropped()", InvokeUserCode_RpcOnDropped);
		RemoteProcedureCalls.RegisterRpc(typeof(QuotaGun), "System.Void QuotaGun::RpcSetRemovedOrganIndicator(System.Int32)", InvokeUserCode_RpcSetRemovedOrganIndicator__Int32);
		RemoteProcedureCalls.RegisterRpc(typeof(QuotaGun), "System.Void QuotaGun::RpcPlayShootEffects(UnityEngine.Vector3)", InvokeUserCode_RpcPlayShootEffects__Vector3);
		RemoteProcedureCalls.RegisterRpc(typeof(QuotaGun), "System.Void QuotaGun::RpcPlayHitEffects(System.Boolean,UnityEngine.Vector3)", InvokeUserCode_RpcPlayHitEffects__Boolean__Vector3);
	}
}

}
