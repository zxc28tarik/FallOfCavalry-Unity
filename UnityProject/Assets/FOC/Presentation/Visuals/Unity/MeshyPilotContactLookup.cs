#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FOC.Presentation.Visuals
{
    /// <summary>Bounded, flat-floor pilot correction cache. Samples the actual
    /// chosen Animator once, then applies only six leg rotation deltas at a
    /// measured review stride speed. Not a motion library, terrain solver or
    /// gameplay position source; no production movement activation.</summary>
    public sealed class MeshyPilotContactLookup
    {
        private const int Intervals=240,Capacity=8;
        private static readonly HumanBodyBones[] JointOrder={HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot};
        private readonly struct Key : IEquatable<Key>
        {
            private readonly int prefab,avatar,clip;
            private readonly bool ik;
            private readonly float floorOffset;
            private readonly Vector3 scale;
            private readonly Quaternion rotation;
            public Key(GameObject p,Animator a,AnimationClip c,bool footIk,float offset)
            {prefab=p.GetInstanceID();avatar=a.avatar.GetInstanceID();clip=c.GetInstanceID();ik=footIk;floorOffset=offset;scale=a.transform.lossyScale;rotation=a.transform.rotation;}
            public bool Equals(Key other)=>prefab==other.prefab&&avatar==other.avatar&&clip==other.clip&&ik==other.ik&&floorOffset.Equals(other.floorOffset)&&scale.Equals(other.scale)&&rotation.Equals(other.rotation);
            public override bool Equals(object? other)=>other is Key key&&Equals(key);
            public override int GetHashCode()=>HashCode.Combine(prefab,avatar,clip,ik,floorOffset,scale,rotation);
        }
        private sealed class Entry
        {
            public readonly Quaternion[][] deltas=new Quaternion[Intervals+1][];
            public float maximumCorrection;
            public float strideSpeed;
        }
        private static readonly Dictionary<Key,Entry> Cache=new Dictionary<Key,Entry>();
        private static readonly Queue<Key> Order=new Queue<Key>();
        public static int CachedProfileCount=>Cache.Count;
        private readonly Entry entry;
        private readonly Transform[] joints;
        private readonly Animator animator;
        private readonly float floorOffset,groundY;
        private readonly Vector3 scale;
        private readonly Avatar avatar;
        private readonly Quaternion rotation;
        public float MaximumCorrectionMeters=>entry.maximumCorrection;
        public float DiagnosticStrideSpeedMetersPerSecond=>entry.strideSpeed;
        private MeshyPilotContactLookup(Entry data,Transform[] mapped,Animator target,float offset,float ground)
        {entry=data;joints=mapped;animator=target;floorOffset=offset;groundY=ground;scale=target.transform.lossyScale;avatar=target.avatar;rotation=target.transform.rotation;}
        public static MeshyPilotContactLookup Create(GameObject prefab,Animator target,AnimationClip clip,PlayableGraph graph,AnimationClipPlayable playable,float groundY)
        {
            if(!graph.IsValid()||clip==null||!clip.isHumanMotion||target.avatar==null||!target.avatar.isValid||!target.avatar.isHuman)
                throw new InvalidOperationException("Contact lookup requires the actual valid Humanoid playback path.");
            if(Vector3.Dot(target.transform.up,Vector3.up)<.99999f)throw new InvalidOperationException("Pilot lookup supports an upright flat-floor actor only.");
            var offset=groundY-target.transform.position.y;
            var key=new Key(prefab,target,clip,playable.GetApplyFootIK(),offset);
            var joints=new Transform[JointOrder.Length];
            for(var i=0;i<joints.Length;i++)joints[i]=target.GetBoneTransform(JointOrder[i])??throw new InvalidOperationException("Missing contact joint.");
            if(!Cache.TryGetValue(key,out var entry))
            {
                entry=Build(prefab,target,clip,graph,playable,joints,groundY);
                if(Cache.Count==Capacity)Cache.Remove(Order.Dequeue());
                Cache.Add(key,entry);Order.Enqueue(key);
            }
            return new MeshyPilotContactLookup(entry,joints,target,offset,groundY);
        }
        private static Entry Build(GameObject prefab,Animator target,AnimationClip clip,PlayableGraph graph,AnimationClipPlayable playable,Transform[] joints,float groundY)
        {
            var entry=new Entry();var before=new Quaternion[joints.Length];
            var savedTime=playable.GetTime();var solver=new MeshyPilotSoleContact(prefab,target);
            try
            {
                var motion=new MeshyHasanPilotPlayer.LocomotionContactMeasurement{clip=clip.name,durationSeconds=clip.length,
                    frames=new MeshyHasanPilotPlayer.LocomotionContactFrame[121]};
                for(var sample=0;sample<=120;sample++)
                {
                    playable.SetTime(clip.length*sample/120d);graph.Evaluate(0);solver.Apply(groundY);
                    motion.frames[sample]=solver.MeasureFrame(sample/120f,groundY);
                }
                var stance=MeshyLocomotionStanceAudit.Measure(motion);
                entry.strideSpeed=stance.fittedNativeSpeedMetersPerSecond;
                for(var sample=0;sample<=Intervals;sample++)
                {
                    playable.SetTime(clip.length*sample/(double)Intervals);graph.Evaluate(0);
                    for(var i=0;i<joints.Length;i++)before[i]=joints[i].localRotation;
                    solver.Apply(groundY);
                    var phase=sample/(float)Intervals;
                    var frame=solver.MeasureFrame(phase,groundY);
                    for(var side=0;side<2;side++)
                    {
                        var offset=Vector3.zero;var total=0f;
                        foreach(var window in stance.windows)
                        {
                            if(!window.marker.StartsWith(side==0?"Left":"Right",StringComparison.Ordinal))continue;
                            var unwrapped=phase;
                            if(unwrapped<window.startPhase-.04f)unwrapped+=1;
                            if(unwrapped>window.endPhase+.04f)unwrapped-=1;
                            if(unwrapped<window.startPhase-.04f||unwrapped>window.endPhase+.04f)continue;
                            var heel=window.marker.EndsWith("Heel",StringComparison.Ordinal);
                            var point=side==0?(heel?frame.leftHeelCenter:frame.leftToeCenter):(heel?frame.rightHeelCenter:frame.rightToeCenter);
                            var height=side==0?(heel?frame.leftHeel:frame.leftToe):(heel?frame.rightHeel:frame.rightToe);
                            var edge=Mathf.Min(Mathf.Clamp01((unwrapped-window.startPhase+.04f)/.04f),Mathf.Clamp01((window.endPhase+.04f-unwrapped)/.04f));
                            var weight=Mathf.SmoothStep(0,1,edge)*Mathf.Clamp01((.05f-height)/.035f);
                            offset+=Vector3.ProjectOnPlane(window.worldAnchor-Vector3.forward*entry.strideSpeed*unwrapped*clip.length-point,Vector3.up)*weight;
                            total+=weight;
                        }
                        if(total>0)solver.ApplyHorizontalSupport(side,offset/Mathf.Max(1,total));
                    }
                    solver.Apply(groundY);var deltas=new Quaternion[joints.Length];
                    for(var i=0;i<joints.Length;i++)deltas[i]=joints[i].localRotation*Quaternion.Inverse(before[i]);
                    entry.deltas[sample]=deltas;
                }
                entry.maximumCorrection=solver.MaximumCorrectionMeters;
                return entry;
            }
            finally{playable.SetTime(savedTime);graph.Evaluate(0);}
        }
        public void Apply(float phase)
        {
            if(float.IsNaN(phase)||float.IsInfinity(phase))throw new ArgumentException("Nonfinite contact phase.");
            if(Vector3.Dot(animator.transform.up,Vector3.up)<.99999f)
                throw new InvalidOperationException("Pilot contact lookup cannot be reused on tilted terrain.");
            if(Mathf.Abs((groundY-animator.transform.position.y)-floorOffset)>.00001f)
                throw new InvalidOperationException("Pilot contact lookup floor/root height binding changed.");
            if(animator.avatar!=avatar||(animator.transform.lossyScale-scale).sqrMagnitude>1e-10f||Quaternion.Angle(animator.transform.rotation,rotation)>.001f)
                throw new InvalidOperationException("Pilot contact lookup Avatar/scale/orientation binding changed.");
            var sample=Mathf.Clamp01(phase)*Intervals;var index=Mathf.Min(Intervals-1,Mathf.FloorToInt(sample));var fraction=sample-index;
            for(var i=0;i<joints.Length;i++)
                joints[i].localRotation=Quaternion.Slerp(entry.deltas[index][i],entry.deltas[index+1][i],fraction)*joints[i].localRotation;
        }
    }
}
