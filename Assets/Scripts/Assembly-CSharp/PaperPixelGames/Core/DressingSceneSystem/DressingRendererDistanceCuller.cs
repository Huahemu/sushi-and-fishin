using System.Collections;
using System.Collections.Generic;
using PaperPixelGames.Core.Streaming;
using UnityEngine;

namespace PaperPixelGames.Core.DressingSceneSystem
{
	public sealed class DressingRendererDistanceCuller : MonoBehaviour
	{
		private sealed class GroupEntry
		{
			public DressingActivationGroup Group;

			public MeshRenderer[] Renderers;

			public bool[] OriginalForceRenderingOff;

			public Bounds Bounds;

			public bool DesiredCulled;

			public bool AppliedCulled;

			public bool TransitionQueued;

			public int TransitionIndex;
		}

		[SerializeField]
		[Min(0.1f)]
		private float _evaluationInterval = 0.25f;

		[SerializeField]
		[Min(0f)]
		private float _restoreMarginBeyondFarClip = 60f;

		[SerializeField]
		[Min(0f)]
		private float _cullMarginBeyondFarClip = 100f;

		[SerializeField]
		[Min(32f)]
		private int _rendererChangesPerFrame = 512;

		private readonly List<GroupEntry> _entries = new List<GroupEntry>();

		private readonly HashSet<int> _registeredGroupIds = new HashSet<int>();

		private readonly Queue<GroupEntry> _transitionQueue = new Queue<GroupEntry>();

		private DressingSceneManager _manager;

		private Coroutine _buildRoutine;

		private bool _topologyRefreshRequested;

		private float _nextEvaluationTime;

		public void Initialize(DressingSceneManager manager)
		{
			_manager = manager;
			RequestTopologyRefresh();
		}

		private void OnEnable()
		{
			if (_manager != null)
			{
				RequestTopologyRefresh();
			}
		}

		private void Update()
		{
			ProcessRendererTransitions();
			if (!(_manager == null) && !_manager.IsStreamingBusy && !(Time.unscaledTime < _nextEvaluationTime))
			{
				_nextEvaluationTime = Time.unscaledTime + Mathf.Max(0.1f, _evaluationInterval);
				EvaluateDistanceCulling();
			}
		}

		public void RequestTopologyRefresh()
		{
			_topologyRefreshRequested = true;
			if (_buildRoutine == null && base.isActiveAndEnabled)
			{
				_buildRoutine = StartCoroutine(BuildNewGroupsRoutine());
			}
		}

		private IEnumerator BuildNewGroupsRoutine()
		{
			while (_topologyRefreshRequested)
			{
				_topologyRefreshRequested = false;
				PruneDestroyedGroups();
				DressingActivationGroup[] groups = Object.FindObjectsByType<DressingActivationGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
				foreach (DressingActivationGroup dressingActivationGroup in groups)
				{
					if (dressingActivationGroup == null || !dressingActivationGroup.gameObject.scene.IsValid())
					{
						continue;
					}
					int instanceID = dressingActivationGroup.GetInstanceID();
					if (!_registeredGroupIds.Add(instanceID))
					{
						continue;
					}
					MeshRenderer[] componentsInChildren = dressingActivationGroup.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
					List<MeshRenderer> list = new List<MeshRenderer>(componentsInChildren.Length);
					Bounds bounds = default;
					bool flag = false;
					foreach (MeshRenderer meshRenderer in componentsInChildren)
					{
						if (!(meshRenderer == null) && meshRenderer.gameObject.isStatic)
						{
							list.Add(meshRenderer);
							if (!flag)
							{
								bounds = meshRenderer.bounds;
								flag = true;
							}
							else
							{
								bounds.Encapsulate(meshRenderer.bounds);
							}
						}
					}
					if (flag && list.Count > 0)
					{
						GroupEntry groupEntry = new GroupEntry
						{
							Group = dressingActivationGroup,
							Renderers = list.ToArray(),
							OriginalForceRenderingOff = new bool[list.Count],
							Bounds = bounds
						};
						for (int k = 0; k < groupEntry.Renderers.Length; k++)
						{
							MeshRenderer meshRenderer2 = groupEntry.Renderers[k];
							groupEntry.OriginalForceRenderingOff[k] = meshRenderer2 != null && meshRenderer2.forceRenderingOff;
						}
						_entries.Add(groupEntry);
					}
					yield return null;
				}
			}
			_buildRoutine = null;
			EvaluateDistanceCulling();
		}

