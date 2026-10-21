namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Diagnostics;
using global::System.Runtime.CompilerServices;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::TMPro;
using global::UnityEngine;

public class DebtBag : Item
{
	[CompilerGenerated]
	private sealed class _003CSuckRoutine_003Ed__13 : IEnumerator<object>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public DebtBag _003C_003E4__this;

		public Vector3 pipeStart;

		public Vector3 dir;

		private float _003Ctime_003E5__2;

		private float _003Cspeed_003E5__3;

		private Transform _003Ct_003E5__4;

		private Vector3 _003CstartScale_003E5__5;

		private float _003CshrinkTimer_003E5__6;

		object IEnumerator<object>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CSuckRoutine_003Ed__13(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
		}

		private bool MoveNext()
		{
			int num = _003C_003E1__state;
			DebtBag debtBag = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				_003C_003E2__current = new WaitForSeconds(UnityEngine.Random.Range(0f, 0.5f));
				_003C_003E1__state = 1;
				return true;
			case 1:
				_003C_003E1__state = -1;
				_003Ctime_003E5__2 = 0f;
				_003Cspeed_003E5__3 = 0f;
				_003Ct_003E5__4 = debtBag.transform;
				goto IL_0155;
			case 2:
				_003C_003E1__state = -1;
				goto IL_0155;
			case 3:
				{
					_003C_003E1__state = -1;
					break;
				}
				IL_0155:
				if (_003Ctime_003E5__2 < debtBag.duration)
				{
					float deltaTime = Time.deltaTime;
					_003Ctime_003E5__2 += deltaTime;
					_003Cspeed_003E5__3 += debtBag.acceleration * deltaTime;
					_003Cspeed_003E5__3 = Mathf.Min(_003Cspeed_003E5__3, debtBag.maxSpeed);
					float num2 = Vector3.Dot(_003Ct_003E5__4.position - pipeStart, dir);
					Vector3 b = pipeStart + dir * num2;
					Vector3 vector = Vector3.Lerp(_003Ct_003E5__4.position, b, deltaTime * debtBag.alignSpeed);
					Vector3 vector2 = dir * (_003Cspeed_003E5__3 * deltaTime);
					_003Ct_003E5__4.position = vector + vector2;
					_003C_003E2__current = null;
					_003C_003E1__state = 2;
					return true;
				}
				_003CstartScale_003E5__5 = _003Ct_003E5__4.localScale;
				_003CshrinkTimer_003E5__6 = 0f;
				break;
			}
			if (_003CshrinkTimer_003E5__6 < debtBag.shrinkTime)
			{
				_003CshrinkTimer_003E5__6 += Time.deltaTime;
				float t = _003CshrinkTimer_003E5__6 / debtBag.shrinkTime;
				debtBag.transform.localScale = Vector3.Lerp(_003CstartScale_003E5__5, Vector3.zero, t);
				_003C_003E2__current = null;
				_003C_003E1__state = 3;
				return true;
			}
			NetworkSingleton<WinSceneManager>.Instance.debtBags.Remove(debtBag);
			NetworkServer.Destroy(debtBag.gameObject);
			return false;
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}
	}

	[Header("Settings")]
	[SerializeField]
	private float alignSpeed = 1f;

	[SerializeField]
	private float acceleration = 10f;

	[SerializeField]
	private float maxSpeed = 50f;

	[SerializeField]
	private float duration = 1.5f;

	[SerializeField]
	private float shrinkTime = 0.5f;

	[Header("References")]
	[SerializeField]
	private TextMeshPro moneyText;

	public long moneyInBag;

	public override void OnStartServer()
	{
		base.OnStartServer();
		NetworkSingleton<WinSceneManager>.Instance.debtBags.Add(this);
	}

	[Server]
	public void ServerSetMoney(long money)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void DebtBag::ServerSetMoney(System.Int64)' called when server was not active");
			return;
		}
		moneyInBag = money;
		RpcSetMoneyText(MoneyFormatter.FormatWithDollar(money));
	}

	[ClientRpc]
	private void RpcSetMoneyText(string text)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteString(text);
		SendRPCInternal("System.Void DebtBag::RpcSetMoneyText(System.String)", -2012068234, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void ServerLock()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void DebtBag::ServerLock()' called when server was not active");
			return;
		}
		ServerDrop();
		IsInteractable = false;
		Rb.isKinematic = true;
		Rb.interpolation = RigidbodyInterpolation.None;
		RpcLock();
	}

	[ClientRpc]
	private void RpcLock()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void DebtBag::RpcLock()", -1393171932, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	public void ServerSuckToPipe(Vector3 position, Vector3 direction)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void DebtBag::ServerSuckToPipe(UnityEngine.Vector3,UnityEngine.Vector3)' called when server was not active");
			return;
		}
		RpcDisableColliders();
		StartCoroutine(SuckRoutine(position, direction.normalized));
	}

	[IteratorStateMachine(typeof(_003CSuckRoutine_003Ed__13))]
	[Server]
	private IEnumerator SuckRoutine(Vector3 pipeStart, Vector3 dir)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Collections.IEnumerator DebtBag::SuckRoutine(UnityEngine.Vector3,UnityEngine.Vector3)' called when server was not active");
			return null;
		}
		return new _003CSuckRoutine_003Ed__13(0)
		{
			_003C_003E4__this = this,
			pipeStart = pipeStart,
			dir = dir
		};
	}

	[ClientRpc]
	private void RpcDisableColliders()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void DebtBag::RpcDisableColliders()", 1300717950, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcSetMoneyText__String(string text)
	{
		moneyText.text = text;
		InteractableName = text;
	}

	protected static void InvokeUserCode_RpcSetMoneyText__String(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcSetMoneyText called on server.");
		}
		else
		{
			((DebtBag)obj).UserCode_RpcSetMoneyText__String(reader.ReadString());
		}
	}

	protected void UserCode_RpcLock()
	{
		if (!base.isServer)
		{
			IsInteractable = false;
			Rb.isKinematic = true;
			Rb.interpolation = RigidbodyInterpolation.None;
		}
	}

	protected static void InvokeUserCode_RpcLock(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcLock called on server.");
		}
		else
		{
			((DebtBag)obj).UserCode_RpcLock();
		}
	}

	protected void UserCode_RpcDisableColliders()
	{
		Collider[] componentsInChildren = GetComponentsInChildren<Collider>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].enabled = false;
		}
	}

	protected static void InvokeUserCode_RpcDisableColliders(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcDisableColliders called on server.");
		}
		else
		{
			((DebtBag)obj).UserCode_RpcDisableColliders();
		}
	}

	static DebtBag()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(DebtBag), "System.Void DebtBag::RpcSetMoneyText(System.String)", InvokeUserCode_RpcSetMoneyText__String);
		RemoteProcedureCalls.RegisterRpc(typeof(DebtBag), "System.Void DebtBag::RpcLock()", InvokeUserCode_RpcLock);
		RemoteProcedureCalls.RegisterRpc(typeof(DebtBag), "System.Void DebtBag::RpcDisableColliders()", InvokeUserCode_RpcDisableColliders);
	}
}

}
