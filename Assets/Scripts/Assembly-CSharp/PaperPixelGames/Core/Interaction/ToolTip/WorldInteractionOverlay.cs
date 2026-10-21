using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Interaction.ToolTip
{
	public sealed class WorldInteractionOverlay : MonoBehaviour
	{
		private static readonly Dictionary<int, WorldInteractionOverlay> Instances = new Dictionary<int, WorldInteractionOverlay>();

		private static bool stackLabelsVisible = true;

		private readonly List<ToolTipUI> tooltips = new List<ToolTipUI>();

		private RaycastHit[] hits = new RaycastHit[32];

		private readonly Vector3[] corners = new Vector3[4];

		public RectTransform Root { get; private set; }

		public RectTransform Labels { get; private set; }

		public RectTransform Tooltips { get; private set; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetInstances()
		{
			Instances.Clear();
			stackLabelsVisible = true;
		}

		public static void SetStackLabelsVisible(bool visible)
		{
			stackLabelsVisible = visible;
			foreach (WorldInteractionOverlay value in Instances.Values)
			{
				if (value != null && value.Labels != null)
				{
					value.Labels.gameObject.SetActive(visible);
				}
			}
		}

		public static WorldInteractionOverlay ForScene(Scene scene)
		{
			if (Instances.TryGetValue(scene.handle, out var value) && value != null)
			{
				return value;
			}
			GameObject gameObject = new GameObject("World interaction overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
			SceneManager.MoveGameObjectToScene(gameObject, scene);
			WorldInteractionOverlay worldInteractionOverlay = gameObject.AddComponent<WorldInteractionOverlay>();
			worldInteractionOverlay.Root = (RectTransform)gameObject.transform;
			Canvas component = gameObject.GetComponent<Canvas>();
			component.renderMode = RenderMode.ScreenSpaceOverlay;
			component.sortingOrder = -10;
			component.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
			CanvasScaler component2 = gameObject.GetComponent<CanvasScaler>();
			component2.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			component2.referenceResolution = new Vector2(1920f, 1080f);
			component2.matchWidthOrHeight = 0.5f;
			worldInteractionOverlay.Labels = worldInteractionOverlay.CreateLayer("Stack counts");
			worldInteractionOverlay.Labels.gameObject.SetActive(stackLabelsVisible);
			worldInteractionOverlay.Tooltips = worldInteractionOverlay.CreateLayer("Tooltips");
			Instances[scene.handle] = worldInteractionOverlay;
			return worldInteractionOverlay;
		}

		private RectTransform CreateLayer(string layerName)
		{
			RectTransform obj = (RectTransform)new GameObject(layerName, typeof(RectTransform)).transform;
			obj.SetParent(Root, worldPositionStays: false);
			obj.anchorMin = Vector2.zero;
			obj.anchorMax = Vector2.one;
			Vector2 offsetMin = (obj.offsetMax = Vector2.zero);
			obj.offsetMin = offsetMin;
			return obj;
		}

		public void Register(ToolTipUI tooltip)
		{
			if (!tooltips.Contains(tooltip))
			{
				tooltips.Add(tooltip);
			}
		}

		public void Unregister(ToolTipUI tooltip)
		{
			tooltips.Remove(tooltip);
		}

		public bool TryProject(Camera camera, Vector3 worldPoint, out Vector2 point)
		{
			point = default;
			if (camera == null || !camera.isActiveAndEnabled)
			{
				return false;
			}
			Vector3 vector = camera.WorldToScreenPoint(worldPoint);
			if (vector.z <= camera.nearClipPlane || !camera.pixelRect.Contains(new Vector2(vector.x, vector.y)))
			{
				return false;
			}
			return RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, vector, null, out point);
		}

		public Rect VisibleRect(Camera camera, float padding = 12f)
		{
			Rect pixelRect = camera.pixelRect;
			RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, pixelRect.min, null, out var localPoint);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, pixelRect.max, null, out var localPoint2);
			localPoint = Vector2.Max(localPoint, Root.rect.min) + Vector2.one * padding;
			localPoint2 = Vector2.Min(localPoint2, Root.rect.max) - Vector2.one * padding;
			return Rect.MinMaxRect(localPoint.x, localPoint.y, Mathf.Max(localPoint.x, localPoint2.x), Mathf.Max(localPoint.y, localPoint2.y));
		}

		public void PlaceTooltip(RectTransform panel, Vector2 anchor, Camera camera, Vector3 preferredScale)
		{
			Rect rect = VisibleRect(camera);
			Vector2 vector = Vector2.Scale(panel.rect.size, preferredScale);
			float num = Mathf.Min(1f, rect.width / Mathf.Max(1f, vector.x), rect.height / Mathf.Max(1f, vector.y));
			panel.localScale = preferredScale * num;
			Vector2 a = Vector2.Scale(panel.rect.size, panel.localScale);
			float value = anchor.x - a.x * 0.5f;
			float value2 = anchor.y + 24f;
			panel.anchoredPosition = new Vector2(Mathf.Clamp(value, rect.xMin, Mathf.Max(rect.xMin, rect.xMax - a.x)), Mathf.Clamp(value2, rect.yMin, Mathf.Max(rect.yMin, rect.yMax - a.y))) + Vector2.Scale(a, panel.pivot);
		}

		public bool IsCoveredByTooltip(RectTransform label)
		{
			Rect rect = GetRect(label);
			foreach (ToolTipUI tooltip in tooltips)
			{
				if (tooltip != null && tooltip.IsVisible && rect.Overlaps(GetRect(tooltip.Panel)))
				{
					return true;
				}
			}
			return false;
		}

		private Rect GetRect(RectTransform rect)
		{
			rect.GetWorldCorners(corners);
			Vector2 vector = Root.InverseTransformPoint(corners[0]);
			Vector2 vector2 = Root.InverseTransformPoint(corners[2]);
			return Rect.MinMaxRect(vector.x, vector.y, vector2.x, vector2.y);
		}

		public bool HasLineOfSight(Camera camera, Bounds bounds, Transform owner, IReadOnlyList<Transform> members = null)
		{
			if (ClearRay(camera, bounds.center, owner, members))
			{
				return true;
			}
			Vector3 vector = bounds.extents * 0.8f;
			for (int i = 0; i < 8; i++)
			{
				Vector3 vector2 = new Vector3(((i & 1) == 0) ? (0f - vector.x) : vector.x, ((i & 2) == 0) ? (0f - vector.y) : vector.y, ((i & 4) == 0) ? (0f - vector.z) : vector.z);
				if (ClearRay(camera, bounds.center + vector2, owner, members))
				{
					return true;
				}
			}
			return false;
		}

		private bool ClearRay(Camera camera, Vector3 target, Transform owner, IReadOnlyList<Transform> members)
		{
			Vector3 position = camera.transform.position;
			Vector3 vector = target - position;
			float magnitude = vector.magnitude;
			if (magnitude < 0.02f)
			{
				return true;
			}
			int num;
			for (num = Physics.RaycastNonAlloc(position, vector / magnitude, hits, magnitude - 0.01f, camera.cullingMask & -5, QueryTriggerInteraction.Ignore); num == hits.Length; num = Physics.RaycastNonAlloc(position, vector / magnitude, hits, magnitude - 0.01f, camera.cullingMask & -5, QueryTriggerInteraction.Ignore))
			{
				Array.Resize(ref hits, hits.Length * 2);
			}
			for (int i = 0; i < num; i++)
			{
				Transform transform = hits[i].transform;
				if ((owner != null && transform.IsChildOf(owner)) || transform.IsChildOf(camera.transform.root))
				{
					continue;
				}
				bool flag = false;
				if (members != null)
				{
					for (int j = 0; j < members.Count; j++)
					{
						if (members[j] != null && transform.IsChildOf(members[j]))
						{
							flag = true;
							break;
						}
					}
				}
				if (!flag)
				{
					return false;
				}
			}
			return true;
		}

		private void OnDestroy()
		{
			int key = base.gameObject.scene.handle;
			if (Instances.TryGetValue(key, out var value) && value == this)
			{
				Instances.Remove(key);
			}
		}
	}
}
