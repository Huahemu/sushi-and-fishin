using UnityEngine;

namespace Threepeat
{
	public class ObjectCycler : MonoBehaviour
	{
		public GameObject[] objects;

		public int currIndex;

		private float lastChangeTime;

		public float changeInterval = 8f;

		public KeyCode resetHotkey = KeyCode.R;

		private void Start()
		{
		}

		private void Update()
		{
			if (objects.Length == 0)
			{
				return;
			}
			if (Input.GetKeyDown(resetHotkey))
			{
				lastChangeTime = Time.time;
				objects[currIndex].SetActive(value: false);
				currIndex = 0;
				objects[currIndex].SetActive(value: true);
			}
			else if (Time.time - lastChangeTime > changeInterval)
			{
				objects[currIndex].SetActive(value: false);
				currIndex++;
				if (currIndex >= objects.Length)
				{
					currIndex = 0;
				}
				objects[currIndex].SetActive(value: true);
				lastChangeTime = Time.time;
			}
		}
	}
}
