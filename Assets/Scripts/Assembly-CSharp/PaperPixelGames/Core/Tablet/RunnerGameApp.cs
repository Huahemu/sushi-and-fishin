using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Economy;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityServiceLocator;

namespace PaperPixelGames.Core.Tablet
{
	public sealed class RunnerGameApp : BaseApp
	{
		private sealed class PoolEntry
		{
			public readonly RectTransform Root;

			public readonly RectTransform Hitbox;

			public float SpawnY;

			public float SlalomAmplitude;

			public float SlalomPhase;

			public PoolEntry(RectTransform root, RectTransform hitbox)
			{
				Root = root;
				Hitbox = hitbox;
			}
		}

		private const float Gravity = 850f;

		private const float JumpInitialVelocity = 290f;

		private const float JumpHoldAcceleration = 600f;

		private const float JumpHoldDuration = 0.15f;

		private const float MaxJumpHeight = 120f;

		private const float MaxFallSpeed = 650f;

		private const float ScorePerSecond = 10f;

		private const string HighScorePreferenceKey = "runner_game_high_score";

		private const float FishBaseSpawnInterval = 1.4f;

		private const float FishMinSpawnInterval = 0.55f;

		private const float FishBaseSpeed = 140f;

		private const float FishMaxSpeed = 280f;

		private const float FishSpeedIncreasePerScore = 0.35f;

		private const float FishSpawnIntervalDecreasePerScore = 0.004f;

		private const float CoinBaseSpawnInterval = 2.1f;

		private const float CoinMinSpawnInterval = 1.1f;

		private const float CoinSpawnIntervalDecreasePerScore = 0.006f;

		private const float CoinSpawnChance = 0.5f;

		private const float CoinSpawnDelayAfterFish = 0.35f;

		private const float FishSlalomMinAmplitude = 5f;

		private const float FishSlalomMaxAmplitude = 9f;

		private const float FishSlalomAngularFrequency = 3.5f;

		private const string HitboxChildName = "Hitbox";

		private const float RunMinFramesPerSecond = 24f;

		private const float RunMaxFramesPerSecond = 36f;

		private const float JumpFramesPerSecond = 6f;

		private const float FarParallaxUvSpeed = 0.012f;

		private const float NearParallaxUvSpeed = 0.035f;

		[Header("UI")]
		[SerializeField]
		private RectTransform _gameArea;

		[SerializeField]
		private Graphic _inputBlocker;

		[SerializeField]
		private RawImage _farParallaxLayer;

		[SerializeField]
		private RawImage _nearParallaxLayer;

		[SerializeField]
		private TextMeshProUGUI _startText;

		[SerializeField]
		private TextMeshProUGUI _highScoreText;

		[SerializeField]
		private TextMeshProUGUI _scoreText;

		[SerializeField]
		private TextMeshProUGUI _gameOverScoreText;

		[SerializeField]
		private TextMeshProUGUI _gameOverCoinText;

		[SerializeField]
		private TextMeshProUGUI _retryText;

		[Header("Player")]
		[SerializeField]
		private RectTransform _player;

		[SerializeField]
		private Image _playerImage;

		[SerializeField]
		private Sprite[] _runFrames;

		[SerializeField]
		private Sprite[] _jumpFrames;

		[Header("Fish Pool")]
		[SerializeField]
		private Transform _fishPoolParent;

		[SerializeField]
		private float _fishSpawnX;

		[SerializeField]
		private float _fishDespawnX;

		[SerializeField]
		private float _fishMinY;

		[SerializeField]
		private float _fishMaxY;

		[Header("Coin Pool")]
		[SerializeField]
		private Transform _coinPoolParent;

		[SerializeField]
		private float _coinSpawnX;

		[SerializeField]
		private float _coinDespawnX;

		[SerializeField]
		private float _coinMinY;

		[SerializeField]
		private float _coinMaxY;

		private readonly List<PoolEntry> _fishPool = new List<PoolEntry>();

