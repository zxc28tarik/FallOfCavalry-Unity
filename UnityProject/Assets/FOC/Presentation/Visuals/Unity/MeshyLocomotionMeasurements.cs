#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FOC.Presentation.Visuals
{
    public sealed partial class MeshyHasanPilotPlayer
    {
        [Serializable] public sealed class LocomotionContactFrame
        {
            public float phase,leftHeel,leftToe,rightHeel,rightToe;
            public Vector3 leftSupport,rightSupport,rightHand,leftHand;
            public Vector3 leftHeelCenter,leftToeCenter,rightHeelCenter,rightToeCenter;
        }
        [Serializable] public sealed class LocomotionContactMeasurement
        {
            public string clip="",avatar="";
            public bool footIk;
            public bool measuredSoleCleanup;
            public float maximumSoleCorrectionMeters;
            public float groundY,minimumHeelToeClearance,maximumLowestFootClearance,loopSurfaceGap,loopHandGap,rootTravel;
            public float durationSeconds;
            public float diagnosticStrideSpeedMetersPerSecond;
            public string sliding="In-place local support trajectories only. World-space sliding requires reviewed stance windows and external motion speed; no gameplay speed is assumed.";
            public LocomotionContactFrame[] frames=Array.Empty<LocomotionContactFrame>();
        }
        private sealed class SoleRegion
        {
            public int[] heel=Array.Empty<int>(),toe=Array.Empty<int>();
        }
        private LocomotionContactMeasurement MeasureLocomotion(Actor actor)
        {
            var reference=Instantiate(characterPrefab);reference.SetActive(false);
            var bake=new Mesh();
            try
            {
                var refAnimator=reference.GetComponent<Animator>();refAnimator.enabled=false;
                RestoreSourceBindPose(characterPrefab,reference);
                var refSkin=reference.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s=>s.sharedMesh.triangles.Length/3==9586);
                SoleRegion Region(bool left)
                {
                    var name=left?"Left":"Right";
                    var footName=refAnimator.avatar.humanDescription.human.Single(h=>h.humanName==name+"Foot").boneName;
                    var toeName=refAnimator.avatar.humanDescription.human.Single(h=>h.humanName==name+"Toes").boneName;
                    var foot=reference.GetComponentsInChildren<Transform>(true).Single(t=>t.name==footName);
                    var toe=reference.GetComponentsInChildren<Transform>(true).Single(t=>t.name==toeName);
                    var bones=new HashSet<int>(Enumerable.Range(0,refSkin.bones.Length).Where(i=>refSkin.bones[i]==foot||refSkin.bones[i]==toe));
                    var vertices=refSkin.sharedMesh.vertices;var weights=refSkin.sharedMesh.boneWeights;
                    var points=new Dictionary<int,Vector3>();
                    for(var i=0;i<vertices.Length;i++)
                    {
                        var w=weights[i];var influence=(bones.Contains(w.boneIndex0)?w.weight0:0)+(bones.Contains(w.boneIndex1)?w.weight1:0)+(bones.Contains(w.boneIndex2)?w.weight2:0)+(bones.Contains(w.boneIndex3)?w.weight3:0);
                        var p=refSkin.transform.TransformPoint(vertices[i]);
                        if(influence>=.5f&&p.y<=foot.position.y+.02f)points.Add(i,p);
                    }
                    if(points.Count<12)throw new InvalidOperationException("Missing weighted heel/toe surface.");
                    var min=points.Values.Min(v=>v.y);var max=points.Values.Max(v=>v.y);
                    var sole=points.Keys.Where(i=>points[i].y<=min+.3f*(max-min)).ToArray();
                    var forward=Vector3.ProjectOnPlane(toe.position-foot.position,Vector3.up).normalized;
                    var projection=sole.ToDictionary(i=>i,i=>Vector3.Dot(points[i]-foot.position,forward));
                    var near=projection.Values.Min();var far=projection.Values.Max();
                    return new SoleRegion{heel=sole.Where(i=>projection[i]<=near+.3f*(far-near)).ToArray(),toe=sole.Where(i=>projection[i]>=far-.3f*(far-near)).ToArray()};
                }
                var leftRegion=Region(true);var rightRegion=Region(false);
                var skin=actor.view.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s=>s.sharedMesh.triangles.Length/3==9586);
                var frames=new List<LocomotionContactFrame>();Vector3[]? first=null,last=null;
                var root=actor.view.transform.position;var rootTravel=0f;
                for(var i=0;i<=120;i++)
                {
                    var phase=i/120f;SetPhase(actor,phase);skin.BakeMesh(bake);
                    var vertices=bake.vertices.Select(skin.transform.TransformPoint).ToArray();
                    if(vertices.Any(p=>float.IsNaN(p.y)||float.IsInfinity(p.y)))throw new InvalidOperationException("Nonfinite locomotion skin.");
                    if(i==0)first=vertices;if(i==120)last=vertices;
                    Vector3 Lowest(IEnumerable<int> indices)=>indices.Select(v=>vertices[v]).OrderBy(v=>v.y).First();
                    Vector3 Center(int[] indices)=>indices.Aggregate(Vector3.zero,(sum,index)=>sum+vertices[index])/indices.Length;
                    frames.Add(new LocomotionContactFrame{phase=phase,leftHeel=Lowest(leftRegion.heel).y-groundHeight,leftToe=Lowest(leftRegion.toe).y-groundHeight,
                        rightHeel=Lowest(rightRegion.heel).y-groundHeight,rightToe=Lowest(rightRegion.toe).y-groundHeight,
                        leftSupport=Lowest(leftRegion.heel.Concat(leftRegion.toe)),rightSupport=Lowest(rightRegion.heel.Concat(rightRegion.toe)),
                        leftHeelCenter=Center(leftRegion.heel),leftToeCenter=Center(leftRegion.toe),rightHeelCenter=Center(rightRegion.heel),rightToeCenter=Center(rightRegion.toe),
                        rightHand=actor.animator.GetBoneTransform(HumanBodyBones.RightHand).position,leftHand=actor.animator.GetBoneTransform(HumanBodyBones.LeftHand).position});
                    rootTravel=Mathf.Max(rootTravel,Vector3.Distance(root,actor.view.transform.position));
                }
                return new LocomotionContactMeasurement{clip=actor.clip!.name,avatar=actor.animator.avatar.name,durationSeconds=actor.clip.length,footIk=actor.playable.GetApplyFootIK(),groundY=groundHeight,
                    measuredSoleCleanup=actor.soleContact!=null,maximumSoleCorrectionMeters=actor.soleContact?.MaximumCorrectionMeters??0f,
                    diagnosticStrideSpeedMetersPerSecond=actor.soleContact?.DiagnosticStrideSpeedMetersPerSecond??0f,
                    minimumHeelToeClearance=frames.Min(f=>Mathf.Min(f.leftHeel,f.leftToe,f.rightHeel,f.rightToe)),
                    maximumLowestFootClearance=frames.Max(f=>Mathf.Min(f.leftHeel,f.leftToe,f.rightHeel,f.rightToe)),
                    loopSurfaceGap=Enumerable.Range(0,first!.Length).Max(i=>Vector3.Distance(first[i],last![i])),
                    loopHandGap=Mathf.Max(Vector3.Distance(frames[0].leftHand,frames[120].leftHand),Vector3.Distance(frames[0].rightHand,frames[120].rightHand)),
                    rootTravel=rootTravel,frames=frames.ToArray()};
            }
            finally{Destroy(reference);Destroy(bake);}
        }
    }
}
