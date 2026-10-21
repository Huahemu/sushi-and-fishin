using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace PaperPixelGames.Framework.Input
{
	[DefaultExecutionOrder(10000)]
	public class InputManager : Singleton<InputManager>
	{
		[SerializeField]
		private InputHandler _inputHandler;

		public InputHandler InputHandler => _inputHandler;

		protected override void Awake()
		{
			base.Awake();
			_inputHandler.Register();
		}

		private void OnDisable()
		{
			_inputHandler.UnRegister();
		}
	}
}
