namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;
using global::UnityEngine.Events;
using global::UnityEngine.Serialization;

namespace AirFishLab.ScrollingList
{
	[Serializable]
	public class ListSetting
	{
		[SerializeField]
		[Tooltip("The type of the list.")]
		private CircularScrollingList.ListType _listType;

		[SerializeField]
		[Tooltip("The major moving direction of the list.")]
		private CircularScrollingList.Direction _direction;

		[SerializeField]
		[Tooltip("The controlling mode of the list.")]
		private CircularScrollingList.ControlMode _controlMode = CircularScrollingList.ControlMode.Everything;

		[SerializeField]
		[Tooltip("The focusing position of the list")]
		private CircularScrollingList.FocusingPosition _focusingPosition = CircularScrollingList.FocusingPosition.Center;

		[SerializeField]
		[Tooltip("To show the list contents in the reversed order. Available when the 'FocusingPosition' is 'center'")]
		[FormerlySerializedAs("_reverseOrder")]
		private bool _reverseContentOrder;

		[SerializeField]
		[Tooltip("To align a box at the focusing position after sliding")]
		[FormerlySerializedAs("_alignMiddle")]
		[FormerlySerializedAs("_alignInCenter")]
		private bool _alignAtFocusingPosition;

		[SerializeField]
		[Tooltip("To reverse the scrolling direction")]
		[FormerlySerializedAs("_reverseDirection")]
		private bool _reverseScrollingDirection;

		[SerializeField]
		[Tooltip("Specify the initial content ID at the focusing position")]
		[FormerlySerializedAs(" _centeredContentID")]
		private int _initFocusingContentID;

		[SerializeField]
		[Tooltip("Move the selected box to the focusing position")]
		[FormerlySerializedAs("_centerSelectedBox")]
		private bool _focusSelectedBox;

		[SerializeField]
		[Tooltip("Whether to initialize the list on Start or not. If set to false, manually call Initialize() to initialize the list.")]
		private bool _initializeOnStart = true;

		[SerializeField]
		[Tooltip("The factor that adjusting the distance between boxes. The larger, the closer.")]
		private float _boxDensity = 1f;

		[SerializeField]
		[Tooltip("The curve specifying the passive position of the box. The x axis is the major position of the box, which is mapped to [-1, 1]. The y axis defines the factor of the passive position of the box. Point (0, 0) is the center of the list layout.")]
		private AnimationCurve _boxPositionCurve = AnimationCurve.Constant(-1f, 1f, 0f);

		[SerializeField]
		[Tooltip("The curve specifying the box scale. The x axis is the major position of the box, which is mapped to [-1, 1]. The y axis specifies the value of 'localScale' of the box at the corresponding position.")]
		private AnimationCurve _boxScaleCurve = AnimationCurve.Constant(-1f, 1f, 1f);

		[SerializeField]
		[Tooltip("The curve specifying the velocity factor of the box after releasing. The x axis is the the moving duration in seconds, which starts from 0. The y axis is the factor of releasing velocity.")]
		private AnimationCurve _boxVelocityCurve = new AnimationCurve(new Keyframe(0f, 1f, 0f, -2.5f), new Keyframe(1f, 0f, 0f, 0f));

