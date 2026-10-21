namespace SushiAndFish.Casino.Original
{
public interface IUIManager
{
	bool IsActive { get; }

	int Priority { get; }

	void CloseUI();

	void OpenUI();
}

}
