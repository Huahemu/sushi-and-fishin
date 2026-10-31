#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SushiAndFish.FriendsLife.Editor
{
    // Ship the added model/runtime notices beside the supported Windows Player.
    // This copies only the photo module's own text files, never game assets.
    public sealed class PhotoRuntimeBuildNotices:IPostprocessBuildWithReport
    {
        public int callbackOrder=>100;
        public void OnPostprocessBuild(BuildReport report)
        {
            if(report.summary.platform!=BuildTarget.StandaloneWindows64)return;
            string root=Path.Combine(Application.dataPath,"IncrementalAddons","FriendsLife","PhotoRuntime");
            string source=Path.Combine(root,"ThirdPartyNotices");
            if(!File.Exists(Path.Combine(source,"SegFormer-LICENSE.txt"))||!File.Exists(Path.Combine(source,"ONNXRuntime-LICENSE.txt")))throw new BuildFailedException("Photo runtime dependency notices are missing");
            string target=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report.summary.outputPath)),"ThirdPartyNotices","FriendsLifePhoto");
            Directory.CreateDirectory(target);foreach(string file in Directory.GetFiles(source))File.Copy(file,Path.Combine(target,Path.GetFileName(file)),true);
            File.Copy(Path.Combine(root,"README.md"),Path.Combine(target,"PHOTO-RUNTIME.md"),true);
        }
    }
}
#endif
