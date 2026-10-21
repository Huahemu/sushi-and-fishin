using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PlayFab;
using PlayFab.AuthenticationModels;
using PlayFab.ClientModels;
using Steamworks;
using UnityEngine;

namespace PaperPixelGames.Core.Reporting
{
	public class PlayFabAuthService : MonoBehaviour
	{
		private const int AuthenticationTimeoutSeconds = 20;

		private const int TokenRefreshLeadSeconds = 120;

		private AuthTicket _authTicket;

		private Task<bool> _authenticationTask;

		private CancellationTokenSource _lifetimeCancellation;

		public bool IsAuthenticated { get; private set; }

		public bool IsAuthenticating { get; private set; }

		public bool IsInitialized { get; private set; }

		public PlayFabReportAuthState State { get; private set; }

		public string LastError { get; private set; }

		public string PlayFabId { get; private set; }

		public string EntityId { get; private set; }

		public string EntityType { get; private set; }

		public string EntityToken { get; private set; }

		public DateTime? EntityTokenExpirationUtc { get; private set; }

		public bool HasValidEntityToken
		{
			get
			{
				if (!IsAuthenticated || string.IsNullOrWhiteSpace(EntityToken))
				{
					return false;
				}
				if (EntityTokenExpirationUtc.HasValue)
				{
					return EntityTokenExpirationUtc.Value > DateTime.UtcNow.AddSeconds(120.0);
				}
				return true;
			}
		}

		private void Awake()
		{
			_lifetimeCancellation = new CancellationTokenSource();
		}

		private void OnDestroy()
		{
			_lifetimeCancellation?.Cancel();
			_lifetimeCancellation?.Dispose();
			_lifetimeCancellation = null;
			CancelSteamTicket();
		}

		private void OnApplicationPause(bool paused)
		{
			if (!paused && IsAuthenticated && !HasValidEntityToken)
			{
				InvalidateAuthentication("Entity token expired while the application was suspended.");
			}
		}

		private void OnApplicationQuit()
		{
			CancelSteamTicket();
		}

		public void Initialize()
		{
			if (!IsInitialized)
			{
				IsInitialized = true;
				ClearAuthenticationContext(clearStaticCredentials: false);
				State = PlayFabReportAuthState.Initialized;
				LastError = null;
				Debug.Log("[PlayFabAuthService] Initialized.");
			}
		}

		public void Login(bool isRetry = false)
		{
			LoginWithoutThrowingAsync(isRetry);
		}

		private async Task LoginWithoutThrowingAsync(bool isRetry)
		{
			try
			{
				await EnsureAuthenticatedAsync(isRetry, forceRefresh: false, (_lifetimeCancellation != null) ? _lifetimeCancellation.Token : CancellationToken.None);
			}
			catch (OperationCanceledException)
			{
			}
		}

		public async Task<bool> EnsureAuthenticatedAsync(bool isRetry, bool forceRefresh, CancellationToken cancellationToken)
		{
			if (!IsInitialized)
			{
				Initialize();
			}
			if (!forceRefresh && HasValidEntityToken)
			{
				return true;
			}
			if (_authenticationTask == null || _authenticationTask.IsCompleted)
			{
				CancellationToken cancellationToken2 = ((_lifetimeCancellation != null) ? _lifetimeCancellation.Token : CancellationToken.None);
				_authenticationTask = AuthenticateAsync(isRetry | forceRefresh, cancellationToken2);
			}
			return await AwaitWithCancellation(_authenticationTask, cancellationToken);
		}

		public void InvalidateAuthentication(string error)
		{
			ClearAuthenticationContext(clearStaticCredentials: true);
			State = PlayFabReportAuthState.Failed;
			LastError = error;
			Debug.LogWarning("[PlayFabAuthService] Authentication invalidated: " + error);
		}

