using System;
using System.IO;
using System.Linq;
using FOC.Editor.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FOC.Tests.VisualPipeline
{
    /// <summary>Format/provenance/deformation checks, not animation quality approval.</summary>
    public sealed class MeshyHasanPilotMotionAdaptationTests
    {
        [Test]
        public void PelvisOnlySharedProofCannotMasqueradeAsArticulatedHumanoidSource()
        {
            var proof=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/FOC/Generated/VisualProof/Animations/ANM_OneHandedAttack.anim");
            Assert.That(proof,Is.Not.Null);Assert.That(proof.humanMotion,Is.False);
            Assert.Throws<InvalidOperationException>(()=>MeshyHasanPilotMotionAdaptation.ValidateGenericDiagnostic(proof));
        }

        [TestCase("Idle")]
        [TestCase("Turn")]
        [TestCase("OneHandedAttack")]
        [TestCase("ArmRaise")]
        [TestCase("Crouch")]
        [TestCase("MountedSeated")]
        public void ConvertedActionIsRealMuscleMotionNotRenamedForeignTransformCurves(string motion)
        {
            var source=AssetDatabase.LoadAssetAtPath<AnimationClip>(MeshyHasanPilotMotionAdaptation.SourceClipPath(motion));
            Assert.That(source,Is.Not.Null);Assert.That(source.humanMotion,Is.False,"Provenance must not claim source already was Humanoid.");
            MeshyHasanPilotMotionAdaptation.ValidateGenericDiagnostic(source);
            var converted=AssetDatabase.LoadAssetAtPath<AnimationClip>(MeshyHasanPilotMotionAdaptation.OutputClipPath(motion));
            Assert.That(converted,Is.Not.Null,"Run the explicit adaptation step; a missing asset is not a skipped test.");
            var audit=MeshyHasanPilotMotionAdaptation.AuditClip(converted,motion);
            Assert.That(audit.humanMotion,Is.True);Assert.That(audit.muscleCurves,Is.EqualTo(HumanTrait.MuscleCount));Assert.That(audit.transformCurves,Is.Zero);
            Assert.That(converted.length,Is.EqualTo(source.length).Within(.001f));
            Assert.That(AnimationUtility.GetCurveBindings(converted).Any(b=>b.propertyName.StartsWith("blendShape.",StringComparison.Ordinal)),Is.False,"Donor hand/garment shapes cannot be grafted onto Meshy.");
        }

        [Test]
        public void ConvertedActionsHaveDistinctMotionDataAndTruthfulDiagnosticProvenance()
        {
            var clips=MeshyHasanPilotMotionAdaptation.LoadClips();
            Assert.That(clips.Select(MeshyHasanPilotMotionAdaptation.CurveSignature).Distinct().Count(),Is.EqualTo(6));
            var provenance=JsonUtility.FromJson<MeshyHasanPilotMotionAdaptation.Provenance>(File.ReadAllText(MeshyHasanPilotMotionAdaptation.ProvenancePath));
            Assert.That(provenance.motions.Length,Is.EqualTo(6));
            Assert.That(provenance.motions.All(m=>!m.sourceWasHumanoid&&m.status=="EXISTING_FOC_DIAGNOSTIC_CONVERTED_NOT_PRODUCTION_ACCEPTED"),Is.True);
            Assert.That(provenance.inputs.Length,Is.EqualTo(8));
            Assert.That(provenance.origin,Does.Contain("not Meshy-supplied motion"));
        }

        [Test]
        public void VerifyActuallySamplesConvertedActionsThroughForeignTargetAvatarAndSkin()
        {
            var report=MeshyHasanPilotMotionAdaptation.Verify();
            Assert.That(report.sourceAvatarHuman&&report.sourceAvatarValid&&report.targetAvatarHuman&&report.targetAvatarValid,Is.True);
            Assert.That(report.motions.Length,Is.EqualTo(6));
            foreach(var motion in report.motions)
            {
                Assert.That(motion.targetSamples.Length,Is.EqualTo(8),motion.motion);
                Assert.That(motion.maximumJointTravel,Is.GreaterThan(.0005f),motion.motion);
                Assert.That(motion.targetSamples.All(s=>s.trackedJointPositions.Length==6),Is.True,motion.motion);
            }
            Assert.That(report.visualAcceptance,Does.StartWith("NOT EVALUATED"));Assert.That(report.mountAcceptance,Does.StartWith("NOT EVALUATED"));
        }
    }
}
