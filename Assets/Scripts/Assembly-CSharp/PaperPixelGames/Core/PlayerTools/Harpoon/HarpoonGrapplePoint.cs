using UnityEngine;

namespace PaperPixelGames.Core.PlayerTools.Harpoon
{
	public class HarpoonGrapplePoint : MonoBehaviour
	{
		[SerializeField]
		private float _upBoostVelocity = 5.5f;

		[SerializeField]
		private float _initialForwardVelocity = 9f;

		[SerializeField]
		private float _forwardTargetSpeed = 13f;

		[SerializeField]
		private float _forwardAcceleration = 8f;

		[SerializeField]
		private float _orbitStartDistance = 5.5f;

		[SerializeField]
		private float _orbitRadius;

		[SerializeField]
		private float _orbitTargetSpeed = 11f;

		[SerializeField]
		private float _orbitAcceleration = 10f;

		[SerializeField]
		private float _radialSpring = 55f;

		[SerializeField]
		private float _radialDamp = 11f;

		[SerializeField]
		private float _planeDamp = 8f;

		[SerializeField]
		private float _maxAcceleration = 120f;

		[SerializeField]
		[Tooltip("Additional distance beyond the initial rope length before the grapple breaks. Zero disables automatic breaking.")]
		private float _breakDistanceTolerance = 4f;

		[SerializeField]
		private bool _breakAppliesReleaseVelocity;

		public float UpBoostVelocity => _upBoostVelocity;

		public float InitialForwardVelocity => _initialForwardVelocity;

		public float ForwardTargetSpeed => _forwardTargetSpeed;

		public float ForwardAcceleration => _forwardAcceleration;

		public float OrbitStartDistance => _orbitStartDistance;

		public float OrbitRadius => _orbitRadius;

		public float OrbitTargetSpeed => _orbitTargetSpeed;

		public float OrbitAcceleration => _orbitAcceleration;

		public float RadialSpring => _radialSpring;

		public float RadialDamp => _radialDamp;

		public float PlaneDamp => _planeDamp;

		public float MaxAcceleration => _maxAcceleration;

		public float BreakDistanceTolerance => _breakDistanceTolerance;

		public bool BreakAppliesReleaseVelocity => _breakAppliesReleaseVelocity;
	}
}
