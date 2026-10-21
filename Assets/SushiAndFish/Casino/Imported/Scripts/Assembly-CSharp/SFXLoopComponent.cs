namespace SushiAndFish.Casino.Original
{
using global::FMOD.Studio;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class SFXLoopComponent : NetworkBehaviour
{
	[SerializeField]
	private EventReference eventReference;

	[SerializeField]
	private bool allowFadeout = true;

	public EventInstance loopInstance;

	[Command(requiresAuthority = false)]
	public void CmdLoopSFX(bool play)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(play);
		SendCommandInternal("System.Void SFXLoopComponent::CmdLoopSFX(System.Boolean)", 1121932687, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	public void RpcLoopSFX(bool play)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(play);
		SendRPCInternal("System.Void SFXLoopComponent::RpcLoopSFX(System.Boolean)", 824686118, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public void LoopSFX(bool play)
	{
		if (eventReference.IsNull)
		{
			return;
		}
		if (play)
		{
			if (loopInstance.isValid())
			{
				loopInstance.getPlaybackState(out var state);
				if (state == PLAYBACK_STATE.PLAYING)
				{
					return;
				}
			}
			loopInstance = RuntimeManager.CreateInstance(eventReference);
			loopInstance.set3DAttributes(base.transform.position.To3DAttributes());
			RuntimeManager.AttachInstanceToGameObject(loopInstance, base.gameObject, nonRigidbodyVelocity: true);
			loopInstance.start();
		}
		else if (loopInstance.isValid())
		{
			loopInstance.stop((!allowFadeout) ? FMOD.Studio.STOP_MODE.IMMEDIATE : FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
			loopInstance.release();
		}
	}

	private void OnDisable()
	{
		LoopSFX(play: false);
	}

	public void ModulatePitch(float pitch)
	{
		loopInstance.setPitch(pitch);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_CmdLoopSFX__Boolean(bool play)
	{
		RpcLoopSFX(play);
	}

	protected static void InvokeUserCode_CmdLoopSFX__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdLoopSFX called on client.");
		}
		else
		{
			((SFXLoopComponent)obj).UserCode_CmdLoopSFX__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcLoopSFX__Boolean(bool play)
	{
		LoopSFX(play);
	}

	protected static void InvokeUserCode_RpcLoopSFX__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcLoopSFX called on server.");
		}
		else
		{
			((SFXLoopComponent)obj).UserCode_RpcLoopSFX__Boolean(reader.ReadBool());
		}
	}

	static SFXLoopComponent()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(SFXLoopComponent), "System.Void SFXLoopComponent::CmdLoopSFX(System.Boolean)", InvokeUserCode_CmdLoopSFX__Boolean, requiresAuthority: false);
		RemoteProcedureCalls.RegisterRpc(typeof(SFXLoopComponent), "System.Void SFXLoopComponent::RpcLoopSFX(System.Boolean)", InvokeUserCode_RpcLoopSFX__Boolean);
	}
}

}