		private readonly List<PoolEntry> _coinPool = new List<PoolEntry>();

		private readonly List<PoolEntry> _activeFish = new List<PoolEntry>();

		private readonly List<PoolEntry> _activeCoins = new List<PoolEntry>();

		private readonly Vector3[] _firstWorldCorners = new Vector3[4];

		private readonly Vector3[] _secondWorldCorners = new Vector3[4];

		private bool _initialized;

		private bool _isRunning;

		private bool _payoutGranted;

		private bool _ownsGameplayLock;

		private bool _ownsCursorOverride;

		private bool _previousCursorVisibility;

		private bool _wasAirborne;

		private float _elapsedTime;

		private float _fishSpawnTimer;

		private float _coinSpawnTimer;

		private float _verticalVelocity;

		private float _jumpHoldTime;

		private float _animationTimer;

		private float _farParallaxOffset;

		private float _nearParallaxOffset;

		private Vector2 _playerGroundPosition;

		private int _animationFrameIndex;

		private RectTransform _playerHitbox;

		private int _score;

		private int _highScore;

		private int _coinsCollected;

		private float CurrentFishSpeed => Mathf.Min(140f + (float)_score * 0.35f, 280f);

		private float CurrentFishSpawnInterval => Mathf.Max(0.55f, 1.4f - (float)_score * 0.004f);

		private float CurrentCoinSpawnInterval => Mathf.Max(1.1f, 2.1f - (float)_score * 0.006f);

		private float CurrentRunFramesPerSecond => Mathf.Lerp(24f, 36f, Mathf.InverseLerp(140f, 280f, CurrentFishSpeed));

		public override void Initialize()
		{
			if (!_initialized)
			{
				_initialized = true;
				CachePool(_fishPoolParent, _fishPool);
				CachePool(_coinPoolParent, _coinPool);
				SetPoolActive(_fishPool, isActive: false);
				SetPoolActive(_coinPool, isActive: false);
				if (_inputBlocker != null)
				{
					_inputBlocker.raycastTarget = true;
				}
				if (_player != null)
				{
					_playerGroundPosition = _player.anchoredPosition;
					_playerHitbox = FindHitbox(_player);
				}
				_highScore = LoadHighScore();
				ShowStartScreen();
			}
		}

		public override void Show()
		{
			base.Show();
			SetGameplayInputLock(shouldLock: true);
			if (!_ownsCursorOverride)
			{
				_previousCursorVisibility = CursorHelper.CursorVisible;
				_ownsCursorOverride = true;
			}
			CursorHelper.CursorVisible = false;
			EventSystem.current?.SetSelectedGameObject(null);
			ShowStartScreen();
		}

		public override void Hide()
		{
			StopRun();
			SetGameplayInputLock(shouldLock: false);
			ReleaseCursorOverride();
			base.Hide();
		}

		private void OnDisable()
		{
			StopRun();
			SetGameplayInputLock(shouldLock: false);
			ReleaseCursorOverride();
		}

		private void OnDestroy()
		{
			SetGameplayInputLock(shouldLock: false);
			ReleaseCursorOverride();
		}

		private void ReleaseCursorOverride()
		{
			if (_ownsCursorOverride)
			{
				_ownsCursorOverride = false;
				CursorHelper.CursorVisible = _previousCursorVisibility;
			}
		}

		private void Update()
		{
			if (!_initialized || _gameArea == null)
			{
				return;
			}
			float unscaledDeltaTime = Time.unscaledDeltaTime;
			UpdateParallax(unscaledDeltaTime);
			if (!_isRunning)
			{
				if (WasSpacePressed())
				{
					StartRun();
				}
				return;
			}
			UpdateScore(unscaledDeltaTime);
			UpdateJump(unscaledDeltaTime);
			UpdatePlayerAnimation(unscaledDeltaTime);
			bool fishSpawnedThisFrame = UpdateFishSpawn(unscaledDeltaTime);
			UpdateCoinSpawn(unscaledDeltaTime, fishSpawnedThisFrame);
			MovePoolEntries(_activeFish, CurrentFishSpeed, _fishDespawnX, unscaledDeltaTime, applySlalom: true);
			MovePoolEntries(_activeCoins, CurrentFishSpeed, _coinDespawnX, unscaledDeltaTime, applySlalom: false);
			CheckCollisions();
		}

