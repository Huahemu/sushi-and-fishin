namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ContentManagement;
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ListStateProcessing;
using global::UnityEngine;
using global::UnityEngine.EventSystems;
using global::UnityEngine.Serialization;
using global::UnityEngine.UI;

namespace AirFishLab.ScrollingList
{
	public class CircularScrollingList : MonoBehaviour, IBeginDragHandler, IEventSystemHandler, IDragHandler, IEndDragHandler, IScrollHandler
	{
		public enum ListType
		{
			Circular = 0,
			Linear = 1
		}

		[Flags]
		public enum ControlMode
		{
			Pointer = 1,
			MouseWheel = 2,
			Everything = Pointer | MouseWheel
		}

		public enum FocusingPosition
		{
			Top = 0,
			Center = 1,
			Bottom = 2
		}

		public enum Direction
		{
			Vertical = 0,
			Horizontal = 1
		}

		[SerializeField]
		[Tooltip("The object that stores the contents for the list to display. It should be derived from the class BaseListBank.")]
		private BaseListBank _listBank;

		[SerializeField]
		private ListBoxSetting _boxSetting;

		[SerializeField]
		[Tooltip("The objects that are used for displaying the content. They should be derived from the class ListBox")]
		private List<ListBox> _listBoxes;

		[SerializeField]
		[FormerlySerializedAs("_setting")]
		[Tooltip("The setting of this list")]
		public ListSetting _listSetting;

		private RectTransform _rectTransform;

		private Camera _canvasRefCamera;

		private ControlMode _controlMode;

		private InputProcessor _inputProcessor;

		private IListMovementProcessor _listMovementProcessor;

		private IListBoxController _listBoxController;

		private ListContentProvider _listContentProvider;

		private bool _isInitialized;

		private bool _isInteractable = true;

		private bool _hasNoContent;

		private bool _isMoving;

		public BaseListBank ListBank => _listBank;

		public ListBox[] ListBoxes => _listBoxes.ToArray();

		public ListBoxSetting BoxSetting => _boxSetting;

		public ListSetting ListSetting => _listSetting;

		public bool IsInteractable => _isInteractable;

		private void Reset()
		{
			if (_boxSetting == null)
			{
				_boxSetting = new ListBoxSetting();
			}
			_boxSetting.BoxRootTransform = base.transform;
		}

		private void Start()
		{
			if (_listSetting.InitializeOnStart)
			{
				Initialize();
			}
		}

		public void SetListBank(BaseListBank listBank)
		{
			if (!CheckIsInitialized())
			{
				_listBank = listBank;
			}
		}

		public void Initialize()
		{
			if (!CheckIsInitialized())
			{
				Validate();
				_boxSetting.Initialize(base.gameObject);
				_listSetting.Initialize(_listBank, base.name);
				GetComponentReference();
				SetListBoxes();
				InitializeMembers();
				_isInitialized = true;
			}
		}

		private bool CheckIsInitialized()
		{
			if (_isInitialized)
			{
				Debug.LogWarning("The list '" + base.name + "' is initialized. Skip.");
			}
			return _isInitialized;
		}

		private void Validate()
		{
			if (!_listBank)
			{
				throw new UnassignedReferenceException("The 'ListBank' is not assigned in the list '" + base.name + "'");
			}
		}

		private void GetComponentReference()
		{
			_rectTransform = GetComponent<RectTransform>();
			Canvas componentInParent = GetComponentInParent<Canvas>();
			if (componentInParent.renderMode != RenderMode.ScreenSpaceOverlay)
			{
				_canvasRefCamera = componentInParent.worldCamera;
			}
		}

		private void InitializeMembers()
		{
			if (_listSetting.FocusSelectedBox)
			{
				_listSetting.OnBoxSelected.AddListener((ListBox box) =>
				{
					SelectContentID(box.ContentID, notToIgnore: false);
				});
			}
			_controlMode = _listSetting.ControlMode;
			_inputProcessor = new InputProcessor(_rectTransform, _canvasRefCamera);
			_listContentProvider = new ListContentProvider(_listSetting, _listBank, _listBoxes.Count);
			_hasNoContent = _listContentProvider.GetContentCount() == 0;
			ListStateProcessorManager.GetProcessors(new ListSetupData(this, _listSetting, _rectTransform, _canvasRefCamera, new List<IListBox>(_listBoxes), _listContentProvider), out _listMovementProcessor, out _listBoxController);
		}

		private void SetListBoxes()
		{
			ListBox boxPrefab = _boxSetting.BoxPrefab;
			Transform boxRootTransform = _boxSetting.BoxRootTransform;
			int numOfBoxes = _boxSetting.NumOfBoxes;
			for (int i = ReassignListBoxes(_listBoxes, boxRootTransform, numOfBoxes); i < numOfBoxes; i++)
			{
				ListBox item = GenerateListBox(boxPrefab, boxRootTransform, i);
				_listBoxes.Add(item);
			}
		}

