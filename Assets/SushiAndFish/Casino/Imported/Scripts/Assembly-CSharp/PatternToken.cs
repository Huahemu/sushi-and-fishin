namespace SushiAndFish.Casino.Original
{
public struct PatternToken
{
	public bool isLiteral;

	public string literal;

	public SlotType slot;

	public static PatternToken Lit(string text)
	{
		return new PatternToken
		{
			isLiteral = true,
			literal = text
		};
	}

	public static PatternToken Slot(SlotType s)
	{
		return new PatternToken
		{
			isLiteral = false,
			slot = s
		};
	}
}

}
