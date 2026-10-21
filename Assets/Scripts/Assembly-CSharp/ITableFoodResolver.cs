using PaperPixelGames.Core.Data;
using Unity.Collections;

public interface ITableFoodResolver
{
	bool TryGetServedDish(FixedString32Bytes id, out ServedDishSO so);

	bool TryGetBeverage(FixedString32Bytes id, out ServedDishSO so);

	bool TryGetSideDish(FixedString32Bytes id, out ServedDishSO so);
}
