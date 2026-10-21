namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;

namespace AirFishLab.ScrollingList
{
	[Serializable]
	public class ListBoxSetting
	{
		[SerializeField]
		[Tooltip("The root transform that holding the list boxes")]
		private Transform _boxRootTransform;

		[SerializeField]
		[Tooltip("The prefab of the box")]
		private ListBox _boxPrefab;

		[SerializeField]
		[Min(1f)]
		[Tooltip("The number of boxes to be generated")]
		private int _numOfBoxes = 5;

		private string _name;

		private bool _isInitialized;

		public Transform BoxRootTransform
		{
			get
			{
				return _boxRootTransform;
			}
			set
			{
				_boxRootTransform = value;
			}
		}

		public ListBox BoxPrefab => _boxPrefab;

		public int NumOfBoxes => _numOfBoxes;

		public void SetBoxRootTransform(RectTransform rootTransform)
		{
			if (!CheckIsInitialized())
			{
				_boxRootTransform = rootTransform;
			}
		}

		public void SetBoxPrefab(ListBox boxPrefab)
		{
			if (!CheckIsInitialized())
			{
				_boxPrefab = boxPrefab;
			}
		}

		public void SetNumOfBoxes(int numOfBoxes)
		{
			if (!CheckIsInitialized())
			{
				_numOfBoxes = numOfBoxes;
			}
		}

		private bool CheckIsInitialized()
		{
			if (_isInitialized)
			{
				Debug.LogWarning("The list setting of the list '" + _name + "' is initialized. Skip");
			}
			return _isInitialized;
		}

		public void Initialize(GameObject listObject)
		{
			string name = listObject.name;
			if (!BoxRootTransform)
			{
				Debug.LogWarning("The 'BoxRootTransform' is not assigned in the list '" + name + "'. Use itself as the 'BoxRootTransform'");
				BoxRootTransform = listObject.transform;
			}
			if (!BoxPrefab)
			{
				throw new UnassignedReferenceException("The 'BoxPrefab' is not assigned in the list '" + name + "'");
			}
			if (NumOfBoxes <= 0)
			{
				throw new InvalidOperationException("The 'NumOfBoxes' is 0 or negative in the list '" + name + "'");
			}
			_name = name;
			_isInitialized = true;
		}
	}
}

}
