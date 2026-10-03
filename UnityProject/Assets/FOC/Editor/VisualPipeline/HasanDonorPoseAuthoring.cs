using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FOC.Presentation.Visuals;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FOC.Editor.Visuals
{
    /// <summary>Exports real skin matrices for offline donor collision sculpting.</summary>
    public static class HasanDonorPoseAuthoring
    {
        [Serializable]private sealed class Pose { public string name; public float[] features; public float[] matrices; }
        [Serializable]private sealed class Samples { public string semantics="bind-to-posed row-major bone matrices; same canonical skeleton"; public Pose[] poses; }
        public static void Export()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(HistoricalArtCandidatePipeline.CharacterRoot+"/"+HasanDonorPipeline.Id+".prefab");
            var actor=Object.Instantiate(prefab);Avatar avatar=null;
            try
            {
                var animator=actor.GetComponent<Animator>();
                avatar=AvatarBuilder.BuildGenericAvatar(actor,"Root");animator.avatar=avatar;animator.Rebind();animator.Update(0);
                var skin=actor.GetComponentsInChildren<SkinnedMeshRenderer>().Single(s=>s.name=="LOD0");
                var output=new List<Pose>();
                void Sample(string name,string motion,float phase)
                {
                    animator.Play(motion,0,phase);animator.Update(0);
                    var matrices=new List<float>();
                    for(var b=0;b<skin.bones.Length;b++)
                    {
                        var m=actor.transform.worldToLocalMatrix*skin.bones[b].localToWorldMatrix*skin.sharedMesh.bindposes[b];
                        for(var row=0;row<4;row++)for(var col=0;col<4;col++)matrices.Add(m[row,col]);
                    }
                    output.Add(new Pose{name=name,features=HasanGarmentPoseCorrectives.CaptureFeatures(actor),matrices=matrices.ToArray()});
                }
                Sample("Neutral","Idle",0);
                foreach(var gait in new[]{"Walk","Run"})
                    foreach(var phase in new[]{0f,.125f,.25f,.375f,.5f,.625f,.75f,.875f})
                        Sample(gait+Mathf.RoundToInt(phase*1000),gait,phase);
                Sample("CrouchHalf","Crouch",.25f);Sample("CrouchFull","Crouch",.5f);
                Sample("Mounted","MountedSeated",.25f);
                var folder=Path.GetFullPath("../TestResults/HasanDonor");Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder,"pose-samples.json"),JsonUtility.ToJson(new Samples{poses=output.ToArray()}));
                Debug.Log("FOC_HASAN_POSE_SAMPLES count="+output.Count+" canonicalBones="+skin.bones.Length);
                output.Clear();
                Sample("Neutral","Idle",0);
                foreach(var gait in new[]{"Walk","Run"})
                    foreach(var phase in new[]{.0625f,.1875f,.3125f,.4375f,.5625f,.6875f,.8125f,.9375f})
                        Sample(gait+Mathf.RoundToInt(phase*10000),gait,phase);
                Sample("CrouchQuarter","Crouch",.125f);Sample("CrouchThreeQuarter","Crouch",.375f);
                Sample("Mounted","MountedSeated",.25f);
                File.WriteAllText(Path.Combine(folder,"pose-validation-samples.json"),JsonUtility.ToJson(new Samples{poses=output.ToArray()}));
                Debug.Log("FOC_HASAN_HELD_OUT_SAMPLES count="+output.Count);
            }
            finally{Object.DestroyImmediate(actor);if(avatar!=null)Object.DestroyImmediate(avatar);}
        }
    }
}
