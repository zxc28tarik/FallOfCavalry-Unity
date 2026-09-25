using System;
using System.Linq;
using UnityEngine;

namespace FOC.Presentation.Visuals
{
    /// <summary>Authoring-only deformation diagnostic. Not a substitute for shared animation or loadout acceptance.</summary>
    public static class HistoricalArtPoseReview
    {
        public static Transform Bone(GameObject root,string name)=>root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        public static void Standing(GameObject human)
        {
            foreach(var side in new[]{"L","R"})
            {
                var sign=side=="L"?1f:-1f;
                Limb(Bone(human,"UpperArm_"+side),Bone(human,"LowerArm_"+side),Bone(human,"Hand_"+side),human.transform.TransformPoint(new Vector3(sign*.27f,1.01f,.12f)),human.transform.TransformDirection(new Vector3(sign*.4f,0,-1)));
            }
        }
        public static void Mounted(GameObject human,Transform seat)
        {
            var pelvis=Bone(human,"Pelvis");human.transform.SetParent(seat,true);human.transform.position+=seat.position-pelvis.position;
            foreach(var side in new[]{"L","R"})
            {
                var sign=side=="L"?1f:-1f;
                Limb(Bone(human,"UpperLeg_"+side),Bone(human,"LowerLeg_"+side),Bone(human,"Foot_"+side),seat.TransformPoint(new Vector3(sign*.46f,-.72f,.16f)),seat.TransformDirection(new Vector3(sign*.75f,0,1)));
                Bone(human,"Foot_"+side).rotation=seat.rotation;
                Limb(Bone(human,"UpperArm_"+side),Bone(human,"LowerArm_"+side),Bone(human,"Hand_"+side),seat.TransformPoint(new Vector3(sign*.16f,.21f,.33f)),seat.TransformDirection(new Vector3(sign,0,-.3f)));
            }
        }
        public static void Limb(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 pole)
        {
            var a=upper.position;var l1=Vector3.Distance(a,lower.position);var l2=Vector3.Distance(lower.position,end.position);var delta=target-a;
            if(l1<.001f||l2<.001f||delta.sqrMagnitude<.000001f)throw new InvalidOperationException("Invalid authoring limb.");
            var direction=delta.normalized;var distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(l1-l2)+.0001f,l1+l2-.0001f);
            var along=(l1*l1-l2*l2+distance*distance)/(2*distance);var perpendicular=Vector3.ProjectOnPlane(pole,direction).normalized;
            var knee=a+direction*along+perpendicular*Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
            upper.rotation=Quaternion.FromToRotation(lower.position-a,knee-a)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(end.position-lower.position,a+direction*distance-lower.position)*lower.rotation;
        }
    }
}
