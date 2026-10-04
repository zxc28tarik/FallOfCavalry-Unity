#nullable enable
using System;
using UnityEngine;

namespace FOC.Presentation.Visuals
{
    /// <summary>
    /// Measured, target-specific presentation-child height correction. Original
    /// Humanoid clip and its FootIK remain untouched. Never apply to actor/gameplay
    /// root; reset the presentation baseline before evaluating each new pose.
    /// </summary>
    public sealed class MeshyTargetContactProfile : ScriptableObject
    {
        public AnimationClip sourceClip=null!;
        public Avatar targetAvatar=null!;
        public bool useFootIK;
        public float groundY;
        public bool loop;
        public float maximumBodyCorrection=.12f;
        public float[] correctionMeters=Array.Empty<float>();
        public string sourcePoseGateEvidence="";
        public string supportProvenance="";

        public float Evaluate(float normalizedPhase)
        {
            if(!Finite(normalizedPhase)||normalizedPhase<0||normalizedPhase>1)
                throw new ArgumentOutOfRangeException(nameof(normalizedPhase),"Explicit normalized [0,1] contact phase required; caller owns wrapping.");
            if(correctionMeters==null||correctionMeters.Length<3)
                throw new InvalidOperationException("Measured contact samples missing.");
            var at=normalizedPhase*(correctionMeters.Length-1);
            var a=Mathf.Min(Mathf.FloorToInt(at),correctionMeters.Length-1);
            var b=Mathf.Min(a+1,correctionMeters.Length-1);
            var value=Mathf.Lerp(correctionMeters[a],correctionMeters[b],at-a);
            if(!Finite(value)||!Finite(maximumBodyCorrection)||maximumBodyCorrection<=0||Mathf.Abs(value)>maximumBodyCorrection)
                throw new InvalidOperationException("Invalid/out-of-bound measured contact correction.");
            return value;
        }

        public void ValidateBinding(Animator animator,AnimationClip clip)
        {
            if(animator==null||clip==null||targetAvatar==null||sourceClip==null||animator.avatar!=targetAvatar||clip!=sourceClip)
                throw new InvalidOperationException("Target contact profile requires its exact measured Avatar and original clip.");
            if(!targetAvatar.isValid||!targetAvatar.isHuman||!sourceClip.isHumanMotion||sourceClip.length<=0||!Finite(groundY))
                throw new InvalidOperationException("Invalid Humanoid contact profile binding.");
            if(string.IsNullOrWhiteSpace(sourcePoseGateEvidence)||string.IsNullOrWhiteSpace(supportProvenance))
                throw new InvalidOperationException("Reviewed contact provenance missing.");
            if(correctionMeters==null||correctionMeters.Length<3)
                throw new InvalidOperationException("Measured contact samples missing.");
            for(var i=0;i<correctionMeters.Length;i++)
                if(!Finite(correctionMeters[i])||Mathf.Abs(correctionMeters[i])>maximumBodyCorrection)
                    throw new InvalidOperationException("Invalid measured contact sample.");
            Evaluate(0);Evaluate(1);
        }
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
