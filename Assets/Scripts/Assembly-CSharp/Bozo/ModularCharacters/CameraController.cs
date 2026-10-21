using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Bozo.ModularCharacters
{
	public class CameraController : MonoBehaviour
	{
		[Serializable]
		private class CameraPositions
		{
			public OutfitType type;

			public Transform ZoomOutPos;

			public Transform ZoomInPos;

			public float fovOut;

			public float fovIn;

			public float startingPosition;
		}

		private Camera cam;

		private float position;

		public float scrollSpeed;

		public Transform startPosition;

		public Transform endPosition;

		public Vector2 fov;

		public Slider slider;

		public float tweenSpeed;

		private float tweenTimer;

		private float currentPosition;

		private float targetPosition;

		[SerializeField]
		private CameraPositions[] cameraPositions;

		private Dictionary<OutfitType, CameraPositions> camPos = new Dictionary<OutfitType, CameraPositions>();

		[SerializeField]
		private InputActionReference zoomAction;

		private void Awake()
		{
			cam = Camera.main;
			CameraPositions[] array = this.cameraPositions;
			foreach (CameraPositions cameraPositions in array)
			{
				camPos.Add(cameraPositions.type, cameraPositions);
			}
		}

		private void OnEnable()
		{
			if (zoomAction != null)
			{
				zoomAction.action.Enable();
			}
		}

		private void OnDisable()
		{
			if (zoomAction != null)
			{
				zoomAction.action.Disable();
			}
		}

		private void Update()
		{
			float num = 0f;
			if (zoomAction != null)
			{
				num = zoomAction.action.ReadValue<Vector2>().y;
			}
			if (num > 0f)
			{
				position += scrollSpeed;
				tweenTimer = -1f;
			}
			else if (num < 0f)
			{
				position -= scrollSpeed;
				tweenTimer = -1f;
			}
			slider.value = position;
			position = Mathf.Clamp(position, 0f, 1f);
			if (tweenTimer >= 0f)
			{
				position = Mathf.Lerp(targetPosition, currentPosition, tweenTimer);
				tweenTimer -= Time.deltaTime * tweenSpeed;
			}
			cam.transform.position = Vector3.Lerp(startPosition.position, endPosition.position, position);
			cam.transform.rotation = Quaternion.Lerp(startPosition.rotation, endPosition.rotation, position);
			cam.fieldOfView = Mathf.Lerp(fov.x, fov.y, position);
		}

		public void SetPosition(float value)
		{
			position = value;
		}

		public void tweenPosition(float value)
		{
			tweenTimer = 1f;
			targetPosition = value;
			currentPosition = position;
		}

		public void TweenPosition(OutfitType type)
		{
			CameraPositions value = null;
			if (!camPos.TryGetValue(type, out value))
			{
				value = cameraPositions[0];
			}
			startPosition = value.ZoomOutPos;
			endPosition = value.ZoomInPos;
			fov.x = value.fovOut;
			fov.y = value.fovIn;
			tweenTimer = 1f;
			targetPosition = value.startingPosition;
			currentPosition = position;
		}
	}
}
