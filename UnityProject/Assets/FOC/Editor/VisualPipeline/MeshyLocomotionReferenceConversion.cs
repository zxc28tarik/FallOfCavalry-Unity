#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace FOC.Editor.Visuals
{
    /// <summary>
    /// Converts the supplied locomotion's actual physical pose into a different
    /// reference Avatar on the SAME rig. No new choreography, grounding or rig.
    /// </summary>
    public static class MeshyLocomotionReferenceConversion
    {
        public const string OutputRoot = MeshyHasanPilotPipeline.OutputRoot + "/MotionClosure/ReferenceLocomotion";
        public const string ManifestPath = OutputRoot + "/ReferenceLocomotionProvenance.json";
        public const int SamplesPerSecond = 60;
        public const float PositionToleranceMeters = .005f;
        public const float RotationToleranceDegrees = 1f;
        public static readonly string[] Motions = { "Walking", "Running" };
        public static string ReportPath => Path.GetFullPath("../TestResults/MeshyMotionClosure/reference-locomotion.json");
        public static string ClipPath(string motion)
        {
            Require(Motions.Contains(motion), "Only unchanged supplied Walking/Running may use this conversion.");
            return OutputRoot + "/ANM_MeshyReference_" + motion + ".anim";
        }

        [Serializable] public sealed class AssetHash { public string path = "", sha256 = ""; }
        [Serializable] public sealed class Sample
        {
            public float phase, maximumJointErrorMeters, maximumJointRotationErrorDegrees, maximumSkinErrorMeters;
            public float originalMinimumSkinY, candidateMinimumSkinY, actorRootPositionErrorMeters, actorRootRotationErrorDegrees;
            public string worstJoint = "", worstRotationJoint = ""; public int worstSkinVertex; public bool heldOut;
            public BoneError[] bones = Array.Empty<BoneError>(); public WeightInfluence[] worstVertexWeights = Array.Empty<WeightInfluence>();
        }
        [Serializable] public sealed class BoneError { public string name = ""; public float positionMeters, worldRotationDegrees, localRotationDegrees; }
        [Serializable] public sealed class WeightInfluence { public string bone = ""; public float weight; }
        [Serializable] public sealed class DiagnosisSample
        {
            public string motion = "", extractionReference = ""; public float phase;
            public string[] outsideNominalMuscles = Array.Empty<string>(); public Sample directRoundtrip = new Sample();
        }
        [Serializable] public sealed class Diagnosis
        {
            public string status = "NOT_COMPLETED", utc = "", unityVersion = "", error = "";
            public string method = "Actual original-Avatar source clip playback; same physical hierarchy copied onto either original-Avatar or calibrated-Avatar extraction clone; immediate GetHumanPose/SetHumanPose roundtrip. No baked curves, interpolation, correction or source edits.";
            public bool inputAssetsUnchanged; public HumanDescription originalDescription, calibratedDescription;
            public DiagnosisSample[] samples = Array.Empty<DiagnosisSample>();
        }
        [Serializable] public sealed class MotionAudit
        {
            public string motion = "", sourceClip = "", candidateClip = "", originalAvatar = "", candidateAvatar = "";
            public float duration, maximumAbsoluteMuscle, maximumExtractionRoundtripJointMeters, maximumExtractionRoundtripSkinMeters;
            public float maximumJointErrorMeters, maximumJointRotationErrorDegrees, maximumSkinErrorMeters;
            public int authoredFrames, measuredFrames, heldOutFrames; public bool withinTolerance;
            public Sample[] samples = Array.Empty<Sample>();
        }
        [Serializable] public sealed class Manifest
        {
            public int schemaVersion = 1, samplesPerSecond = SamplesPerSecond;
            public string method = "Original Avatar + original Humanoid clip actual Mecanim playback; copy physical local TRS onto a disabled same-hierarchy calibrated-Avatar clone; HumanPoseHandler.GetHumanPose; new calibrated-reference muscle/RootT/RootQ curves. Original model/Avatar/clips remain unchanged.";
            public string bodySpace = "Identity unit actor root. HumanPose.bodyPosition is already world COM divided by extraction Avatar humanScale: write directly, with no second normalization. Quaternion hemisphere continuity only; no axis, sign, muscle, height or contact correction.";
            public string acceptance = "A numerical pose-preservation experiment only. No foot contact cleanup, mounted acceptance, production activation or art PASS.";
            public AssetHash[] inputs = Array.Empty<AssetHash>(), outputs = Array.Empty<AssetHash>();
            public MotionAudit[] motions = Array.Empty<MotionAudit>();
        }
        [Serializable] public sealed class Report
        {
            public string status = "NOT_COMPLETED", operation = "", utc = "", unityVersion = "", error = "";
            public string acceptance = "NUMERICAL_REFERENCE_CONVERSION_ONLY; original source contact/art limitations remain, Windows visual QA required.";
            public float positionToleranceMeters = PositionToleranceMeters, rotationToleranceDegrees = RotationToleranceDegrees;
            public bool inputAssetsUnchanged; public MotionAudit[] motions = Array.Empty<MotionAudit>();
        }

        public static void Run() => Batch(Generate);
        public static void RunVerify() => Batch(Verify);
        public static void RunDiagnose()
        {
            var code = 0;
            try { Diagnose(); } catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        /// <summary>Read-only controlled Get/Set investigation; no candidate assets are written.</summary>
        public static Diagnosis Diagnose()
        {
            var result = new Diagnosis { utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion };
            var samples = new List<DiagnosisSample>();
            try
            {
                var inputs = RecordInputs(); var prefab = Load<GameObject>(MeshyHasanPilotPipeline.PrefabPath);
                var original = prefab.GetComponent<Animator>().avatar; var calibrated = MeshyHumanoidCalibration.LoadTargetAvatar();
                ValidateSameRigAvatar(prefab, calibrated);
                result.originalDescription = original.humanDescription; result.calibratedDescription = calibrated.humanDescription;
                foreach (var motion in Motions)
                using (var source = new Playback(prefab, original, SourceClip(motion)))
                foreach (var avatar in new[] { original, calibrated })
                {
                    var extraction = CreateClone(prefab, avatar, false); var mesh = new Mesh();
                    try
                    {
                        var copy = new PhysicalPoseCopy(source.Actor, extraction); var skin = LodZero(extraction);
                        using (var handler = new HumanPoseHandler(avatar, extraction.transform))
                        foreach (var phase in Enumerable.Range(0, 8).Select(i => i / 8f).Concat(new[] { .14814815f, .7037037f, .5813953f, .60465115f }))
                        {
                            source.Sample(SourceClip(motion).length * phase); copy.Copy();
                            var pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
                            handler.GetHumanPose(ref pose); ValidatePose(pose);
                            var outside = pose.muscles.Select((v, i) => new { value = v, index = i }).Where(v => Mathf.Abs(v.value) > 1f)
                                .Select(v => HumanTrait.MuscleName[v.index] + "=" + v.value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                            handler.SetHumanPose(ref pose);
                            samples.Add(new DiagnosisSample { motion = motion, extractionReference = avatar == original ? "ORIGINAL_CONTROL" : "CALIBRATED",
                                phase = phase, outsideNominalMuscles = outside, directRoundtrip = Compare(source, extraction, skin, mesh, phase, false) });
                        }
                    }
                    finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(extraction); }
                }
                CheckHashes(inputs); result.inputAssetsUnchanged = true; result.status = "MEASURED_NOT_ACCEPTANCE"; return result;
            }
            catch (Exception e) { result.error = e.ToString(); result.status = "DIAGNOSIS_FAILED"; throw; }
            finally
            {
                result.samples = samples.ToArray(); Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(ReportPath)!, "reference-locomotion-diagnosis.json"), JsonUtility.ToJson(result, true));
            }
        }
        private static void Batch(Func<Report> work)
        {
            try { work(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static Report Generate()
        {
            var report = NewReport("Generate");
            var audits = new List<MotionAudit>();
            try
            {
                var inputs = RecordInputs();
                var prefab = Load<GameObject>(MeshyHasanPilotPipeline.PrefabPath);
                var avatar = MeshyHumanoidCalibration.LoadTargetAvatar();
                ValidateSameRigAvatar(prefab, avatar);
                Directory.CreateDirectory(OutputRoot); AssetDatabase.Refresh();
                foreach (var motion in Motions)
                {
                    var source = SourceClip(motion);
                    var audit = NewAudit(motion, prefab, avatar, source);
                    var clip = Convert(prefab, avatar, source, audit);
                    try { Store(clip, ClipPath(motion)); }
                    finally { if (!AssetDatabase.Contains(clip)) Object.DestroyImmediate(clip); }
                    audits.Add(audit);
                }
                AssetDatabase.SaveAssets();
                foreach (var audit in audits)
                    Measure(prefab, avatar, SourceClip(audit.motion), Load<AnimationClip>(ClipPath(audit.motion)), audit);
                report.motions = audits.ToArray(); CheckHashes(inputs); report.inputAssetsUnchanged = true;
                var manifest = new Manifest { inputs = inputs, outputs = Motions.Select(ClipPath).Select(Hash).ToArray(), motions = report.motions };
                File.WriteAllText(ManifestPath, JsonUtility.ToJson(manifest, true));
                AssetDatabase.ImportAsset(ManifestPath); AssetDatabase.SaveAssets();
                ValidateAcceptance(report); return report;
            }
            catch (Exception e) { report.motions = audits.ToArray(); report.error = e.ToString(); report.status = "REFERENCE_CONVERSION_FAILED"; throw; }
            finally { WriteReport(report); }
        }

        /// <summary>Read-only Assets verification, writes only an external evidence report.</summary>
        public static Report Verify()
        {
            var report = NewReport("Verify"); var audits = new List<MotionAudit>();
            try
            {
                Require(File.Exists(ManifestPath), "Reference conversion has not been generated.");
                var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
                Require(manifest != null && manifest.schemaVersion == 1 && manifest.samplesPerSecond == SamplesPerSecond, "Unknown conversion provenance.");
                CheckHashes(manifest.inputs); CheckHashes(manifest.outputs);
                Require(manifest.outputs.Length == 2 && Motions.All(m => manifest.outputs.Count(h => h.path == ClipPath(m)) == 1), "Missing or duplicate candidate clip provenance.");
                var prefab = Load<GameObject>(MeshyHasanPilotPipeline.PrefabPath); var avatar = MeshyHumanoidCalibration.LoadTargetAvatar();
                ValidateSameRigAvatar(prefab, avatar);
                foreach (var motion in Motions)
                {
                    var audit = NewAudit(motion, prefab, avatar, SourceClip(motion));
                    var authored = manifest.motions.Single(m => m.motion == motion);
                    audit.authoredFrames = authored.authoredFrames; audit.maximumAbsoluteMuscle = authored.maximumAbsoluteMuscle;
                    audit.maximumExtractionRoundtripJointMeters = authored.maximumExtractionRoundtripJointMeters;
                    audit.maximumExtractionRoundtripSkinMeters = authored.maximumExtractionRoundtripSkinMeters;
                    Measure(prefab, avatar, SourceClip(motion), Load<AnimationClip>(ClipPath(motion)), audit); audits.Add(audit);
                }
                CheckHashes(manifest.inputs); report.inputAssetsUnchanged = true; report.motions = audits.ToArray();
                ValidateAcceptance(report); return report;
            }
            catch (Exception e) { report.motions = audits.ToArray(); report.error = e.ToString(); report.status = "REFERENCE_CONVERSION_FAILED"; throw; }
            finally { WriteReport(report); }
        }

        public static void ValidateSameRigAvatar(GameObject prefab, Avatar avatar)
        {
            Require(prefab != null && prefab.GetComponent<Animator>() != null, "Missing supplied Meshy prefab/Animator.");
            var original = prefab.GetComponent<Animator>().avatar;
            Require(original != null && original.isValid && original.isHuman && avatar != null && avatar.isValid && avatar.isHuman, "Both reference Avatars must be valid Human.");
            var expected = original.humanDescription.human.Select(b => b.humanName + ":" + b.boneName).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            var actual = avatar.humanDescription.human.Select(b => b.humanName + ":" + b.boneName).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            Require(expected.SequenceEqual(actual), "Conversion requires the same physical rig and semantic mapping; foreign/side-swapped rigs are forbidden.");
        }

        private static AnimationClip Convert(GameObject prefab, Avatar avatar, AnimationClip original, MotionAudit audit)
        {
            using (var source = new Playback(prefab, prefab.GetComponent<Animator>().avatar, original))
            {
                var extraction = CreateClone(prefab, avatar, false); AnimationClip? result = null;
                try
                {
                    var copy = new PhysicalPoseCopy(source.Actor, extraction);
                    var skin = LodZero(extraction); var baked = new Mesh();
                    try
                    {
                        var names = HumanTrait.MuscleName.Concat(new[] { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" }).ToArray();
                        var frames = Mathf.Max(1, Mathf.CeilToInt(original.length * SamplesPerSecond));
                        var keys = names.Select(_ => new List<Keyframe>(frames + 1)).ToArray();
                        var pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] }; var previous = Quaternion.identity;
                        using (var handler = new HumanPoseHandler(avatar, extraction.transform))
                        for (var frame = 0; frame <= frames; frame++)
                        {
                            var time = original.length * frame / frames;
                            source.Sample(time); copy.Copy(); handler.GetHumanPose(ref pose); ValidatePose(pose);
                            if (frame > 0 && Quaternion.Dot(previous, pose.bodyRotation) < 0f)
                                pose.bodyRotation = new Quaternion(-pose.bodyRotation.x, -pose.bodyRotation.y, -pose.bodyRotation.z, -pose.bodyRotation.w);
                            previous = pose.bodyRotation;
                            var values = pose.muscles.Concat(new[] { pose.bodyPosition.x, pose.bodyPosition.y, pose.bodyPosition.z,
                                pose.bodyRotation.x, pose.bodyRotation.y, pose.bodyRotation.z, pose.bodyRotation.w }).ToArray();
                            for (var index = 0; index < names.Length; index++) keys[index].Add(new Keyframe(time, values[index]));
                            audit.maximumAbsoluteMuscle = Mathf.Max(audit.maximumAbsoluteMuscle, pose.muscles.Max(Mathf.Abs));
                            // Separately measure representability before curve/playback error.
                            // Never hide a HumanPose conversion floor by moving the body.
                            if (frame % Math.Max(1, frames / 8) == 0 || frame == frames)
                            {
                                handler.SetHumanPose(ref pose);
                                var direct = Compare(source, extraction, skin, baked, frame / (float)frames, false);
                                audit.maximumExtractionRoundtripJointMeters = Mathf.Max(audit.maximumExtractionRoundtripJointMeters, direct.maximumJointErrorMeters);
                                audit.maximumExtractionRoundtripSkinMeters = Mathf.Max(audit.maximumExtractionRoundtripSkinMeters, direct.maximumSkinErrorMeters);
                            }
                        }
                        result = new AnimationClip { name = "ANM_MeshyReference_" + audit.motion, frameRate = SamplesPerSecond, legacy = false };
                        for (var index = 0; index < names.Length; index++)
                        {
                            var curve = new AnimationCurve(keys[index].ToArray());
                            for (var key = 0; key < curve.length; key++)
                            {
                                AnimationUtility.SetKeyLeftTangentMode(curve, key, AnimationUtility.TangentMode.Linear);
                                AnimationUtility.SetKeyRightTangentMode(curve, key, AnimationUtility.TangentMode.Linear);
                            }
                            AnimationUtility.SetEditorCurve(result, EditorCurveBinding.FloatCurve("", typeof(Animator), names[index]), curve);
                        }
                        var settings = AnimationUtility.GetAnimationClipSettings(result);
                        settings.loopTime = AnimationUtility.GetAnimationClipSettings(original).loopTime;
                        settings.loopBlend = false;
                        settings.loopBlendPositionY = true; settings.loopBlendPositionXZ = true; settings.loopBlendOrientation = true;
                        settings.keepOriginalPositionY = true; settings.keepOriginalPositionXZ = true; settings.keepOriginalOrientation = true;
                        settings.heightFromFeet = false; settings.level = 0f; settings.orientationOffsetY = 0f;
                        AnimationUtility.SetAnimationClipSettings(result, settings);
                        MeshyHasanPilotMotionAdaptation.AuditClip(result, audit.motion);
                        audit.authoredFrames = frames + 1;
                        return result;
                    }
                    finally { Object.DestroyImmediate(baked); }
                }
                catch { if (result != null) Object.DestroyImmediate(result); throw; }
                finally { Object.DestroyImmediate(extraction); }
            }
        }

        private static void Measure(GameObject prefab, Avatar avatar, AnimationClip original, AnimationClip candidate, MotionAudit audit)
        {
            MeshyHasanPilotMotionAdaptation.AuditClip(candidate, audit.motion);
            Require(Mathf.Abs(original.length - candidate.length) < .00001f, "Conversion changed duration.");
            using (var source = new Playback(prefab, prefab.GetComponent<Animator>().avatar, original))
            using (var target = new Playback(prefab, avatar, candidate))
            {
                var frames = Mathf.Max(1, Mathf.CeilToInt(original.length * SamplesPerSecond)); var samples = new List<Sample>();
                // Every training frame AND independent half-frame interpolation.
                for (var step = 0; step <= frames * 2; step++)
                {
                    var phase = step / (float)(frames * 2); var time = original.length * phase;
                    source.Sample(time); target.Sample(time);
                    samples.Add(Compare(source, target.Actor, target.Skin, target.Baked, phase, step % 2 != 0));
                }
                audit.samples = samples.ToArray(); audit.measuredFrames = samples.Count; audit.heldOutFrames = samples.Count(s => s.heldOut);
                audit.maximumJointErrorMeters = samples.Max(s => s.maximumJointErrorMeters);
                audit.maximumJointRotationErrorDegrees = samples.Max(s => s.maximumJointRotationErrorDegrees);
                audit.maximumSkinErrorMeters = samples.Max(s => s.maximumSkinErrorMeters);
                audit.withinTolerance = audit.maximumJointErrorMeters <= PositionToleranceMeters && audit.maximumSkinErrorMeters <= PositionToleranceMeters
                    && audit.maximumJointRotationErrorDegrees <= RotationToleranceDegrees
                    && samples.All(s => s.actorRootPositionErrorMeters < .00001f && s.actorRootRotationErrorDegrees < .01f);
            }
        }

        private static Sample Compare(Playback source, GameObject target, SkinnedMeshRenderer targetSkin, Mesh targetBaked, float phase, bool heldOut)
        {
            var sample = new Sample { phase = phase, heldOut = heldOut };
            var sourceBones = source.Skin.bones; var targetBones = targetSkin.bones;
            Require(sourceBones.Length == 23 && targetBones.Length == sourceBones.Length, "The original 23 skin bones must remain intact.");
            var boneErrors = new List<BoneError>();
            for (var i = 0; i < sourceBones.Length; i++)
            {
                Require(sourceBones[i].name == targetBones[i].name, "Bone order/hierarchy changed.");
                var error = Vector3.Distance(sourceBones[i].position, targetBones[i].position);
                var angle = Quaternion.Angle(sourceBones[i].rotation, targetBones[i].rotation);
                Require(Finite(error) && Finite(angle), "Nonfinite joint comparison.");
                if (error > sample.maximumJointErrorMeters) { sample.maximumJointErrorMeters = error; sample.worstJoint = sourceBones[i].name; }
                if (angle > sample.maximumJointRotationErrorDegrees) sample.worstRotationJoint = sourceBones[i].name;
                sample.maximumJointRotationErrorDegrees = Mathf.Max(sample.maximumJointRotationErrorDegrees, angle);
                boneErrors.Add(new BoneError { name = sourceBones[i].name, positionMeters = error, worldRotationDegrees = angle,
                    localRotationDegrees = Quaternion.Angle(sourceBones[i].localRotation, targetBones[i].localRotation) });
            }
            source.Skin.BakeMesh(source.Baked); targetSkin.BakeMesh(targetBaked);
            var a = source.Baked.vertices; var b = targetBaked.vertices;
            Require(a.Length > 0 && a.Length == b.Length && source.Skin.sharedMesh == targetSkin.sharedMesh, "Pose comparison requires identical original LOD0 geometry.");
            sample.originalMinimumSkinY = float.PositiveInfinity; sample.candidateMinimumSkinY = float.PositiveInfinity;
            for (var i = 0; i < a.Length; i++)
            {
                var p = source.Skin.transform.TransformPoint(a[i]); var q = targetSkin.transform.TransformPoint(b[i]);
                Require(Finite(p) && Finite(q), "Nonfinite baked skin comparison."); var error = Vector3.Distance(p, q);
                if (error > sample.maximumSkinErrorMeters) { sample.maximumSkinErrorMeters = error; sample.worstSkinVertex = i; }
                sample.originalMinimumSkinY = Mathf.Min(sample.originalMinimumSkinY, p.y);
                sample.candidateMinimumSkinY = Mathf.Min(sample.candidateMinimumSkinY, q.y);
            }
            sample.actorRootPositionErrorMeters = Vector3.Distance(source.Actor.transform.position, target.transform.position);
            sample.actorRootRotationErrorDegrees = Quaternion.Angle(source.Actor.transform.rotation, target.transform.rotation);
            sample.bones = boneErrors.ToArray(); var weights = source.Skin.sharedMesh.boneWeights[sample.worstSkinVertex];
            var indices = new[] { weights.boneIndex0, weights.boneIndex1, weights.boneIndex2, weights.boneIndex3 };
            var values = new[] { weights.weight0, weights.weight1, weights.weight2, weights.weight3 };
            sample.worstVertexWeights = Enumerable.Range(0, 4).Where(i => values[i] > 0f)
                .Select(i => new WeightInfluence { bone = sourceBones[indices[i]].name, weight = values[i] }).ToArray();
            return sample;
        }

        private sealed class PhysicalPoseCopy
        {
            private readonly Transform[] source, target;
            public PhysicalPoseCopy(GameObject from, GameObject to)
            {
                source = from.GetComponentsInChildren<Transform>(true);
                target = source.Select(t => t == from.transform ? to.transform : to.transform.Find(AnimationUtility.CalculateTransformPath(t, from.transform))).ToArray();
                Require(target.All(t => t != null), "Reference conversion is same-hierarchy only.");
            }
            public void Copy()
            {
                for (var i = 0; i < source.Length; i++)
                { target[i].localPosition = source[i].localPosition; target[i].localRotation = source[i].localRotation; target[i].localScale = source[i].localScale; }
            }
        }

        private sealed class Playback : IDisposable
        {
            public readonly GameObject Actor; public readonly SkinnedMeshRenderer Skin; public readonly Mesh Baked;
            private PlayableGraph graph; private AnimationClipPlayable playable;
            public Playback(GameObject prefab, Avatar avatar, AnimationClip clip)
            {
                Require(clip != null && clip.isHumanMotion && clip.length > 0f, "Expected the actual nonempty Humanoid clip.");
                Actor = CreateClone(prefab, avatar, true); Skin = LodZero(Actor); Baked = new Mesh();
                try
                {
                    graph = PlayableGraph.Create("Measured Meshy reference locomotion"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    playable = AnimationClipPlayable.Create(graph, clip); playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
                    AnimationPlayableOutput.Create(graph, "Actual Humanoid playback", Actor.GetComponent<Animator>()).SetSourcePlayable(playable); graph.Play();
                }
                catch { Dispose(); throw; }
            }
            public void Sample(float time) { playable.SetTime(time); graph.Evaluate(0f); }
            public void Dispose() { if (graph.IsValid()) graph.Destroy(); Object.DestroyImmediate(Baked); Object.DestroyImmediate(Actor); }
        }

        private static GameObject CreateClone(GameObject prefab, Avatar avatar, bool animated)
        {
            var actor = Object.Instantiate(prefab);
            try
            {
                actor.SetActive(true); actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Require((actor.transform.localScale - Vector3.one).sqrMagnitude < 1e-10f, "Root must be unit scale for normalized HumanPose body curves.");
                var animator = actor.GetComponent<Animator>(); animator.enabled = false; animator.runtimeAnimatorController = null;
                FOC.Presentation.Visuals.MeshyHasanPilotPlayer.RestoreSourceBindPose(prefab, actor);
                animator.avatar = avatar; animator.Rebind();
                // Rebind may select the Avatar's reference. Actual source bind
                // remains the unchanged physical starting point for BOTH paths.
                FOC.Presentation.Visuals.MeshyHasanPilotPlayer.RestoreSourceBindPose(prefab, actor);
                animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.enabled = animated;
                foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
                return actor;
            }
            catch { Object.DestroyImmediate(actor); throw; }
        }

        private static SkinnedMeshRenderer LodZero(GameObject actor) => actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s => s.sharedMesh != null).OrderByDescending(s => s.sharedMesh.triangles.Length).First();
        private static AnimationClip SourceClip(string motion) => AssetDatabase.LoadAllAssetsAtPath(MeshyHasanPilotBuild.SourcePath).OfType<AnimationClip>().Single(c => c.name == motion);
        private static MotionAudit NewAudit(string motion, GameObject prefab, Avatar avatar, AnimationClip source) => new MotionAudit
        { motion = motion, sourceClip = source.name, candidateClip = ClipPath(motion), originalAvatar = prefab.GetComponent<Animator>().avatar.name, candidateAvatar = avatar.name, duration = source.length };
        /// <summary>Only freshly verified candidates may be requested by an integration caller.</summary>
        public static AnimationClip[] LoadAcceptedClips()
        {
            Verify();
            return Motions.Select(m => Load<AnimationClip>(ClipPath(m))).ToArray();
        }

        /// <summary>Fail closed even if a stale report claims PASS or withinTolerance.</summary>
        public static void ValidateAcceptance(Report report)
        {
            Require(report != null && report.inputAssetsUnchanged && report.motions != null && report.motions.Length == 2
                && Motions.All(name => report.motions.Count(m => m.motion == name) == 1), "Complete unchanged-input reference conversion evidence is required.");
            foreach (var motion in report.motions)
            {
                Require(motion.authoredFrames >= 2 && motion.heldOutFrames == motion.authoredFrames - 1
                    && motion.measuredFrames == motion.authoredFrames * 2 - 1 && motion.samples.Length == motion.measuredFrames
                    && motion.samples.Count(s => s.heldOut) == motion.heldOutFrames, "Missing actual training/held-out playback samples.");
                var samples = motion.samples;
                Require(motion.withinTolerance && Finite(motion.maximumJointErrorMeters) && Finite(motion.maximumSkinErrorMeters)
                    && Finite(motion.maximumJointRotationErrorDegrees) && motion.maximumJointErrorMeters <= PositionToleranceMeters
                    && motion.maximumSkinErrorMeters <= PositionToleranceMeters && motion.maximumJointRotationErrorDegrees <= RotationToleranceDegrees
                    && samples.All(s => Finite(s.phase) && s.phase >= 0f && s.phase <= 1f && Finite(s.maximumJointErrorMeters)
                        && s.maximumJointErrorMeters >= 0f && s.maximumJointErrorMeters <= PositionToleranceMeters
                        && Finite(s.maximumSkinErrorMeters) && s.maximumSkinErrorMeters >= 0f && s.maximumSkinErrorMeters <= PositionToleranceMeters
                        && Finite(s.maximumJointRotationErrorDegrees) && s.maximumJointRotationErrorDegrees >= 0f && s.maximumJointRotationErrorDegrees <= RotationToleranceDegrees
                        && Finite(s.actorRootPositionErrorMeters) && s.actorRootPositionErrorMeters >= 0f && s.actorRootPositionErrorMeters < .00001f
                        && Finite(s.actorRootRotationErrorDegrees) && s.actorRootRotationErrorDegrees >= 0f && s.actorRootRotationErrorDegrees < .01f),
                    "Reference conversion exceeded strict 5 mm joint/skin or 1 degree joint tolerance; no claimed PASS or grounding compensation can select it.");
                Require(Mathf.Abs(samples.Max(s => s.maximumJointErrorMeters) - motion.maximumJointErrorMeters) < 1e-6f
                    && Mathf.Abs(samples.Max(s => s.maximumSkinErrorMeters) - motion.maximumSkinErrorMeters) < 1e-6f
                    && Mathf.Abs(samples.Max(s => s.maximumJointRotationErrorDegrees) - motion.maximumJointRotationErrorDegrees) < 1e-6f,
                    "Reference conversion summary does not match actual samples.");
            }
            report.status = "NUMERICAL_REFERENCE_CONVERSION_PASS_VISUAL_ACCEPTANCE_PENDING";
        }
        private static AssetHash[] RecordInputs()
        {
            var roots = new[] { MeshyHasanPilotPipeline.PrefabPath, MeshyHasanPilotBuild.SourcePath, MeshyHumanoidCalibration.TargetAvatarPath };
            return roots.Concat(AssetDatabase.GetDependencies(roots, true)).Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && File.Exists(p) && !p.StartsWith(OutputRoot + "/", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal).SelectMany(p => File.Exists(p + ".meta") ? new[] { p, p + ".meta" } : new[] { p }).OrderBy(p => p, StringComparer.Ordinal).Select(Hash).ToArray();
        }
        private static void CheckHashes(IEnumerable<AssetHash> hashes) { foreach (var h in hashes) Require(Hash(h.path).sha256 == h.sha256, "Protected conversion asset changed: " + h.path); }
        private static AssetHash Hash(string path)
        { using (var sha = SHA256.Create()) using (var file = File.OpenRead(path)) return new AssetHash { path = path, sha256 = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant() }; }
        private static void Store(AnimationClip generated, string path)
        { var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path); if (existing == null) AssetDatabase.CreateAsset(generated, path); else { EditorUtility.CopySerialized(generated, existing); EditorUtility.SetDirty(existing); } }
        private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing conversion input: " + path);
        private static void ValidatePose(HumanPose p)
        { Require(p.muscles != null && p.muscles.Length == HumanTrait.MuscleCount && p.muscles.All(Finite) && Finite(p.bodyPosition) && Finite(p.bodyRotation.x) && Finite(p.bodyRotation.y) && Finite(p.bodyRotation.z) && Finite(p.bodyRotation.w) && Mathf.Abs(Quaternion.Dot(p.bodyRotation, p.bodyRotation) - 1f) < .001f, "Invalid HumanPose; never clamp or reorient it to pass."); }
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        private static void Require(bool ok, string error) { if (!ok) throw new InvalidOperationException(error); }
        private static Report NewReport(string operation) => new Report { operation = operation, unityVersion = Application.unityVersion, utc = DateTime.UtcNow.ToString("O") };
        private static void WriteReport(Report report) { Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!); File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true)); }
    }
}
