using System.Collections.Generic;
using UnityEngine;

namespace PaperPixelGames.Framework.Quality.Appliers
{
	public class QualityApplierActivity : IQualityApplier
	{
		public List<GameObject> MediumObjects;

		public List<GameObject> LowObjects;

		public void Apply(int qualityLevel)
		{
			string text = ((qualityLevel >= 0 && qualityLevel < QualitySettings.names.Length) ? QualitySettings.names[qualityLevel] : string.Empty);
			bool isActive = text == "High" || text == "Ultra";
			bool isActive2 = text != "Low";
			SetObjectsActive(MediumObjects, isActive);
			SetObjectsActive(LowObjects, isActive2);
		}

		private void SetObjectsActive(List<GameObject> objects, bool isActive)
		{
			if (objects == null)
			{
				return;
			}
			int count = objects.Count;
			for (int i = 0; i < count; i++)
			{
				GameObject gameObject = objects[i];
				if (!(gameObject == null) && gameObject.activeSelf != isActive)
				{
					gameObject.SetActive(isActive);
				}
			}
		}
	}
}
