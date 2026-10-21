namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;
using global::UnityEngine.EventSystems;

namespace AirFishLab.ScrollingList
{
	public class InputProcessor
	{
		private readonly RectTransform _rectTransform;

		private readonly Vector2 _maxRectPos;

		private readonly Camera _canvasRefCamera;

		private float _lastInputTime;

		private Vector2 _lastLocalInputPos;

		public InputProcessor(RectTransform rectTransform, Camera canvasRefCamera)
		{
			_rectTransform = rectTransform;
			_maxRectPos = _rectTransform.rect.max;
			_canvasRefCamera = canvasRefCamera;
		}

		public InputInfo GetInputInfo(PointerEventData eventData, InputPhase phase)
		{
			Vector2 deltaLocalPos = ((phase == InputPhase.Scrolled) ? GetScrollDeltaPos(eventData.scrollDelta) : GetDeltaPos(eventData.position, phase));
			Vector2 deltaLocalPosNormalized = new Vector2(deltaLocalPos.x / _maxRectPos.x, deltaLocalPos.y / _maxRectPos.y);
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (phase == InputPhase.Began || phase == InputPhase.Scrolled)
			{
				_lastInputTime = realtimeSinceStartup;
			}
			float deltaTime = realtimeSinceStartup - _lastInputTime;
			_lastInputTime = realtimeSinceStartup;
			return new InputInfo
			{
				Phase = phase,
				DeltaLocalPos = deltaLocalPos,
				DeltaLocalPosNormalized = deltaLocalPosNormalized,
				DeltaTime = deltaTime
			};
		}

		private Vector2 GetScrollDeltaPos(Vector2 scrollDelta)
		{
			if (!(scrollDelta.y > 0f))
			{
				return Vector2.down;
			}
			return Vector2.up;
		}

		private Vector2 GetDeltaPos(Vector2 screenInputPos, InputPhase phase)
		{
			RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, screenInputPos, _canvasRefCamera, out var localPoint);
			if (phase == InputPhase.Began)
			{
				_lastLocalInputPos = localPoint;
				return Vector2.zero;
			}
			Vector2 result = localPoint - _lastLocalInputPos;
			_lastLocalInputPos = localPoint;
			return result;
		}
	}
}

}
