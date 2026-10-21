namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::Dissonance;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class PlayerVoiceFX : NetworkBehaviour
{
	private DissonanceComms _comms;

	private VoipManipulationManager _manager;

	private IDissonancePlayer _dissonancePlayer;

	private Coroutine coroutine;

	private bool inOverridableCoroutine;

	public bool isMuffled;

	public override void OnStartClient()
	{
		base.OnStartClient();
		InitializeReferences();
	}

	private void InitializeReferences()
	{
		if (_comms == null)
		{
			_comms = UnityEngine.Object.FindAnyObjectByType<DissonanceComms>();
		}
		if (_manager == null)
		{
			_manager = UnityEngine.Object.FindAnyObjectByType<VoipManipulationManager>();
		}
		if (_dissonancePlayer == null)
		{
			_dissonancePlayer = GetComponent<IDissonancePlayer>();
		}
	}

	[Command(requiresAuthority = false)]
	public void CmdStartVoiceFX(VoipManipulationManager.VoipFX voipFX)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		GeneratedNetworkCode._Write_VoipManipulationManager_002FVoipFX(writer, voipFX);
		SendCommandInternal("System.Void PlayerVoiceFX::CmdStartVoiceFX(VoipManipulationManager/VoipFX)", -681699849, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	public void RpcStartVoiceFX(VoipManipulationManager.VoipFX voipFX)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		GeneratedNetworkCode._Write_VoipManipulationManager_002FVoipFX(writer, voipFX);
		SendRPCInternal("System.Void PlayerVoiceFX::RpcStartVoiceFX(VoipManipulationManager/VoipFX)", 87756608, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public void StartVoiceFX(VoipManipulationManager.VoipFX voipFX)
	{
		if (AllowedToChangeFX())
		{
			_manager.AssignPlayerVoipFX(_dissonancePlayer.PlayerId, voipFX);
		}
	}

	public void ResetVoiceFX()
	{
		if (AllowedToChangeFX())
		{
			_manager.AssignPlayerVoipFX(_dissonancePlayer.PlayerId, VoipManipulationManager.VoipFX.Default);
		}
	}

	[Command(requiresAuthority = false)]
	public void CmdResetVoiceFX()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendCommandInternal("System.Void PlayerVoiceFX::CmdResetVoiceFX()", 661324567, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	public void RpcResetVoiceFX()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void PlayerVoiceFX::RpcResetVoiceFX()", -457294148, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Command(requiresAuthority = false)]
	public void CmdStartTimedVoiceFX(VoipManipulationManager.VoipFX voipFX, float duration, bool overridable)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		GeneratedNetworkCode._Write_VoipManipulationManager_002FVoipFX(writer, voipFX);
		writer.WriteFloat(duration);
		writer.WriteBool(overridable);
		SendCommandInternal("System.Void PlayerVoiceFX::CmdStartTimedVoiceFX(VoipManipulationManager/VoipFX,System.Single,System.Boolean)", 1023355318, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	public void RpcStartTimedVoiceFX(VoipManipulationManager.VoipFX voipFX, float duration, bool overridable)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		GeneratedNetworkCode._Write_VoipManipulationManager_002FVoipFX(writer, voipFX);
		writer.WriteFloat(duration);
		writer.WriteBool(overridable);
		SendRPCInternal("System.Void PlayerVoiceFX::RpcStartTimedVoiceFX(VoipManipulationManager/VoipFX,System.Single,System.Boolean)", -364832183, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private IEnumerator DelayedCall(Action action, float delay, bool overridable)
	{
		inOverridableCoroutine = overridable;
		yield return new WaitForSeconds(delay);
		inOverridableCoroutine = false;
		coroutine = null;
		action();
	}

	public bool AllowedToChangeFX()
	{
        if(CasinoRuntimeContext.Active){InitializeReferences();if(!_manager||!_comms||!_comms.isActiveAndEnabled||_dissonancePlayer==null||string.IsNullOrEmpty(_dissonancePlayer.PlayerId))return false;}
		if (coroutine == null)
		{
			return true;
		}
		if (inOverridableCoroutine && coroutine != null)
		{
			StopCoroutine(coroutine);
			coroutine = null;
			inOverridableCoroutine = false;
			return true;
		}
		if (!inOverridableCoroutine && coroutine != null)
		{
			Debug.Log("Voice FX override denied");
			return false;
		}
		return true;
	}

	[Command(requiresAuthority = false)]
	public void CmdSetNoMouthFX(bool active)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(active);
		SendCommandInternal("System.Void PlayerVoiceFX::CmdSetNoMouthFX(System.Boolean)", 58745215, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	public void RpcSetNoMouthFX(bool active)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(active);
		SendRPCInternal("System.Void PlayerVoiceFX::RpcSetNoMouthFX(System.Boolean)", 526642084, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private IEnumerator SetNoMouthFXRoutine(bool active)
	{
        if(CasinoRuntimeContext.Active){InitializeReferences();if(!_manager||!_comms||!_comms.isActiveAndEnabled||_dissonancePlayer==null)yield break;}
		while (_dissonancePlayer == null || _dissonancePlayer.PlayerId == null)
		{
			yield return new WaitForSeconds(0.2f);
		}
		bool setFX = _manager.SetPlayerNoMouthFX(_dissonancePlayer.PlayerId, active);
		while (!setFX)
		{
			setFX = _manager.SetPlayerNoMouthFX(_dissonancePlayer.PlayerId, active);
			yield return new WaitForSeconds(0.2f);
		}
		isMuffled = active;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_CmdStartVoiceFX__VoipFX(VoipManipulationManager.VoipFX voipFX)
	{
		RpcStartVoiceFX(voipFX);
	}

	protected static void InvokeUserCode_CmdStartVoiceFX__VoipFX(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdStartVoiceFX called on client.");
		}
		else
		{
			((PlayerVoiceFX)obj).UserCode_CmdStartVoiceFX__VoipFX(GeneratedNetworkCode._Read_VoipManipulationManager_002FVoipFX(reader));
		}
	}

	protected void UserCode_RpcStartVoiceFX__VoipFX(VoipManipulationManager.VoipFX voipFX)
	{
		StartVoiceFX(voipFX);
	}

	protected static void InvokeUserCode_RpcStartVoiceFX__VoipFX(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcStartVoiceFX called on server.");
		}
		else
		{
			((PlayerVoiceFX)obj).UserCode_RpcStartVoiceFX__VoipFX(GeneratedNetworkCode._Read_VoipManipulationManager_002FVoipFX(reader));
		}
	}

	protected void UserCode_CmdResetVoiceFX()
	{
		RpcResetVoiceFX();
	}

	protected static void InvokeUserCode_CmdResetVoiceFX(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdResetVoiceFX called on client.");
		}
		else
		{
			((PlayerVoiceFX)obj).UserCode_CmdResetVoiceFX();
		}
	}

	protected void UserCode_RpcResetVoiceFX()
	{
		ResetVoiceFX();
	}

	protected static void InvokeUserCode_RpcResetVoiceFX(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcResetVoiceFX called on server.");
		}
		else
		{
			((PlayerVoiceFX)obj).UserCode_RpcResetVoiceFX();
		}
	}

	protected void UserCode_CmdStartTimedVoiceFX__VoipFX__Single__Boolean(VoipManipulationManager.VoipFX voipFX, float duration, bool overridable)
	{
		RpcStartTimedVoiceFX(voipFX, duration, overridable);
	}

	protected static void InvokeUserCode_CmdStartTimedVoiceFX__VoipFX__Single__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdStartTimedVoiceFX called on client.");
		}
		else
		{
			((PlayerVoiceFX)obj).UserCode_CmdStartTimedVoiceFX__VoipFX__Single__Boolean(GeneratedNetworkCode._Read_VoipManipulationManager_002FVoipFX(reader), reader.ReadFloat(), reader.ReadBool());
		}
	}

	protected void UserCode_RpcStartTimedVoiceFX__VoipFX__Single__Boolean(VoipManipulationManager.VoipFX voipFX, float duration, bool overridable)
	{
		StartVoiceFX(voipFX);
		coroutine = StartCoroutine(DelayedCall(() =>
		{
			ResetVoiceFX();
		}, duration, overridable));
	}

	protected static void InvokeUserCode_RpcStartTimedVoiceFX__VoipFX__Single__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcStartTimedVoiceFX called on server.");
		}
		else
		{
			((PlayerVoiceFX)obj).UserCode_RpcStartTimedVoiceFX__VoipFX__Single__Boolean(GeneratedNetworkCode._Read_VoipManipulationManager_002FVoipFX(reader), reader.ReadFloat(), reader.ReadBool());
		}
	}

	protected void UserCode_CmdSetNoMouthFX__Boolean(bool active)
	{
		RpcSetNoMouthFX(active);
	}

	protected static void InvokeUserCode_CmdSetNoMouthFX__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdSetNoMouthFX called on client.");
		}
		else
		{
			((PlayerVoiceFX)obj).UserCode_CmdSetNoMouthFX__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcSetNoMouthFX__Boolean(bool active)
	{
		StartCoroutine(SetNoMouthFXRoutine(active));
	}

	protected static void InvokeUserCode_RpcSetNoMouthFX__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetNoMouthFX called on server.");
		}
		else
		{
			((PlayerVoiceFX)obj).UserCode_RpcSetNoMouthFX__Boolean(reader.ReadBool());
		}
	}

	static PlayerVoiceFX()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(PlayerVoiceFX), "System.Void PlayerVoiceFX::CmdStartVoiceFX(VoipManipulationManager/VoipFX)", InvokeUserCode_CmdStartVoiceFX__VoipFX, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(PlayerVoiceFX), "System.Void PlayerVoiceFX::CmdResetVoiceFX()", InvokeUserCode_CmdResetVoiceFX, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(PlayerVoiceFX), "System.Void PlayerVoiceFX::CmdStartTimedVoiceFX(VoipManipulationManager/VoipFX,System.Single,System.Boolean)", InvokeUserCode_CmdStartTimedVoiceFX__VoipFX__Single__Boolean, requiresAuthority: false);
		RemoteProcedureCalls.RegisterCommand(typeof(PlayerVoiceFX), "System.Void PlayerVoiceFX::CmdSetNoMouthFX(System.Boolean)", InvokeUserCode_CmdSetNoMouthFX__Boolean, requiresAuthority: false);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerVoiceFX), "System.Void PlayerVoiceFX::RpcStartVoiceFX(VoipManipulationManager/VoipFX)", InvokeUserCode_RpcStartVoiceFX__VoipFX);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerVoiceFX), "System.Void PlayerVoiceFX::RpcResetVoiceFX()", InvokeUserCode_RpcResetVoiceFX);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerVoiceFX), "System.Void PlayerVoiceFX::RpcStartTimedVoiceFX(VoipManipulationManager/VoipFX,System.Single,System.Boolean)", InvokeUserCode_RpcStartTimedVoiceFX__VoipFX__Single__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerVoiceFX), "System.Void PlayerVoiceFX::RpcSetNoMouthFX(System.Boolean)", InvokeUserCode_RpcSetNoMouthFX__Boolean);
	}
}

}
