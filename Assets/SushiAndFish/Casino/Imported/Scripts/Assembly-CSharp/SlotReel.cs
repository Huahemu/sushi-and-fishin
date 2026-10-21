namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Linq;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;
using global::UnityEngine.UI;

public class SlotReel : NetworkBehaviour
{
	public AnimationCurve scrollCurve;

	public Image[] symbols;

	public Sprite[] atlas;

	private float[] _symbolPosY;

	private float _step;

	private float _botY;

	private float _reelLenght;

	private int _currentSeed;

	private Coroutine _spinRoutine;

	private void Awake()
	{
		_symbolPosY = new float[symbols.Length];
		for (int i = 0; i < symbols.Length; i++)
		{
			_symbolPosY[i] = symbols[i].rectTransform.anchoredPosition.y;
		}
		_step = Mathf.Abs(_symbolPosY[0] - _symbolPosY[1]);
		_reelLenght = Mathf.Abs(_symbolPosY[0] - _symbolPosY[^1]);
		_botY = _symbolPosY[^1];
	}

	[Server]
	public void ServerStartScrolling(float duration, int turnCount, int seed)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void SlotReel::ServerStartScrolling(System.Single,System.Int32,System.Int32)' called when server was not active");
		}
		else
		{
			RpcStartScrolling(duration, turnCount, seed);
		}
	}

	[ClientRpc]
	private void RpcStartScrolling(float duration, int turnCount, int seed)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteFloat(duration);
		writer.WriteVarInt(turnCount);
		writer.WriteVarInt(seed);
		SendRPCInternal("System.Void SlotReel::RpcStartScrolling(System.Single,System.Int32,System.Int32)", -1735686638, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private IEnumerator ScrollRoutine(float duration, int turnCount)
	{
		float timer = 0f;
		float totalDistance = _reelLenght * (float)turnCount;
		while (timer < duration)
		{
			float time = timer / duration;
			float offset = scrollCurve.Evaluate(time) * totalDistance;
			UpdateReel(offset);
			timer += Time.deltaTime;
			yield return null;
		}
		UpdateReel(totalDistance);
	}

	private void UpdateReel(float offset)
	{
		float num = offset % (_reelLenght + _step);
		int num2 = Mathf.FloorToInt(offset / (_reelLenght + _step));
		for (int i = 0; i < symbols.Length; i++)
		{
			float num3 = _symbolPosY[i] - num;
			while (num3 < _botY)
			{
				num3 += _reelLenght + _step;
				int num4 = (_currentSeed * 31 + num2) * 31 + i;
				int num5 = (num4 ^ (num4 >> 16)) * 2146121005;
				int num6 = (num5 ^ (num5 >> 15)) * -2073254261;
				int num7 = ((num6 ^ (num6 >> 16)) & 0x7FFFFFFF) % atlas.Length;
				symbols[i].sprite = atlas[num7];
			}
			Vector2 anchoredPosition = symbols[i].rectTransform.anchoredPosition;
			anchoredPosition.y = num3;
			symbols[i].rectTransform.anchoredPosition = anchoredPosition;
		}
	}

	[Server]
	public void ServerReset()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void SlotReel::ServerReset()' called when server was not active");
		}
		else
		{
			RpcReset();
		}
	}

	[ClientRpc]
	private void RpcReset()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void SlotReel::RpcReset()", 1040813013, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public List<int> GetResult()
	{
		List<int> list = new List<int>();
		symbols = symbols.OrderByDescending((Image s) => s.rectTransform.anchoredPosition.y).ToArray();
		for (int num = 0; num < symbols.Length - 1; num++)
		{
			Sprite sprite = symbols[num].sprite;
			list.Add(Array.IndexOf(atlas, sprite));
		}
		return list;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcStartScrolling__Single__Int32__Int32(float duration, int turnCount, int seed)
	{
		global::System.Random random = new global::System.Random(seed);
		_currentSeed = random.Next();
		symbols = symbols.OrderByDescending((Image s) => s.rectTransform.anchoredPosition.y).ToArray();
		for (int num = 0; num < symbols.Length; num++)
		{
			_symbolPosY[num] = symbols[num].rectTransform.anchoredPosition.y;
		}
		if (_spinRoutine != null)
		{
			StopCoroutine(_spinRoutine);
		}
		_spinRoutine = StartCoroutine(ScrollRoutine(duration, turnCount));
	}

	protected static void InvokeUserCode_RpcStartScrolling__Single__Int32__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcStartScrolling called on server.");
		}
		else
		{
			((SlotReel)obj).UserCode_RpcStartScrolling__Single__Int32__Int32(reader.ReadFloat(), reader.ReadVarInt(), reader.ReadVarInt());
		}
	}

	protected void UserCode_RpcReset()
	{
		if (_spinRoutine != null)
		{
			StopCoroutine(_spinRoutine);
		}
		symbols = symbols.OrderByDescending((Image s) => s.rectTransform.anchoredPosition.y).ToArray();
		for (int num = 0; num < symbols.Length; num++)
		{
			Vector2 anchoredPosition = symbols[num].rectTransform.anchoredPosition;
			anchoredPosition.y = _symbolPosY[num];
			symbols[num].rectTransform.anchoredPosition = anchoredPosition;
		}
	}

	protected static void InvokeUserCode_RpcReset(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcReset called on server.");
		}
		else
		{
			((SlotReel)obj).UserCode_RpcReset();
		}
	}

	static SlotReel()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(SlotReel), "System.Void SlotReel::RpcStartScrolling(System.Single,System.Int32,System.Int32)", InvokeUserCode_RpcStartScrolling__Single__Int32__Int32);
		RemoteProcedureCalls.RegisterRpc(typeof(SlotReel), "System.Void SlotReel::RpcReset()", InvokeUserCode_RpcReset);
	}
}

}
