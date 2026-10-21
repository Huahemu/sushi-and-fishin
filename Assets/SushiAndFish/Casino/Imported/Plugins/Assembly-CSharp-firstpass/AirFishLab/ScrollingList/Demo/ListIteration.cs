namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::UnityEngine;

namespace AirFishLab.ScrollingList.Demo
{
	public class ListIteration : MonoBehaviour
	{
		[SerializeField]
		private CircularScrollingList _list;

		[SerializeField]
		private int _step = 1;

		[SerializeField]
		private float _stepInterval = 0.1f;

		private int _currentID;

		private void Start()
		{
			_list.SetInteractable(interactable: false);
			StartCoroutine(IterationLoop());
		}

		private IEnumerator IterationLoop()
		{
			while (true)
			{
				_list.SelectContentID(_currentID);
				_currentID = (int)Mathf.Repeat(_currentID + _step, _list.ListBank.GetContentCount());
				yield return new WaitForSeconds(_stepInterval);
			}
		}
	}
}

}
