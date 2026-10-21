using System.Threading.Tasks;
using UnityEngine;

namespace PaperPixelGames.Core.Reporting
{
	public interface IScreenshotCaptureService
	{
		Task<Texture2D> CaptureAsync();
	}
}
