using DG.Tweening;
using PaperPixelGames.Core.Data.Tools;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.PlayerTools
{
	public sealed class FishingRodUI : MonoBehaviour
	{
		private static readonly OceanLayer[] CastZones = new OceanLayer[5]
		{
			OceanLayer.SunlightZone,
			OceanLayer.MidZone,
			OceanLayer.DeepZone,
			OceanLayer.AbyssalRift,
			OceanLayer.ForgottenDepth
		};

		[SerializeField]
		private GameObject _visualObject;

		[SerializeField]
		private RectTransform _castTrack;

		[SerializeField]
		private RectTransform _arrowPivot;

		[SerializeField]
		private RectTransform[] _zoneAreas = new RectTransform[5];

		[SerializeField]
		private TextMeshProUGUI _castZoneText;

		[SerializeField]
		private GameObject _challengeHudRoot;

		[SerializeField]
		private Image _progressFill;

		[SerializeField]
		private Image _tensionFill;

		[SerializeField]
		private TextMeshProUGUI _progressValue;

		[SerializeField]
		private TextMeshProUGUI _tensionValue;

		[SerializeField]
		private TextMeshProUGUI _counterText;

		[SerializeField]
		private TextMeshProUGUI _directionText;

		[SerializeField]
		[Min(0f)]
		private float _fillTweenDuration = 0.12f;

		[SerializeField]
		private Color _counterAccentColor = new Color(1f, 0.76f, 0.2f);

		[SerializeField]
		private Color _counterTiredColor = new Color(0.35f, 1f, 0.52f);

		[SerializeField]
		private Color _counterWarningColor = new Color(1f, 0.48f, 0.12f);

		[SerializeField]
		private Color _counterDangerColor = new Color(1f, 0.12f, 0.08f);

		[SerializeField]
		[Range(0f, 1f)]
		private float _counterWarningTension = 0.65f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _counterDangerTension = 0.85f;

		[SerializeField]
		[Min(0f)]
		private float _counterEntranceDistance = 55f;

		[SerializeField]
		[Min(0f)]
		private float _counterEntranceDuration = 0.18f;

		[SerializeField]
		[Min(0f)]
		private float _counterPunchDistance = 12f;

		[SerializeField]
		[Range(0f, 0.5f)]
		private float _counterPunchScale = 0.12f;

		[SerializeField]
		[Range(0f, 0.25f)]
		private float _counterReelPulseScale = 0.045f;

		private FishingProgressRing _feedbackRing;

		private float _cancelProgress;

		private float _counterSuccessUntil;

		private float _progressTarget;

		private float _tensionTarget;

		private float _lastTension;

		private float _trendUntil;

		private string _tensionTrend = "";

		private string _reelBinding = "";

		private RectTransform _counterRect;

		private Vector2 _counterBasePosition;

		private Vector3 _counterBaseScale = Vector3.one;

		private Color _counterBaseColor = Color.white;

		private Color _tensionBaseColor = Color.white;

		private Sequence _counterSequence;

		private Tween _counterPulseTween;

		private Tween _counterShakeTween;

		private string _currentCounterText = string.Empty;

		private FishingRodFishDirection _currentCounterDirection;

		private bool _currentCounterReeling;

		private float _currentCounterTension;

		private int _counterTensionBand = -1;

		private void Awake()
		{
			CacheCounterDefaults();
			ResetVisuals();
			SetLegacyVisible(visible: false);
			SetChallengeVisible(visible: false);
		}

		private void OnDisable()
		{
			KillTweens();
		}

		public void ShowReady()
		{
			SetLegacyVisible(visible: false);
			SetChallengeVisible(visible: false);
			SetCastZoneVisible(visible: false);
		}

		public void StartCast(FishingRodData data, FishingRodLevelData levelData)
		{
			ResetVisuals();
			SetChallengeVisible(visible: false);
			SetLegacyVisible(visible: true);
			SetCastZoneVisible(visible: true);
			ConfigureCastScale(data, levelData);
			SetCastCharge(0f);
			SetCastZoneFeedback(string.Empty);
		}

		public void SetCastCharge(float normalizedCharge)
		{
			if (!(_castTrack == null) && !(_arrowPivot == null))
			{
				Vector2 anchoredPosition = _arrowPivot.anchoredPosition;
				anchoredPosition.y = Mathf.Lerp(_castTrack.rect.yMin, _castTrack.rect.yMax, Mathf.Clamp01(normalizedCharge));
				_arrowPivot.anchoredPosition = anchoredPosition;
			}
		}

		public void SetCastZoneFeedback(string value)
		{
			if (_castZoneText != null)
			{
				_castZoneText.text = value ?? string.Empty;
			}
		}

		public void StopCast()
		{
			SetLegacyVisible(visible: false);
			SetCastZoneVisible(visible: false);
		}

		public void ShowWaiting()
		{
			SetLegacyVisible(visible: false);
			SetChallengeVisible(visible: false);
		}

		public void ShowChallenge(FishingRodFishDirection direction, float catchProgress, float lineTension, bool reeling, float counterProgress = 0f, float preparationRemaining = 0f, string reelBinding = "")
		{
			_reelBinding = reelBinding;
			SetMeterVisibility(visible: true);
			SetDirectionInstruction(direction);
			EnsureFeedbackRing();
			SetLegacyVisible(visible: false);
			SetChallengeVisible(visible: true);
			SetProgress(catchProgress);
			SetTension(lineTension);
			string value = ((_cancelProgress > 0f) ? FishingRodText.Get("cancel_hold", "HOLD TO CANCEL", "İPTAL İÇİN BASILI TUT") : ((preparationRemaining > 0f) ? FishingRodText.Get("get_ready", "GET READY", "HAZIRLAN") : GetCounterInstruction(direction, reeling)));
			bool flag = direction == FishingRodFishDirection.Left || direction == FishingRodFishDirection.Right || direction == FishingRodFishDirection.Forward;
			bool flag2 = Time.unscaledTime < _counterSuccessUntil;
			_feedbackRing.gameObject.SetActive((flag || _cancelProgress > 0f) | flag2);
			_feedbackRing.color = ((_cancelProgress > 0f) ? _counterWarningColor : ((Time.unscaledTime < _counterSuccessUntil) ? _counterTiredColor : _counterAccentColor));
			_feedbackRing.SetProgress((_cancelProgress > 0f) ? _cancelProgress : (flag2 ? 1f : counterProgress));
			SetCounter(value, direction, reeling);
			UpdateCounterTensionFeeling(lineTension);
		}

		public void ShowSuccess()
		{
			SetLegacyVisible(visible: false);
			SetChallengeVisible(visible: false);
		}

		public void ShowFailure(bool wrongReel = false)
		{
			HideDirectionInstruction();
			SetLegacyVisible(visible: false);
			SetChallengeVisible(visible: true);
			PlayCounterFailure();
			if (_counterText != null)
			{
				TextMeshProUGUI counterText = _counterText;
				counterText.text = counterText.text + "\n<size=60%>" + (wrongReel ? FishingRodText.Get("failure_reel", "Stop reeling while the fish pulls.", "Balık çekerken sarmayı durdur.") : FishingRodText.Get("failure_counter", "Follow the counter arrow to reduce tension.", "Gerilimi azaltmak için yön okunu takip et.")) + "</size>";
			}
		}

		public void StopFishing()
		{
			KillTweens();
			ResetVisuals();
			SetLegacyVisible(visible: false);
			SetChallengeVisible(visible: false);
		}

		public void ClearDebug()
		{
		}

		private void SetProgress(float value)
		{
			SetFill(_progressFill, value);
			if (_progressValue != null)
			{
				_progressValue.text = $"{Mathf.Clamp01(value) * 100f:0}%";
			}
		}

		private void SetTension(float value)
		{
			float num = Mathf.Clamp01(value);
			SetFill(_tensionFill, num);
			if (Mathf.Abs(num - _lastTension) > 0.001f)
			{
				_tensionTrend = ((num > _lastTension) ? "+" : "−");
				_trendUntil = Time.unscaledTime + 0.3f;
			}
			if (_tensionValue != null)
			{
				_tensionValue.text = ((num >= _counterDangerTension) ? "! " : "") + $"{num * 100f:0}% " + ((Time.unscaledTime < _trendUntil) ? _tensionTrend : "");
			}
			_lastTension = num;
		}

		private string GetCounterInstruction(FishingRodFishDirection direction, bool reeling)
		{
			if (direction == FishingRodFishDirection.Centered || direction == FishingRodFishDirection.Tired)
			{
				return FishingRodText.Get("reel_now", "REEL · {0}", "SAR · {0}", _reelBinding) + "\n<size=65%>" + ((direction == FishingRodFishDirection.Tired) ? FishingRodText.Get("tired", "Fish is tired", "Balık yoruldu") : FishingRodText.Get("steady", "Line is steady", "Misina sakin")) + "</size>";
			}
			if (reeling)
			{
				if (!FishingRodPreferences.ToggleReel)
				{
					return FishingRodText.Get("release_reel", "RELEASE THE REEL", "SARMA TUŞUNU BIRAK");
				}
				return FishingRodText.Get("stop_reel", "STOP REELING · {0}", "SARMAYI DURDUR · {0}", _reelBinding);
			}
			return "<size=75%>" + FishingRodText.Get("counter_devices", "Mouse / stick / movement keys", "Fare / stick / hareket tuşları") + "</size>";
		}

		private void SetDirectionInstruction(FishingRodFishDirection direction)
		{
			if (direction != FishingRodFishDirection.Left && direction != FishingRodFishDirection.Right && direction != FishingRodFishDirection.Forward)
			{
				HideDirectionInstruction();
				return;
			}
			if (_directionText == null && _counterText != null)
			{
				GameObject gameObject = new GameObject("PullDirectionText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
				gameObject.layer = _counterText.gameObject.layer;
				_directionText = gameObject.GetComponent<TextMeshProUGUI>();
				RectTransform rectTransform = _directionText.rectTransform;
				rectTransform.SetParent(_counterRect.parent, worldPositionStays: false);
				rectTransform.anchorMin = _counterRect.anchorMin;
				rectTransform.anchorMax = _counterRect.anchorMax;
				rectTransform.pivot = _counterRect.pivot;
				rectTransform.sizeDelta = new Vector2(_counterRect.sizeDelta.x, 50f);
				rectTransform.anchoredPosition = _counterBasePosition + Vector2.up * 65f;
				rectTransform.localScale = _counterBaseScale;
				_directionText.font = _counterText.font;
				_directionText.fontSharedMaterial = _counterText.fontSharedMaterial;
				_directionText.fontSize = _counterText.fontSize;
				_directionText.fontStyle = _counterText.fontStyle;
				_directionText.alignment = TextAlignmentOptions.Center;
				_directionText.raycastTarget = false;
				_directionText.color = _counterAccentColor;
			}
			if (!(_directionText == null))
			{
				TextMeshProUGUI directionText = _directionText;
				directionText.text = direction switch
				{
					FishingRodFishDirection.Left => FishingRodText.Get("counter_right", "PULL RIGHT >", "SAĞA ÇEK >"), 
					FishingRodFishDirection.Right => FishingRodText.Get("counter_left", "< PULL LEFT", "< SOLA ÇEK"), 
					_ => FishingRodText.Get("counter_back", "PULL BACK", "GERİ ÇEK"), 
				};
				_directionText.gameObject.SetActive(value: true);
			}
		}

		private void HideDirectionInstruction()
		{
			if (_directionText != null)
			{
				_directionText.gameObject.SetActive(value: false);
			}
		}

		public void CounterSucceeded()
		{
			_counterSuccessUntil = Time.unscaledTime + 0.4f;
		}

		public void SetCancelProgress(float value)
		{
			_cancelProgress = Mathf.Clamp01(value);
		}

		public void ShowCatch(string name, float weight, bool ready, bool collecting, string binding)
		{
			HideDirectionInstruction();
			KillCounterTweens(resetTransform: true);
			SetLegacyVisible(visible: false);
			SetChallengeVisible(visible: true);
			SetMeterVisibility(visible: false);
			if (_feedbackRing != null)
			{
				_feedbackRing.gameObject.SetActive(value: false);
			}
			string arg = (collecting ? FishingRodText.Get("collecting", "Collecting…", "Toplanıyor…") : (ready ? FishingRodText.Get("collect", "Collect · {0}", "Topla · {0}", binding) : FishingRodText.Get("landing", "Landing the fish…", "Balık alınıyor…")));
			SetCounterImmediate($"<b>{name}</b> · {weight:0.00} kg\n<size=65%>{arg}</size>");
		}

		private void SetMeterVisibility(bool visible)
		{
			SetMeterRootVisible(_progressFill, visible);
			SetMeterRootVisible(_tensionFill, visible);
			if (_progressValue != null)
			{
				_progressValue.gameObject.SetActive(visible);
			}
			if (_tensionValue != null)
			{
				_tensionValue.gameObject.SetActive(visible);
			}
		}

		private void SetMeterRootVisible(Image fill, bool visible)
		{
			if (!(fill == null))
			{
				Transform parent = fill.transform.parent;
				if (parent != null && parent.gameObject != _challengeHudRoot && (_counterText == null || !_counterText.transform.IsChildOf(parent)))
				{
					parent.gameObject.SetActive(visible);
				}
				else
				{
					fill.gameObject.SetActive(visible);
				}
			}
		}

		private void EnsureFeedbackRing()
		{
			if (!(_feedbackRing != null))
			{
				GameObject gameObject = new GameObject("CounterProgress", typeof(RectTransform), typeof(CanvasRenderer), typeof(FishingProgressRing));
				RectTransform component = gameObject.GetComponent<RectTransform>();
				gameObject.layer = ((_counterText != null) ? _counterText.gameObject.layer : base.gameObject.layer);
				component.SetParent((_counterRect != null) ? _counterRect.parent : base.transform, worldPositionStays: false);
				component.anchorMin = ((_counterRect != null) ? _counterRect.anchorMin : new Vector2(0.5f, 0.5f));
				component.anchorMax = ((_counterRect != null) ? _counterRect.anchorMax : new Vector2(0.5f, 0.5f));
				component.anchoredPosition = _counterBasePosition + new Vector2(-190f, 65f);
				component.sizeDelta = new Vector2(32f, 32f);
				_feedbackRing = gameObject.GetComponent<FishingProgressRing>();
				_feedbackRing.raycastTarget = false;
			}
		}

		private void ResetVisuals()
		{
			KillTweens();
			HideDirectionInstruction();
			PrepareArrowPivot();
			SetCastCharge(0f);
			SetCastZoneFeedback(string.Empty);
			SetCastZoneVisible(visible: false);
			if (_progressFill != null)
			{
				_progressFill.fillAmount = 0f;
			}
			if (_tensionFill != null)
			{
				_tensionFill.fillAmount = 0f;
				_tensionFill.color = _tensionBaseColor;
			}
			if (_progressValue != null)
			{
				_progressValue.text = "0%";
			}
			if (_tensionValue != null)
			{
				_tensionValue.text = "0%";
			}
			_currentCounterTension = (_cancelProgress = (_progressTarget = (_tensionTarget = (_lastTension = 0f))));
			_counterSuccessUntil = 0f;
			_trendUntil = 0f;
			_tensionTrend = "";
			if (_feedbackRing != null)
			{
				_feedbackRing.gameObject.SetActive(value: false);
			}
			SetCounterImmediate(string.Empty);
		}

		private void KillTweens()
		{
			_progressFill?.DOKill();
			_tensionFill?.DOKill();
			KillCounterTweens(resetTransform: true);
		}

		private void ConfigureCastScale(FishingRodData data, FishingRodLevelData levelData)
		{
			if (data == null || levelData == null || _zoneAreas == null || _zoneAreas.Length != CastZones.Length)
			{
				return;
			}
			float num = Mathf.Max(0f, data.MinimumCastDistance);
			float num2 = Mathf.Max(num, data.GetMaximumCastDistance(levelData));
			if (num2 - num <= Mathf.Epsilon)
			{
				return;
			}
			float num3 = num;
			for (int i = 0; i < CastZones.Length; i++)
			{
				RectTransform rectTransform = _zoneAreas[i];
				if (!(rectTransform == null))
				{
					float num4 = Mathf.Min(num2, data.GetZoneMaximumDistance(CastZones[i]));
					bool flag = num4 > num3;
					rectTransform.gameObject.SetActive(flag);
					if (flag)
					{
						SetZoneAreaBounds(rectTransform, Mathf.InverseLerp(num, num2, num3), Mathf.InverseLerp(num, num2, num4));
					}
					num3 = Mathf.Max(num3, num4);
				}
			}
		}

		private void PrepareArrowPivot()
		{
			if (!(_arrowPivot == null))
			{
				_arrowPivot.anchorMin = new Vector2(1f, 0.5f);
				_arrowPivot.anchorMax = new Vector2(1f, 0.5f);
				Vector2 sizeDelta = _arrowPivot.sizeDelta;
				sizeDelta.y = 0f;
				_arrowPivot.sizeDelta = sizeDelta;
			}
		}

		private static void SetZoneAreaBounds(RectTransform zoneArea, float minimum, float maximum)
		{
			zoneArea.anchorMin = new Vector2(0f, minimum);
			zoneArea.anchorMax = new Vector2(1f, maximum);
			zoneArea.anchoredPosition = Vector2.zero;
			Vector2 sizeDelta = zoneArea.sizeDelta;
			sizeDelta.y = 0f;
			zoneArea.sizeDelta = sizeDelta;
		}

		private void SetLegacyVisible(bool visible)
		{
			if (_visualObject != null)
			{
				_visualObject.SetActive(visible);
			}
		}

		private void SetCastZoneVisible(bool visible)
		{
			if (_castZoneText != null)
			{
				_castZoneText.gameObject.SetActive(visible);
			}
		}

		private void SetChallengeVisible(bool visible)
		{
			if (!visible)
			{
				KillCounterTweens(resetTransform: true);
			}
			if (_challengeHudRoot != null)
			{
				_challengeHudRoot.SetActive(visible);
			}
		}

		private void CacheCounterDefaults()
		{
			if (!(_counterText == null))
			{
				_counterRect = _counterText.rectTransform;
				_counterBasePosition = _counterRect.anchoredPosition;
				_counterBaseScale = _counterRect.localScale;
				_counterBaseColor = _counterText.color;
				_tensionBaseColor = ((_tensionFill != null) ? _tensionFill.color : Color.white);
			}
		}

		private void SetCounter(string value, FishingRodFishDirection direction, bool reeling)
		{
			if (!(_counterText == null))
			{
				if (value == null)
				{
					value = string.Empty;
				}
				bool num = _currentCounterText != value || _currentCounterDirection != direction || _currentCounterReeling != reeling;
				_counterText.text = value;
				if (num)
				{
					_currentCounterText = value;
					_currentCounterDirection = direction;
					_currentCounterReeling = reeling;
					PlayCounterTransition(direction, reeling);
				}
			}
		}

		private void SetCounterImmediate(string value)
		{
			_currentCounterText = value ?? string.Empty;
			_currentCounterDirection = FishingRodFishDirection.Centered;
			_currentCounterReeling = false;
			if (!(_counterText == null))
			{
				_counterText.text = _currentCounterText;
				_counterText.color = _counterBaseColor;
				_counterText.alpha = 1f;
			}
		}

		private void PlayCounterTransition(FishingRodFishDirection direction, bool reeling)
		{
			if (!(_counterText == null) && !(_counterRect == null))
			{
				KillCounterTweens(resetTransform: true);
				_counterText.alpha = 1f;
				if (!FishingRodPreferences.ReducedMotion)
				{
					StartCounterReelPulse(direction, reeling);
				}
			}
		}

		private void StartCounterReelPulse(FishingRodFishDirection direction, bool reeling)
		{
			_counterPulseTween?.Kill();
			_counterPulseTween = null;
			if (reeling && (direction == FishingRodFishDirection.Centered || direction == FishingRodFishDirection.Tired) && !(_counterRect == null))
			{
				_counterPulseTween = _counterRect.DOScale(_counterBaseScale * (1f + _counterReelPulseScale), 0.32f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
			}
		}

		private void UpdateCounterTensionFeeling(float lineTension)
		{
			_currentCounterTension = Mathf.Clamp01(lineTension);
			float num = Mathf.Min(_counterWarningTension, _counterDangerTension);
			float b = Mathf.Max(_counterWarningTension, _counterDangerTension);
			Color color = ((_currentCounterTension < num) ? Color.Lerp(_counterBaseColor, _counterWarningColor, Mathf.InverseLerp(0f, num, _currentCounterTension)) : Color.Lerp(_counterWarningColor, _counterDangerColor, Mathf.InverseLerp(num, b, _currentCounterTension)));
			if (_counterText != null)
			{
				_counterText.color = color;
			}
			if (_tensionFill != null)
			{
				_tensionFill.color = Color.Lerp(_tensionBaseColor, color, 0.85f);
			}
			RefreshCounterTensionShake();
		}

		private void RefreshCounterTensionShake()
		{
			if (!(_tensionFill == null) && !FishingRodPreferences.ReducedMotion)
			{
				Color color = _tensionFill.color;
				color.a = ((_currentCounterTension >= _counterDangerTension) ? Mathf.Lerp(0.7f, 1f, (Mathf.Sin(Time.unscaledTime * 8f) + 1f) * 0.5f) : 1f);
				_tensionFill.color = color;
			}
		}

		private void PlayCounterFailure()
		{
			if (!(_counterText == null) && !(_counterRect == null))
			{
				KillCounterTweens(resetTransform: true);
				if (_feedbackRing != null)
				{
					_feedbackRing.gameObject.SetActive(value: false);
				}
				_currentCounterText = LocalizationDatabase.GetString("UiTable", "gameplay.fishing.line_broke", "LINE BROKE");
				_currentCounterDirection = FishingRodFishDirection.Centered;
				_currentCounterReeling = false;
				_counterText.text = "<b>" + _currentCounterText + "</b>";
				_counterText.color = _counterDangerColor;
				_counterText.alpha = 1f;
				if (!FishingRodPreferences.ReducedMotion)
				{
					_counterRect.localScale = _counterBaseScale * 0.65f;
					_counterSequence = DOTween.Sequence();
					_counterSequence.Join(_counterText.DOFade(1f, 0.08f)).Join(_counterRect.DOScale(_counterBaseScale * 1.22f, 0.16f).SetEase(Ease.OutBack)).Append(_counterRect.DOShakeAnchorPos(0.32f, 7f, 18))
						.AppendInterval(0.35f)
						.Append(_counterRect.DOAnchorPos(_counterBasePosition + Vector2.down * 40f, 0.28f).SetEase(Ease.InQuad))
						.Join(_counterText.DOFade(0f, 0.24f));
				}
			}
		}

		private void KillCounterTweens(bool resetTransform)
		{
			_counterSequence?.Kill();
			_counterPulseTween?.Kill();
			_counterShakeTween?.Kill();
			_counterSequence = null;
			_counterPulseTween = null;
			_counterShakeTween = null;
			_counterTensionBand = -1;
			if (resetTransform && !(_counterRect == null))
			{
				_counterRect.anchoredPosition = _counterBasePosition;
				_counterRect.localScale = _counterBaseScale;
				if (_counterText != null)
				{
					_counterText.alpha = 1f;
				}
			}
		}

		private void SetFill(Image image, float value)
		{
			if (!(image == null))
			{
				if (image == _progressFill)
				{
					_progressTarget = Mathf.Clamp01(value);
				}
				if (image == _tensionFill)
				{
					_tensionTarget = Mathf.Clamp01(value);
				}
			}
		}

		private void Update()
		{
			float t = ((_fillTweenDuration <= 0f) ? 1f : (1f - Mathf.Exp((0f - Time.unscaledDeltaTime) * 5f / _fillTweenDuration)));
			if (_progressFill != null)
			{
				_progressFill.fillAmount = Mathf.Lerp(_progressFill.fillAmount, _progressTarget, t);
			}
			if (_tensionFill != null)
			{
				_tensionFill.fillAmount = Mathf.Lerp(_tensionFill.fillAmount, _tensionTarget, t);
			}
		}
	}
}
