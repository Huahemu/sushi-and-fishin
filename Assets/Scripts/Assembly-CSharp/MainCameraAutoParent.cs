using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;

[RequireComponent(typeof(ParentConstraint))]
public class MainCameraAutoParent : MonoBehaviour
{
	public Vector3 offset = new Vector3(0f, 0f, 1f);

	private Vector3 originalPosition;

	private ParentConstraint constraint;

	private void Start()
	{
		originalPosition = base.transform.position;
		base.transform.position = Vector3.zero;
		base.transform.rotation = Quaternion.identity;
		base.transform.localScale = Vector3.one;
		constraint = GetComponent<ParentConstraint>();
		constraint.SetSources(new List<ConstraintSource>());
		if (Camera.main != null)
		{
			ConstraintSource source = new ConstraintSource
			{
				sourceTransform = Camera.main.transform,
				weight = 1f
			};
			constraint.AddSource(source);
			constraint.constraintActive = true;
			constraint.SetTranslationOffset(0, offset);
			constraint.locked = true;
		}
		else
		{
			Debug.LogError("No MainCamera found in the scene.");
		}
	}

	private void Update()
	{
		constraint.SetTranslationOffset(0, offset);
	}
}
