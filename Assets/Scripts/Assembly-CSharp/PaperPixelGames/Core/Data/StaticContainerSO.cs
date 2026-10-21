using PaperPixelGames.Core.Interaction;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "StaticContainer", menuName = "Data/StaticContainerSO")]
	public class StaticContainerSO : ItemSO
	{
		public StaticContainerType ContainerType;
	}
}
