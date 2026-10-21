using System;

namespace Bozo.ModularCharacters
{
	public interface IOutfitExtension
	{
		string GetID();

		void Initalize(OutfitSystem outfitSystem, Outfit outfit);

		void Execute(OutfitSystem outfitSystem, Outfit outfit);

		object GetValue();

		Type GetValueType();
	}
	public interface IOutfitExtension<T> : IOutfitExtension
	{
		new T GetValue();
	}
}
