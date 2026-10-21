namespace PaperPixelGames.Core
{
	public interface ISaveable<T>
	{
		T Save();

		void Load(T data);
	}
}
