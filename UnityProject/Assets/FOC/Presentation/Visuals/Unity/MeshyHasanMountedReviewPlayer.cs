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
            public string status = "NOT_COMPLETED", sourceSha = "", worktreeState = "", unityVersion = "", avatar = "";
            public string horse = "", harness = "", riderSocket = "Socket_Rider";
            public string[] checks = { "pelvis/saddle", "knees", "boots/stirrups", "hands/reins", "coat/horse clipping", "torso posture" };
            public string[] captures = Array.Empty<string>();
            public string posePolicy = "Seat and stirrup anchors constrain presentation-only limbs after actual Mecanim playback. Retained saddle follows MountSpine; obsolete rigid reins are removed from disposable mesh copies and replaced by hand-to-bit connections following MountHead. Source horse, character and harness assets remain unchanged. Sitting clip alone is not riding acceptance.";
            public int visibleSaddleInstances;
            public int removedRigidReinTriangles;
            public float maximumReinEndpointError;
            public MountedFitSample[] samples = Array.Empty<MountedFitSample>();
        }
        [Serializable] private sealed class MountedFitSample
        {
            public string state=""; public float phase,pelvisSeatDistance,pelvisTargetError,leftAnkleError,rightAnkleError,leftHandError,rightHandError;
            public float leftReinEndpointError,rightReinEndpointError;
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
            public void Dispose() { if (graph.IsValid()) graph.Destroy(); if (instance != null) { instance.SetActive(false); Destroy(instance); } }
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
            MeshyMountedTackBinding? tack = null;
            var removedReinTriangles = 0;
            var maximumReinError = 0f;
            var visibleSaddleInstances = 0;
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
                tack?.Dispose();
                horse.Dispose();
                horse = new HorseMotion(horsePrefab, state.Item2); horse.instance.transform.SetParent(horseRoot.transform, false);
                harness = Instantiate(harnessPrefab, horse.instance.transform, false); harness.name = "HAR_SipahiHarness_01_REVIEW_" + state.Item1;
                tack = new MeshyMountedTackBinding(horse.instance,harness);
                removedReinTriangles = tack.RemovedRigidReinTriangles;
                socket = horse.instance.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Socket_Rider");
                actor.view.transform.SetParent(socket, false); actor.view.transform.localPosition = Vector3.zero; actor.view.transform.localRotation = Quaternion.identity;
                for (var phaseIndex = 0; phaseIndex < (state.Item1 == "idle" ? 1 : 3); phaseIndex++)
                {
                    var phase = state.Item1 == "idle" ? .5f : phaseIndex / 3f;
                    SetPhase(actor, phase); horse.SetPhase(phase);
                    var hipRise = Arg("--meshy-mounted-hip-rise")==null ? .10f : float.Parse(Arg("--meshy-mounted-hip-rise")!,CultureInfo.InvariantCulture);
                    if(hipRise<0f||hipRise>.2f)throw new InvalidOperationException("Mounted review hip rise outside measured fitting range.");
                    var fit = FitMountedPose(actor,socket,leftFootRotation,rightFootRotation,state.Item1,phase,hipRise);
                    if(fit.pelvisTargetError>.001f||fit.leftAnkleError>.01f||fit.rightAnkleError>.01f)
                        throw new InvalidOperationException("Mounted seat/stirrup fit exceeded the 1cm anchor gate.");
                    tack.Update(actor.animator.GetBoneTransform(HumanBodyBones.LeftHand),actor.animator.GetBoneTransform(HumanBodyBones.RightHand));
                    fit.leftReinEndpointError=tack.LeftReinEndpointError;fit.rightReinEndpointError=tack.RightReinEndpointError;
                    maximumReinError=Mathf.Max(maximumReinError,Mathf.Max(fit.leftReinEndpointError,fit.rightReinEndpointError));
                    fitSamples.Add(fit);
                    var sampled=SnapshotTrackedJoints(actor);
                    yield return null; yield return new WaitForEndOfFrame();
                    AssertStablePoseAcrossRenderBoundary(actor,sampled);
                    visibleSaddleInstances=horseRoot.GetComponentsInChildren<Transform>()
                        .Count(t=>t.gameObject.activeInHierarchy&&t.name.StartsWith("HAR_SipahiHarness_01_REVIEW",StringComparison.Ordinal));
                    if(visibleSaddleInstances!=1)throw new InvalidOperationException("Mounted review must have exactly one active saddle instance.");
                    var file = "mounted-" + state.Item1 + "-" + phase.ToString("0.00", CultureInfo.InvariantCulture) + ".png";
                    FrameMounted(horseRoot, actor, socket, false); Capture(file, "mounted", phase); capturesMounted.Add(file);
                    if (state.Item1 == "idle")
                    {
                        var sideFile = "mounted-idle-side-" + phase.ToString("0.00", CultureInfo.InvariantCulture) + ".png";
                        FrameMounted(horseRoot, actor, socket, true); Capture(sideFile, "mounted-side", phase); capturesMounted.Add(sideFile);
                    }
                    if(Arg("--meshy-camera-aware")=="true")
                    {
                        cameraView.fieldOfView=60;cameraView.transform.position=new Vector3(0,18,-28);
                        cameraView.transform.rotation=Quaternion.Euler(28,0,0);
                        var tacticalFile="normal-tactical-mounted-"+state.Item1+(state.Item1=="idle"||phaseIndex==0?"":"-"+phase.ToString("0.00",CultureInfo.InvariantCulture))+".png";
                        Capture(tacticalFile,"NORMAL_TACTICAL_PRIMARY",phase);capturesMounted.Add(tacticalFile);
                        cameraView.fieldOfView=34;
                        if(state.Item1=="idle")
                        {
                            FrameMounted(horseRoot,actor,socket,true);Capture("mounted-side-inspection.png","CHARACTER_INSPECTION_SECONDARY",phase);capturesMounted.Add("mounted-side-inspection.png");
                            FrameMounted(horseRoot,actor,socket,false);Capture("mounted-three-quarter-inspection.png","CHARACTER_INSPECTION_SECONDARY",phase);capturesMounted.Add("mounted-three-quarter-inspection.png");
                            cameraView.transform.position=socket.position+socket.TransformDirection(new Vector3(1f,.45f,.3f))*1.6f;
                            cameraView.transform.LookAt(socket.position);
                            Capture("diagnostic-saddle-closeup.png","DEBUG_ONLY_DIAGNOSTIC",phase);capturesMounted.Add("diagnostic-saddle-closeup.png");
                        }
                    }
                }
            }
            ClearActors(); tack?.Dispose(); horse.Dispose(); Destroy(horseRoot);
            var evidence = new MountedEvidence { status = "WINDOWS_MOUNTED_CAPTURE_COMPLETE_VISUAL_FIT_QA_REQUIRED", sourceSha = Arg("--meshy-sha") ?? "NOT_SUPPLIED", worktreeState=Arg("--meshy-worktree")??"NOT_SUPPLIED", unityVersion = Application.unityVersion, avatar = calibratedAvatar.name, horse = horsePrefab.name, harness = harnessPrefab.name, captures = capturesMounted.ToArray(),samples=fitSamples.ToArray(),visibleSaddleInstances=visibleSaddleInstances,removedRigidReinTriangles=removedReinTriangles,maximumReinEndpointError=maximumReinError };
            File.WriteAllText(Path.Combine(output, "mounted-evidence.json"), JsonUtility.ToJson(evidence, true));
            Application.Quit(0);
        }

        private static MountedFitSample FitMountedPose(Actor actor,Transform seat,Quaternion leftFootRotation,Quaternion rightFootRotation,string state,float phase,float hipRise)
        {
            Transform B(HumanBodyBones bone)=>actor.animator.GetBoneTransform(bone)??throw new InvalidOperationException("Mounted mapping missing: "+bone);
            var pelvis=B(HumanBodyBones.Hips);
            // Hips is an internal joint, not the skin's seated contact point.
            // The old zero-distance constraint buried the haunches and put the
            // saddle rims through the belt. Keep an explicit, reviewable offset
            // from the retained seat while the ankles remain tied to stirrups.
            var pelvisTarget=seat.TransformPoint(Vector3.up*hipRise);
            actor.view.transform.position+=pelvisTarget-pelvis.position;
            var result=new MountedFitSample{state=state,phase=phase,pelvis=pelvis.position,pelvisSeatDistance=Vector3.Distance(pelvis.position,seat.position),pelvisTargetError=Vector3.Distance(pelvis.position,pelvisTarget)};
            foreach(var left in new[]{true,false})
            {
                var sign=left?-1f:1f;
                var upper=B(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
                var lower=B(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
                var foot=B(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                // Authoring ledger: iron sole (x=+/-.46,y=1.01,z=-.22),
                // seat=(0,1.81,-.42). Meshy ankle is 0.1335m above its sole.
                var ankle=seat.TransformPoint(new Vector3(sign*.46f,-.6665f+MeshyMountedTackBinding.StirrupRise,.13f));
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
