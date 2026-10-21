using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Core.ProcessedIngredient;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Interaction.ToolTip;
using PaperPixelGames.Core.Kitchen.PreparationStation.GastronomPan;
using PaperPixelGames.Core.Kitchen.Wholasale;
using PaperPixelGames.Framework.Patterns;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Staff
{
	[DefaultExecutionOrder(10010)]
	public sealed class StaffStorageStack : MonoBehaviour, IBasicInteractable, IHeldItemMouse1Interactable, ITooltipProvider, ITooltipWorldAnchor
	{
		private Transform point;

		private readonly List<BaseHoldable> members = new List<BaseHoldable>();

		private readonly List<Transform> visibilityMembers = new List<Transform>();

		private readonly List<Mesh> bakedMeshes = new List<Mesh>();

		private GameObject graphic;

		private BoxCollider hit;

		private TextMeshProUGUI label;

		private WorldInteractionOverlay overlay;

		private Bounds visualBounds;

		private float nextVisibilityCheck;

		private bool unobstructed;

		private Camera visibilityCamera;

		private string signature;

		private string selectedType;

		private bool gazed;

		public bool CanGaze => members.Count > 0;

		public bool CanInteractable => CanGaze;

		public IReadOnlyList<BaseHoldable> Members => members;

		public void Initialize(Transform anchor)
		{
			point = anchor;
			hit = base.gameObject.AddComponent<BoxCollider>();
			hit.isTrigger = true;
			hit.enabled = false;
			GameObject gameObject = new GameObject("Stack count");
			overlay = WorldInteractionOverlay.ForScene(base.gameObject.scene);
			gameObject.transform.SetParent(overlay.Labels, worldPositionStays: false);
			label = gameObject.AddComponent<TextMeshProUGUI>();
			label.fontSize = 22f;
			label.alignment = TextAlignmentOptions.Center;
			label.color = Color.white;
			label.raycastTarget = false;
			label.outlineWidth = 0.2f;
			label.outlineColor = new Color32(35, 24, 17, byte.MaxValue);
			label.textWrappingMode = TextWrappingModes.NoWrap;
			label.rectTransform.sizeDelta = new Vector2(84f, 32f);
			label.gameObject.SetActive(value: false);
		}

		public void Refresh(List<BaseHoldable> items)
		{
			foreach (BaseHoldable member in members)
			{
				if (member != null && !items.Contains(member))
				{
					member.ClientSetStackHidden(hidden: false);
				}
			}
			members.Clear();
			members.AddRange(items);
			visibilityMembers.Clear();
			foreach (BaseHoldable member2 in members)
			{
				if (member2 != null)
				{
					visibilityMembers.Add(member2.transform);
				}
			}
			if (point == null)
			{
				hit.enabled = false;
				return;
			}
			base.transform.SetPositionAndRotation(point.position, point.rotation);
			if (!members.Any((BaseHoldable i) => StaffWorkplaceRegistry.StackType(i) == selectedType))
			{
				selectedType = ((members.Count > 0) ? StaffWorkplaceRegistry.StackType(members[0]) : null);
			}
			List<BaseHoldable> list = Selected().ToList();
			BaseHoldable baseHoldable = list.OrderByDescending(StaffWorkplaceRegistry.StackContent).FirstOrDefault();
			string text = string.Join(";", list.Select((BaseHoldable i) => i.NetworkObjectId + ":" + StaffWorkplaceRegistry.StackContent(i)));
			text = text + ":" + (list.FirstOrDefault()?.QuickSlotDisplayName ?? string.Empty);
			if (baseHoldable != null)
			{
				text = text + ":" + string.Join(",", (from r in baseHoldable.GetComponentsInChildren<Renderer>()
					where r.enabled
					select r).Select(RendererSignature));
			}
			if (signature != text)
			{
				signature = text;
				RebuildGraphic(baseHoldable);
				if (gazed)
				{
					GazeTooltip();
				}
			}
			foreach (BaseHoldable member3 in members)
			{
				member3.ClientSetStackHidden(hidden: true);
			}
			hit.enabled = baseHoldable != null;
			label.text = "×" + list.Count;
			if (gazed && members.Count == 0)
			{
				GazeExit();
			}
		}

		private static string RendererSignature(Renderer renderer)
		{
			Mesh mesh = ((renderer is SkinnedMeshRenderer skinnedMeshRenderer) ? skinnedMeshRenderer.sharedMesh : (renderer.TryGetComponent<MeshFilter>(out var component) ? component.sharedMesh : null));
			return renderer.GetInstanceID() + ":" + ((mesh != null) ? mesh.GetInstanceID() : 0);
		}

		private IEnumerable<BaseHoldable> Selected()
		{
			return members.Where((BaseHoldable i) => i != null && StaffWorkplaceRegistry.StackType(i) == selectedType);
		}

		public BaseHoldable TakeCandidate(bool empty)
		{
			return Selected().FirstOrDefault((BaseHoldable i) => i.CanHold.Value && !i.IsStaffStackReserved && ((!empty) ? (StaffWorkplaceRegistry.StackContent(i) > 0) : (StaffWorkplaceRegistry.StackContent(i) == 0)));
		}

		private bool TryReturnToPackageStack(Interactor interactor, BaseHoldable held)
		{
			if (!held.IsSpawned || !interactor.IsSpawned)
			{
				return true;
			}
			List<BaseHoldable> source = (from p in Selected()
				where StaffStackPackage.IsPackage(p) && p.IsSpawned && p.CanHold.Value
				select p).ToList();
			if (StaffStackPackage.IsPackage(held))
			{
				BaseHoldable baseHoldable = source.FirstOrDefault((BaseHoldable p) => p != held && StaffStackPackage.Matches(p, held));
				if (baseHoldable == null)
				{
					return false;
				}
				held.ServerAddToStaffStackRpc(new NetworkObjectReference(interactor.NetworkObject), new NetworkObjectReference(baseHoldable.NetworkObject), baseHoldable.StaffStackPoint);
				return true;
			}
			List<BaseHoldable> list = source.Where((BaseHoldable p) => held.ItemSO != null && StaffStackPackage.Output(p)?.Id == held.ItemSO.Id).ToList();
			if (list.Count == 0)
			{
				return false;
			}
			List<BaseHoldable> source2 = list.Where((BaseHoldable p) => !p.IsStaffStackReserved).ToList();
			(source2.FirstOrDefault((BaseHoldable p) => StaffWorkplaceRegistry.StackContent(p) < StaffStackPackage.Capacity(p)) ?? source2.FirstOrDefault())?.TriggerCrossInput(interactor, held, InputKind.Mouse1, InputActionPhase.Started);
			return true;
		}

		private void RebuildGraphic(BaseHoldable source)
		{
			if (graphic != null)
			{
				graphic.SetActive(value: false);
				UnityEngine.Object.Destroy(graphic);
			}
			foreach (Mesh bakedMesh in bakedMeshes)
			{
				if (bakedMesh != null)
				{
					UnityEngine.Object.Destroy(bakedMesh);
				}
			}
			bakedMeshes.Clear();
			if (source == null)
			{
				return;
			}
			base.gameObject.layer = source.GetComponentInChildren<Collider>()?.gameObject.layer ?? source.gameObject.layer;
			graphic = new GameObject("Stack product visual");
			graphic.transform.SetParent(base.transform, worldPositionStays: false);
			Bounds bounds = new Bounds(point.position, Vector3.one * 0.1f);
			bool flag = false;
			Renderer[] componentsInChildren = source.GetComponentsInChildren<Renderer>();
			foreach (Renderer renderer in componentsInChildren)
			{
				if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
				{
					continue;
				}
				Mesh mesh = null;
				MeshFilter component;
				if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
				{
					mesh = new Mesh();
					skinnedMeshRenderer.BakeMesh(mesh);
					bakedMeshes.Add(mesh);
				}
				else if (renderer.TryGetComponent<MeshFilter>(out component))
				{
					mesh = component.sharedMesh;
				}
				if (!(mesh == null))
				{
					GameObject obj = new GameObject(renderer.name, typeof(MeshFilter), typeof(MeshRenderer));
					obj.transform.SetParent(graphic.transform, worldPositionStays: false);
					obj.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
					obj.transform.localScale = renderer.transform.lossyScale;
					obj.GetComponent<MeshFilter>().sharedMesh = mesh;
					MeshRenderer component2 = obj.GetComponent<MeshRenderer>();
					component2.sharedMaterials = renderer.sharedMaterials;
					MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
					renderer.GetPropertyBlock(materialPropertyBlock);
					component2.SetPropertyBlock(materialPropertyBlock);
					if (!flag)
					{
						bounds = renderer.bounds;
						flag = true;
					}
					else
					{
						bounds.Encapsulate(renderer.bounds);
					}
				}
			}
			hit.center = base.transform.InverseTransformPoint(bounds.center);
			hit.size = new Vector3(Mathf.Max(0.12f, bounds.size.x), Mathf.Max(0.08f, bounds.size.y), Mathf.Max(0.12f, bounds.size.z));
			visualBounds = bounds;
			nextVisibilityCheck = 0f;
		}

		private void LateUpdate()
		{
			Camera main = Camera.main;
			bool flag = members.Count > 0 && graphic != null && main != null && (main.transform.position - visualBounds.center).sqrMagnitude < 25f;
			if (!(label != null))
			{
				return;
			}
			Vector2 vector = default;
			flag = flag && overlay != null && overlay.TryProject(main, visualBounds.center + Vector3.up * visualBounds.extents.y * 0.65f, out vector);
			if (flag)
			{
				if (main != visibilityCamera || Time.unscaledTime >= nextVisibilityCheck)
				{
					visibilityCamera = main;
					nextVisibilityCheck = Time.unscaledTime + 0.1f;
					unobstructed = overlay.HasLineOfSight(main, visualBounds, base.transform, visibilityMembers);
				}
				label.rectTransform.anchoredPosition = vector + Vector2.up * 18f;
				Rect rect = overlay.VisibleRect(main);
				Vector2 vector2 = label.rectTransform.sizeDelta * 0.5f;
				Vector2 anchoredPosition = label.rectTransform.anchoredPosition;
				flag = unobstructed && rect.Contains(anchoredPosition - vector2) && rect.Contains(anchoredPosition + vector2) && !overlay.IsCoveredByTooltip(label.rectTransform);
			}
			label.gameObject.SetActive(flag);
		}

		public Vector3 GetWorldPosition()
		{
			if (!(graphic != null))
			{
				return base.transform.position;
			}
			return visualBounds.center;
		}

		public TooltipData GetTooltip()
		{
			List<BaseHoldable> list = Selected().ToList();
			BaseHoldable baseHoldable = list.FirstOrDefault();
			int num = list.Count((BaseHoldable i) => StaffWorkplaceRegistry.StackContent(i) == 0);
			int num2 = ((IEnumerable<BaseHoldable>)list).Sum((Func<BaseHoldable, int>)StaffWorkplaceRegistry.StackContent);
			string text = ((!(baseHoldable is GastroPan gastroPan)) ? baseHoldable?.QuickSlotDisplayName : StaffWorkplaceRegistry.Instance?.Database.FindById(gastroPan.IngredientId.ToString())?.LocalizedName);
			bool flag = baseHoldable is WholesaleEntity || baseHoldable is GastroPan || baseHoldable is SideDishIngredientEntity;
			return new TooltipData
			{
				Title = (text ?? ""),
				Body = (flag ? string.Format(StaffText.Get("stack_contents"), list.Count, num2, num) : string.Format(StaffText.Get("stack_items"), list.Count))
			};
		}

		private void GazeTooltip()
		{
			if (Singleton<ToolTipHandler>.TryGetInstance(out var instance))
			{
				instance.OnGazeEnter(this, GetWorldPosition());
			}
		}

		public void GazeEnter()
		{
			gazed = true;
			GazeTooltip();
		}

		public void GazeExit()
		{
			gazed = false;
			if (Singleton<ToolTipHandler>.TryGetInstance(out var instance))
			{
				instance.OnGazeExit(this);
			}
		}

		public void TriggerCrossInput(Interactor interactor, BaseHoldable held, InputKind inputKind, InputActionPhase phase)
		{
			HandleInput(interactor, held, inputKind, phase);
		}

		public void TriggerInput(Interactor interactor, InputKind inputKind, InputActionPhase phase)
		{
			HandleInput(interactor, null, inputKind, phase);
		}

		private void HandleInput(Interactor interactor, BaseHoldable held, InputKind inputKind, InputActionPhase phase)
		{
			if (phase != InputActionPhase.Started || interactor == null)
			{
				return;
			}
			switch (inputKind)
			{
			case InputKind.Interact:
			{
				List<string> list = members.Select(StaffWorkplaceRegistry.StackType).Distinct().ToList();
				if (list.Count > 1)
				{
					selectedType = list[(list.IndexOf(selectedType) + 1) % list.Count];
					signature = null;
				}
				break;
			}
			case InputKind.Mouse1:
			case InputKind.Mouse2:
			{
				if (inputKind == InputKind.Mouse1 && held != null && TryReturnToPackageStack(interactor, held))
				{
					break;
				}
				bool flag = Keyboard.current?.shiftKey.isPressed ?? false;
				BaseHoldable baseHoldable = ((inputKind == InputKind.Mouse2 && !flag) ? TakeCandidate(empty: false) : null);
				if (StaffStackPackage.IsPackage(baseHoldable))
				{
					if (held != null)
					{
						baseHoldable.TriggerCrossInput(interactor, held, inputKind, phase);
					}
					else
					{
						baseHoldable.TriggerInput(interactor, inputKind, phase);
					}
					break;
				}
				BaseHoldable baseHoldable2 = TakeCandidate(inputKind == InputKind.Mouse2);
				if (baseHoldable2 != null)
				{
					interactor.TryHold(baseHoldable2);
				}
				break;
			}
			}
		}

		private void OnDisable()
		{
			GazeExit();
			if (label != null)
			{
				label.gameObject.SetActive(value: false);
			}
		}

		private void OnDestroy()
		{
			GazeExit();
			if (label != null)
			{
				UnityEngine.Object.Destroy(label.gameObject);
			}
			foreach (BaseHoldable member in members)
			{
				if (member != null)
				{
					member.ClientSetStackHidden(hidden: false);
				}
			}
			foreach (Mesh bakedMesh in bakedMeshes)
			{
				if (bakedMesh != null)
				{
					UnityEngine.Object.Destroy(bakedMesh);
				}
			}
		}
	}
}
