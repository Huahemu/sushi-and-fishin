using System.Collections.Generic;
using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentQuestLinks : MonoBehaviour
	{
		[SerializeField]
		private List<string> _relatedQuestIds = new List<string>();

		public List<string> RelatedQuestIds => _relatedQuestIds;
	}
}
