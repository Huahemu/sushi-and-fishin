using System.Collections.Generic;
using UnityEngine;

namespace PaperPixelGames.Core.Staff
{
	public sealed class StaffConversionTable
	{
		private readonly struct Edge
		{
			public readonly string Source;

			public readonly string Target;

			public readonly float Yield;

			public Edge(string source, string target, float yield)
			{
				Source = source;
				Target = target;
				Yield = yield;
			}
		}

		private readonly List<Edge> snapshot = new List<Edge>();

		private readonly List<Edge> scratch = new List<Edge>();

		private readonly Dictionary<string, List<Edge>> routes = new Dictionary<string, List<Edge>>();

		private readonly Dictionary<(string, string), float> yields = new Dictionary<(string, string), float>();

		private readonly HashSet<string> path = new HashSet<string>();

		public void Refresh(StaffWorkplaceRegistry world)
		{
			scratch.Clear();
			foreach (StaffPrepRecipe prepRecipe in world.PrepRecipes)
			{
				if (prepRecipe?.WholeFish != null && prepRecipe.Input != null)
				{
					scratch.Add(new Edge(prepRecipe.WholeFish.Id, prepRecipe.Input.Id, Mathf.Max(1, prepRecipe.WholeFish.MinYieldRange)));
				}
			}
			foreach (StaffSupplyConversion conversion in world.Conversions)
			{
				if (conversion?.Input != null && conversion.Output != null)
				{
					scratch.Add(new Edge(conversion.Input.Id, conversion.Output.Id, conversion.Yield));
				}
			}
			bool flag = snapshot.Count == scratch.Count;
			int num = 0;
			while (flag && num < snapshot.Count)
			{
				flag = snapshot[num].Source == scratch[num].Source && snapshot[num].Target == scratch[num].Target && snapshot[num].Yield == scratch[num].Yield;
				num++;
			}
			if (flag)
			{
				return;
			}
			snapshot.Clear();
			snapshot.AddRange(scratch);
			routes.Clear();
			yields.Clear();
			foreach (Edge item in snapshot)
			{
				if (!routes.TryGetValue(item.Source, out var value))
				{
					routes.Add(item.Source, value = new List<Edge>());
				}
				value.Add(item);
			}
		}

		public float Convert(string source, string target, float amount)
		{
			if (source == target)
			{
				return amount;
			}
			if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target) || amount <= 0f)
			{
				return 0f;
			}
			(string, string) key = (source, target);
			if (!yields.TryGetValue(key, out var value))
			{
				path.Clear();
				value = Search(source, target);
				yields.Add(key, value);
			}
			return amount * value;
		}

		private float Search(string source, string target)
		{
			if (source == target)
			{
				return 1f;
			}
			if (!routes.TryGetValue(source, out var value) || !path.Add(source))
			{
				return 0f;
			}
			float num = 0f;
			foreach (Edge item in value)
			{
				num = Mathf.Max(num, item.Yield * Search(item.Target, target));
			}
			path.Remove(source);
			return num;
		}
	}
}
