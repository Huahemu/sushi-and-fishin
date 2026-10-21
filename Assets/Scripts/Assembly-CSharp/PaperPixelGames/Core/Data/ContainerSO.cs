using PaperPixelGames.Core.Interaction;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "DynamicContainer", menuName = "Data/DynamicContainerSO")]
	public class ContainerSO : ItemSO
	{
		public DynamicContainerType ContainerType;
	}
}
