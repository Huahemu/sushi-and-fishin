using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using DistantLands.Cozy;
using DistantLands.Cozy.Data;
using Kamgam.SettingsGenerator;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Ocean.Creatures;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Core.Settings;
using PaperPixelGames.Core.Streaming;
using PaperPixelGames.Core.UI;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CozyWeatherManager : NetworkBehaviour
{
	private readonly NetworkVariable<float> _synchronizedBloomIntensity = new NetworkVariable<float>(0f);

	[SerializeField]
	private CozyWeather _cozyWeather;

	[SerializeField]
	private List<WeatherData> weatherDatas;

	[Header("Bloom By Time (0..1)")]
	[SerializeField]
	[Range(0f, 24f)]
	private float _rampUpStartHour = 19f;

	[SerializeField]
	[Range(0f, 24f)]
	private float _rampUpEndHour = 22f;

	[SerializeField]
	[Range(0f, 24f)]
	private float _flatStartHour = 22f;

	[SerializeField]
	[Range(0f, 24f)]
	private float _flatEndHour = 4f;

	[SerializeField]
	[Range(0f, 24f)]
	private float _rampDownStartHour = 4f;

	[SerializeField]
	[Range(0f, 24f)]
	private float _rampDownEndHour = 7f;

	[SerializeField]
	[Range(0f, 1f)]
	private float _dayIntensity;

	[SerializeField]
	[Range(0f, 1f)]
	private float _nightIntensity = 1f;

	[SerializeField]
	[Range(0f, 5f)]
	private float _bloomTweenDuration = 0.6f;

	[SerializeField]
	private Ease _bloomTweenEase = Ease.InOutSine;

	[Header("Underwater Volumes By Time")]
	[Tooltip("Underwater post-processing used outside the night time range.")]
	[SerializeField]
	private Volume _underwaterVolume;

	[Tooltip("Underwater post-processing blended in during the night time range.")]
	[SerializeField]
	private Volume _underwaterNightVolume;

	[SerializeField]
	[Range(0f, 24f)]
	private float _underwaterNightRampUpStartHour = 19f;

	[SerializeField]
	[Range(0f, 24f)]
	private float _underwaterNightRampUpEndHour = 22f;

	[SerializeField]
	[Range(0f, 24f)]
	private float _underwaterNightRampDownStartHour = 4f;

	[SerializeField]
	[Range(0f, 24f)]
	private float _underwaterNightRampDownEndHour = 7f;

	private TimeUI _timeUI;

	private Tweener _bloomTween;

	private int _lastDispatchedDay = -1;

	private int _lastDispatchedMinuteOfDay = -1;

	private int _streamingSuspendDepth;

	private readonly Dictionary<Behaviour, bool> _cozyBehaviourEnabledStatesBeforeSuspend = new Dictionary<Behaviour, bool>();

	private readonly List<Behaviour> _cozySuspendBuffer = new List<Behaviour>();

	private bool _bound;

	private int _boundSceneHandle;

	private int _boundCozyId;

	public MeridiemTime CurrentTime => _cozyWeather.timeModule.currentTime;

	public int CurrentDay
	{
		get
		{
			return _cozyWeather.timeModule.currentDay;
		}
		set
		{
			_cozyWeather.timeModule.currentDay = value;
		}
	}

	public float CurrentHourFloat
	{
		get
		{
			if (_cozyWeather == null)
			{
				return 12f;
			}
			int hours = _cozyWeather.timeModule.currentTime.hours;
			int minutes = _cozyWeather.timeModule.currentTime.minutes;
			return (float)hours + (float)minutes / 60f;
		}
	}

	private bool IsStreamingSuspended => _streamingSuspendDepth > 0;

	public event Action<float, float, Ease> BloomIntensityUpdated;

	public float GetCurrentBloomIntensityTarget()
	{
		if (_cozyWeather == null)
		{
			return _dayIntensity;
		}
		float num = _cozyWeather.timeModule.currentTime.hours;
		num += (float)_cozyWeather.timeModule.currentTime.minutes / 60f;
		return CalculateBloomIntensity(num);
	}

	public float GetSynchronizedBloomIntensityTarget()
	{
		if (!base.IsSpawned)
		{
			return GetCurrentBloomIntensityTarget();
		}
		return _synchronizedBloomIntensity.Value;
	}

	public Volume GetDominantUnderwaterVolume()
	{
		if (_underwaterNightVolume == null)
		{
			return _underwaterVolume;
		}
		if (_underwaterVolume == null)
		{
			return _underwaterNightVolume;
		}
		if (!(_underwaterNightVolume.weight > _underwaterVolume.weight))
		{
			return _underwaterVolume;
		}
		return _underwaterNightVolume;
	}

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();
		_timeUI = Singleton<GameUIManager>.Instance.TimeUI;
		if (base.IsServer)
		{
			_synchronizedBloomIntensity.Value = GetCurrentBloomIntensityTarget();
		}
		NetworkVariable<float> synchronizedBloomIntensity = _synchronizedBloomIntensity;
		synchronizedBloomIntensity.OnValueChanged = (NetworkVariable<float>.OnValueChangedDelegate)Delegate.Combine(synchronizedBloomIntensity.OnValueChanged, new NetworkVariable<float>.OnValueChangedDelegate(HandleSynchronizedBloomIntensityChanged));
		RuntimeSettings.UnderwaterDepthOfFieldChanged += HandleUnderwaterDepthOfFieldChanged;
		RuntimeSettings.UnderwaterLensDistortionChanged += HandleUnderwaterLensDistortionChanged;
		TryBind();
		ApplyBloomIntensity(_synchronizedBloomIntensity.Value, 0f, Ease.Linear);
		UpdateUnderwaterVolumeWeights();
		ApplyUnderwaterDepthOfFieldSetting(RuntimeSettings.UnderwaterDepthOfFieldEnabled);
		ApplyUnderwaterLensDistortionSetting(RuntimeSettings.UnderwaterLensDistortionEnabled);
		UpdateReflectionCubemapBlend();
		UtilityMethods.Log("[CozyWeatherManager] Cozy Weather Client side event connected");
	}

	public override void OnNetworkDespawn()
	{
		NetworkVariable<float> synchronizedBloomIntensity = _synchronizedBloomIntensity;
		synchronizedBloomIntensity.OnValueChanged = (NetworkVariable<float>.OnValueChangedDelegate)Delegate.Remove(synchronizedBloomIntensity.OnValueChanged, new NetworkVariable<float>.OnValueChangedDelegate(HandleSynchronizedBloomIntensityChanged));
		RuntimeSettings.UnderwaterDepthOfFieldChanged -= HandleUnderwaterDepthOfFieldChanged;
		RuntimeSettings.UnderwaterLensDistortionChanged -= HandleUnderwaterLensDistortionChanged;
		base.OnNetworkDespawn();
		Unbind();
	}

	public void ServerSync()
	{
		LinkModule linkModule = _cozyWeather.linkModule as LinkModule;
		if (linkModule == null)
		{
			UtilityMethods.Log("[CozyWeatherManager] ServerSync linkmodule is null!", LogType.Error);
			return;
		}
		linkModule.SyncCozyWeatherMaster();
		linkModule.SyncCozyAmbienceMaster();
		linkModule.SyncCozyTimeMaster();
	}

	public void SyncDay()
	{
		LinkModule linkModule = _cozyWeather.linkModule as LinkModule;
		if (linkModule == null)
		{
			UtilityMethods.Log("[CozyWeatherManager] ServerSync linkmodule is null!", LogType.Error);
		}
		else
		{
			linkModule.SyncCozyDay();
		}
	}

	private void TryBind()
	{
		if (base.IsSpawned && !(_cozyWeather == null))
		{
			int instanceID = _cozyWeather.GetInstanceID();
			int num = base.gameObject.scene.handle;
			if (!_bound || _boundCozyId != instanceID || _boundSceneHandle != num)
			{
				Unbind();
				CozyWeather.Events.onNewHour += HandleNewHour;
				CozyWeather.Events.onNewDay += HandleNewDay;
				CozyWeather.Events.onNewMinute += HandleNewMinute;
				_bound = true;
				_boundCozyId = instanceID;
				_boundSceneHandle = num;
				UtilityMethods.Log($"[CozyWeatherManager] Bound onNewHour | cozyId:{instanceID} scene:{num}");
			}
		}
	}

	private void Unbind()
	{
		if (_bound)
		{
			CozyWeather.Events.onNewHour -= HandleNewHour;
			CozyWeather.Events.onNewDay -= HandleNewDay;
			CozyWeather.Events.onNewMinute -= HandleNewMinute;
			_bound = false;
		}
	}

	private void HandleNewDay()
	{
		if (!IsStreamingSuspended && base.IsServer)
		{
			Singleton<SeaCreatureManager>.Instance.ResetAllLayerSpawnStates();
		}
	}

	private void HandleNewHour()
	{
		if (!IsStreamingSuspended)
		{
			TriggerBlackBoardTimeEvents();
			UpdateBloomByHour();
			UpdateReflectionCubemapBlend();
			if (NetworkManager.Singleton.IsHost)
			{
				UtilityMethods.Log("[CozyWeatherManager] Hourly saving on host side.");
				Singleton<SaveManager>.Instance.RequestHourlySave(host: true);
			}
			else
			{
				UtilityMethods.Log("[CozyWeatherManager] Hourly saving on client side.");
				Singleton<SaveManager>.Instance.RequestHourlySave(host: false);
			}
			if (base.IsServer && _cozyWeather.timeModule.currentDay == 1 && _cozyWeather.timeModule.currentTime.hours >= 21)
			{
				CompleteFirstDayTutorialForNight();
			}
		}
	}

	private static void CompleteFirstDayTutorialForNight()
	{
		QuestReferenceHub instance = QuestReferenceHub.Instance;
		if (!(instance == null))
		{
			instance.CompleteCurrentMainGroupIfContains(QuestId.ServeCucumberMaki);
			instance.CompleteCurrentMainGroupIfContains(QuestId.PickUpTheDirtyTray);
			instance.CompleteCurrentMainGroupIfContains(QuestId.ServeCustomersUntilNight);
		}
	}

	private void HandleNewMinute()
	{
		if (!IsStreamingSuspended)
		{
			TriggerBlackBoardTimeEvents();
			UpdateUnderwaterVolumeWeights();
			UpdateReflectionCubemapBlend();
		}
	}

	private void UpdateReflectionCubemapBlend()
	{
		if (Singleton<DressingSceneManager>.TryGetInstance(out var instance))
		{
			instance.UpdateReflectionCubemapBlend(CurrentHourFloat);
		}
	}

	private void UpdateBloomByHour()
	{
		if (base.IsServer)
		{
			float currentBloomIntensityTarget = GetCurrentBloomIntensityTarget();
			if (!Mathf.Approximately(_synchronizedBloomIntensity.Value, currentBloomIntensityTarget))
			{
				_synchronizedBloomIntensity.Value = currentBloomIntensityTarget;
			}
		}
	}

	private void HandleSynchronizedBloomIntensityChanged(float previousValue, float currentValue)
	{
		ApplyBloomIntensity(currentValue, _bloomTweenDuration, _bloomTweenEase);
	}

	private void ApplyBloomIntensity(float target, float duration, Ease ease)
	{
		BloomIntensityUpdated?.Invoke(target, duration, ease);
		if (SettingsVolume.Instance == null || SettingsVolume.Instance.Volume == null)
		{
			return;
		}
		VolumeProfile profile = SettingsVolume.Instance.Volume.profile;
		if (profile == null || !profile.TryGet<Bloom>(out var bloom))
		{
			return;
		}
		_bloomTween?.Kill();
		if (duration <= 0f)
		{
			bloom.intensity.value = target;
			return;
		}
		_bloomTween = DOTween.To(() => bloom.intensity.value, (float x) =>
		{
			bloom.intensity.value = x;
		}, target, duration).SetEase(ease).SetUpdate(isIndependentUpdate: true);
	}

	private void UpdateUnderwaterVolumeWeights()
	{
		if (!(_cozyWeather == null))
		{
			float num = CalculateUnderwaterNightWeight(CurrentHourFloat);
			if (_underwaterVolume != null)
			{
				_underwaterVolume.weight = 1f - num;
			}
			if (_underwaterNightVolume != null)
			{
				_underwaterNightVolume.weight = num;
			}
		}
	}

	private void HandleUnderwaterDepthOfFieldChanged(bool enabled)
	{
		ApplyUnderwaterDepthOfFieldSetting(enabled);
	}

	private void ApplyUnderwaterDepthOfFieldSetting(bool enabled)
	{
		SetDepthOfFieldActive(_underwaterVolume, enabled);
		SetDepthOfFieldActive(_underwaterNightVolume, enabled);
	}

	private static void SetDepthOfFieldActive(Volume volume, bool enabled)
	{
		if (!(volume == null) && !(volume.profile == null) && volume.profile.TryGet<DepthOfField>(out var component))
		{
			component.active = enabled;
		}
	}

	private void HandleUnderwaterLensDistortionChanged(bool enabled)
	{
		ApplyUnderwaterLensDistortionSetting(enabled);
	}

	private void ApplyUnderwaterLensDistortionSetting(bool enabled)
	{
		SetPaniniProjectionActive(_underwaterVolume, enabled);
		SetPaniniProjectionActive(_underwaterNightVolume, enabled);
	}

	private static void SetPaniniProjectionActive(Volume volume, bool enabled)
	{
		if (!(volume == null) && !(volume.profile == null) && volume.profile.TryGet<PaniniProjection>(out var component))
		{
			component.active = enabled;
		}
	}

	private float CalculateUnderwaterNightWeight(float hour)
	{
		hour = Mathf.Repeat(hour, 24f);
		if (IsInRange(hour, _underwaterNightRampUpStartHour, _underwaterNightRampUpEndHour))
		{
			float t = RangeT(hour, _underwaterNightRampUpStartHour, _underwaterNightRampUpEndHour);
			return Mathf.SmoothStep(0f, 1f, t);
		}
		if (IsInRange(hour, _underwaterNightRampUpEndHour, _underwaterNightRampDownStartHour))
		{
			return 1f;
		}
		if (IsInRange(hour, _underwaterNightRampDownStartHour, _underwaterNightRampDownEndHour))
		{
			float t2 = RangeT(hour, _underwaterNightRampDownStartHour, _underwaterNightRampDownEndHour);
			return Mathf.SmoothStep(1f, 0f, t2);
		}
		return 0f;
	}

	private float CalculateBloomIntensity(float hour)
	{
		hour = Mathf.Repeat(hour, 24f);
		if (IsInRange(hour, _rampUpStartHour, _rampUpEndHour))
		{
			float t = RangeT(hour, _rampUpStartHour, _rampUpEndHour);
			return Mathf.SmoothStep(_dayIntensity, _nightIntensity, t);
		}
		if (IsInRange(hour, _flatStartHour, _flatEndHour))
		{
			return _nightIntensity;
		}
		if (IsInRange(hour, _rampDownStartHour, _rampDownEndHour))
		{
			float t2 = RangeT(hour, _rampDownStartHour, _rampDownEndHour);
			return Mathf.SmoothStep(_nightIntensity, _dayIntensity, t2);
		}
		return _dayIntensity;
	}

	private static bool IsInRange(float hour, float start, float end)
	{
		start = Mathf.Repeat(start, 24f);
		end = Mathf.Repeat(end, 24f);
		if (Mathf.Approximately(start, end))
		{
			return true;
		}
		if (start < end)
		{
			if (hour >= start)
			{
				return hour < end;
			}
			return false;
		}
		if (!(hour >= start))
		{
			return hour < end;
		}
		return true;
	}

	private static float RangeT(float hour, float start, float end)
	{
		start = Mathf.Repeat(start, 24f);
		end = Mathf.Repeat(end, 24f);
		hour = Mathf.Repeat(hour, 24f);
		float num = ((start < end) ? (end - start) : (24f - start + end));
		if (num <= 0.0001f)
		{
			return 0f;
		}
		return Mathf.Clamp01(((hour >= start) ? (hour - start) : (24f - start + hour)) / num);
	}

	public void TriggerBlackBoardTimeEvents(bool force = false)
	{
		int currentDay = _cozyWeather.timeModule.currentDay;
		int currentHour = _cozyWeather.events.currentHour;
		int num = _cozyWeather.events.currentMinute;
		if (num >= 60)
		{
			num %= 60;
		}
		currentHour = Mathf.Clamp(currentHour, 0, 24);
		num = Mathf.Clamp(num, 0, 59);
		int num2 = ((currentHour == 24) ? 1440 : (currentHour * 60 + num));
		if (force || _lastDispatchedDay != currentDay || _lastDispatchedMinuteOfDay != num2)
		{
			_lastDispatchedDay = currentDay;
			_lastDispatchedMinuteOfDay = num2;
			if (Singleton<ResidentManager>.Instance != null)
			{
				Singleton<ResidentManager>.Instance.SetScheduleTime(currentHour, num);
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void ServerPauseTimeRpc()
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default;
			RpcParams rpcParams = default;
			FastBufferWriter bufferWriter = __beginSendRpc(1980002110u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 1980002110u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			_cozyWeather.timeModule.perennialProfile.pauseTime = true;
		}
	}

	[Rpc(SendTo.Server)]
	public void ServerResumeTimeRpc()
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default;
			RpcParams rpcParams = default;
			FastBufferWriter bufferWriter = __beginSendRpc(1431069553u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 1431069553u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			_cozyWeather.timeModule.perennialProfile.pauseTime = false;
		}
	}

	public void ClientPauseTime()
	{
		_cozyWeather.timeModule.perennialProfile.pauseTime = true;
	}

	public void ClientResumeTime()
	{
		_cozyWeather.timeModule.perennialProfile.pauseTime = false;
	}

	public void ClientSetTime(int hour, int minute)
	{
		_cozyWeather.timeModule.currentTime = new MeridiemTime(hour, minute);
		UpdateUnderwaterVolumeWeights();
	}

	public void BeginStreamingSuspend()
	{
		_streamingSuspendDepth++;
		if (_streamingSuspendDepth > 1)
		{
			return;
		}
		if (_cozyWeather == null)
		{
			UtilityMethods.Log("[CozyWeatherManager] Streaming suspend skipped because CozyWeather reference is missing.", LogType.Warning);
			return;
		}
		CacheCozyBehavioursForSuspend();
		for (int i = 0; i < _cozySuspendBuffer.Count; i++)
		{
			Behaviour behaviour = _cozySuspendBuffer[i];
			if (!(behaviour == null))
			{
				_cozyBehaviourEnabledStatesBeforeSuspend[behaviour] = behaviour.enabled;
				if (behaviour.enabled)
				{
					behaviour.enabled = false;
				}
			}
		}
	}

	public void EndStreamingSuspend()
	{
		if (_streamingSuspendDepth <= 0)
		{
			UtilityMethods.Log("[CozyWeatherManager] EndStreamingSuspend called without a matching begin.", LogType.Warning);
			return;
		}
		_streamingSuspendDepth--;
		if (_streamingSuspendDepth > 0)
		{
			return;
		}
		bool flag = _cozyWeather != null;
		if (!flag)
		{
			UtilityMethods.Log("[CozyWeatherManager] Streaming resume final sync skipped because CozyWeather reference is missing.", LogType.Warning);
		}
		foreach (KeyValuePair<Behaviour, bool> item in _cozyBehaviourEnabledStatesBeforeSuspend)
		{
			if (item.Key != null)
			{
				item.Key.enabled = item.Value;
			}
		}
		_cozyBehaviourEnabledStatesBeforeSuspend.Clear();
		_cozySuspendBuffer.Clear();
		if (flag)
		{
			UpdateUnderwaterVolumeWeights();
			UpdateBloomByHour();
			TriggerBlackBoardTimeEvents(force: true);
		}
	}

	private void CacheCozyBehavioursForSuspend()
	{
		_cozySuspendBuffer.Clear();
		AddCozyBehaviourForSuspend(_cozyWeather);
		AddCozyMemberBehavioursForSuspend("modules");
		AddCozyMemberBehavioursForSuspend("weatherModule");
		AddCozyMemberBehavioursForSuspend("timeModule");
		AddCozyMemberBehavioursForSuspend("atmosphereModule");
		AddCozyMemberBehavioursForSuspend("ambienceModule");
		AddCozyMemberBehavioursForSuspend("climateModule");
		AddCozyMemberBehavioursForSuspend("windModule");
		AddCozyMemberBehavioursForSuspend("linkModule");
	}

	private void AddCozyMemberBehavioursForSuspend(string memberName)
	{
		if (!(_cozyWeather == null) && !string.IsNullOrWhiteSpace(memberName))
		{
			Type type = _cozyWeather.GetType();
			FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			object obj = ((field != null) ? field.GetValue(_cozyWeather) : null);
			if (obj == null)
			{
				PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				obj = ((property != null) ? property.GetValue(_cozyWeather) : null);
			}
			AddCozyValueBehavioursForSuspend(obj);
		}
	}

	private void AddCozyValueBehavioursForSuspend(object value)
	{
		if (value == null)
		{
			return;
		}
		if (value is Behaviour behaviour)
		{
			AddCozyBehaviourForSuspend(behaviour);
		}
		else
		{
			if (!(value is IEnumerable enumerable) || value is string)
			{
				return;
			}
			foreach (object item in enumerable)
			{
				AddCozyValueBehavioursForSuspend(item);
			}
		}
	}

	private void AddCozyBehaviourForSuspend(Behaviour behaviour)
	{
		if (!(behaviour == null) && !(behaviour is LinkModule) && !_cozySuspendBuffer.Contains(behaviour))
		{
			_cozySuspendBuffer.Add(behaviour);
		}
	}

	[Rpc(SendTo.Server)]
	public void ServerRequestChangeTimeRpc(int hour, int minute, bool checkFishSpawn)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default;
			RpcParams rpcParams = default;
			FastBufferWriter bufferWriter = __beginSendRpc(2116501819u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			BytePacker.WriteValueBitPacked(bufferWriter, hour);
			BytePacker.WriteValueBitPacked(bufferWriter, minute);
			bufferWriter.WriteValueSafe(in checkFishSpawn, default(FastBufferWriter.ForPrimitives));
			__endSendRpc(ref bufferWriter, 2116501819u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			_cozyWeather.timeModule.currentTime = new MeridiemTime(hour, minute);
			UpdateUnderwaterVolumeWeights();
			UtilityMethods.Log($"[CozyWeatherManager/ServerRequestChangeTimeRpc] hour: {hour}");
			if (checkFishSpawn && hour > 7)
			{
				Singleton<SeaCreatureManager>.Instance.ResetAllLayerSpawnStates();
			}
		}
	}

	private void Update()
	{
		TryBind();
		if (!(_timeUI == null))
		{
			_timeUI.Time = $"{_cozyWeather.timeModule.currentTime.hours:D2}:{_cozyWeather.timeModule.currentTime.minutes:D2}";
		}
	}

	[Rpc(SendTo.Server)]
	public void ServerRequestChangeWeatherRpc(WeatherType weatherType)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default;
			RpcParams rpcParams = default;
			FastBufferWriter bufferWriter = __beginSendRpc(2290416063u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in weatherType, default(FastBufferWriter.ForEnums));
			__endSendRpc(ref bufferWriter, 2290416063u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			WeatherProfile weatherProfileByType = GetWeatherProfileByType(weatherType);
			if (!(weatherProfileByType == null))
			{
				_cozyWeather.weatherModule.ecosystem.SetWeather(weatherProfileByType);
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void ServerRequestChangeDayRpc(int day)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default;
			RpcParams rpcParams = default;
			FastBufferWriter bufferWriter = __beginSendRpc(329848556u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			BytePacker.WriteValueBitPacked(bufferWriter, day);
			__endSendRpc(ref bufferWriter, 329848556u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			_cozyWeather.timeModule.currentDay = day;
		}
	}

	[Rpc(SendTo.Server)]
	public void ServerRequestAddDayRpc(int day)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default;
			RpcParams rpcParams = default;
			FastBufferWriter bufferWriter = __beginSendRpc(4258253777u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			BytePacker.WriteValueBitPacked(bufferWriter, day);
			__endSendRpc(ref bufferWriter, 4258253777u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			_cozyWeather.timeModule.currentDay += day;
		}
	}

	[Rpc(SendTo.Server)]
	public void ServerRequestChangeYearRpc(int year)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default;
			RpcParams rpcParams = default;
			FastBufferWriter bufferWriter = __beginSendRpc(3947099665u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			BytePacker.WriteValueBitPacked(bufferWriter, year);
			__endSendRpc(ref bufferWriter, 3947099665u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			_cozyWeather.timeModule.currentYear = year;
		}
	}

	[Rpc(SendTo.Server)]
	public void ServerRequestSetPauseTimeRpc(bool pause)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default;
			RpcParams rpcParams = default;
			FastBufferWriter bufferWriter = __beginSendRpc(1744012126u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in pause, default(FastBufferWriter.ForPrimitives));
			__endSendRpc(ref bufferWriter, 1744012126u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			_cozyWeather.timeModule.perennialProfile.pauseTime = pause;
		}
	}

	private WeatherProfile GetWeatherProfileByType(WeatherType weatherType)
	{
		foreach (WeatherData weatherData in weatherDatas)
		{
			if (weatherData.WeatherType == weatherType)
			{
				return weatherData.WeatherProfile;
			}
		}
		return null;
	}

	protected override void __initializeVariables()
	{
		if (_synchronizedBloomIntensity == null)
		{
			throw new Exception("CozyWeatherManager._synchronizedBloomIntensity cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		_synchronizedBloomIntensity.Initialize(this);
		__nameNetworkVariable(_synchronizedBloomIntensity, "_synchronizedBloomIntensity");
		NetworkVariableFields.Add(_synchronizedBloomIntensity);
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		__registerRpc(1980002110u, __rpc_handler_1980002110, "ServerPauseTimeRpc", RpcInvokePermission.Everyone);
		__registerRpc(1431069553u, __rpc_handler_1431069553, "ServerResumeTimeRpc", RpcInvokePermission.Everyone);
		__registerRpc(2116501819u, __rpc_handler_2116501819, "ServerRequestChangeTimeRpc", RpcInvokePermission.Everyone);
		__registerRpc(2290416063u, __rpc_handler_2290416063, "ServerRequestChangeWeatherRpc", RpcInvokePermission.Everyone);
		__registerRpc(329848556u, __rpc_handler_329848556, "ServerRequestChangeDayRpc", RpcInvokePermission.Everyone);
		__registerRpc(4258253777u, __rpc_handler_4258253777, "ServerRequestAddDayRpc", RpcInvokePermission.Everyone);
		__registerRpc(3947099665u, __rpc_handler_3947099665, "ServerRequestChangeYearRpc", RpcInvokePermission.Everyone);
		__registerRpc(1744012126u, __rpc_handler_1744012126, "ServerRequestSetPauseTimeRpc", RpcInvokePermission.Everyone);
		base.__initializeRpcs();
	}

	private static void __rpc_handler_1980002110(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			((CozyWeatherManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((CozyWeatherManager)target).ServerPauseTimeRpc();
			((CozyWeatherManager)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1431069553(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			((CozyWeatherManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((CozyWeatherManager)target).ServerResumeTimeRpc();
			((CozyWeatherManager)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2116501819(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out int value);
			ByteUnpacker.ReadValueBitPacked(reader, out int value2);
			reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
			((CozyWeatherManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((CozyWeatherManager)target).ServerRequestChangeTimeRpc(value, value2, value3);
			((CozyWeatherManager)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2290416063(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out WeatherType value, default(FastBufferWriter.ForEnums));
			((CozyWeatherManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((CozyWeatherManager)target).ServerRequestChangeWeatherRpc(value);
			((CozyWeatherManager)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_329848556(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out int value);
			((CozyWeatherManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((CozyWeatherManager)target).ServerRequestChangeDayRpc(value);
			((CozyWeatherManager)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_4258253777(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out int value);
			((CozyWeatherManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((CozyWeatherManager)target).ServerRequestAddDayRpc(value);
			((CozyWeatherManager)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_3947099665(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out int value);
			((CozyWeatherManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((CozyWeatherManager)target).ServerRequestChangeYearRpc(value);
			((CozyWeatherManager)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1744012126(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			((CozyWeatherManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((CozyWeatherManager)target).ServerRequestSetPauseTimeRpc(value);
			((CozyWeatherManager)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	protected override string __getTypeName()
	{
		return "CozyWeatherManager";
	}
}