		private int ReassignListBoxes(List<ListBox> listBoxes, Transform rootTransform, int desiredNumOfBoxes)
		{
			List<ListBox> list = new List<ListBox>();
			foreach (Transform item in rootTransform)
			{
				if (item.TryGetComponent<ListBox>(out var component))
				{
					list.Add(component);
				}
			}
			int count = list.Count;
			if (count > desiredNumOfBoxes)
			{
				Debug.LogWarning("The number of existing boxes are more than the number of desired boxes in the list '" + base.name + "'");
			}
			int num = Mathf.Min(count, desiredNumOfBoxes);
			listBoxes.Clear();
			for (int i = 0; i < num; i++)
			{
				listBoxes.Add(list[i]);
			}
			return num;
		}

		private static ListBox GenerateListBox(ListBox prefab, Transform rootTransform, int index)
		{
			ListBox listBox = UnityEngine.Object.Instantiate(prefab, rootTransform);
			listBox.name = $"{prefab.name} ({index})";
			return listBox;
		}

		public void SetInteractable(bool interactable)
		{
			_isInteractable = interactable;
		}

		public void MoveOneUnitUp()
		{
			if (!_hasNoContent)
			{
				SetUnitMovement(1);
			}
		}

		public void MoveOneUnitDown()
		{
			if (!_hasNoContent)
			{
				SetUnitMovement(-1);
			}
		}

		public void UpdateBoxOpacities()
		{
			ListBox focusingBox = GetFocusingBox();
			int index = (_listBoxes.IndexOf(focusingBox) - 1 + _listBoxes.Count) % _listBoxes.Count;
			ListBox listBox = _listBoxes[index];
			foreach (ListBox listBox2 in _listBoxes)
			{
				Text componentInChildren = listBox2.GetComponentInChildren<Text>();
				if (componentInChildren != null)
				{
					componentInChildren.color = ((listBox2 == listBox) ? new Color(componentInChildren.color.r, componentInChildren.color.g, componentInChildren.color.b, 1f) : new Color(componentInChildren.color.r, componentInChildren.color.g, componentInChildren.color.b, 0.05f));
				}
			}
		}

		public void EndMovement()
		{
			if (!_listMovementProcessor.IsMovementEnded())
			{
				bool flag = _listMovementProcessor.NeedToAlign();
				_listMovementProcessor.EndMovement(flag);
				if (!flag)
				{
					_listSetting.OnMovementEnd.Invoke();
					_isMoving = false;
				}
			}
		}

		public ListBox GetFocusingBox()
		{
			return _listBoxController.GetFocusingBox() as ListBox;
		}

		public int GetFocusingContentID()
		{
			return _listBoxController.GetFocusingBox().ContentID;
		}

		public void Refresh(int focusingContentID = -1)
		{
			_hasNoContent = _listContentProvider.GetContentCount() == 0;
			_listBoxController.RefreshBoxes(focusingContentID);
		}

		public void SelectContentID(int contentID, bool notToIgnore = true)
		{
			if (!_hasNoContent)
			{
				if (!_listContentProvider.IsIDValid(contentID))
				{
					throw new IndexOutOfRangeException("'contentID' is invalid");
				}
				int focusingContentID = GetFocusingContentID();
				int shortestIDDiff = _listContentProvider.GetShortestIDDiff(focusingContentID, contentID);
				SetSelectionMovement(shortestIDDiff, notToIgnore);
			}
		}

		public void OnBeginDrag(PointerEventData eventData)
		{
			if (_controlMode.HasFlag(ControlMode.Pointer))
			{
				SetMovement(eventData, InputPhase.Began);
			}
		}

		public void OnDrag(PointerEventData eventData)
		{
			if (_controlMode.HasFlag(ControlMode.Pointer))
			{
				SetMovement(eventData, InputPhase.Moved);
			}
		}

		public void OnEndDrag(PointerEventData eventData)
		{
			if (_controlMode.HasFlag(ControlMode.Pointer))
			{
				SetMovement(eventData, InputPhase.Ended);
			}
		}

		public void OnScroll(PointerEventData eventData)
		{
			if (_controlMode.HasFlag(ControlMode.MouseWheel))
			{
				SetMovement(eventData, InputPhase.Scrolled);
			}
		}

		private void Update()
		{
			if (_isInitialized && _isMoving)
			{
				float movement = _listMovementProcessor.GetMovement(Time.deltaTime);
				_listBoxController.UpdateBoxes(movement);
				if (_listMovementProcessor.IsMovementEnded())
				{
					_listSetting.OnMovementEnd.Invoke();
					_isMoving = false;
				}
			}
		}

		private bool ToIgnoreMovement()
		{
			if (!_hasNoContent)
			{
				return !_isInteractable;
			}
			return true;
		}

		private void SetMovement(PointerEventData eventData, InputPhase phase)
		{
			if (!ToIgnoreMovement())
			{
				InputInfo inputInfo = _inputProcessor.GetInputInfo(eventData, phase);
				_listMovementProcessor.SetMovement(inputInfo);
				_isMoving = true;
			}
		}

		private void SetUnitMovement(int unit)
		{
			if (!ToIgnoreMovement())
			{
				_listMovementProcessor.SetUnitMovement(unit);
				_isMoving = true;
			}
		}

		private void SetSelectionMovement(int shortestIDDiff, bool notToIgnore)
		{
			if (notToIgnore || !ToIgnoreMovement())
			{
				_listMovementProcessor.SetSelectionMovement(shortestIDDiff);
				_isMoving = true;
			}
		}
	}
}

}
