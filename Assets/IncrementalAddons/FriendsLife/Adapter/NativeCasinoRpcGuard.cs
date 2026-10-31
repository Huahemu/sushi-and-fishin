using System;
using System.Collections;
using System.Reflection;
namespace SushiAndFish.FriendsLife
{
    public static class NativeCasinoRpcGuard
    {
        public static void RequireNew(Type type,string method)
        {
            var field=typeof(Mirror.RemoteCalls.RemoteProcedureCalls).GetField("remoteCallDelegates",BindingFlags.Static|BindingFlags.NonPublic);
            var registry=field?.GetValue(null) as IDictionary;if(registry==null)throw new InvalidOperationException("Original Mirror RPC registry is not readable; no handler registered");
            ushort id=Mirror.Extensions.GetStableHashCode16(method);
            if(registry.Contains(id)){var value=registry[id];var existing=value.GetType().GetField("componentType").GetValue(value) as Type;if(existing!=type)throw new InvalidOperationException("NEW native RPC identifier collides with an existing original handler: "+method);}
        }
    }
}
