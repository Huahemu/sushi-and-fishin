using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixPlays.ElementalVFX
{
	public class EarthShield : Shield
	{
		[SerializeField]
		private List<Rigidbody> _ShardRigidbodies;

		[SerializeField]
		private float _AnimSpeed;

		[SerializeField]
		private AnimationCurve _AnimCurve;

		[SerializeField]
		private ParticleSystem _SpawnExplosionEffectPrefab;

		[SerializeField]
		private Transform _RotationObjectOuter;

		[SerializeField]
		private Transform _RotationObjectInner;

		[SerializeField]
		private float _RotationSpeedOuter;

		[SerializeField]
		private float _RotationSpeedInner;

		[SerializeField]
		private Vector2 _ShardRadiusSpawn;

		[SerializeField]
		private ParticleSystem _HitEffectPrefab;

		[SerializeField]
		private float _ShardScale;

		private List<MeshCollider> _meshColliders;

		private List<Vector3> _sourcePositions;

		private List<Quaternion> _sourceRotations;

		private IEnumerator Coroutine_Animate()
		{
			StartCoroutine(Coroutine_Rotate());
			if (_sourcePositions == null)
			{
				_sourcePositions = new List<Vector3>();
				_sourceRotations = new List<Quaternion>();
				foreach (Rigidbody shardRigidbody in _ShardRigidbodies)
				{
					_sourcePositions.Add(shardRigidbody.transform.localPosition);
					_sourceRotations.Add(shardRigidbody.transform.localRotation);
				}
			}
			if (_meshColliders == null)
			{
				_meshColliders = new List<MeshCollider>();
				foreach (Rigidbody shardRigidbody2 in _ShardRigidbodies)
				{
					if (shardRigidbody2.TryGetComponent<MeshCollider>(out var component))
					{
						_meshColliders.Add(component);
						component.enabled = false;
					}
				}
			}
			foreach (MeshCollider meshCollider in _meshColliders)
			{
				meshCollider.enabled = false;
			}
			List<Vector3> shardPosition = new List<Vector3>();
			List<Quaternion> shardRotation = new List<Quaternion>();
			for (int i = 0; i < _ShardRigidbodies.Count; i++)
			{
				_ShardRigidbodies[i].gameObject.SetActive(value: true);
				_ShardRigidbodies[i].isKinematic = true;
				_ShardRigidbodies[i].transform.localScale = Vector3.one * _ShardScale;
				Vector3 localPosition = _ShardRigidbodies[i].transform.localPosition;
				localPosition.y = 0f;
				float num = Random.Range(_ShardRadiusSpawn.x, _ShardRadiusSpawn.y);
				Quaternion item = Quaternion.Euler(Random.Range(0, 360), Random.Range(0, 360), Random.Range(0, 360));
				Vector3 vector = base.transform.position + localPosition.normalized * num;
				if (Physics.Raycast(new Ray(base.transform.position, Vector3.down), out var hitInfo, 10f))
				{
					vector.y = hitInfo.point.y;
				}
				else
				{
					vector.y = base.transform.position.y - 1f;
				}
				shardPosition.Add(vector);
				shardRotation.Add(item);
				Object.Instantiate(_SpawnExplosionEffectPrefab, vector, Quaternion.identity).Play();
			}
			float lerp = 0f;
			while (lerp < 1f)
			{
				for (int j = 0; j < _ShardRigidbodies.Count; j++)
				{
					_ShardRigidbodies[j].transform.localPosition = Vector3.Lerp(base.transform.InverseTransformPoint(shardPosition[j]), _sourcePositions[j], _AnimCurve.Evaluate(lerp));
					_ShardRigidbodies[j].transform.localRotation = Quaternion.Lerp(shardRotation[j], _sourceRotations[j], _AnimCurve.Evaluate(lerp));
				}
				lerp += Time.deltaTime * _AnimSpeed;
				yield return null;
			}
			for (int k = 0; k < _ShardRigidbodies.Count; k++)
			{
				_ShardRigidbodies[k].transform.localPosition = _sourcePositions[k];
				_ShardRigidbodies[k].transform.localRotation = _sourceRotations[k];
			}
		}

		private IEnumerator Coroutine_Rotate()
		{
			while (true)
			{
				_RotationObjectInner.Rotate(0f, _RotationSpeedInner * Time.deltaTime, 0f);
				_RotationObjectOuter.Rotate(0f, _RotationSpeedOuter * Time.deltaTime, 0f);
				yield return null;
			}
		}

		private IEnumerator Coroutine_StopAnimation()
		{
			foreach (Rigidbody shardRigidbody in _ShardRigidbodies)
			{
				shardRigidbody.isKinematic = false;
			}
			foreach (MeshCollider meshCollider in _meshColliders)
			{
				meshCollider.enabled = true;
			}
			float lerp = 0f;
			Vector3 startScale = Vector3.one * _ShardScale;
			Vector3 endScale = Vector3.zero;
			while (lerp < 1f)
			{
				for (int i = 0; i < _ShardRigidbodies.Count; i++)
				{
					_ShardRigidbodies[i].transform.localScale = Vector3.Lerp(startScale, endScale, lerp);
				}
				lerp += Time.deltaTime * _AnimSpeed;
				yield return null;
			}
			for (int j = 0; j < _ShardRigidbodies.Count; j++)
			{
				_ShardRigidbodies[j].gameObject.SetActive(value: false);
			}
		}

		protected override void PlayImplementation()
		{
			StopAllCoroutines();
			StartCoroutine(Coroutine_Animate());
		}

		protected override void StopImplemenation()
		{
			StopAllCoroutines();
			StartCoroutine(Coroutine_StopAnimation());
		}

		protected override void HitImplementation(Vector3 point, Vector3 normal)
		{
			ParticleSystem particleSystem = Object.Instantiate(_HitEffectPrefab, point, Quaternion.identity);
			particleSystem.transform.forward = normal;
			particleSystem.Play();
		}
	}
}
