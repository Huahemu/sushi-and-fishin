namespace PaperPixelGames.Core.Restaurant
{
	public readonly struct DeliveryEvaluation
	{
		public readonly OrderMatchResult Result;

		public readonly OrderIssueFlags Issues;

		public readonly int Penalty;

		public readonly int Earned;

		public DeliveryEvaluation(OrderMatchResult result, OrderIssueFlags issues, int penalty, int earned)
		{
			Result = result;
			Issues = issues;
			Penalty = penalty;
			Earned = earned;
		}
	}
}
