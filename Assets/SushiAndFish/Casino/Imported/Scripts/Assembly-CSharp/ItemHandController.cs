namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;

public class ItemHandController : MonoBehaviour
{
	[Header("Hand Transform Reference")]
	[SerializeField]
	private Transform handTransform;

	[Header("Hand Transform Watcher")]
	[SerializeField]
	private HandTransformWatcher handWatcher;

	[Header("Animation Parameters")]
	[SerializeField]
	private string handsWidthParam = "handsWidth";

	[SerializeField]
	private string handsClutchingParam = "handsClutching";

	[SerializeField]
	private string isHoldingItemParam = "isHoldingItem";

	[Header("Width Settings")]
	[SerializeField]
	private float minItemWidth = 0.1f;

	[SerializeField]
	private float maxItemWidth = 2f;

	[SerializeField]
	private float widthMultiplier = 1.5f;

	[Header("Clutching Settings")]
	[SerializeField]
	private float clutchingThreshold = 0.5f;

	[SerializeField]
	private float maxClutching = 0.8f;

	[SerializeField]
	private float minItemWidthForMaxClutch = 0.2f;

	[SerializeField]
	private float maxItemWidthForMinClutch = 0.8f;

	private Animator animator;

	private void Awake()
	{
		animator = GetComponent<Animator>();
		if (handTransform == null)
		{
			Debug.LogError("ItemHandController: Hand Transform reference is missing!");
		}
		else if (handWatcher == null)
		{
			Debug.LogError("ItemHandController: Hand Transform Watcher reference is missing! Add the HandTransformWatcher component to your hand transform GameObject.");
		}
	}

	private void OnEnable()
	{
		if (handWatcher != null)
		{
			HandTransformWatcher handTransformWatcher = handWatcher;
			handTransformWatcher.OnChildrenChanged = (Action)Delegate.Combine(handTransformWatcher.OnChildrenChanged, new Action(OnHandChildrenChanged));
		}
	}

	private void OnDisable()
	{
		if (handWatcher != null)
		{
			HandTransformWatcher handTransformWatcher = handWatcher;
			handTransformWatcher.OnChildrenChanged = (Action)Delegate.Remove(handTransformWatcher.OnChildrenChanged, new Action(OnHandChildrenChanged));
		}
	}

	private void OnHandChildrenChanged()
	{
		if (handTransform.childCount > 0)
		{
			Item component = handTransform.GetChild(0).GetComponent<Item>();
			if (component != null)
			{
				OnItemPickup(component);
			}
		}
		else
		{
			OnItemDropped();
		}
	}

	private void OnItemPickup(Item item)
	{
		if (!(animator == null) && !(item == null))
		{
			float num = 1f;
			float num2 = Mathf.InverseLerp(minItemWidth, maxItemWidth, num);
			num2 = Mathf.Clamp01(num2 * widthMultiplier);
			animator.SetFloat(handsWidthParam, num2);
			float num3 = 0f;
			if (num < clutchingThreshold)
			{
				num3 = 0f;
			}
			else
			{
				num3 = Mathf.InverseLerp(maxItemWidthForMinClutch, minItemWidthForMaxClutch, num);
				num3 = Mathf.Clamp01(num3) * maxClutching;
			}
			animator.SetFloat(handsClutchingParam, num3);
			animator.SetBool(isHoldingItemParam, value: true);
		}
	}

	public void RefreshCurrentItem()
	{
		if (handTransform != null && handTransform.childCount > 0)
		{
			Item component = handTransform.GetChild(0).GetComponent<Item>();
			if (component != null)
			{
				OnItemPickup(component);
			}
		}
	}

	private void OnItemDropped()
	{
		if (!(animator == null))
		{
			animator.SetFloat(handsWidthParam, 0f);
			animator.SetFloat(handsClutchingParam, 0f);
			animator.SetBool(isHoldingItemParam, value: false);
		}
	}
}

}
