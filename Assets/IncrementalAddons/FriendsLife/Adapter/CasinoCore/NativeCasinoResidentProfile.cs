using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SushiAndFish.Casino.Original;
using UnityEngine;

namespace SushiAndFish.FriendsLife.CasinoCore
{
    public static class NativeCasinoResidentProfile
    {
        const uint ProfileAssetId=0xF19C4511u;
        static object registeredConnection;static bool registered;
        static readonly Dictionary<string,PlayerInteract> profiles=new Dictionary<string,PlayerInteract>(StringComparer.Ordinal);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){profiles.Clear();registered=false;registeredConnection=null;Mirror.NetworkClient.OnConnectedEvent-=RegisterClientSpawn;Mirror.NetworkClient.OnConnectedEvent+=RegisterClientSpawn;}
        static void RegisterClientSpawn()
        {
            if(registered&&ReferenceEquals(registeredConnection,Mirror.NetworkClient.connection))return;
            Mirror.NetworkClient.RegisterSpawnHandler(ProfileAssetId,SpawnClient,UnspawnClient);registered=true;registeredConnection=Mirror.NetworkClient.connection;
        }
        static GameObject SpawnClient(Mirror.SpawnMessage message)=>CreateParts("OriginalCasinoNpcReplica",message.position,message.rotation);
        static void UnspawnClient(GameObject instance){if(instance)UnityEngine.Object.Destroy(instance);}
        static GameObject CreateParts(string name,Vector3 position,Quaternion rotation)
        {
            var root=new GameObject(name);root.SetActive(false);UnityEngine.Object.DontDestroyOnLoad(root);root.transform.SetPositionAndRotation(position,rotation);
            root.AddComponent<Mirror.NetworkIdentity>();root.AddComponent<ActorCasinoIdentity>();
            root.AddComponent<NpcCasinoProfile>().enabled=false;root.AddComponent<PlayerBuff>().enabled=false;
            root.AddComponent<PlayerInteract>().enabled=false;root.AddComponent<PlayerEnergy>();root.AddComponent<NativeCasinoOriginalProfileState>();
            root.SetActive(true);return root;
        }
        public static bool TryGet(string actor,out PlayerInteract input,out string reason)
        {
            input=null;reason=null;var runtime=FriendsLifeRuntime.Instance;var economy=FriendsEconomyService.Instance;
            if(!runtime||!runtime.IsAuthority||!runtime.IsWorldRunning||!economy||runtime.GetPerson(actor)?.addedByPlayer!=true)
            {reason="Actual added NPC identity and personal economy required";return false;}
            string key=runtime.State.worldId+"/"+actor;
            if(profiles.TryGetValue(key,out input)&&input)
            {
                var originalEnergy=input.GetComponent<PlayerEnergy>();var cachedIdentity=input.GetComponent<Mirror.NetworkIdentity>();
                if(!originalEnergy||!cachedIdentity||cachedIdentity.netId==0||!Mirror.NetworkServer.spawned.TryGetValue(cachedIdentity.netId,out var actualIdentity)||actualIdentity!=cachedIdentity)
                {reason="Existing original NPC profile has no complete active source identity; preserve current session until an actual safe reload";return false;}
                var originalState=input.GetComponent<NativeCasinoOriginalProfileState>();
                if(!originalState){reason="Original NPC profile state binding is unavailable";return false;}
                if(!originalState.Matches(runtime,economy,actor))
                {
                    if(UnityEngine.Object.FindObjectsByType<GameBase>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(g=>g&&g.NativeActor==actor&&g.NativeHasUnsettledResult))
                    {reason="Existing live original round cannot reset its profile state from another save instance";return false;}
                    if(!originalState.Bind(runtime,economy,actor,out reason))return false;
                }
                return true;
            }
            var row=economy.State.casinoActors.FirstOrDefault(p=>p.personId==actor);
            if(row==null)
            {
                if(!Guid.TryParse(actor,out var id)){reason="Stable actual resident GUID required";return false;}
                ulong profileKey=0xF100000000000000UL|(BitConverter.ToUInt64(id.ToByteArray(),0)&0x00FFFFFFFFFFFFFFUL);
                while(economy.State.casinoActors.Any(p=>p.progressionKey==profileKey.ToString(CultureInfo.InvariantCulture)))profileKey++;
                row=new FriendCasinoActor{personId=actor,progressionKey=profileKey.ToString(CultureInfo.InvariantCulture)};economy.State.casinoActors.Add(row);
            }
            if(!ulong.TryParse(row.progressionKey,NumberStyles.None,CultureInfo.InvariantCulture,out var ownProfileKey))
            {reason="Resident original-progression key is invalid";return false;}
            RegisterClientSpawn();
            var root=CreateParts("OriginalCasinoNpcProfile_"+actor,Vector3.zero,Quaternion.identity);
            var identity=root.GetComponent<ActorCasinoIdentity>();identity.personId=actor;
            var profile=root.GetComponent<NpcCasinoProfile>();profile.playerName=runtime.GetPerson(actor).displayName;
            // This is a reserved original NPC progression slot, never a Steam
            // account, client connection or LocalManager player registration.
            profile.steamId=ownProfileKey;
            if(!root.GetComponent<NativeCasinoOriginalProfileState>().Bind(runtime,economy,actor,out reason))
            {UnityEngine.Object.Destroy(root);input=null;return false;}
            Mirror.NetworkServer.Spawn(root,ProfileAssetId);
            var networkIdentity=root.GetComponent<Mirror.NetworkIdentity>();input=root.GetComponent<PlayerInteract>();
            if(networkIdentity.netId==0||!Mirror.NetworkServer.spawned.TryGetValue(networkIdentity.netId,out var actual)||actual!=networkIdentity)
            {reason="Original NPC source profile did not obtain its real server network identity";UnityEngine.Object.Destroy(root);input=null;return false;}
            // Spawn invokes original PlayerBuff.OnStartServer defaults. Original
            // energy remains its actual component default/restored actor value.
            profiles[key]=input;runtime.NotifyChanged();return true;
        }
    }
    [Serializable] public sealed class NativeCasinoResult
    {
        public string id,actor,round,machine,type;public int ordinal;public long bet,payout,balanceAfterCents;
        public bool tipsy,melody,immunity;public Vector3 position;public string dataJson;
    }
    public static class NativeCasinoResultLedger
    {
        public static event Action<NativeCasinoResult> Recorded;
        public static bool Record(GameBase game,string actor,string round,int ordinal,long bet,long payout,bool tipsy,bool melody,bool immunity,Dictionary<string,object> data)
        {
            var economy=FriendsEconomyService.Instance;var runtime=FriendsLifeRuntime.Instance;
            if(!game||!economy||!runtime||!runtime.IsAuthority)return false;
            economy.State.originalCasinoResults=economy.State.originalCasinoResults??new List<NativeCasinoResult>();
            string id=round+"/result/"+ordinal;
            if(economy.State.originalCasinoResults.Any(r=>r.id==id))return true;
            var row=new NativeCasinoResult{id=id,actor=actor,round=round,ordinal=ordinal,machine=NativeCasinoTargetDirectory.Path(game.transform),
                type=game.GameType.ToString(),bet=bet,payout=payout,balanceAfterCents=economy.BalanceCents(actor),tipsy=tipsy,melody=melody,immunity=immunity,
                position=game.transform.position,dataJson=data==null?null:Newtonsoft.Json.JsonConvert.SerializeObject(data)};
            economy.State.originalCasinoResults.Add(row);runtime.NotifyChanged();Recorded?.Invoke(row);return true;
        }
    }
}
namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendsEconomySave
    {public List<CasinoCore.NativeCasinoResult> originalCasinoResults=new List<CasinoCore.NativeCasinoResult>();}
}