		private void PruneDestroyedGroups()
		{
			_registeredGroupIds.Clear();
			for (int num = _entries.Count - 1; num >= 0; num--)
			{
				GroupEntry groupEntry = _entries[num];
				if (groupEntry.Group == null)
				{
					_entries.RemoveAt(num);
				}
				else
				{
					_registeredGroupIds.Add(groupEntry.Group.GetInstanceID());
				}
			}
		}

		private void EvaluateDistanceCulling()
		{
			Camera main = Camera.main;
			if (main == null)
			{
				QueueRestoreAll();
				return;
			}
			float num = Mathf.Max(1f, main.farClipPlane);
			float num2 = num + Mathf.Max(0f, _restoreMarginBeyondFarClip);
			float num3 = num + Mathf.Max(_restoreMarginBeyondFarClip, _cullMarginBeyondFarClip);
			float num4 = num2 * num2;
			float num5 = num3 * num3;
			Vector3 position = main.transform.position;
			for (int num6 = _entries.Count - 1; num6 >= 0; num6--)
			{
				GroupEntry groupEntry = _entries[num6];
				if (groupEntry.Group == null)
				{
					_entries.RemoveAt(num6);
				}
				else
				{
					float num7 = groupEntry.Bounds.SqrDistance(position);
					bool shouldCull = (groupEntry.DesiredCulled ? (num7 > num4) : (num7 > num5));
					QueueTransition(groupEntry, shouldCull);
				}
			}
		}

		private void QueueRestoreAll()
		{
			for (int i = 0; i < _entries.Count; i++)
			{
				QueueTransition(_entries[i], shouldCull: false);
			}
		}

		private void QueueTransition(GroupEntry entry, bool shouldCull)
		{
			if (entry.DesiredCulled != shouldCull || (!entry.TransitionQueued && entry.AppliedCulled != shouldCull))
			{
				entry.DesiredCulled = shouldCull;
				entry.TransitionIndex = 0;
				if (!entry.TransitionQueued)
				{
					entry.TransitionQueued = true;
					_transitionQueue.Enqueue(entry);
				}
			}
		}

		private void ProcessRendererTransitions()
		{
			int num = Mathf.Max(32, _rendererChangesPerFrame);
			while (num > 0 && _transitionQueue.Count > 0)
			{
				GroupEntry groupEntry = _transitionQueue.Peek();
				if (groupEntry.Group == null || groupEntry.Renderers == null)
				{
					groupEntry.TransitionQueued = false;
					_transitionQueue.Dequeue();
					continue;
				}
				while (num > 0 && groupEntry.TransitionIndex < groupEntry.Renderers.Length)
				{
					int num2 = groupEntry.TransitionIndex++;
					MeshRenderer meshRenderer = groupEntry.Renderers[num2];
					if (meshRenderer != null)
					{
						meshRenderer.forceRenderingOff = groupEntry.DesiredCulled || groupEntry.OriginalForceRenderingOff[num2];
					}
					num--;
				}
				if (groupEntry.TransitionIndex >= groupEntry.Renderers.Length)
				{
					groupEntry.AppliedCulled = groupEntry.DesiredCulled;
					groupEntry.TransitionQueued = false;
					groupEntry.TransitionIndex = 0;
					_transitionQueue.Dequeue();
					continue;
				}
				break;
			}
		}

		private void OnDisable()
		{
			if (_buildRoutine != null)
			{
				StopCoroutine(_buildRoutine);
				_buildRoutine = null;
			}
			_topologyRefreshRequested = false;
			RestoreImmediately();
		}

		private void OnDestroy()
		{
			RestoreImmediately();
		}

		private void RestoreImmediately()
		{
			_transitionQueue.Clear();
			for (int i = 0; i < _entries.Count; i++)
			{
				GroupEntry groupEntry = _entries[i];
				if (groupEntry.Renderers == null)
				{
					continue;
				}
				for (int j = 0; j < groupEntry.Renderers.Length; j++)
				{
					MeshRenderer meshRenderer = groupEntry.Renderers[j];
					if (meshRenderer != null)
					{
						meshRenderer.forceRenderingOff = groupEntry.OriginalForceRenderingOff[j];
					}
				}
			}
			_entries.Clear();
			_registeredGroupIds.Clear();
		}
	}
}
