namespace SushiAndFish.Casino.Original
{
namespace SettingsSystem
{
	public interface ISettingsApplier
	{
		void Apply(SettingItemBase entry);

		void ApplyAll(SettingsLayout layout);
	}
}

}
