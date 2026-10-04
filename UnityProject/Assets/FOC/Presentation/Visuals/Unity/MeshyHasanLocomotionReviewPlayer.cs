#nullable enable
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FOC.Presentation.Visuals
{
    public sealed partial class MeshyHasanPilotPlayer
    {
        [Serializable] private sealed class LocomotionEvidence
        {
            public string status = "NOT_COMPLETED", sourceSha = "", unityVersion = "", avatar = "";
            public string sourceClipPolicy = "Meshy Walking/Running remain on their original source Avatar; calibrated Avatar uses the retained CC0 UAL Walk/Jog/Sprint Humanoid library because direct cross-Avatar rebake was measured lossy.";
            public string[] requiredChecks = { "heel/toe contact", "penetration", "hover", "foot sliding", "loop continuity", "calibrated-avatar arm stability" };
            public CaptureEvidence[] captures = Array.Empty<CaptureEvidence>();
        }

        private IEnumerator RunLocomotionReview()
        {
            if (output == null || calibratedAvatar == null || !calibratedAvatar.isValid || !calibratedAvatar.isHuman)
                throw new InvalidOperationException("Locomotion review requires a valid calibrated Avatar and output path.");
            if (locomotionClips.Length < 3) throw new InvalidOperationException("Walk/Jog/Sprint library clips are missing.");
            var names = new[] { "Walking", "Running" };
            foreach (var name in names)
            {
                ClearActors();
                var actor = CreateActor(FindClip(name), Vector3.zero, null);
                actor.calibrationScenario = "SourceMeshy-" + name;
                actor.sourceClipName = name;
                foreach (var phase in new[] { 0f, .25f, .5f, .75f })
                {
                    yield return null; SetPhase(actor, phase); var sampled = SnapshotTrackedJoints(actor);
                    yield return new WaitForEndOfFrame(); AssertStablePoseAcrossRenderBoundary(actor, sampled);
                    Frame("quarter");
                    Capture("source-" + name.ToLowerInvariant() + "-" + phase.ToString("0.00", CultureInfo.InvariantCulture) + ".png", "quarter", phase);
                }
            }
            foreach (var clip in locomotionClips)
            {
                ClearActors();
                var actor = CreateActor(clip, Vector3.zero, calibratedAvatar);
                actor.calibrationScenario = "CalibratedLibrary-" + clip.name;
                actor.sourceClipName = clip.name;
                foreach (var phase in new[] { 0f, .25f, .5f, .75f })
                {
                    yield return null; SetPhase(actor, phase); var sampled = SnapshotTrackedJoints(actor);
                    yield return new WaitForEndOfFrame(); AssertStablePoseAcrossRenderBoundary(actor, sampled);
                    Frame("quarter");
                    Capture("calibrated-" + clip.name.Replace('|', '_') + "-" + phase.ToString("0.00", CultureInfo.InvariantCulture) + ".png", "quarter", phase);
                }
            }
            ClearActors();
            var result = new LocomotionEvidence
            {
                status = "WINDOWS_CAPTURE_COMPLETE_VISUAL_CONTACT_AND_ARM_QA_REQUIRED",
                sourceSha = Arg("--meshy-sha") ?? "NOT_SUPPLIED", unityVersion = Application.unityVersion,
                avatar = calibratedAvatar.name, captures = captures.ToArray()
            };
            File.WriteAllText(Path.Combine(output, "locomotion-evidence.json"), JsonUtility.ToJson(result, true));
            Application.Quit(0);
        }

        private IEnumerator RunCalibratedBenchmark()
        {
            if (output == null || calibratedAvatar == null || !calibratedAvatar.isValid || !calibratedAvatar.isHuman)
                throw new InvalidOperationException("Calibrated benchmark requires a valid calibrated Avatar and output path.");
            var clip = locomotionClips.FirstOrDefault(c => c != null && c.name.EndsWith("Walk_Loop", StringComparison.Ordinal))
                ?? locomotionClips.FirstOrDefault(c => c != null);
            if (clip == null) throw new InvalidOperationException("Calibrated benchmark motion is missing.");
            MeshyHasanPilotBenchmark.Report? report = null;
            var benchmark = MeshyHasanPilotBenchmark.Run(characterPrefab, clip, transform,
                r => report = r, b => FrameBounds(b), 30, calibratedAvatar);
            try { while (benchmark.MoveNext()) yield return benchmark.Current; }
            finally { (benchmark as IDisposable)?.Dispose(); }
            if (report == null || report.cases.Length != 3) throw new InvalidOperationException("Calibrated 1/12/100 benchmark did not produce all cases.");
            report.status = "CALIBRATED_AVATAR_ASSEMBLER_POOL_BENCHMARK_COMPLETE";
            File.WriteAllText(Path.Combine(output, "calibrated-benchmark.json"), JsonUtility.ToJson(report, true));
            Application.Quit(0);
        }
    }
}
