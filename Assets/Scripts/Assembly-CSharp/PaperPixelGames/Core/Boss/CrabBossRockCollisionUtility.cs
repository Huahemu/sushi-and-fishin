using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Player;
using UnityEngine;

namespace PaperPixelGames.Core.Boss
{
	internal static class CrabBossRockCollisionUtility
	{
		public static bool TryFindFirstBoxImpact(CrabBossController boss, Vector3 start, Vector3 end, Vector3 halfExtents, Quaternion rotation, LayerMask worldLayers, out CrabBossRockHit result)
		{
			result = default;
			Vector3 vector = end - start;
			float magnitude = vector.magnitude;
			Vector3 vector2 = ((magnitude > Mathf.Epsilon) ? (vector / magnitude) : Vector3.down);
			float num = float.PositiveInfinity;
			Physics.SyncTransforms();
			Collider[] array = Physics.OverlapBox(start, halfExtents, rotation, -1, QueryTriggerInteraction.Ignore);
			foreach (Collider collider in array)
			{
				if (TryClassifyBoxCollider(collider, boss, worldLayers, out var player))
				{
					Vector3 position = collider.ClosestPoint(start);
					result = new CrabBossRockHit((player != null) ? CrabBossRockImpactType.Player : CrabBossRockImpactType.World, player, (player == null) ? collider : null, position, -vector2, 0f);
					if (player == null)
					{
						return true;
					}
					num = 0f;
				}
			}
			if (num == 0f)
			{
				return true;
			}
			if (magnitude <= Mathf.Epsilon)
			{
				return false;
			}
			RaycastHit[] array2 = Physics.BoxCastAll(start, halfExtents, vector2, rotation, magnitude, -1, QueryTriggerInteraction.Ignore);
			for (int i = 0; i < array2.Length; i++)
			{
				RaycastHit raycastHit = array2[i];
				if (!(raycastHit.distance >= num) && TryClassifyBoxCollider(raycastHit.collider, boss, worldLayers, out var player2))
				{
					num = raycastHit.distance;
					result = new CrabBossRockHit((player2 != null) ? CrabBossRockImpactType.Player : CrabBossRockImpactType.World, player2, (player2 == null) ? raycastHit.collider : null, raycastHit.point, raycastHit.normal, raycastHit.distance);
				}
			}
			return !float.IsPositiveInfinity(num);
		}

		private static bool TryClassifyBoxCollider(Collider collider, CrabBossController boss, LayerMask worldLayers, out PPG_Player player)
		{
			player = collider.GetComponentInParent<PPG_Player>();
			if (player != null)
			{
				if (player.IsSpawned && boss != null && boss.ServerIsPlayerInRoom(player.OwnerClientId))
				{
					return collider is CapsuleCollider;
				}
				return false;
			}
			if ((worldLayers.value & (1 << collider.gameObject.layer)) != 0)
			{
				return IsWorldCollider(collider, boss);
			}
			return false;
		}

