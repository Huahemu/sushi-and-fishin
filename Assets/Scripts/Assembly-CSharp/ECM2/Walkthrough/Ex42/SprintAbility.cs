using UnityEngine;

namespace ECM2.Walkthrough.Ex42
{
	public class SprintAbility : MonoBehaviour
	{
		[Space(15f)]
		public float maxSprintSpeed = 10f;

		public float maxSwimSpeed = 10f;

		[Header("Input")]
		[SerializeField]
		private bool _useToggleSprint = true;

		private Character _character;

		private bool _isSprinting;

		private bool _sprintInputPressed;

		private float _cachedMaxWalkSpeed;

		private float _cachedMaxSwimSpeed;

		public bool SprintDisabled { get; set; }

		public bool UseToggleSprint
		{
			get
			{
				return _useToggleSprint;
			}
			set
			{
				_useToggleSprint = value;
			}
		}

		private void Awake()
		{
			_character = GetComponent<Character>();
			_cachedMaxWalkSpeed = _character.maxWalkSpeed;
			_cachedMaxSwimSpeed = _character.maxSwimSpeed;
		}

		private void OnEnable()
		{
			_character.BeforeSimulationUpdated += OnBeforeSimulationUpdated;
		}

		private void OnDisable()
		{
			_character.BeforeSimulationUpdated -= OnBeforeSimulationUpdated;
		}

		public void Sprint()
		{
			_sprintInputPressed = true;
			_character.IsSprinting = true;
		}

		public void StopSprinting()
		{
			_sprintInputPressed = false;
			_character.IsSprinting = false;
		}

		public bool IsSprinting()
		{
			return _isSprinting;
		}

		private bool CanSprint()
		{
			if (!SprintDisabled && (_character.IsWalking() || _character.IsSwimming() || _character.IsFlying() || _character.IsFalling()))
			{
				return !_character.IsCrouched();
			}
			return false;
		}

		public void CheckSprintInput()
		{
			if (!_isSprinting && _sprintInputPressed && CanSprint())
			{
				_isSprinting = true;
				_character.maxWalkSpeed = maxSprintSpeed;
				_character.maxSwimSpeed = maxSwimSpeed;
			}
			else if (_isSprinting && (!_sprintInputPressed || !CanSprint()))
			{
				_isSprinting = false;
				_character.maxWalkSpeed = _cachedMaxWalkSpeed;
				_character.maxSwimSpeed = _cachedMaxSwimSpeed;
			}
		}

		private void OnBeforeSimulationUpdated(float deltaTime)
		{
			CheckSprintInput();
		}
	}
}
