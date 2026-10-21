using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using PaperPixelGames.Framework.Utility;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace PaperPixelGames.Networking
{
	[DisallowMultipleComponent]
	[RequireComponent(typeof(SteamLobbyService))]
	public sealed class LobbyHostRecommendationService : MonoBehaviour
	{
		[Serializable]
		private sealed class HostProfileData
		{
			public int Version;

			public string PingLocation;

			public int StableFps;

			public int MedianFps;

			public int CpuCoreCount;

			public int CpuFrequencyMhz;

			public int SystemMemoryMb;

			public int GraphicsMemoryMb;
		}

		[Serializable]
		private sealed class RecommendationMessageData
		{
			public int Revision;

			public ulong RecommendedHostSteamId;

			public int WorstPingMs;

			public int AveragePingMs;

			public int StableFps;
		}

		private sealed class CandidateProfile
		{
			public Friend Member;

			public HostProfileData Data;

			public NetPingLocation PingLocation;
		}

		private sealed class CandidateScore
		{
			public CandidateProfile Profile;

			public int WorstPingMs;

			public int AveragePingMs;

			public float PerformanceScore;
		}

		[Header("Sampling")]
		[SerializeField]
		[Min(0f)]
		private float _warmupSeconds = 1f;

		[SerializeField]
		[Min(1f)]
		private float _sampleSeconds = 5f;

		[SerializeField]
		[Min(1f)]
		private float _pingDataTimeoutSeconds = 5f;

		[SerializeField]
		[Min(30f)]
		private int _minimumFrameSamples = 60;

		[Header("Selection")]
		[SerializeField]
		[Min(0f)]
		private float _minimumStableFps = 30f;

		[SerializeField]
		[Min(0f)]
		private int _networkShortlistToleranceMs = 15;

		[SerializeField]
		[Min(0f)]
		private float _evaluationDebounceSeconds = 3f;

		private const int ProfileVersion = 1;

		private const string ProfileMemberDataKey = "host_profile_v1";

		private const string RecommendationProtocol = "host_recommendation_v1:";

		private SteamLobbyService _steamLobbyService;

		private Coroutine _profileSamplingCoroutine;

		private Coroutine _evaluationCoroutine;

		private ulong _activeLobbyId;

		private int _activationVersion;

		private int _membershipRevision;

		private int _lastAnnouncedRevision = -1;

		private string _lastReceivedRecommendationKey = string.Empty;

		private bool _initialized;

		public void Initialize(SteamLobbyService steamLobbyService)
		{
			if (_initialized && _steamLobbyService == steamLobbyService)
			{
				return;
			}
			Shutdown();
			_steamLobbyService = steamLobbyService;
			if (!(_steamLobbyService == null))
			{
				_steamLobbyService.LobbyCreated += OnLobbyCreated;
				_steamLobbyService.LobbyEntered += OnLobbyEntered;
				_steamLobbyService.LobbyMemberJoined += OnLobbyMemberJoined;
				_steamLobbyService.LobbyMemberLeft += OnLobbyMemberLeft;
				_steamLobbyService.LobbyDataChanged += OnLobbyDataChanged;
				_steamLobbyService.LobbyInternalMessageReceived += OnLobbyInternalMessageReceived;
				_initialized = true;
				if ((ulong)_steamLobbyService.CurrentLobby.Id != 0L)
				{
					ActivateLobby(_steamLobbyService.CurrentLobby);
				}
			}
		}

		public void Shutdown()
		{
			StopRuntimeCoroutines();
			if (_steamLobbyService != null)
			{
				_steamLobbyService.LobbyCreated -= OnLobbyCreated;
				_steamLobbyService.LobbyEntered -= OnLobbyEntered;
				_steamLobbyService.LobbyMemberJoined -= OnLobbyMemberJoined;
				_steamLobbyService.LobbyMemberLeft -= OnLobbyMemberLeft;
				_steamLobbyService.LobbyDataChanged -= OnLobbyDataChanged;
				_steamLobbyService.LobbyInternalMessageReceived -= OnLobbyInternalMessageReceived;
			}
			_steamLobbyService = null;
			_initialized = false;
			ResetLobbyState();
		}

		private void OnDestroy()
		{
			Shutdown();
		}

		private void OnLobbyCreated(Lobby lobby)
		{
			ActivateLobby(lobby);
		}

		private void OnLobbyEntered(Lobby lobby)
		{
			ActivateLobby(lobby);
		}

		private void ActivateLobby(Lobby lobby)
		{
			if ((ulong)lobby.Id != 0L && _activeLobbyId != lobby.Id.Value)
			{
				StopRuntimeCoroutines();
				ResetLobbyState();
				_activeLobbyId = lobby.Id.Value;
				_activationVersion++;
				int activationVersion = _activationVersion;
				PrepareLocalProfileAsync(lobby, activationVersion);
				ScheduleEvaluation(lobby);
			}
		}

		private async void PrepareLocalProfileAsync(Lobby lobby, int activationVersion)
		{
			try
			{
				SteamNetworkingUtils.InitRelayNetworkAccess();
				await SteamNetworkingUtils.WaitForPingDataAsync(_pingDataTimeoutSeconds);
				if (!IsActiveWaitingLobby(lobby, activationVersion))
				{
					return;
				}
				NetPingLocation? localPingLocation = SteamNetworkingUtils.LocalPingLocation;
				if (!localPingLocation.HasValue)
				{
					UtilityMethods.Log("[HostRecommendation] Local Steam ping location is unavailable.", LogType.Warning);
					return;
				}
				if (_profileSamplingCoroutine != null)
				{
					StopCoroutine(_profileSamplingCoroutine);
				}
				_profileSamplingCoroutine = StartCoroutine(SampleAndPublishLocalProfile(lobby, activationVersion, localPingLocation.Value));
			}
			catch (Exception arg)
			{
				UtilityMethods.Log($"[HostRecommendation] Failed to prepare the local profile. Exception={arg}", LogType.Warning);
			}
		}

		private IEnumerator SampleAndPublishLocalProfile(Lobby lobby, int activationVersion, NetPingLocation pingLocation)
		{
			if (_warmupSeconds > 0f)
			{
				yield return new WaitForSecondsRealtime(_warmupSeconds);
			}
			List<float> fpsSamples = new List<float>(Mathf.Max(_minimumFrameSamples, 300));
			float focusedSampleTime = 0f;
			float wallClockDeadline = Time.realtimeSinceStartup + _sampleSeconds + 15f;
			while (focusedSampleTime < _sampleSeconds && Time.realtimeSinceStartup < wallClockDeadline)
			{
				if (!IsActiveWaitingLobby(lobby, activationVersion))
				{
					_profileSamplingCoroutine = null;
					yield break;
				}
				float unscaledDeltaTime = Time.unscaledDeltaTime;
				if (Application.isFocused && unscaledDeltaTime > 0.0001f && unscaledDeltaTime < 0.25f)
				{
					fpsSamples.Add(1f / unscaledDeltaTime);
					focusedSampleTime += unscaledDeltaTime;
				}
				yield return null;
			}
			_profileSamplingCoroutine = null;
			if (fpsSamples.Count < _minimumFrameSamples || !IsActiveWaitingLobby(lobby, activationVersion))
			{
				UtilityMethods.Log($"[HostRecommendation] Local FPS sample was not published. Samples={fpsSamples.Count}", LogType.Warning);
				yield break;
			}
			fpsSamples.Sort();
			float percentile = GetPercentile(fpsSamples, 0.1f);
			float percentile2 = GetPercentile(fpsSamples, 0.5f);
			HostProfileData hostProfileData = new HostProfileData
			{
				Version = 1,
				PingLocation = pingLocation.ToString(),
				StableFps = Mathf.RoundToInt(percentile),
				MedianFps = Mathf.RoundToInt(percentile2),
				CpuCoreCount = SystemInfo.processorCount,
				CpuFrequencyMhz = SystemInfo.processorFrequency,
				SystemMemoryMb = SystemInfo.systemMemorySize,
				GraphicsMemoryMb = SystemInfo.graphicsMemorySize
			};
			string value = JsonConvert.SerializeObject(hostProfileData);
			lobby.SetMemberData("host_profile_v1", value);
			UtilityMethods.Log("[HostRecommendation] Published local host profile" + $"\n   StableFps: {hostProfileData.StableFps}" + $"\n   MedianFps: {hostProfileData.MedianFps}" + $"\n   CpuCores: {hostProfileData.CpuCoreCount}" + $"\n   SystemMemoryMb: {hostProfileData.SystemMemoryMb}" + $"\n   GraphicsMemoryMb: {hostProfileData.GraphicsMemoryMb}");
			ScheduleEvaluation(lobby);
		}

		private void OnLobbyMemberJoined(Lobby lobby, Friend _)
		{
			if (IsActiveLobby(lobby))
			{
				_membershipRevision++;
				ScheduleEvaluation(lobby);
			}
		}

		private void OnLobbyMemberLeft(Lobby lobby, Friend _)
		{
			if (IsActiveLobby(lobby))
			{
				_membershipRevision++;
				ScheduleEvaluation(lobby);
			}
		}

		private void OnLobbyDataChanged(Lobby lobby)
		{
			if (IsActiveLobby(lobby))
			{
				ScheduleEvaluation(lobby);
			}
		}

		private void ScheduleEvaluation(Lobby lobby)
		{
			if (CanLocalUserEvaluate(lobby))
			{
				if (_evaluationCoroutine != null)
				{
					StopCoroutine(_evaluationCoroutine);
				}
				_evaluationCoroutine = StartCoroutine(EvaluateAfterDelay(lobby));
			}
		}

		private IEnumerator EvaluateAfterDelay(Lobby lobby)
		{
			if (_evaluationDebounceSeconds > 0f)
			{
				yield return new WaitForSecondsRealtime(_evaluationDebounceSeconds);
			}
			_evaluationCoroutine = null;
			if (CanLocalUserEvaluate(lobby))
			{
				TryEvaluateAndPublish(lobby);
			}
		}

		private void TryEvaluateAndPublish(Lobby lobby)
		{
			List<Friend> list;
			try
			{
				list = lobby.Members.OrderBy((Friend member) => member.Id.Value).ToList();
			}
			catch (Exception arg)
			{
				UtilityMethods.Log($"[HostRecommendation] Could not enumerate lobby members. Exception={arg}", LogType.Warning);
				return;
			}
			if (list.Count < 2)
			{
				return;
			}
			List<CandidateProfile> list2 = new List<CandidateProfile>(list.Count);
			foreach (Friend item in list)
			{
				if (!TryReadCandidateProfile(lobby, item, out var candidate))
				{
					return;
				}
				list2.Add(candidate);
			}
			List<CandidateScore> list3 = BuildCandidateScores(list2);
			if (list3.Count == 0)
			{
				return;
			}
			List<CandidateScore> list4 = list3.Where((CandidateScore score) => (float)score.Profile.Data.StableFps >= _minimumStableFps).ToList();
			if (list4.Count == 0)
			{
				list4 = list3;
			}
			int bestWorstPing = list4.Min((CandidateScore score) => score.WorstPingMs);
			List<CandidateScore> source = list4.Where((CandidateScore score) => score.WorstPingMs <= bestWorstPing + _networkShortlistToleranceMs).ToList();
			SteamId currentOwnerId = lobby.Owner.Id;
			CandidateScore candidateScore = (from score in source
				orderby score.PerformanceScore descending, score.AveragePingMs, (ulong)score.Profile.Member.Id == (ulong)currentOwnerId descending, score.Profile.Member.Id.Value
				select score).First();
			if (_lastAnnouncedRevision != _membershipRevision)
			{
				RecommendationMessageData recommendationMessageData = new RecommendationMessageData
				{
					Revision = _membershipRevision,
					RecommendedHostSteamId = candidateScore.Profile.Member.Id.Value,
					WorstPingMs = candidateScore.WorstPingMs,
					AveragePingMs = candidateScore.AveragePingMs,
					StableFps = candidateScore.Profile.Data.StableFps
				};
				string payload = "host_recommendation_v1:" + JsonConvert.SerializeObject(recommendationMessageData);
				if (!_steamLobbyService.SendLobbyInternalMessage(payload))
				{
					UtilityMethods.Log("[HostRecommendation] Failed to send the recommendation message.", LogType.Warning);
					return;
				}
				_lastAnnouncedRevision = _membershipRevision;
				AcceptRecommendation(lobby, recommendationMessageData);
				UtilityMethods.Log("[HostRecommendation] Best host selected" + $"\n   SteamId: {candidateScore.Profile.Member.Id}" + $"\n   WorstPingMs: {candidateScore.WorstPingMs}" + $"\n   AveragePingMs: {candidateScore.AveragePingMs}" + $"\n   StableFps: {candidateScore.Profile.Data.StableFps}" + $"\n   PerformanceScore: {candidateScore.PerformanceScore:0.0}");
			}
		}

		private List<CandidateScore> BuildCandidateScores(List<CandidateProfile> profiles)
		{
			List<CandidateScore> list = new List<CandidateScore>(profiles.Count);
			foreach (CandidateProfile profile in profiles)
			{
				int num = 0;
				int num2 = 0;
				int num3 = 0;
				bool flag = false;
				foreach (CandidateProfile profile2 in profiles)
				{
					if ((ulong)profile.Member.Id != (ulong)profile2.Member.Id)
					{
						int num4 = profile.PingLocation.EstimatePingTo(profile2.PingLocation);
						if (num4 < 0)
						{
							flag = true;
							break;
						}
						num = Mathf.Max(num, num4);
						num2 += num4;
						num3++;
					}
				}
				if (!flag && num3 != 0)
				{
					list.Add(new CandidateScore
					{
						Profile = profile,
						WorstPingMs = num,
						AveragePingMs = Mathf.RoundToInt((float)num2 / (float)num3),
						PerformanceScore = CalculatePerformanceScore(profile.Data)
					});
				}
			}
			return list;
		}

		private static float CalculatePerformanceScore(HostProfileData profile)
		{
			float num = (float)Mathf.Clamp(profile.StableFps, 0, 144) * 1.5f;
			float num2 = (float)Mathf.Clamp(profile.MedianFps, 0, 144) * 0.5f;
			float num3 = (float)Mathf.Clamp(profile.CpuCoreCount, 0, 16) * 3f;
			float num4 = (float)Mathf.Clamp(profile.CpuFrequencyMhz, 0, 5000) / 200f;
			float num5 = Mathf.Clamp((float)profile.SystemMemoryMb / 1024f, 0f, 32f) * 0.25f;
			float num6 = Mathf.Clamp((float)profile.GraphicsMemoryMb / 1024f, 0f, 16f) * 0.5f;
			return num + num2 + num3 + num4 + num5 + num6;
		}

		private static float GetPercentile(List<float> sortedValues, float percentile)
		{
			if (sortedValues == null || sortedValues.Count == 0)
			{
				return 0f;
			}
			int index = Mathf.Clamp(Mathf.RoundToInt((float)(sortedValues.Count - 1) * percentile), 0, sortedValues.Count - 1);
			return sortedValues[index];
		}

		private bool TryReadCandidateProfile(Lobby lobby, Friend member, out CandidateProfile candidate)
		{
			candidate = null;
			string memberData = lobby.GetMemberData(member, "host_profile_v1");
			if (string.IsNullOrWhiteSpace(memberData))
			{
				return false;
			}
			try
			{
				HostProfileData hostProfileData = JsonConvert.DeserializeObject<HostProfileData>(memberData);
				if (hostProfileData == null || hostProfileData.Version != 1 || hostProfileData.StableFps <= 0 || string.IsNullOrWhiteSpace(hostProfileData.PingLocation))
				{
					return false;
				}
				NetPingLocation? netPingLocation = NetPingLocation.TryParseFromString(hostProfileData.PingLocation);
				if (!netPingLocation.HasValue)
				{
					return false;
				}
				candidate = new CandidateProfile
				{
					Member = member,
					Data = hostProfileData,
					PingLocation = netPingLocation.Value
				};
				return true;
			}
			catch
			{
				return false;
			}
		}

		private void OnLobbyInternalMessageReceived(Lobby lobby, Friend sender, string payload)
		{
			if (!IsActiveLobby(lobby) || string.IsNullOrEmpty(payload) || !payload.StartsWith("host_recommendation_v1:", StringComparison.Ordinal))
			{
				return;
			}
			if ((ulong)sender.Id != (ulong)lobby.Owner.Id)
			{
				UtilityMethods.Log($"[HostRecommendation] Ignored recommendation from non-owner sender. Sender={sender.Id}", LogType.Warning);
				return;
			}
			try
			{
				RecommendationMessageData recommendationMessageData = JsonConvert.DeserializeObject<RecommendationMessageData>(payload.Substring("host_recommendation_v1:".Length));
				if (recommendationMessageData != null && recommendationMessageData.RecommendedHostSteamId != 0L)
				{
					AcceptRecommendation(lobby, recommendationMessageData);
				}
			}
			catch (Exception arg)
			{
				UtilityMethods.Log($"[HostRecommendation] Invalid recommendation payload. Exception={arg}", LogType.Warning);
			}
		}

		private void AcceptRecommendation(Lobby lobby, RecommendationMessageData result)
		{
			string text = $"{lobby.Id.Value}:{result.Revision}:{result.RecommendedHostSteamId}";
			if (!(_lastReceivedRecommendationKey == text))
			{
				_lastReceivedRecommendationKey = text;
				_steamLobbyService.NotifyLobbyHostRecommendationReceived(new LobbyHostRecommendation(lobby.Id.Value, result.RecommendedHostSteamId, result.WorstPingMs, result.AveragePingMs, result.StableFps));
			}
		}

		private bool CanLocalUserEvaluate(Lobby lobby)
		{
			if (IsActiveLobby(lobby) && SteamClient.IsValid && (ulong)lobby.Owner.Id == (ulong)SteamClient.SteamId && lobby.MemberCount >= 2)
			{
				return string.Equals(lobby.GetData("session_state"), "waiting", StringComparison.Ordinal);
			}
			return false;
		}

		private bool IsActiveWaitingLobby(Lobby lobby, int activationVersion)
		{
			if (activationVersion == _activationVersion && IsActiveLobby(lobby))
			{
				return string.Equals(lobby.GetData("session_state"), "waiting", StringComparison.Ordinal);
			}
			return false;
		}

		private bool IsActiveLobby(Lobby lobby)
		{
			if (_steamLobbyService != null && lobby.Id.Value != 0L && lobby.Id.Value == _activeLobbyId)
			{
				return _steamLobbyService.CurrentLobby.Id.Value == _activeLobbyId;
			}
			return false;
		}

		private void StopRuntimeCoroutines()
		{
			if (_profileSamplingCoroutine != null)
			{
				StopCoroutine(_profileSamplingCoroutine);
				_profileSamplingCoroutine = null;
			}
			if (_evaluationCoroutine != null)
			{
				StopCoroutine(_evaluationCoroutine);
				_evaluationCoroutine = null;
			}
		}

		private void ResetLobbyState()
		{
			_activeLobbyId = 0uL;
			_membershipRevision = 0;
			_lastAnnouncedRevision = -1;
			_lastReceivedRecommendationKey = string.Empty;
		}
	}
}
