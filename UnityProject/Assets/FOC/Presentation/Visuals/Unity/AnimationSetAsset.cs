#nullable enable
using System;
using UnityEngine;
using FOC.Visuals.Core;

namespace FOC.Presentation.Visuals
{
    [CreateAssetMenu(menuName="FOC/Visuals/Animation Set",fileName="ANM_SharedHuman")]
    public sealed class AnimationSetAsset:ScriptableObject
    {public AnimationCapability[] capabilities=Array.Empty<AnimationCapability>();public RuntimeAnimatorController controller=null!;public AnimationClip[] clips=Array.Empty<AnimationClip>();public bool hasMountedSet;}
}
