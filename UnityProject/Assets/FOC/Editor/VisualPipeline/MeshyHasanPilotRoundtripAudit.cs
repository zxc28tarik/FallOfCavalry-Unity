#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object=UnityEngine.Object;

namespace FOC.Editor.Visuals
{
    /// <summary>Read-only loss localization, not a retarget/choreography repair.</summary>
    public static class MeshyHasanPilotRoundtripAudit
    {
        public static string ReportPath=>Path.GetFullPath("../TestResults/MeshyPilot/motion-roundtrip.json");
        private static readonly HumanBodyBones[] Bones={HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.Head,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot};
        [Serializable]public sealed class Delta
        {
            public float maximumPosition;public float maximumRotation;public string worstPositionJoint="";public string worstRotationJoint="";
            public float leftHandPosition;public float rightHandPosition;public float leftFootPosition;public float rightFootPosition;
        }
        [Serializable]public sealed class Sample
        {
            public string motion="";public float phase;public Vector3 extractedBody;public Quaternion extractedBodyRotation;public string[] outsideNominalMuscles=Array.Empty<string>();
            public Delta sourceToDirectPose=new Delta();public Delta sourceToConvertedClip=new Delta();public Delta directPoseToConvertedClip=new Delta();public Delta meshyDirectPoseToConvertedClip=new Delta();
            public Vector3[] sourcePositions=Array.Empty<Vector3>();public Vector3[] directPositions=Array.Empty<Vector3>();public Vector3[] convertedPositions=Array.Empty<Vector3>();public Vector3[] meshyDirectPositions=Array.Empty<Vector3>();public Vector3[] meshyConvertedPositions=Array.Empty<Vector3>();
        }
        [Serializable]public sealed class Report
        {
            public string status="DIAGNOSTIC_NOT_COMPLETED";public string error="";public string unityVersion="";public string utc="";
            public string interpretation="Same-avatar source->direct isolates HumanPose extraction/reconstruction. Direct->clip on same Avatar isolates curve-format/playback loss. Meshy direct->clip isolates target clip execution. No offset/clamp/rig/asset edits are performed.";
            public string[] jointOrder=Bones.Select(b=>b.ToString()).ToArray();public Sample[] samples=Array.Empty<Sample>();
        }
        public static void Run()
        {
            try{Audit();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static Report Audit()
        {
            var report=new Report{unityVersion=Application.unityVersion,utc=DateTime.UtcNow.ToString("O")};
            try
            {
                var donor=AssetDatabase.LoadAssetAtPath<GameObject>(MeshyHasanPilotMotionAdaptation.DonorPrefabPath);
                var meshy=AssetDatabase.LoadAssetAtPath<GameObject>(MeshyHasanPilotPipeline.PrefabPath);
                if(donor==null||meshy==null)throw new InvalidOperationException("Roundtrip prefabs missing.");
                var samples=new List<Sample>();
                foreach(var motion in new[]{"Idle","Turn","OneHandedAttack","ArmRaise","Crouch","MountedSeated"})
                    AuditMotion(donor,meshy,motion,samples);
                report.samples=samples.ToArray();report.status="COMPLETED_DIAGNOSTIC_NOT_VISUAL_ACCEPTANCE";
                foreach(var group in samples.GroupBy(s=>s.motion))Debug.Log("FOC_ROUNDTRIP "+group.Key+" sourceToDirectMax="+group.Max(s=>s.sourceToDirectPose.maximumPosition)+" directToClipMax="+group.Max(s=>s.directPoseToConvertedClip.maximumPosition)+" sourceToClipMax="+group.Max(s=>s.sourceToConvertedClip.maximumPosition)+" meshyDirectToClipMax="+group.Max(s=>s.meshyDirectPoseToConvertedClip.maximumPosition));
                return report;
            }
            catch(Exception e){report.error=e.ToString();throw;}
            finally{Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));}
        }
        private static void AuditMotion(GameObject donor,GameObject meshy,string motion,List<Sample> result)
        {
            var sourceClip=AssetDatabase.LoadAssetAtPath<AnimationClip>(MeshyHasanPilotMotionAdaptation.SourceClipPath(motion));
            var convertedClip=AssetDatabase.LoadAssetAtPath<AnimationClip>(MeshyHasanPilotMotionAdaptation.OutputClipPath(motion));
            if(sourceClip==null||convertedClip==null)throw new InvalidOperationException("Required roundtrip clip missing: "+motion);
            GameObject? source=null,direct=null,played=null,meshyDirect=null,meshyPlayed=null;
            Avatar? generic=null;var donorGraph=default(PlayableGraph);var meshyGraph=default(PlayableGraph);
            try
            {
                source=Clone(donor);direct=Clone(donor);played=Clone(donor);meshyDirect=Clone(meshy);meshyPlayed=Clone(meshy);
                var sourceAnimator=source.GetComponent<Animator>();var sourceAvatar=sourceAnimator.avatar;
                var sourceBones=Map(sourceAnimator);var directBones=Map(direct.GetComponent<Animator>());var playedBones=Map(played.GetComponent<Animator>());
                var meshyDirectBones=Map(meshyDirect.GetComponent<Animator>());var meshyPlayedBones=Map(meshyPlayed.GetComponent<Animator>());
                sourceAnimator.enabled=false;sourceAnimator.runtimeAnimatorController=null;generic=AvatarBuilder.BuildGenericAvatar(source,"Root");sourceAnimator.avatar=generic;sourceAnimator.Rebind();
                var all=source.GetComponentsInChildren<Transform>(true);var positions=all.Select(t=>t.localPosition).ToArray();var rotations=all.Select(t=>t.localRotation).ToArray();var scales=all.Select(t=>t.localScale).ToArray();
                var donorPlayable=Playback(played,convertedClip,out donorGraph);var meshyPlayable=Playback(meshyPlayed,convertedClip,out meshyGraph);
                using(var reader=new HumanPoseHandler(sourceAvatar,source.transform))
                using(var writer=new HumanPoseHandler(sourceAvatar,direct.transform))
                using(var meshyWriter=new HumanPoseHandler(meshyDirect.GetComponent<Animator>().avatar,meshyDirect.transform))
                {
                    foreach(var phase in new[]{0f,.125f,.25f,.375f,.5f,.625f,.75f,.875f})
                    {
                        for(var i=0;i<all.Length;i++){all[i].localPosition=positions[i];all[i].localRotation=rotations[i];all[i].localScale=scales[i];}
                        sourceClip.SampleAnimation(source,sourceClip.length*phase);
                        var sourcePositions=sourceBones.Select(t=>t.position).ToArray();var sourceRotations=sourceBones.Select(t=>t.rotation).ToArray();
                        var pose=new HumanPose{muscles=new float[HumanTrait.MuscleCount]};reader.GetHumanPose(ref pose);
                        writer.SetHumanPose(ref pose);meshyWriter.SetHumanPose(ref pose);
                        donorPlayable.SetTime(phase*convertedClip.length);donorGraph.Evaluate(0);
                        meshyPlayable.SetTime(phase*convertedClip.length);meshyGraph.Evaluate(0);
                        result.Add(new Sample
                        {
                            motion=motion,phase=phase,extractedBody=pose.bodyPosition,extractedBodyRotation=pose.bodyRotation,
                            outsideNominalMuscles=pose.muscles.Select((value,index)=>new{value,index}).Where(v=>Mathf.Abs(v.value)>1.0001f).Select(v=>HumanTrait.MuscleName[v.index]+"="+v.value.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).ToArray(),
                            sourceToDirectPose=Compare(sourcePositions,sourceRotations,directBones),sourceToConvertedClip=Compare(sourcePositions,sourceRotations,playedBones),
                            directPoseToConvertedClip=Compare(directBones.Select(t=>t.position).ToArray(),directBones.Select(t=>t.rotation).ToArray(),playedBones),
                            meshyDirectPoseToConvertedClip=Compare(meshyDirectBones.Select(t=>t.position).ToArray(),meshyDirectBones.Select(t=>t.rotation).ToArray(),meshyPlayedBones),
                            sourcePositions=sourcePositions,directPositions=directBones.Select(t=>t.position).ToArray(),convertedPositions=playedBones.Select(t=>t.position).ToArray(),meshyDirectPositions=meshyDirectBones.Select(t=>t.position).ToArray(),meshyConvertedPositions=meshyPlayedBones.Select(t=>t.position).ToArray()
                        });
                    }
                }
            }
            finally
            {
                if(donorGraph.IsValid())donorGraph.Destroy();if(meshyGraph.IsValid())meshyGraph.Destroy();
                if(source!=null)Object.DestroyImmediate(source);if(direct!=null)Object.DestroyImmediate(direct);if(played!=null)Object.DestroyImmediate(played);if(meshyDirect!=null)Object.DestroyImmediate(meshyDirect);if(meshyPlayed!=null)Object.DestroyImmediate(meshyPlayed);if(generic!=null)Object.DestroyImmediate(generic);
            }
        }
        private static GameObject Clone(GameObject prefab)
        {
            var clone=Object.Instantiate(prefab);
            try
            {
                clone.name=prefab.name;clone.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                var animator=clone.GetComponent<Animator>();animator.enabled=false;animator.runtimeAnimatorController=null;animator.applyRootMotion=false;return clone;
            }
            catch{Object.DestroyImmediate(clone);throw;}
        }
        private static Transform[] Map(Animator animator)
        {
            if(animator.avatar==null||!animator.avatar.isHuman||!animator.avatar.isValid)throw new InvalidOperationException("Roundtrip requires existing valid Human Avatar.");
            var result=Bones.Select(animator.GetBoneTransform).ToArray();if(result.Any(t=>t==null))throw new InvalidOperationException("Mapped roundtrip bone missing.");return result;
        }
        private static AnimationClipPlayable Playback(GameObject actor,AnimationClip clip,out PlayableGraph graph)
        {
            var animator=actor.GetComponent<Animator>();animator.enabled=true;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            graph=PlayableGraph.Create("Read-only same-Avatar motion roundtrip");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);playable.SetApplyPlayableIK(false);
            AnimationPlayableOutput.Create(graph,"Converted muscle clip",animator).SetSourcePlayable(playable);graph.Play();return playable;
        }
        private static Delta Compare(Vector3[] positions,Quaternion[] rotations,Transform[] actual)
        {
            var delta=new Delta();
            for(var i=0;i<actual.Length;i++)
            {
                var distance=Vector3.Distance(positions[i],actual[i].position);var angle=Quaternion.Angle(rotations[i],actual[i].rotation);
                if(float.IsNaN(distance)||float.IsInfinity(distance)||float.IsNaN(angle)||float.IsInfinity(angle))throw new InvalidOperationException("Nonfinite roundtrip joint.");
                if(distance>delta.maximumPosition){delta.maximumPosition=distance;delta.worstPositionJoint=Bones[i].ToString();}if(angle>delta.maximumRotation){delta.maximumRotation=angle;delta.worstRotationJoint=Bones[i].ToString();}
                if(Bones[i]==HumanBodyBones.LeftHand)delta.leftHandPosition=distance;if(Bones[i]==HumanBodyBones.RightHand)delta.rightHandPosition=distance;if(Bones[i]==HumanBodyBones.LeftFoot)delta.leftFootPosition=distance;if(Bones[i]==HumanBodyBones.RightFoot)delta.rightFootPosition=distance;
            }
            return delta;
        }
    }
}