		private async Task<bool> AuthenticateAsync(bool isRetry, CancellationToken cancellationToken)
		{
			IsAuthenticating = true;
			IsAuthenticated = false;
			State = (isRetry ? PlayFabReportAuthState.Retrying : PlayFabReportAuthState.Authenticating);
			LastError = null;
			Debug.Log(isRetry ? "[PlayFabAuthService] Login retry requested." : "[PlayFabAuthService] Login requested.");
			try
			{
				if (!TryGetSteamTicket(out var ticketHex))
				{
					SetFailed("Failed to get Steam auth ticket.");
					return false;
				}
				LoginResult loginResult = await LoginWithSteamAsync(ticketHex, cancellationToken);
				PlayFabId = loginResult.PlayFabId;
				if (TryApplyLoginEntityContext(loginResult))
				{
					SetAuthenticated();
					return true;
				}
				Debug.LogWarning("[PlayFabAuthService] LoginResult has no complete entity context. Requesting a fresh entity token.");
				if (!TryApplyEntityContext(await GetEntityTokenAsync(cancellationToken)))
				{
					SetFailed("GetEntityToken returned incomplete entity context.");
					return false;
				}
				SetAuthenticated();
				return true;
			}
			catch (OperationCanceledException)
			{
				SetFailed("PlayFab authentication was cancelled.");
				return false;
			}
			catch (TimeoutException ex2)
			{
				SetFailed(ex2.Message);
				return false;
			}
			catch (Exception ex3)
			{
				SetFailed("PlayFab Steam login failed: " + ex3.Message);
				return false;
			}
			finally
			{
				IsAuthenticating = false;
				CancelSteamTicket();
			}
		}

		private async Task<LoginResult> LoginWithSteamAsync(string ticketHex, CancellationToken cancellationToken)
		{
			TaskCompletionSource<LoginResult> completion = new TaskCompletionSource<LoginResult>(TaskCreationOptions.RunContinuationsAsynchronously);
			PlayFabClientAPI.LoginWithSteam(new LoginWithSteamRequest
			{
				SteamTicket = ticketHex,
				CreateAccount = true
			}, (LoginResult result) =>
			{
				completion.TrySetResult(result);
			}, (PlayFabError error) =>
			{
				completion.TrySetException(new Exception(BuildErrorReport(error)));
			});
			return await AwaitWithTimeout(completion.Task, 20, "PlayFab Steam login timed out.", cancellationToken);
		}

		private async Task<GetEntityTokenResponse> GetEntityTokenAsync(CancellationToken cancellationToken)
		{
			TaskCompletionSource<GetEntityTokenResponse> completion = new TaskCompletionSource<GetEntityTokenResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
			PlayFabAuthenticationAPI.GetEntityToken(new GetEntityTokenRequest(), (GetEntityTokenResponse result) =>
			{
				completion.TrySetResult(result);
			}, (PlayFabError error) =>
			{
				completion.TrySetException(new Exception(BuildErrorReport(error)));
			});
			return await AwaitWithTimeout(completion.Task, 20, "PlayFab entity token request timed out.", cancellationToken);
		}

		private bool TryApplyLoginEntityContext(LoginResult result)
		{
			if (result?.EntityToken?.Entity == null || string.IsNullOrWhiteSpace(result.EntityToken.Entity.Id) || string.IsNullOrWhiteSpace(result.EntityToken.Entity.Type) || string.IsNullOrWhiteSpace(result.EntityToken.EntityToken))
			{
				return false;
			}
			EntityId = result.EntityToken.Entity.Id;
			EntityType = result.EntityToken.Entity.Type;
			EntityToken = result.EntityToken.EntityToken;
			EntityTokenExpirationUtc = ToUtc(result.EntityToken.TokenExpiration);
			PlayFabSettings.staticPlayer.EntityToken = EntityToken;
			return true;
		}

		private bool TryApplyEntityContext(GetEntityTokenResponse result)
		{
			if (result?.Entity == null || string.IsNullOrWhiteSpace(result.Entity.Id) || string.IsNullOrWhiteSpace(result.Entity.Type) || string.IsNullOrWhiteSpace(result.EntityToken))
			{
				return false;
			}
			EntityId = result.Entity.Id;
			EntityType = result.Entity.Type;
			EntityToken = result.EntityToken;
			EntityTokenExpirationUtc = ToUtc(result.TokenExpiration);
			PlayFabSettings.staticPlayer.EntityToken = EntityToken;
			return true;
		}

