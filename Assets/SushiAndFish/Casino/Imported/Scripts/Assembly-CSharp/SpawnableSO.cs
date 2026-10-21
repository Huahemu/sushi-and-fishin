namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

[CreateAssetMenu(fileName = "SpawnableSO", menuName = "Spawnable/SpawnableSO")]
public class SpawnableSO : ScriptableObject
{
	[UniqueID("spawnables")]
	public int spawnableID;

	public string spawnableName;

	[TextArea(3, 10)]
	public string spawnableDescription;

	public GameObject prefab;
}

}
