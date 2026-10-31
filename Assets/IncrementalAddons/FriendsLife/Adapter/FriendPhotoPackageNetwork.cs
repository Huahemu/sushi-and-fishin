using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bozo.ModularCharacters;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    // Generated geometry is a versioned resource, separate from the small rules
    // snapshot. A complete validated package arrives before appearance commit.
    public sealed class FriendPhotoPackageNetwork:MonoBehaviour
    {
        const string ChunkMessage="FriendsLife/PhotoPackageChunk/v1",RequestMessage="FriendsLife/PhotoPackageRequest/v1",AckMessage="FriendsLife/PhotoPackageAck/v1";
        const int ChunkSize=48*1024,MaxBytes=128*1024*1024;
        sealed class Incoming{public string id;public byte[][] chunks;public int count,total;public float at;}
        static FriendPhotoPackageNetwork instance;NetworkManager network;bool registered;
        readonly Dictionary<string,Incoming> incoming=new Dictionary<string,Incoming>();readonly Dictionary<string,TaskCompletionSource<bool>> awaiting=new Dictionary<string,TaskCompletionSource<bool>>();readonly HashSet<string> requested=new HashSet<string>(),downloadSent=new HashSet<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Bootstrap(){var _=Instance;}
        public static FriendPhotoPackageNetwork Instance{get{if(!instance){var go=new GameObject("Friend Photo Resource Transport");DontDestroyOnLoad(go);instance=go.AddComponent<FriendPhotoPackageNetwork>();}return instance;}}
        public static void RequestPackage(string packageId)
        {if(!Guid.TryParseExact(packageId,"N",out _))return;Instance.requested.Add(packageId);}
        public static async Task<bool> ShareIfGenerated(CharacterData data)
        {
            var self=Instance;var network=NetworkManager.Singleton;if(!network||!network.IsListening||network.IsServer)return true;
            foreach(string id in data.outfitDatas.Select(o=>PackageId(o.outfit)).Where(s=>s!=null).Distinct())
            {
                if(!self.registered)return false;var source=new TaskCompletionSource<bool>();self.awaiting[id]=source;self.StartCoroutine(self.SendPackage(NetworkManager.ServerClientId,id));
                float until=Time.realtimeSinceStartup+120;while(!source.Task.IsCompleted&&Time.realtimeSinceStartup<until)await Task.Yield();self.awaiting.Remove(id);if(!source.Task.IsCompleted||!source.Task.Result)return false;
            }return true;
        }
        public static string PackageId(string path){var parts=(path??"").Split('/');return parts.Length==4&&parts[1]=="FriendsGenerated"&&Guid.TryParseExact(parts[2],"N",out _)?parts[2]:null;}
        void Awake(){instance=this;}
        void Update()
        {
            var current=NetworkManager.Singleton;if(network!=current||network&&!network.IsListening){Unregister();network=current;downloadSent.Clear();}
            if(network&&network.IsListening&&network.CustomMessagingManager!=null&&!registered)
            {network.CustomMessagingManager.RegisterNamedMessageHandler(ChunkMessage,OnChunk);network.CustomMessagingManager.RegisterNamedMessageHandler(RequestMessage,OnRequest);network.CustomMessagingManager.RegisterNamedMessageHandler(AckMessage,OnAck);registered=true;}
            if(registered&&!network.IsServer)foreach(string id in requested.Where(id=>!downloadSent.Contains(id)).ToArray()){SendString(RequestMessage,NetworkManager.ServerClientId,id);downloadSent.Add(id);}
            foreach(var pair in incoming.Where(p=>Time.unscaledTime-p.Value.at>120).ToArray())incoming.Remove(pair.Key);
        }
        void OnDestroy(){Unregister();if(instance==this)instance=null;}
        void Unregister(){if(network&&registered&&network.CustomMessagingManager!=null){network.CustomMessagingManager.UnregisterNamedMessageHandler(ChunkMessage);network.CustomMessagingManager.UnregisterNamedMessageHandler(RequestMessage);network.CustomMessagingManager.UnregisterNamedMessageHandler(AckMessage);}registered=false;}
        void OnRequest(ulong sender,FastBufferReader reader)
        {if(!network.IsServer||!network.ConnectedClients.ContainsKey(sender))return;reader.ReadValueSafe(out string id);if(!Guid.TryParseExact(id,"N",out _))return;StartCoroutine(SendPackage(sender,id));}
        void OnAck(ulong sender,FastBufferReader reader)
        {if(sender!=NetworkManager.ServerClientId)return;reader.ReadValueSafe(out string id);reader.ReadValueSafe(out bool accepted);if(awaiting.TryGetValue(id,out var task))task.TrySetResult(accepted);}
        void OnChunk(ulong sender,FastBufferReader reader)
        {
            if(network.IsServer&&!network.ConnectedClients.ContainsKey(sender)||!network.IsServer&&sender!=NetworkManager.ServerClientId)return;
            try
            {
                reader.ReadValueSafe(out string id);reader.ReadValueSafe(out int index);reader.ReadValueSafe(out int count);reader.ReadValueSafe(out int total);reader.ReadValueSafe(out int length);
                if(!Guid.TryParseExact(id,"N",out _)||count<1||count>MaxBytes/ChunkSize+1||total<1||total>MaxBytes||length<1||length>ChunkSize||index<0||index>=count||count!=(total+ChunkSize-1)/ChunkSize)return;
                string key=sender+"/"+id;if(!incoming.TryGetValue(key,out var pending)){if(incoming.Count>=8)return;incoming[key]=pending=new Incoming{id=id,chunks=new byte[count][],total=total,at=Time.unscaledTime};}
                if(pending.total!=total||pending.chunks.Length!=count)return;var bytes=new byte[length];reader.ReadBytesSafe(ref bytes,length);if(pending.chunks[index]==null){pending.chunks[index]=bytes;pending.count++;}pending.at=Time.unscaledTime;if(pending.count!=count)return;
                var all=new byte[total];int cursor=0;foreach(var chunk in pending.chunks){if(cursor+chunk.Length>all.Length)throw new InvalidDataException("Photo package chunk size mismatch");Buffer.BlockCopy(chunk,0,all,cursor,chunk.Length);cursor+=chunk.Length;}if(cursor!=total)throw new InvalidDataException("Photo package length mismatch");
                FriendGeneratedModels.ImportPackage(id,Encoding.UTF8.GetString(all));incoming.Remove(key);requested.Remove(id);downloadSent.Remove(id);
                if(network.IsServer){using(var writer=new FastBufferWriter(256,Allocator.Temp)){writer.WriteValueSafe(id);writer.WriteValueSafe(true);network.CustomMessagingManager.SendNamedMessage(AckMessage,sender,writer,NetworkDelivery.ReliableSequenced);}}
                else RefreshProjection(id);
            }catch(Exception error){Debug.LogWarning("[FriendsLife] Photo package rejected: "+error.Message);}
        }
        IEnumerator SendPackage(ulong target,string id)
        {
            byte[] all;try{all=Encoding.UTF8.GetBytes(FriendGeneratedModels.ExportPackage(id));}catch(Exception error){Debug.LogWarning(error.Message);yield break;}if(all.Length>MaxBytes)yield break;
            int count=(all.Length+ChunkSize-1)/ChunkSize;for(int index=0;index<count;index++)
            {
                if(!network||!network.IsListening)yield break;int length=Math.Min(ChunkSize,all.Length-index*ChunkSize);using(var writer=new FastBufferWriter(length+256,Allocator.Temp,length+256))
                {writer.WriteValueSafe(id);writer.WriteValueSafe(index);writer.WriteValueSafe(count);writer.WriteValueSafe(all.Length);writer.WriteValueSafe(length);writer.WriteBytesSafe(all,length,index*ChunkSize);network.CustomMessagingManager.SendNamedMessage(ChunkMessage,target,writer,NetworkDelivery.ReliableFragmentedSequenced);}yield return null;
            }
        }
        void SendString(string channel,ulong target,string value){using(var writer=new FastBufferWriter(256,Allocator.Temp)){writer.WriteValueSafe(value);network.CustomMessagingManager.SendNamedMessage(channel,target,writer,NetworkDelivery.ReliableSequenced);}}
        static void RefreshProjection(string id)
        {
            var runtime=FriendsLifeRuntime.Instance;if(!runtime)return;foreach(var person in runtime.GetPeople().Where(p=>(p.appearanceJson??"").Contains(id)))
            {
                var actor=runtime.ActorOf(person.personId);if(!actor||actor.ResidentData==null||actor.ResidentData.CharacterData==null)continue;
                // The resident's original customization path holds visibility,
                // activates a sleeping model while loading, then restores it.
                // Direct vendor loading under an inactive parent cannot attach outfits.
                FriendResidentBindings.ApplyAppearance(actor,FriendResidentBindings.DesiredAppearance(actor));
            }
        }
    }
}
