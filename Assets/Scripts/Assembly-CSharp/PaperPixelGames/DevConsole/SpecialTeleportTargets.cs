using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Boss;
using UnityEngine;

namespace PaperPixelGames.DevConsole
{
	internal static class SpecialTeleportTargets
	{
		public readonly struct Target
		{
			public Vector3 Position { get; }

			public Vector3? LookAt { get; }

			public Target(Vector3 position, Vector3? lookAt = null)
			{
				Position = position;
				LookAt = lookAt;
			}
		}

		private static readonly Dictionary<string, Target> Targets = new Dictionary<string, Target>(StringComparer.OrdinalIgnoreCase)
		{
			{
				"boat",
				new Target(new Vector3(131f, 6f, -18.5f))
			},
			{
				"rest",
				new Target(new Vector3(21.5f, 6f, 59f))
			},
			{
				"gabe",
				new Target(new Vector3(-6.5f, 5f, -3.5f))
			},
			{
				"marina",
				new Target(new Vector3(-6.5f, 5f, -19.5f))
			},
			{
				"lighthouse",
				new Target(new Vector3(180f, 5f, -80f), new Vector3(185f, 5f, -100f))
			},
			{
				"lh",
				new Target(new Vector3(180f, 5f, -80f), new Vector3(185f, 5f, -100f))
			},
			{
				"c1",
				new Target(new Vector3(76f, -50f, 293f))
			},
			{
				"c2",
				new Target(new Vector3(-281f, -52f, 219f))
			},
			{
				"c3",
				new Target(new Vector3(-210f, -135f, 595f))
			},
			{
				"boss",
				new Target(new Vector3(-181.3f, -161.4f, -38.7f), new Vector3(-217.3f, -161.4f, -38.7f))
			}
		};

		public static string KeywordList => string.Join(", ", Targets.Keys);

		public static bool TryGetTarget(string keyword, out Target target)
		{
			target = default;
			if (string.IsNullOrWhiteSpace(keyword))
			{
				return false;
			}
			string text = keyword.Trim();
			if (text.Equals("boss", StringComparison.OrdinalIgnoreCase) && TryResolveCurrentBossTarget(out target))
			{
				return true;
			}
			return Targets.TryGetValue(text, out target);
		}

		private static bool TryResolveCurrentBossTarget(out Target target)
		{
			target = default;
			CrabBossController crabBossController = UnityEngine.Object.FindFirstObjectByType<CrabBossController>();
			if (crabBossController == null)
			{
				return false;
			}
			Transform transform = crabBossController.transform;
			Animator[] componentsInChildren = crabBossController.GetComponentsInChildren<Animator>(includeInactive: true);
			foreach (Animator animator in componentsInChildren)
			{
				if (animator != null && animator.name.StartsWith("RiftStoneCrab", StringComparison.OrdinalIgnoreCase))
				{
					transform = animator.transform;
					break;
				}
			}
			Vector3 forward = crabBossController.transform.forward;
			forward.y = 0f;
			if (forward.sqrMagnitude <= 0.0001f)
			{
				forward = Vector3.forward;
			}
			else
			{
				forward.Normalize();
			}
			Vector3 vector = transform.position + Vector3.up * 3f;
			Vector3 position = vector + forward * 36f;
			target = new Target(position, vector);
			return true;
		}
	}
}
