using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoaxGames
{
	public class AddonAdaptToSurfaceSlope : MonoBehaviour
	{
		public enum AlignmentAlgorithm
		{
			SIMPLE_SURFACE_NORMAL = 0,
			COMPLEX_SURFACE_NORMAL = 1
		}

		[Serializable]
		public class CustomGroundDetectionCast
		{
			public Transform targetTransform;

			public float radius;

			public float distance;
		}

		[SerializeField]
		[Tooltip("With this parameter you can choose the prefered algorithm. For most action triggered animations like for example sliding, COMPLEX_SURFACE_NORMAL should give better visual results.")]
		protected AlignmentAlgorithm m_alignmentAlgorithm = AlignmentAlgorithm.COMPLEX_SURFACE_NORMAL;

		[SerializeField]
		[Tooltip("A list of all animator states (in the animator controller), that will force adapt the characters rotation to the surface slope when they are active. Layer == animator layer. Animator state == animator state name.")]
		protected List<FootIK.AnimatorStateEntry> m_forceAdaptRotationToSurfaceSlopeAnimStates = new List<FootIK.AnimatorStateEntry>();

		[SerializeField]
		[Tooltip("The transition speed with which the adaption to the surface slope takes place.")]
		protected float m_adaptRotationToSurfaceSlopeTransitionSpeed = 10f;

		[SerializeField]
		[Tooltip("The transition speed with which the positional correction offset regarding to surface slope takes place.")]
		protected float m_positionOffsetTransitionSpeed = 10f;

		[SerializeField]
		[Tooltip("This offset is additionally applied to the calculated grounded position in regard to the surface slope")]
		protected float m_positionalHeightOffset;

		[SerializeField]
		[Tooltip("If true surface rotation is calculated every frame. If false surface rotation is calculated only on every animation entry-event.")]
		protected bool m_updateTargetRotationContinously = true;

		[SerializeField]
		[Tooltip("If true surface rotation is always applied. Default is false so that this addon can be triggered by the above defined animation states.")]
		protected bool m_forceAdaptRotationToSurfaceSlope;

		[SerializeField]
		[Tooltip("Use this and the second custom ground detection cast target (both have to be assigned!) to compute a more accurate normal. This is useful for characters that crawl on the ground.")]
		private CustomGroundDetectionCast m_customGroundDetectionCastTargetOne;

		[SerializeField]
		[Tooltip("Use this and the second custom ground detection cast target (both have to be assigned!) to compute a more accurate normal. This is useful for characters that crawl on the ground.")]
		private CustomGroundDetectionCast m_customGroundDetectionCastTargetTwo;

		[SerializeField]
		[Tooltip("(Optional) Use this as an additional detection point (optional, but if configured, it requires both custom ground detection targets to be assigned in order to be active) to optimize triangulation for computing a better normal.")]
		private CustomGroundDetectionCast m_overrideMainGroundDetectionCastTarget;

		protected Quaternion m_surfaceDeltaRot = Quaternion.identity;

		protected List<KeyValuePair<int, int>> m_adaptRotationToSurfaceSlopeAnimStates = new List<KeyValuePair<int, int>>();

		protected Transform m_transform;

		protected Animator m_animator;

		protected FootIK m_footIK;

		protected bool m_prevValidationState;

		protected Quaternion m_targetRotation = Quaternion.identity;

		protected bool m_prevForceAdaptRotation;

		protected Vector3 m_currOffsetVec = Vector3.zero;

		protected Vector3[] m_individualHitPoints = new Vector3[2];

		private RaycastHit m_cachedHit;

		public bool forceAdaptRotationToSurfaceSlope
		{
			get
			{
				return m_forceAdaptRotationToSurfaceSlope;
			}
			set
			{
				m_forceAdaptRotationToSurfaceSlope = value;
			}
		}

		protected virtual void Awake()
		{
			m_transform = base.transform;
			m_animator = GetComponent<Animator>();
			m_footIK = GetComponent<FootIK>();
		}

		protected virtual void Start()
		{
			foreach (FootIK.AnimatorStateEntry forceAdaptRotationToSurfaceSlopeAnimState in m_forceAdaptRotationToSurfaceSlopeAnimStates)
			{
				m_adaptRotationToSurfaceSlopeAnimStates.Add(new KeyValuePair<int, int>(forceAdaptRotationToSurfaceSlopeAnimState.layer, Animator.StringToHash(forceAdaptRotationToSurfaceSlopeAnimState.animatorState)));
				m_footIK.tryAddToForceInvalidOnAnimatorState(forceAdaptRotationToSurfaceSlopeAnimState);
			}
		}

		protected virtual void OnAnimatorIK(int layerIdx)
		{
			if (m_footIK == null || layerIdx != m_footIK.animatorIkPassLayerIndex)
			{
				return;
			}
			GroundedResult groundedResult = m_footIK.getGroundedResult();
			if (groundedResult == null)
			{
				return;
			}
			if (m_customGroundDetectionCastTargetOne.targetTransform != null && m_customGroundDetectionCastTargetTwo.targetTransform != null && getHitPoint(m_customGroundDetectionCastTargetOne, m_transform.up, out m_cachedHit))
			{
				m_individualHitPoints[0] = m_cachedHit.point;
				if (getHitPoint(m_customGroundDetectionCastTargetTwo, m_transform.up, out m_cachedHit))
				{
					m_individualHitPoints[1] = m_cachedHit.point;
					Vector3 normalized = (m_individualHitPoints[0] - groundedResult.groundedPoint).normalized;
					Vector3 normalized2 = (m_individualHitPoints[1] - groundedResult.groundedPoint).normalized;
					Vector3 normalized3 = Vector3.Cross(normalized, normalized2).normalized;
					float num = Vector3.Dot(normalized3, m_transform.up);
					if (num < 0f)
					{
						normalized3 *= -1f;
						num *= -1f;
					}
					groundedResult.groundedNormal = normalized3;
					if (m_overrideMainGroundDetectionCastTarget.targetTransform != null && getHitPoint(m_overrideMainGroundDetectionCastTarget, m_transform.up, out m_cachedHit))
					{
						Vector3 normalized4 = (m_individualHitPoints[0] - m_cachedHit.point).normalized;
						Vector3 normalized5 = (m_individualHitPoints[1] - m_cachedHit.point).normalized;
						Vector3 normalized6 = Vector3.Cross(normalized4, normalized5).normalized;
						float num2 = Vector3.Dot(normalized6, m_transform.up);
						if (num2 < 0f)
						{
							normalized6 *= -1f;
							num2 *= -1f;
						}
						if (num2 > num)
						{
							groundedResult.groundedNormal = normalized6;
						}
					}
				}
			}
			Vector3 b = Vector3.zero;
			bool flag = isValidForAdaptingRotationToSurfaceSlope();
			if (flag != m_prevValidationState || m_updateTargetRotationContinously)
			{
				if (flag && groundedResult.isGrounded)
				{
					if (m_alignmentAlgorithm == AlignmentAlgorithm.SIMPLE_SURFACE_NORMAL)
					{
						m_targetRotation = Quaternion.FromToRotation(m_transform.up, groundedResult.groundedNormal);
					}
					else
					{
						Vector3 vector = Vector3.Cross(groundedResult.groundedNormal, m_transform.up);
						Vector3 onNormal = Vector3.Cross(vector, groundedResult.groundedNormal);
						Vector3 vector2 = Vector3.Project(m_transform.forward, vector);
						Vector3 vector3 = Vector3.Project(m_transform.forward, onNormal);
						Vector3 vector4 = vector2 + vector3;
						m_targetRotation = Quaternion.FromToRotation(m_transform.forward, vector4.normalized);
					}
					b = Vector3.Project(groundedResult.groundedPoint - m_transform.position, groundedResult.groundedNormal);
					b += m_positionalHeightOffset * groundedResult.groundedNormal;
				}
				else
				{
					m_targetRotation = Quaternion.identity;
				}
				m_prevValidationState = flag;
			}
			m_surfaceDeltaRot = Quaternion.Slerp(m_surfaceDeltaRot, m_targetRotation, Time.deltaTime * m_adaptRotationToSurfaceSlopeTransitionSpeed);
			m_currOffsetVec = Vector3.Lerp(m_currOffsetVec, b, Time.deltaTime * m_positionOffsetTransitionSpeed);
			Vector3 vector5 = m_animator.bodyPosition - m_transform.position;
			m_animator.bodyPosition = m_transform.position + m_surfaceDeltaRot * vector5 + m_currOffsetVec;
			m_animator.bodyRotation = m_surfaceDeltaRot * m_animator.bodyRotation;
		}

		protected virtual bool isValidForAdaptingRotationToSurfaceSlope()
		{
			if (m_forceAdaptRotationToSurfaceSlope != m_prevForceAdaptRotation)
			{
				if (m_forceAdaptRotationToSurfaceSlope)
				{
					m_footIK.setIsValidAndShouldCheckForGrounded(FootIK.ValidationType.FORCE_INVALID);
				}
				else
				{
					m_footIK.setIsValidAndShouldCheckForGrounded(FootIK.ValidationType.CHECK_IS_GROUNDED);
				}
				m_prevForceAdaptRotation = m_forceAdaptRotationToSurfaceSlope;
			}
			if (m_forceAdaptRotationToSurfaceSlope)
			{
				return true;
			}
			foreach (KeyValuePair<int, int> adaptRotationToSurfaceSlopeAnimState in m_adaptRotationToSurfaceSlopeAnimStates)
			{
				if (m_animator.layerCount > adaptRotationToSurfaceSlopeAnimState.Key)
				{
					AnimatorStateInfo nextAnimatorStateInfo = m_animator.GetNextAnimatorStateInfo(adaptRotationToSurfaceSlopeAnimState.Key);
					if (nextAnimatorStateInfo.shortNameHash == adaptRotationToSurfaceSlopeAnimState.Value && nextAnimatorStateInfo.normalizedTime > 0f)
					{
						return true;
					}
					if ((nextAnimatorStateInfo.shortNameHash == adaptRotationToSurfaceSlopeAnimState.Value || !(nextAnimatorStateInfo.normalizedTime > 0f)) && m_animator.GetCurrentAnimatorStateInfo(adaptRotationToSurfaceSlopeAnimState.Key).shortNameHash == adaptRotationToSurfaceSlopeAnimState.Value)
					{
						return true;
					}
				}
			}
			return false;
		}

		protected virtual bool getHitPoint(CustomGroundDetectionCast cast, Vector3 upVec, out RaycastHit hit)
		{
			float num = cast.radius + cast.distance;
			float maxDistance = num + cast.distance - cast.radius;
			Vector3 origin = cast.targetTransform.position + upVec * num;
			if (Physics.SphereCast(new Ray(origin, -upVec), cast.radius, out hit, maxDistance, m_footIK.collisionLayerMask, m_footIK.triggerCollisionInteraction))
			{
				return true;
			}
			return false;
		}
	}
}
