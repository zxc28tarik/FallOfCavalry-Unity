using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FOC.Editor.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FOC.Tests.VisualPipeline
{
    /// <summary>Rejected-conversion safety and retained source truth, never accepting the failed experiment.</summary>
    public sealed class MeshyLocomotionReferenceConversionTests
    {
        [TestCase("Walking")]
        [TestCase("Running")]
        public void SuppliedLocomotionKeepsItsOriginalHumanAvatarAndUnchangedSourceBytes(string motion)
        {
            var path = MeshyHasanPilotBuild.SourcePath;
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                Assert.That(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(),
                    Is.EqualTo("356dd92317ec1787f48aabe4e85157d31dfec2dd029168c856c5ffd6a125fadf"));
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c => c.name == motion);
            Assert.That(clip.isHumanMotion, Is.True);
            Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(path));
            var avatar = source.GetComponent<Animator>().avatar;
            Assert.That(avatar.isValid && avatar.isHuman, Is.True);
            Assert.That(AssetDatabase.GetAssetPath(avatar), Is.EqualTo(path));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeshyHasanPilotPipeline.PrefabPath);
            Assert.That(prefab.GetComponent<Animator>().avatar, Is.SameAs(avatar));
            Assert.That(avatar, Is.Not.SameAs(MeshyHumanoidCalibration.LoadTargetAvatar()));
        }

        [Test]
        public void FailedMeasuredConversionCannotBecomeSelectableThroughClaimedPassFlags()
        {
            // Representative measured failures, not a relaxed replacement gate.
            foreach (var failure in new[] { "skin", "joint", "rotation" })
            {
                var report = CompleteZeroErrorFixture();
                var bad = report.motions[0]; var sample = bad.samples[1];
                if (failure == "skin") sample.maximumSkinErrorMeters = bad.maximumSkinErrorMeters = .09f;
                else if (failure == "joint") sample.maximumJointErrorMeters = bad.maximumJointErrorMeters = .017f;
                else sample.maximumJointRotationErrorDegrees = bad.maximumJointRotationErrorDegrees = 65f;
                bad.withinTolerance = true; report.status = "NUMERICAL_REFERENCE_CONVERSION_PASS_VISUAL_ACCEPTANCE_PENDING";
                var error = Assert.Throws<InvalidOperationException>(() => MeshyLocomotionReferenceConversion.ValidateAcceptance(report));
                Assert.That(error.Message, Does.Contain("strict 5 mm"), failure);
            }
        }

        [Test]
        public void MissingHeldOutEvidenceNonfiniteValuesOrFalseSummariesCannotPassSelectionGate()
        {
            var missing = CompleteZeroErrorFixture(); missing.motions[0].samples = Array.Empty<MeshyLocomotionReferenceConversion.Sample>();
            Assert.Throws<InvalidOperationException>(() => MeshyLocomotionReferenceConversion.ValidateAcceptance(missing));
            var nonfinite = CompleteZeroErrorFixture(); nonfinite.motions[0].samples[1].maximumSkinErrorMeters = float.NaN;
            Assert.Throws<InvalidOperationException>(() => MeshyLocomotionReferenceConversion.ValidateAcceptance(nonfinite));
            var stale = CompleteZeroErrorFixture(); stale.motions[0].samples[1].maximumSkinErrorMeters = .004f;
            var error = Assert.Throws<InvalidOperationException>(() => MeshyLocomotionReferenceConversion.ValidateAcceptance(stale));
            Assert.That(error.Message, Does.Contain("summary does not match"));
            var changed = CompleteZeroErrorFixture(); changed.inputAssetsUnchanged = false;
            Assert.Throws<InvalidOperationException>(() => MeshyLocomotionReferenceConversion.ValidateAcceptance(changed));
            // Synthetic validator control, explicitly not the rejected real clips.
            Assert.DoesNotThrow(() => MeshyLocomotionReferenceConversion.ValidateAcceptance(CompleteZeroErrorFixture()));
        }

        [Test]
        public void RejectedReferenceClipsAreNotDependenciesOfOriginalPrefabOrProductionCatalog()
        {
            foreach (var path in new[] { MeshyHasanPilotPipeline.PrefabPath,
                "Assets/FOC/Content/Resources/FOC/Visuals/FOC_VisualCatalog.asset" })
            {
                Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Not.Null);
                Assert.That(AssetDatabase.GetDependencies(path, true).Any(p => p.StartsWith(MeshyLocomotionReferenceConversion.OutputRoot + "/", StringComparison.Ordinal)),
                    Is.False, "Failed experimental clips must remain outside runtime/production selection: " + path);
            }
        }

        [Test]
        public void ReferenceConversionStillRejectsForeignRigsWithoutChangingCanonicalOrTargetMapping()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeshyHasanPilotPipeline.PrefabPath);
            var donor = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FOC/ArtSource/HistoricalSlice/MotionLibrary/UAL1_Standard.fbx");
            Assert.That(donor, Is.Not.Null);
            var avatar = donor.GetComponent<Animator>().avatar;
            Assert.That(avatar.isValid && avatar.isHuman, Is.True);
            var error = Assert.Throws<InvalidOperationException>(() => MeshyLocomotionReferenceConversion.ValidateSameRigAvatar(prefab, avatar));
            Assert.That(error.Message, Does.Contain("same physical rig"));
            Assert.DoesNotThrow(() => MeshyLocomotionReferenceConversion.ValidateSameRigAvatar(prefab, MeshyHumanoidCalibration.LoadTargetAvatar()));
        }

        private static MeshyLocomotionReferenceConversion.Report CompleteZeroErrorFixture()
        {
            return new MeshyLocomotionReferenceConversion.Report
            {
                inputAssetsUnchanged = true,
                motions = MeshyLocomotionReferenceConversion.Motions.Select(name => new MeshyLocomotionReferenceConversion.MotionAudit
                {
                    motion = name, withinTolerance = true, authoredFrames = 2, measuredFrames = 3, heldOutFrames = 1,
                    samples = new[] { new MeshyLocomotionReferenceConversion.Sample { phase = 0 },
                        new MeshyLocomotionReferenceConversion.Sample { phase = .5f, heldOut = true },
                        new MeshyLocomotionReferenceConversion.Sample { phase = 1 } }
                }).ToArray()
            };
        }
    }
}
