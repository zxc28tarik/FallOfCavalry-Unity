#nullable enable
using System;
using System.IO;
using System.Linq;
using FOC.Editor.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FOC.Tests.VisualPipeline
{
    /// <summary>Measured-reference and experiment-integrity gates, NOT art approval.</summary>
    public sealed class MeshyHumanoidCalibrationTests
    {
        private MeshyHumanoidCalibration.Report report=null!;
        [OneTimeSetUp] public void FreshReadOnlyAudit()=>report=MeshyHumanoidCalibration.Verify();

        [Test] public void TargetCandidateIsSeparateValidAvatarWithUnchangedHumanMapping()
        {
            var original=AssetDatabase.LoadAssetAtPath<GameObject>(MeshyHasanPilotPipeline.PrefabPath).GetComponent<Animator>().avatar;
            var candidate=MeshyHumanoidCalibration.LoadTargetAvatar();
            Assert.That(candidate,Is.Not.SameAs(original));Assert.That(candidate.isValid&&candidate.isHuman,Is.True);
            var originalMap=original.humanDescription.human.Select(h=>h.humanName+":"+h.boneName).OrderBy(s=>s).ToArray();
            var candidateMap=candidate.humanDescription.human.Select(h=>h.humanName+":"+h.boneName).OrderBy(s=>s).ToArray();
            Assert.That(candidateMap,Is.EqualTo(originalMap));Assert.That(report.inputAssetsUnchanged,Is.True);
        }

        [TestCase("CalibratedTarget")]
        [TestCase("CalibratedSource")]
        public void ReferenceUsesGravityVerticalLegsAndHorizontalStraightArms(string role)
        {
            var alignment=report.rigs.Single(r=>r.role==role).alignment;
            Assert.That(Vector3.Distance(alignment.gravityUp,Vector3.up),Is.LessThan(.00001f));
            Assert.That(Mathf.Abs(alignment.physicalForward.y),Is.LessThan(.00001f));
            Assert.That(Mathf.Abs(alignment.anatomicalLeft.y),Is.LessThan(.00001f));
            Assert.That(alignment.segmentDirectionErrors,Has.Length.EqualTo(8));
            Assert.That(alignment.maximumSegmentDirectionError,Is.LessThan(.1f));
            Assert.That(Vector3.Dot(Vector3.Cross(alignment.gravityUp,alignment.anatomicalLeft),alignment.physicalForward),Is.GreaterThan(.9999f));
            if(alignment.measuredToes)Assert.That(alignment.toeForwardErrors.All(e=>e<.1f),Is.True);
        }

        [Test] public void ForwardFootHeadingDoesNotFlattenTheNaturallySlopedAnkleToToeBone()
        {
            var alignment=report.rigs.Single(r=>r.role=="CalibratedTarget").alignment;
            Assert.That(alignment.originalAnkleToToePitchDegrees,Has.Length.EqualTo(2));
            Assert.That(alignment.originalAnkleToToePitchDegrees.All(p=>p<-30f&&p>-60f),Is.True,"Measured supplied ankle-to-toe geometry is sloped, not a horizontal sole axis.");
            Assert.That(alignment.ankleToToePitchDriftDegrees.All(p=>p<.1f),Is.True);
            Assert.That(alignment.toeForwardErrors.All(e=>e<.1f),Is.True,"Only projected toe heading is aligned to physical front.");
            Assert.That(alignment.measuredHeadMarkers,Is.True);Assert.That(alignment.orthogonalHeadUpError,Is.LessThan(.1f));Assert.That(alignment.headForwardError,Is.LessThan(.1f));
            Assert.That(Mathf.Abs(alignment.headUpError-alignment.headMarkersNonorthogonalityDegrees),Is.LessThan(.1f),"Raw head_end marker has intrinsic anatomical obliquity; do not hide or force it orthogonal.");
        }

        [Test] public void OriginalSourceReflectionIsIndependentlyMeasuredAndNotHidden()
        {
            var donor=report.rigs.Single(r=>r.role=="OriginalSource").physicalForward;
            var target=report.rigs.Single(r=>r.role=="OriginalTarget").physicalForward;
            Assert.That(donor.hasWeightedSoleEvidence,Is.True);Assert.That(donor.weightedSoleVertices,Is.GreaterThan(8));
            Assert.That(donor.sideConventionContradictsPhysicalForward,Is.True);Assert.That(donor.mappedForwardDotPhysical,Is.LessThan(-.8f));
            Assert.That(target.hasToeBones&&target.hasHeadFront,Is.True);Assert.That(target.mappedForwardDotPhysical,Is.GreaterThan(.8f));
        }

        [Test] public void CorrectedSourceMappingDoesNotPretendTheOldRightLabelIsAnatomicallyRight()
        {
            var source=MeshyHumanoidCalibration.LoadSourceAvatar();Assert.That(source.isValid&&source.isHuman,Is.True);
            var audit=report.rigs.Single(r=>r.role=="CalibratedSource");
            Assert.That(audit.mappingChanges,Has.Length.EqualTo(12));
            Assert.That(source.humanDescription.human.Single(h=>h.boneName=="Hand_R").humanName,Is.EqualTo("LeftHand"));
            Assert.That(source.humanDescription.human.Single(h=>h.boneName=="Hand_L").humanName,Is.EqualTo("RightHand"));
            Assert.That(audit.physicalForward.sideConventionContradictsPhysicalForward,Is.False);
            Assert.That(audit.attackOwnership,Does.Contain("LEFT-handed"));
        }

        [Test] public void ZeroMuscleSamplesUseExplicitCanonicalBodyWithoutClaimingTPose()
        {
            foreach(var rig in report.rigs)
            {
                Assert.That(rig.zeroMuscle.joints,Has.Length.EqualTo(MeshyHumanoidCalibration.JointOrder.Length));
                Assert.That(rig.zeroMuscle.joints.All(Finite),Is.True);
                Assert.That(rig.zeroMuscle.bodyPosition.x,Is.Zero);Assert.That(rig.zeroMuscle.bodyPosition.z,Is.Zero);
                Assert.That(rig.zeroMuscle.bodyPosition.y,Is.EqualTo(rig.originalNeutralBodyPosition.y));
                Assert.That(Quaternion.Angle(rig.zeroMuscle.bodyRotation,Quaternion.identity),Is.LessThan(.001f));
                Assert.That(rig.zeroBodyPolicy,Does.Contain("not assumed to be a T-pose"));
            }
        }

        [TestCase("A")]
        [TestCase("B")]
        [TestCase("C")]
        public void ScenarioSamplesExecuteActualHumanoidClipsAndFiniteLodZeroSkin(string scenario)
        {
            var result=report.scenarios.Single(s=>s.id==scenario);
            Assert.That(result.samples,Has.Length.EqualTo(MeshyHasanPilotMotionAdaptation.MotionNames.Length*MeshyHumanoidCalibration.Phases.Length));
            Assert.That(result.samples.All(s=>s.target.triangles==9586),Is.True);
            Assert.That(result.samples.All(s=>Finite(s.target.skinMinimum)&&Finite(s.target.skinMaximum)),Is.True);
            Assert.That(result.samples.All(s=>s.target.joints.All(Finite)&&s.source.joints.All(Finite)),Is.True);
            Assert.That(result.samples.All(s=>s.limbDirectionErrorDegrees.Length==8&&s.limbDirectionErrorDegrees.All(Finite)),Is.True);
            foreach(var clip in MeshyHumanoidCalibration.LoadClips(scenario))Assert.That(clip.humanMotion,Is.True);
            // Intentionally no relaxed art/contact tolerance: significant
            // measured failures stay in evidence and cannot become art PASS.
            Assert.That(report.acceptance,Does.Contain("MEASUREMENT ONLY"));
        }

        [Test] public void CandidateClipsRemainDistinctDiagnosticConversionsWithoutTransformOverrides()
        {
            var clips=MeshyHumanoidCalibration.LoadClips("C");Assert.That(clips,Has.Length.EqualTo(6));
            foreach(var clip in clips)
            {
                Assert.That(clip.name,Does.StartWith("ANM_HumanoidCandidate_"));
                MeshyHasanPilotMotionAdaptation.AuditClip(clip,clip.name);
                Assert.That(AnimationUtility.GetCurveBindings(clip).All(b=>b.type==typeof(Animator)&&b.path==""),Is.True);
            }
            Assert.That(clips.Select(MeshyHasanPilotMotionAdaptation.CurveSignature).Distinct().Count(),Is.EqualTo(6));
            var manifest=JsonUtility.FromJson<MeshyHumanoidCalibration.Manifest>(File.ReadAllText(MeshyHumanoidCalibration.ManifestPath));
            Assert.That(manifest.includesScenarioC,Is.True);Assert.That(manifest.limitations,Does.Contain("not production"));
        }

        [Test] public void NeutralReferenceImprovementCannotHideTheUnmetAttackQualityGate()
        {
            var q=report.qualityDiagnostics.Single(d=>d.scenario=="C");
            Assert.That(q.physicalSourceSidesConsistent&&q.physicalTargetSidesConsistent&&q.neutralHandSidesConsistent,Is.True);
            Assert.That(q.neutralArmDirectionMaximumDegrees,Is.LessThan(q.neutralDirectionReviewToleranceDegrees));Assert.That(q.neutralLegDirectionMaximumDegrees,Is.LessThan(q.neutralDirectionReviewToleranceDegrees));
            Assert.That(q.perFootNeutralDirectionDifferencesDegrees,Has.Length.EqualTo(2));
            Assert.That(q.neutralPhysicalFootDirectionMaximumDegrees,Is.EqualTo(q.perFootNeutralDirectionDifferencesDegrees.Max()));
            // A genuine remaining per-foot difference must stay FAIL. Do not
            // confuse integrity of the diagnostic with acceptance of its art.
            Assert.That(q.failedChecks.Contains("ZERO_MUSCLE_REFERENCE_ORIENTATION"),Is.EqualTo(q.neutralPhysicalFootDirectionMaximumDegrees>q.neutralDirectionReviewToleranceDegrees));
            Assert.That(q.intendedRightHandAttackDominates,Is.False);Assert.That(q.hasAuthoredAttackTorsoIntent,Is.False);
            Assert.That(q.failedChecks,Does.Contain("REQUIRED_RIGHT_HAND_ATTACK_OWNERSHIP"));Assert.That(q.failedChecks,Does.Contain("SOURCE_ATTACK_HAS_NO_AUTHORED_TORSO_INTENT"));
            Assert.That(q.status,Is.EqualTo("QUALITY_DIAGNOSTICS_FAIL"));Assert.That(q.palmRoll,Does.StartWith("UNVERIFIED"));
            Assert.That(q.actorRootStayedInPlace,Is.True);
        }

        [Test] public void SplayedFeetRequireTheirOwnPhysicalBindAxesNotAnAveragedActorFront()
        {
            foreach(var yaw in new[]{-11.06844f,12.95301f})
            {
                var ownBindHeading=Quaternion.AngleAxis(yaw,Vector3.up)*Vector3.forward;var sampledRotation=Quaternion.AngleAxis(-yaw,Vector3.up);
                var measured=MeshyHumanoidCalibration.TransformFootHeading(ownBindHeading,Quaternion.identity,sampledRotation);
                Assert.That(Vector3.Angle(measured,Vector3.forward),Is.LessThan(.02f));
                var wrong=MeshyHumanoidCalibration.TransformFootHeading(Vector3.forward,Quaternion.identity,sampledRotation);
                Assert.That(Vector3.Angle(wrong,Vector3.forward),Is.GreaterThan(10f),"The old averaged-axis measurement invents divergence from legitimate bind splay.");
            }
            foreach(var role in new[]{"CalibratedSource","CalibratedTarget"})
            {
                var feet=report.rigs.Single(r=>r.role==role).physicalForward.feet;
                Assert.That(feet.Select(f=>f.humanBone),Is.EquivalentTo(new[]{"LeftFoot","RightFoot"}));Assert.That(feet.All(f=>f.available),Is.True);
                Assert.That(feet.All(f=>Mathf.Abs(f.bindHorizontalHeading.y)<.00001f),Is.True);
                Assert.That(feet.All(f=>f.weightedSoleVertices>8),Is.True);
                if(role=="CalibratedSource")Assert.That(feet.All(f=>f.method.Contains("principal longitudinal axis")),Is.True);
                else Assert.That(feet.All(f=>f.method.Contains("Own ankle-to-toe")),Is.True);
            }
        }

        private static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        private static bool Finite(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
    }
}
