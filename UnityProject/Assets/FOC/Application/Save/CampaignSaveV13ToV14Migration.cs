#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace FOC.Application.Save
{
    /// <summary>Atomically remaps temporary 14A/proof identities without changing geography route or journey progress.</summary>
    public sealed class CampaignSaveV13ToV14Migration : ISaveMigration
    {
        private static readonly IReadOnlyDictionary<string,string> Remaps=new Dictionary<string,string>(StringComparer.Ordinal)
        {
            ["city-home"]="city-istanbul",["city-other"]="city-bursa",["slice-player-sipahi"]="hasan-aga",
            ["commander-a"]="hasan-aga",["commander-b"]="ali-cavus",["army-a"]="army-hasan-retinue",["army-b"]="army-bursa-garrison",
            ["org-a"]="org-hasan-retinue",["org-b"]="org-bursa-garrison",["unit-a"]="unit-hasan-sipahi",["unit-b"]="unit-bursa-garrison",
            ["source-a"]="recruitment-hasan-retinue",["source-b"]="recruitment-bursa-garrison",["record-a"]="record-hasan-sipahi",["record-b"]="record-bursa-garrison",
            ["caravan-proof"]="caravan-bursa-istanbul",["house-proof"]="house-hasan-aga",["clique-proof"]="clique-marmara-merchants",
            ["faith-proof"]="religion-islam",["sect-proof"]="sect-hanafi",["faction-proof"]="faction-ottoman-state",["faction-foreign"]="faction-republic-of-venice",
            ["battle-main"]="battle-legacy-bursa-skirmish",["report-proof"]="report-legacy-military",["encounter-proof"]="encounter-legacy-road",["contract-proof"]="contract-legacy-management",
            ["Proof Home"]="İstanbul",["Proof Other"]="Bursa",["Army A"]="Hasan Ağa Kapı Halkı",["Army B"]="Bursa Garnizonu"
        };
        public int FromVersion=>13;public int ToVersion=>14;
        public CampaignSaveData Apply(CampaignSaveData source)
        {
            if(source==null)throw new ArgumentNullException(nameof(source));if(source.SaveVersion!=FromVersion)throw new InvalidOperationException("Migration requires schema v13.");
            Rewrite(source,new HashSet<object>(ReferenceComparer.Instance));source.ContentDataVersion="VERTICAL_SLICE_HISTORICAL-1648-09-01-r1";source.SaveVersion=ToVersion;return source;
        }
        private static void Rewrite(object value,HashSet<object> visited)
        {
            if(value==null||value is string||value.GetType().IsValueType||!visited.Add(value))return;
            if(value is IList list){for(var index=0;index<list.Count;index++){var item=list[index];if(item is string text&&Remaps.TryGetValue(text,out var replacement))list[index]=replacement;else if(item!=null)Rewrite(item,visited);}return;}
            foreach(var property in value.GetType().GetProperties(BindingFlags.Instance|BindingFlags.Public))
            {
                if(!property.CanRead||property.GetIndexParameters().Length!=0)continue;var current=property.GetValue(value,null);if(current is string text&&property.CanWrite&&Remaps.TryGetValue(text,out var replacement))property.SetValue(value,replacement,null);else if(current!=null)Rewrite(current,visited);
            }
        }
        private sealed class ReferenceComparer:IEqualityComparer<object>{public static ReferenceComparer Instance{get;}=new ReferenceComparer();public new bool Equals(object? x,object? y)=>ReferenceEquals(x,y);public int GetHashCode(object obj)=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);}
    }
}
