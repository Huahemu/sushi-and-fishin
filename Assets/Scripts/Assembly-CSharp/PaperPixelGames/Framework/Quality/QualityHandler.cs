using System.Collections.Generic;
using Kamgam.SettingsGenerator;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Framework.Quality
{
	public sealed class QualityHandler : MonoBehaviour
	{
		[SerializeField]
		private QualityConnectionSO _qualityConnectionSo;

		[SerializeReference]
		private List<IQualityApplier> _appliers = new List<IQualityApplier>();

		private QualityConnection _connection;

		private void OnEnable()
		{
			_connection = _qualityConnectionSo.GetConnection() as QualityConnection;
			_connection.QualityChanged += OnQualityChanged;
			ApplyCurrentQuality(_connection.Get());
		}

		private void OnDestroy()
		{
			_connection.QualityChanged -= OnQualityChanged;
		}

		public void ApplyCurrentQuality(int level)
		{
			for (int i = 0; i < _appliers.Count; i++)
			{
				_appliers[i]?.Apply(level);
			}
		}

		public void OnQualityChanged(int level)
		{
			UtilityMethods.Log("[QualityHandler] - Level:" + level);
			ApplyCurrentQuality(level);
		}
	}
}
