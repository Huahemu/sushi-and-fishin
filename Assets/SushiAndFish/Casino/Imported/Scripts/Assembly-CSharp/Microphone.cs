namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Runtime.InteropServices;
using global::DG.Tweening;
using global::Dissonance;
using global::Dissonance.Integrations.MirrorIgnorance;
using SushiAndFish.Casino.Original.Extensions;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::TMPro;
using global::UnityEngine;

public class Microphone : ConsumableItem
{
	[Header("Settings")]
	[SerializeField]
	private float minThreshold = 0.1f;

	[SerializeField]
	private float maxThreshold = 0.5f;

	[SerializeField]
	private float chargeIncrease = 0.1f;

	[SerializeField]
	private float chargeDecrease = 0.1f;

	[SerializeField]
	private float amplitudeLerp = 0.01f;

	[SerializeField]
	private float amplitudeMultiplier = 100f;

	[SerializeField]
	private float inputRange = 2f;

	[SerializeField]
	private float buffRange = 10f;

	[SerializeField]
	private float duration = 30f;

	[Header("References")]
	[SerializeField]
	private TextMeshPro multiplierText;

	[SerializeField]
	private Transform durationIndicator;

	[SerializeField]
	private Transform chargeIndicator;

	[SerializeField]
	private Transform buffArea;

	[SerializeField]
	private Animator anim;

	[SerializeField]
	private Transform micTransform;

	[Header("SFX")]
	[SerializeField]
	private EventReference micOnSfx;

	[SerializeField]
	private EventReference micTapSfx;

	[SerializeField]
	private EventReference micClickSfx;

	private bool _isActive;

	private bool _hasEnded;

	[SerializeField]
	[SyncVar(hook = "OnAmplitudeChanged")]
	private float currentAmplitude;

	[SerializeField]
	[SyncVar(hook = "OnChargeChanged")]
	private float currentCharge;

	private Dictionary<PlayerController, float> _amplitudes = new Dictionary<PlayerController, float>();

	private PlayerProfile _holderProfile;

	private PlayerVoiceFX _playerVoiceFX;

	private VoicePlayerState _localVps;

	private PlayerController _localController;

	private List<PlayerBuff> _buffs = new List<PlayerBuff>();

	public Action<float, float> _Mirror_SyncVarHookDelegate_currentAmplitude;

	public Action<float, float> _Mirror_SyncVarHookDelegate_currentCharge;

