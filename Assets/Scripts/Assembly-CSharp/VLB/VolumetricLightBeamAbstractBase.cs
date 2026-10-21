using UnityEngine;

namespace VLB
{
	public abstract class VolumetricLightBeamAbstractBase : MonoBehaviour
	{
		public enum AttachedLightType
		{
			NoLight = 0,
			OtherLight = 1,
			SpotLight = 2
		}

		public const string ClassName = "VolumetricLightBeamAbstractBase";

		[SerializeField]
		protected int pluginVersion = -1;

		protected Light m_CachedLightSpot;

		public bool hasGeometry => GetBeamGeometry() != null;

		public Bounds bounds
		{
			get
			{
				if (!(GetBeamGeometry() != null))
				{
					return new Bounds(Vector3.zero, Vector3.zero);
				}
				return GetBeamGeometry().meshRenderer.bounds;
			}
		}

		public int _INTERNAL_pluginVersion => pluginVersion;

		public Light lightSpotAttached => m_CachedLightSpot;

		public abstract BeamGeometryAbstractBase GetBeamGeometry();

		protected abstract void SetBeamGeometryNull();

		public abstract bool IsScalable();

		public abstract Vector3 GetLossyScale();

		public Light GetLightSpotAttachedSlow(out AttachedLightType lightType)
		{
			Light component = GetComponent<Light>();
			if ((bool)component)
			{
				if (component.type == LightType.Spot)
				{
					lightType = AttachedLightType.SpotLight;
					return component;
				}
				lightType = AttachedLightType.OtherLight;
				return null;
			}
			lightType = AttachedLightType.NoLight;
			return null;
		}

		protected void InitLightSpotAttachedCached()
		{
			m_CachedLightSpot = GetLightSpotAttachedSlow(out var _);
		}

		private void OnDestroy()
		{
			DestroyBeam();
		}

		protected void DestroyBeam()
		{
			if (Application.isPlaying)
			{
				BeamGeometryAbstractBase.DestroyBeamGeometryGameObject(GetBeamGeometry());
			}
			SetBeamGeometryNull();
		}
	}
}