		private void StartRun()
		{
			ResetRunState();
			_isRunning = true;
			SetTextVisible(_startText, isVisible: false);
			SetTextVisible(_highScoreText, isVisible: false);
			SetTextVisible(_gameOverScoreText, isVisible: false);
			SetTextVisible(_gameOverCoinText, isVisible: false);
			SetTextVisible(_retryText, isVisible: false);
			SetTextVisible(_scoreText, isVisible: true);
			RefreshScoreText();
		}

		private void EndRun()
		{
			if (_isRunning)
			{
				_isRunning = false;
				if (!_payoutGranted && _coinsCollected > 0 && TryGetEconomyHandler(out var economyHandler))
				{
					economyHandler.EarnMoney(_coinsCollected, ulong.MaxValue);
					_payoutGranted = true;
				}
				if (_score > _highScore)
				{
					_highScore = _score;
					SaveHighScore();
				}
				SetTextVisible(_scoreText, isVisible: false);
				SetTextVisible(_gameOverScoreText, isVisible: true);
				SetTextVisible(_gameOverCoinText, isVisible: true);
				SetTextVisible(_retryText, isVisible: true);
				SetText(_gameOverScoreText, _score);
				SetText(_gameOverCoinText, _coinsCollected);
			}
		}

		private void StopRun()
		{
			_isRunning = false;
			SetPoolActive(_activeFish, isActive: false);
			SetPoolActive(_activeCoins, isActive: false);
			_activeFish.Clear();
			_activeCoins.Clear();
		}

		private void ResetRunState()
		{
			StopRun();
			_elapsedTime = 0f;
			_fishSpawnTimer = 1.4f;
			_coinSpawnTimer = 2.1f;
			_verticalVelocity = 0f;
			_jumpHoldTime = 0f;
			ResetPlayerAnimation();
			_score = 0;
			_coinsCollected = 0;
			_payoutGranted = false;
			if (_player != null)
			{
				_player.anchoredPosition = _playerGroundPosition;
			}
		}

		private void ShowStartScreen()
		{
			StopRun();
			SetTextVisible(_startText, isVisible: true);
			SetTextVisible(_highScoreText, isVisible: true);
			SetTextVisible(_scoreText, isVisible: false);
			SetTextVisible(_gameOverScoreText, isVisible: false);
			SetTextVisible(_gameOverCoinText, isVisible: false);
			SetTextVisible(_retryText, isVisible: false);
			SetText(_highScoreText, _highScore);
		}

		private void UpdateScore(float deltaTime)
		{
			_elapsedTime += deltaTime;
			_score = Mathf.FloorToInt(_elapsedTime * 10f);
			RefreshScoreText();
		}

		private void UpdateJump(float deltaTime)
		{
			if (!(_player == null))
			{
				if (_player.anchoredPosition.y <= _playerGroundPosition.y && WasJumpPressed())
				{
					_verticalVelocity = 290f;
					_jumpHoldTime = 0f;
				}
				if (IsJumpHeld() && _verticalVelocity > 0f && _jumpHoldTime < 0.15f)
				{
					_verticalVelocity += 600f * deltaTime;
					_jumpHoldTime += deltaTime;
				}
				_verticalVelocity -= 850f * deltaTime;
				_verticalVelocity = Mathf.Max(_verticalVelocity, -650f);
				Vector2 anchoredPosition = _player.anchoredPosition + Vector2.up * (_verticalVelocity * deltaTime);
				float num = _playerGroundPosition.y + 120f;
				if (anchoredPosition.y >= num)
				{
					anchoredPosition.y = num;
					_verticalVelocity = 0f;
					_jumpHoldTime = 0.15f;
				}
				if (anchoredPosition.y <= _playerGroundPosition.y)
				{
					anchoredPosition.y = _playerGroundPosition.y;
					_verticalVelocity = 0f;
					_jumpHoldTime = 0f;
				}
				_player.anchoredPosition = anchoredPosition;
			}
		}

