namespace PaperPixelGames.Core.Restaurant
{
	public enum RestaurantFeedbackCue : byte
	{
		RequestOrder = 0,
		OrderTaken = 1,
		OrderServed = 2,
		RequestPayment = 3,
		OrderServedIncomplete = 4,
		PaymentCollectedFarewell = 5,
		WaitingChatter = 6,
		WaitingChatterReply = 7,
		EatingChatter = 8
	}
}
