#nullable enable
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FOC.Presentation.Visuals
{
    /// <summary>Optional measured A/B/C mode of the existing assembler/cache/pool player.</summary>
    public sealed partial class MeshyHasanPilotPlayer
    {
        public Avatar? calibratedAvatar;
        public AnimationClip[] dualCalibrationClips = Array.Empty<AnimationClip>();

        private static readonly HumanBodyBones[] CalibrationTrackedBones =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest,
            HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes
        };

        [Serializable]
        public sealed class CalibrationPlayerEvidence
        {
            public string status = "TECHNICAL_CAPTURE_NOT_CALIBRATION_ACCEPTANCE";
            public string sourceSha = "", unityVersion = "", platform = "", graphicsDevice = "";
            public string[] scenarios = Array.Empty<string>();
            public string contactStatus = "NOT_CORRECTED: raw A/B/C motion comparison, not foot-contact acceptance";
            public string mountStatus = "NOT_RUN_ON_FOOT_GATE_REQUIRED", benchmarkStatus = "NOT_RUN_ON_FOOT_GATE_REQUIRED";
            public int captures, activeLeases, poolViewsCreated, poolViewsReused;
            public CaptureEvidence[] results = Array.Empty<CaptureEvidence>();
        }

        private IEnumerator RunCalibration()
        {
            if (output == null) throw new InvalidOperationException("Calibration requires an explicit output directory.");
            if (calibratedAvatar == null || !calibratedAvatar.isHuman || !calibratedAvatar.isValid)
                throw new InvalidOperationException("Generate and validate the separate calibrated Avatar before the player comparison.");
            if (dualCalibrationClips.Length != 0 && dualCalibrationClips.Length != 6)
                throw new InvalidOperationException("Partial scenario C clips cannot be treated as a complete comparison.");
            var scenarios = dualCalibrationClips.Length == 6 ? new[] { "A", "B", "C" } : new[] { "A", "B" };
            foreach (var scenario in scenarios)
            {
                var selectedClips = scenario == "C" ? dualCalibrationClips : retargetClips;
                foreach (var motion in new[] { "Idle", "Turn", "OneHandedAttack", "ArmRaise", "Crouch", "MountedSeated" })
                {
                    var selected = selectedClips.Where(c => c != null && c.name.EndsWith("_" + motion, StringComparison.Ordinal)).ToArray();
                    if (selected.Length != 1) throw new InvalidOperationException("Required comparison clip missing: " + scenario + "/" + motion);
                    ClearActors();
                    var actor = CreateActor(selected[0], Vector3.zero, scenario == "A" ? null : calibratedAvatar);
                    actor.calibrationScenario = scenario;
                    actor.sourceClipName = "ANM_HasanDonor_" + motion;
                    foreach (var phase in new[] { .25f, .5f, .75f })
                    {
                        yield return null; SetPhase(actor, phase);
                        var sampled = SnapshotTrackedJoints(actor);
                        yield return new WaitForEndOfFrame();
                        AssertStablePoseAcrossRenderBoundary(actor, sampled);
                        // Fixed framing and ground=0 for every case; no hidden height or yaw correction.
                        FrameBounds(new Bounds(new Vector3(0, .9f, 0), new Vector3(2.2f, 1.8f, .8f)));
                        Capture(scenario + "-" + motion.ToLowerInvariant() + "-" + phase.ToString("0.00", CultureInfo.InvariantCulture) + ".png", "calibration-quarter", phase);
                    }
                }
            }
            // Raw original imported locomotion on both Avatars reveals whether changing
            // the reference alone damages source motion. This is NOT contact cleanup.
            foreach (var scenario in new[] { "A", "B" }) foreach (var motion in new[] { "Walking", "Running" })
            {
                ClearActors();
                var actor = CreateActor(FindClip(motion), Vector3.zero, scenario == "A" ? null : calibratedAvatar);
                actor.calibrationScenario = scenario;
                actor.sourceClipName = FindClip(motion).name;
                foreach (var phase in new[] { .25f, .75f })
                {
                    yield return null; SetPhase(actor, phase); var sampled = SnapshotTrackedJoints(actor);
                    yield return new WaitForEndOfFrame(); AssertStablePoseAcrossRenderBoundary(actor, sampled);
                    FrameBounds(new Bounds(new Vector3(0, .9f, 0), new Vector3(2.2f, 1.8f, .8f)));
                    Capture(scenario + "-raw-" + motion.ToLowerInvariant() + "-" + phase.ToString("0.00", CultureInfo.InvariantCulture) + ".png", "calibration-quarter", phase);
                }
            }
            ClearActors();
            if (captures.Count != scenarios.Length * 18 + 8 || assembler.ActiveLeaseCount != 0 || pool.LeasedCount != 0)
                throw new InvalidOperationException("Incomplete calibration capture matrix or leaked presentation lease.");
            var report = new CalibrationPlayerEvidence
            {
                sourceSha = Arg("--meshy-sha") ?? "NOT_SUPPLIED", unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(), graphicsDevice = SystemInfo.graphicsDeviceName,
                scenarios = scenarios, captures = captures.Count, activeLeases = assembler.ActiveLeaseCount,
                poolViewsCreated = pool.CreatedViewCount, poolViewsReused = pool.ReusedViewCount, results = captures.ToArray()
            };
            File.WriteAllText(Path.Combine(output, "calibration-player-evidence.json"), JsonUtility.ToJson(report, true));
            Debug.Log("FOC_MESHY_CALIBRATION_CAPTURE_TECHNICAL_PASS captures=" + captures.Count);
            Application.Quit(0);
        }
    }
}
