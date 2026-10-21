namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::System.Runtime.InteropServices;
using global::DG.Tweening;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class ImmunityCross : ConsumableItem
{
	[Header("Settings")]
	[SerializeField]
	private float radius;

	[SerializeField]
	private float duration;

	[Header("References")]
	[SerializeField]
	private Transform statueTransform;

	[SerializeField]
	private Transform areaTransform;

	[SerializeField]
	private Animator anim;

	[Header("SFX")]
	[SerializeField]
	private SFXComponent onDestroySfx;

	private PlayerVoiceFX _playerVoiceFX;

	[SerializeField]
	private SFXLoopComponent loopComponent;

	private bool _isActive;

	private bool _hasEnded;

	[SyncVar]
	private float _usedDuration;

	private PlayerProfile _holderProfile;

	private readonly List<PlayerBuff> _buffs = new List<PlayerBuff>();

	[SerializeField]
	private MeshRenderer[] meshRenderers;

	public float Network_usedDuration
	{
		get
		{
			return _usedDuration;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _usedDuration, 2uL, null);
		}
	}

	private void Update()
	{
		if (!_isActive || !base.NetworkHolder)
		{
			return;
		}
		if (base.isServer)
		{
			Network_usedDuration = _usedDuration + Time.deltaTime / NetworkSingleton<UpgradeManager>.Instance.GetUpgradeData(_holderProfile.steamId, PlayerUpgradeType.Stakeholder);
			if (_usedDuration >= duration)
			{
				_hasEnded = true;
				foreach (PlayerBuff buff in _buffs)
				{
					buff.ResetBuffArea(PlayerBuffType.Immunity, this);
				}
				_buffs.Clear();
				loopComponent.RpcLoopSFX(play: false);
				onDestroySfx.RpcPlayOneShotWith3DPos();
				DestroyItem();
				return;
			}
		}
		float num = Mathf.InverseLerp(duration, 0f, _usedDuration);
		if (!DOTween.IsTweening(statueTransform))
		{
			statueTransform.localPosition = new Vector3(0f, 1f * num, 0.4f * num);
		}
		Color value = Color.Lerp(Color.black, Color.white, Mathf.SmoothStep(0f, 1f, num));
		MeshRenderer[] array = meshRenderers;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].material.SetColor("_Color", value);
		}
	}

	protected override void OnUseItem(bool isPressed)
	{
		if (_hasEnded)
		{
			return;
		}
		_isActive = isPressed;
		if (base.isServer)
		{
			if (_buffs.Count != MonoSingleton<LocalManager>.Instance.players.Count)
			{
				_buffs.Clear();
				foreach (PlayerReferences player in MonoSingleton<LocalManager>.Instance.players)
				{
					_buffs.Add(player.buff);
				}
			}
			foreach (PlayerBuff buff in _buffs)
			{
				BuffArea area = new BuffArea
				{
					Source = base.transform,
					Range = radius,
					Amount = 1f,
					IsActive = _isActive
				};
				buff.SetBuffArea(PlayerBuffType.Immunity, this, area);
			}
		}
		loopComponent.LoopSFX(_isActive);
		float endValue = (_isActive ? 1f : 0f);
		DOTween.To(() => anim.GetFloat("Blend"), (float x) =>
		{
			anim.SetFloat("Blend", x);
		}, endValue, 0.25f).SetEase(Ease.OutCubic);
		float num = Mathf.InverseLerp(duration, 0f, _usedDuration);
		statueTransform.DOLocalMove(_isActive ? new Vector3(0f, 1f * num, 0.4f * num) : Vector3.zero, 0.2f).SetEase(Ease.OutQuad);
		areaTransform.DOScale(isPressed ? (Vector3.one * radius) : Vector3.zero, 0.1f).SetEase(Ease.OutQuad);
	}

	protected override void OnPickedUp(PlayerInventory playerInventory)
	{
		base.OnPickedUp(playerInventory);
		if (_hasEnded)
		{
			return;
		}
		_holderProfile = playerInventory.GetComponent<PlayerProfile>();
		_buffs.Clear();
		foreach (PlayerReferences player in MonoSingleton<LocalManager>.Instance.players)
		{
			_buffs.Add(player.buff);
		}
		playerInventory.TryGetComponent<PlayerVoiceFX>(out _playerVoiceFX);
	}

	protected override void OnDropped(PlayerInventory playerInventory)
	{
		base.OnDropped(playerInventory);
		_isActive = false;
		foreach (PlayerBuff buff in _buffs)
		{
			buff.ResetBuffArea(PlayerBuffType.Immunity, this);
		}
		_buffs.Clear();
		ResetLocal();
		RpcReset();
		if ((bool)_playerVoiceFX)
		{
			_playerVoiceFX.RpcResetVoiceFX();
			_playerVoiceFX = null;
		}
	}

	protected override void OnHolderChanged(PlayerInventory oldHolder, PlayerInventory newHolder)
	{
		base.OnHolderChanged(oldHolder, newHolder);
		if (!newHolder)
		{
			ResetLocal();
		}
	}

	[ClientRpc]
	private void RpcReset()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void ImmunityCross::RpcReset()", 665502891, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void ResetLocal()
	{
		anim.SetFloat("Blend", 0f);
		anim.Update(0f);
		statueTransform.DOKill();
		statueTransform.localPosition = Vector3.zero;
		areaTransform.DOScale(0f, 0.1f).SetEase(Ease.OutQuad);
		loopComponent.LoopSFX(play: false);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcReset()
	{
		if (!base.isServer)
		{
			ResetLocal();
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
			((ImmunityCross)obj).UserCode_RpcReset();
		}
	}

	static ImmunityCross()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(ImmunityCross), "System.Void ImmunityCross::RpcReset()", InvokeUserCode_RpcReset);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteFloat(_usedDuration);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 2L) != 0L)
		{
			writer.WriteFloat(_usedDuration);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref _usedDuration, null, reader.ReadFloat());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 2L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _usedDuration, null, reader.ReadFloat());
		}
	}
}

}