		private void UpdatePlayerAnimation(float deltaTime)
		{
			if (_playerImage == null)
			{
				return;
			}
			bool flag = _player != null && _player.anchoredPosition.y > _playerGroundPosition.y;
			Sprite[] array = (flag ? _jumpFrames : _runFrames);
			float num = (flag ? 6f : CurrentRunFramesPerSecond);
			if (array == null || array.Length == 0)
			{
				return;
			}
			if (_wasAirborne != flag)
			{
				_wasAirborne = flag;
				_animationTimer = 0f;
				_animationFrameIndex = 0;
			}
			if (array.Length == 1)
			{
				_playerImage.sprite = array[0];
				return;
			}
			_animationTimer += deltaTime;
			float num2 = 1f / num;
			int num3 = Mathf.FloorToInt(_animationTimer / num2);
			if (num3 > 0)
			{
				_animationTimer %= num2;
				_animationFrameIndex = (_animationFrameIndex + num3) % array.Length;
			}
			_playerImage.sprite = array[_animationFrameIndex % array.Length];
		}

		private void UpdateParallax(float deltaTime)
		{
			float num = (_isRunning ? (CurrentFishSpeed / 140f) : 1f);
			_farParallaxOffset = Mathf.Repeat(_farParallaxOffset + 0.012f * num * deltaTime, 1f);
			_nearParallaxOffset = Mathf.Repeat(_nearParallaxOffset + 0.035f * num * deltaTime, 1f);
			SetParallaxOffset(_farParallaxLayer, _farParallaxOffset);
			SetParallaxOffset(_nearParallaxLayer, _nearParallaxOffset);
		}

		private static void SetParallaxOffset(RawImage layer, float horizontalOffset)
		{
			if (!(layer == null))
			{
				Rect uvRect = layer.uvRect;
				uvRect.x = horizontalOffset;
				layer.uvRect = uvRect;
			}
		}

		private void ResetPlayerAnimation()
		{
			_animationTimer = 0f;
			_animationFrameIndex = 0;
			_wasAirborne = false;
			if (_playerImage != null && _runFrames != null && _runFrames.Length != 0)
			{
				_playerImage.sprite = _runFrames[0];
			}
		}

		private bool UpdateFishSpawn(float deltaTime)
		{
			_fishSpawnTimer -= deltaTime;
			if (_fishSpawnTimer > 0f)
			{
				return false;
			}
			SpawnFromPool(_fishPool, _activeFish, _fishSpawnX, _fishMinY, _fishMaxY, applySlalom: true);
			_fishSpawnTimer = CurrentFishSpawnInterval;
			return true;
		}

		private void UpdateCoinSpawn(float deltaTime, bool fishSpawnedThisFrame)
		{
			_coinSpawnTimer -= deltaTime;
			if (_coinSpawnTimer > 0f)
			{
				return;
			}
			if (fishSpawnedThisFrame)
			{
				_coinSpawnTimer = 0.35f;
				return;
			}
			if (UnityEngine.Random.value <= 0.5f)
			{
				SpawnFromPool(_coinPool, _activeCoins, _coinSpawnX, _coinMinY, _coinMaxY, applySlalom: false);
			}
			_coinSpawnTimer = CurrentCoinSpawnInterval;
		}

