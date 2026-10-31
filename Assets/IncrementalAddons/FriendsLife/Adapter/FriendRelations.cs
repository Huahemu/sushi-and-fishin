using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TomodachiLife.Simulation;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // Local gates until the lead's FriendsLifeFeatureFlags is repointed here.
    // Hidden kinds keep their data; they only never start on their own.
    public static class FriendRelationFlags
    {
        public static readonly bool AutoRomance=false,Children=false,Cohabit=false,FamilyHousing=false;
        public const double EpisodeTimeoutMinutes=120;
        public static bool AutoAllowed(string action)
        {
            switch(action)
            {
                case "Befriend":case "BestFriends":case "Fight":case "Reconcile":case "Comfort":return true;
                case "Confess":case "Date":case "Propose":case "Wedding":case "Breakup":case "Reunite":return AutoRomance;
                case "Baby":case "Birth":case "Care":case "Grow":return Children;
                case "Cohabit":return Cohabit;
                default:return false;
            }
        }
    }

    // Tomodachi per-pair level on the existing relationship table (FamilyBond).
    // a and b are person ids or native resident ids; either may be the player.
    public static class FriendRelations
    {
        public const int Known=0,Friend=1,GoodFriend=2,BestFriend=3;
        public static string Label(string a,string b)
        {var r=FriendsLifeRuntime.Instance;return Resolve(r,a,b,out var x,out var y)?r.Core.FamilyRelationshipLabel(x,y):"";}
        public static int Level(string a,string b)
        {var r=FriendsLifeRuntime.Instance;return Resolve(r,a,b,out var x,out var y)?r.Core.FamilyRelationLevel(x,y):Known;}
        // Five steps on the source meter: 认识 starts at 2, 朋友 3, 好朋友 4, 挚友 range 5.
        public static int Bar(string a,string b)
        {var r=FriendsLifeRuntime.Instance;return Resolve(r,a,b,out var x,out var y)?Mathf.Clamp((r.Core.FamilyFriendship(x,y)+19)/40,0,5):0;}
        public static string Recent(string a,string b)
        {
            var r=FriendsLifeRuntime.Instance;if(!Resolve(r,a,b,out var x,out var y)||r.Core.State.family==null)return "";
            return r.Core.State.family.memories.LastOrDefault(m=>m.kind!="choice"&&(m.focusId==x&&m.targetId==y||m.focusId==y&&m.targetId==x))?.message??"";
        }
        // Chats, gifts and help from the player (M2) use this. Positive gains are
        // scaled by compatibility; a pair with the player can reach 挚友 directly.
        public static bool Add(string a,string b,int delta,string reason)
        {
            var r=FriendsLifeRuntime.Instance;if(delta==0||!r||!r.IsAuthority||!Resolve(r,a,b,out var x,out var y))return false;
            var first=r.PersonFromNative(x);var second=r.PersonFromNative(y);var payer=first??second;if(payer==null)return false;
            bool player=first?.controllerKind==PersonControllerKind.Human||second?.controllerKind==PersonControllerKind.Human;
            var receipt=r.ExecuteKernel("relation/"+Guid.NewGuid().ToString("N"),payer.personId,scope=>scope.AdjustFamilyBond(x,y,delta,reason,player).success,null,false);
            if(!receipt.success)Debug.LogWarning("[FriendsLife] Relation change skipped ("+reason+"): "+receipt.message);
            return receipt.success;
        }
        internal static int Roll(string salt)
        {uint seed=2166136261u;foreach(char ch in salt??"")seed=unchecked((seed^ch)*16777619u);return (int)(seed%100);}
        internal static bool Resolve(FriendsLifeRuntime r,string a,string b,out string x,out string y)
        {x=Native(r,a);y=Native(r,b);return x!=null&&y!=null&&x!=y;}
        static string Native(FriendsLifeRuntime r,string id)
        {
            if(!r||!r.Ready||string.IsNullOrEmpty(id))return null;
            if(id=="player")return r.LocalHuman()?.nativeResidentId;
            return (r.GetPerson(id)??r.State.people.FirstOrDefault(p=>p.displayName==id))?.nativeResidentId;
        }
    }

    // Editor-eval hooks for play-tests. Host only; ids may be person ids, native ids,
    // display names or "player".
    public static class FriendRelationsDebug
    {
        public static string Dump()
        {
            var r=FriendsLifeRuntime.Instance;if(!r||!r.Ready)return "FriendsLife is not ready.";
            var sb=new StringBuilder();var people=r.GetPeople();var k=r.Core;
            for(int i=0;i<people.Length;i++)for(int j=i+1;j<people.Length;j++)
            {
                string x=people[i].nativeResidentId,y=people[j].nativeResidentId;
                var to=k.State.relationships.FirstOrDefault(v=>v.fromId==x&&v.toId==y);var back=k.State.relationships.FirstOrDefault(v=>v.fromId==y&&v.toId==x);
                if(to==null&&back==null)continue;
                sb.AppendLine(people[i].displayName+" <-> "+people[j].displayName+": "+k.FamilyRelationshipLabel(x,y)+" level="+k.FamilyRelationLevel(x,y)+
                    " bar="+FriendRelations.Bar(x,y)+" meter="+to?.meter+"/"+back?.meter+" kind="+to?.kind+"/"+back?.kind+" rank="+to?.nativeRank+
                    " fighting="+(to?.fighting==true||back?.fighting==true)+" compatibility="+k.FamilyCompatibility(x,y));
            }
            foreach(var e in r.State.episodes.Where(e=>!e.committed&&e.sourceKind=="Family"))
                sb.AppendLine("open episode "+e.episodeId+" "+e.familyChoice?.action+" status="+e.status+" age="+(k.State.gameMinutes-e.sourceMinute).ToString("0")+"min");
            sb.AppendLine("cancelled episodes="+r.State.episodes.Count(e=>e.sourceKind=="Family"&&e.status=="Cancelled")+" next pair moment in "+((k.State.family?.nextSuggestionGameMinutes??0)-k.State.gameMinutes).ToString("0")+"min");
            string text=sb.ToString();Debug.Log("[FriendsLife] Relations\n"+text);return text;
        }
        public static string ForceQuarrel(string a,string b)=>Run(a,b,"debug-quarrel",(k,x,y)=>k.ForceFamilyQuarrel(x,y));
        public static string SetLevel(string a,string b,int level)=>Run(a,b,"debug-level",(k,x,y)=>k.SetFamilyRelationLevel(x,y,level));
        // Starts the next autonomous pair moment on the next world step and counts
        // every friend pair as met today (skips waiting for them to walk past).
        public static string PairMomentNow()
        {
            var r=FriendsLifeRuntime.Instance;if(!r||!r.Ready||!r.IsAuthority)return "Host only.";
            r.MarkAllFriendsMet();r.Core.EnsureFamilyState().nextSuggestionGameMinutes=0;r.MarkChanged();return "Pair moment scheduled.";
        }
        // Ages every open family episode past the timeout; the next tick cancels it.
        public static string ExpireEpisodes()
        {
            var r=FriendsLifeRuntime.Instance;if(!r||!r.Ready||!r.IsAuthority)return "Host only.";int n=0;
            foreach(var e in r.State.episodes.Where(e=>!e.committed&&e.sourceKind=="Family")){e.sourceMinute=r.Core.State.gameMinutes-FriendRelationFlags.EpisodeTimeoutMinutes*2-1;n++;}
            r.MarkChanged();return n+" episodes will time out.";
        }
        static string Run(string a,string b,string op,Func<LifeSimulationRuntime,string,string,LifeSimulationResolution> command)
        {
            var r=FriendsLifeRuntime.Instance;if(!r||!r.Ready||!r.IsAuthority)return "Host only.";
            if(!FriendRelations.Resolve(r,a,b,out var x,out var y))return "Unknown person.";
            string message=null;
            var receipt=r.ExecuteKernel(op+"/"+Guid.NewGuid().ToString("N"),r.PersonFromNative(x).personId,scope=>{var result=command(scope,x,y);message=result.message;return result.success;},null,false);
            if(receipt.success)r.SaveNow();return receipt.success?message:receipt.message;
        }
    }

    public sealed partial class FriendsLifeRuntime
    {
        const float RelationMeetMeters=10f;
        readonly Dictionary<string,double> relationMeetings=new Dictionary<string,double>(StringComparer.Ordinal);
        readonly List<PaperPixelGames.Core.Ai.NPCs.ResidentController> relationBodies=new List<PaperPixelGames.Core.Ai.NPCs.ResidentController>();
        readonly List<string> relationIds=new List<string>();
        readonly HashSet<string> relationBusy=new HashSet<string>(StringComparer.Ordinal);
        float nextRelationScan;
        // The kernel is replaced on every scope commit, so the ports are re-bound here.
        void BindRelationPorts()
        {
            if(Core.FamilyPairContact==null)Core.FamilyPairContact=RelationPairContact;
            if(Core.FamilyAutoAllowed==null)Core.FamilyAutoAllowed=FriendRelationFlags.AutoAllowed;
        }
        // Friends meet when their bodies are close; "met today" pairs get moments.
        void ObserveRelationMeetings()
        {
            if(Time.unscaledTime<nextRelationScan)return;nextRelationScan=Time.unscaledTime+2f;
            relationBodies.Clear();relationIds.Clear();relationBusy.Clear();
            foreach(var e in State.episodes)if(!e.committed&&e.sourceKind=="Family"){if(e.focusPersonId!=null)relationBusy.Add(e.focusPersonId);if(e.targetPersonId!=null)relationBusy.Add(e.targetPersonId);}
            foreach(var person in State.people)
            {
                if(person.controllerKind!=PersonControllerKind.Npc)continue;var actor=ActorOf(person.personId);
                if(actor&&actor.isActiveAndEnabled){relationBodies.Add(actor);relationIds.Add(person.nativeResidentId);}
            }
            for(int i=0;i<relationBodies.Count;i++)for(int j=i+1;j<relationBodies.Count;j++)
                if((relationBodies[i].transform.position-relationBodies[j].transform.position).sqrMagnitude<=RelationMeetMeters*RelationMeetMeters)
                    relationMeetings[RelationKey(relationIds[i],relationIds[j])]=Core.State.gameMinutes;
        }
        internal void MarkAllFriendsMet()
        {
            var npcs=State.people.Where(p=>p.controllerKind==PersonControllerKind.Npc).Select(p=>p.nativeResidentId).ToArray();
            for(int i=0;i<npcs.Length;i++)for(int j=i+1;j<npcs.Length;j++)relationMeetings[RelationKey(npcs[i],npcs[j])]=Core.State.gameMinutes;
        }
        static string RelationKey(string a,string b)=>string.CompareOrdinal(a,b)<0?a+"|"+b:b+"|"+a;
        // -1: the player, unbound, working or already in a moment; 0: free; 1: met today.
        int RelationPairContact(string a,string b)
        {
            var x=PersonFromNative(a);var y=PersonFromNative(b);
            if(x?.controllerKind!=PersonControllerKind.Npc||y?.controllerKind!=PersonControllerKind.Npc||!ActorOf(x.personId)||!ActorOf(y.personId))return -1;
            var economy=FriendsEconomyService.Instance;if(economy&&(economy.HasActiveJob(x.personId)||economy.HasActiveJob(y.personId)))return -1;
            if(relationBusy.Contains(x.personId)||relationBusy.Contains(y.personId))return -1;
            return relationMeetings.TryGetValue(RelationKey(a,b),out var met)&&Math.Floor(met/1440)==Math.Floor(Core.State.gameMinutes/1440)?1:0;
        }
    }
}
