namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::DG.Tweening;
using global::FMOD.Studio;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class Wheel : NetworkBehaviour
{
	[Header("Wheel Settings")]
	[SerializeField]
	protected float spinDuration = 3f;

	[SerializeField]
	protected int minTurnAmount = 3;

	[SerializeField]
	protected bool spinDirection;

	[SerializeField]
	protected Ease easing = Ease.OutCubic;

	[SerializeField]
	protected float lightIntensity = 20f;

	[Header("References")]
	[SerializeField]
	private Transform wheelTransform;

	[SerializeField]
	private Transform resultsParent;

	[SerializeField]
	protected Transform resultSelector;

	[SerializeField]
	private Light resultsLight;

	private WheelResult[] _results;

	[Header("SFX")]
	[SerializeField]
	protected EventReference sfxSpinEvent;

	protected EventInstance sfxSpinInstance;

	protected bool _isSpinning;

	private WheelResult[] Results
	{
		get
		{
			if (_results == null || _results.Length == 0)
			{
				_results = resultsParent.GetComponentsInChildren<WheelResult>();
			}
			return _results;
		}
	}

	public event Action<string> OnWheelStopped;

	[Server]
	public virtual void SpinTheWheel(global::System.Random rng)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Wheel::SpinTheWheel(System.Random)' called when server was not active");
		}
		else if (!_isSpinning)
		{
			_isSpinning = true;
			float num = (float)(rng.NextDouble() * 360.0);
			float num2 = (float)minTurnAmount * 360f + num;
			if (spinDirection)
			{
				num2 *= -1f;
			}
			RpcSpinWheel(num2, spinDuration);
			StartCoroutine(WaitAndStop());
		}
	}

	protected IEnumerator WaitAndStop()
	{
		yield return new WaitForSeconds(spinDuration);
		StopTheWheel();
	}

	private void StopTheWheel()
	{
		string obj = FindResult();
		ResetWheel();
		OnWheelStopped?.Invoke(obj);
	}

	public void ResetWheel()
	{
		_isSpinning = false;
		RpcResetWheel();
	}

	[ClientRpc]
	private void RpcResetWheel()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Wheel::RpcResetWheel()", -1712800393, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private string FindResult()
	{
		Vector3 position = resultSelector.transform.position;
		float num = float.MaxValue;
		int num2 = -1;
		for (int i = 0; i < Results.Length; i++)
		{
			float sqrMagnitude = (Results[i].transform.position - position).sqrMagnitude;
			if (sqrMagnitude < num)
			{
				num = sqrMagnitude;
				num2 = i;
			}
		}
		if (num2 >= 0)
		{
			RpcResultFeedback(num2);
		}
		if (num2 < 0)
		{
			return "Unknown";
		}
		return Results[num2].result;
	}

	[ClientRpc]
	protected void RpcSpinWheel(float finalAngle, float duration)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteFloat(finalAngle);
		writer.WriteFloat(duration);
		SendRPCInternal("System.Void Wheel::RpcSpinWheel(System.Single,System.Single)", 617594292, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcResultFeedback(int index)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarInt(index);
		SendRPCInternal("System.Void Wheel::RpcResultFeedback(System.Int32)", 1059369322, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcResetWheel()
	{
		sfxSpinInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
		sfxSpinInstance.release();
	}

	protected static void InvokeUserCode_RpcResetWheel(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcResetWheel called on server.");
		}
		else
		{
			((Wheel)obj).UserCode_RpcResetWheel();
		}
	}

	protected void UserCode_RpcSpinWheel__Single__Single(float finalAngle, float duration)
	{
		wheelTransform.DOLocalRotate(new Vector3(0f, 0f, 0f - finalAngle), duration, RotateMode.FastBeyond360).SetEase(easing);
		sfxSpinInstance = RuntimeManager.CreateInstance(sfxSpinEvent);
		sfxSpinInstance.set3DAttributes(base.transform.position.To3DAttributes());
		sfxSpinInstance.setParameterByName("spinDuration", duration * 1000f);
		sfxSpinInstance.start();
	}

	protected static void InvokeUserCode_RpcSpinWheel__Single__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSpinWheel called on server.");
		}
		else
		{
			((Wheel)obj).UserCode_RpcSpinWheel__Single__Single(reader.ReadFloat(), reader.ReadFloat());
		}
	}

	protected void UserCode_RpcResultFeedback__Int32(int index)
	{
		if (index < 0 || index >= Results.Length)
		{
			return;
		}
		Results[index].SelectedResultFeedback();
		if ((bool)resultsLight)
		{
			resultsLight.transform.rotation = Quaternion.LookRotation(Results[index].transform.position - resultsLight.transform.position);
			resultsLight.DOIntensity(lightIntensity, 0.5f).OnComplete(() =>
			{
				resultsLight.DOIntensity(0f, 1f);
			});
		}
	}

	protected static void InvokeUserCode_RpcResultFeedback__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcResultFeedback called on server.");
		}
		else
		{
			((Wheel)obj).UserCode_RpcResultFeedback__Int32(reader.ReadVarInt());
		}
	}

	static Wheel()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(Wheel), "System.Void Wheel::RpcResetWheel()", InvokeUserCode_RpcResetWheel);
		RemoteProcedureCalls.RegisterRpc(typeof(Wheel), "System.Void Wheel::RpcSpinWheel(System.Single,System.Single)", InvokeUserCode_RpcSpinWheel__Single__Single);
		RemoteProcedureCalls.RegisterRpc(typeof(Wheel), "System.Void Wheel::RpcResultFeedback(System.Int32)", InvokeUserCode_RpcResultFeedback__Int32);
	}
}

}