		[SerializeField]
		[Tooltip("The curve specifying the movement factor of the box. The x axis is the moving duration in seconds, which starts from 0. The y axis is the lerping factor for reaching the target position.")]
		private AnimationCurve _boxMovementCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 8f), new Keyframe(0.25f, 1f, 0f, 0f));

		[SerializeField]
		[Tooltip("The callback to be invoked when a box is selected. The registered callbacks will be added to the 'onClick' event of boxes, therefore, boxes should be 'Button's.")]
		private ListBoxSelectedEvent _onBoxSelected;

		[SerializeField]
		[Tooltip("The callback to be invoked when the focusing box is changed. The first argument is previous focusing box, and the second one is current focusing box.")]
		[FormerlySerializedAs("_onCenteredBoxChanged")]
		private ListTwoBoxesEvent _onFocusingBoxChanged;

		[SerializeField]
		[Tooltip("The callback to be invoked when the movement is ended")]
		private UnityEvent _onMovementEnd;

		private string _name;

		private bool _isInitialized;

		public CircularScrollingList.ListType ListType => _listType;

		public CircularScrollingList.Direction Direction => _direction;

		public CircularScrollingList.ControlMode ControlMode => _controlMode;

		public bool AlignAtFocusingPosition => _alignAtFocusingPosition;

		public bool ReverseScrollingDirection => _reverseScrollingDirection;

		public CircularScrollingList.FocusingPosition FocusingPosition => _focusingPosition;

		public bool ReverseContentOrder => _reverseContentOrder;

		public int InitFocusingContentID => _initFocusingContentID;

		public bool FocusSelectedBox => _focusSelectedBox;

		public bool InitializeOnStart => _initializeOnStart;

		public float BoxDensity => _boxDensity;

		public AnimationCurve BoxPositionCurve => _boxPositionCurve;

		public AnimationCurve BoxScaleCurve => _boxScaleCurve;

		public AnimationCurve BoxVelocityCurve => _boxVelocityCurve;

		public AnimationCurve BoxMovementCurve => _boxMovementCurve;

		public ListBoxSelectedEvent OnBoxSelected => _onBoxSelected;

		public ListTwoBoxesEvent OnFocusingBoxChanged => _onFocusingBoxChanged;

		public UnityEvent OnMovementEnd => _onMovementEnd;

		public void SetListType(CircularScrollingList.ListType listType)
		{
			if (!CheckIsInitialized())
			{
				_listType = listType;
			}
		}

		public void SetDirection(CircularScrollingList.Direction direction)
		{
			if (!CheckIsInitialized())
			{
				_direction = direction;
			}
		}

		public void SetControlMode(CircularScrollingList.ControlMode controlMode)
		{
			if (!CheckIsInitialized())
			{
				_controlMode = controlMode;
			}
		}

		public void SetAlignAtFocusingPosition(bool toAlign)
		{
			if (!CheckIsInitialized())
			{
				_alignAtFocusingPosition = toAlign;
			}
		}

		public void SetReverseScrollingDirection(bool toReverse)
		{
			if (!CheckIsInitialized())
			{
				_reverseScrollingDirection = toReverse;
			}
		}

		public void SetFocusingPosition(CircularScrollingList.FocusingPosition focusingPosition)
		{
			if (!CheckIsInitialized())
			{
				_focusingPosition = focusingPosition;
			}
		}

		public void SetReverseContentOrder(bool toReverse)
		{
			if (!CheckIsInitialized())
			{
				_reverseContentOrder = toReverse;
			}
		}

		public void SetInitFocusingContentID(int contentID)
		{
			if (!CheckIsInitialized())
			{
				_initFocusingContentID = contentID;
			}
		}

		public void SetFocusSelectedBox(bool toFocus)
		{
			if (!CheckIsInitialized())
			{
				_focusSelectedBox = toFocus;
			}
		}

		public void SetBoxDensity(float boxDensity)
		{
			if (!CheckIsInitialized())
			{
				_boxDensity = boxDensity;
			}
		}

		public void SetBoxPositionCurve(AnimationCurve curve)
		{
			if (!CheckIsInitialized())
			{
				_boxPositionCurve = curve;
			}
		}

		public void SetBoxScaleCurve(AnimationCurve curve)
		{
			if (!CheckIsInitialized())
			{
				_boxScaleCurve = curve;
			}
		}

		public void SetBoxVelocityCurve(AnimationCurve curve)
		{
			if (!CheckIsInitialized())
			{
				_boxVelocityCurve = curve;
			}
		}

		public void SetBoxMovementCurve(AnimationCurve curve)
		{
			if (!CheckIsInitialized())
			{
				_boxMovementCurve = curve;
			}
		}

		public void AddOnBoxSelectedCallback(UnityAction<ListBox> callback)
		{
			_onBoxSelected.AddListener(callback);
		}

		public void RemoveOnBoxSelectedCallback(UnityAction<ListBox> callback)
		{
			_onBoxSelected.RemoveListener(callback);
		}

		public void AddOnFocusingBoxChangedCallback(UnityAction<ListBox, ListBox> callback)
		{
			_onFocusingBoxChanged.AddListener(callback);
		}

		public void RemoveOnFocusingBoxChangedCallback(UnityAction<ListBox, ListBox> callback)
		{
			_onFocusingBoxChanged.RemoveListener(callback);
		}

		public void AddOnMovementEndCallback(UnityAction callback)
		{
			_onMovementEnd.AddListener(callback);
		}

		public void RemoveOnMovementEndCallback(UnityAction callback)
		{
			_onMovementEnd.RemoveListener(callback);
		}

		private bool CheckIsInitialized()
		{
			if (_isInitialized)
			{
				Debug.LogWarning("The list setting of the list '" + _name + "' is initialized. Skip");
			}
			return _isInitialized;
		}

		public void Initialize(BaseListBank listBank, string name)
		{
			int contentCount = listBank.GetContentCount();
			if (_initFocusingContentID < 0 || (contentCount > 0 && _initFocusingContentID >= contentCount))
			{
				throw new IndexOutOfRangeException("The 'InitFocusingContentID' is negative or greater than the number of contents in the list bank in the list '" + name + "'.");
			}
			if (Mathf.Approximately(_boxDensity, 0f))
			{
				throw new InvalidOperationException("The 'BoxDensity' shouldn't be 0 in the list '" + name + "'");
			}
			switch (_focusingPosition)
			{
			case CircularScrollingList.FocusingPosition.Top:
				_reverseContentOrder = false;
				break;
			case CircularScrollingList.FocusingPosition.Bottom:
				_reverseContentOrder = true;
				break;
			}
			_name = name;
			_isInitialized = true;
		}
	}
}

}
