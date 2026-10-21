using System.Collections.Generic;

namespace PaperPixelGames.Core.Player
{
	public interface IAttachableObject
	{
		PlayerAttachmentMode AttachmentMode { get; }

		List<AttachSeat> Seats { get; set; }

		bool ServerTryReleasePlayerForRecovery(PPG_Player player, string reason);
	}
}
