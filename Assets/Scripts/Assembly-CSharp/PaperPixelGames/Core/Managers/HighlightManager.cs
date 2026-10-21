using EPOOutline;
using PaperPixelGames.Framework.Patterns;

namespace PaperPixelGames.Core.Managers
{
	public class HighlightManager : Singleton<HighlightManager>
	{
		private Outlinable _currentHighlightedObject;

		private Outlinable _persistentHighlightedObject;

		public void SetHighlight(Outlinable outlinable)
		{
			if (!(outlinable == null))
			{
				ClearCurrentHighlight();
				_currentHighlightedObject = outlinable;
				_currentHighlightedObject.enabled = true;
			}
		}

		public void SetPersistent(Outlinable outlinable)
		{
			if (!(outlinable == null))
			{
				ClearPersistentHighlight();
				_persistentHighlightedObject = outlinable;
				_persistentHighlightedObject.enabled = true;
			}
		}

		public void ClearCurrentHighlight()
		{
			if (!(_currentHighlightedObject == null))
			{
				_currentHighlightedObject.enabled = false;
				_currentHighlightedObject = null;
			}
		}

		public void ClearCurrentHighlight(Outlinable outlinable)
		{
			if (!(outlinable == null) && !(_currentHighlightedObject != outlinable))
			{
				ClearCurrentHighlight();
			}
		}

		public void ClearPersistentHighlight()
		{
			if (!(_persistentHighlightedObject == null))
			{
				_persistentHighlightedObject.enabled = false;
				_persistentHighlightedObject = null;
			}
		}

		public void ClearPersistentHighlight(Outlinable outlinable)
		{
			if (!(outlinable == null) && !(_persistentHighlightedObject != outlinable))
			{
				ClearPersistentHighlight();
			}
		}
	}
}
