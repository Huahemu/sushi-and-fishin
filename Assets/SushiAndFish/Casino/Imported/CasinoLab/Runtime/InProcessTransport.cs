namespace SushiAndFish.Casino.Original
{
using global::System;
using global::Mirror;

namespace CasinoLab
{
    // Mirror's host connection carries the original Commands/RPCs directly in
    // memory. No Steam lobby, network socket, or external client is used.
    public sealed class InProcessTransport:Transport
    {
        bool running;
        public override bool Available()=>true;
        public override bool ClientConnected()=>running && NetworkClient.isConnected;
        public override void ClientConnect(string address)=>throw new InvalidOperationException("This test project supports local host mode only.");
        public override void ClientSend(ArraySegment<byte> segment,int channelId=0) { }
        public override void ClientDisconnect() { }
        public override Uri ServerUri()=>new Uri("casino-lab://localhost");
        public override bool ServerActive()=>running;
        public override void ServerStart()=>running=true;
        public override void ServerSend(int connectionId,ArraySegment<byte> segment,int channelId=0) { }
        public override void ServerDisconnect(int connectionId) { }
        public override string ServerGetClientAddress(int connectionId)=>"local-test-player";
        public override void ServerStop()=>running=false;
        public override int GetMaxPacketSize(int channelId=0)=>65535;
        public override void Shutdown()=>running=false;
    }
}

}
