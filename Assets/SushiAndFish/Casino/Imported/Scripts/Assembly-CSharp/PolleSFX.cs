namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::FMOD.Studio;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class PolleSFX : NetworkBehaviour
{
	[SerializeField]
	private EventReference polleEvent;

	[SerializeField]
	private EventReference exitEvent;

	[SerializeField]
	private int amtOfVoiceLines;

	private EventInstance _eventInstance;

	private int _lastLine = -1;

	private bool _isPlaying;

	public void PlayPolleSays()
	{
		if (!polleEvent.IsNull && !_isPlaying)
		{
			_isPlaying = true;
			int randomNumber = GetRandomNumber();
			PlayPolleSays(randomNumber);
			CmdPlayPolleSays(randomNumber);
		}
	}

	[Command(requiresAuthority = false)]
	private void CmdPlayPolleSays(int voiceLineIndex)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(voiceLineIndex);
		SendCommandInternal("System.Void PolleSFX::CmdPlayPolleSays(System.Int32)", 862651091, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcPlayPolleSays(int voiceLineIndex)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(voiceLineIndex);
		SendRPCInternal("System.Void PolleSFX::RpcPlayPolleSays(System.Int32)", 201215486, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void PlayPolleSays(int voiceLineIndex)
	{
		if (_eventInstance.isValid())
		{
			_eventInstance.getPlaybackState(out var state);
			if (state == PLAYBACK_STATE.PLAYING)
			{
				return;
			}
		}
		_eventInstance = RuntimeManager.CreateInstance(polleEvent);
		RuntimeManager.AttachInstanceToGameObject(_eventInstance, base.gameObject);
		_eventInstance.setParameterByName("PolleSays", voiceLineIndex);
		StartCoroutine("PolleRoutine");
	}

	public IEnumerator PolleRoutine()
	{
		_eventInstance.start();
		yield return new WaitForSeconds(0.3f);
		_eventInstance.getPlaybackState(out var playbackState);
		while (playbackState == PLAYBACK_STATE.PLAYING)
		{
			_eventInstance.getPlaybackState(out playbackState);
			yield return new WaitForSeconds(0.1f);
		}
		_eventInstance.release();
		if (!exitEvent.IsNull)
		{
			SFXManager.SFXOneShot3DAttached(exitEvent, base.gameObject);
			yield return new WaitForSeconds(3f);
		}
		_isPlaying = false;
	}

	private int GetRandomNumber()
	{
		if (amtOfVoiceLines <= 1)
		{
			return 0;
		}
		int num = amtOfVoiceLines;
		if (_lastLine >= 0)
		{
			num--;
		}
		int num2 = Random.Range(0, num);
		if (num2 == _lastLine)
		{
			num2++;
		}
		_lastLine = num2;
		return num2;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_CmdPlayPolleSays__Int32(int voiceLineIndex)
	{
		RpcPlayPolleSays(voiceLineIndex);
	}

	protected static void InvokeUserCode_CmdPlayPolleSays__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdPlayPolleSays called on client.");
		}
		else
		{
			((PolleSFX)obj).UserCode_CmdPlayPolleSays__Int32(reader.ReadVarInt());
		}
	}

	protected void UserCode_RpcPlayPolleSays__Int32(int voiceLineIndex)
	{
		if (!_isPlaying)
		{
			_isPlaying = true;
			PlayPolleSays(voiceLineIndex);
		}
	}

	protected static void InvokeUserCode_RpcPlayPolleSays__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPlayPolleSays called on server.");
		}
		else
		{
			((PolleSFX)obj).UserCode_RpcPlayPolleSays__Int32(reader.ReadVarInt());
		}
	}

	static PolleSFX()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(PolleSFX), "System.Void PolleSFX::CmdPlayPolleSays(System.Int32)", InvokeUserCode_CmdPlayPolleSays__Int32, requiresAuthority: false);
		RemoteProcedureCalls.RegisterRpc(typeof(PolleSFX), "System.Void PolleSFX::RpcPlayPolleSays(System.Int32)", InvokeUserCode_RpcPlayPolleSays__Int32);
	}
}

}
