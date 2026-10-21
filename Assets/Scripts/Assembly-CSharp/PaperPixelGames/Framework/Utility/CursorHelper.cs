using UnityEngine;

namespace PaperPixelGames.Framework.Utility
{
	public static class CursorHelper
	{
		public static bool CursorDefaultStatus;

		private static bool _cursorVisible;

		public static bool CursorVisible
		{
			get
			{
				return _cursorVisible;
			}
			set
			{
				Cursor.lockState = ((!value) ? CursorLockMode.Locked : CursorLockMode.None);
				Cursor.visible = value;
				_cursorVisible = value;
			}
		}
	}
}
