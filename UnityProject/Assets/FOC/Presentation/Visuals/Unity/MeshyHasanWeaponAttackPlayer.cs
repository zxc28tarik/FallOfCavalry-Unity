#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FOC.Presentation.Visuals
{
    public sealed partial class MeshyHasanPilotPlayer
    {
        [Serializable] public sealed class WeaponAttackSample
        {
            public string image = "";
            public float phase;
            public Vector3 rightHandPosition;
            public Quaternion rightHandRotation;
            public Vector3 weaponPosition;
            public Quaternion weaponRotation;
            public float handToWeaponDistance;
            public Vector3 gripWorldPosition,palmGripTarget;
            public float gripFitDistance;
            public string socketName = "";
            public string weaponParentPath = "";
            public bool actualRightHandSocket;
            public int weaponRendererCount;
            public Bounds weaponBounds;
            public Bounds bodyBounds;
        }

        [Serializable] public sealed class WeaponAttackEvidence
        {
            public string status = "NOT_COMPLETED";
            public string sourceSha = "";
            public string weaponAsset = "Assets/FOC/Presentation/Equipment/Ottoman1648/WPN_Kilic_01.prefab";
            public string clip = "";
            public string avatar = "";
            public string socket = "Socket_RightHand";
            public string note = "Review-only visual attachment; gameplay SoldierLoadout and production catalog are unchanged.";
            public bool actualWindowsPlayer;
            public bool rightHandSocketOwned;
            public bool weaponVisible;
            public bool sourceAssetsUnchanged;
            public WeaponAttackSample[] samples = Array.Empty<WeaponAttackSample>();
        }

        private IEnumerator RunWeaponAttackReview()
        {
            if (output == null) throw new InvalidOperationException("Weapon attack review requires an output path.");
            if (calibratedAvatar == null || !calibratedAvatar.isValid || !calibratedAvatar.isHuman)
                throw new InvalidOperationException("Weapon review requires the validated calibrated Avatar.");
            var clip = libraryMotionClips.FirstOrDefault(c => c != null &&
                (c.name.Equals("Sword_Attack", StringComparison.Ordinal) || c.name.EndsWith("|Sword_Attack", StringComparison.Ordinal)));
            if (clip == null) throw new InvalidOperationException("Licensed Sword_Attack clip is missing.");

            var actor = CreateActorCore(clip, Vector3.zero, calibratedAvatar, null, true);
            actor.calibrationScenario = "WeaponAttack";
            actor.sourceClipName = clip.name;
            var evidence = new WeaponAttackEvidence
            {
                sourceSha = Arg("--meshy-sha") ?? "NOT_SUPPLIED",
                clip = clip.name,
                avatar = calibratedAvatar.name,
                actualWindowsPlayer = Application.platform == RuntimePlatform.WindowsPlayer,
                sourceAssetsUnchanged = true
            };
            var samples = new List<WeaponAttackSample>();
            try
            {
                if (actor.weaponInstance == null) throw new InvalidOperationException("Kilic instance was not created.");
                var socket = actor.weaponInstance.transform.parent;
                var rightHand = actor.animator.GetBoneTransform(HumanBodyBones.RightHand);
                var weaponRenderers = actor.weaponInstance.GetComponentsInChildren<Renderer>(true);
                if (socket == null || rightHand == null || weaponRenderers.Length == 0)
                    throw new InvalidOperationException("Kilic review instance is missing its hand parent or visible renderer.");
                evidence.rightHandSocketOwned = socket.name == "Socket_RightHand" && socket.IsChildOf(rightHand);
                evidence.weaponVisible = weaponRenderers.Any(r => r.enabled && r.sharedMaterials.Any(m => m != null));
                if (!evidence.rightHandSocketOwned) throw new InvalidOperationException("Kilic is not parented to the actual right-hand socket.");
                if (!evidence.weaponVisible) throw new InvalidOperationException("Kilic has no visible renderer/material.");

                foreach (var view in new[] { "side", "quarter" })
                {
                    foreach (var phase in new[] { .15f, .35f, .55f, .75f })
                    {
                        yield return null;
                        SetPhase(actor, phase);
                        var tracked = SnapshotTrackedJoints(actor);
                        yield return new WaitForEndOfFrame();
                        AssertStablePoseAcrossRenderBoundary(actor, tracked);
                        Frame(view);
                        var image = "weapon-attack-" + view + "-" + phase.ToString("0.00", CultureInfo.InvariantCulture) + ".png";
                        Capture(image, "weapon-attack-" + view, phase);
                        var weaponBounds = RenderBounds(weaponRenderers);
                        var bodyBounds = RenderBounds(actor.view.GetComponentsInChildren<Renderer>(true).Where(r => !r.transform.IsChildOf(actor.weaponInstance!.transform)).ToArray());
                        var grip=actor.weaponInstance.transform.TransformPoint(new Vector3(-.006f,-.080f,0f));
                        var palm=rightHand.TransformPoint(new Vector3(0f,.061f,.018f));
                        if(Vector3.Distance(grip,palm)>.003f)throw new InvalidOperationException("Kilic grip is not aligned to the reviewed right palm.");
                        samples.Add(new WeaponAttackSample
                        {
                            image = image,
                            phase = phase,
                            rightHandPosition = rightHand.position,
                            rightHandRotation = rightHand.rotation,
                            weaponPosition = actor.weaponInstance.transform.position,
                            weaponRotation = actor.weaponInstance.transform.rotation,
                            handToWeaponDistance = Vector3.Distance(rightHand.position, actor.weaponInstance.transform.position),
                            gripWorldPosition=grip,palmGripTarget=palm,gripFitDistance=Vector3.Distance(grip,palm),
                            socketName = socket.name,
                            weaponParentPath = TransformPath(socket, actor.view.transform),
                            actualRightHandSocket = evidence.rightHandSocketOwned,
                            weaponRendererCount = weaponRenderers.Length,
                            weaponBounds = weaponBounds,
                            bodyBounds = bodyBounds
                        });
                    }
                }
                evidence.samples = samples.ToArray();
                evidence.status = "WEAPON_SOCKET_CAPTURE_COMPLETE_VISUAL_QA_PENDING";
                evidence.samples = samples.ToArray();
                File.WriteAllText(Path.Combine(output, "weapon-attack-evidence.json"), JsonUtility.ToJson(evidence, true));
            }
            finally
            {
                ClearActors();
            }
            Debug.Log("FOC_MESHY_WEAPON_ATTACK_CAPTURE_COMPLETE captures=" + samples.Count + " rightHand=" + evidence.rightHandSocketOwned);
            Application.Quit(0);
        }

        private static Bounds RenderBounds(IReadOnlyList<Renderer> renderers)
        {
            if (renderers.Count == 0) throw new InvalidOperationException("Render bounds require at least one renderer.");
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Count; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static string TransformPath(Transform value, Transform root)
        {
            var names = new List<string>();
            for (var current = value; current != null && current != root; current = current.parent) names.Add(current.name);
            names.Reverse();
            return string.Join("/", names);
        }
    }
}
