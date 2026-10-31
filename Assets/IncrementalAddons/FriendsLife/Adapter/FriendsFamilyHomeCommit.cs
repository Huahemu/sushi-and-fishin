using System;

namespace SushiAndFish.FriendsLife
{
    public sealed partial class FriendsLifeRuntime
    {
        // The source plan and economy projection finish synchronously together.
        // Existing SaveNow and kernel ingress already honor committing.
        internal IDisposable DeferFamilyHomeCommit()
        {
            if(!Ready||!IsAuthority||committing)return null;
            committing=true;return new FamilyHomeCommitScope(this);
        }
        sealed class FamilyHomeCommitScope:IDisposable
        {
            FriendsLifeRuntime owner;
            public FamilyHomeCommitScope(FriendsLifeRuntime value){owner=value;}
            public void Dispose(){if(!owner)return;var runtime=owner;owner=null;runtime.committing=false;runtime.SaveNow();}
        }
    }
}
