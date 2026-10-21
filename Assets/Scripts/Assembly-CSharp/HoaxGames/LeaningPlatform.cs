using System.Collections.Generic;
using UnityEngine;

namespace HoaxGames
{
	public class LeaningPlatform : MonoBehaviour
	{
		public class FootIkOverrideSettings
		{
			public float ikRotationStiffness;

			public float antiDownwardsIntersectionStiffness;

			public float isGluedToGroundDistanceThreshold;

			public FootIkOverrideSettings(float _ikRotationStiffness, float _antiDownwardsIntersectionStiffness, float _isGluedToGroundDistanceThreshold)
			{
				ikRotationStiffness = _ikRotationStiffness;
				antiDownwardsIntersectionStiffness = _antiDownwardsIntersectionStiffness;
				isGluedToGroundDistanceThreshold = _isGluedToGroundDistanceThreshold;
			}
		}

		private Dictionary<CharacterController, float> m_skinWidthLookupDic = new Dictionary<CharacterController, float>();

		private Dictionary<FootIK, FootIkOverrideSettings> m_footIkLookupDic = new Dictionary<FootIK, FootIkOverrideSettings>();

		private void Start()
		{
		}

		private void OnTriggerEnter(Collider other)
		{
			CharacterController component = other.GetComponent<CharacterController>();
			if ((bool)component)
			{
				if (!m_skinWidthLookupDic.ContainsKey(component))
				{
					m_skinWidthLookupDic.Add(component, component.skinWidth);
				}
				component.skinWidth = 0.0001f;
			}
			FootIK component2 = other.GetComponent<FootIK>();
			if ((bool)component2)
			{
				if (!m_footIkLookupDic.ContainsKey(component2))
				{
					m_footIkLookupDic.Add(component2, new FootIkOverrideSettings(component2.getIkRotationStiffness(), component2.getAntiDownwardsIntersectionStiffness(), component2.getIsGluedToGroundDistanceThreshold()));
				}
				component2.setIkRotationStiffness(16f);
				component2.setAntiDownwardsIntersectionStiffness(250f);
				component2.setIsGluedToGroundDistanceThreshold(0.1f);
			}
		}

		private void OnTriggerExit(Collider other)
		{
			CharacterController component = other.GetComponent<CharacterController>();
			if ((bool)component && m_skinWidthLookupDic.ContainsKey(component))
			{
				component.skinWidth = m_skinWidthLookupDic[component];
			}
			FootIK component2 = other.GetComponent<FootIK>();
			if ((bool)component2 && m_footIkLookupDic.ContainsKey(component2))
			{
				FootIkOverrideSettings footIkOverrideSettings = m_footIkLookupDic[component2];
				component2.setIkRotationStiffness(footIkOverrideSettings.ikRotationStiffness);
				component2.setAntiDownwardsIntersectionStiffness(footIkOverrideSettings.antiDownwardsIntersectionStiffness);
				component2.setIsGluedToGroundDistanceThreshold(footIkOverrideSettings.isGluedToGroundDistanceThreshold);
			}
		}
	}
}
