#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Presentation.Visuals;
using UnityEditor;
using UnityEngine;

namespace FOC.Editor.Visuals
{
    /// <summary>Fail closed: renamed fixture prefabs still fail dependency inspection.</summary>
    public static class ProductionVisualCatalogGate
    {
        public static IReadOnlyList<string> Audit(VisualCatalogAsset catalog)
        {
            if(catalog==null)return new[]{"Missing production catalog."};
            var errors=new List<string>();
            foreach(var id in new[]{"hasan-aga","sipahi","cebeli","tufekci"})if(!catalog.Profiles.Any(p=>p.visualProfileId==id))errors.Add("Missing production profile: "+id);
            foreach(var id in new[]{"kilic","mizrak","fitilli-tufek","zirh-gomlek","kalkan","sipahi-ati","tufek-muhimmat"})if(!catalog.Equipment.Any(e=>e.definitionId==id))errors.Add("Missing production equipment: "+id);
            foreach(var entry in catalog.Assets)
            {
                if(IsProof(entry.id))errors.Add("Proof ID: "+entry.id);
                if(entry.prefab==null){errors.Add("Missing prefab: "+entry.id);continue;}
                var path=AssetDatabase.GetAssetPath(entry.prefab);
                foreach(var dependency in AssetDatabase.GetDependencies(path,true))if(IsProof(dependency))errors.Add("Proof dependency: "+entry.id+" -> "+dependency);
                foreach(var renderer in entry.prefab.GetComponentsInChildren<Renderer>(true))
                {
                    var mesh=renderer is SkinnedMeshRenderer skin?skin.sharedMesh:renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if(mesh==null){errors.Add("Missing mesh: "+entry.id);continue;}
                    var meshPath=AssetDatabase.GetAssetPath(mesh);
                    if(IsProof(mesh.name)||IsProof(meshPath)||meshPath=="Library/unity default resources"||meshPath=="Resources/unity_builtin_extra")errors.Add("Primitive/fixture mesh: "+entry.id+" -> "+mesh.name);
                }
            }
            if(IsProof(catalog.ConsolidatedCharacterAssetId.Value)||IsProof(catalog.CrowdAssetId.Value))errors.Add("Proof runtime fallback.");
            var validation=catalog.BuildCoreCatalog().Validate();errors.AddRange(validation.Errors);
            return errors.Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        }
        public static bool IsProof(string value)=>value.IndexOf("_Proof",StringComparison.OrdinalIgnoreCase)>=0||value.IndexOf("/VisualProof/",StringComparison.OrdinalIgnoreCase)>=0;
        public static void Run()
        {
            var errors=Audit(AssetDatabase.LoadAssetAtPath<VisualCatalogAsset>(VisualProofAssetGenerator.RuntimeCatalogPath));
            foreach(var error in errors)Debug.LogError("FOC_PRODUCTION_ART_GATE "+error);
            Debug.Log("FOC_PRODUCTION_ART_GATE_"+(errors.Count==0?"TECHNICAL_PASS":"NOT_READY")+" errors="+errors.Count+" (visual QA remains independent)");
            EditorApplication.Exit(errors.Count==0?0:1);
        }
    }
}
