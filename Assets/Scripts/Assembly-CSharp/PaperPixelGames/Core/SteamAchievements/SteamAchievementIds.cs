namespace PaperPixelGames.Core.SteamAchievements
{
	public static class SteamAchievementIds
	{
		public const string ChildMakerCry = "ACH_CHILD_MAKER_CRY";

		public const string FirstFishCaught = "ACH_FIRST_FISH_CAUGHT";

		public const string FirstSushiMade = "ACH_FIRST_SUSHI_MADE";

		public const string FirstMoneyEarned = "ACH_FIRST_MONEY_EARNED";

		public const string HarpoonedFriend = "ACH_HARPOONED_FRIEND";

		public const string FirstDrown = "ACH_FIRST_DROWN";

		public const string EmployeeOfTheDay = "ACH_EMPLOYEE_OF_THE_DAY";

		public const string LastCall = "ACH_LAST_CALL";

		public const string NoMoreQuestionMarks = "ACH_NO_MORE_QUESTION_MARKS";

		public const string CatTax = "ACH_CAT_TAX";

		public const string ShellShocked = "ACH_SHELL_SHOCKED";

		public const string FishyBusiness = "ACH_FISHY_BUSINESS";

		public const string TheAbyssStaresBack = "ACH_THE_ABYSS_STARES_BACK";

		public const string FromSeaToTable = "ACH_FROM_SEA_TO_TABLE";

		public const string PiecesOfAMystery = "ACH_PIECES_OF_A_MYSTERY";

		public static string ToApiName(SteamAchievementId id)
		{
			return id switch
			{
				SteamAchievementId.ChildMakerCry => "ACH_CHILD_MAKER_CRY", 
				SteamAchievementId.FirstFishCaught => "ACH_FIRST_FISH_CAUGHT", 
				SteamAchievementId.FirstSushiMade => "ACH_FIRST_SUSHI_MADE", 
				SteamAchievementId.FirstMoneyEarned => "ACH_FIRST_MONEY_EARNED", 
				SteamAchievementId.HarpoonedFriend => "ACH_HARPOONED_FRIEND", 
				SteamAchievementId.FirstDrown => "ACH_FIRST_DROWN", 
				SteamAchievementId.EmployeeOfTheDay => "ACH_EMPLOYEE_OF_THE_DAY", 
				SteamAchievementId.LastCall => "ACH_LAST_CALL", 
				SteamAchievementId.NoMoreQuestionMarks => "ACH_NO_MORE_QUESTION_MARKS", 
				SteamAchievementId.CatTax => "ACH_CAT_TAX", 
				SteamAchievementId.ShellShocked => "ACH_SHELL_SHOCKED", 
				SteamAchievementId.FishyBusiness => "ACH_FISHY_BUSINESS", 
				SteamAchievementId.TheAbyssStaresBack => "ACH_THE_ABYSS_STARES_BACK", 
				SteamAchievementId.FromSeaToTable => "ACH_FROM_SEA_TO_TABLE", 
				SteamAchievementId.PiecesOfAMystery => "ACH_PIECES_OF_A_MYSTERY", 
				_ => string.Empty, 
			};
		}
	}
}
