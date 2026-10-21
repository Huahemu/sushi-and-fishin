namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::MoreMountains.Feedbacks;
using global::UnityEngine;
using global::UnityEngine.Events;

public class Hoop : NetworkBehaviour
{
	[SerializeField]
	private Transform hoopCenterPosition;

	[SerializeField]
	private MMF_Player registerHoopFb;

	public UnityEvent onHoopRegistered;

	private readonly Dictionary<Item, Vector3> _entryPositions = new Dictionary<Item, Vector3>();

	private void OnTriggerEnter(Collider other)
	{
		if (base.isServer && (bool)other.attachedRigidbody && other.attachedRigidbody.TryGetComponent<Basketball>(out var component))
		{
			if (component.transform.position.y < hoopCenterPosition.position.y)
			{
				other.attachedRigidbody.linearVelocity *= 0.3f;
				other.attachedRigidbody.AddForce(Vector3.down * 100f, ForceMode.Acceleration);
			}
			else if (!_entryPositions.ContainsKey(component))
			{
				_entryPositions[component] = component.transform.position;
			}
		}
	}

	private void OnTriggerExit(Collider other)
	{
		if (base.isServer && (bool)other.attachedRigidbody && other.attachedRigidbody.TryGetComponent<Basketball>(out var component) && _entryPositions.TryGetValue(component, out var value))
		{
			if (value.y > hoopCenterPosition.position.y && component.transform.position.y < hoopCenterPosition.position.y)
			{
				RegisterHoop(component);
			}
			_entryPositions.Remove(component);
		}
	}

	private void RegisterHoop(Basketball ball)
	{
		RpcRegisterHoop();
		onHoopRegistered?.Invoke();
	}

	[ClientRpc]
	private void RpcRegisterHoop()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Hoop::RpcRegisterHoop()", -1963564451, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcRegisterHoop()
	{
		registerHoopFb.PlayFeedbacks();
	}

	protected static void InvokeUserCode_RpcRegisterHoop(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcRegisterHoop called on server.");
		}
		else
		{
			((Hoop)obj).UserCode_RpcRegisterHoop();
		}
	}

	static Hoop()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(Hoop), "System.Void Hoop::RpcRegisterHoop()", InvokeUserCode_RpcRegisterHoop);
	}
}

}
