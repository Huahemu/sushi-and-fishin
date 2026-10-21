using CableSystem;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Data.Tools;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.PlayerTools
{
	public sealed class FishingRodHangingCableVisual : MonoBehaviour
	{
		private Transform _lineTip;

		private Transform _graphicPivot;

		private GameObject _cableOrigin;

		private GameObject _cableEnd;

		private GameObject _graphic;

		private FishingRodData _data;

		private ItemOffset _graphicOffset;

		private Cable _cable;

		private Rigidbody _cableOriginRigidbody;

		private Rigidbody _cableEndRigidbody;

		private Material _runtimeMaterial;

		private float _cableLinkLength;

		private float _seededCableLength;

		private Vector3 _initialEndVelocity;

		private bool _initialized;

		private bool _physicsReady;

		public GameObject Graphic => _graphic;

		public bool Initialize(Transform lineTip, FishSO fish, FishingRodData data, string layerName)
		{
			if (fish?.DeadGraphic == null)
			{
				return false;
			}
			ItemOffset graphicOffset = fish.FishingRodCollectOffset ?? new ItemOffset(Vector3.zero, new Vector3(25f, 180f, 90f));
			return Initialize(lineTip, fish.DeadGraphic, graphicOffset, data, layerName, fish.DeadGraphic.name + "_FishingRodPreview");
		}

		public bool Initialize(Transform lineTip, GameObject graphicPrefab, ItemOffset graphicOffset, FishingRodData data, string layerName, string instanceName)
		{
			if (_initialized || lineTip == null || graphicPrefab == null || data == null)
			{
				return false;
			}
			_lineTip = lineTip;
			_data = data;
			_graphicOffset = graphicOffset ?? new ItemOffset(Vector3.zero, Vector3.zero);
			base.transform.SetParent(null, worldPositionStays: true);
			base.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
			_cableOrigin = CreateEndpoint("FishingRodHangingCableOrigin", base.transform, isKinematic: true);
			_cableEnd = CreateEndpoint("FishingRodHangingCableEnd", base.transform, isKinematic: true);
			_cableOriginRigidbody = _cableOrigin.GetComponent<Rigidbody>();
			_cableEndRigidbody = _cableEnd.GetComponent<Rigidbody>();
			_cableOrigin.transform.position = lineTip.position;
			_seededCableLength = Mathf.Max(0.01f, data.CaughtFishCableLength) * (1f + Mathf.Clamp(data.CaughtFishCableSlack, 0f, 0.5f));
			_cableEnd.transform.position = lineTip.position + Vector3.down * _seededCableLength;
			GameObject gameObject = new GameObject("FishingRodHangingGraphicPivot");
			_graphicPivot = gameObject.transform;
			_graphicPivot.SetParent(_cableEnd.transform, worldPositionStays: false);
			_graphicPivot.SetPositionAndRotation(_cableEnd.transform.position, ResolveYawRotation(_lineTip));
			_graphic = Object.Instantiate(graphicPrefab, _graphicPivot);
			_graphic.name = (string.IsNullOrWhiteSpace(instanceName) ? (graphicPrefab.name + "_FishingRodHangingPreview") : instanceName);
			DisableGraphicPhysics(_graphic);
			_cable = base.gameObject.AddComponent<Cable>();
			_cable.InitialPoint = _cableOrigin;
			_cable.DestinyPoint = _cableEnd;
			_cable.Segments = Mathf.Clamp(data.CaughtFishCableSegmentCount, 3, 16);
			_cable.smoothSteps = 3;
			_cable.CableWidth = Mathf.Max(0.001f, data.CaughtFishCableWidth);
			_cable.DisableSegmentColliders = true;
			_cable.ApplyGeneratedSegmentLayer = true;
			_cable.GeneratedSegmentLayerName = layerName;
			_cable.LinecastMask = 0;
			_cable.VisualOnly = false;
			_cable.UseGravity = true;
			_cable.Spring = Mathf.Max(0.01f, data.CaughtFishCableSpring);
			_cable.Damping = Mathf.Max(0f, data.CaughtFishCableDamper);
			_cable.CableMass = Mathf.Max(0.01f, data.CaughtFishCableMass);
			_cable.LinealFriccion = Mathf.Max(0f, data.CaughtFishCableLinearDamping);
			_cable.AngularFriccion = Mathf.Max(0f, data.CaughtFishCableAngularDamping);
			_cable.MinDamping = 0f;
			_cableLinkLength = _seededCableLength / ((float)_cable.Segments + 1f);
			_cable.MaxDamping = _cableLinkLength;
			Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
			if (shader == null)
			{
				shader = Shader.Find("Sprites/Default");
			}
			if (shader != null)
			{
				_runtimeMaterial = new Material(shader)
				{
					name = "FishingRodHangingCable_Runtime"
				};
				_runtimeMaterial.color = new Color(0.88f, 0.94f, 0.96f, 0.9f);
				_cable.CableMaterial = _runtimeMaterial;
			}
			UtilityMethods.SetLayerRecursive(base.gameObject, layerName, includeChildren: true);
			_initialEndVelocity = lineTip.TransformDirection(data.CaughtFishInitialSwingVelocity);
			ApplyTuning(data);
			_initialized = true;
			return true;
		}

		public void ApplyTuning(FishingRodData data)
		{
			if (!_initialized && _graphic == null)
			{
				return;
			}
			if (data != null)
			{
				_data = data;
			}
			if (!(_data == null))
			{
				if (_cable != null)
				{
					_cable.CableWidth = Mathf.Max(0.001f, _data.CaughtFishCableWidth);
					_cable.Spring = Mathf.Max(0.01f, _data.CaughtFishCableSpring);
					_cable.Damping = Mathf.Max(0f, _data.CaughtFishCableDamper);
					_cable.CableMass = Mathf.Max(0.01f, _data.CaughtFishCableMass);
					_cable.LinealFriccion = Mathf.Max(0f, _data.CaughtFishCableLinearDamping);
					_cable.AngularFriccion = Mathf.Max(0f, _data.CaughtFishCableAngularDamping);
					_cable.MaxDamping = Mathf.Max(0.001f, _cableLinkLength);
				}
				if (_cableEndRigidbody != null)
				{
					_cableEndRigidbody.mass = Mathf.Max(0.01f, _data.CaughtFishCableMass);
					_cableEndRigidbody.linearDamping = Mathf.Max(0f, _data.CaughtFishCableLinearDamping);
					_cableEndRigidbody.angularDamping = Mathf.Max(0f, _data.CaughtFishCableAngularDamping);
				}
				ItemOffset graphicOffset = _graphicOffset;
				if (!(_graphic == null) && graphicOffset != null)
				{
					_graphic.transform.SetLocalPositionAndRotation(graphicOffset.PositionOffset, Quaternion.Euler(graphicOffset.RotationOffset));
					_graphic.transform.localScale = graphicOffset.ScaleOffset;
				}
			}
		}

		private void FixedUpdate()
		{
			if (_initialized && !(_lineTip == null) && !(_cableOriginRigidbody == null))
			{
				_cableOriginRigidbody.MovePosition(_lineTip.position);
				TryActivateCablePhysics();
			}
		}

		private void TryActivateCablePhysics()
		{
			if (_physicsReady || _cable == null || _cableEndRigidbody == null || _cable.CableSegments == null || _cable.CableSegments.Count != _cable.Segments || _cableEnd.GetComponent<SpringJoint>() == null)
			{
				return;
			}
			Vector3 position = _lineTip.position;
			Vector3 vector = position + Vector3.down * _seededCableLength;
			int num = _cable.CableSegments.Count + 1;
			_cableOriginRigidbody.position = position;
			_cableOriginRigidbody.linearVelocity = Vector3.zero;
			_cableOriginRigidbody.angularVelocity = Vector3.zero;
			for (int i = 0; i < _cable.CableSegments.Count; i++)
			{
				GameObject gameObject = _cable.CableSegments[i];
				if (!(gameObject == null) && gameObject.TryGetComponent<Rigidbody>(out var component))
				{
					component.position = Vector3.Lerp(position, vector, ((float)i + 1f) / (float)num);
					component.linearVelocity = Vector3.zero;
					component.angularVelocity = Vector3.zero;
					ConfigureCableBody(component);
					if (gameObject.TryGetComponent<SpringJoint>(out var component2))
					{
						ConfigureCableJoint(component2);
					}
				}
			}
			_cableEndRigidbody.position = vector;
			_cableEndRigidbody.linearVelocity = Vector3.zero;
			_cableEndRigidbody.angularVelocity = Vector3.zero;
			ConfigureCableBody(_cableEndRigidbody);
			ConfigureCableJoint(_cableEnd.GetComponent<SpringJoint>());
			_cableEndRigidbody.isKinematic = false;
			_cableEndRigidbody.useGravity = true;
			_cableEndRigidbody.linearVelocity = _initialEndVelocity;
			_cableEndRigidbody.WakeUp();
			_physicsReady = true;
		}

		private void ConfigureCableBody(Rigidbody rigidbody)
		{
			rigidbody.mass = Mathf.Max(0.01f, _data.CaughtFishCableMass);
			rigidbody.linearDamping = Mathf.Max(0f, _data.CaughtFishCableLinearDamping);
			rigidbody.angularDamping = Mathf.Max(0f, _data.CaughtFishCableAngularDamping);
		}

		private void ConfigureCableJoint(SpringJoint joint)
		{
			if (!(joint == null))
			{
				joint.spring = Mathf.Max(0.01f, _data.CaughtFishCableSpring);
				joint.damper = Mathf.Max(0f, _data.CaughtFishCableDamper);
				joint.minDistance = 0f;
				joint.maxDistance = Mathf.Max(0.001f, _cableLinkLength);
			}
		}

		private void LateUpdate()
		{
			if (_initialized && !(_lineTip == null) && !(_graphicPivot == null) && !(_cableOrigin == null) && !(_cableEnd == null) && !(_data == null))
			{
				Vector3 toDirection = _cableEnd.transform.position - _cableOrigin.transform.position;
				if (toDirection.sqrMagnitude <= 0.0001f)
				{
					toDirection = Vector3.down;
				}
				toDirection.Normalize();
				Quaternion quaternion = ResolveYawRotation(_lineTip);
				Quaternion b = Quaternion.FromToRotation(Vector3.down, toDirection) * quaternion;
				Quaternion b2 = Quaternion.Slerp(quaternion, b, Mathf.Clamp01(_data.CaughtFishSwingRotationInfluence));
				float num = Mathf.Max(0.01f, _data.CaughtFishSwingRotationResponse);
				_graphicPivot.rotation = Quaternion.Slerp(_graphicPivot.rotation, b2, 1f - Mathf.Exp((0f - num) * Time.deltaTime));
			}
		}

		private static Quaternion ResolveYawRotation(Transform source)
		{
			if (source == null)
			{
				return Quaternion.identity;
			}
			Vector3 vector = Vector3.ProjectOnPlane(source.forward, Vector3.up);
			if (vector.sqrMagnitude <= 0.0001f)
			{
				vector = Vector3.ProjectOnPlane(source.up, Vector3.up);
			}
			if (!(vector.sqrMagnitude > 0.0001f))
			{
				return Quaternion.identity;
			}
			return Quaternion.LookRotation(vector.normalized, Vector3.up);
		}

		private static GameObject CreateEndpoint(string objectName, Transform parent, bool isKinematic)
		{
			GameObject obj = new GameObject(objectName);
			obj.transform.SetParent(parent, worldPositionStays: false);
			Rigidbody rigidbody = obj.AddComponent<Rigidbody>();
			rigidbody.isKinematic = isKinematic;
			rigidbody.useGravity = !isKinematic;
			rigidbody.detectCollisions = false;
			rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
			return obj;
		}

		private static void DisableGraphicPhysics(GameObject graphic)
		{
			Collider[] componentsInChildren = graphic.GetComponentsInChildren<Collider>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].enabled = false;
			}
			Rigidbody[] componentsInChildren2 = graphic.GetComponentsInChildren<Rigidbody>(includeInactive: true);
			for (int j = 0; j < componentsInChildren2.Length; j++)
			{
				componentsInChildren2[j].isKinematic = true;
				componentsInChildren2[j].useGravity = false;
				componentsInChildren2[j].detectCollisions = false;
			}
		}

		private void OnDestroy()
		{
			if (_runtimeMaterial != null)
			{
				Object.Destroy(_runtimeMaterial);
			}
		}
	}
}
