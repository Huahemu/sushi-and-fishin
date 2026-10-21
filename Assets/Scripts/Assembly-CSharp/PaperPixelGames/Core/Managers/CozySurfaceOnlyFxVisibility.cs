using System.Collections;
using System.Collections.Generic;
using DistantLands.Cozy;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace PaperPixelGames.Core.Managers
{
	[DisallowMultipleComponent]
	public sealed class CozySurfaceOnlyFxVisibility : MonoBehaviour
	{
		[SerializeField]
		private CozyWeather _cozyWeather;

		[Tooltip("Renderers that should only be visible above water.")]
		[SerializeField]
		private Renderer[] _surfaceOnlyRenderers;

		[SerializeField]
		private string _runtimeParticleFxName = "Meteor Shower Particle FX";

		[SerializeField]
		[Min(0.1f)]
		private float _runtimeSearchIntervalSeconds = 0.25f;

		private readonly List<Renderer> _registeredRenderers = new List<Renderer>();

		private readonly Dictionary<Renderer, bool> _originalForceRenderingOff = new Dictionary<Renderer, bool>();

		private EventBinding<LocalPlayerReadyEvent> _localPlayerReadyBinding;

		private PPG_Player _localPlayer;

		private Renderer _runtimeParticleRenderer;

		private Coroutine _runtimeSearchCoroutine;

		private bool _isUnderwater;

		private void Awake()
		{
			if (_cozyWeather == null)
			{
				_cozyWeather = GetComponent<CozyWeather>();
			}
		}

		private void OnEnable()
		{
			if (Application.isPlaying)
			{
				RegisterSerializedRenderers();
				_localPlayerReadyBinding = new EventBinding<LocalPlayerReadyEvent>(HandleLocalPlayerReady);
				EventBus<LocalPlayerReadyEvent>.Register(_localPlayerReadyBinding);
				BindLocalPlayer((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null);
				_runtimeSearchCoroutine = StartCoroutine(RegisterRuntimeParticleRendererWhenAvailable());
			}
		}

		private void OnDisable()
		{
			if (_localPlayerReadyBinding != null)
			{
				EventBus<LocalPlayerReadyEvent>.Deregister(_localPlayerReadyBinding);
			}
			if (_runtimeSearchCoroutine != null)
			{
				StopCoroutine(_runtimeSearchCoroutine);
			}
			BindLocalPlayer(null);
			RestoreRendererStates();
			_localPlayerReadyBinding = null;
			_runtimeSearchCoroutine = null;
			_runtimeParticleRenderer = null;
		}

		private void HandleLocalPlayerReady(LocalPlayerReadyEvent readyEvent)
		{
			BindLocalPlayer(readyEvent.LocalPlayer);
		}

		private void BindLocalPlayer(PPG_Player player)
		{
			if (!(_localPlayer == player))
			{
				if (_localPlayer != null)
				{
					_localPlayer.UnderwaterStateChanged -= HandleUnderwaterStateChanged;
				}
				_localPlayer = player;
				if (_localPlayer != null)
				{
					_localPlayer.UnderwaterStateChanged += HandleUnderwaterStateChanged;
				}
				SetUnderwaterHidden(_localPlayer != null && _localPlayer.IsUnderWater);
			}
		}

		private void HandleUnderwaterStateChanged(bool isUnderwater)
		{
			SetUnderwaterHidden(isUnderwater);
		}

		private void RegisterSerializedRenderers()
		{
			if (_surfaceOnlyRenderers != null)
			{
				for (int i = 0; i < _surfaceOnlyRenderers.Length; i++)
				{
					RegisterRenderer(_surfaceOnlyRenderers[i]);
				}
			}
		}

		private IEnumerator RegisterRuntimeParticleRendererWhenAvailable()
		{
			WaitForSecondsRealtime wait = new WaitForSecondsRealtime(_runtimeSearchIntervalSeconds);
			while (_runtimeParticleRenderer == null)
			{
				if (_cozyWeather != null && _cozyWeather.particleFXParent != null && !string.IsNullOrEmpty(_runtimeParticleFxName))
				{
					Transform transform = _cozyWeather.particleFXParent.Find(_runtimeParticleFxName);
					if (transform != null)
					{
						_runtimeParticleRenderer = transform.GetComponent<Renderer>();
						RegisterRenderer(_runtimeParticleRenderer);
						if (_runtimeParticleRenderer != null)
						{
							break;
						}
					}
				}
				yield return wait;
			}
		}

		private void RegisterRenderer(Renderer targetRenderer)
		{
			if (!(targetRenderer == null) && !_originalForceRenderingOff.ContainsKey(targetRenderer))
			{
				_registeredRenderers.Add(targetRenderer);
				_originalForceRenderingOff.Add(targetRenderer, targetRenderer.forceRenderingOff);
				ApplyRendererState(targetRenderer);
			}
		}

		private void SetUnderwaterHidden(bool isUnderwater)
		{
			_isUnderwater = isUnderwater;
			for (int i = 0; i < _registeredRenderers.Count; i++)
			{
				ApplyRendererState(_registeredRenderers[i]);
			}
		}

		private void ApplyRendererState(Renderer targetRenderer)
		{
			if (!(targetRenderer == null) && _originalForceRenderingOff.TryGetValue(targetRenderer, out var value))
			{
				targetRenderer.forceRenderingOff = _isUnderwater | value;
			}
		}

		private void RestoreRendererStates()
		{
			for (int i = 0; i < _registeredRenderers.Count; i++)
			{
				Renderer renderer = _registeredRenderers[i];
				if (renderer != null && _originalForceRenderingOff.TryGetValue(renderer, out var value))
				{
					renderer.forceRenderingOff = value;
				}
			}
			_registeredRenderers.Clear();
			_originalForceRenderingOff.Clear();
		}
	}
}
