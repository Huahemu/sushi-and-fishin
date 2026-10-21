namespace SushiAndFish.Casino.Original
{
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class RandomNPCSFX : NetworkBehaviour
{
	[SerializeField]
	private EventReference eventReference;

	[SerializeField]
	private GameObject head;

	private int assignedVoice;

	private float assignedPitch;

	[SerializeField]
	private float minWaitTime = 7f;

	[SerializeField]
	private float maxWaitTime = 45f;

	private float waitTime;

	[SerializeField]
	private float interactCooldownTime = 2.5f;

	private float interactCooldownTimer;

	private int amt_of_voices = 3;

	private int amt_of_lines = 10;

	public override void OnStartServer()
	{
		assignedPitch = Random.Range(-4f, 4f);
		assignedVoice = Random.Range(0, amt_of_voices + 1);
		SetNextVoiceWaitTime();
	}

	private void SetNextVoiceWaitTime()
	{
		waitTime = Time.time + Random.Range(minWaitTime, maxWaitTime);
	}

	public void ManagedUpdate()
	{
		if (base.gameObject.activeInHierarchy && Time.time >= waitTime)
		{
			PlayRandomVoiceLine();
			SetNextVoiceWaitTime();
		}
	}

	private void PlayRandomVoiceLine()
	{
		if (base.isServer)
		{
			SFXParams[] sFXParams = new SFXParams[3]
			{
				new SFXParams("NPCVoice", assignedVoice),
				new SFXParams("NPCPitch", assignedPitch),
				new SFXParams("NPCVoiceLine", Random.Range(0, amt_of_lines + 1))
			};
			RpcPlayRandomVoiceLine(sFXParams);
		}
	}

	[ClientRpc]
	private void RpcPlayRandomVoiceLine(SFXParams[] sFXParams)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		GeneratedNetworkCode._Write_SFXParams_005B_005D(writer, sFXParams);
		SendRPCInternal("System.Void RandomNPCSFX::RpcPlayRandomVoiceLine(SFXParams[])", -1511883347, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void CmdInteractPlayRandomVoiceLine()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void RandomNPCSFX::CmdInteractPlayRandomVoiceLine()' called when server was not active");
		}
		else if (base.gameObject.activeInHierarchy && !(Time.time <= interactCooldownTimer))
		{
			SFXParams[] sFXParams = new SFXParams[3]
			{
				new SFXParams("NPCVoice", assignedVoice),
				new SFXParams("NPCPitch", assignedPitch),
				new SFXParams("NPCVoiceLine", Random.Range(0, amt_of_lines + 1))
			};
			RpcPlayRandomVoiceLine(sFXParams);
			SetNextVoiceWaitTime();
			interactCooldownTimer = Time.time + interactCooldownTime;
		}
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcPlayRandomVoiceLine__SFXParams_005B_005D(SFXParams[] sFXParams)
	{
		if (base.gameObject.activeInHierarchy)
		{
			if (head != null)
			{
				SFXManager.SFXOneShot3DAttachedWithParameters(eventReference, sFXParams, head);
			}
			else
			{
				SFXManager.SFXOneShot3DAttachedWithParameters(eventReference, sFXParams, base.gameObject);
			}
		}
	}

	protected static void InvokeUserCode_RpcPlayRandomVoiceLine__SFXParams_005B_005D(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPlayRandomVoiceLine called on server.");
		}
		else
		{
			((RandomNPCSFX)obj).UserCode_RpcPlayRandomVoiceLine__SFXParams_005B_005D(GeneratedNetworkCode._Read_SFXParams_005B_005D(reader));
		}
	}

	static RandomNPCSFX()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(RandomNPCSFX), "System.Void RandomNPCSFX::RpcPlayRandomVoiceLine(SFXParams[])", InvokeUserCode_RpcPlayRandomVoiceLine__SFXParams_005B_005D);
	}
}

}
