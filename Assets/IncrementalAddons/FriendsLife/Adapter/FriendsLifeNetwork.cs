using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    [Serializable] public sealed class FriendsWorldCommand
    {
        public string commandId, worldId, kind = "action", actorPersonId, targetPersonId, actionId, optionId;
        public string displayName, nativeProfileJson, appearanceJson, catchphrase, hobby;
    }
    [Serializable] public sealed class FriendsWorldResponse
    {
        public string commandId, personId, message;
        public bool accepted, unresolved;
        public long revision;
    }

    /// <summary>Named NGO messages carry an authority snapshot and explicit human inputs. Clients never tick or settle a kernel.</summary>
    public sealed class FriendsLifeNetwork : MonoBehaviour
    {
        public static FriendsLifeNetwork Instance { get; private set; }
        const string SnapshotMessage = "FriendsLife/Snapshot/v2", RequestMessage = "FriendsLife/Request/v2", ResponseMessage = "FriendsLife/Response/v2", HelloMessage = "FriendsLife/Hello/v2";
        const int MaxPayload = 4 * 1024 * 1024;
        const int MaxDecodedPayload = 16 * 1024 * 1024;
        public event Action<FriendsWorldResponse> ResponseReceived;
        // Housing UI requires the real command transport; the standalone
        // source-action fallback is not a housing submission or an ACK.
        public bool CanSubmitHousingCommand=>runtime&&runtime.Ready&&network&&network==NetworkManager.Singleton&&network.IsListening&&network.CustomMessagingManager!=null&&
            (runtime.IsAuthority?network.IsServer:registered&&network.IsConnectedClient);

        NetworkManager network;
        FriendsLifeRuntime runtime;
        bool registered, helloSent;
        long sentRevision = -1;
        float nextSend, nextBinding, nextHello;
        readonly Dictionary<string, FriendsWorldResponse> responses = new Dictionary<string, FriendsWorldResponse>();
        sealed class PendingRequest
        {
            public FriendsWorldCommand command;
            public float nextAttempt, expiresAt;
        }
        readonly Dictionary<string, PendingRequest> pendingRequests = new Dictionary<string, PendingRequest>();

        void Awake() { Instance = this; runtime = GetComponent<FriendsLifeRuntime>(); }
        void OnDestroy() { Unregister(); if (Instance == this) Instance = null; }
        void Update()
        {
            if (runtime == null) runtime = FriendsLifeRuntime.Instance;
            var current = NetworkManager.Singleton;
            if (network != current || network != null && !network.IsListening) { Unregister(); network = current; }
            if (network == null || !network.IsListening || network.CustomMessagingManager == null || runtime == null) return;
            if (!registered) Register();
            if (!network.IsServer)
            {
                if ((!helloSent || !runtime.Ready) && network.IsConnectedClient && Time.unscaledTime >= nextHello)
                { nextHello = Time.unscaledTime + 2f; SendJson(HelloMessage, NetworkManager.ServerClientId, "{}"); }
                if (runtime.Ready && Time.unscaledTime >= nextBinding) { nextBinding = Time.unscaledTime + 1f; runtime.RefreshActorBindings(); }
                RetryPendingRequests();
                return;
            }
            if (!runtime.Ready) return;
            if (Time.unscaledTime >= nextBinding) { nextBinding = Time.unscaledTime + 1f; runtime.RefreshActorBindings(); }
            if (Time.unscaledTime < nextSend || runtime.State.revision == sentRevision) return;
            nextSend = Time.unscaledTime + 1f;
            string snapshot = null;
            bool prepared = false;
            foreach (ulong client in network.ConnectedClientsIds)
                if (client != network.LocalClientId)
                {
                    if (!prepared) { snapshot = runtime.ExportSnapshot(); prepared = true; }
                    SendJson(SnapshotMessage, client, snapshot);
                }
            if (!prepared) runtime.PrepareLiveStateWithoutSnapshot();
            sentRevision = runtime.State.revision;
        }
        void Register()
        {
            network.CustomMessagingManager.RegisterNamedMessageHandler(SnapshotMessage, OnSnapshot);
            network.CustomMessagingManager.RegisterNamedMessageHandler(RequestMessage, OnRequest);
            network.CustomMessagingManager.RegisterNamedMessageHandler(ResponseMessage, OnResponse);
            network.CustomMessagingManager.RegisterNamedMessageHandler(HelloMessage, OnHello);
            network.OnClientConnectedCallback += OnClientConnected;
            network.OnClientDisconnectCallback += OnClientDisconnected;
            registered = true; helloSent = false; sentRevision = -1;
        }
        void Unregister()
        {
            if (network != null)
            {
                if (registered && network.CustomMessagingManager != null)
                {
                    network.CustomMessagingManager.UnregisterNamedMessageHandler(SnapshotMessage);
                    network.CustomMessagingManager.UnregisterNamedMessageHandler(RequestMessage);
                    network.CustomMessagingManager.UnregisterNamedMessageHandler(ResponseMessage);
                    network.CustomMessagingManager.UnregisterNamedMessageHandler(HelloMessage);
                }
                network.OnClientConnectedCallback -= OnClientConnected;
                network.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            registered = false; helloSent = false;
        }
        void OnClientConnected(ulong client)
        {
            if (!network.IsServer || runtime == null || !runtime.Ready || client == network.LocalClientId) return;
            runtime.RefreshActorBindings();
            SendJson(SnapshotMessage, client, runtime.ExportSnapshot());
        }
        void OnClientDisconnected(ulong client) { if (client == network.LocalClientId) helloSent = false; }
        void OnHello(ulong sender, FastBufferReader reader)
        {
            if (network == null || !network.IsServer || !runtime.Ready || !network.ConnectedClients.ContainsKey(sender)) return;
            SendJson(SnapshotMessage, sender, runtime.ExportSnapshot());
        }
        void OnSnapshot(ulong sender, FastBufferReader reader)
        {
            if (network == null || network.IsServer || sender != NetworkManager.ServerClientId) return;
            try { helloSent = runtime.ApplySnapshot(ReadJson(reader)); runtime.RefreshActorBindings(); }
            catch (Exception error) { Debug.LogWarning("[FriendsLife] Snapshot rejected: " + error.Message); }
        }
        void OnResponse(ulong sender, FastBufferReader reader)
        {
            if (network == null || network.IsServer || sender != NetworkManager.ServerClientId) return;
            try
            {
                var response = JsonUtility.FromJson<FriendsWorldResponse>(ReadJson(reader));
                if (response == null || string.IsNullOrEmpty(response.commandId)) return;
                pendingRequests.Remove(response.commandId);
                ResponseReceived?.Invoke(response);
            }
            catch (Exception error) { Debug.LogWarning("[FriendsLife] Response rejected: " + error.Message); }
        }
        void OnRequest(ulong sender, FastBufferReader reader)
        {
            if (network == null || !network.IsServer || !runtime.Ready || !network.ConnectedClients.ContainsKey(sender)) return;
            try
            {
                var command = JsonUtility.FromJson<FriendsWorldCommand>(ReadJson(reader));
                if (command == null || string.IsNullOrEmpty(command.commandId) || command.commandId.Length > 80) return;
                runtime.RefreshActorBindings();
                var boundSender=runtime.HumanForClient(sender);
                if(boundSender==null||command.actorPersonId!=boundSender.personId||command.worldId!=runtime.State.worldId)
                {SendJson(ResponseMessage,sender,JsonUtility.ToJson(new FriendsWorldResponse{commandId=command.commandId,accepted=false,message="请重新进入游戏后再试。",revision=runtime.State.revision}));return;}
                string cacheKey = sender + "/" + command.worldId + "/" + command.commandId;
                if (responses.TryGetValue(cacheKey, out var previous)) { SendJson(ResponseMessage, sender, JsonUtility.ToJson(previous)); return; }
                var response = ExecuteForSender(sender, command);
                // A failed create can already have a person ID. Allow its corrected draft
                // to retry the same origin command; successful mutations stay idempotent.
                if (response.accepted) responses[cacheKey] = response;
                if (responses.Count > 512) responses.Remove(responses.Keys.First());
                SendJson(ResponseMessage, sender, JsonUtility.ToJson(response));
                if (response.accepted) SendJson(SnapshotMessage, sender, runtime.ExportSnapshot());
            }
            catch (Exception error) { Debug.LogWarning("[FriendsLife] Command rejected for client " + sender + ": " + error.Message); }
        }
        FriendsWorldResponse ExecuteForSender(ulong sender, FriendsWorldCommand command)
        {
            var response = new FriendsWorldResponse { commandId = command.commandId, message = "操作无效。", revision = runtime.State.revision };
            runtime.RefreshActorBindings();
            var human = runtime.HumanForClient(sender);
            if (human == null || command.actorPersonId != human.personId || command.worldId != runtime.State.worldId)
            { response.message = "请重新进入游戏后再试。"; return response; }
            if (command.kind == "create")
            {
                var existing = runtime.State.people.FirstOrDefault(p => p.originCommandId == command.commandId);
                var added = existing ?? runtime.CreatePerson(command.displayName, command.nativeProfileJson, PersonControllerKind.Npc, command.commandId);
                if (added == null) { response.message = runtime.LastError; return response; }
                added.originCommandId = command.commandId;
                if (!string.IsNullOrEmpty(command.appearanceJson) && !runtime.SetAppearance(added.personId, command.appearanceJson))
                { response.personId = added.personId; response.message = runtime.LastError ?? "外貌资料无效，草稿已保留。"; return response; }
                response.accepted = true; response.personId = added.personId; response.message = "朋友草稿已保存。"; runtime.SaveNow();
                response.revision = runtime.State.revision;
                return response;
            }
            var target = runtime.GetPerson(command.targetPersonId);
            if (target == null) return response;
            if (target.controllerKind == PersonControllerKind.Human && target.personId != human.personId)
            { response.message = "只能提交你本人的资料和输入。"; return response; }
            if (command.kind == "profile")
            {
                response.accepted = runtime.UpdatePersonProfile(target.personId, command.displayName, command.nativeProfileJson, command.catchphrase, command.hobby);
                response.message = response.accepted ? "资料已保存。" : runtime.LastError;
            }
            else if (command.kind == "appearance")
            {
                response.accepted = runtime.SetAppearance(target.personId, command.appearanceJson);
                response.message = response.accepted ? "外貌已保存。" : runtime.LastError;
            }
            else if (command.kind == "housing")
            {
                var economy = FriendsEconomyService.Instance;
                response.accepted = economy != null && economy.ExecuteHousingCommand(command, human.personId);
                response.message = response.accepted ? "操作已提交。" : economy?.LastFailure ?? "服务尚未就绪。";
            }
            else if (command.kind == "world-talk")
            {
                response.accepted = FriendWorldTalkAuthority.Instance.Execute(runtime, human.personId, command, out var message);
                response.unresolved = FriendWorldTalkAuthority.Find(runtime, command.optionId)?.phase == "Unknown";
                response.message = message;
            }
            else if (command.kind == "action")
            {
                if (!runtime.CanSubmitHumanAction(human.personId, target.personId, command.actionId))
                { response.message = "这不是你的操作。"; return response; }
                string op = "human/" + human.personId + "/" + command.commandId;
                response.message = runtime.SubmitSourceActionAs(human.personId, target.personId, command.actionId, command.optionId, op);
                response.accepted = runtime.State.receipts.Any(r => r.opId == op && r.success) ||
                    command.actionId.StartsWith("family:", StringComparison.Ordinal) && runtime.State.episodes.Any(e => e.sourceId == command.actionId.Substring(7) && (e.committed || e.inputAccepted));
            }
            response.personId = target.personId; response.revision = runtime.State.revision;
            return response;
        }
        public string RequestAction(string targetPersonId, string actionId, string optionId = null)
        {
            var human = runtime?.LocalHuman();
            if (human == null) return "玩家资料尚未同步。";
            return Submit(new FriendsWorldCommand { actorPersonId = human.personId, targetPersonId = targetPersonId, actionId = actionId, optionId = optionId });
        }
        public string Submit(FriendsWorldCommand command)
        {
            if (runtime == null || !runtime.Ready || command == null) return "正在加载，请稍候。";
            if (string.IsNullOrEmpty(command.commandId)) command.commandId = Guid.NewGuid().ToString("N");
            command.worldId = runtime.State.worldId;
            if (runtime.IsAuthority)
            {
                if (network == null || !network.IsListening)
                {
                    if (command.kind == "world-talk")
                    {
                        bool accepted = FriendWorldTalkAuthority.Instance.Execute(runtime, command.actorPersonId, command, out var result);
                        ResponseReceived?.Invoke(new FriendsWorldResponse { commandId = command.commandId, personId = command.targetPersonId, accepted = accepted, unresolved = FriendWorldTalkAuthority.Find(runtime, command.optionId)?.phase == "Unknown", message = result, revision = runtime.State.revision });
                        return result;
                    }
                    return runtime.SubmitSourceActionAs(command.actorPersonId, command.targetPersonId, command.actionId, command.optionId, "human/" + command.actorPersonId + "/" + command.commandId);
                }
                var response = ExecuteForSender(network.LocalClientId, command); ResponseReceived?.Invoke(response); return response.message;
            }
            if (network == null || !registered) return "正在连接，请稍后再试。";
            pendingRequests[command.commandId] = new PendingRequest { command = command, nextAttempt = Time.unscaledTime + 2f, expiresAt = Time.unscaledTime + 115f };
            SendJson(RequestMessage, NetworkManager.ServerClientId, JsonUtility.ToJson(command));
            return "已发送，请稍候。";
        }
        void RetryPendingRequests()
        {
            if (pendingRequests.Count == 0) return;
            foreach (var pair in pendingRequests.ToArray())
            {
                var pending = pair.Value;
                if (Time.unscaledTime >= pending.expiresAt || runtime.Ready && pending.command.worldId != runtime.State.worldId)
                {
                    pendingRequests.Remove(pair.Key);
                    ResponseReceived?.Invoke(new FriendsWorldResponse { commandId = pair.Key, unresolved = true, message = "暂未收到操作结果。" });
                    continue;
                }
                if (!network.IsConnectedClient || !runtime.Ready || Time.unscaledTime < pending.nextAttempt) continue;
                pending.nextAttempt = Time.unscaledTime + 2f;
                SendJson(RequestMessage, NetworkManager.ServerClientId, JsonUtility.ToJson(pending.command));
            }
        }
        void SendJson(string channel, ulong target, string json)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json ?? "");
            if (bytes.Length > MaxDecodedPayload) throw new InvalidOperationException("Friends snapshot exceeds the decoded transport boundary.");
            bool compressed = bytes.Length > 16384;
            if (compressed)
            {
                using (var output = new MemoryStream())
                {
                    using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Fastest, true)) gzip.Write(bytes, 0, bytes.Length);
                    bytes = output.ToArray();
                }
            }
            if (bytes.Length > MaxPayload) throw new InvalidOperationException("Friends snapshot exceeds the explicit transport boundary.");
            using (var writer = new FastBufferWriter(Math.Max(64, bytes.Length + 4), Allocator.Temp, MaxPayload + 4))
            {
                writer.WriteValueSafe(compressed ? -bytes.Length : bytes.Length);
                writer.WriteBytesSafe(bytes);
                network.CustomMessagingManager.SendNamedMessage(channel, target, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }
        static string ReadJson(FastBufferReader reader)
        {
            reader.ReadValueSafe(out int length);
            bool compressed = length < 0;
            if (length == int.MinValue) throw new InvalidOperationException("Invalid Friends message size.");
            if (compressed) length = -length;
            if (length > MaxPayload || length > reader.Length - reader.Position) throw new InvalidOperationException("Invalid Friends message size.");
            byte[] bytes = new byte[length];
            for (int i = 0; i < length; i++) reader.ReadByteSafe(out bytes[i]);
            if (compressed)
            {
                using (var input = new MemoryStream(bytes))
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    var buffer = new byte[8192]; int read;
                    while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (output.Length + read > MaxDecodedPayload) throw new InvalidOperationException("Decoded Friends message is too large.");
                        output.Write(buffer, 0, read);
                    }
                    bytes = output.ToArray();
                }
            }
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
