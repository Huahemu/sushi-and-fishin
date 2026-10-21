using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Netcode;
using SushiAndFish.CityExpansion;

public static partial class CityExpansionInstallation
{
    public static void AssignAndVerifyNetworkHashes()
    {
        // The recovered NGO player DLL has no Editor OnValidate code. Assign
        // stable hashes for these new scene objects through serialized APIs.
        var scene=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene);
        var seats=All(scene).Select(t=>t.GetComponent<CityDiningSeat>()).Where(s=>s).OrderBy(s=>s.seatId).ToArray();
        if(seats.Length!=12)throw new Exception("Saved scene must contain all 12 dining seats.");
        var networks=All(scene).Select(t=>t.GetComponent<NetworkObject>()).Where(n=>n).ToArray();
        var used=new HashSet<uint>(networks.Where(n=>!n.GetComponent<CityDiningSeat>()).Select(n=>(uint)new SerializedObject(n).FindProperty("GlobalObjectIdHash").longValue));
        foreach(var seat in seats)
        {
            var net=seat.GetComponent<NetworkObject>();
            string id=GlobalObjectId.GetGlobalObjectIdSlow(net).ToString();
            if(id.Contains("-00000000000000000000000000000000-"))throw new Exception("Seat has no saved scene identity.");
            uint value;using(var sha=SHA256.Create())value=BitConverter.ToUInt32(sha.ComputeHash(Encoding.UTF8.GetBytes("CityDiningSeat:"+id)),0);
            while(value==0||used.Contains(value))value++;
            used.Add(value);
            var data=new SerializedObject(net);data.FindProperty("GlobalObjectIdHash").longValue=value;
            var source=data.FindProperty("InScenePlacedSourceGlobalObjectIdHash");if(source!=null)source.longValue=0;
            data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(net);
        }
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        scene=EditorSceneManager.OpenScene(IslandFrontEndBuilder.GameScene);
        var hashes=All(scene).Select(t=>t.GetComponent<CityDiningSeat>()).Where(s=>s).Select(s=>(uint)new SerializedObject(s.GetComponent<NetworkObject>()).FindProperty("GlobalObjectIdHash").longValue).ToArray();
        if(hashes.Length!=12||hashes.Distinct().Count()!=12||hashes.Contains(0u))throw new Exception("Seat scene identities were not saved uniquely.");
        var receipt=JsonUtility.FromJson<WorldReceipt>(File.ReadAllText(WorldPath));
        foreach(var row in receipt.seats)
        {
            var seat=All(scene).Select(t=>t.GetComponent<CityDiningSeat>()).Single(s=>s&&s.seatId==row.id);
            row.networkHash=(uint)new SerializedObject(seat.GetComponent<NetworkObject>()).FindProperty("GlobalObjectIdHash").longValue;
        }
        receipt.diagnostics.Add("Reopened saved scene: all 12 native EmoteArea network hashes are unique and nonzero. Hashes are derived from persistent Unity GlobalObjectIds; original scene objects and the source EmoteArea prefab are unchanged.");
        File.WriteAllText(WorldPath,JsonUtility.ToJson(receipt,true));
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        Debug.Log("CITY_SAVED_NETWORK_SEATS_VERIFIED 12");
    }
}
