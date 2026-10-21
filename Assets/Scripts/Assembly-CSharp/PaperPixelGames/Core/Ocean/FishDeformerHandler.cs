using Deform;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean
{
	public class FishDeformerHandler : MonoBehaviour
	{
		private Deformable _deformer;

		private SineDeformer _sineDeformer;

		private FishSO _data;

		public void CacheDeformer(FishSO so, GameObject go)
		{
			if (!go.TryGetComponent<Deformable>(out _deformer) || !_deformer.transform.GetChild(0).TryGetComponent<SineDeformer>(out _sineDeformer))
			{
				UtilityMethods.Log("[FishDeformerHandler] _sineDeformer not found!", LogType.Warning);
			}
			else
			{
				_data = so;
			}
		}

		public void SetStatus(bool status)
		{
			if (!(_deformer == null) && !(_sineDeformer == null))
			{
				_deformer.UpdateMode = ((!status) ? UpdateMode.Stop : UpdateMode.Auto);
			}
		}

		public void ChangePreset(FishAnim anim)
		{
			if (!(_deformer == null) && !(_sineDeformer == null))
			{
				SetStatus(status: true);
				switch (anim)
				{
				case FishAnim.Move:
					_sineDeformer.Frequency = _data.MovePreset.Frequency;
					_sineDeformer.Amplitude = _data.MovePreset.Amplitude;
					_sineDeformer.Falloff = _data.MovePreset.Falloff;
					_sineDeformer.Offset = _data.MovePreset.Offset;
					_sineDeformer.Speed = _data.MovePreset.Speed;
					break;
				case FishAnim.Sprint:
					_sineDeformer.Frequency = _data.SprintPreset.Frequency;
					_sineDeformer.Amplitude = _data.SprintPreset.Amplitude;
					_sineDeformer.Falloff = _data.SprintPreset.Falloff;
					_sineDeformer.Offset = _data.SprintPreset.Offset;
					_sineDeformer.Speed = _data.SprintPreset.Speed;
					break;
				case FishAnim.Struggle:
					_sineDeformer.Frequency = _data.StrugglePreset.Frequency;
					_sineDeformer.Amplitude = _data.StrugglePreset.Amplitude;
					_sineDeformer.Falloff = _data.StrugglePreset.Falloff;
					_sineDeformer.Offset = _data.StrugglePreset.Offset;
					_sineDeformer.Speed = _data.StrugglePreset.Speed;
					break;
				default:
					SetStatus(status: false);
					break;
				}
			}
		}
	}
}
