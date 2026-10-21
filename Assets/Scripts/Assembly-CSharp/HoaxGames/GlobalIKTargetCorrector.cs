using UnityEngine;

namespace HoaxGames
{
	public class GlobalIKTargetCorrector : MonoBehaviour
	{
		public enum OffsetMethod
		{
			FULLBODY_OFFSET = 0,
			RIGHT_HAND_OFFSET = 1,
			RIGHT_SHOULDER_OFFSET = 2,
			LEFT_HAND_OFFSET = 3,
			LEFT_SHOULDER_OFFSET = 4
		}

		public enum UpdateMode
		{
			Update = 0,
			FixedUpdate = 1,
			LateUpdate = 2,
			ManualUpdate = 3
		}

		[SerializeField]
		private FootIK m_footIK;

		[SerializeField]
		private bool m_negate = true;

		[SerializeField]
		private UpdateMode m_updateMode;

		[SerializeField]
		private bool m_utilizeLocalStartOffsetToParent;

		[SerializeField]
		private bool m_resetToLocalStartPositionWhenDisabled;

		[SerializeField]
		private bool m_resetToLocalStartPositionWhenFootIkIsDisabled;

		[SerializeField]
		private OffsetMethod m_offsetMethod;

		private Transform m_transform;

		private Transform m_parentTransform;

		private Vector3 m_localOffset;

		private void Awake()
		{
			m_transform = base.transform;
			m_parentTransform = m_transform.parent;
		}

		private void OnDisable()
		{
			if (m_resetToLocalStartPositionWhenDisabled)
			{
				m_transform.localPosition = m_localOffset;
			}
		}

		private void Start()
		{
			m_localOffset = m_transform.localPosition;
		}

		private void Update()
		{
			if (m_updateMode == UpdateMode.Update)
			{
				updateCorrection();
			}
		}

		private void FixedUpdate()
		{
			if (m_updateMode == UpdateMode.FixedUpdate)
			{
				updateCorrection();
			}
		}

		private void LateUpdate()
		{
			if (m_updateMode == UpdateMode.LateUpdate)
			{
				updateCorrection();
			}
		}

		public void updateCorrection()
		{
			if (m_footIK == null)
			{
				return;
			}
			if (!m_footIK.enabled && m_resetToLocalStartPositionWhenFootIkIsDisabled)
			{
				m_transform.localPosition = m_localOffset;
				return;
			}
			Vector3 vector = m_footIK.fullBodyOffset;
			if (m_offsetMethod == OffsetMethod.RIGHT_HAND_OFFSET)
			{
				vector = m_footIK.getRightHandOffsetVecIncludingLeaning();
			}
			else if (m_offsetMethod == OffsetMethod.RIGHT_SHOULDER_OFFSET)
			{
				vector = m_footIK.getRightShoulderOffsetVecIncludingLeaning();
			}
			else if (m_offsetMethod == OffsetMethod.LEFT_HAND_OFFSET)
			{
				vector = m_footIK.getLeftHandOffsetVecIncludingLeaning();
			}
			else if (m_offsetMethod == OffsetMethod.LEFT_SHOULDER_OFFSET)
			{
				vector = m_footIK.getLeftShoulderOffsetVecIncludingLeaning();
			}
			if (m_negate)
			{
				vector *= -1f;
			}
			if (!m_utilizeLocalStartOffsetToParent)
			{
				m_transform.position = m_parentTransform.position + vector;
			}
			else
			{
				m_transform.position = m_parentTransform.position + m_localOffset + vector;
			}
		}
	}
}
