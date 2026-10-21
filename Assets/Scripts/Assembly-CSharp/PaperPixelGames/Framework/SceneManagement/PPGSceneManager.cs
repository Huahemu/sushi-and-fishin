using Michsky.LSS;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PaperPixelGames.Framework.SceneManagement
{
	public class PPGSceneManager : Singleton<PPGSceneManager>
	{
		private string _lastLoadedScene = string.Empty;

		private LoadSceneMode _lastLoadedMode;

		private AsyncOperation _loadingOperation;

		public bool IsLoading
		{
			get
			{
				if (_loadingOperation != null)
				{
					return !_loadingOperation.isDone;
				}
				return false;
			}
		}

		public void LateUpdate()
		{
			if (_loadingOperation != null && _loadingOperation.isDone)
			{
				_loadingOperation = null;
			}
		}

		public void LoadScene(string sceneName, LoadSceneMode loadMode = LoadSceneMode.Single)
		{
			if (_lastLoadedMode == LoadSceneMode.Additive)
			{
				SceneManager.UnloadSceneAsync(_lastLoadedScene);
			}
			if (loadMode == LoadSceneMode.Single && _loadingOperation != null)
			{
				if (!_loadingOperation.isDone)
				{
					UtilityMethods.Log("[PPGSceneManager] Ongoing load operation exists!", LogType.Warning);
					return;
				}
				UtilityMethods.Log($"[PPGSceneManager] Loading scene: {sceneName} - {loadMode}");
				_loadingOperation = SceneManager.LoadSceneAsync(sceneName, loadMode);
				LSS_LoadingScreen.TryToSetLoadingOp(_loadingOperation);
			}
			else
			{
				UtilityMethods.Log($"[PPGSceneManager] Loading scene: {sceneName} - {loadMode}");
				_loadingOperation = SceneManager.LoadSceneAsync(sceneName, loadMode);
				LSS_LoadingScreen.TryToSetLoadingOp(_loadingOperation);
			}
			_lastLoadedMode = loadMode;
			_lastLoadedScene = sceneName;
		}
	}
}
