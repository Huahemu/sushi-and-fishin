namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Diagnostics;
using global::System.Runtime.CompilerServices;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::UnityEngine;

public class PlayerBuff : NetworkBehaviour
{
	[CompilerGenerated]
	private sealed class _003CRemoveBuffAfter_003Ed__12 : IEnumerator<object>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public float delay;

		public PlayerBuff _003C_003E4__this;

		public PlayerBuffType type;

		public float amount;

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
		public _003CRemoveBuffAfter_003Ed__12(int _003C_003E1__state)
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
			PlayerBuff playerBuff = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				_003C_003E2__current = new WaitForSeconds(delay);
				_003C_003E1__state = 1;
				return true;
			case 1:
				_003C_003E1__state = -1;
				playerBuff._buffs[type] -= amount;
				return false;
			}
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

	private readonly SyncDictionary<PlayerBuffType, float> _buffs = new SyncDictionary<PlayerBuffType, float>();

	private readonly Dictionary<PlayerBuffType, Dictionary<Item, BuffArea>> _buffAreas = new Dictionary<PlayerBuffType, Dictionary<Item, BuffArea>>();

	private readonly List<Item> _removeCache = new List<Item>();

	private PlayerBuffUI _pb;

	private void Awake()
	{
		_pb = MonoSingleton<LocalManager>.Instance.playerBuffUI;
	}

	public override void OnStartServer()
	{
		_buffs[PlayerBuffType.TipsyFortune] = 1f;
		_buffs[PlayerBuffType.InspiringMelody] = 0f;
		_buffs[PlayerBuffType.Immunity] = 0f;
	}

	public override void OnStartClient()
	{
		if (base.isLocalPlayer)
		{
			SyncDictionary<PlayerBuffType, float> buffs = _buffs;
			buffs.OnChange = (Action<SyncIDictionary<PlayerBuffType, float>.Operation, PlayerBuffType, float>)Delegate.Combine(buffs.OnChange, new Action<SyncIDictionary<PlayerBuffType, float>.Operation, PlayerBuffType, float>(OnBuffsChanged));
		}
	}

	private void OnBuffsChanged(SyncIDictionary<PlayerBuffType, float>.Operation op, PlayerBuffType key, float oldValue)
	{
		if (base.isLocalPlayer)
		{
			_pb.OnChanged(key, _buffs[key]);
		}
	}

	[Server]
	public void SetBuffArea(PlayerBuffType type, Item item, BuffArea area)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void PlayerBuff::SetBuffArea(PlayerBuffType,Item,BuffArea)' called when server was not active");
			return;
		}
		if (!_buffAreas.TryGetValue(type, out var value))
		{
			value = new Dictionary<Item, BuffArea>();
			_buffAreas[type] = value;
		}
		value[item] = area;
	}

	[Server]
	public void ResetBuffArea(PlayerBuffType type, Item item)
	{
		Dictionary<Item, BuffArea> value;
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void PlayerBuff::ResetBuffArea(PlayerBuffType,Item)' called when server was not active");
		}
		else if (_buffAreas.TryGetValue(type, out value))
		{
			value.Remove(item);
		}
	}

	private void Update()
	{
		if (!base.isServer)
		{
			return;
		}
		Vector3 position = base.transform.position;
		foreach (var (playerBuffType2, dictionary2) in _buffAreas)
		{
			float num = 0f;
			_removeCache.Clear();
			switch (playerBuffType2)
			{
			case PlayerBuffType.Immunity:
				foreach (KeyValuePair<Item, BuffArea> item in dictionary2)
				{
					Item key2 = item.Key;
					BuffArea value2 = item.Value;
					if (!value2.Source)
					{
						_removeCache.Add(key2);
					}
					else if (value2.IsActive && !((value2.Source.position - position).sqrMagnitude > value2.Range * value2.Range))
					{
						num = 1f;
						break;
					}
				}
				break;
			case PlayerBuffType.InspiringMelody:
				foreach (KeyValuePair<Item, BuffArea> item2 in dictionary2)
				{
					Item key = item2.Key;
					BuffArea value = item2.Value;
					if (!value.Source)
					{
						_removeCache.Add(key);
					}
					else if (value.IsActive && !((value.Source.position - position).sqrMagnitude > value.Range * value.Range))
					{
						num += value.Amount;
						if (num >= 1f)
						{
							break;
						}
					}
				}
				num = Mathf.Clamp01(num);
				break;
			}
			foreach (Item item3 in _removeCache)
			{
				dictionary2.Remove(item3);
			}
			_buffs[playerBuffType2] = num;
		}
	}

	[Server]
	public void ApplyBuff(PlayerBuffType type, float amount, float duration = 0f)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void PlayerBuff::ApplyBuff(PlayerBuffType,System.Single,System.Single)' called when server was not active");
			return;
		}
		if (_buffs.ContainsKey(type))
		{
			_buffs[type] += amount;
		}
		else
		{
			_buffs[type] = 1f + amount;
		}
		if (duration > 0f)
		{
			StartCoroutine(RemoveBuffAfter(type, amount, duration));
		}
	}

	[IteratorStateMachine(typeof(_003CRemoveBuffAfter_003Ed__12))]
	[Server]
	private IEnumerator RemoveBuffAfter(PlayerBuffType type, float amount, float delay)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Collections.IEnumerator PlayerBuff::RemoveBuffAfter(PlayerBuffType,System.Single,System.Single)' called when server was not active");
			return null;
		}
		return new _003CRemoveBuffAfter_003Ed__12(0)
		{
			_003C_003E4__this = this,
			type = type,
			amount = amount,
			delay = delay
		};
	}

	public float GetValue(PlayerBuffType type)
	{
		return _buffs.GetValueOrDefault(type, 0f);
	}

	public PlayerBuff()
	{
		InitSyncObject(_buffs);
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
