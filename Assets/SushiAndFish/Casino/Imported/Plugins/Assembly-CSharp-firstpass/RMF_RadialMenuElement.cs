namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;
using global::UnityEngine.EventSystems;
using global::UnityEngine.UI;

[AddComponentMenu("Radial Menu Framework/RMF Element")]
public class RMF_RadialMenuElement : MonoBehaviour
{
	[HideInInspector]
	public RectTransform rt;

	[HideInInspector]
	public RMF_RadialMenu parentRM;

	[Tooltip("Each radial element needs a button. This is generally a child one level below this primary radial element game object.")]
	public Button button;

	[Tooltip("This is the text label that will appear in the center of the radial menu when this option is moused over. Best to keep it short.")]
	public string label;

	[HideInInspector]
	public float angleMin;

	[HideInInspector]
	public float angleMax;

	[HideInInspector]
	public float angleOffset;

	[HideInInspector]
	public bool active;

	[HideInInspector]
	public int assignedIndex;

	private CanvasGroup cg;

	private void Awake()
	{
		rt = base.gameObject.GetComponent<RectTransform>();
		if (base.gameObject.GetComponent<CanvasGroup>() == null)
		{
			cg = base.gameObject.AddComponent<CanvasGroup>();
		}
		else
		{
			cg = base.gameObject.GetComponent<CanvasGroup>();
		}
		if (rt == null)
		{
			Debug.LogError("Radial Menu: Rect Transform for radial element " + base.gameObject.name + " could not be found. Please ensure this is an object parented to a canvas.");
		}
		if (button == null)
		{
			Debug.LogError("Radial Menu: No button attached to " + base.gameObject.name + "!");
		}
	}

	private void Start()
	{
		rt.rotation = Quaternion.Euler(0f, 0f, 0f - angleOffset);
		cg.blocksRaycasts = false;
	}

	public void setAllAngles(float offset, float baseOffset)
	{
		angleOffset = offset;
		angleMin = offset - baseOffset / 2f;
		angleMax = offset + baseOffset / 2f;
	}

	public void highlightThisElement(PointerEventData p)
	{
		ExecuteEvents.Execute(button.gameObject, p, ExecuteEvents.selectHandler);
		active = true;
		setParentMenuLable(label);
	}

	public void setParentMenuLable(string l)
	{
		if (parentRM.textLabel != null)
		{
			parentRM.textLabel.text = l;
		}
	}

	public void unHighlightThisElement(PointerEventData p)
	{
		ExecuteEvents.Execute(button.gameObject, p, ExecuteEvents.deselectHandler);
		active = false;
	}

	public void clickMeTest()
	{
		Debug.Log(assignedIndex);
	}
}

}
