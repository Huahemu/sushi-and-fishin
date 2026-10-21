namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::JetBrains.Annotations;
using global::Mirror;
using global::UnityEngine;

public class PlayerInteract : NetworkBehaviour
{
	public float raycastDistance = 3f;

	public float raycastRadius = 0.1f;

	public float raycastBlockThreshold = 0.5f;

	public LayerMask raycastLayer = -1;

	public IInteractable TargetInteractable;

	private readonly RaycastHit[] _hits = new RaycastHit[64];

	private float _holdInteractTimer;

	private bool _hasHold;

	private PlayerInventory _pi;

	private PlayerController _pc;

	private Camera _cam;

	private bool _isLocked;

	private InteractionUIPanel _interactionUIPanel;

	private ItemPriceDisplayManager _priceDisplayManager;

	private ItemDescriptionDisplayManager _descriptionDisplayManager;

	private Item _currentlyHoveredItem;

	private PlayerHandButtonAnimation _playerHandButtonAnimation;

	private bool IsLocked
	{
		get
		{
			bool flag = (bool)_pi.NetworkholdingItem || (_pc.State == PlayerController.PlayerState.Ragdoll && _pc.hasBody);
			if (_isLocked != flag)
			{
				_isLocked = flag;
				if (_isLocked)
				{
					SetTargetInteractable(null);
					if (MonoSingleton<CursorManager>.Instance != null)
					{
						MonoSingleton<CursorManager>.Instance.SetCursorType(CursorManager.CursorType.Default);
					}
				}
			}
			return _isLocked;
		}
	}

	public event Action<IInteractable> OnTargetInteractableChanged;

	private void Awake()
	{
		_cam = MonoSingleton<LocalManager>.Instance.mainCamera;
		_pc = GetComponent<PlayerController>();
		_pi = GetComponent<PlayerInventory>();
		_playerHandButtonAnimation = GetComponent<PlayerHandButtonAnimation>();
		_priceDisplayManager = UnityEngine.Object.FindFirstObjectByType<ItemPriceDisplayManager>();
		_descriptionDisplayManager = UnityEngine.Object.FindFirstObjectByType<ItemDescriptionDisplayManager>();
	}

	private void OnEnable()
	{
		InputEvents.OnInteractEvent = (Action<bool>)Delegate.Combine(InputEvents.OnInteractEvent, new Action<bool>(OnInteractEvent));
		OnTargetInteractableChanged += OnTargetInteractableChangedHandler;
	}

	private void OnDisable()
	{
		InputEvents.OnInteractEvent = (Action<bool>)Delegate.Remove(InputEvents.OnInteractEvent, new Action<bool>(OnInteractEvent));
		OnTargetInteractableChanged -= OnTargetInteractableChangedHandler;
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		if (!base.isLocalPlayer)
		{
			base.enabled = false;
			return;
		}
		if (MonoSingleton<LocalManager>.Instance != null)
		{
			_interactionUIPanel = MonoSingleton<LocalManager>.Instance.interactionUIPanel;
		}
		MonoSingleton<CursorManager>.Instance.SetCursorType(CursorManager.CursorType.Default);
	}

	private void Update()
	{
        if (CasinoRuntimeContext.IsHostIntegration && CasinoRuntimeContext.CanInteract?.Invoke() != true) { SetTargetInteractable(null); return; }
		if (!IsLocked)
		{
			RaycastInteractable();
			HoldInteract();
			UpdateInteractionUI();
		}
	}

	private void RaycastInteractable()
	{
		// A wide sphere can overlap adjacent cells on a small game board. Give
		// the exact crosshair ray priority, so aiming at one tile cannot choose
		// its neighbour merely because the neighbour's pivot is closer.
		var ray = new Ray(_cam.transform.position, _cam.transform.forward);
		int direct = Physics.RaycastNonAlloc(ray, _hits, raycastDistance, raycastLayer, QueryTriggerInteraction.Collide);
		Array.Sort(_hits, 0, direct, Comparer<RaycastHit>.Create((a,b)=>a.distance.CompareTo(b.distance)));
		float firstSolid = float.MaxValue;
		for(int i=0;i<direct;i++) {
			var hit=_hits[i];
			if(hit.transform==transform || (CasinoRuntimeContext.PlayerRoot && hit.transform.IsChildOf(CasinoRuntimeContext.PlayerRoot)))continue;
			if(hit.distance>firstSolid+raycastBlockThreshold)break;
			var target=hit.collider.GetComponent<IInteractable>() ?? hit.collider.GetComponentInParent<IInteractable>();
			if(target!=null){SetTargetInteractable(target);return;}
			if(!hit.collider.isTrigger)firstSolid=Mathf.Min(firstSolid,hit.distance);
		}
		int num = Physics.SphereCastNonAlloc(ray, raycastRadius, _hits, raycastDistance, raycastLayer, QueryTriggerInteraction.Collide);
		Array.Sort(_hits, 0, num, Comparer<RaycastHit>.Create((RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance)));
		IInteractable targetInteractable = null;
		float num2 = float.MaxValue;
		float num3 = raycastDistance + 1f;
		for (int num4 = 0; num4 < num; num4++)
		{
			RaycastHit raycastHit = _hits[num4];
			if (raycastHit.transform == base.transform || (CasinoRuntimeContext.PlayerRoot && raycastHit.transform.IsChildOf(CasinoRuntimeContext.PlayerRoot)))
			{
				continue;
			}
			if (raycastHit.distance > num3 + raycastBlockThreshold)
			{
				break;
			}
			var component=raycastHit.collider.GetComponent<IInteractable>() ?? raycastHit.collider.GetComponentInParent<IInteractable>();
			if (component!=null)
			{
				Vector3 to = raycastHit.collider.bounds.center - _cam.transform.position;
				float num5 = Vector3.Angle(_cam.transform.forward, to);
				if (num5 < num2)
				{
					num2 = num5;
					targetInteractable = component;
				}
			}
			if (!raycastHit.collider.isTrigger && num3 >= raycastDistance)
			{
				num3 = raycastHit.distance;
			}
		}
		SetTargetInteractable(targetInteractable);
	}

