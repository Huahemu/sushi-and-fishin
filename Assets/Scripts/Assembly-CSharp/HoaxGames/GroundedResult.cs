using UnityEngine;

namespace HoaxGames
{
	public class GroundedResult
	{
		public bool isValid;

		public bool isGrounded;

		public Transform groundedTransform;

		public Vector3 groundedPoint;

		public Vector3 groundedPosition;

		public Vector3 groundedNormal;

		public GroundedResult(bool _isValid, bool _isGrounded, Transform _groundedTransform, Vector3 _groundedPoint, Vector3 _groundedPosition, Vector3 _groundedNormal)
		{
			init(_isValid, _isGrounded, _groundedTransform, _groundedPoint, _groundedPosition, _groundedNormal);
		}

		public void init(bool _isValid, bool _isGrounded, Transform _groundedTransform, Vector3 _groundedPoint, Vector3 _groundedPosition, Vector3 _groundedNormal)
		{
			isValid = _isValid;
			isGrounded = _isGrounded;
			groundedTransform = _groundedTransform;
			groundedPoint = _groundedPoint;
			groundedPosition = _groundedPosition;
			groundedNormal = _groundedNormal;
		}
	}
}
