using UnityEngine;
using UnityEngine.EventSystems;

namespace Michsky.MUIP
{
	[AddComponentMenu("Modern UI Pack/Tools/Element Tabbing")]
	public class ElementTabbing : MonoBehaviour
	{
		public enum ObjectType
		{
			Button = 0,
			InputField = 1
		}

		private int currentIndex = -1;

		private bool catchedObject;

		private ObjectType type;

		private void Update()
		{
			if (!Input.GetKeyDown(KeyCode.Tab))
			{
				return;
			}
			if (currentIndex > base.transform.childCount - 2)
			{
				SelectElement(0);
				return;
			}
			if (catchedObject && type == ObjectType.Button)
			{
				base.transform.GetChild(currentIndex).GetComponent<ButtonManager>().OnDeselect(null);
			}
			else if (catchedObject && type == ObjectType.InputField)
			{
				base.transform.GetChild(currentIndex).GetComponent<CustomInputField>().inputText.DeactivateInputField();
			}
			currentIndex++;
			for (int i = 0; i < base.transform.childCount; i++)
			{
				if (i >= currentIndex)
				{
					if (base.transform.GetChild(i).GetComponent<ButtonManager>() != null)
					{
						base.transform.GetChild(i).GetComponent<ButtonManager>().OnSelect(null);
						EventSystem.current.SetSelectedGameObject(base.transform.GetChild(i).gameObject);
						type = ObjectType.Button;
						break;
					}
					if (base.transform.GetChild(i).GetComponent<CustomInputField>() != null)
					{
						base.transform.GetChild(i).GetComponent<CustomInputField>().inputText.ActivateInputField();
						EventSystem.current.SetSelectedGameObject(base.transform.GetChild(i).gameObject);
						type = ObjectType.InputField;
						break;
					}
					catchedObject = false;
				}
			}
		}

		private void SelectElement(int index)
		{
			currentIndex = index;
			if (base.transform.GetChild(index).GetComponent<ButtonManager>() != null)
			{
				base.transform.GetChild(index).GetComponent<ButtonManager>().OnSelect(null);
				EventSystem.current.SetSelectedGameObject(base.transform.GetChild(index).gameObject);
				type = ObjectType.Button;
			}
			else if (base.transform.GetChild(index).GetComponent<CustomInputField>() != null)
			{
				base.transform.GetChild(index).GetComponent<CustomInputField>().inputText.ActivateInputField();
				EventSystem.current.SetSelectedGameObject(base.transform.GetChild(index).gameObject);
				type = ObjectType.InputField;
			}
			else
			{
				catchedObject = false;
			}
		}
	}
}
