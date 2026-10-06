#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace FOC.Editor.Visuals
{
    /// <summary>A narrow reviewed LOD2 overlay. Original calibration provenance is never refreshed.</summary>
    public static class MeshyLod2Derivation
    {
        public const string LedgerPath=MeshyHumanoidCalibration.OutputRoot+"/LOD2Derivation.json";
        public const string JsonPath=MeshyHasanPilotPipeline.SourceRoot+"/Hasan_Meshy_LOD2.json";
        public const string MeshPath=MeshyHasanPilotPipeline.OutputRoot+"/MESH_HasanAga_MeshyPilot_LOD2.asset";
        public const string BaselineHash="7804ef74e08c0631ee5d9e39741ad0689aa97f8598d403cec266a2763f8b24cf";
        public const string OriginalJsonHash="1d4c0f5e973522564bcd1c977ea5bb39c0cdcffd7eab798617eaff0786b92fdb";
        public const string OriginalMeshHash="5d643a48033b569a18ee973ebe528178d916fa0a13b7a00df79d22b3b2987418";
        [Serializable] public sealed class Change { public string path="",beforeSha256="",afterSha256=""; }
        [Serializable] public sealed class Preserved { public string path="",sha256=""; }
        [Serializable] public sealed class Ledger
        {
            public int version=1;
            public string status="",baselineManifestSha256="",method="",visualEvidence="";
            public Change[] changes=Array.Empty<Change>();
            public Preserved[] preserved=Array.Empty<Preserved>();
            public int triangles;
            public bool productionActivated;
        }
        public static bool Exists=>File.Exists(LedgerPath);
        public static string Hash(string path)
        {
            using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
        public static void Validate(Ledger ledger,Func<string,string> hash)
        {
            Require(ledger.version==1&&ledger.status=="REVIEWED_LOD2_DERIVATION"&&!ledger.productionActivated,"Unreviewed/unknown LOD2 derivation.");
            Require(ledger.baselineManifestSha256==BaselineHash&&hash(MeshyHumanoidCalibration.ManifestPath)==BaselineHash,"Original calibration manifest changed.");
            Require(ledger.triangles>0&&ledger.triangles<5752&&!string.IsNullOrWhiteSpace(ledger.visualEvidence),"LOD2 reduction/review evidence missing.");
            Require(ledger.changes.Length==2&&ledger.changes.Select(c=>c.path).Distinct().Count()==2,"Exactly two distinct LOD2 changes required.");
            foreach(var change in ledger.changes)
            {
                Require(change.path==JsonPath||change.path==MeshPath,"LOD2 overlay cannot exempt another asset: "+change.path);
                Require(change.beforeSha256==(change.path==JsonPath?OriginalJsonHash:OriginalMeshHash),"LOD2 original hash mismatch.");
                Require(change.afterSha256.Length==64&&hash(change.path)==change.afterSha256,"LOD2 approved output changed: "+change.path);
            }
            Require(ledger.preserved.Length>=125&&ledger.preserved.Select(p=>p.path).Distinct().Count()==ledger.preserved.Length,"Incomplete/duplicate immutable snapshot.");
            foreach(var input in ledger.preserved)
            {
                Require(input.path.StartsWith("Assets/",StringComparison.Ordinal)&&!input.path.Contains("..")&&input.path!=JsonPath&&input.path!=MeshPath,"Invalid protected path.");
                Require(hash(input.path)==input.sha256,"Protected derivation input changed: "+input.path);
            }
        }
        public static Ledger Verify()
        {
            var ledger=JsonUtility.FromJson<Ledger>(File.ReadAllText(LedgerPath));Validate(ledger,Hash);
            var baseline=JsonUtility.FromJson<MeshyHumanoidCalibration.Manifest>(File.ReadAllText(MeshyHumanoidCalibration.ManifestPath));
            Require(baseline.inputs.Length==124,"Unexpected original calibration baseline.");
            foreach(var input in baseline.inputs.Concat(baseline.outputs))
            {
                if(input.path==MeshPath){Require(input.sha256==OriginalMeshHash,"Original baseline mesh hash mismatch.");continue;}
                Require(Hash(input.path)==input.sha256,"Original calibration input/output changed: "+input.path);
                Require(ledger.preserved.Any(p=>p.path==input.path&&p.sha256==input.sha256),"Overlay omitted original protected asset: "+input.path);
            }
            var json=JsonUtility.FromJson<MeshyHasanPilotLodImport.LodData>(File.ReadAllText(JsonPath));
            Require(json.lod==2&&json.triangles.Length/3==ledger.triangles&&json.runtimeSourceSha256==MeshyHasanPilotLodImport.RuntimeHash,"Derived JSON/ledger mismatch.");
            return ledger;
        }
        public static bool AllowsInput(string path,string originalHash)
        {
            if(path!=MeshPath||originalHash!=OriginalMeshHash||!Exists)return false;
            Verify();return true;
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
