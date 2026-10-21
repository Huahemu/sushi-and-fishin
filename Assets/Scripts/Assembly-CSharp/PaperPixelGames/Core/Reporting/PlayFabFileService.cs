using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PlayFab;
using PlayFab.DataModels;
using UnityEngine;
using UnityEngine.Networking;

namespace PaperPixelGames.Core.Reporting
{
	public class PlayFabFileService
	{
		private const int PlayFabRequestTimeoutSeconds = 20;

		private const int FileUploadTimeoutSeconds = 30;

		private readonly PlayFabAuthService _auth;

		public PlayFabFileService(PlayFabAuthService auth)
		{
			_auth = auth;
		}

		public async Task<ScreenshotUploadResult> UploadScreenshotAsync(byte[] bytes, string fileName, CancellationToken cancellationToken)
		{
			if (_auth == null || !_auth.HasValidEntityToken)
			{
				return ScreenshotUploadResult.Fail("Entity authentication is not valid.");
			}
			if (bytes == null || bytes.Length == 0)
			{
				return ScreenshotUploadResult.Fail("Screenshot payload is empty.");
			}
			InitiateFileUploadsResponse initiation = null;
			bool finalized = false;
			ScreenshotUploadResult result;
			try
			{
				_ = 2;
				try
				{
					initiation = await InitiateAsync(fileName, cancellationToken);
					if (initiation?.UploadDetails == null || initiation.UploadDetails.Count == 0)
					{
						result = ScreenshotUploadResult.Fail("PlayFab returned no upload URL.");
					}
					else
					{
						InitiateFileUploadMetadata initiateFileUploadMetadata = initiation.UploadDetails.Find((InitiateFileUploadMetadata detail) => detail != null && string.Equals(detail.FileName, fileName, StringComparison.Ordinal));
						if (initiateFileUploadMetadata == null)
						{
							initiateFileUploadMetadata = initiation.UploadDetails[0];
						}
						if (string.IsNullOrWhiteSpace(initiateFileUploadMetadata?.UploadUrl))
						{
							result = ScreenshotUploadResult.Fail("PlayFab returned an invalid upload URL.");
						}
						else
						{
							await UploadAsync(initiateFileUploadMetadata.UploadUrl, bytes, cancellationToken);
							await FinalizeAsync(fileName, initiation.ProfileVersion, cancellationToken);
							finalized = true;
							result = ScreenshotUploadResult.Ok(fileName, _auth.EntityId, _auth.EntityType);
						}
					}
				}
				catch (OperationCanceledException)
				{
					result = ScreenshotUploadResult.Fail("Screenshot upload was cancelled.");
				}
				catch (TimeoutException ex2)
				{
					result = ScreenshotUploadResult.Fail(ex2.Message);
				}
				catch (Exception ex3)
				{
					result = ScreenshotUploadResult.Fail(ex3.Message);
				}
			}
			finally
			{
				if (!finalized && initiation != null)
				{
					await TryAbortAsync(fileName, initiation.ProfileVersion);
				}
			}
			return result;
		}

		public async Task<bool> DeleteScreenshotAsync(string fileName, CancellationToken cancellationToken)
		{
			if (_auth == null || !_auth.HasValidEntityToken || string.IsNullOrWhiteSpace(fileName))
			{
				return false;
			}
			TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			PlayFabDataAPI.DeleteFiles(new DeleteFilesRequest
			{
				Entity = BuildEntityKey(),
				FileNames = new List<string> { fileName }
			}, (DeleteFilesResponse _) =>
			{
				completion.TrySetResult(result: true);
			}, (PlayFabError error) =>
			{
				completion.TrySetException(new Exception(error.GenerateErrorReport()));
			});
			try
			{
				return await AwaitWithTimeout(completion.Task, 20, "PlayFab screenshot delete timed out.", cancellationToken);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[PlayFabFileService] Screenshot cleanup failed: " + ex.Message);
				return false;
			}
		}

		private async Task<InitiateFileUploadsResponse> InitiateAsync(string fileName, CancellationToken cancellationToken)
		{
			TaskCompletionSource<InitiateFileUploadsResponse> completion = new TaskCompletionSource<InitiateFileUploadsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
			PlayFabDataAPI.InitiateFileUploads(new InitiateFileUploadsRequest
			{
				Entity = BuildEntityKey(),
				FileNames = new List<string> { fileName }
			}, (InitiateFileUploadsResponse result) =>
			{
				completion.TrySetResult(result);
			}, (PlayFabError error) =>
			{
				completion.TrySetException(new Exception(error.GenerateErrorReport()));
			});
			return await AwaitWithTimeout(completion.Task, 20, "PlayFab file initiation timed out.", cancellationToken);
		}

		private static async Task UploadAsync(string url, byte[] bytes, CancellationToken cancellationToken)
		{
			using UnityWebRequest request = UnityWebRequest.Put(url, bytes);
			request.timeout = 30;
			request.SetRequestHeader("Content-Type", "image/jpeg");
			using (cancellationToken.Register(request.Abort))
			{
				UnityWebRequestAsyncOperation operation = request.SendWebRequest();
				while (!operation.isDone)
				{
					cancellationToken.ThrowIfCancellationRequested();
					await Task.Yield();
				}
				if (request.result != UnityWebRequest.Result.Success)
				{
					throw new Exception("Screenshot PUT failed: " + request.error);
				}
			}
		}

		private async Task FinalizeAsync(string fileName, int profileVersion, CancellationToken cancellationToken)
		{
			TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			PlayFabDataAPI.FinalizeFileUploads(new FinalizeFileUploadsRequest
			{
				Entity = BuildEntityKey(),
				FileNames = new List<string> { fileName },
				ProfileVersion = profileVersion
			}, (FinalizeFileUploadsResponse _) =>
			{
				completion.TrySetResult(result: true);
			}, (PlayFabError error) =>
			{
				completion.TrySetException(new Exception(error.GenerateErrorReport()));
			});
			await AwaitWithTimeout(completion.Task, 20, "PlayFab file finalization timed out.", cancellationToken);
		}

		private async Task TryAbortAsync(string fileName, int profileVersion)
		{
			if (_auth == null || !_auth.HasValidEntityToken)
			{
				return;
			}
			TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			PlayFabDataAPI.AbortFileUploads(new AbortFileUploadsRequest
			{
				Entity = BuildEntityKey(),
				FileNames = new List<string> { fileName },
				ProfileVersion = profileVersion
			}, (AbortFileUploadsResponse _) =>
			{
				completion.TrySetResult(result: true);
			}, (PlayFabError _) =>
			{
				completion.TrySetResult(result: false);
			});
			try
			{
				if (!(await AwaitWithTimeout(completion.Task, 20, "PlayFab file abort timed out.", CancellationToken.None)))
				{
					Debug.LogWarning("[PlayFabFileService] Pending upload could not be aborted: " + fileName);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[PlayFabFileService] Pending upload cleanup failed: " + ex.Message);
			}
		}

		private EntityKey BuildEntityKey()
		{
			return new EntityKey
			{
				Id = _auth.EntityId,
				Type = _auth.EntityType
			};
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
	}
}
