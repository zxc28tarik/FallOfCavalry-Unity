#nullable enable
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FOC.Presentation.Visuals
{
    public sealed partial class MeshyHasanPilotPlayer
    {
        [Serializable] private sealed class MountedEvidence
        {
            public string status = "NOT_COMPLETED", sourceSha = "", unityVersion = "", avatar = "";
            public string horse = "", harness = "", riderSocket = "Socket_Rider";
            public string[] checks = { "pelvis/saddle", "knees", "boots/stirrups", "hands/reins", "coat/horse clipping", "torso posture" };
            public string[] captures = Array.Empty<string>();
            public string posePolicy = "Seat position and measured tack anchors constrain presentation-only limb rotations after Mecanim. Source mesh, bind poses, weights, horse and harness are unchanged. Sitting clip alone is not riding acceptance.";
            public MountedFitSample[] samples = Array.Empty<MountedFitSample>();
        }
        [Serializable] private sealed class MountedFitSample
        {
            public string state=""; public float phase,pelvisSeatDistance,leftAnkleError,rightAnkleError,leftHandError,rightHandError;
            public Vector3 pelvis,leftAnkle,rightAnkle,leftKnee,rightKnee,leftHand,rightHand;
        }

        private sealed class HorseMotion : IDisposable
        {
            public readonly GameObject instance;
            public readonly Animator animator;
            public readonly PlayableGraph graph;
            public readonly AnimationClipPlayable playable;
            public readonly AnimationClip clip;
            public HorseMotion(GameObject prefab, AnimationClip source)
            {
                instance = Instantiate(prefab); instance.name = "MNT_Horse_Anatolian_01_REVIEW";
                animator = instance.GetComponent<Animator>() ?? throw new InvalidOperationException("Horse Animator missing.");
                clip = source; animator.enabled = false; animator.runtimeAnimatorController = null; animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.enabled = true; animator.Rebind();
                graph = PlayableGraph.Create("Mounted horse review"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                playable = AnimationClipPlayable.Create(graph, source); playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
                AnimationPlayableOutput.Create(graph, "Horse review", animator).SetSourcePlayable(playable); graph.Play(); graph.Evaluate(0);
            }
            public void SetPhase(float phase) { playable.SetTime(phase * clip.length); graph.Evaluate(0); }
            public void Dispose() { if (graph.IsValid()) graph.Destroy(); if (instance != null) Destroy(instance); }
        }

        private IEnumerator RunMountedReview()
        {
            if (output == null || calibratedAvatar == null || !calibratedAvatar.isValid || !calibratedAvatar.isHuman)
                throw new InvalidOperationException("Mounted review requires a valid calibrated Avatar and output path.");
            if (horsePrefab == null || harnessPrefab == null || horseClips.Length < 3)
                throw new InvalidOperationException("Existing horse, harness or horse animations are missing.");
            var riderClip = locomotionClips.FirstOrDefault(c => c != null && c.name.EndsWith("Sitting_Idle_Loop", StringComparison.Ordinal))
                ?? libraryMotionClips.FirstOrDefault(c => c != null && c.name.EndsWith("Sitting_Idle_Loop", StringComparison.Ordinal));
            if (riderClip == null) throw new InvalidOperationException("Existing seated Humanoid review clip is missing.");
            var horseRoot = new GameObject("Mounted review root");
            var horse = new HorseMotion(horsePrefab, horseClips.First(c => c.name.EndsWith("Idle", StringComparison.Ordinal)));
            horse.instance.transform.SetParent(horseRoot.transform, false);
            var harness = Instantiate(harnessPrefab, horse.instance.transform, false); harness.name = "HAR_SipahiHarness_01_REVIEW";
            var socket = horse.instance.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == "Socket_Rider");
            if (socket == null) throw new InvalidOperationException("Existing horse Socket_Rider is missing.");
            ClearActors();
            var actor = CreateActor(riderClip, Vector3.zero, calibratedAvatar);
            actor.calibrationScenario = "Mounted-Calibrated"; actor.sourceClipName = riderClip.name;
            actor.view.transform.SetParent(socket, false);
            actor.view.transform.localPosition = Vector3.zero;
            actor.view.transform.localRotation = Quaternion.identity;
            var capturesMounted = new System.Collections.Generic.List<string>();
            var fitSamples = new System.Collections.Generic.List<MountedFitSample>();
            var rest = Instantiate(characterPrefab); rest.SetActive(false);
            var restAnimator=rest.GetComponent<Animator>();
            restAnimator.enabled=false; RestoreSourceBindPose(characterPrefab,rest);
            var leftFootRotation=restAnimator.GetBoneTransform(HumanBodyBones.LeftFoot).rotation;
            var rightFootRotation=restAnimator.GetBoneTransform(HumanBodyBones.RightFoot).rotation;
            Destroy(rest);
            var states = new[] { ("idle", horseClips.First(c => c.name.EndsWith("Idle", StringComparison.Ordinal))),
                ("motion", horseClips.First(c => c.name.EndsWith("Walk", StringComparison.Ordinal))),
                ("gallop", horseClips.First(c => c.name.EndsWith("Gallop", StringComparison.Ordinal))) };
            foreach (var state in states)
            {
                actor.view.transform.SetParent(horseRoot.transform, false);
                horse.Dispose();
                horse = new HorseMotion(horsePrefab, state.Item2); horse.instance.transform.SetParent(horseRoot.transform, false);
                harness = Instantiate(harnessPrefab, horse.instance.transform, false); harness.name = "HAR_SipahiHarness_01_REVIEW_" + state.Item1;
                socket = horse.instance.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Socket_Rider");
                actor.view.transform.SetParent(socket, false); actor.view.transform.localPosition = Vector3.zero; actor.view.transform.localRotation = Quaternion.identity;
                for (var phaseIndex = 0; phaseIndex < (state.Item1 == "idle" ? 1 : 3); phaseIndex++)
                {
                    var phase = state.Item1 == "idle" ? .5f : phaseIndex / 3f;
                    SetPhase(actor, .5f); horse.SetPhase(phase);
                    fitSamples.Add(FitMountedPose(actor,socket,leftFootRotation,rightFootRotation,state.Item1,phase));
                    var sampled=SnapshotTrackedJoints(actor);
                    yield return null; yield return new WaitForEndOfFrame();
                    AssertStablePoseAcrossRenderBoundary(actor,sampled);
                    var file = "mounted-" + state.Item1 + "-" + phase.ToString("0.00", CultureInfo.InvariantCulture) + ".png";
                    FrameMounted(horseRoot, actor, socket, false); Capture(file, "mounted", phase); capturesMounted.Add(file);
                    if (state.Item1 == "idle")
                    {
                        var sideFile = "mounted-idle-side-" + phase.ToString("0.00", CultureInfo.InvariantCulture) + ".png";
                        FrameMounted(horseRoot, actor, socket, true); Capture(sideFile, "mounted-side", phase); capturesMounted.Add(sideFile);
                    }
                }
            }
            ClearActors(); horse.Dispose(); Destroy(horseRoot);
            var evidence = new MountedEvidence { status = "WINDOWS_MOUNTED_CAPTURE_COMPLETE_VISUAL_FIT_QA_REQUIRED", sourceSha = Arg("--meshy-sha") ?? "NOT_SUPPLIED", unityVersion = Application.unityVersion, avatar = calibratedAvatar.name, horse = horsePrefab.name, harness = harnessPrefab.name, captures = capturesMounted.ToArray(),samples=fitSamples.ToArray() };
            File.WriteAllText(Path.Combine(output, "mounted-evidence.json"), JsonUtility.ToJson(evidence, true));
            Application.Quit(0);
        }

        private static MountedFitSample FitMountedPose(Actor actor,Transform seat,Quaternion leftFootRotation,Quaternion rightFootRotation,string state,float phase)
        {
            Transform B(HumanBodyBones bone)=>actor.animator.GetBoneTransform(bone)??throw new InvalidOperationException("Mounted mapping missing: "+bone);
            var pelvis=B(HumanBodyBones.Hips);
            // Align the measured pelvis to the existing saddle socket; do not
            // guess a constant displacement from a standing character height.
            actor.view.transform.position+=seat.position-pelvis.position;
            var result=new MountedFitSample{state=state,phase=phase,pelvis=pelvis.position,pelvisSeatDistance=Vector3.Distance(pelvis.position,seat.position)};
            foreach(var left in new[]{true,false})
            {
                var sign=left?-1f:1f;
                var upper=B(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
                var lower=B(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
                var foot=B(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                // Authoring ledger: iron sole (x=+/-.46,y=1.01,z=-.22),
                // seat=(0,1.81,-.42). Meshy ankle is 0.1335m above its sole.
                var ankle=seat.TransformPoint(new Vector3(sign*.46f,-.6665f,.13f));
                HistoricalArtPoseReview.Limb(upper,lower,foot,ankle,seat.TransformDirection(new Vector3(sign*.45f,.02f,.65f)));
                foot.rotation=seat.rotation*(left?leftFootRotation:rightFootRotation);
                var arm=B(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
                var elbow=B(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
                var hand=B(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                var rein=seat.TransformPoint(new Vector3(sign*.11f,.21f,.30f));
                HistoricalArtPoseReview.Limb(arm,elbow,hand,rein,seat.TransformPoint(new Vector3(sign*.28f,.24f,-.06f))-arm.position);
                if(left){result.leftAnkle=foot.position;result.leftKnee=lower.position;result.leftHand=hand.position;result.leftAnkleError=Vector3.Distance(foot.position,ankle);result.leftHandError=Vector3.Distance(hand.position,rein);}
                else{result.rightAnkle=foot.position;result.rightKnee=lower.position;result.rightHand=hand.position;result.rightAnkleError=Vector3.Distance(foot.position,ankle);result.rightHandError=Vector3.Distance(hand.position,rein);}
            }
            return result;
        }

        private void FrameMounted(GameObject root, Actor actor, Transform socket, bool side)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
            var bounds = renderers.Length == 0 ? new Bounds(Vector3.zero, Vector3.one) : renderers.Skip(1).Aggregate(renderers[0].bounds, (b, r) => { b.Encapsulate(r.bounds); return b; });
            var distance = bounds.extents.magnitude / Mathf.Sin(cameraView.fieldOfView * Mathf.Deg2Rad * .5f) * 1.12f;
            var direction = side ? new Vector3(1f, .16f, .10f).normalized : new Vector3(.42f, .20f, 1f).normalized;
            cameraView.transform.position = bounds.center + direction * distance; cameraView.transform.LookAt(bounds.center);
        }
    }
}