	public float NetworkcurrentAmplitude
	{
		get
		{
			return currentAmplitude;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref currentAmplitude, 2uL, _Mirror_SyncVarHookDelegate_currentAmplitude);
		}
	}

	public float NetworkcurrentCharge
	{
		get
		{
			return currentCharge;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref currentCharge, 4uL, _Mirror_SyncVarHookDelegate_currentCharge);
		}
	}

	private void OnAmplitudeChanged(float oldValue, float newValue)
	{
	}

	private void OnChargeChanged(float oldValue, float newValue)
	{
		chargeIndicator.localScale = new Vector3(1f, Mathf.Clamp(newValue, 0.01f, 1f), 1f);
	}

	protected override void OnPickedUp(PlayerInventory playerInventory)
	{
		base.OnPickedUp(playerInventory);
		playerInventory.TryGetComponent<PlayerVoiceFX>(out _playerVoiceFX);
		playerInventory.TryGetComponent<PlayerProfile>(out _holderProfile);
		_buffs.Clear();
		foreach (PlayerReferences player in MonoSingleton<LocalManager>.Instance.players)
		{
			_buffs.Add(player.buff);
		}
		if ((bool)_playerVoiceFX)
		{
			_playerVoiceFX.RpcStartVoiceFX(VoipManipulationManager.VoipFX.Radio);
		}
		if (_isActive && !_hasEnded)
		{
			RpcSetArea(isEnabled: true);
		}
	}

	protected override void OnDropped(PlayerInventory playerInventory)
	{
		base.OnDropped(playerInventory);
		NetworkcurrentAmplitude = 0f;
		foreach (PlayerBuff buff in _buffs)
		{
			buff.ResetBuffArea(PlayerBuffType.InspiringMelody, this);
		}
		_buffs.Clear();
		if ((bool)_playerVoiceFX)
		{
			_playerVoiceFX.RpcResetVoiceFX();
			_playerVoiceFX = null;
		}
		RpcSetArea(isEnabled: false);
		RpcOnDropped();
	}

	[ClientRpc]
	private void RpcOnDropped()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Microphone::RpcOnDropped()", 1197080143, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	protected override void OnUseItem(bool isPressed)
	{
		if (isPressed && !_isActive)
		{
			_isActive = true;
			MirrorIgnorancePlayer component = NetworkClient.localPlayer.GetComponent<MirrorIgnorancePlayer>();
			if (component != null)
			{
				_localVps = DissonanceComms.GetSingleton()?.FindPlayer(component.PlayerId);
			}
			_localController = NetworkClient.localPlayer.GetComponent<PlayerController>();
			PlayMicOnSFX();
			anim.SetBool("IsActivated", value: true);
			buffArea.DOScale(Vector3.one * buffRange, 0.3f).SetEase(Ease.OutQuad);
			StartCoroutine(DurationRoutine());
			if (base.isServer)
			{
				StartCoroutine(DestroyItemCoroutine());
			}
		}
	}

	private IEnumerator DurationRoutine()
	{
		float currentDuration = duration;
		while (currentDuration > 0f)
		{
			currentDuration -= Time.deltaTime;
			float y = Mathf.Clamp(currentDuration / duration, 0.01f, 1f);
			durationIndicator.localScale = new Vector3(1f, y, 1f);
			yield return null;
		}
	}

	private IEnumerator DestroyItemCoroutine()
	{
		yield return new WaitForSeconds(duration);
		_hasEnded = true;
		RpcOnDurationEnd();
		yield return new WaitForSeconds(1.5f);
		DestroyItem();
	}

	[ClientRpc]
	private void RpcOnDurationEnd()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Microphone::RpcOnDurationEnd()", 457111312, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void Update()
	{
		if (!_isActive)
		{
			return;
		}
		if ((bool)base.NetworkHolder)
		{
			CmdSendMicAmplitude(_localController, _localVps.Amplitude);
		}
		if (base.isServer)
		{
			if ((bool)base.NetworkHolder)
			{
				SetAmplitude();
			}
			SetCharge();
		}
	}

	[Command(requiresAuthority = false)]
	private void CmdSendMicAmplitude(PlayerController identity, float amplitude)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(identity);
		writer.WriteFloat(amplitude);
		SendCommandInternal("System.Void Microphone::CmdSendMicAmplitude(PlayerController,System.Single)", 704293981, writer, 0, requiresAuthority: false);
		NetworkWriterPool.Return(writer);
	}

	private void SetAmplitude()
	{
		float num = 0f;
		foreach (KeyValuePair<PlayerController, float> amplitude in _amplitudes)
		{
			PlayerController key = amplitude.Key;
			float value = amplitude.Value;
			if (!((base.transform.position - key.head.transform.position).sqrMagnitude > inputRange * inputRange))
			{
				num += value;
			}
		}
		num *= amplitudeMultiplier;
		num *= NetworkSingleton<UpgradeManager>.Instance.GetUpgradeData(_holderProfile.steamId, PlayerUpgradeType.Stakeholder);
		NetworkcurrentAmplitude = Mathf.Lerp(currentAmplitude, num, Time.deltaTime * amplitudeLerp);
	}

	private void SetCharge()
	{
		if (currentAmplitude >= minThreshold)
		{
			float num = Mathf.InverseLerp(minThreshold, maxThreshold, currentAmplitude);
			NetworkcurrentCharge = Mathf.Clamp01(currentCharge + chargeIncrease * num * Time.deltaTime);
		}
		else
		{
			NetworkcurrentCharge = Mathf.Clamp01(currentCharge - chargeDecrease * Time.deltaTime);
		}
		foreach (PlayerBuff buff in _buffs)
		{
			BuffArea area = new BuffArea
			{
				Source = base.transform,
				Range = buffRange,
				Amount = currentCharge,
				IsActive = true
			};
			buff.SetBuffArea(PlayerBuffType.InspiringMelody, this, area);
		}
	}

	[ClientRpc]
	private void RpcSetArea(bool isEnabled)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isEnabled);
		SendRPCInternal("System.Void Microphone::RpcSetArea(System.Boolean)", -653807674, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void PlayMicOnSFX()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void Microphone::PlayMicOnSFX()", -2037547392, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void MicTapSfx()
	{
		SFXManager.SFXOneShot(micTapSfx, base.transform.position);
	}

	private void MicClickSfx()
	{
		SFXManager.SFXOneShot(micClickSfx, base.transform.position);
	}

	private void DisableMic()
	{
		micTransform.gameObject.SetActive(value: false);
	}

	public Microphone()
	{
		_Mirror_SyncVarHookDelegate_currentAmplitude = OnAmplitudeChanged;
		_Mirror_SyncVarHookDelegate_currentCharge = OnChargeChanged;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcOnDropped()
	{
		anim.Play("Default", 0, 0f);
		anim.Update(0f);
	}

	protected static void InvokeUserCode_RpcOnDropped(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnDropped called on server.");
		}
		else
		{
			((Microphone)obj).UserCode_RpcOnDropped();
		}
	}

	protected void UserCode_RpcOnDurationEnd()
	{
		buffArea.DOScale(Vector3.zero, 0.3f).SetEase(Ease.OutQuad);
		if ((bool)base.NetworkHolder)
		{
			anim.SetTrigger("Drop");
		}
	}

	protected static void InvokeUserCode_RpcOnDurationEnd(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnDurationEnd called on server.");
		}
		else
		{
			((Microphone)obj).UserCode_RpcOnDurationEnd();
		}
	}

	protected void UserCode_CmdSendMicAmplitude__PlayerController__Single(PlayerController identity, float amplitude)
	{
		_amplitudes[identity] = amplitude;
	}

	protected static void InvokeUserCode_CmdSendMicAmplitude__PlayerController__Single(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdSendMicAmplitude called on client.");
		}
		else
		{
			((Microphone)obj).UserCode_CmdSendMicAmplitude__PlayerController__Single(reader.ReadNetworkBehaviour<PlayerController>(), reader.ReadFloat());
		}
	}

	protected void UserCode_RpcSetArea__Boolean(bool isEnabled)
	{
		buffArea.DOScale(isEnabled ? (Vector3.one * buffRange) : Vector3.zero, 0.3f).SetEase(Ease.OutQuad);
	}

	protected static void InvokeUserCode_RpcSetArea__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetArea called on server.");
		}
		else
		{
			((Microphone)obj).UserCode_RpcSetArea__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_PlayMicOnSFX()
	{
		SFXManager.SFXOneShot3DAttached(micOnSfx, base.gameObject);
	}

	protected static void InvokeUserCode_PlayMicOnSFX(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC PlayMicOnSFX called on server.");
		}
		else
		{
			((Microphone)obj).UserCode_PlayMicOnSFX();
		}
	}

	static Microphone()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(Microphone), "System.Void Microphone::CmdSendMicAmplitude(PlayerController,System.Single)", InvokeUserCode_CmdSendMicAmplitude__PlayerController__Single, requiresAuthority: false);
		RemoteProcedureCalls.RegisterRpc(typeof(Microphone), "System.Void Microphone::RpcOnDropped()", InvokeUserCode_RpcOnDropped);
		RemoteProcedureCalls.RegisterRpc(typeof(Microphone), "System.Void Microphone::RpcOnDurationEnd()", InvokeUserCode_RpcOnDurationEnd);
		RemoteProcedureCalls.RegisterRpc(typeof(Microphone), "System.Void Microphone::RpcSetArea(System.Boolean)", InvokeUserCode_RpcSetArea__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(Microphone), "System.Void Microphone::PlayMicOnSFX()", InvokeUserCode_PlayMicOnSFX);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteFloat(currentAmplitude);
			writer.WriteFloat(currentCharge);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 2L) != 0L)
		{
			writer.WriteFloat(currentAmplitude);
		}
		if ((syncVarDirtyBits & 4L) != 0L)
		{
			writer.WriteFloat(currentCharge);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref currentAmplitude, _Mirror_SyncVarHookDelegate_currentAmplitude, reader.ReadFloat());
			GeneratedSyncVarDeserialize(ref currentCharge, _Mirror_SyncVarHookDelegate_currentCharge, reader.ReadFloat());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 2L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref currentAmplitude, _Mirror_SyncVarHookDelegate_currentAmplitude, reader.ReadFloat());
		}
		if ((num & 4L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref currentCharge, _Mirror_SyncVarHookDelegate_currentCharge, reader.ReadFloat());
		}
	}
}

}
