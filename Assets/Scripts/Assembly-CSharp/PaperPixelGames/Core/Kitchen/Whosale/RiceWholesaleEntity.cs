using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Kitchen.Wholasale;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.Kitchen.Whosale
{
	public class RiceWholesaleEntity : WholesaleEntity
	{
		[Serializable]
		private class ProxyColliderEntry
		{
			public Collider Collider;

			[Range(0.2f, 1f)]
			public float EmptyHeightRatio = 0.55f;

			public bool LockBottom = true;

			[HideInInspector]
			public Vector3 FullCenter;

			[HideInInspector]
			public Vector3 FullSize;

			[HideInInspector]
			public float FullRadius;

			[HideInInspector]
			public float FullHeight;

			[HideInInspector]
			public bool Cached;
		}

		private const string FullModelFallbackName = "RiceBag";

		private const string EmptyModelFallbackName = "RiceBag_Empty";

		[Header("Blend")]
		[SerializeField]
		private SkinnedMeshRenderer _sackRenderer;

		[SerializeField]
		private int _blendShapeIndex;

		[SerializeField]
		[Range(0f, 1f)]
		private float _heldMinimumFillRatio = 0.35f;

		[Header("Held Z By Count")]
		[SerializeField]
		private float _heldFullLocalZ = -0.365f;

		[SerializeField]
		[Min(0f)]
		private float _heldZDecreasePerMissingRice;

		[Header("Proxy Colliders (optional)")]
		[SerializeField]
		private List<ProxyColliderEntry> _proxyColliders = new List<ProxyColliderEntry>();

		[Header("Model Swap (WORLD roots)")]
		[SerializeField]
		private GameObject fullPackModelRoot;

		[SerializeField]
		private GameObject emptyPackModelRoot;

		private readonly List<SkinnedMeshRenderer> _worldRenderers = new List<SkinnedMeshRenderer>(1);

		private readonly List<SkinnedMeshRenderer> _fpsRenderers = new List<SkinnedMeshRenderer>(1);

		private readonly List<SkinnedMeshRenderer> _tpsRenderers = new List<SkinnedMeshRenderer>(1);

		private GameObject _worldFullRoot;

		private GameObject _worldEmptyRoot;

		private GameObject _fpsFullRoot;

		private GameObject _fpsEmptyRoot;

		private GameObject _tpsFullRoot;

		private GameObject _tpsEmptyRoot;

		public override void ApplyStaffHeldContents(GameObject graphic, byte count)
		{
			if (!(graphic == null))
			{
				List<SkinnedMeshRenderer> list = new List<SkinnedMeshRenderer>(1);
				CacheShadowRenderer(graphic, list);
				ApplyBlend(list, count, _heldMinimumFillRatio);
				GameObject fullRoot = ResolveShadowModelRoot(graphic, ResolveWorldModelRoot(fullPackModelRoot, "RiceBag"), "Staff full");
				GameObject emptyRoot = ResolveShadowModelRoot(graphic, ResolveWorldModelRoot(emptyPackModelRoot, "RiceBag_Empty"), "Staff empty");
				ApplyShadowModelSwap(graphic, fullRoot, emptyRoot, count == 0);
				ApplyHeldYOffset(graphic, ResolveHeldPosition(count, base.ItemSO.TPSHoldOffset.PositionOffset));
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			CacheProxyCollidersIfNeeded();
			CacheRenderersAndModelRoots();
			ApplyAll(base.CurrentCount);
		}

		protected override void ClientHoldEveryone()
		{
			base.ClientHoldEveryone();
			Singleton<DelayHelper>.Instance.DelayActionUntilEndOfFrame(() =>
			{
				CacheRenderersAndModelRoots();
				ApplyAll(base.CurrentCount);
			});
		}

		protected override void HandleVisualUpdate(byte newValue)
		{
			base.HandleVisualUpdate(newValue);
			CacheRenderersAndModelRoots();
			ApplyAll(newValue);
			ApplyProxyColliders(newValue);
		}

		protected override void ClientPlace(Vector3 pos, Vector3 rot, ulong holderClientId)
		{
			base.ClientPlace(pos, rot, holderClientId);
			CacheRenderersAndModelRoots();
			ApplyAll(base.CurrentCount);
		}

		protected override void ClientDrop(Vector3 pos, Quaternion rot)
		{
			base.ClientDrop(pos, rot);
			CacheRenderersAndModelRoots();
			ApplyAll(base.CurrentCount);
		}

		public override void ClientSetVisibility(bool visible)
		{
			base.ClientSetVisibility(visible);
			if (visible)
			{
				CacheRenderersAndModelRoots();
				ApplyAll(base.CurrentCount);
			}
		}

		private void ApplyAll(byte count)
		{
			ApplyModelSwapAll(count);
			ApplyBlendAll(count);
			ApplyHeldYOffset(count);
		}

		private void CacheRenderersAndModelRoots()
		{
			_worldRenderers.Clear();
			_fpsRenderers.Clear();
			_tpsRenderers.Clear();
			if (_sackRenderer != null)
			{
				_worldRenderers.Add(_sackRenderer);
			}
			else
			{
				SkinnedMeshRenderer componentInChildren = GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
				if (componentInChildren != null)
				{
					_worldRenderers.Add(componentInChildren);
				}
			}
			CacheShadowRenderer(_fpsShadowObject, _fpsRenderers);
			CacheShadowRenderer(_tpsShadowObject, _tpsRenderers);
			_worldFullRoot = ResolveWorldModelRoot(fullPackModelRoot, "RiceBag");
			_worldEmptyRoot = ResolveWorldModelRoot(emptyPackModelRoot, "RiceBag_Empty");
			_fpsFullRoot = ResolveShadowModelRoot(_fpsShadowObject, _worldFullRoot, "FPS full");
			_fpsEmptyRoot = ResolveShadowModelRoot(_fpsShadowObject, _worldEmptyRoot, "FPS empty");
			_tpsFullRoot = ResolveShadowModelRoot(_tpsShadowObject, _worldFullRoot, "TPS full");
			_tpsEmptyRoot = ResolveShadowModelRoot(_tpsShadowObject, _worldEmptyRoot, "TPS empty");
		}

		private GameObject ResolveWorldModelRoot(GameObject configuredRoot, string fallbackName)
		{
			GameObject gameObject = FindChildByRuntimeName(configuredRoot, fallbackName);
			if (gameObject != null)
			{
				return gameObject;
			}
			GameObject gameObject2 = FindChildByRuntimeName(base.gameObject, fallbackName);
			if (gameObject2 != null)
			{
				return gameObject2;
			}
			if (configuredRoot != null)
			{
				return configuredRoot;
			}
			UtilityMethods.Log("[RiceWholesaleEntity] World model root not found. Object=" + base.name + ", FallbackName=" + fallbackName, LogType.Warning);
			return null;
		}

		private static void CacheShadowRenderer(GameObject shadowObj, List<SkinnedMeshRenderer> target)
		{
			if (!(shadowObj == null))
			{
				SkinnedMeshRenderer componentInChildren = shadowObj.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
				if (componentInChildren != null)
				{
					target.Add(componentInChildren);
				}
			}
		}

		private static GameObject FindSameNameChildRecursive(GameObject shadowInstanceRoot, GameObject worldRoot)
		{
			if (shadowInstanceRoot == null || worldRoot == null)
			{
				return null;
			}
			if (HasSameRuntimeName(shadowInstanceRoot.name, worldRoot.name))
			{
				return shadowInstanceRoot;
			}
			return FindChildByRuntimeName(shadowInstanceRoot, worldRoot.name);
		}

		private static GameObject FindChildByRuntimeName(GameObject root, string childName)
		{
			if (root == null || string.IsNullOrEmpty(childName))
			{
				return null;
			}
			Transform[] componentsInChildren = root.GetComponentsInChildren<Transform>(includeInactive: true);
			foreach (Transform transform in componentsInChildren)
			{
				if (!(transform == null) && HasSameRuntimeName(transform.name, childName))
				{
					return transform.gameObject;
				}
			}
			return null;
		}

		private static bool HasSameRuntimeName(string instanceName, string sourceName)
		{
			return NormalizeRuntimeName(instanceName) == NormalizeRuntimeName(sourceName);
		}

		private static string NormalizeRuntimeName(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return string.Empty;
			}
			return value.Replace("(Clone)", string.Empty).Trim();
		}

		private GameObject ResolveShadowModelRoot(GameObject shadowInstanceRoot, GameObject worldRoot, string label)
		{
			if (shadowInstanceRoot == null || worldRoot == null)
			{
				return null;
			}
			GameObject gameObject = FindSameNameChildRecursive(shadowInstanceRoot, worldRoot);
			if (gameObject != null)
			{
				return gameObject;
			}
			GameObject gameObject2 = FindMatchingHierarchyPathInShadow(shadowInstanceRoot, worldRoot.transform);
			if (gameObject2 != null)
			{
				return gameObject2;
			}
			UtilityMethods.Log("[RiceWholesaleEntity] Shadow model root not found. Label=" + label + ", Object=" + base.name + ", WorldRoot=" + worldRoot.name + ", ShadowRoot=" + shadowInstanceRoot.name, LogType.Warning);
			return null;
		}

		private GameObject FindMatchingHierarchyPathInShadow(GameObject shadowInstanceRoot, Transform worldRoot)
		{
			if (shadowInstanceRoot == null || worldRoot == null)
			{
				return null;
			}
			List<int> list = new List<int>();
			Transform transform = worldRoot;
			while (transform != null && transform != base.transform)
			{
				GameObject gameObject = FindSameNameChildRecursive(shadowInstanceRoot, transform.gameObject);
				if (gameObject != null)
				{
					return TryFollowChildIndexPath(gameObject.transform, list);
				}
				list.Insert(0, transform.GetSiblingIndex());
				transform = transform.parent;
			}
			return null;
		}

		private static GameObject TryFollowChildIndexPath(Transform anchor, List<int> childIndexPath)
		{
			if (anchor == null || childIndexPath == null)
			{
				return null;
			}
			Transform transform = anchor;
			for (int i = 0; i < childIndexPath.Count; i++)
			{
				int num = childIndexPath[i];
				if (num < 0 || num >= transform.childCount)
				{
					return null;
				}
				transform = transform.GetChild(num);
			}
			if (!(transform != null))
			{
				return null;
			}
			return transform.gameObject;
		}

		private void ApplyBlendAll(byte count)
		{
			ApplyBlend(_worldRenderers, count, 0f);
			ApplyBlend(_fpsRenderers, count, _heldMinimumFillRatio);
			ApplyBlend(_tpsRenderers, count, _heldMinimumFillRatio);
		}

		private void ApplyBlend(List<SkinnedMeshRenderer> renderers, byte count, float minimumFillRatio)
		{
			float num = ResolveFillRatio(count, minimumFillRatio);
			float value = (1f - num) * 100f;
			for (int i = 0; i < renderers.Count; i++)
			{
				SkinnedMeshRenderer skinnedMeshRenderer = renderers[i];
				if (!(skinnedMeshRenderer == null) && CanApplyBlendShape(skinnedMeshRenderer))
				{
					skinnedMeshRenderer.SetBlendShapeWeight(_blendShapeIndex, value);
				}
			}
		}

		private float ResolveFillRatio(byte count, float minimumFillRatio)
		{
			if (base.WholesaleSO == null || base.WholesaleSO.Quantity <= 0)
			{
				return 1f;
			}
			return Mathf.Max(Mathf.Clamp01((float)(int)count / (float)(int)base.WholesaleSO.Quantity), Mathf.Clamp01(minimumFillRatio));
		}

		private bool CanApplyBlendShape(SkinnedMeshRenderer renderer)
		{
			if (renderer.sharedMesh != null && _blendShapeIndex >= 0)
			{
				return _blendShapeIndex < renderer.sharedMesh.blendShapeCount;
			}
			return false;
		}

		private void ApplyHeldYOffset(byte count)
		{
			ApplyHeldYOffset(_fpsShadowObject, ResolveHeldPosition(count, (base.ItemSO != null) ? base.ItemSO.FPSHoldOffset.PositionOffset : Vector3.zero));
			ApplyHeldYOffset(_tpsShadowObject, ResolveHeldPosition(count, (base.ItemSO != null) ? base.ItemSO.TPSHoldOffset.PositionOffset : Vector3.zero));
		}

		private Vector3 ResolveHeldPosition(byte count, Vector3 defaultPosition)
		{
			defaultPosition.z = ResolveHeldZ(count);
			return defaultPosition;
		}

		private float ResolveHeldZ(byte count)
		{
			int num = ((base.WholesaleSO != null) ? base.WholesaleSO.Quantity : count);
			int num2 = Mathf.Max(0, num - count);
			return _heldFullLocalZ - (float)num2 * _heldZDecreasePerMissingRice;
		}

		private void ApplyHeldYOffset(GameObject shadowObject, Vector3 localPosition)
		{
			if (!(shadowObject == null))
			{
				shadowObject.transform.localPosition = localPosition;
			}
		}

		private void ApplyModelSwapAll(byte count)
		{
			bool flag = count == 0;
			SetModelRootActive(_worldFullRoot, !flag);
			SetModelRootActive(_worldEmptyRoot, flag);
			ApplyShadowModelSwap(_fpsShadowObject, _fpsFullRoot, _fpsEmptyRoot, flag);
			ApplyShadowModelSwap(_tpsShadowObject, _tpsFullRoot, _tpsEmptyRoot, flag);
		}

		private static void ApplyShadowModelSwap(GameObject shadowObject, GameObject fullRoot, GameObject emptyRoot, bool isEmpty)
		{
			if (!(shadowObject == null))
			{
				if (!(fullRoot == shadowObject))
				{
					SetModelRootActive(fullRoot, !isEmpty);
				}
				else if (emptyRoot == null)
				{
					SetRenderersActive(fullRoot, !isEmpty);
				}
				else
				{
					SetRenderersActiveExcept(fullRoot, emptyRoot, !isEmpty);
				}
				SetModelRootActive(emptyRoot, isEmpty);
			}
		}

		private static void SetModelRootActive(GameObject root, bool active)
		{
			if (root != null)
			{
				root.SetActive(active);
			}
		}

		private static void SetRenderersActive(GameObject root, bool active)
		{
			if (root == null)
			{
				return;
			}
			Renderer[] componentsInChildren = root.GetComponentsInChildren<Renderer>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				if (componentsInChildren[i] != null)
				{
					componentsInChildren[i].enabled = active;
				}
			}
		}

		private static void SetRenderersActiveExcept(GameObject root, GameObject excludedRoot, bool active)
		{
			if (root == null)
			{
				return;
			}
			Renderer[] componentsInChildren = root.GetComponentsInChildren<Renderer>(includeInactive: true);
			Transform transform = ((excludedRoot != null) ? excludedRoot.transform : null);
			foreach (Renderer renderer in componentsInChildren)
			{
				if (!(renderer == null) && (!(transform != null) || !renderer.transform.IsChildOf(transform)))
				{
					renderer.enabled = active;
				}
			}
		}

		private void CacheProxyCollidersIfNeeded()
		{
			if (_proxyColliders == null || _proxyColliders.Count == 0)
			{
				return;
			}
			for (int i = 0; i < _proxyColliders.Count; i++)
			{
				ProxyColliderEntry proxyColliderEntry = _proxyColliders[i];
				if (proxyColliderEntry != null && !(proxyColliderEntry.Collider == null) && !proxyColliderEntry.Cached)
				{
					if (proxyColliderEntry.Collider is BoxCollider boxCollider)
					{
						proxyColliderEntry.FullCenter = boxCollider.center;
						proxyColliderEntry.FullSize = boxCollider.size;
						proxyColliderEntry.Cached = true;
					}
					else if (proxyColliderEntry.Collider is CapsuleCollider capsuleCollider)
					{
						proxyColliderEntry.FullCenter = capsuleCollider.center;
						proxyColliderEntry.FullRadius = capsuleCollider.radius;
						proxyColliderEntry.FullHeight = capsuleCollider.height;
						proxyColliderEntry.FullSize = new Vector3(capsuleCollider.radius * 2f, capsuleCollider.height, capsuleCollider.radius * 2f);
						proxyColliderEntry.Cached = true;
					}
					else if (proxyColliderEntry.Collider is SphereCollider sphereCollider)
					{
						proxyColliderEntry.FullCenter = sphereCollider.center;
						proxyColliderEntry.FullRadius = sphereCollider.radius;
						proxyColliderEntry.FullSize = Vector3.one * (sphereCollider.radius * 2f);
						proxyColliderEntry.Cached = true;
					}
				}
			}
		}

		private void ApplyProxyColliders(byte count)
		{
			if (_proxyColliders == null || _proxyColliders.Count == 0 || base.WholesaleSO == null || base.WholesaleSO.Quantity <= 0)
			{
				return;
			}
			CacheProxyCollidersIfNeeded();
			float tEmpty = 1f - (float)(int)count / (float)(int)base.WholesaleSO.Quantity;
			for (int i = 0; i < _proxyColliders.Count; i++)
			{
				ProxyColliderEntry proxyColliderEntry = _proxyColliders[i];
				if (proxyColliderEntry != null && !(proxyColliderEntry.Collider == null) && proxyColliderEntry.Cached)
				{
					if (proxyColliderEntry.Collider is BoxCollider box)
					{
						ApplyBox(box, proxyColliderEntry, tEmpty);
					}
					else if (proxyColliderEntry.Collider is CapsuleCollider capsule)
					{
						ApplyCapsule(capsule, proxyColliderEntry, tEmpty);
					}
					else if (proxyColliderEntry.Collider is SphereCollider sphere)
					{
						ApplySphere(sphere, proxyColliderEntry, tEmpty);
					}
				}
			}
		}

		private static void ApplyBox(BoxCollider box, ProxyColliderEntry e, float tEmpty)
		{
			Vector3 fullSize = e.FullSize;
			float y = e.FullSize.y;
			float b = y * Mathf.Clamp01(e.EmptyHeightRatio);
			float num = (fullSize.y = Mathf.Lerp(y, b, tEmpty));
			Vector3 fullCenter = e.FullCenter;
			if (e.LockBottom)
			{
				float num2 = e.FullCenter.y - y * 0.5f;
				fullCenter.y = num2 + num * 0.5f;
			}
			box.size = fullSize;
			box.center = fullCenter;
		}

		private static void ApplyCapsule(CapsuleCollider capsule, ProxyColliderEntry e, float tEmpty)
		{
			float fullHeight = e.FullHeight;
			float b = fullHeight * Mathf.Clamp01(e.EmptyHeightRatio);
			float a = Mathf.Lerp(fullHeight, b, tEmpty);
			capsule.height = Mathf.Max(a, capsule.radius * 2f);
			Vector3 fullCenter = e.FullCenter;
			if (e.LockBottom)
			{
				float num = e.FullCenter.y - fullHeight * 0.5f;
				fullCenter.y = num + capsule.height * 0.5f;
			}
			capsule.center = fullCenter;
		}

		private static void ApplySphere(SphereCollider sphere, ProxyColliderEntry e, float tEmpty)
		{
			float fullRadius = e.FullRadius;
			float b = fullRadius * Mathf.Clamp01(e.EmptyHeightRatio);
			float num = (sphere.radius = Mathf.Lerp(fullRadius, b, tEmpty));
			Vector3 fullCenter = e.FullCenter;
			if (e.LockBottom)
			{
				float num3 = e.FullCenter.y - fullRadius;
				fullCenter.y = num3 + num;
			}
			sphere.center = fullCenter;
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "RiceWholesaleEntity";
		}
	}
}
