namespace SushiAndFish.Casino.Original
{
using global::System;

[Serializable]
public struct CardData
{
	public Suit Suit;

	public Rank Rank;

	public CardData(Suit suit, Rank rank)
	{
		Suit = suit;
		Rank = rank;
	}

	public int GetBlackjackValue()
	{
		switch (Rank)
		{
		case Rank.Jack:
		case Rank.Queen:
		case Rank.King:
			return 10;
		case Rank.Ace:
			return 11;
		default:
			return (int)Rank;
		}
	}

	public int GetBaccaratValue()
	{
		Rank rank = Rank;
		if ((uint)(rank - 11) <= 2u)
		{
			return 0;
		}
		return (int)Rank;
	}

	public string GetDisplayString()
	{
		string text;
		switch (Rank)
		{
		case Rank.Ace:
			text = "A";
			break;
		case Rank.Jack:
			text = "J";
			break;
		case Rank.Queen:
			text = "Q";
			break;
		case Rank.King:
			text = "K";
			break;
		default:
		{
			int rank = (int)Rank;
			text = rank.ToString();
			break;
		}
		}
		return text + Suit switch
		{
			Suit.Hearts => "♥", 
			Suit.Diamonds => "♦", 
			Suit.Clubs => "♣", 
			Suit.Spades => "♠", 
			_ => "?", 
		};
	}
}

}
