using System;
using System.IO;
using Netcode.Transports.Local;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class OfflineSingleplayerSmoke
{
    const string Key="SushiAndFish.OfflineSmoke";
    [Serializable] class Report{public string utc,error,transport;public bool steamClientValid,started,host,localClientConnected,stopped;public int clients;}
    static NetworkManager manager;static Report report;static int frames;
    static OfflineSingleplayerSmoke(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Update;}
    public static void Start(){SessionState.SetBool(Key,true);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.update+=Update;EditorApplication.EnterPlaymode();}
    static void Update()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;
        try
        {
            if(report==null)
            {
                report=new Report{utc=DateTime.UtcNow.ToString("o"),steamClientValid=Steamworks.SteamClient.IsValid};
                var go=new GameObject("Offline host transport smoke");manager=go.AddComponent<NetworkManager>();var transport=go.AddComponent<LocalSingleplayerTransport>();
                manager.NetworkConfig=new NetworkConfig{NetworkTransport=transport,ConnectionApproval=true,EnableSceneManagement=false};
                manager.ConnectionApprovalCallback+=(request,response)=>{response.Approved=request.ClientNetworkId==NetworkManager.ServerClientId;response.CreatePlayerObject=false;response.Pending=false;};
                report.started=manager.StartHost();report.transport=manager.NetworkConfig.NetworkTransport.GetType().FullName;
            }
            if(++frames<20)return;
            report.host=manager.IsHost;report.clients=manager.ConnectedClients.Count;report.localClientConnected=manager.IsConnectedClient;
            manager.Shutdown();report.stopped=!manager.IsListening;
            Finish();
        }
        catch(Exception error){if(report==null)report=new Report();report.error=error.ToString();Finish();}
    }
    static void Finish()
    {
        SessionState.SetBool(Key,false);File.WriteAllText(Path.Combine(IslandFrontEndInventory.Reports,"offline-singleplayer-transport-smoke.json"),JsonUtility.ToJson(report,true));
        EditorApplication.Exit(string.IsNullOrEmpty(report.error)&&report.started&&report.host&&report.clients==1&&report.localClientConnected?0:2);
    }
}
