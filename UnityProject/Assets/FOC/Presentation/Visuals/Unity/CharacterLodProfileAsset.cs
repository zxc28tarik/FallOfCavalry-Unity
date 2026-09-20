#nullable enable
using UnityEngine;
using FOC.Visuals.Core;

namespace FOC.Presentation.Visuals
{
    [CreateAssetMenu(menuName="FOC/Visuals/LOD Profile",fileName="LOD_BattleStandard")]
    public sealed class CharacterLodProfileAsset:ScriptableObject
    {public string profileId="battle-standard";[Range(0.01f,1f)]public float lod0=0.60f;[Range(0.01f,1f)]public float lod1=0.25f;[Range(0.001f,1f)]public float lod2=0.08f;public int maximumNearRenderers=6;public int maximumNearMaterials=4;public CharacterLodProfile ToCore()=>new CharacterLodProfile(profileId,lod0,lod1,lod2,maximumNearRenderers,maximumNearMaterials);}
}