		private void MovePoolEntries(List<PoolEntry> activeEntries, float speed, float despawnX, float deltaTime, bool applySlalom)
		{
			for (int num = activeEntries.Count - 1; num >= 0; num--)
			{
				PoolEntry poolEntry = activeEntries[num];
				if (poolEntry.Root == null)
				{
					activeEntries.RemoveAt(num);
				}
				else
				{
					Vector2 gameAreaPosition = GetGameAreaPosition(poolEntry.Root);
					gameAreaPosition.x -= speed * deltaTime;
					if (applySlalom)
					{
						gameAreaPosition.y = poolEntry.SpawnY + Mathf.Sin(_elapsedTime * 3.5f + poolEntry.SlalomPhase) * poolEntry.SlalomAmplitude;
					}
					SetGameAreaPosition(poolEntry.Root, gameAreaPosition);
					if (gameAreaPosition.x < despawnX)
					{
						poolEntry.Root.gameObject.SetActive(value: false);
						activeEntries.RemoveAt(num);
					}
				}
			}
		}

		private void CheckCollisions()
		{
			if (_playerHitbox == null)
			{
				return;
			}
			for (int num = _activeFish.Count - 1; num >= 0; num--)
			{
				if (IsOverlapping(_playerHitbox, _activeFish[num].Hitbox))
				{
					EndRun();
					return;
				}
			}
			for (int num2 = _activeCoins.Count - 1; num2 >= 0; num2--)
			{
				PoolEntry poolEntry = _activeCoins[num2];
				if (IsOverlapping(_playerHitbox, poolEntry.Hitbox))
				{
					poolEntry.Root.gameObject.SetActive(value: false);
					_activeCoins.RemoveAt(num2);
					_coinsCollected++;
				}
			}
		}

		private void SpawnFromPool(List<PoolEntry> pool, List<PoolEntry> activeEntries, float spawnX, float minY, float maxY, bool applySlalom)
		{
			PoolEntry inactiveEntry = GetInactiveEntry(pool);
			if (inactiveEntry != null && !(inactiveEntry.Root == null))
			{
				float num = Mathf.Min(minY, maxY);
				float num2 = Mathf.Max(minY, maxY);
				inactiveEntry.SlalomAmplitude = (applySlalom ? UnityEngine.Random.Range(5f, 9f) : 0f);
				inactiveEntry.SlalomPhase = UnityEngine.Random.value * MathF.PI * 2f;
				SetGameAreaPosition(gameAreaPosition: new Vector2(spawnX, inactiveEntry.SpawnY = UnityEngine.Random.Range(num + inactiveEntry.SlalomAmplitude, num2 - inactiveEntry.SlalomAmplitude)), target: inactiveEntry.Root);
				inactiveEntry.Root.gameObject.SetActive(value: true);
				activeEntries.Add(inactiveEntry);
			}
		}

		private static PoolEntry GetInactiveEntry(List<PoolEntry> pool)
		{
			int num = 0;
			foreach (PoolEntry item in pool)
			{
				if (item.Root != null && !item.Root.gameObject.activeSelf)
				{
					num++;
				}
			}
			if (num == 0)
			{
				return null;
			}
			int num2 = UnityEngine.Random.Range(0, num);
			foreach (PoolEntry item2 in pool)
			{
				if (!(item2.Root == null) && !item2.Root.gameObject.activeSelf && num2-- == 0)
				{
					return item2;
				}
			}
			return null;
		}

		private bool IsOverlapping(RectTransform first, RectTransform second)
		{
			if (first == null || second == null || _gameArea == null)
			{
				return false;
			}
			Rect gameAreaRect = GetGameAreaRect(first, _firstWorldCorners);
			Rect gameAreaRect2 = GetGameAreaRect(second, _secondWorldCorners);
			return gameAreaRect.Overlaps(gameAreaRect2, allowInverse: true);
		}

		private Rect GetGameAreaRect(RectTransform target, Vector3[] worldCorners)
		{
			target.GetWorldCorners(worldCorners);
			Vector3 vector = _gameArea.InverseTransformPoint(worldCorners[0]);
			float num = vector.x;
			float num2 = vector.x;
			float num3 = vector.y;
			float num4 = vector.y;
			for (int i = 1; i < worldCorners.Length; i++)
			{
				Vector3 vector2 = _gameArea.InverseTransformPoint(worldCorners[i]);
				num = Mathf.Min(num, vector2.x);
				num2 = Mathf.Max(num2, vector2.x);
				num3 = Mathf.Min(num3, vector2.y);
				num4 = Mathf.Max(num4, vector2.y);
			}
			return Rect.MinMaxRect(num, num3, num2, num4);
		}

