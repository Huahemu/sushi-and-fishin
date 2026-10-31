using System;
using System.Collections.Generic;
using System.Linq;

namespace SushiAndFish.FriendsLife
{
    [Serializable] public sealed class HomeVacatedGeneration
    {
        public string plotId,instanceId,operationId;
    }
    [Serializable] public sealed class HomeSplitChoice
    {
        public string personId,plotId,typeId;
        public float yaw;
    }
    // Pure rules shared by the authority adapter and the local regression runner.
    // A template never owns/reserves a plot. Only a LIVE generation can own it.
    public static class HousingAtomicBatch
    {
        // Attempts include a failing native call because it may have changed a
        // participant before returning false/throwing. A stale precondition is
        // checked before adding that participant to the inverse batch.
        public static bool TryApply<T>(IReadOnlyList<T> participants,Func<T,bool> ready,Func<T,bool> apply,
            Func<T,bool> inverse,List<T> attempts,out string failure)
        {
            failure=null;if(attempts.Count!=0){failure="住户还没离开，请稍后再试";return false;}
            try
            {
                foreach(var participant in participants)
                {
                    if(!ready(participant))throw new InvalidOperationException("住户位置或移动接口已变化");
                    attempts.Add(participant);if(!apply(participant))throw new InvalidOperationException("原公开移动接口未完成退出");
                }
                return true;
            }
            catch(Exception error)
            {
                UnityEngine.Debug.LogWarning("[FriendsLife] Housing exit failed: "+error);failure="住户还没离开，请稍后再试";
                if(!Rollback(attempts,inverse,out var rollback))UnityEngine.Debug.LogWarning("[FriendsLife] Housing exit rollback: "+rollback);
                return false;
            }
        }
        public static bool Rollback<T>(List<T> attempts,Func<T,bool> inverse,out string failure)
        {
            failure=null;var failed=new List<T>();
            for(int index=attempts.Count-1;index>=0;index--)
            {
                bool restored;try{restored=inverse(attempts[index]);}catch{restored=false;}
                if(!restored)failed.Add(attempts[index]);
            }
            attempts.Clear();failed.Reverse();attempts.AddRange(failed);
            if(attempts.Count==0)return true;failure="人物退出回滚仍有 "+attempts.Count+" 项等待恢复";return false;
        }
    }
    public static class HousingVacancyRules
    {
        public static string Generation(string plotId,string instanceId)
            =>string.IsNullOrEmpty(instanceId)?"legacy-addon/"+plotId:instanceId;
        public static bool IsRetired(IEnumerable<HomeVacatedGeneration> retired,string plotId,string instanceId)
        {
            string generation=Generation(plotId,instanceId);
            return retired!=null&&retired.Any(r=>r!=null&&r.plotId==plotId&&r.instanceId==generation);
        }
        public static bool ValidateSplit(IEnumerable<string> requiredOwners,IReadOnlyList<HomeSplitChoice> choices,
            int groupLevel,Func<string,bool> plotIsActuallyEmpty,Func<string,int> typeLevel,out string failure)
        {
            failure=null;var required=(requiredOwners??Enumerable.Empty<string>()).ToArray();
            if(required.Length==0||required.Any(string.IsNullOrEmpty)||required.Distinct(StringComparer.Ordinal).Count()!=required.Length)
            {failure="分住成员资料不完整";return false;}
            if(choices==null||choices.Count!=required.Length||choices.Any(c=>c==null)||
               choices.Select(c=>c.personId).Distinct(StringComparer.Ordinal).Count()!=choices.Count||
               choices.Any(c=>!required.Contains(c.personId,StringComparer.Ordinal)))
            {failure="请为每位分住成员各选择一处空地";return false;}
            if(choices.Any(c=>string.IsNullOrEmpty(c.plotId))||choices.Select(c=>c.plotId).Distinct(StringComparer.Ordinal).Count()!=choices.Count)
            {failure="分住地块不能重复";return false;}
            foreach(var choice in choices)
            {
                if(!plotIsActuallyEmpty(choice.plotId)){failure="所选地块已被使用，请重新选择";return false;}
                if(typeLevel(choice.typeId)!=groupLevel){failure="分住住宅须与合住住宅等级相同";return false;}
                if(float.IsNaN(choice.yaw)||float.IsInfinity(choice.yaw)||Math.Abs(choice.yaw/90f-(float)Math.Round(choice.yaw/90f))>.0001f)
                {failure="仅支持四个朝向";return false;}
            }
            return true;
        }
        public static bool PlanDependents(IReadOnlyList<string> members,IReadOnlyDictionary<string,string> ownerHomes,
            Func<string,IEnumerable<string>> parents,out Dictionary<string,string> destinations,out string failure)
        {
            destinations=null;failure=null;
            if(members==null||ownerHomes==null||members.Count==0||members.Distinct(StringComparer.Ordinal).Count()!=members.Count||
               ownerHomes.Count==0||ownerHomes.Any(p=>!members.Contains(p.Key)||string.IsNullOrEmpty(p.Value)))
            {failure="分住成员与住宅资料不一致";return false;}
            var planned=ownerHomes.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);var visiting=new HashSet<string>(StringComparer.Ordinal);
            bool Resolve(string person)
            {
                if(planned.ContainsKey(person))return true;if(!visiting.Add(person))return false;
                foreach(string parent in parents(person)??Enumerable.Empty<string>())
                    if(!string.IsNullOrEmpty(parent)&&members.Contains(parent)&&Resolve(parent)){planned[person]=planned[parent];visiting.Remove(person);return true;}
                visiting.Remove(person);return false;
            }
            foreach(string member in members)if(!Resolve(member)){failure="分住时缺少孩子的同行家人";return false;}
            destinations=planned;return true;
        }
    }
}