	private void SetTargetInteractable([CanBeNull] IInteractable interactable)
	{
		if (TargetInteractable == interactable)
		{
			return;
		}
		if (_currentlyHoveredItem != null)
		{
			if (_priceDisplayManager != null)
			{
				_priceDisplayManager.HidePriceForItem(_currentlyHoveredItem);
			}
			if (_descriptionDisplayManager != null)
			{
				_descriptionDisplayManager.HideDescriptionForItem(_currentlyHoveredItem);
			}
			_currentlyHoveredItem = null;
		}
		TargetInteractable?.OnHoverExit(this);
		if (TargetInteractable != null)
		{
			TargetInteractable.OnInteractableChanged -= OnTargetInteractableDataChangedHandler;
		}
		TargetInteractable = interactable;
		if (TargetInteractable != null)
		{
			TargetInteractable.OnInteractableChanged += OnTargetInteractableDataChangedHandler;
		}
		TargetInteractable?.OnHover(this);
		Item item = interactable as Item;
		if (item != null && (item.spawnableSo != null || item.GetComponent<GachaSphere>() != null))
		{
			_currentlyHoveredItem = item;
			if (_priceDisplayManager != null)
			{
				_priceDisplayManager.ShowPriceForItem(item);
			}
			if (_descriptionDisplayManager != null && item.ShouldShowHoverDescription)
			{
				_descriptionDisplayManager.ShowDescriptionForItem(item);
			}
		}
		ResetInteract();
		OnTargetInteractableChanged?.Invoke(TargetInteractable);
	}

	private void OnTargetInteractableDataChangedHandler(IInteractable interactable)
	{
		TargetInteractable?.OnHoverExit(this);
		TargetInteractable?.OnHover(this);
		OnTargetInteractableChanged?.Invoke(interactable);
	}

	private void HoldInteract()
	{
		if (!InputEvents.IsInteractPressed || TargetInteractable == null || !TargetInteractable.IsInteractable || !TargetInteractable.MeetRequirements || !TargetInteractable.HoldInteract || _hasHold)
		{
			return;
		}
		if (_holdInteractTimer < TargetInteractable.HoldDuration)
		{
			_holdInteractTimer += Time.deltaTime;
			TargetInteractable?.OnHold(this);
			if (TargetInteractable != null)
			{
				TargetInteractable.HoldProgress = Mathf.Clamp01(_holdInteractTimer / TargetInteractable.HoldDuration);
			}
		}
		else
		{
			_hasHold = true;
			_holdInteractTimer = 0f;
			TargetInteractable?.OnHoldExit(this);
			TargetInteractable?.OnInteract(this);
		}
	}

	private void UpdateInteractionUI()
	{
		if (!(_interactionUIPanel == null) && TargetInteractable != null)
		{
			if (TargetInteractable.IsBeingHold && TargetInteractable.HoldInteract)
			{
				_interactionUIPanel.UpdateProgressBar(TargetInteractable.HoldProgress);
			}
			else
			{
				_interactionUIPanel.UpdateProgressBar(0f);
			}
		}
	}

	private void OnTargetInteractableChangedHandler(IInteractable interactable)
	{
		UpdateInteractionUI(interactable);
		UpdateCursor(interactable);
	}

	private void UpdateInteractionUI(IInteractable interactable)
	{
		if (!(_interactionUIPanel == null))
		{
			if (interactable != null && interactable.IsInteractable && interactable.MeetRequirements)
			{
				_interactionUIPanel.SetItemNameText(interactable.InteractableName);
				_interactionUIPanel.SetTooltip(interactable.TooltipMessage);
				_interactionUIPanel.UpdateProgressBar(0f);
			}
			else
			{
				_interactionUIPanel.ResetUI();
			}
		}
	}

	private void UpdateCursor(IInteractable interactable)
	{
		if (!(MonoSingleton<CursorManager>.Instance == null))
		{
			if (interactable == null || !interactable.IsInteractable || !interactable.MeetRequirements)
			{
				MonoSingleton<CursorManager>.Instance.SetCursorType(CursorManager.CursorType.Default);
				return;
			}
			CursorManager.CursorType cursorType = interactable.CursorType;
			MonoSingleton<CursorManager>.Instance.SetCursorType(cursorType);
		}
	}

	private void OnInteractEvent(bool performed)
	{
        if (CasinoRuntimeContext.IsHostIntegration && performed && CasinoRuntimeContext.CanInteract?.Invoke() != true) return;
		if (performed)
		{
			if (TargetInteractable == null || !TargetInteractable.IsInteractable || !TargetInteractable.MeetRequirements || TargetInteractable.HoldInteract)
			{
				return;
			}
			if (_playerHandButtonAnimation != null && !_pi.NetworkholdingItem && !(TargetInteractable is Item))
			{
				MonoBehaviour monoBehaviour = TargetInteractable as MonoBehaviour;
				if (monoBehaviour != null)
				{
					_playerHandButtonAnimation.PressButton(monoBehaviour.transform);
				}
			}
			TargetInteractable?.OnInteract(this);
		}
		else
		{
			ResetInteract();
		}
	}

	private void ResetInteract()
	{
		_holdInteractTimer = 0f;
		_hasHold = false;
		TargetInteractable?.OnHoldExit(this);
		if (_interactionUIPanel != null && TargetInteractable != null)
		{
			_interactionUIPanel.UpdateProgressBar(0f);
		}
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