		public static bool TryFindFirstImpact(CrabBossController boss, IReadOnlyList<PPG_Player> players, Vector3 segmentStart, Vector3 segmentEnd, float playerCenterOffset, float radius, LayerMask worldLayers, out CrabBossRockHit result)
		{
			result = default;
			if (!IsFinite(segmentStart) || !IsFinite(segmentEnd) || !IsFinite(playerCenterOffset) || !IsFinite(radius) || radius <= 0f)
			{
				return false;
			}
			Vector3 vector = segmentEnd - segmentStart;
			float magnitude = vector.magnitude;
			Vector3 vector2 = ((magnitude > Mathf.Epsilon) ? (vector / magnitude) : Vector3.down);
			bool result2 = false;
			float num = float.PositiveInfinity;
			if (worldLayers.value != 0)
			{
				Collider[] array = Physics.OverlapSphere(segmentStart, radius, worldLayers, QueryTriggerInteraction.Ignore);
				foreach (Collider collider in array)
				{
					if (IsWorldCollider(collider, boss))
					{
						Vector3 vector3 = collider.ClosestPoint(segmentStart);
						Vector3 vector4 = segmentStart - vector3;
						result = new CrabBossRockHit(CrabBossRockImpactType.World, null, collider, vector3, (vector4.sqrMagnitude > Mathf.Epsilon) ? vector4.normalized : (-vector2), 0f);
						num = 0f;
						result2 = true;
						break;
					}
				}
				RaycastHit[] array2 = ((magnitude > Mathf.Epsilon) ? Physics.SphereCastAll(segmentStart, radius, vector2, magnitude, worldLayers, QueryTriggerInteraction.Ignore) : Array.Empty<RaycastHit>());
				for (int j = 0; j < array2.Length; j++)
				{
					RaycastHit raycastHit = array2[j];
					if (IsWorldCollider(raycastHit.collider, boss) && !(raycastHit.distance >= num))
					{
						num = raycastHit.distance;
						Vector3 normal = ((raycastHit.normal.sqrMagnitude > Mathf.Epsilon) ? raycastHit.normal : (-vector2));
						result = new CrabBossRockHit(CrabBossRockImpactType.World, null, raycastHit.collider, raycastHit.point, normal, raycastHit.distance);
						result2 = true;
					}
				}
			}
			if (players == null)
			{
				return result2;
			}
			for (int k = 0; k < players.Count; k++)
			{
				PPG_Player pPG_Player = players[k];
				if (pPG_Player == null || !pPG_Player.IsSpawned || boss == null || !boss.ServerIsPlayerInRoom(pPG_Player.OwnerClientId))
				{
					continue;
				}
				Vector3 vector5 = ResolvePlayerHitCenter(pPG_Player, playerCenterOffset);
				CapsuleCollider capsuleCollider = ((pPG_Player.FirstPersonCharacter != null) ? pPG_Player.FirstPersonCharacter.GetComponent<CapsuleCollider>() : pPG_Player.GetComponentInChildren<CapsuleCollider>());
				bool flag;
				float distance;
				if (capsuleCollider != null)
				{
					ResolveCapsule(capsuleCollider, out var pointA, out var pointB, out var radius2);
					flag = TryFindCapsuleEntry(segmentStart, segmentEnd, pointA, pointB, radius + radius2, out distance);
					vector5 = (pointA + pointB) * 0.5f;
				}
				else
				{
					flag = TryFindSphereEntry(segmentStart, segmentEnd, vector5, radius, out distance);
				}
				if (flag && !(distance >= num))
				{
					Vector3 vector6 = segmentStart + vector2 * distance;
					Vector3 normal2 = vector6 - vector5;
					if (normal2.sqrMagnitude <= Mathf.Epsilon)
					{
						normal2 = -vector2;
					}
					else
					{
						normal2.Normalize();
					}
					num = distance;
					result = new CrabBossRockHit(CrabBossRockImpactType.Player, pPG_Player, null, vector6, normal2, distance);
					result2 = true;
				}
			}
			return result2;
		}

		internal static bool TryFindSphereEntry(Vector3 segmentStart, Vector3 segmentEnd, Vector3 center, float radius, out float distance)
		{
			distance = 0f;
			if (!IsFinite(segmentStart) || !IsFinite(segmentEnd) || !IsFinite(center) || !IsFinite(radius) || radius <= 0f)
			{
				return false;
			}
			Vector3 lhs = segmentStart - center;
			float num = lhs.sqrMagnitude - radius * radius;
			if (num <= 0f)
			{
				return true;
			}
			Vector3 vector = segmentEnd - segmentStart;
			float magnitude = vector.magnitude;
			if (magnitude <= Mathf.Epsilon)
			{
				return false;
			}
			float num2 = Vector3.Dot(lhs, vector / magnitude);
			float num3 = num2 * num2 - num;
			if (num2 >= 0f || num3 < 0f)
			{
				return false;
			}
			distance = 0f - num2 - Mathf.Sqrt(num3);
			if (distance >= 0f)
			{
				return distance <= magnitude;
			}
			return false;
		}

