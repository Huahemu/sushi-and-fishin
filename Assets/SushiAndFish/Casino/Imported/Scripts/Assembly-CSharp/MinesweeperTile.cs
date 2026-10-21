namespace SushiAndFish.Casino.Original
{
using global::DG.Tweening;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class MinesweeperTile : InteractableEventTrigger
{
	[Header("Settings")]
	[SerializeField]
	private float explosionPower;

	[SerializeField]
	private float explosionUpPower;

	[SerializeField]
	private float explosionTorquePower;

	[Header("References")]
	[SerializeField]
	private Minesweeper minesweeperGame;

	[SerializeField]
	private MeshRenderer meshRenderer;

	[SerializeField]
	private Transform mineTransform;

	[SerializeField]
	private Material defaultMaterial;

	[SerializeField]
	private Material enabledMaterial;

	[SerializeField]
	private Material revealMaterial;

	[SerializeField]
	private ParticleSystem explodeVfx;

	[Header("SFX")]
	[SerializeField]
	private EventReference explosionSFX;

	public override void ServerOnInteract(PlayerInteract playerInteract)
	{
		base.ServerOnInteract(playerInteract);
		minesweeperGame.RevealTile(this);
	}

	[Server]
	public void ServerSetButtonColor(int i)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void MinesweeperTile::ServerSetButtonColor(System.Int32)' called when server was not active");
		}
		else
		{
			RpcSetButtonColor(i);
		}
	}

	[Server]
	public void ServerSetMine(bool isEnabled)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void MinesweeperTile::ServerSetMine(System.Boolean)' called when server was not active");
		}
		else
		{
			RpcSetMine(isEnabled);
		}
	}

	[Server]
	public void ServerExplode()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void MinesweeperTile::ServerExplode()' called when server was not active");
		}
		else
		{
			Explode();
		}
	}

	[ClientRpc]
	private void Explode()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void MinesweeperTile::Explode()", 290275528, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcSetButtonColor(int i)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(i);
		SendRPCInternal("System.Void MinesweeperTile::RpcSetButtonColor(System.Int32)", 1786757900, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcSetMine(bool isEnabled)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isEnabled);
		SendRPCInternal("System.Void MinesweeperTile::RpcSetMine(System.Boolean)", -1822623430, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_Explode()
	{
		explodeVfx.Play();
		SFXManager.SFXOneShot(explosionSFX, base.transform.position);
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (localPlayer.TryGetComponent<Rigidbody>(out var component) && (component.worldCenterOfMass - base.transform.position).sqrMagnitude < 4f && localPlayer.TryGetComponent<PlayerController>(out var component2))
		{
			Vector3 position = base.transform.position;
			Vector3 horizontalProjectionOfVector = FathF.GetHorizontalProjectionOfVector(component.worldCenterOfMass - position);
			Vector3 vector = Vector3.up * explosionUpPower;
			Vector3 vector2 = horizontalProjectionOfVector * explosionPower;
			float num = Vector3.Distance(component.worldCenterOfMass, position);
			float num2 = 1f - Mathf.Clamp01(num / 2f);
			Vector3 force = (vector2 + vector) * num2;
			Vector3 vector3 = component.worldCenterOfMass - position;
			Vector3 torque = Vector3.Cross(Vector3.up, vector3.normalized) * (explosionTorquePower * horizontalProjectionOfVector.magnitude);
			component2.LocalKnockback(force, torque);
		}
	}

	protected static void InvokeUserCode_Explode(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC Explode called on server.");
		}
		else
		{
			((MinesweeperTile)obj).UserCode_Explode();
		}
	}

	protected void UserCode_RpcSetButtonColor__Int32(int i)
	{
		switch (i)
		{
		case 0:
			meshRenderer.material = defaultMaterial;
			break;
		case 1:
			meshRenderer.material = enabledMaterial;
			break;
		case 2:
			meshRenderer.material = revealMaterial;
			break;
		}
	}

	protected static void InvokeUserCode_RpcSetButtonColor__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetButtonColor called on server.");
		}
		else
		{
			((MinesweeperTile)obj).UserCode_RpcSetButtonColor__Int32(reader.ReadVarInt());
		}
	}

	protected void UserCode_RpcSetMine__Boolean(bool isEnabled)
	{
		if (isEnabled)
		{
			mineTransform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
		}
		else
		{
			mineTransform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack);
		}
	}

	protected static void InvokeUserCode_RpcSetMine__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetMine called on server.");
		}
		else
		{
			((MinesweeperTile)obj).UserCode_RpcSetMine__Boolean(reader.ReadBool());
		}
	}

	static MinesweeperTile()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(MinesweeperTile), "System.Void MinesweeperTile::Explode()", InvokeUserCode_Explode);
		RemoteProcedureCalls.RegisterRpc(typeof(MinesweeperTile), "System.Void MinesweeperTile::RpcSetButtonColor(System.Int32)", InvokeUserCode_RpcSetButtonColor__Int32);
		RemoteProcedureCalls.RegisterRpc(typeof(MinesweeperTile), "System.Void MinesweeperTile::RpcSetMine(System.Boolean)", InvokeUserCode_RpcSetMine__Boolean);
	}
}

}
