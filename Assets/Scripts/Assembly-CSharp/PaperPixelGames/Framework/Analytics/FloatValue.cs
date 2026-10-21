namespace PaperPixelGames.Framework.Analytics
{
	public class FloatValue : IEventValue
	{
		private readonly float _value;

		public FloatValue(float value)
		{
			_value = value;
		}

		public void SendEvent(string eventString)
		{
		}
	}
}
