using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
namespace SushiAndFish.FriendsLife
{
    // The path comes only from the persistent original photo record.
    public sealed class FriendSavedPhotoImage:MonoBehaviour
    {
        static readonly Dictionary<string,Texture2D> images=new Dictionary<string,Texture2D>();
        static readonly Queue<string> order=new Queue<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){foreach(var image in images.Values)if(image)Destroy(image);images.Clear();order.Clear();}
        public void Bind(string path)
        {
            if(string.IsNullOrEmpty(path)||!File.Exists(path))return;
            try
            {
                if(!images.TryGetValue(path,out var texture)||!texture)
                {
                    texture=new Texture2D(2,2,TextureFormat.RGBA32,false){hideFlags=HideFlags.DontSave};
                    if(!ImageConversion.LoadImage(texture,File.ReadAllBytes(path),true)){Destroy(texture);return;}
                    images[path]=texture;order.Enqueue(path);
                    while(order.Count>8){string old=order.Dequeue();if(images.TryGetValue(old,out var stale)){images.Remove(old);if(stale)Destroy(stale);}}
                }
                var image=GetComponent<RawImage>();image.texture=texture;image.color=Color.white;
            }
            catch(Exception error){Debug.LogWarning("[FriendsLife] Saved photograph: "+error.Message);}
        }
    }
}
