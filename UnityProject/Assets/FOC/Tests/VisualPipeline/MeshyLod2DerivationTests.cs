using System;
using System.IO;
using FOC.Editor.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FOC.Tests.VisualPipeline
{
    public sealed class MeshyLod2DerivationTests
    {
        [Test] public void ReviewedDerivationPreservesTheOriginalCalibrationManifestAndOnlyTwoLod2Outputs()
        {
            var ledger=MeshyLod2Derivation.Verify();
            Assert.That(ledger.changes.Length,Is.EqualTo(2));Assert.That(ledger.triangles,Is.EqualTo(5606));
            Assert.That(MeshyLod2Derivation.Hash(MeshyHumanoidCalibration.ManifestPath),Is.EqualTo(MeshyLod2Derivation.BaselineHash));
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshyLod2Derivation.MeshPath);
            Assert.That(mesh.triangles.Length/3,Is.EqualTo(ledger.triangles));
            Assert.That(MeshyLod2Derivation.AllowsInput(MeshyLod2Derivation.MeshPath,MeshyLod2Derivation.OriginalMeshHash),Is.True);
            Assert.That(MeshyLod2Derivation.AllowsInput(MeshyHasanPilotPipeline.ModelPath,MeshyHasanPilotLodImport.RuntimeHash),Is.False);
        }
        [TestCase("extra")]
        [TestCase("wrong_original")]
        [TestCase("stale_output")]
        [TestCase("unreviewed")]
        [TestCase("changed_protected")]
        public void CorruptOrExpandedOverlayIsRejected(string corruption)
        {
            var ledger=JsonUtility.FromJson<MeshyLod2Derivation.Ledger>(File.ReadAllText(MeshyLod2Derivation.LedgerPath));
            if(corruption=="extra")ledger.changes[0].path=MeshyHasanPilotPipeline.ModelPath;
            if(corruption=="wrong_original")ledger.changes[0].beforeSha256=new string('0',64);
            if(corruption=="stale_output")ledger.changes[0].afterSha256=new string('0',64);
            if(corruption=="unreviewed")ledger.status="CANDIDATE_ONLY";
            if(corruption=="changed_protected")ledger.preserved[0].sha256=new string('0',64);
            Assert.Throws<InvalidOperationException>(()=>MeshyLod2Derivation.Validate(ledger,MeshyLod2Derivation.Hash));
        }
    }
}
