namespace SushiAndFish.Casino.Original
{
using global::Mirror;
using global::Mirror.RemoteCalls;
using SushiAndFish.Casino.Original.Smooth;
using global::UnityEngine;

public class ServerAuthorityExamplePlayerController : NetworkBehaviour
{
	private Rigidbody rb;

	public float transformMovementSpeed = 30f;

	public float rigidbodyMovementForce = 500f;

	private SmoothSyncMirror smoothSync;

	private void Awake()
	{
		rb = GetComponent<Rigidbody>();
		smoothSync = GetComponent<SmoothSyncMirror>();
	}

	public override void OnStartServer()
	{
		rb.isKinematic = false;
		base.OnStartServer();
	}

	private void Update()
	{
		if (base.isOwned)
		{
			if (Input.GetKeyUp(KeyCode.DownArrow))
			{
				CmdMove(KeyCode.DownArrow);
			}
			if (Input.GetKeyUp(KeyCode.UpArrow))
			{
				CmdMove(KeyCode.UpArrow);
			}
			if (Input.GetKeyUp(KeyCode.LeftArrow))
			{
				CmdMove(KeyCode.LeftArrow);
			}
			if (Input.GetKeyUp(KeyCode.RightArrow))
			{
				CmdMove(KeyCode.RightArrow);
			}
			if (Input.GetKeyUp(KeyCode.T))
			{
				CmdTeleport();
			}
		}
	}

	[Command]
	private void CmdTeleport()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendCommandInternal("System.Void ServerAuthorityExamplePlayerController::CmdTeleport()", 55251365, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	[Command]
	private void CmdMove(KeyCode keyCode)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		GeneratedNetworkCode._Write_UnityEngine_002EKeyCode(writer, keyCode);
		SendCommandInternal("System.Void ServerAuthorityExamplePlayerController::CmdMove(UnityEngine.KeyCode)", -953675450, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_CmdTeleport()
	{
		smoothSync.teleportAnyObjectFromServer(base.transform.position + Vector3.right * 5f, base.transform.rotation, base.transform.localScale);
	}

	protected static void InvokeUserCode_CmdTeleport(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdTeleport called on client.");
		}
		else
		{
			((ServerAuthorityExamplePlayerController)obj).UserCode_CmdTeleport();
		}
	}

	protected void UserCode_CmdMove__KeyCode(KeyCode keyCode)
	{
		switch (keyCode)
		{
		case KeyCode.DownArrow:
			rb.AddForce(new Vector3(0f, -1.5f, -1f) * rigidbodyMovementForce);
			break;
		case KeyCode.UpArrow:
			rb.AddForce(new Vector3(0f, 1.5f, 1f) * rigidbodyMovementForce);
			break;
		case KeyCode.LeftArrow:
			rb.AddForce(new Vector3(-1f, 0f, 0f) * rigidbodyMovementForce);
			break;
		case KeyCode.RightArrow:
			rb.AddForce(new Vector3(1f, 0f, 0f) * rigidbodyMovementForce);
			break;
		}
	}

	protected static void InvokeUserCode_CmdMove__KeyCode(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdMove called on client.");
		}
		else
		{
			((ServerAuthorityExamplePlayerController)obj).UserCode_CmdMove__KeyCode(GeneratedNetworkCode._Read_UnityEngine_002EKeyCode(reader));
		}
	}

	static ServerAuthorityExamplePlayerController()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(ServerAuthorityExamplePlayerController), "System.Void ServerAuthorityExamplePlayerController::CmdTeleport()", InvokeUserCode_CmdTeleport, requiresAuthority: true);
		RemoteProcedureCalls.RegisterCommand(typeof(ServerAuthorityExamplePlayerController), "System.Void ServerAuthorityExamplePlayerController::CmdMove(UnityEngine.KeyCode)", InvokeUserCode_CmdMove__KeyCode, requiresAuthority: true);
	}
}

}
