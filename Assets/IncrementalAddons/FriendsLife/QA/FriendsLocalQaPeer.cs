#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Netcode.Transports.Local;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.States.MainMenu;
using PaperPixelGames.Networking;
using SushiAndFish.FrontEnd;
using Unity.Netcode;
using UnityEngine;

namespace SushiAndFish.FriendsLife.QA
{
    // Opt-in development QA only; ordinary builds and normal launch never use
    // this transport or its local test identity. Steam authentication is not tested.
    public sealed class FriendsLocalQaPeer : MonoBehaviour
    {
        const string Token="FriendsLife-local-regression-20261025";
        string role,folder,lastCommand;bool started;float nextReport;int hostSlot=-1;
        FriendsLifeNetwork replies;
        readonly List<FriendsWorldResponse> responses=new List<FriendsWorldResponse>();
        [Serializable] sealed class Input{public string id,kind,targetName,action,option;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ConfigureQaMemory()
        {
            if(!Environment.GetCommandLineArgs().Contains("--friends-qa-low-memory")||!Application.persistentDataPath.Contains("WorldRepair QA"))return;
            QualitySettings.globalTextureMipmapLimit=2;QualitySettings.vSyncCount=0;Application.targetFrameRate=20;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--friends-qa-peer");
            if(index<0||index+1>=args.Length)return;
            string role=args[index+1];if(role!="host"&&role!="client")return;
            Create(role);
        }
        static FriendsLocalQaPeer Create(string role)
        {
            if(!Application.persistentDataPath.Contains("WorldRepair QA"))throw new InvalidOperationException("Local peer QA requires the isolated QA product");
            var existing=FindFirstObjectByType<FriendsLocalQaPeer>();if(existing)return existing;
            var peer=new GameObject("Explicit local multiplayer QA").AddComponent<FriendsLocalQaPeer>();
            peer.role=role;peer.folder=Path.Combine(Application.persistentDataPath,"FriendsLife","LocalPeerQa",role);
            Directory.CreateDirectory(peer.folder);DontDestroyOnLoad(peer.gameObject);Application.runInBackground=true;
            if(Environment.GetCommandLineArgs().Contains("--friends-qa-low-memory"))
            {QualitySettings.globalTextureMipmapLimit=2;QualitySettings.vSyncCount=0;Application.targetFrameRate=20;}
            return peer;
        }
        public static object StartHostFromEditor(){var peer=Create("host");return new{peer.role,peer.folder};}
        public static object StartHostSlotFromEditor(int slotIndex)
        {
            if(slotIndex<0)throw new ArgumentOutOfRangeException(nameof(slotIndex));
            var peer=Create("host");
            if(peer.started||NetworkManager.Singleton&&NetworkManager.Singleton.IsListening)throw new InvalidOperationException("Choose QA host slot before starting a session");
            peer.hostSlot=slotIndex;return new{peer.role,peer.folder,peer.hostSlot};
        }
        void Update()
        {
            try
            {
                if(!started&&NetworkManager.Singleton&&IslandFrontEndFlow.Active&&IslandFrontEndFlow.Active.BackdropLoaded)StartPeer();
                if(replies!=FriendsLifeNetwork.Instance)
                {if(replies)replies.ResponseReceived-=Response;replies=FriendsLifeNetwork.Instance;if(replies)replies.ResponseReceived+=Response;}
                if(Time.unscaledTime<nextReport||string.IsNullOrEmpty(folder))return;nextReport=Time.unscaledTime+2;
                HandleCommand();WriteStatus();
            }
            catch(Exception error){if(!string.IsNullOrEmpty(folder))File.WriteAllText(Path.Combine(folder,"error.txt"),error.ToString());Debug.LogException(error);enabled=false;}
        }
        void StartPeer()
        {
            var network=NetworkManager.Singleton;
            if(network.IsListening)throw new InvalidOperationException("Configure local QA before starting a session");
            var transport=network.gameObject.AddComponent<FriendsLocalQaTransport>();network.NetworkConfig.NetworkTransport=transport;
            if(role=="host")
            {
                var original=network.ConnectionApprovalCallback;
                network.ConnectionApprovalCallback=(request,response)=>
                {
                    if(request.ClientNetworkId==0){original?.Invoke(request,response);return;}
                    bool allowed=Encoding.UTF8.GetString(request.Payload??Array.Empty<byte>())==Token&&network.ConnectedClients.Count<2;
                    response.Approved=allowed;response.Pending=false;response.CreatePlayerObject=false;response.Reason=allowed?"":"Local QA peer rejected";
                    if(allowed)
                    {
                        var coordinator=FindFirstObjectByType<MultiplayerCoordinator>();
                        var field=typeof(MultiplayerCoordinator).GetField("_steamIdByClientId",BindingFlags.Instance|BindingFlags.NonPublic);
                        ((Dictionary<ulong,ulong>)field.GetValue(coordinator))[request.ClientNetworkId]=2;
                    }
                };
                IslandFrontEndFlow.Active.ShowLoadGames();
                var slot=Resources.FindObjectsOfTypeAll<SaveSlot>().Where(s=>s&&s.gameObject.scene.name=="MainMenuState"&&s.ContainsSave&&(hostSlot<0||s.SlotIndex==hostSlot)).OrderBy(s=>s.SlotIndex).FirstOrDefault();
                if(!slot)throw new InvalidOperationException("No isolated QA save slot");slot.LaunchOriginalSlot();
            }
            else
            {
                OfflineSingleplayerMode.SelectSaveProfile("2");network.NetworkConfig.ConnectionData=Encoding.UTF8.GetBytes(Token);
                if(!network.StartClient())throw new InvalidOperationException("Local QA connection did not start");
                Singleton<PaperPixelGames.Framework.States.StateManager>.Instance.ExitCurrentState();
            }
            started=true;
        }
        void Response(FriendsWorldResponse response)
        {responses.Add(response);if(responses.Count>64)responses.RemoveAt(0);}
        void HandleCommand()
        {
            string path=Path.Combine(folder,"command.json");if(!File.Exists(path))return;
            var input=JsonUtility.FromJson<Input>(File.ReadAllText(path));if(input==null||string.IsNullOrEmpty(input.id)||input.id==lastCommand)return;
            var runtime=FriendsLifeRuntime.Instance;if(!runtime||!runtime.Ready||runtime.LocalHuman()==null)return;
            var target=runtime.People.FirstOrDefault(p=>p.displayName==input.targetName);if(target==null)throw new InvalidOperationException("QA target not found");
            lastCommand=input.id;
            if(input.kind=="action"||input.kind=="duplicate-action")
            {
                replies.Submit(new FriendsWorldCommand{commandId=input.id,kind="action",actorPersonId=runtime.LocalHuman().personId,targetPersonId=target.personId,actionId=input.action,optionId=input.option});
                if(input.kind=="duplicate-action")replies.Submit(new FriendsWorldCommand{commandId=input.id,kind="action",actorPersonId=runtime.LocalHuman().personId,targetPersonId=target.personId,actionId=input.action,optionId=input.option});
            }
            else if(input.kind=="burst-profile")
            {
                var source=runtime.Core.FindResident(target.nativeResidentId);
                foreach(string suffix in new[]{"a","b"})replies.Submit(new FriendsWorldCommand{commandId=input.id+suffix,kind="profile",actorPersonId=runtime.LocalHuman().personId,targetPersonId=target.personId,
                    displayName=source.displayName,nativeProfileJson=source.avatarJson,catchphrase=source.catchphrase,hobby=input.option+suffix});
            }
            else if(input.kind=="housing")
                replies.Submit(new FriendsWorldCommand{commandId=input.id,kind="housing",actorPersonId=runtime.LocalHuman().personId,targetPersonId=target.personId,actionId=input.action,optionId=input.option});
        }
        void WriteStatus()
        {
            var network=NetworkManager.Singleton;var runtime=FriendsLifeRuntime.Instance;
            var status=new{utc=DateTime.UtcNow.ToString("O"),role,started,frame=Time.frameCount,
                listening=network&&network.IsListening,connected=network&&network.IsConnectedClient,server=network&&network.IsServer,clientId=network?network.LocalClientId:0,
                clients=network&&network.IsServer?network.ConnectedClientsIds.ToArray():Array.Empty<ulong>(),
                ready=runtime&&runtime.Ready,worldId=runtime?.State?.worldId,revision=runtime?.State?.revision,
                human=runtime?.LocalHuman()?.personId,lastCommand,responses,
                people=runtime&&runtime.Ready?runtime.People.Select(p=>new{p.personId,p.displayName,p.controllerKind,p.hostResidentId,actor=runtime.ActorOf(p.personId)!=null,
                    wallet=FriendsEconomyService.Instance?FriendsEconomyService.Instance.BalanceCents(p.personId):0}).ToArray():null};
            File.WriteAllText(Path.Combine(folder,"status.json"),Newtonsoft.Json.JsonConvert.SerializeObject(status,Newtonsoft.Json.Formatting.Indented));
        }
        void OnDestroy(){if(replies)replies.ResponseReceived-=Response;}
    }
}
#endif
