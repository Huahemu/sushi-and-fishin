using PaperPixelGames.Core.Player;
using UnityEngine;

namespace PaperPixelGames.Core.Surf
{
	public sealed class SurfCameraRig
	{
		private readonly RaycastHit[] _hits = new RaycastHit[32];

		private Vector3 _pivot;

		private Vector3 _lastBoardPosition;

		private float _speed;

		private float _distance;

		private bool _initialized;

		public Vector3 Position { get; private set; }

		public Quaternion Rotation { get; private set; }

		public float FieldOfView { get; private set; }

		public void Update(Transform board, SurfboardWorld world, SurfCameraSettings settings, float pose, float yaw, float pitch, float dt, bool instant)
		{
			Vector3 vector = (board.TryGetComponent<Surfboard>(out var component) ? component.SurfacePosition : board.position);
			instant |= !_initialized || Vector3.Distance(vector, _lastBoardPosition) > 20f;
			dt = Mathf.Clamp(dt, 0.0001f, 0.1f);
			float value = (instant ? 0f : (Vector3.ProjectOnPlane(vector - _lastBoardPosition, Vector3.up).magnitude / dt));
			_lastBoardPosition = vector;
			_speed = (instant ? 0f : Mathf.Lerp(_speed, Mathf.Clamp(value, 0f, 8f), 1f - Mathf.Exp(-4f * dt)));
			float num = _speed / 8f;
			Vector3 vector2 = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
			Vector3 pivot = vector + Vector3.up * Mathf.Lerp(0.85f, 1.35f, pose) + vector2 * Mathf.Lerp(0.35f, 1.1f, num);
			if (instant)
			{
				_pivot = pivot;
			}
			else
			{
				_pivot.x = Mathf.Lerp(_pivot.x, pivot.x, 1f - Mathf.Exp((0f - settings.horizontalFollow) * dt));
				_pivot.z = Mathf.Lerp(_pivot.z, pivot.z, 1f - Mathf.Exp((0f - settings.horizontalFollow) * dt));
				_pivot.y = Mathf.Lerp(_pivot.y, pivot.y, 1f - Mathf.Exp((0f - settings.verticalFollow) * dt));
			}
			if (world != null)
			{
				_pivot.y = Mathf.Max(_pivot.y, world.SampleHeight(_pivot) + settings.waterClearance);
			}
			float num2 = Mathf.Lerp(settings.proneDistance, settings.standingDistance, pose) + settings.speedPullback * num;
			Vector3 vector3 = _pivot - Quaternion.Euler(Mathf.Clamp(pitch, 6f, 45f), yaw, 0f) * Vector3.forward * num2;
			if (world != null)
			{
				vector3.y = Mathf.Max(vector3.y, world.SampleHeight(vector3) + settings.waterClearance);
			}
			Vector3 offset = vector3 - _pivot;
			float num3 = ClearDistance(board, _pivot, offset, settings.collisionRadius);
			_distance = ((instant || num3 < _distance) ? num3 : Mathf.Lerp(_distance, num3, 1f - Mathf.Exp(-6f * dt)));
			Position = _pivot + offset.normalized * _distance;
			if (world != null)
			{
				Vector3 position = Position;
				position.y = Mathf.Max(position.y, world.SampleHeight(Position) + settings.waterClearance);
				Vector3 offset2 = position - _pivot;
				Position = _pivot + offset2.normalized * ClearDistance(board, _pivot, offset2, settings.collisionRadius);
			}
			Rotation = Quaternion.LookRotation(_pivot - Position, Vector3.up);
			float num4 = settings.fieldOfView + settings.speedFieldOfView * num;
			FieldOfView = (instant ? num4 : Mathf.Lerp(FieldOfView, num4, 1f - Mathf.Exp(-4f * dt)));
			_initialized = true;
		}

		private float ClearDistance(Transform board, Vector3 pivot, Vector3 offset, float radius)
		{
			float magnitude = offset.magnitude;
			if (magnitude < 0.001f)
			{
				return 0.001f;
			}
			int layerMask = ~LayerMask.GetMask("Player", "ClientPlayer", "Water", "Ignore Raycast", "BoidsCollider", "ViewModel");
			int num = Physics.SphereCastNonAlloc(pivot, radius, offset.normalized, _hits, magnitude, layerMask, QueryTriggerInteraction.Ignore);
			float num2 = magnitude;
			for (int i = 0; i < num; i++)
			{
				Collider collider = _hits[i].collider;
				if (!collider.transform.IsChildOf(board) && !(collider.GetComponentInParent<PPG_Player>() != null) && !(collider.GetComponentInParent<Surfboard>() != null))
				{
					num2 = Mathf.Min(num2, Mathf.Max(0.05f, _hits[i].distance - 0.12f));
				}
			}
			return num2;
		}
	}
}