		private void SetAuthenticated()
		{
			IsAuthenticating = false;
			IsAuthenticated = true;
			State = PlayFabReportAuthState.Authenticated;
			LastError = null;
			Debug.Log("[PlayFabAuthService] Authentication succeeded.\n   PlayFabId: " + PlayFabId + "\n   EntityId: " + EntityId + "\n   EntityType: " + EntityType + $"\n   TokenExpirationUtc: {EntityTokenExpirationUtc:O}");
		}

		private void SetFailed(string error)
		{
			ClearAuthenticationContext(clearStaticCredentials: true);
			State = PlayFabReportAuthState.Failed;
			LastError = error;
			Debug.LogError("[PlayFabAuthService] " + error);
		}

		private void ClearAuthenticationContext(bool clearStaticCredentials)
		{
			IsAuthenticating = false;
			IsAuthenticated = false;
			PlayFabId = null;
			EntityId = null;
			EntityType = null;
			EntityToken = null;
			EntityTokenExpirationUtc = null;
			if (clearStaticCredentials)
			{
				PlayFabSettings.staticPlayer.ForgetAllCredentials();
			}
		}

		private bool TryGetSteamTicket(out string ticketHex)
		{
			ticketHex = null;
			try
			{
				if (!SteamClient.IsValid)
				{
					Debug.LogError("[PlayFabAuthService] SteamClient is not valid.");
					return false;
				}
				CancelSteamTicket();
				_authTicket = SteamUser.GetAuthSessionTicket(SteamClient.SteamId);
				if (_authTicket == null || _authTicket.Handle == 0)
				{
					Debug.LogError("[PlayFabAuthService] Steam auth ticket handle is invalid.");
					_authTicket = null;
					return false;
				}
				if (_authTicket.Data == null || _authTicket.Data.Length == 0)
				{
					Debug.LogError("[PlayFabAuthService] Steam auth ticket data is empty.");
					return false;
				}
				ticketHex = ConvertToHex(_authTicket.Data);
				return !string.IsNullOrWhiteSpace(ticketHex);
			}
			catch (Exception arg)
			{
				Debug.LogError($"[PlayFabAuthService] Exception while getting Steam auth ticket: {arg}");
				CancelSteamTicket();
				return false;
			}
		}

		private void CancelSteamTicket()
		{
			if (_authTicket == null)
			{
				return;
			}
			try
			{
				if (_authTicket.Handle != 0)
				{
					_authTicket.Cancel();
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[PlayFabAuthService] Steam auth ticket cancellation failed: " + ex.Message);
			}
			finally
			{
				_authTicket = null;
			}
		}

		private static DateTime? ToUtc(DateTime? value)
		{
			if (!value.HasValue)
			{
				return null;
			}
			return (value.Value.Kind == DateTimeKind.Utc) ? value.Value : value.Value.ToUniversalTime();
		}

		private static string BuildErrorReport(PlayFabError error)
		{
			if (error == null)
			{
				return "Unknown PlayFab error.";
			}
			return error.GenerateErrorReport();
		}

		private static string ConvertToHex(byte[] bytes)
		{
			return string.Concat(bytes.Select((byte value) => value.ToString("x2")));
		}

		private static async Task<T> AwaitWithTimeout<T>(Task<T> task, int timeoutSeconds, string timeoutMessage, CancellationToken cancellationToken)
		{
			Task task2 = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);
			if (await Task.WhenAny(task, task2) == task)
			{
				return await task;
			}
			cancellationToken.ThrowIfCancellationRequested();
			throw new TimeoutException(timeoutMessage);
		}

		private static async Task<T> AwaitWithCancellation<T>(Task<T> task, CancellationToken cancellationToken)
		{
			if (!cancellationToken.CanBeCanceled)
			{
				return await task;
			}
			Task task2 = Task.Delay(-1, cancellationToken);
			if (await Task.WhenAny(task, task2) == task)
			{
				return await task;
			}
			throw new OperationCanceledException(cancellationToken);
		}
	}
}
