namespace PaperPixelGames.Core.Blackjack
{
	public enum BlackjackHandState : byte
	{
		Waiting = 0,
		Playing = 1,
		Standing = 2,
		Blackjack = 3,
		Bust = 4,
		Won = 5,
		Lost = 6,
		Push = 7,
		Forfeited = 8
	}
}
