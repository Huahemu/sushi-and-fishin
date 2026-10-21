using UnityEngine;

namespace PaperPixelGames.Core.Staff
{
	public sealed class StaffStockNeed
	{
		public string Id;

		public float Target;

		public float Orders;

		public float Current;

		public double Deadline;

		public float Missing => Mathf.Max(0f, Target - Current);

		public int Priority
		{
			get
			{
				if (!(Current < Orders))
				{
					if (!(Current <= 0f))
					{
						if (!(Current < Target))
						{
							return 0;
						}
						return 1;
					}
					return 2;
				}
				return 3;
			}
		}
	}
}
