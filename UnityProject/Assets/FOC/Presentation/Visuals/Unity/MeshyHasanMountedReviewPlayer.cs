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
            actor.view.transform.localPosition = new Vector3(0f, -.62f, 0f);
            actor.view.transform.localRotation = Quaternion.identity;
            var capturesMounted = new System.Collections.Generic.List<string>();
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
                actor.view.transform.SetParent(socket, false); actor.view.transform.localPosition = new Vector3(0f, -.62f, 0f); actor.view.transform.localRotation = Quaternion.identity;
                for (var phaseIndex = 0; phaseIndex < (state.Item1 == "idle" ? 1 : 3); phaseIndex++)
                {
                    var phase = state.Item1 == "idle" ? .5f : phaseIndex / 3f;
                    SetPhase(actor, .5f); horse.SetPhase(phase); yield return null; yield return new WaitForEndOfFrame();
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
            var evidence = new MountedEvidence { status = "WINDOWS_MOUNTED_CAPTURE_COMPLETE_VISUAL_FIT_QA_REQUIRED", sourceSha = Arg("--meshy-sha") ?? "NOT_SUPPLIED", unityVersion = Application.unityVersion, avatar = calibratedAvatar.name, horse = horsePrefab.name, harness = harnessPrefab.name, captures = capturesMounted.ToArray() };
            File.WriteAllText(Path.Combine(output, "mounted-evidence.json"), JsonUtility.ToJson(evidence, true));
            Application.Quit(0);
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
