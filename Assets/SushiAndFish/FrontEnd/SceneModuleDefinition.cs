using UnityEngine;

namespace SushiAndFish.FrontEnd
{
    [CreateAssetMenu(menuName="Sushi & Fish/Extracted scene module")]
    public sealed class SceneModuleDefinition : ScriptableObject
    {
        public string displayName;
        [TextArea] public string description;
        public string bootstrapScenePath;
        public string[] scenePaths;
        public string[] sceneNames;
        public string[] sceneGuids;
    }
}
