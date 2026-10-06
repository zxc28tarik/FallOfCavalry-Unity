#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FOC.Presentation.Visuals
{
    /// <summary>Measured flat-floor review only. Fits an external diagnostic
    /// speed to heel/toe support trajectories, never a campaign speed or clip.
    /// Odd samples are held out of the speed/anchor fit.</summary>
    public static class MeshyLocomotionStanceAudit
    {
        [Serializable] public sealed class Window
        {
            public string marker="";
            public float startPhase,endPhase;
            public int trainingSamples,heldOutSamples;
            public float maximumHeldOutHorizontalDrift,rmsHeldOutHorizontalDrift;
            public Vector3 worldAnchor;
        }
        [Serializable] public sealed class Result
        {
            public string status="NOT_MEASURED",clip="";
            public float fittedNativeSpeedMetersPerSecond;
            public float leastSquaresSpeedMetersPerSecond;
            public float maximumHeldOutHorizontalDrift;
            public Window[] windows=Array.Empty<Window>();
            public string scope="Selected heel/toe support windows at a measured diagnostic external speed; no gameplay movement, terrain, turn, transition or production stance-lock claim.";
        }
        private sealed class Samples
        {
            public string marker="";
            public int start;
            public readonly List<(float time,Vector3 point,int index)> values=new List<(float,Vector3,int)>();
        }
        public static Result Measure(MeshyHasanPilotPlayer.LocomotionContactMeasurement motion)
        {
            if(motion.frames.Length!=121||motion.durationSeconds<=0||float.IsNaN(motion.durationSeconds)||float.IsInfinity(motion.durationSeconds))
                throw new ArgumentException("Stance review requires the actual 121-phase skin measurement.");
            bool Finite(Vector3 p)=>!float.IsNaN(p.x+p.y+p.z)&&!float.IsInfinity(p.x+p.y+p.z);
            if(motion.frames.Any(f=>!Finite(f.leftHeelCenter)||!Finite(f.leftToeCenter)||!Finite(f.rightHeelCenter)||!Finite(f.rightToeCenter)||
                float.IsNaN(f.leftHeel+f.leftToe+f.rightHeel+f.rightToe)||float.IsInfinity(f.leftHeel+f.leftToe+f.rightHeel+f.rightToe)))
                throw new ArgumentException("Nonfinite measured support surface.");
            const int count=120;
            var samples=new List<Samples>();
            for(var marker=0;marker<4;marker++)
            {
                Vector3 Point(int index)
                {
                    var f=motion.frames[(index%count+count)%count];
                    return marker==0?f.leftHeelCenter:marker==1?f.leftToeCenter:marker==2?f.rightHeelCenter:f.rightToeCenter;
                }
                float Height(int index)
                {
                    var f=motion.frames[index];return marker==0?f.leftHeel:marker==1?f.leftToe:marker==2?f.rightHeel:f.rightToe;
                }
                var support=Enumerable.Range(0,count).Select(i=>Height(i)<=.015f&&
                    (Point(i+1).z-Point(i-1).z)/(2*motion.durationSeconds/count)<-.1f).ToArray();
                var starts=Enumerable.Range(0,count).Where(i=>support[i]&&!support[(i+count-1)%count]).ToArray();
                var longest=new Samples{marker=new[]{"LeftHeel","LeftToe","RightHeel","RightToe"}[marker]};
                foreach(var start in starts)
                {
                    var candidate=new Samples{marker=longest.marker,start=start};
                    for(var j=0;j<count&&support[(start+j)%count];j++)
                        candidate.values.Add(((start+j)*motion.durationSeconds/count,Point(start+j),start+j));
                    if(candidate.values.Count>longest.values.Count)longest=candidate;
                }
                // Less than 8% of a cycle is a strike/roll transient, not a
                // reliable support window. Retain it in the skin/contact data.
                if(longest.values.Count>=10)samples.Add(longest);
            }
            var result=new Result{clip=motion.clip};
            if(!samples.Any(s=>s.marker.StartsWith("Left",StringComparison.Ordinal))||
                !samples.Any(s=>s.marker.StartsWith("Right",StringComparison.Ordinal)))
            {result.status="NO_TWO_SIDED_RELIABLE_SUPPORT_WINDOWS";return result;}
            var covariance=0f;var variance=0f;
            foreach(var sample in samples)
            {
                var train=sample.values.Where(p=>p.index%2==0).ToArray();
                var time=train.Average(p=>p.time);var z=train.Average(p=>p.point.z);
                foreach(var point in train){covariance+=(point.time-time)*(point.point.z-z);variance+=(point.time-time)*(point.time-time);}
            }
            var speed=-covariance/variance;
            if(float.IsNaN(speed)||float.IsInfinity(speed)||speed<=0)throw new InvalidOperationException("Invalid measured diagnostic stride speed.");
            result.leastSquaresSpeedMetersPerSecond=speed;
            if(motion.diagnosticStrideSpeedMetersPerSecond>0)speed=motion.diagnosticStrideSpeedMetersPerSecond;
            result.fittedNativeSpeedMetersPerSecond=speed;
            result.windows=samples.Select(sample=>
            {
                var train=sample.values.Where(p=>p.index%2==0).ToArray();
                var anchor=train.Aggregate(Vector3.zero,(sum,p)=>sum+p.point+Vector3.forward*speed*p.time)/train.Length;
                var held=sample.values.Where(p=>p.index%2!=0).ToArray();
                var errors=held.Select(p=>Vector3.ProjectOnPlane(p.point+Vector3.forward*speed*p.time-anchor,Vector3.up).magnitude).ToArray();
                return new Window{marker=sample.marker,startPhase=sample.start/(float)count,endPhase=(sample.start+sample.values.Count-1)/(float)count,
                    trainingSamples=train.Length,heldOutSamples=held.Length,worldAnchor=anchor,
                    maximumHeldOutHorizontalDrift=errors.Max(),rmsHeldOutHorizontalDrift=Mathf.Sqrt(errors.Average(e=>e*e))};
            }).ToArray();
            result.maximumHeldOutHorizontalDrift=result.windows.Max(w=>w.maximumHeldOutHorizontalDrift);
            result.status="MEASURED_DIAGNOSTIC_SUPPORT_WINDOWS_NOT_AUTOMATIC_VISUAL_ACCEPTANCE";
            return result;
        }
    }
}