		internal static void ResolveCapsule(CapsuleCollider capsule, out Vector3 pointA, out Vector3 pointB, out float radius)
		{
			Vector3 lossyScale = capsule.transform.lossyScale;
			lossyScale = new Vector3(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));
			int direction = capsule.direction;
			float num = direction switch
			{
				1 => Mathf.Max(lossyScale.x, lossyScale.z), 
				0 => Mathf.Max(lossyScale.y, lossyScale.z), 
				_ => Mathf.Max(lossyScale.x, lossyScale.y), 
			};
			radius = capsule.radius * num;
			float num2 = Mathf.Max(0f, capsule.height * lossyScale[direction] * 0.5f - radius);
			Vector3 direction2 = direction switch
			{
				1 => Vector3.up, 
				0 => Vector3.right, 
				_ => Vector3.forward, 
			};
			Vector3 vector = capsule.transform.TransformDirection(direction2) * num2;
			Vector3 vector2 = capsule.transform.TransformPoint(capsule.center);
			pointA = vector2 - vector;
			pointB = vector2 + vector;
		}

		internal static bool TryFindCapsuleEntry(Vector3 start, Vector3 end, Vector3 pointA, Vector3 pointB, float radius, out float distance)
		{
			distance = 0f;
			if (!IsFinite(start) || !IsFinite(end) || !IsFinite(pointA) || !IsFinite(pointB) || !IsFinite(radius) || radius <= 0f)
			{
				return false;
			}
			Vector3 vector = pointB - pointA;
			float magnitude = vector.magnitude;
			if (magnitude <= Mathf.Epsilon)
			{
				return TryFindSphereEntry(start, end, pointA, radius, out distance);
			}
			vector /= magnitude;
			Vector3 vector2 = start - pointA;
			float num = Vector3.Dot(vector2, vector);
			if ((vector2 - vector * Mathf.Clamp(num, 0f, magnitude)).sqrMagnitude <= radius * radius)
			{
				return true;
			}
			Vector3 vector3 = end - start;
			float magnitude2 = vector3.magnitude;
			if (magnitude2 <= Mathf.Epsilon)
			{
				return false;
			}
			Vector3 vector4 = vector3 / magnitude2;
			float num2 = Vector3.Dot(vector4, vector);
			Vector3 rhs = vector4 - vector * num2;
			Vector3 lhs = vector2 - vector * num;
			float sqrMagnitude = rhs.sqrMagnitude;
			float num3 = Vector3.Dot(lhs, rhs);
			float num4 = lhs.sqrMagnitude - radius * radius;
			float num5 = num3 * num3 - sqrMagnitude * num4;
			float num6 = float.PositiveInfinity;
			if (sqrMagnitude > Mathf.Epsilon && num5 >= 0f)
			{
				float num7 = (0f - num3 - Mathf.Sqrt(num5)) / sqrMagnitude;
				float num8 = num + num7 * num2;
				if (num7 >= 0f && num7 <= magnitude2 && num8 >= 0f && num8 <= magnitude)
				{
					num6 = num7;
				}
			}
			if (TryFindSphereEntry(start, end, pointA, radius, out var distance2))
			{
				num6 = Mathf.Min(num6, distance2);
			}
			if (TryFindSphereEntry(start, end, pointB, radius, out var distance3))
			{
				num6 = Mathf.Min(num6, distance3);
			}
			if (float.IsPositiveInfinity(num6))
			{
				return false;
			}
			distance = num6;
			return true;
		}

		private static bool IsWorldCollider(Collider collider, CrabBossController boss)
		{
			if (collider != null && (boss == null || !collider.transform.IsChildOf(boss.transform)) && collider.GetComponentInParent<PPG_Player>() == null)
			{
				return collider.GetComponentInParent<CrabBossLocalRockSlot>() == null;
			}
			return false;
		}

		private static bool IsFinite(float value)
		{
			if (!float.IsNaN(value))
			{
				return !float.IsInfinity(value);
			}
			return false;
		}

		private static bool IsFinite(Vector3 value)
		{
			if (IsFinite(value.x) && IsFinite(value.y))
			{
				return IsFinite(value.z);
			}
			return false;
		}

		public static Vector3 ResolvePlayerHitCenter(PPG_Player player, float centerOffset)
		{
			Transform transform = ((player != null && player.FirstPersonCharacter != null) ? player.FirstPersonCharacter.transform : ((player != null) ? player.transform : null));
			if (!(transform != null))
			{
				return Vector3.zero;
			}
			return transform.position + Vector3.up * centerOffset;
		}
	}
}
