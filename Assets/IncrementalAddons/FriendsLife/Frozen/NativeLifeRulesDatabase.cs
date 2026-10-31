using System;
using System.Collections.Generic;
using System.Linq;
#if !LIFE_SIMULATION_STANDALONE
using UnityEngine;
#endif

namespace TomodachiLife.Simulation
{
    public sealed class NativeLifeRulesDatabase
    {
        public NativeLifeRulesData Data { get; private set; }
        private readonly Dictionary<string, NativeLifeEventRule> events = new Dictionary<string, NativeLifeEventRule>();
        private readonly Dictionary<string, NativeLifeFoodRule> foods = new Dictionary<string, NativeLifeFoodRule>();
        private readonly Dictionary<string, NativeLifeGoodsRule> goods = new Dictionary<string, NativeLifeGoodsRule>();
        private readonly Dictionary<uint, NativeLifeConditionRule> conditions = new Dictionary<uint, NativeLifeConditionRule>();
        public NativeLifeRulesDatabase(NativeLifeRulesData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            foreach (var item in data.events ?? Array.Empty<NativeLifeEventRule>()) events.Add(item.name,item);
            foreach (var item in data.foods ?? Array.Empty<NativeLifeFoodRule>()) foods.Add(item.name,item);
            foreach (var item in data.goods ?? Array.Empty<NativeLifeGoodsRule>()) goods.Add(item.name,item);
            foreach (var item in data.conditions ?? Array.Empty<NativeLifeConditionRule>()) conditions.Add(item.id,item);
        }
        public static NativeLifeRulesDatabase FromJson(string json) => new NativeLifeRulesDatabase(LifeSimulationJson.Read<NativeLifeRulesData>(json));
        public static NativeLifeRulesDatabase Load()
        {
#if LIFE_SIMULATION_STANDALONE
            throw new InvalidOperationException("Standalone caller must use FromJson.");
#else
            var asset = Resources.Load<TextAsset>("LifeRules/native-life-rules");
            if (asset == null) throw new InvalidOperationException("LifeRules/native-life-rules resource is missing.");
            return FromJson(asset.text);
#endif
        }
        public static LifeSimulationConfiguration LoadConfiguration()
        {
#if LIFE_SIMULATION_STANDALONE
            return new LifeSimulationConfiguration();
#else
            var asset = Resources.Load<TextAsset>("LifeRules/reconstruction-config");
            return asset == null ? new LifeSimulationConfiguration() : LifeSimulationJson.Read<LifeSimulationConfiguration>(asset.text);
#endif
        }
        public NativeLifeEventRule FindEvent(string name) => name != null && events.TryGetValue(name,out var value) ? value : null;
        public NativeLifeFoodRule FindFood(string name) => name != null && foods.TryGetValue(name,out var value) ? value : null;
        public NativeLifeGoodsRule FindGoods(string name) => name != null && goods.TryGetValue(name,out var value) ? value : null;
        public NativeLifeConditionRule FindCondition(uint id) => conditions.TryGetValue(id,out var value) ? value : null;
        public bool TryGiftSatisfy(uint hash, out int amount)
        {
            var value = (Data.giftTastes ?? Array.Empty<NativeLifeGiftTasteRule>()).FirstOrDefault(v => v.hash == hash);
            amount = value == null ? 0 : value.satisfy; return value != null;
        }
    }
}
