namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;

public interface IDropdownOptionsProvider
{
	List<string> GetOptions();

	int GetDefaultIndex(List<string> options);
}

}
