using System;
using System.IO;
using UnityEngine;

namespace SushiAndFish.FriendsLife
{
    public static class PhotoFriendPaths
    {
        public static string Folder(string name)
        {
            string root=Path.Combine(Application.persistentDataPath,"FriendsLife");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Two local processes otherwise share the same OS product folder.
            // An explicit QA client must really receive the host's resources.
            var args=Environment.GetCommandLineArgs();int peer=Array.IndexOf(args,"--friends-qa-peer");
            if(Application.persistentDataPath.IndexOf("WorldRepair QA",StringComparison.Ordinal)>=0&&peer>=0&&peer+1<args.Length&&args[peer+1]=="client")
                root=Path.Combine(root,"LocalPeerClient");
#endif
            return Path.Combine(root,name);
        }
    }
}
