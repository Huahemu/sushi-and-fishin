using System.Collections.Generic;
using FIMSpace.FEyes;
using PaperPixelGames.Core.Player;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
public sealed class FEyesProximityLookTarget : MonoBehaviour
{
	[Header("References")]
	[SerializeField]
	private PPG_Player _ownPlayer;

	[SerializeField]
	private FEyesAnimator _eyes;

	[Header("Filtering")]
	[SerializeField]
	private string[] _allowedTags = new string[1] { "Player" };

	[SerializeField]
	private bool _ignoreTriggerColliders = true;

	[Header("Target Resolution")]
	[SerializeField]
	private string _preferredChildName = "Head";

	[Header("Offset (World Space)")]
	[SerializeField]
	private bool _useOffset;

	[SerializeField]
	private Vector3 _worldOffset = Vector3.zero;

	[Header("Selection")]
	[SerializeField]
	private bool _chooseClosest = true;

	[SerializeField]
	private Transform _distanceOrigin;

	private readonly Dictionary<Transform, int> _insideCounts = new Dictionary<Transform, int>();

	private readonly Dictionary<Collider, Transform> _colliderToTarget = new Dictionary<Collider, Transform>();

	private readonly Dictionary<Transform, Transform> _rootToPreferredChild = new Dictionary<Transform, Transform>();

	private readonly Queue<Transform> _childSearchQueue = new Queue<Transform>();

	private readonly List<Collider> _collidersToRemove = new List<Collider>(8);

	private readonly List<Transform> _targetsToRemove = new List<Transform>(8);

	private bool _isOverriding;

	private Transform _previousTargetAtOverride;

	private Transform _currentAppliedTarget;

	private SphereCollider _sphere;

	private Transform _ownRoot;

	private void Reset()
	{
		_eyes = GetComponent<FEyesAnimator>();
		_sphere = GetComponent<SphereCollider>();
		if ((bool)_sphere)
		{
			_sphere.isTrigger = true;
			_sphere.center = Vector3.up;
			_sphere.radius = 2f;
		}
		_distanceOrigin = base.transform;
	}

	private void Awake()
	{
		if (_eyes == null)
		{
			_eyes = GetComponent<FEyesAnimator>();
		}
		_sphere = GetComponent<SphereCollider>();
		_sphere.isTrigger = true;
		if (_distanceOrigin == null)
		{
			_distanceOrigin = base.transform;
		}
		_ownRoot = ((_ownPlayer != null) ? _ownPlayer.transform.root : base.transform.root);
	}

	private void OnDisable()
	{
		_insideCounts.Clear();
		_colliderToTarget.Clear();
		_rootToPreferredChild.Clear();
		_childSearchQueue.Clear();
		_collidersToRemove.Clear();
		_targetsToRemove.Clear();
		RestorePreviousTargetIfNeeded();
	}

	private void Update()
	{
		if (_isOverriding)
		{
			CleanupNullTargets();
			if (_insideCounts.Count == 0)
			{
				RestorePreviousTargetIfNeeded();
			}
			else if (_currentAppliedTarget == null || !_insideCounts.ContainsKey(_currentAppliedTarget))
			{
				ApplyBestTarget();
			}
		}
	}

	private void OnTriggerEnter(Collider other)
	{
		if (IsSelfCollider(other) || _eyes == null || (_ignoreTriggerColliders && other.isTrigger) || !IsAllowedCollider(other))
		{
			return;
		}
		Transform transform = ResolveCandidateTransform(other);
		if (!(transform == null))
		{
			if (!_colliderToTarget.ContainsKey(other))
			{
				_colliderToTarget.Add(other, transform);
			}
			if (_insideCounts.TryGetValue(transform, out var value))
			{
				_insideCounts[transform] = value + 1;
			}
			else
			{
				_insideCounts.Add(transform, 1);
			}
			ApplyBestTarget();
		}
	}

	private void OnTriggerExit(Collider other)
	{
		if (_eyes == null || (_ignoreTriggerColliders && other.isTrigger))
		{
			return;
		}
		if (_colliderToTarget.TryGetValue(other, out var value))
		{
			_colliderToTarget.Remove(other);
			if (value != null && _insideCounts.TryGetValue(value, out var value2))
			{
				value2--;
				if (value2 <= 0)
				{
					_insideCounts.Remove(value);
				}
				else
				{
					_insideCounts[value] = value2;
				}
			}
		}
		ApplyBestTarget();
	}

	private void ApplyBestTarget()
	{
		CleanupNullTargets();
		if (_insideCounts.Count == 0)
		{
			RestorePreviousTargetIfNeeded();
			return;
		}
		if (!_isOverriding)
		{
			_isOverriding = true;
			_previousTargetAtOverride = _eyes.EyesTarget;
		}
		Transform transform = SelectBestTarget();
		if (transform == null)
		{
			RestorePreviousTargetIfNeeded();
			return;
		}
		if (_useOffset)
		{
			_eyes.SetEyesTarget(transform, _worldOffset);
		}
		else
		{
			_eyes.SetEyesTarget(transform);
		}
		_currentAppliedTarget = transform;
	}

