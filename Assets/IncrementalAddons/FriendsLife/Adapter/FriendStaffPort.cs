using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using DistantLands.Cozy;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Patterns;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // Friend-owned minute contracts use the original team, payroll and work
    // runtime. Ownership is the slot/id/hire-sequence/role tuple, never a label.
    public static class FriendStaffPort
    {
        [Serializable] sealed class Marker {public int employeeId,hireSequence;public StaffRole role;public string personId,name;}
        [Serializable] sealed class Markers {public int version=1;public List<Marker> entries=new List<Marker>();}
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly FieldInfo TeamField=typeof(StaffManager).GetField("_team",Flags),RuntimeField=typeof(StaffManager).GetField("_runtime",Flags);
        static readonly MethodInfo Generate=typeof(StaffManager).GetMethod("GenerateCandidate",Flags),Recalculate=typeof(StaffManager).GetMethod("RecalculateNextIds",Flags);
        static StaffManager bound;static NetworkList<StaffEmployeeEntry> team;static bool correcting;static string loadedSlot;static Markers markers=new Markers();
        static readonly HashSet<string> newThisSession=new HashSet<string>();
        public static string LastFailure {get;private set;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset()
        {StaffManager.InstanceAvailable-=Bind;CozyWeather.Events.onNewHour-=BeforePayroll;if(team!=null)team.OnListChanged-=Changed;bound=null;team=null;loadedSlot=null;markers=new Markers();newThisSession.Clear();correcting=false;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Install()
        {StaffManager.InstanceAvailable+=Bind;CozyWeather.Events.onNewHour+=BeforePayroll;if(StaffManager.Instance)Bind(StaffManager.Instance);}
        static void Bind(StaffManager manager)
        {
            if(bound==manager)return;if(team!=null)team.OnListChanged-=Changed;bound=manager;team=TeamField?.GetValue(manager)as NetworkList<StaffEmployeeEntry>;if(team!=null)team.OnListChanged+=Changed;
            if(!manager.GetComponent<FriendStaffPortObserver>())manager.gameObject.AddComponent<FriendStaffPortObserver>();
        }
        static void Changed(NetworkListEvent<StaffEmployeeEntry> change){NormalizeOwned();}
        static void BeforePayroll(){NormalizeOwned();}
        static string Slot()
        {
            var save=Singleton<SaveManager>.Instance;if(!save||save.CurrentSaveSlotIndex<0)return null;string host=new string((save.CurrentSaveSlotHostSteamID??"local").Where(char.IsLetterOrDigit).ToArray());return host+"-"+save.CurrentSaveSlotIndex;
        }
        static string MarkerKey(Marker m)=>loadedSlot+"/"+m.employeeId+"/"+m.hireSequence+"/"+m.personId;
        static string FilePath=>Path.Combine(Application.persistentDataPath,"FriendsLife","StaffMinuteContracts",loadedSlot+".json");
        static bool LoadMarkers()
        {
            string slot=Slot();if(slot==null)return false;if(slot==loadedSlot)return true;loadedSlot=slot;markers=File.Exists(FilePath)?JsonUtility.FromJson<Markers>(File.ReadAllText(FilePath)):new Markers();if(markers==null||markers.version!=1)throw new InvalidDataException("Friend staff ownership marker is invalid");return true;
        }
        static void SaveMarkers(){Directory.CreateDirectory(Path.GetDirectoryName(FilePath));File.WriteAllText(FilePath,JsonUtility.ToJson(markers));}
        static bool SavedAsOwned(StaffEmployeeEntry entry)
        {
            var save=Singleton<SaveManager>.Instance;if(!save||!save.TryGetCurrentSaveSlot(out var slot))return false;
            return slot.TabletData?.StaffData?.Team?.Any(e=>e.EmployeeId==entry.EmployeeId&&e.HireSequence==entry.HireSequence&&e.Role==entry.Role&&e.DailyWageDollars==0)==true;
        }
        public static void NormalizeOwned()
        {
            if(correcting||!bound||!bound.IsServer||team==null)return;
            try
            {
                if(!LoadMarkers())return;correcting=true;
                for(int i=0;i<team.Count;i++)
                {
                    var entry=team[i];var marker=markers.entries.FirstOrDefault(m=>m.employeeId==entry.EmployeeId&&m.hireSequence==entry.HireSequence&&m.role==entry.Role);
                    if(marker==null||(!newThisSession.Contains(MarkerKey(marker))&&!SavedAsOwned(entry)))continue;
                    // Original zero-spend returns false. Mark only OUR minute
                    // contract as handled for this day before original payroll,
                    // so its unchanged predicate skips an extra daily payment.
                    if(entry.DailyWageDollars!=0||entry.LastPaidDay!=bound.CurrentDay){entry.DailyWageDollars=0;entry.LastPaidDay=bound.CurrentDay;team[i]=entry;}
                }
            }
            catch(Exception e){LastFailure=e.Message;Debug.LogError("[FriendsLife] Staff ownership restore: "+e.Message);}
            finally{correcting=false;}
        }
        public static bool TryHire(StaffManager manager,string personId,StaffRole role)
        {
            LastFailure=null;var friends=FriendsEconomyService.Instance;var life=FriendsLifeRuntime.Instance;
            if(!manager||!manager.IsServer||!manager.IsSpawned||!friends||!life||life.GetPerson(personId)==null||!StaffRules.IsRoleEnabled(role)||!manager.IsRoleUnlocked(role)||manager.IsRoleFull(role))return false;
            Bind(manager);var runtime=RuntimeField?.GetValue(manager)as StaffRuntime;if(team==null||runtime==null||Generate==null||Recalculate==null||!runtime.CanSpawn(out _,role))return false;
            try
            {
                if(!LoadMarkers())return false;var employee=(StaffEmployeeEntry)Generate.Invoke(manager,new object[]{role});var saved=manager.Save();if(saved==null)return false;
                var retained=friends.SushiAuthorization(personId,role)?.retainedEmployee;
                if(retained!=null&&retained.EmployeeId>0){employee.SkillLevel=StaffRules.ClampSkill(retained.SkillLevel);employee.BuyMissingStock=retained.BuyMissingStock;employee.GreenMarketEnabled=retained.GreenMarketEnabled;employee.BlueMarketEnabled=retained.BlueMarketEnabled;employee.StockingStopped=retained.StockingStopped;employee.RiceCookerEnabled=retained.RiceCookerEnabled;employee.FilletMachineEnabled=retained.FilletMachineEnabled;employee.PrepareOrdersEnabled=retained.PrepareOrdersEnabled;employee.StockConveyorEnabled=retained.StockConveyorEnabled;employee.TakeOrdersEnabled=retained.TakeOrdersEnabled;employee.ServeOrdersEnabled=retained.ServeOrdersEnabled;employee.CollectFilletsEnabled=retained.CollectFilletsEnabled;}
                string name=life.GetPerson(personId).displayName;if(string.IsNullOrWhiteSpace(name))name=employee.Name.ToString();while(Encoding.UTF8.GetByteCount(name)>60)name=name.Substring(0,name.Length-1);
                if(saved.Team.Any(e=>e.Name==name)){LastFailure="Friend name conflicts with an existing employee";return false;}
                employee.Name=new FixedString64Bytes(name);employee.HireSequence=Math.Max(1,saved.NextHireSequence);employee.DailyWageDollars=0;employee.LastPaidDay=manager.CurrentDay;
                var marker=new Marker{employeeId=employee.EmployeeId,hireSequence=employee.HireSequence,role=role,personId=personId,name=name};
                // Persist ownership before reserving contract money; an IO
                // failure cannot leave a half-created reserved contract.
                markers.entries.RemoveAll(m=>m.employeeId==marker.employeeId);markers.entries.Add(marker);newThisSession.Add(MarkerKey(marker));SaveMarkers();
                using(friends.DeferExternalCommit())
                {
                    if(!friends.BindEmployee(personId,employee.EmployeeId)){markers.entries.Remove(marker);newThisSession.Remove(MarkerKey(marker));SaveMarkers();return false;}
                    team.Add(employee);Recalculate.Invoke(manager,null);manager.Ledger.Register(employee);friends.RememberStaffProfile(personId,Snapshot(manager,employee.EmployeeId));runtime.Spawn(employee);friends.StaffEmployeeSpawned(personId,employee.EmployeeId);
                }
                return true;
            }
            catch(Exception e){LastFailure=e.GetBaseException().Message;Debug.LogError("[FriendsLife] New friend staff hire failed: "+LastFailure);return false;}
        }
        // The next shift of a kept friend employee: same entry and EmployeeId,
        // a new body at the original staff spawn.
        public static bool TryResume(StaffManager manager,string personId,int employeeId)
        {
            LastFailure=null;var friends=FriendsEconomyService.Instance;
            if(!manager||!manager.IsServer||!manager.IsSpawned||!friends||!manager.TryGetEmployee(employeeId,out var employee)||employee.Departure!=StaffDeparture.None||!IsOwnedEntry(employeeId,personId))return false;
            Bind(manager);var runtime=RuntimeField?.GetValue(manager)as StaffRuntime;if(runtime==null||!runtime.CanSpawn(out _,employee.Role))return false;
            using(friends.DeferExternalCommit())
            {
                if(!friends.BindEmployee(personId,employeeId))return false;
                manager.ServerSetStatus(employeeId,StaffStatus.Waiting);runtime.Spawn(employee);friends.StaffEmployeeSpawned(personId,employeeId);
            }
            return true;
        }
        // A friend who quit or changed role leaves the team once the body is
        // gone. It is removed like a completed departure, but never as fired.
        public static void ReleaseUnclaimed(StaffManager manager,Func<int,bool> claimed)
        {
            if(!manager||!manager.IsServer||bound!=manager||team==null)return;
            try
            {
                if(!LoadMarkers())return;bool changed=false;
                for(int i=team.Count-1;i>=0;i--)
                {
                    var entry=team[i];if(entry.Departure!=StaffDeparture.None||claimed(entry.EmployeeId)||!IsOwnedEntry(entry.EmployeeId)||NpcFor(entry.EmployeeId))continue;
                    team.RemoveAt(i);markers.entries.RemoveAll(m=>m.employeeId==entry.EmployeeId);changed=true;
                }
                if(changed)SaveMarkers();
            }
            catch(Exception e){LastFailure=e.Message;Debug.LogWarning("[FriendsLife] Friend staff release: "+e.Message);}
        }
        public static StaffNpc NpcFor(int employeeId)
        {
            var manager=StaffManager.Instance;if(!manager)return null;Bind(manager);
            var runtime=RuntimeField?.GetValue(manager)as StaffRuntime;
            return runtime!=null&&runtime.TryGetNpc(employeeId,out var npc)?npc:null;
        }
        public static StaffEmployeeData Snapshot(StaffManager manager,int id)=>manager?manager.Save()?.Team?.FirstOrDefault(e=>e.EmployeeId==id):null;
        public static bool IsOwnedEntry(int id,string personId=null)
        {
            if(!bound||!bound.TryGetEmployee(id,out var entry)||!LoadMarkers())return false;
            return markers.entries.Any(m=>m.employeeId==entry.EmployeeId&&m.hireSequence==entry.HireSequence&&m.role==entry.Role&&(personId==null||m.personId==personId)&&(newThisSession.Contains(MarkerKey(m))||SavedAsOwned(entry)));
        }
    }
    [DefaultExecutionOrder(-31000)]public sealed class FriendStaffPortObserver:MonoBehaviour
    {
        float next;
        void Update()
        {
            FriendStaffPort.NormalizeOwned();if(Time.unscaledTime<next)return;next=Time.unscaledTime+.5f;var friends=FriendsEconomyService.Instance;if(!friends)return;
            // Only a live employee contract can bind an NPC; its body comes from the cached lookup.
            foreach(var job in friends.State.jobs)
            {
                if(job.employeeId==0||job.state==FriendJobPhase.Done)continue;var npc=friends.StaffNpcFor(job.employeeId,false);
                if(npc&&!npc.GetComponent<FriendStaffVisual>()&&FriendStaffPort.IsOwnedEntry(job.employeeId))npc.gameObject.AddComponent<FriendStaffVisual>();
            }
        }
    }
}
