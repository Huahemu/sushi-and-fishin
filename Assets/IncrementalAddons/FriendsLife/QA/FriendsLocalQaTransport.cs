#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Unity.Netcode;
using UnityEngine;

namespace SushiAndFish.FriendsLife.QA
{
    // Explicit loopback-only QA transport. Never selected by the production flow.
    // It carries actual NGO messages between separate game processes. TCP is used
    // for deterministic regression, not to claim Steam or packet-loss coverage.
    public sealed class FriendsLocalQaTransport : NetworkTransport
    {
        struct Event { public NetworkEvent kind; public ulong id; public byte[] bytes; }
        readonly ConcurrentQueue<Event> events=new ConcurrentQueue<Event>();
        readonly Dictionary<ulong,TcpClient> peers=new Dictionary<ulong,TcpClient>();
        readonly object peersLock=new object();
        TcpListener listener;volatile bool running;ulong nextId=1;
        public int port=17891;
        public override ulong ServerClientId=>0;
        public override void Initialize(NetworkManager networkManager=null){}
        public override bool StartServer()
        {
            try
            {
                listener=new TcpListener(IPAddress.Loopback,port);listener.Start();running=true;
                new Thread(Accept){IsBackground=true,Name="Friends QA loopback accept"}.Start();return true;
            }
            catch(Exception e){Debug.LogError("[Friends QA] Local server: "+e.Message);return false;}
        }
        void Accept()
        {
            while(running)
            {
                try{var peer=listener.AcceptTcpClient();ulong id=nextId++;Attach(id,peer);}
                catch(SocketException){if(!running)return;}
                catch(ObjectDisposedException){return;}
            }
        }
        public override bool StartClient()
        {
            try{var peer=new TcpClient();peer.Connect(IPAddress.Loopback,port);running=true;Attach(0,peer);return true;}
            catch(Exception e){Debug.LogError("[Friends QA] Local client: "+e.Message);return false;}
        }
        void Attach(ulong id,TcpClient peer)
        {
            peer.NoDelay=true;peer.SendTimeout=2000;lock(peersLock)peers[id]=peer;
            events.Enqueue(new Event{kind=NetworkEvent.Connect,id=id});
            new Thread(()=>Read(id,peer)){IsBackground=true,Name="Friends QA loopback receive"}.Start();
        }
        static bool ReadExactly(Stream stream,byte[] bytes)
        {
            int at=0;while(at<bytes.Length){int n=stream.Read(bytes,at,bytes.Length-at);if(n==0)return false;at+=n;}return true;
        }
        void Read(ulong id,TcpClient peer)
        {
            try
            {
                var stream=peer.GetStream();var header=new byte[4];
                while(running&&ReadExactly(stream,header))
                {
                    int length=BitConverter.ToInt32(header,0);if(length<0||length>4*1024*1024)break;
                    var packet=new byte[length];if(!ReadExactly(stream,packet))break;
                    events.Enqueue(new Event{kind=NetworkEvent.Data,id=id,bytes=packet});
                }
            }
            catch(IOException){}catch(SocketException){}catch(ObjectDisposedException){}
            finally{lock(peersLock)peers.Remove(id);peer.Close();if(running)events.Enqueue(new Event{kind=NetworkEvent.Disconnect,id=id});}
        }
        public override void Send(ulong clientId,ArraySegment<byte> payload,NetworkDelivery delivery)
        {
            TcpClient peer;lock(peersLock)if(!peers.TryGetValue(clientId,out peer))return;
            try
            {
                lock(peer){var stream=peer.GetStream();var header=BitConverter.GetBytes(payload.Count);stream.Write(header,0,4);stream.Write(payload.Array,payload.Offset,payload.Count);}
            }
            catch(IOException){DisconnectRemoteClient(clientId);}catch(ObjectDisposedException){}
        }
        public override NetworkEvent PollEvent(out ulong clientId,out ArraySegment<byte> payload,out float receiveTime)
        {
            receiveTime=Time.realtimeSinceStartup;clientId=0;payload=default;
            if(!events.TryDequeue(out var item))return NetworkEvent.Nothing;
            clientId=item.id;if(item.bytes!=null)payload=new ArraySegment<byte>(item.bytes);return item.kind;
        }
        public override void DisconnectRemoteClient(ulong clientId)
        {lock(peersLock){if(peers.TryGetValue(clientId,out var peer)){peers.Remove(clientId);peer.Close();}}}
        public override void DisconnectLocalClient()=>DisconnectRemoteClient(0);
        public override ulong GetCurrentRtt(ulong clientId)=>0;
        public override void Shutdown()
        {
            running=false;listener?.Stop();listener=null;
            lock(peersLock){foreach(var peer in peers.Values)peer.Close();peers.Clear();}
            while(events.TryDequeue(out _)){}
        }
        void OnDestroy()=>Shutdown();
    }
}
#endif