	private Transform SelectBestTarget()
	{
		Transform result = null;
		if (!_chooseClosest)
		{
			using (Dictionary<Transform, int>.Enumerator enumerator = _insideCounts.GetEnumerator())
			{
				if (enumerator.MoveNext())
				{
					result = enumerator.Current.Key;
				}
			}
			return result;
		}
		Vector3 vector = (_distanceOrigin ? _distanceOrigin.position : base.transform.position);
		float num = float.PositiveInfinity;
		foreach (KeyValuePair<Transform, int> insideCount in _insideCounts)
		{
			Transform key = insideCount.Key;
			if (!(key == null))
			{
				float sqrMagnitude = (key.position - vector).sqrMagnitude;
				if (sqrMagnitude < num)
				{
					num = sqrMagnitude;
					result = key;
				}
			}
		}
		return result;
	}

	private void RestorePreviousTargetIfNeeded()
	{
		if (_eyes == null || !_isOverriding)
		{
			return;
		}
		bool num = _eyes.EyesTarget == _currentAppliedTarget || _eyes.EyesTarget == null;
		_isOverriding = false;
		_currentAppliedTarget = null;
		if (num)
		{
			if (_previousTargetAtOverride != null)
			{
				_eyes.SetEyesTarget(_previousTargetAtOverride);
			}
			else
			{
				_eyes.SetEyesTarget(null);
			}
		}
		_previousTargetAtOverride = null;
	}

	private void CleanupNullTargets()
	{
		if (_colliderToTarget.Count > 0)
		{
			_collidersToRemove.Clear();
			foreach (KeyValuePair<Collider, Transform> item in _colliderToTarget)
			{
				if (!(item.Key != null) || !(item.Value != null))
				{
					_collidersToRemove.Add(item.Key);
				}
			}
			if (_collidersToRemove.Count > 0)
			{
				for (int i = 0; i < _collidersToRemove.Count; i++)
				{
					_colliderToTarget.Remove(_collidersToRemove[i]);
				}
				_collidersToRemove.Clear();
			}
		}
		if (_insideCounts.Count == 0)
		{
			return;
		}
		_targetsToRemove.Clear();
		foreach (KeyValuePair<Transform, int> insideCount in _insideCounts)
		{
			if (!(insideCount.Key != null))
			{
				_targetsToRemove.Add(insideCount.Key);
			}
		}
		if (_targetsToRemove.Count > 0)
		{
			for (int j = 0; j < _targetsToRemove.Count; j++)
			{
				_insideCounts.Remove(_targetsToRemove[j]);
			}
			_targetsToRemove.Clear();
		}
	}

	private Transform ResolveCandidateTransform(Collider other)
	{
		if (other == null)
		{
			return null;
		}
		PlayerRigBoneReferences componentInParent = other.GetComponentInParent<PlayerRigBoneReferences>();
		if (componentInParent == null && other.attachedRigidbody != null)
		{
			componentInParent = other.attachedRigidbody.GetComponentInParent<PlayerRigBoneReferences>();
		}
		Transform transform = ((!(componentInParent != null) || componentInParent.TpsRig == null || !(componentInParent.TpsRig.Head != null)) ? ((other.attachedRigidbody != null) ? other.attachedRigidbody.transform.root : other.transform.root) : componentInParent.TpsRig.Head);
		if (!string.IsNullOrEmpty(_preferredChildName))
		{
			Transform transform2 = FindDeepChildCached(transform, _preferredChildName);
			if (transform2 != null)
			{
				return transform2;
			}
		}
		return transform;
	}

	private bool IsAllowedCollider(Collider other)
	{
		if (other == null)
		{
			return false;
		}
		Transform transform = ((other.attachedRigidbody != null) ? other.attachedRigidbody.transform : other.transform);
		if (transform == null)
		{
			return false;
		}
		if (transform.root == _ownRoot)
		{
			return false;
		}
		if (_allowedTags == null || _allowedTags.Length == 0)
		{
			return true;
		}
		if (HasAnyAllowedTag(transform))
		{
			return true;
		}
		Transform parent = transform.parent;
		while (parent != null)
		{
			if (HasAnyAllowedTag(parent))
			{
				return true;
			}
			parent = parent.parent;
		}
		return false;
	}

	private bool IsSelfCollider(Collider other)
	{
		if (other == null)
		{
			return true;
		}
		if (((other.attachedRigidbody != null) ? other.attachedRigidbody.transform.root : other.transform.root) == _ownRoot)
		{
			return true;
		}
		return false;
	}

	private bool HasAnyAllowedTag(Transform t)
	{
		for (int i = 0; i < _allowedTags.Length; i++)
		{
			string value = _allowedTags[i];
			if (!string.IsNullOrEmpty(value) && t.CompareTag(value))
			{
				return true;
			}
		}
		return false;
	}

	private Transform FindDeepChildCached(Transform parent, string name)
	{
		if (parent == null)
		{
			return null;
		}
		if (_rootToPreferredChild.TryGetValue(parent, out var value))
		{
			return value;
		}
		_childSearchQueue.Clear();
		_childSearchQueue.Enqueue(parent);
		while (_childSearchQueue.Count > 0)
		{
			Transform transform = _childSearchQueue.Dequeue();
			if (transform.name == name)
			{
				_rootToPreferredChild[parent] = transform;
				_childSearchQueue.Clear();
				return transform;
			}
			for (int i = 0; i < transform.childCount; i++)
			{
				_childSearchQueue.Enqueue(transform.GetChild(i));
			}
		}
		_rootToPreferredChild[parent] = null;
		return null;
	}
}
