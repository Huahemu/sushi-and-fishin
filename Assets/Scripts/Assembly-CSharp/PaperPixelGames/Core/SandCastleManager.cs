using System.Collections.Generic;
using UnityEngine;

namespace PaperPixelGames.Core
{
	public class SandCastleManager : MonoBehaviour
	{
		[SerializeField]
		private List<SandCastle> _sandCastles;

		private void Awake()
		{
			if (_sandCastles == null)
			{
				_sandCastles = new List<SandCastle>();
			}
		}

		public void CrashAllSandCastles()
		{
			if (_sandCastles == null || _sandCastles.Count == 0)
			{
				return;
			}
			foreach (SandCastle sandCastle in _sandCastles)
			{
				if (!(sandCastle == null))
				{
					sandCastle.ServerSetCrashedState(status: true);
				}
			}
		}

		public void ResetAllSandCastlesRpc()
		{
			if (_sandCastles == null || _sandCastles.Count == 0)
			{
				return;
			}
			foreach (SandCastle sandCastle in _sandCastles)
			{
				if (!(sandCastle == null))
				{
					sandCastle.ServerSetCrashedState(status: false);
				}
			}
		}
	}
}
