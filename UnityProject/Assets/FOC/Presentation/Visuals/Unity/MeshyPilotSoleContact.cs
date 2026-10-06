#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FOC.Presentation.Visuals
{
    /// <summary>Isolated, measured penetration-only presentation correction.
    /// Never grounds an airborne foot, translates gameplay, edits a clip/rig,
    /// or claims to solve horizontal stance locking.</summary>
    public sealed class MeshyPilotSoleContact
    {
        private sealed class Foot
        {
            public Transform upper=null!,lower=null!,ankle=null!;
            public int[] indices=Array.Empty<int>();
        }
        private readonly SkinnedMeshRenderer skin;
        private readonly Vector3[] vertices;
        private readonly BoneWeight[] weights;
        private readonly Matrix4x4[] bindposes,matrices;
        private readonly Transform[] bones;
        private readonly Foot[] feet;
        public float MaximumCorrectionMeters { get; private set; }
        public MeshyPilotSoleContact(GameObject sourcePrefab,Animator target)
        {
            skin=target.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s=>s.sharedMesh.triangles.Length/3==9586);
            vertices=skin.sharedMesh.vertices;weights=skin.sharedMesh.boneWeights;bindposes=skin.sharedMesh.bindposes;
            bones=skin.bones;matrices=new Matrix4x4[bones.Length];
            var reference=UnityEngine.Object.Instantiate(sourcePrefab);reference.SetActive(false);
            try
            {
                var animator=reference.GetComponent<Animator>();animator.enabled=false;
                MeshyHasanPilotPlayer.RestoreSourceBindPose(sourcePrefab,reference);
                var source=reference.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s=>s.sharedMesh.triangles.Length/3==9586);
                feet=new[]{Build(true),Build(false)};
                Foot Build(bool left)
                {
                    var footBone=left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot;
                    var toeBone=left?HumanBodyBones.LeftToes:HumanBodyBones.RightToes;
                    var ankle=animator.GetBoneTransform(footBone);var toe=animator.GetBoneTransform(toeBone);
                    var boneIndices=new HashSet<int>(Enumerable.Range(0,source.bones.Length).Where(i=>source.bones[i]==ankle||source.bones[i]==toe));
                    var candidates=new Dictionary<int,Vector3>();
                    for(var i=0;i<vertices.Length;i++)
                    {
                        var w=weights[i];var influence=(boneIndices.Contains(w.boneIndex0)?w.weight0:0)+(boneIndices.Contains(w.boneIndex1)?w.weight1:0)+(boneIndices.Contains(w.boneIndex2)?w.weight2:0)+(boneIndices.Contains(w.boneIndex3)?w.weight3:0);
                        var p=source.transform.TransformPoint(vertices[i]);
                        if(influence>=.5f&&p.y<=ankle.position.y+.02f)candidates.Add(i,p);
                    }
                    if(candidates.Count<12)throw new InvalidOperationException("Missing weighted boot sole.");
                    var min=candidates.Values.Min(p=>p.y);var max=candidates.Values.Max(p=>p.y);
                    return new Foot{upper=target.GetBoneTransform(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg),
                        lower=target.GetBoneTransform(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg),ankle=target.GetBoneTransform(footBone),
                        indices=candidates.Keys.Where(i=>candidates[i].y<=min+.3f*(max-min)).ToArray()};
                }
            }
            finally{if(Application.isPlaying)UnityEngine.Object.Destroy(reference);else UnityEngine.Object.DestroyImmediate(reference);}
        }
        public void Apply(float groundY)
        {
            if(float.IsNaN(groundY)||float.IsInfinity(groundY))throw new ArgumentException("Nonfinite presentation floor.");
            for(var iteration=0;iteration<4;iteration++)
            {
                UpdateMatrices();var changed=false;
                foreach(var foot in feet)
                {
                    var minimum=float.PositiveInfinity;
                    foreach(var index in foot.indices)minimum=Mathf.Min(minimum,Point(index).y);
                    if(float.IsNaN(minimum)||float.IsInfinity(minimum))throw new InvalidOperationException("Nonfinite sole contact.");
                    if(minimum>=groundY-.001f)continue;
                    var correction=groundY-minimum+.0005f;
                    if(correction>.10f)throw new InvalidOperationException("Boot correction exceeds the isolated 10cm limit.");
                    MaximumCorrectionMeters=Mathf.Max(MaximumCorrectionMeters,correction);
                    var rotation=foot.ankle.rotation;
                    // Preserve the evaluated knee bend side and ankle roll.
                    // Only a measured penetrating sole requests a vertical lift.
                    HistoricalArtPoseReview.Limb(foot.upper,foot.lower,foot.ankle,foot.ankle.position+Vector3.up*correction,foot.lower.position-foot.upper.position);
                    foot.ankle.rotation=rotation;changed=true;
                }
                if(!changed)break;
            }
        }
        private void UpdateMatrices(){for(var i=0;i<bones.Length;i++)matrices[i]=bones[i].localToWorldMatrix*bindposes[i];}
        private Vector3 Point(int index)
        {
            var w=weights[index];var v=vertices[index];
            return matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1+
                matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
        }
    }
}
