using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace HoaxGames
{
	public class EffectsPlayer : MonoBehaviour
	{
		public enum PlayDirection
		{
			RANDOM = 0,
			FORWARD = 1,
			BACKWARD = 2
		}

		public enum PlayAtTarget
		{
			LEFT_FOOT = 0,
			RIGHT_FOOT = 1,
			LEFT_HAND = 2,
			RIGHT_HAND = 3,
			THIS_TRANSFORM_TARGET = 4,
			CUSTOM_TRANSFORM_TARGET = 5,
			LEFT_FOOT_RESULT = 6,
			RIGHT_FOOT_RESULT = 7
		}

		[Serializable]
		public abstract class EffectsBase
		{
			public enum InstantiateState
			{
				UPDATE_ALIGNMENT = 0,
				INSTANTIATE_AND_FORGET = 1
			}

			public enum CheckLayerAndTagRule
			{
				LAYER_AND_TAG = 0,
				LAYER_OR_TAG = 1,
				LAYER_ONLY = 2,
				TAG_ONLY = 3,
				NONE = 4
			}

			[SerializeField]
			protected bool m_isActiveForPlay = true;

			[SerializeField]
			protected PlayAtTarget m_playAtTarget = PlayAtTarget.THIS_TRANSFORM_TARGET;

			[SerializeField]
			protected Transform m_optionalCustomPlayAtTarget;

			[SerializeField]
			protected Vector3 m_relativePlayAtPositionOffset;

			[SerializeField]
			protected Vector3 m_relativePlayAtRotationOffset;

			[SerializeField]
			protected float m_playDelay;

			[SerializeField]
			protected InstantiateState m_instantiateState;

			[SerializeField]
			protected LayerMask m_optionalLayerMask = -1;

			[SerializeField]
			protected string m_optionalTriggerTag = "Untagged";

			[SerializeField]
			[Tooltip("If set to LAYER_AND_TAG, both, the layer and the tag have to match. If set to LAYER_OR_TAG, one of the two has to match. If set to NONE, none of the two has to match.")]
			protected CheckLayerAndTagRule m_checkLayerAndTagRule;

			protected GameObject m_sourceGO;

			protected Transform m_sourceTrans;

			protected Transform m_leftFootTrans;

			protected Transform m_rightFootTrans;

			protected Transform m_leftHandTrans;

			protected Transform m_rightHandTrans;

			protected Transform m_defaultSourceParent;

			protected string m_defaultName;

			protected IKResult m_ikResult;

			public float playDelay => m_playDelay;

			public void ChangeActiveForPlay(bool status)
			{
				m_isActiveForPlay = status;
			}

			public virtual void init(string name, Transform parent)
			{
				initBehaviourBase(name, parent);
			}

			protected void initBehaviourBase(string name, Transform parent)
			{
				m_sourceGO = new GameObject(name);
				m_sourceTrans = m_sourceGO.transform;
				m_sourceTrans.position = parent.position;
				m_sourceTrans.rotation = parent.rotation;
				if (m_instantiateState == InstantiateState.UPDATE_ALIGNMENT)
				{
					m_sourceTrans.SetParent(parent);
				}
				m_defaultName = name;
				m_defaultSourceParent = parent;
				Animator componentInBranch = getComponentInBranch<Animator>(parent);
				m_leftFootTrans = componentInBranch.GetBoneTransform(HumanBodyBones.LeftFoot);
				m_rightFootTrans = componentInBranch.GetBoneTransform(HumanBodyBones.RightFoot);
				m_leftHandTrans = componentInBranch.GetBoneTransform(HumanBodyBones.LeftHand);
				m_rightHandTrans = componentInBranch.GetBoneTransform(HumanBodyBones.RightHand);
				updateBehaviourBase(force: true);
			}

			private T getComponentInBranch<T>(Transform target)
			{
				T componentInChildren = target.GetComponentInChildren<T>();
				if (componentInChildren != null)
				{
					return componentInChildren;
				}
				componentInChildren = target.GetComponentInParent<T>();
				if (componentInChildren != null)
				{
					return componentInChildren;
				}
				while (target.parent != null)
				{
					target = target.parent;
					componentInChildren = target.GetComponentInChildren<T>();
					if (componentInChildren != null)
					{
						return componentInChildren;
					}
				}
				return target.GetComponentInChildren<T>();
			}

			public virtual void update()
			{
				updateBehaviourBase();
			}

			protected void updateBehaviourBase(bool force = false)
			{
				if (!force && m_instantiateState == InstantiateState.INSTANTIATE_AND_FORGET)
				{
					return;
				}
				switch (m_playAtTarget)
				{
				case PlayAtTarget.CUSTOM_TRANSFORM_TARGET:
					if (m_optionalCustomPlayAtTarget != null)
					{
						m_sourceTrans.rotation = m_optionalCustomPlayAtTarget.rotation * Quaternion.Euler(m_relativePlayAtRotationOffset);
						m_sourceTrans.position = m_optionalCustomPlayAtTarget.position + m_sourceTrans.rotation * m_relativePlayAtPositionOffset;
					}
					break;
				case PlayAtTarget.LEFT_FOOT:
					if (m_leftFootTrans != null)
					{
						m_sourceTrans.rotation = m_leftFootTrans.rotation * Quaternion.Euler(m_relativePlayAtRotationOffset);
						m_sourceTrans.position = m_leftFootTrans.position + m_sourceTrans.rotation * m_relativePlayAtPositionOffset;
					}
					break;
				case PlayAtTarget.RIGHT_FOOT:
					if (m_rightFootTrans != null)
					{
						m_sourceTrans.rotation = m_rightFootTrans.rotation * Quaternion.Euler(m_relativePlayAtRotationOffset);
						m_sourceTrans.position = m_rightFootTrans.position + m_sourceTrans.rotation * m_relativePlayAtPositionOffset;
					}
					break;
				case PlayAtTarget.LEFT_HAND:
					if (m_leftHandTrans != null)
					{
						m_sourceTrans.rotation = m_leftHandTrans.rotation * Quaternion.Euler(m_relativePlayAtRotationOffset);
						m_sourceTrans.position = m_leftHandTrans.position + m_sourceTrans.rotation * m_relativePlayAtPositionOffset;
					}
					break;
				case PlayAtTarget.RIGHT_HAND:
					if (m_rightHandTrans != null)
					{
						m_sourceTrans.rotation = m_rightHandTrans.rotation * Quaternion.Euler(m_relativePlayAtRotationOffset);
						m_sourceTrans.position = m_rightHandTrans.position + m_sourceTrans.rotation * m_relativePlayAtPositionOffset;
					}
					break;
				case PlayAtTarget.LEFT_FOOT_RESULT:
					if (m_ikResult != null)
					{
						if (m_ikResult.primaryHitTransform != null)
						{
							m_sourceTrans.rotation = Quaternion.LookRotation(m_ikResult.normal, m_leftFootTrans.forward) * Quaternion.Euler(m_relativePlayAtRotationOffset);
						}
						else
						{
							m_sourceTrans.rotation = m_leftFootTrans.rotation * Quaternion.Euler(m_relativePlayAtRotationOffset);
						}
						m_sourceTrans.position = m_ikResult.surfacePoint + m_sourceTrans.rotation * m_relativePlayAtPositionOffset;
					}
					break;
				case PlayAtTarget.RIGHT_FOOT_RESULT:
					if (m_ikResult != null)
					{
						if (m_ikResult.primaryHitTransform != null)
						{
							m_sourceTrans.rotation = Quaternion.LookRotation(m_ikResult.normal, m_rightFootTrans.forward) * Quaternion.Euler(m_relativePlayAtRotationOffset);
						}
						else
						{
							m_sourceTrans.rotation = m_rightFootTrans.rotation * Quaternion.Euler(m_relativePlayAtRotationOffset);
						}
						m_sourceTrans.position = m_ikResult.surfacePoint + m_sourceTrans.rotation * m_relativePlayAtPositionOffset;
					}
					break;
				case PlayAtTarget.THIS_TRANSFORM_TARGET:
					break;
				}
			}

			public virtual bool play()
			{
				if (!m_isActiveForPlay)
				{
					return false;
				}
				playBehaviourBase();
				return true;
			}

			protected void playBehaviourBase()
			{
				if (m_instantiateState == InstantiateState.INSTANTIATE_AND_FORGET)
				{
					m_sourceTrans = null;
				}
				if (m_sourceTrans == null)
				{
					initBehaviourBase(m_defaultName, m_defaultSourceParent);
				}
				else
				{
					updateBehaviourBase(force: true);
				}
			}

			public virtual void stop()
			{
			}

			public virtual bool play(IKResult result)
			{
				if (!m_isActiveForPlay)
				{
					return false;
				}
				if (result.primaryHitTransform == null)
				{
					return false;
				}
				bool flag = false;
				if (m_checkLayerAndTagRule == CheckLayerAndTagRule.NONE)
				{
					flag = true;
				}
				else if (m_checkLayerAndTagRule == CheckLayerAndTagRule.LAYER_ONLY)
				{
					flag = (int)m_optionalLayerMask == -1 || (int)m_optionalLayerMask == ((int)m_optionalLayerMask | (1 << result.primaryHitTransform.gameObject.layer));
				}
				else if (m_checkLayerAndTagRule == CheckLayerAndTagRule.TAG_ONLY)
				{
					flag = string.IsNullOrWhiteSpace(m_optionalTriggerTag) || result.primaryHitTransform.CompareTag(m_optionalTriggerTag);
				}
				else
				{
					bool flag2 = (int)m_optionalLayerMask == -1 || (int)m_optionalLayerMask == ((int)m_optionalLayerMask | (1 << result.primaryHitTransform.gameObject.layer));
					bool flag3 = string.IsNullOrWhiteSpace(m_optionalTriggerTag) || result.primaryHitTransform.CompareTag(m_optionalTriggerTag);
					flag = ((m_checkLayerAndTagRule != CheckLayerAndTagRule.LAYER_AND_TAG) ? (flag3 | flag2) : (flag3 & flag2));
				}
				if (flag)
				{
					m_ikResult = result;
					playBehaviourBase();
					return true;
				}
				return false;
			}
		}

		[Serializable]
		public class EffectsAudio : EffectsBase
		{
			[SerializeField]
			protected PlayDirection m_playDirection;

			[SerializeField]
			[Range(0f, 1f)]
			[Tooltip("0 == fully 2D sound. 1 == fully 3D sound.")]
			protected float m_2D_3D_Sound_Blend = 1f;

			[SerializeField]
			protected AudioRolloffMode m_audioRolloffMode;

			[SerializeField]
			[Range(0f, 1f)]
			[Tooltip("Configure the audio volume with this variable.")]
			protected float m_audioVolume = 1f;

			[SerializeField]
			protected float m_optional3DBlendStart = 1f;

			[SerializeField]
			protected float m_optional3DBlendEnd = 100f;

			[SerializeField]
			protected List<AudioClip> m_audioClips = new List<AudioClip>();

			[SerializeField]
			protected AudioMixerGroup m_optionalAudioMixerGroup;

			protected int m_currentPlayIdx;

			protected AudioSource m_audioSource;

			public EffectsAudio()
			{
				m_optional3DBlendEnd = 100f;
				m_audioVolume = 1f;
			}

			public override void init(string name, Transform parent)
			{
				m_instantiateState = InstantiateState.UPDATE_ALIGNMENT;
				base.init(name, parent);
				m_audioSource = m_sourceGO.AddComponent<AudioSource>();
				m_audioSource.playOnAwake = false;
				m_audioSource.outputAudioMixerGroup = m_optionalAudioMixerGroup;
			}

			public override bool play()
			{
				m_instantiateState = InstantiateState.UPDATE_ALIGNMENT;
				if (!base.play())
				{
					return false;
				}
				playBehaviour();
				return true;
			}

			protected virtual void playBehaviour()
			{
				if (m_audioClips.Count < 1)
				{
					return;
				}
				switch (m_playDirection)
				{
				case PlayDirection.RANDOM:
					m_currentPlayIdx = UnityEngine.Random.Range(0, m_audioClips.Count);
					break;
				case PlayDirection.FORWARD:
					m_currentPlayIdx++;
					if (m_currentPlayIdx >= m_audioClips.Count)
					{
						m_currentPlayIdx = 0;
					}
					break;
				case PlayDirection.BACKWARD:
					m_currentPlayIdx--;
					if (m_currentPlayIdx < 0)
					{
						m_currentPlayIdx = m_audioClips.Count - 1;
					}
					break;
				}
				m_audioSource.volume = m_audioVolume;
				m_audioSource.spatialBlend = m_2D_3D_Sound_Blend;
				m_audioSource.rolloffMode = m_audioRolloffMode;
				m_audioSource.minDistance = m_optional3DBlendStart;
				m_audioSource.maxDistance = m_optional3DBlendEnd;
				m_audioSource.clip = m_audioClips[m_currentPlayIdx];
				m_audioSource.PlayOneShot(m_audioSource.clip, 1f);
			}

			public override void stop()
			{
				base.stop();
				stopBehaviour();
			}

			protected virtual void stopBehaviour()
			{
				m_audioSource.Stop();
			}

			public override bool play(IKResult result)
			{
				m_instantiateState = InstantiateState.UPDATE_ALIGNMENT;
				if (base.play(result))
				{
					playBehaviour();
					return true;
				}
				return false;
			}
		}

		[Serializable]
		public class EffectsInstance : EffectsBase
		{
			[SerializeField]
			protected bool m_doCacheWithEnableDisableBehaviour = true;

			[SerializeField]
			protected GameObject m_prefab;

			protected GameObject m_instance;

			public override void init(string name, Transform parent)
			{
				base.init(name, parent);
				if (m_instantiateState == InstantiateState.INSTANTIATE_AND_FORGET && m_sourceGO != null)
				{
					UnityEngine.Object.Destroy(m_sourceGO);
				}
				else if (m_doCacheWithEnableDisableBehaviour)
				{
					if (m_instance != null)
					{
						UnityEngine.Object.Destroy(m_instance);
					}
					if (m_prefab != null)
					{
						m_instance = UnityEngine.Object.Instantiate(m_prefab);
						m_instance.transform.SetParent(m_sourceTrans);
						m_instance.transform.localPosition = Vector3.zero;
						m_instance.transform.localRotation = Quaternion.identity;
						m_instance.SetActive(value: false);
					}
				}
			}

			public override bool play()
			{
				if (!base.play())
				{
					return false;
				}
				playBehaviour();
				return true;
			}

			protected virtual void playBehaviour()
			{
				if (m_instantiateState == InstantiateState.INSTANTIATE_AND_FORGET)
				{
					m_instance = null;
				}
				if (m_doCacheWithEnableDisableBehaviour)
				{
					if (m_instance == null)
					{
						m_instance = UnityEngine.Object.Instantiate(m_prefab);
					}
					m_instance.transform.SetParent(m_sourceTrans);
					m_instance.transform.localPosition = Vector3.zero;
					m_instance.transform.localRotation = Quaternion.identity;
					m_instance.SetActive(value: true);
				}
				else
				{
					if (m_instance != null)
					{
						UnityEngine.Object.Destroy(m_instance);
					}
					m_instance = UnityEngine.Object.Instantiate(m_prefab);
					m_instance.transform.SetParent(m_sourceTrans);
					m_instance.transform.localPosition = Vector3.zero;
					m_instance.transform.localRotation = Quaternion.identity;
				}
			}

			public override void stop()
			{
				base.stop();
				stopBehaviour();
			}

			protected virtual void stopBehaviour()
			{
				if (m_doCacheWithEnableDisableBehaviour)
				{
					if (m_instance != null)
					{
						m_instance.SetActive(value: false);
					}
				}
				else if (m_instance != null)
				{
					UnityEngine.Object.Destroy(m_instance);
				}
			}

			public override bool play(IKResult result)
			{
				if (base.play(result))
				{
					playBehaviour();
					return true;
				}
				return false;
			}
		}

		[Serializable]
		public class TerrainTextureEvents
		{
			[SerializeField]
			protected List<int> m_filterByTerrainTextureIndexInCaseGroundIsTerrain = new List<int>();

			[SerializeField]
			protected List<EffectsAudio> m_playAudioClipEvents = new List<EffectsAudio>();

			[SerializeField]
			protected List<EffectsInstance> m_playInstanceEvents = new List<EffectsInstance>();

			[SerializeField]
			[HideInInspector]
			protected int m_audioClipEventsCount;

			[SerializeField]
			[HideInInspector]
			protected int m_instanceEventsCount;

			protected Dictionary<Transform, TerrainTextureDetector> m_terrainTextureDetectors = new Dictionary<Transform, TerrainTextureDetector>();

			protected EffectsPlayer m_target;

			public virtual void init(EffectsPlayer target)
			{
				m_target = target;
				Terrain[] array = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
				foreach (Terrain terrain in array)
				{
					TerrainTextureDetector terrainTextureDetector = terrain.GetComponent<TerrainTextureDetector>();
					if (terrainTextureDetector == null)
					{
						terrainTextureDetector = terrain.gameObject.AddComponent<TerrainTextureDetector>();
					}
					m_terrainTextureDetectors.Add(terrain.transform, terrainTextureDetector);
				}
				foreach (EffectsAudio playAudioClipEvent in m_playAudioClipEvents)
				{
					playAudioClipEvent.init("EffectsAudio_Terrain", target.transform);
				}
				foreach (EffectsInstance playInstanceEvent in m_playInstanceEvents)
				{
					playInstanceEvent.init("EffectsInstance_Terrain", target.transform);
				}
			}

			public virtual void update()
			{
				foreach (EffectsAudio playAudioClipEvent in m_playAudioClipEvents)
				{
					playAudioClipEvent.update();
				}
				foreach (EffectsInstance playInstanceEvent in m_playInstanceEvents)
				{
					playInstanceEvent.update();
				}
			}

			public virtual void play()
			{
				foreach (KeyValuePair<Transform, TerrainTextureDetector> terrainTextureDetector in m_terrainTextureDetectors)
				{
					int dominantTextureIndexAt = terrainTextureDetector.Value.GetDominantTextureIndexAt(m_target.transform.position);
					if (dominantTextureIndexAt < 0 || !m_filterByTerrainTextureIndexInCaseGroundIsTerrain.Contains(dominantTextureIndexAt))
					{
						continue;
					}
					foreach (EffectsAudio playAudioClipEvent in m_playAudioClipEvents)
					{
						m_target.StartCoroutine(m_target.playDelayedEffects(playAudioClipEvent));
					}
					{
						foreach (EffectsInstance playInstanceEvent in m_playInstanceEvents)
						{
							m_target.StartCoroutine(m_target.playDelayedEffects(playInstanceEvent));
						}
						break;
					}
				}
			}

			public virtual void play(IKResult result)
			{
				if (!m_terrainTextureDetectors.ContainsKey(result.primaryHitTransform))
				{
					return;
				}
				TerrainTextureDetector terrainTextureDetector = m_terrainTextureDetectors[result.primaryHitTransform];
				if (!m_filterByTerrainTextureIndexInCaseGroundIsTerrain.Contains(terrainTextureDetector.GetDominantTextureIndexAt(result.surfacePoint)))
				{
					return;
				}
				foreach (EffectsAudio playAudioClipEvent in m_playAudioClipEvents)
				{
					m_target.StartCoroutine(m_target.playDelayedEffects(playAudioClipEvent, result));
				}
				foreach (EffectsInstance playInstanceEvent in m_playInstanceEvents)
				{
					m_target.StartCoroutine(m_target.playDelayedEffects(playInstanceEvent, result));
				}
			}

			public virtual void stop()
			{
				foreach (EffectsAudio playAudioClipEvent in m_playAudioClipEvents)
				{
					playAudioClipEvent.stop();
				}
				foreach (EffectsInstance playInstanceEvent in m_playInstanceEvents)
				{
					playInstanceEvent.stop();
				}
			}

			public virtual void onValidate()
			{
				if (m_playAudioClipEvents.Count > m_audioClipEventsCount)
				{
					m_playAudioClipEvents[m_playAudioClipEvents.Count - 1] = new EffectsAudio();
				}
				if (m_playInstanceEvents.Count > m_instanceEventsCount)
				{
					m_playInstanceEvents[m_playInstanceEvents.Count - 1] = new EffectsInstance();
				}
				m_audioClipEventsCount = m_playAudioClipEvents.Count;
				m_instanceEventsCount = m_playInstanceEvents.Count;
			}
		}

		[SerializeField]
		protected List<EffectsAudio> m_playAudioClipEvents = new List<EffectsAudio>();

		[SerializeField]
		protected List<EffectsInstance> m_playInstanceEvents = new List<EffectsInstance>();

		[SerializeField]
		[Tooltip("Can only be used when using the play(IKResult) callback (does not work with play() callback)")]
		protected List<TerrainTextureEvents> m_playTerrainTextureEvents = new List<TerrainTextureEvents>();

		[SerializeField]
		[HideInInspector]
		protected int m_audioClipEventsCount;

		[SerializeField]
		[HideInInspector]
		protected int m_instanceEventsCount;

		[SerializeField]
		[HideInInspector]
		protected int m_terrainTextureEventsCount;

		protected GameObject m_gameObject;

		public List<EffectsAudio> AudioClips => m_playAudioClipEvents;

		protected virtual void Awake()
		{
			m_gameObject = base.gameObject;
			foreach (EffectsAudio playAudioClipEvent in m_playAudioClipEvents)
			{
				playAudioClipEvent.init("EffectsAudio", base.transform);
			}
			foreach (EffectsInstance playInstanceEvent in m_playInstanceEvents)
			{
				playInstanceEvent.init("EffectsInstance", base.transform);
			}
			foreach (TerrainTextureEvents playTerrainTextureEvent in m_playTerrainTextureEvents)
			{
				playTerrainTextureEvent.init(this);
			}
		}

		protected virtual void Start()
		{
		}

		protected virtual void LateUpdate()
		{
			if (m_gameObject == null || !m_gameObject.activeInHierarchy || !base.enabled)
			{
				return;
			}
			foreach (EffectsAudio playAudioClipEvent in m_playAudioClipEvents)
			{
				playAudioClipEvent.update();
			}
			foreach (EffectsInstance playInstanceEvent in m_playInstanceEvents)
			{
				playInstanceEvent.update();
			}
			foreach (TerrainTextureEvents playTerrainTextureEvent in m_playTerrainTextureEvents)
			{
				playTerrainTextureEvent.update();
			}
		}

		public virtual void play()
		{
			if (m_gameObject == null || !m_gameObject.activeInHierarchy || !base.enabled)
			{
				return;
			}
			foreach (EffectsAudio playAudioClipEvent in m_playAudioClipEvents)
			{
				StartCoroutine(playDelayedEffects(playAudioClipEvent));
			}
			foreach (EffectsInstance playInstanceEvent in m_playInstanceEvents)
			{
				StartCoroutine(playDelayedEffects(playInstanceEvent));
			}
			foreach (TerrainTextureEvents playTerrainTextureEvent in m_playTerrainTextureEvents)
			{
				playTerrainTextureEvent.play();
			}
		}

		protected virtual IEnumerator playDelayedEffects(EffectsAudio fx)
		{
			if (fx.playDelay > 0.01f)
			{
				yield return new WaitForSeconds(fx.playDelay);
			}
			fx.play();
		}

		protected virtual IEnumerator playDelayedEffects(EffectsInstance fx)
		{
			if (fx.playDelay > 0.01f)
			{
				yield return new WaitForSeconds(fx.playDelay);
			}
			fx.play();
		}

		public virtual void stop()
		{
			if (m_gameObject == null || !m_gameObject.activeInHierarchy || !base.enabled)
			{
				return;
			}
			foreach (EffectsAudio playAudioClipEvent in m_playAudioClipEvents)
			{
				playAudioClipEvent.stop();
			}
			foreach (EffectsInstance playInstanceEvent in m_playInstanceEvents)
			{
				playInstanceEvent.stop();
			}
			foreach (TerrainTextureEvents playTerrainTextureEvent in m_playTerrainTextureEvents)
			{
				playTerrainTextureEvent.stop();
			}
		}

		public virtual void play(IKResult result)
		{
			if (m_gameObject == null || !m_gameObject.activeInHierarchy || !base.enabled || result.primaryHitTransform == null)
			{
				return;
			}
			foreach (EffectsAudio playAudioClipEvent in m_playAudioClipEvents)
			{
				StartCoroutine(playDelayedEffects(playAudioClipEvent, result));
			}
			foreach (EffectsInstance playInstanceEvent in m_playInstanceEvents)
			{
				StartCoroutine(playDelayedEffects(playInstanceEvent, result));
			}
			foreach (TerrainTextureEvents playTerrainTextureEvent in m_playTerrainTextureEvents)
			{
				playTerrainTextureEvent.play(result);
			}
		}

		public virtual void play(GroundedResult result)
		{
			IKResult result2 = new IKResult(result.groundedPosition, result.groundedPosition, _isGlued: true, result.groundedPosition, result.groundedPosition, result.groundedNormal, result.groundedTransform, result.groundedTransform);
			play(result2);
		}

		protected virtual IEnumerator playDelayedEffects(EffectsAudio fx, IKResult result)
		{
			if (fx.playDelay > 0.01f)
			{
				yield return new WaitForSeconds(fx.playDelay);
			}
			fx.play(result);
		}

		protected virtual IEnumerator playDelayedEffects(EffectsInstance fx, IKResult result)
		{
			if (fx.playDelay > 0.01f)
			{
				yield return new WaitForSeconds(fx.playDelay);
			}
			fx.play(result);
		}
	}
}
