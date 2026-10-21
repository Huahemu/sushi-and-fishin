using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Bozo.Utilities
{
	public class BoZo_EventSystemHandler : MonoBehaviour
	{
		private void Awake()
		{
			InputSystemUIInputModule inputSystemUIInputModule = GetComponent<InputSystemUIInputModule>();
			StandaloneInputModule component = GetComponent<StandaloneInputModule>();
			if (inputSystemUIInputModule == null)
			{
				inputSystemUIInputModule = base.gameObject.AddComponent<InputSystemUIInputModule>();
			}
			if (component != null)
			{
				component.enabled = false;
			}
			inputSystemUIInputModule.enabled = true;
		}
	}
}
