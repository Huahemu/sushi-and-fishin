using System;
using System.Collections.Generic;
using System.Linq;

namespace SushiAndFish.FishingIntegration {
    // Data-only capture ownership. Runtime pending is a restore queue, never
    // the snapshot of catches that already exist in the scene.
    public static class FishingCaptureLedger {
        public static void Normalize(FishingWorldSave save){
            save.pending=save.pending??new List<SavedCatch>();save.stock=save.stock??new List<StoredFish>();save.acquired=save.acquired??new List<string>();save.resolved=save.resolved??new List<string>();
            save.resolved=save.resolved.Concat(save.acquired).Concat(save.stock.Where(s=>s!=null).Select(s=>s.token)).Where(t=>!string.IsNullOrEmpty(t)).Distinct(StringComparer.Ordinal).ToList();
            save.pending=save.pending.Where(p=>p!=null&&!IsResolved(save,p.token)).GroupBy(p=>p.token,StringComparer.Ordinal).Select(g=>g.First()).ToList();
        }
        public static bool IsResolved(FishingWorldSave save,string token)=>string.IsNullOrEmpty(token)||
            save.resolved!=null&&save.resolved.Contains(token)||save.acquired!=null&&save.acquired.Contains(token)||save.stock!=null&&save.stock.Any(s=>s!=null&&s.token==token);
        public static void Resolve(FishingWorldSave save,string token){
            if(string.IsNullOrEmpty(token))return;if(save.resolved==null)save.resolved=new List<string>();if(!save.resolved.Contains(token))save.resolved.Add(token);
            save.pending?.RemoveAll(p=>p==null||p.token==token);
        }
        public static FishingWorldSave Snapshot(FishingWorldSave save,IEnumerable<SavedCatch> live){
            var pending=(save.pending??new List<SavedCatch>()).Concat(live??Enumerable.Empty<SavedCatch>()).Where(p=>p!=null&&!IsResolved(save,p.token)).GroupBy(p=>p.token,StringComparer.Ordinal).Select(g=>g.Last()).ToList();
            return new FishingWorldSave{version=save.version,ownedWeapons=save.ownedWeapons,boat=save.boat,archive=save.archive,stock=save.stock,acquired=save.acquired,resolved=save.resolved,pending=pending};
        }
    }
}
