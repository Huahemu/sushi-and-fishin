#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TomodachiLife.Simulation;
using UnityEngine;

namespace SushiAndFish.FriendsLife.Editor
{
    // Synthetic input sequences verify accounting/ownership; this is not evidence
    // that a native graph, a world navigation or a human choice has run.
    public static class FriendsLifeInvariantChecks
    {
        [Serializable] public sealed class Report
        {
            public bool passed;
            public List<string> checks = new List<string>();
            public string error;
        }
        public static string Run()
        {
            var report = new Report(); GameObject temporary = null;
            try
            {
                if (Application.isPlaying) throw new InvalidOperationException("Run accounting checks in Edit mode.");
                var sourceRules = NativeLifeRulesDatabase.Load();
                var config = NativeLifeRulesDatabase.LoadConfiguration();
                var rulesData = LifeSimulationJson.Read<NativeLifeRulesData>(LifeSimulationJson.Write(sourceRules.Data, false));
                var fixture = new NativeLifeEventRule { name = "FriendsLifeSyntheticOwnershipFixture", enabled = true,
                    branches = new[] { new NativeLifeBranchRule { index = 0, results = new[] { new NativeLifeResultRule {
                        index = 0, demoResult = "ExplicitFixtureResult", rewardMoney0 = 120, rewardMoney1 = 230,
                        feeling0 = 3069107721, feeling1 = 3069107721, relations = Array.Empty<NativeLifeRelationEffect>() } } } } };
                rulesData.events = rulesData.events.Concat(new[] { fixture }).ToArray();
                var rules = new NativeLifeRulesDatabase(rulesData);
                var kernel = LifeSimulationRuntime.CreateNew(rules, config);
                var a = kernel.CreateResident("Fixture A"); var b = kernel.CreateResident("Fixture B");
                temporary = new GameObject("FriendsLife accounting check (temporary)") { hideFlags = HideFlags.HideAndDontSave };
                var runtime = temporary.AddComponent<FriendsLifeRuntime>();
                SetProperty(runtime, "Core", kernel);
                SetProperty(runtime, "State", new FriendsLifeSave { worldId = "synthetic-fixture", people = new List<PersonBinding> {
                    new PersonBinding {personId="A",nativeResidentId=a.id,displayName="Fixture A"},
                    new PersonBinding {personId="B",nativeResidentId=b.id,displayName="Fixture B"} } });
                SetField(runtime, "rules", rules); SetField(runtime, "configuration", config);
                var balances = new Dictionary<string, long> { ["A"] = 1000, ["B"] = 2000 };
                var walletOps = new HashSet<string>();
                runtime.ReadWalletCents = id => balances[id];
                runtime.CommitWalletDelta = (id, delta, op) => { if (!walletOps.Add(op)) return true; if (balances[id] + delta < 0) return false; balances[id] += delta; return true; };
                runtime.CommitWalletBatch = (deltas, op) =>
                {
                    if (!walletOps.Add(op)) return true;
                    var combined = deltas.GroupBy(d => d.personId).ToDictionary(g => g.Key, g => g.Sum(d => d.deltaCents));
                    if (combined.Any(row => balances[row.Key] + row.Value < 0)) return false;
                    foreach (var row in combined) balances[row.Key] += row.Value;
                    return true;
                };
                int calls = 0;
                var spend = runtime.ExecuteKernel("fixture/spend", "A", scope => { calls++; return scope.SpendGameplayMoney(100, "fixture"); });
                Require(spend.success && balances["A"] == 900 && balances["B"] == 2000, "Actor A payment changed actor B.");
                runtime.ExecuteKernel("fixture/spend", "A", scope => { calls++; return scope.SpendGameplayMoney(100, "fixture"); });
                Require(calls == 1 && balances["A"] == 900, "Duplicate operation executed twice.");
                report.checks.Add("Scoped payment and duplicate receipt retain separate actor balances.");
                int beforeEdges = runtime.Core.State.relationships.Count;
                var failed = runtime.ExecuteKernel("fixture/reject", "A", scope => { scope.Relationship(a.id, b.id).meter = 199; return false; });
                Require(!failed.success && runtime.Core.State.relationships.Count == beforeEdges && balances["A"] == 900, "Rejected scope leaked a source relation or money mutation.");
                report.checks.Add("Rejected scope leaves committed relation graph and wallets unchanged.");
                var pending = new LifeSimulationPendingEvent { id = "synthetic-pending", eventName = fixture.name, focusResidentId = a.id, targetResidentId = b.id };
                runtime.State.episodes.Add(new SourceEpisode { episodeId = "synthetic-episode", sourceId = pending.id, sourceKind = "MiiEvent", eventName = fixture.name,
                    branchIndex = 0, nativePending = pending, focusPersonId = "A", targetPersonId = "B", opId = "fixture/native-slots" });
                var wrong = runtime.CompleteNativeEpisode("synthetic-episode", "UnmatchedResult");
                Require(!wrong.success && balances["A"] == 900 && balances["B"] == 2000, "Unknown result was settled.");
                var exact = runtime.CompleteNativeEpisode("synthetic-episode", "ExplicitFixtureResult");
                Require(exact.success && balances["A"] == 1020 && balances["B"] == 2230 && exact.allocations.Count == 2, "Source slot rewards were pooled or misattributed.");
                runtime.CompleteNativeEpisode("synthetic-episode", "ExplicitFixtureResult");
                Require(balances["A"] == 1020 && balances["B"] == 2230, "Source result paid more than once.");
                report.checks.Add("Exact DemoResult and one-time original slot rewards settle to their actual owners.");
                long revision = runtime.State.revision;
                int relationships = runtime.Core.State.relationships.Count;
                runtime.GetPeople(); runtime.GetPerson("A"); runtime.GetRelationships("A"); runtime.GetEpisodes("A");
                Require(runtime.State.revision == revision && runtime.Core.State.relationships.Count == relationships, "Presentation getter mutated source state.");
                report.checks.Add("Presentation reads do not create relations or move the world revision.");
                var draft = runtime.Core.CreateResident("Awaiting actual housing");
                draft.hunger = 83;
                runtime.State.people.Add(new PersonBinding { personId = "draft", nativeResidentId = draft.id, displayName = draft.displayName, addedByPlayer = true });
                float activeHunger = runtime.Core.FindResident(a.id).hunger;
                typeof(FriendsLifeRuntime).GetMethod("AdvanceFrozenWorld", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(runtime, new object[] { 1d });
                Require(runtime.Core.FindResident(draft.id).hunger == 83 && runtime.Core.FindResident(a.id).hunger < activeHunger, "A draft was advanced as an arrived resident.");
                Require(runtime.Core.State.family.choices.All(q => q.focusId != draft.id && q.targetId != draft.id) && runtime.Core.State.daily.requests.All(q => q.residentId != draft.id && q.targetId != draft.id), "Source eligibility included an unarrived draft.");
                Require(runtime.Core.State.residents.Count == 3, "Temporary roster projection dropped a registered draft.");
                report.checks.Add("Unarrived draft is preserved for editing but excluded from world clock, population and source participants.");
                report.passed = true;
            }
            catch (Exception error) { report.error = error.ToString(); }
            finally { if (temporary != null) UnityEngine.Object.DestroyImmediate(temporary); }
            return JsonUtility.ToJson(report, true);
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static void SetProperty(object owner, string name, object value) => owner.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance).SetValue(owner, value);
        static void SetField(object owner, string name, object value) => owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);
        public static string ValidateHostBindings()
        {
            var missing = FriendsHostVisualBindings.OutfitResources.Where(path => Resources.Load<Bozo.ModularCharacters.Outfit>(path) == null).ToArray();
            var catalog = LifeCreativeCatalog.Load();
            var unboundClothes = catalog.Data.clothes.Where(c => FriendsHostVisualBindings.OutfitResource(c) == null).Select(c => c.id).ToArray();
            var unboundRooms = catalog.Data.rooms.Where(r => !FriendsHostVisualBindings.HasRoomTheme(r.id)).Select(r => r.id).ToArray();
            return "host outfit templates=" + FriendsHostVisualBindings.OutfitResources.Length + "; missing=" + string.Join(",", missing) +
                "; clothing mappings=" + catalog.Data.clothes.Length + "; unbound=" + string.Join(",", unboundClothes) +
                "; room mappings=" + catalog.Data.rooms.Length + "; unbound=" + string.Join(",", unboundRooms);
        }
    }
}
#endif
