#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace FOC.Visuals.Core
{
    public static class CanonicalRig
    {
        public static readonly IReadOnlyList<string> HumanBones=new[]{"Root","Pelvis","Spine","Chest","Neck","Head","UpperArm_L","LowerArm_L","Hand_L","UpperArm_R","LowerArm_R","Hand_R","UpperLeg_L","LowerLeg_L","Foot_L","UpperLeg_R","LowerLeg_R","Foot_R"};
        public static readonly IReadOnlyDictionary<VisualSocket,string> HumanSockets=new Dictionary<VisualSocket,string>{{VisualSocket.RightHand,"Socket_RightHand"},{VisualSocket.LeftHand,"Socket_LeftHand"},{VisualSocket.Head,"Socket_Head"},{VisualSocket.BackPrimary,"Socket_BackPrimary"},{VisualSocket.BackSecondary,"Socket_BackSecondary"},{VisualSocket.HipLeft,"Socket_HipLeft"},{VisualSocket.HipRight,"Socket_HipRight"},{VisualSocket.ShieldBack,"Socket_ShieldBack"}};
        public static readonly IReadOnlyList<string> MountBones=new[]{"MountRoot","MountPelvis","MountSpine","MountNeck","MountHead","FrontLeg_L","FrontLeg_R","BackLeg_L","BackLeg_R"};
        public const string RiderSocket="Socket_Rider";
    }

    public sealed class RigDescriptor
    {
        public RigDescriptor(RigKind kind,IEnumerable<string> bones,IEnumerable<string> sockets,bool hasBindPoses,int maximumInfluences,string scaleCode,string forwardAxis){Kind=kind;Bones=Canonical(bones);Sockets=Canonical(sockets);HasBindPoses=hasBindPoses;MaximumInfluences=maximumInfluences;ScaleCode=scaleCode??string.Empty;ForwardAxis=forwardAxis??string.Empty;}public RigKind Kind{get;}public IReadOnlyList<string>Bones{get;}public IReadOnlyList<string>Sockets{get;}public bool HasBindPoses{get;}public int MaximumInfluences{get;}public string ScaleCode{get;}public string ForwardAxis{get;}private static IReadOnlyList<string> Canonical(IEnumerable<string>? values)=>(values??Array.Empty<string>()).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
    }

    public sealed class RigValidationResult
    {
        internal RigValidationResult(IEnumerable<string> errors){Errors=errors.ToArray();}public IReadOnlyList<string> Errors{get;}public bool IsValid=>Errors.Count==0;
    }

    public static class RigValidator
    {
        public static RigValidationResult Validate(RigDescriptor descriptor){if(descriptor==null)throw new ArgumentNullException(nameof(descriptor));var errors=new List<string>();var required=descriptor.Kind==RigKind.Humanoid?CanonicalRig.HumanBones:CanonicalRig.MountBones;foreach(var bone in required)if(!descriptor.Bones.Contains(bone,StringComparer.Ordinal))errors.Add("Missing bone: "+bone);var sockets=descriptor.Kind==RigKind.Humanoid?CanonicalRig.HumanSockets.Values:new[]{CanonicalRig.RiderSocket};foreach(var socket in sockets)if(!descriptor.Sockets.Contains(socket,StringComparer.Ordinal))errors.Add("Missing socket: "+socket);var allowed=new HashSet<string>(required,StringComparer.Ordinal);foreach(var socket in sockets)allowed.Add(socket);foreach(var bone in descriptor.Bones)if(!allowed.Contains(bone))errors.Add("Unknown bone: "+bone);if(!descriptor.HasBindPoses)errors.Add("Missing bindposes.");if(descriptor.MaximumInfluences<1||descriptor.MaximumInfluences>4)errors.Add("Skin influence count must be between 1 and 4.");if(!StringComparer.Ordinal.Equals(descriptor.ScaleCode,"meters-1.0"))errors.Add("Scale must be meters-1.0.");if(!StringComparer.Ordinal.Equals(descriptor.ForwardAxis,"+Z"))errors.Add("Forward axis must be +Z.");return new RigValidationResult(errors);}
    }

    public sealed class CharacterLodProfile
    {
        public CharacterLodProfile(string id,float lod0,float lod1,float lod2,int maximumNearRenderers,int maximumNearMaterials){if(string.IsNullOrWhiteSpace(id)||!(lod0>lod1&&lod1>lod2&&lod2>0f)||maximumNearRenderers<1||maximumNearMaterials<1)throw new ArgumentException("LOD profile is invalid.");Id=id;Lod0=lod0;Lod1=lod1;Lod2=lod2;MaximumNearRenderers=maximumNearRenderers;MaximumNearMaterials=maximumNearMaterials;}public string Id{get;}public float Lod0{get;}public float Lod1{get;}public float Lod2{get;}public int MaximumNearRenderers{get;}public int MaximumNearMaterials{get;}
    }
}
