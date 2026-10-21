using System;
using UnityEngine;

namespace PaperPixelGames.Core.Popup
{
	public interface ICustomPopUp
	{
		Transform Transform { get; set; }

		void Awake();

		void OnDestroy();

		void Initialize();

		void Prepare(Action<bool> callBack, params object[] objects);
	}
}
