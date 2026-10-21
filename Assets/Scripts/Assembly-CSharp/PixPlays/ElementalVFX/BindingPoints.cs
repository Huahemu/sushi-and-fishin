using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixPlays.ElementalVFX
{
	public class BindingPoints : MonoBehaviour
	{
		[Serializable]
		public class BindingPointData
		{
			public BindingPointType Type;

			public Transform Point;
		}

		[SerializeField]
		private List<BindingPointData> _BindingPoints;

		public Transform GetBindingPoint(BindingPointType type)
		{
			BindingPointData bindingPointData = _BindingPoints.Find((BindingPointData x) => x.Type == type);
			if (bindingPointData != null)
			{
				return bindingPointData.Point;
			}
			Debug.LogError("Binding type " + type.ToString() + " not defined");
			return base.transform;
		}
	}
}
