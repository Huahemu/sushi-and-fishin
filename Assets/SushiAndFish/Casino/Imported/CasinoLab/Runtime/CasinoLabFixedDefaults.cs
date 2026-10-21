namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Linq;
using SushiAndFish.Casino.Original.SettingsSystem;
using global::UnityEngine;
using global::UnityEngine.SceneManagement;

namespace CasinoLab
{
    [DefaultExecutionOrder(-29000)]
    public sealed class CasinoLabFixedDefaults:MonoBehaviour
    {
        [Serializable]
        public sealed class Value
        {
            public SettingItemBase item;
            public float number;
            public bool toggle;
            public int index;
            public string binding;
        }
        public SettingsLayout layout;
        public GraphicsSettings graphicsSettings;
        public Value[] values;
        public static CasinoLabFixedDefaults Instance {get;private set;}
        GraphicsSettingsApplier graphics;
        void Awake()
        {
            Instance=this;RestoreValues();
            if (CasinoRuntimeContext.IsHostIntegration) return;
            graphics=new GraphicsSettingsApplier(graphicsSettings);
            graphics.ApplyAllSettings();
            new SoundSettingsApplier().ApplyAll(layout);
            new FramerateSettingsApplier().ApplyAll(layout);
            var vsync=values.Select(v=>v.item).OfType<ToggleSettingItem>().FirstOrDefault(v=>v.key.Equals("vsync",StringComparison.OrdinalIgnoreCase));
            if(vsync)QualitySettings.vSyncCount=vsync.value?1:0;
            // The local test build keeps its configured window and resolution.
            // No SettingsPersistence or PlayerPrefs are read or written here.
            SceneManager.sceneLoaded+=SceneLoaded;
        }
        void OnDestroy(){SceneManager.sceneLoaded-=SceneLoaded;if(Instance==this)Instance=null;}
        void RestoreValues()
        {
            foreach(var value in values)
            {
                if(value.item is SliderSettingItem slider)slider.value=value.number;
                else if(value.item is ToggleSettingItem toggle)toggle.value=value.toggle;
                else if(value.item is DropdownSettingItem dropdown)dropdown.index=value.index;
                else if(value.item is RebindSettingItem rebind)rebind.overridePath=value.binding;
            }
        }
        void SceneLoaded(Scene scene,LoadSceneMode mode)
        {
            if (CasinoRuntimeContext.IsHostIntegration) return;
            RestoreValues();
            // Match the original built-in first-launch graphics application on scene load.
            foreach(var value in values)
            {
                bool apply=value.item is SliderSettingItem s?s.loadOnSceneStart:value.item is ToggleSettingItem t?t.loadOnSceneStart:value.item is DropdownSettingItem d && d.loadOnSceneStart;
                if(apply)graphics.Apply(value.item);
            }
        }
        public bool MatchesSnapshot()
        {
            return values.All(v=>v.item is SliderSettingItem s?Mathf.Approximately(s.value,v.number):v.item is ToggleSettingItem t?t.value==v.toggle:v.item is DropdownSettingItem d?d.index==v.index:v.item is RebindSettingItem r?r.overridePath==v.binding:true);
        }
        public string Snapshot()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(values.Where(v=>v.item).Select(v=>new{v.item.key,type=v.item.GetType().Name,value=CurrentValue(v)}));
        }
        static object CurrentValue(Value value)
        {
            if(value.item is SliderSettingItem slider)return slider.value;
            if(value.item is ToggleSettingItem toggle)return toggle.value;
            if(value.item is DropdownSettingItem dropdown)return dropdown.index;
            if(value.item is RebindSettingItem rebind)return rebind.overridePath;
            return null;
        }
    }
}

}
