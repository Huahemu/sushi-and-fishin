using System;
using Unity.Netcode;
using UnityEngine;

namespace Netcode.Transports.Local
{
    // NGO's host invokes its own local client directly. No sockets, Steam
    // connection, remote clients, or synthetic Steam sessions are opened.
    [DisallowMultipleComponent]
    public sealed class LocalSingleplayerTransport : NetworkTransport
    {
        public override ulong ServerClientId=>0;
        public override void Initialize(NetworkManager networkManager=null){}
        public override bool StartServer()=>true;
        public override bool StartClient()=>false;
        public override void Send(ulong clientId,ArraySegment<byte> payload,NetworkDelivery networkDelivery){}
        public override NetworkEvent PollEvent(out ulong clientId,out ArraySegment<byte> payload,out float receiveTime)
        {clientId=0;payload=default;receiveTime=Time.realtimeSinceStartup;return NetworkEvent.Nothing;}
        public override void DisconnectRemoteClient(ulong clientId){}
        public override void DisconnectLocalClient(){}
        public override ulong GetCurrentRtt(ulong clientId)=>0;
        public override void Shutdown(){}
    }
}
