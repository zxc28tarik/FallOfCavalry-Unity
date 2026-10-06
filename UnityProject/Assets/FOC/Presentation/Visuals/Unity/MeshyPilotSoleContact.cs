#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FOC.Presentation.Visuals
{
    /// <summary>Isolated, measured penetration-only presentation correction.
    /// Never grounds an airborne foot, translates gameplay or edits a clip/rig.
    /// Horizontal support is a separate, bounded review-only request.</summary>
    public sealed class MeshyPilotSoleContact
    {
        private sealed class Foot
        {
            public Transform upper=null!,lower=null!,ankle=null!;
            public int[] indices=Array.Empty<int>();
            public int[] heel=Array.Empty<int>(),toe=Array.Empty<int>();
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
                    var indices=candidates.Keys.Where(i=>candidates[i].y<=min+.3f*(max-min)).ToArray();
                    var forward=Vector3.ProjectOnPlane(toe.position-ankle.position,Vector3.up).normalized;
                    var projections=indices.ToDictionary(i=>i,i=>Vector3.Dot(candidates[i]-ankle.position,forward));
                    var near=projections.Values.Min();var far=projections.Values.Max();
                    return new Foot{upper=target.GetBoneTransform(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg),
                        lower=target.GetBoneTransform(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg),ankle=target.GetBoneTransform(footBone),
                        indices=indices,heel=indices.Where(i=>projections[i]<=near+.3f*(far-near)).ToArray(),
                        toe=indices.Where(i=>projections[i]>=far-.3f*(far-near)).ToArray()};
                }
            }
            finally{if(Application.isPlaying)UnityEngine.Object.Destroy(reference);else UnityEngine.Object.DestroyImmediate(reference);}
        }
        public MeshyHasanPilotPlayer.LocomotionContactFrame MeasureFrame(float phase,float groundY)
        {
            UpdateMatrices();
            Vector3 Center(int[] indices)=>indices.Aggregate(Vector3.zero,(sum,index)=>sum+Point(index))/indices.Length;
            float Minimum(int[] indices)=>indices.Min(index=>Point(index).y)-groundY;
            return new MeshyHasanPilotPlayer.LocomotionContactFrame{phase=phase,
                leftHeel=Minimum(feet[0].heel),leftToe=Minimum(feet[0].toe),rightHeel=Minimum(feet[1].heel),rightToe=Minimum(feet[1].toe),
                leftHeelCenter=Center(feet[0].heel),leftToeCenter=Center(feet[0].toe),rightHeelCenter=Center(feet[1].heel),rightToeCenter=Center(feet[1].toe)};
        }
        public void ApplyHorizontalSupport(int side,Vector3 offset)
        {
            if(side<0||side>1||float.IsNaN(offset.sqrMagnitude)||float.IsInfinity(offset.sqrMagnitude))throw new ArgumentException("Invalid measured horizontal support.");
            var foot=feet[side];var rotation=foot.ankle.rotation;
            HistoricalArtPoseReview.Limb(foot.upper,foot.lower,foot.ankle,foot.ankle.position+Vector3.ClampMagnitude(Vector3.ProjectOnPlane(offset,Vector3.up),.12f),foot.lower.position-foot.upper.position);
            foot.ankle.rotation=rotation;
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