		private void SetGameAreaPosition(RectTransform target, Vector2 gameAreaPosition)
		{
			if (!(target == null) && !(_gameArea == null))
			{
				target.position = _gameArea.TransformPoint(new Vector3(gameAreaPosition.x, gameAreaPosition.y, 0f));
			}
		}

		private Vector2 GetGameAreaPosition(RectTransform target)
		{
			if (target == null || _gameArea == null)
			{
				return Vector2.zero;
			}
			Vector3 vector = _gameArea.InverseTransformPoint(target.position);
			return new Vector2(vector.x, vector.y);
		}

		private void CachePool(Transform parent, List<PoolEntry> destination)
		{
			destination.Clear();
			if (parent == null)
			{
				return;
			}
			foreach (Transform item in parent)
			{
				RectTransform rectTransform = item as RectTransform;
				if (rectTransform != null)
				{
					destination.Add(new PoolEntry(rectTransform, FindHitbox(rectTransform)));
				}
			}
		}

		private static RectTransform FindHitbox(RectTransform root)
		{
			return (root.Find("Hitbox") as RectTransform) ?? root;
		}

		private static void SetPoolActive(List<PoolEntry> entries, bool isActive)
		{
			foreach (PoolEntry entry in entries)
			{
				if (entry.Root != null)
				{
					entry.Root.gameObject.SetActive(isActive);
				}
			}
		}

		private void RefreshScoreText()
		{
			SetText(_scoreText, _score);
		}

		private int LoadHighScore()
		{
			return PlayerPrefs.GetInt("runner_game_high_score");
		}

		private void SaveHighScore()
		{
			PlayerPrefs.SetInt("runner_game_high_score", _highScore);
			PlayerPrefs.Save();
		}

		private void SetGameplayInputLock(bool shouldLock)
		{
			if (_ownsGameplayLock != shouldLock)
			{
				InputHandler inputHandler = Singleton<InputManager>.Instance?.InputHandler;
				if (!(inputHandler == null))
				{
					inputHandler.SetGameplayInputSuppressed(GetInstanceID(), shouldLock);
					_ownsGameplayLock = shouldLock;
				}
			}
		}

		private bool TryGetEconomyHandler(out EconomyHandler economyHandler)
		{
			economyHandler = null;
			if (ServiceLocator.ForSceneOf(this).TryGet<EconomyHandler>(out economyHandler))
			{
				return economyHandler != null;
			}
			return false;
		}

		private static bool WasSpacePressed()
		{
			if (Keyboard.current != null)
			{
				return Keyboard.current.spaceKey.wasPressedThisFrame;
			}
			return false;
		}

		private static bool WasJumpPressed()
		{
			if (Keyboard.current != null)
			{
				if (!Keyboard.current.spaceKey.wasPressedThisFrame && !Keyboard.current.wKey.wasPressedThisFrame)
				{
					return Keyboard.current.upArrowKey.wasPressedThisFrame;
				}
				return true;
			}
			return false;
		}

		private static bool IsJumpHeld()
		{
			if (Keyboard.current != null)
			{
				if (!Keyboard.current.spaceKey.isPressed && !Keyboard.current.wKey.isPressed)
				{
					return Keyboard.current.upArrowKey.isPressed;
				}
				return true;
			}
			return false;
		}

		private static void SetTextVisible(TextMeshProUGUI text, bool isVisible)
		{
			if (text != null)
			{
				text.enabled = isVisible;
			}
		}

		private static void SetText(TextMeshProUGUI text, int value)
		{
			if (text != null)
			{
				text.text = value.ToString();
			}
		}
	}
}
