namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class FollowUsSceneManager : NetworkSingleton<FollowUsSceneManager>
{
	[Header("Settings")]
	[SerializeField]
	private float setSkippableDelay;

	[Header("References")]
	[SerializeField]
	private SkipUI skipUI;

	public override void OnStartServer()
	{
		base.OnStartServer();
		StartCoroutine(SetSkippableRoutine());
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		UICursorSimple.Instance?.ShowCursor();
	}

	private IEnumerator SetSkippableRoutine()
	{
		yield return new WaitForSeconds(setSkippableDelay);
		RpcSetSkippable();
	}

	[ClientRpc]
	private void RpcSetSkippable()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void FollowUsSceneManager::RpcSetSkippable()", 122257351, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcSetSkippable()
	{
		skipUI.Reset();
		skipUI.SetSkippableServer();
		skipUI.SetSkippableForLocal();
	}

	protected static void InvokeUserCode_RpcSetSkippable(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetSkippable called on server.");
		}
		else
		{
			((FollowUsSceneManager)obj).UserCode_RpcSetSkippable();
		}
	}

	static FollowUsSceneManager()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(FollowUsSceneManager), "System.Void FollowUsSceneManager::RpcSetSkippable()", InvokeUserCode_RpcSetSkippable);
	}
}

}
