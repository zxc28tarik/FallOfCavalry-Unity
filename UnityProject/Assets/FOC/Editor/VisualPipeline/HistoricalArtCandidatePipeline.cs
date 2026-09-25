#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FOC.Visuals.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace FOC.Editor.Visuals
{
    /// <summary>Imports authored DCC topology. Never reads/copies a VisualProof mesh.</summary>
    public static class HistoricalArtCandidatePipeline
    {
        public const string SourceRoot="Assets/FOC/ArtSource/HistoricalSlice";
        public const string MountRoot="Assets/FOC/Presentation/Mounts/Ottoman1648";
        public const string CharacterRoot="Assets/FOC/Presentation/Characters/Ottoman1648";
        public const string EquipmentRoot="Assets/FOC/Presentation/Equipment/Ottoman1648";
        public const string MaterialRoot="Assets/FOC/Presentation/Materials/Ottoman1648";
        private static readonly Dictionary<string,Material> ImportedMaterials=new Dictionary<string,Material>(StringComparer.Ordinal);
        [Serializable]public sealed class SourceAsset{public int formatVersion;public string assetId="";public string category="";public string status="";public string generator="";public string source="";public string sourceSha256="";public string license="";public string date="";public string revision="";public SourceBone[] bones=Array.Empty<SourceBone>();public SourceLod[] lods=Array.Empty<SourceLod>();}
        [Serializable]public sealed class SourceBone{public string name="";public string parent="";public float[] position=Array.Empty<float>();}
        [Serializable]public sealed class SourceLod{public SourcePart[] parts=Array.Empty<SourcePart>();}
        [Serializable]public sealed class SourcePart{public string material="";public float[] positions=Array.Empty<float>();public float[] normals=Array.Empty<float>();public float[] uv=Array.Empty<float>();public int[] triangles=Array.Empty<int>();public int[] boneIndices=Array.Empty<int>();public float[] boneWeights=Array.Empty<float>();}

        public static void Run()
        {
            try{Generate();Debug.Log("FOC_14C_CANDIDATE_IMPORT_PASS (technical import only; NOT visual acceptance)");EditorApplication.Exit(0);}
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [MenuItem("FOC/Visuals/Import Historical Art Drafts")]
        public static void Generate()
        {
            ImportedMaterials.Clear();
            foreach(var path in new[]{MountRoot,CharacterRoot,EquipmentRoot,MaterialRoot})Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
            foreach(var file in Directory.GetFiles(SourceRoot,"*.focmesh.json",SearchOption.AllDirectories).OrderBy(x=>x,StringComparer.Ordinal))
                Import(JsonUtility.FromJson<SourceAsset>(File.ReadAllText(file)));
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }
        public static void ValidateSource(SourceAsset source)
        {
            if(source.formatVersion!=1||source.assetId.Contains("Proof")||source.lods.Length!=3||string.IsNullOrWhiteSpace(source.license)||string.IsNullOrWhiteSpace(source.sourceSha256))throw new InvalidOperationException("Invalid authored source: "+source.assetId);
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var b in source.bones)if(!names.Add(b.name)||b.position.Length!=3)throw new InvalidOperationException("Invalid source rig.");
            foreach(var b in source.bones)if(b.parent.Length>0&&!names.Contains(b.parent))throw new InvalidOperationException("Missing parent: "+b.parent);
            foreach(var lod in source.lods)foreach(var p in lod.parts)
            {
                var count=p.positions.Length/3;
                if(count<3||p.positions.Length%3!=0||p.normals.Length!=count*3||p.uv.Length!=count*2||p.triangles.Length%3!=0||p.triangles.Any(i=>i<0||i>=count))throw new InvalidOperationException("Invalid topology: "+source.assetId);
                if(p.positions.Any(float.IsNaN)||p.positions.Any(float.IsInfinity))throw new InvalidOperationException("Nonfinite vertex.");
                var opposed=0;var measured=0;
                for(var ti=0;ti<p.triangles.Length;ti+=3)
                {
                    var a=p.triangles[ti]*3;var b=p.triangles[ti+1]*3;var c=p.triangles[ti+2]*3;
                    var geometric=Vector3.Cross(V(p.positions,b)-V(p.positions,a),V(p.positions,c)-V(p.positions,a));
                    if(geometric.sqrMagnitude<.000000000001f)continue;measured++;
                    if(Vector3.Dot(geometric,V(p.normals,a)+V(p.normals,b)+V(p.normals,c))<-.000001f)opposed++;
                }
                // A few concave decimated boundary faces may oppose averaged smooth
                // normals. An axis/winding inversion affects most of the surface.
                if(measured>0&&opposed>measured*.02f)throw new InvalidOperationException("Triangle winding contradicts authored normals: "+source.assetId+" opposed="+opposed+"/"+measured);
                if(source.bones.Length==0)continue;
                if(p.boneWeights.Length!=count*4||p.boneIndices.Length!=count*4)throw new InvalidOperationException("Missing skin data.");
                for(var i=0;i<count;i++)
                {
                    var sum=0f;for(var j=0;j<4;j++){var ix=i*4+j;if(p.boneIndices[ix]<0||p.boneIndices[ix]>=source.bones.Length||p.boneWeights[ix]<0f||float.IsNaN(p.boneWeights[ix])||float.IsInfinity(p.boneWeights[ix]))throw new InvalidOperationException("Invalid skin influence.");sum+=p.boneWeights[ix];}
                    if(Mathf.Abs(sum-1f)>.0001f)throw new InvalidOperationException("Unnormalized skin weight.");
                }
            }
        }
        private static void Import(SourceAsset source)
        {
            ValidateSource(source);
            var mount=source.category=="Mount";var human=source.bones.Length>0&&!mount;
            var folder=mount?MountRoot:human?CharacterRoot:EquipmentRoot;
            var root=new GameObject(source.assetId);var transforms=new Dictionary<string,Transform>(StringComparer.Ordinal);
            try
            {
                foreach(var b in source.bones){var t=new GameObject(b.name).transform;t.SetParent(root.transform,false);t.position=V(b.position,0);transforms.Add(b.name,t);}
                foreach(var b in source.bones)if(b.parent.Length>0)transforms[b.name].SetParent(transforms[b.parent],true);
                if(human)AddHumanSockets(transforms);
                var bones=source.bones.Select(b=>transforms[b.name]).ToArray();
                var lods=new List<LOD>();
                for(var li=0;li<3;li++)
                {
                    var mesh=new Mesh{name=source.assetId+"_LOD"+li,indexFormat=IndexFormat.UInt32};
                    var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var weights=new List<BoneWeight>();
                    var triangles=new Dictionary<string,List<int>>(StringComparer.Ordinal);
                    foreach(var part in source.lods[li].parts)
                    {
                        var start=vertices.Count;if(!triangles.TryGetValue(part.material,out var list)){list=new List<int>();triangles.Add(part.material,list);}
                        list.AddRange(part.triangles.Select(i=>i+start));
                        for(var i=0;i<part.positions.Length/3;i++)
                        {
                            vertices.Add(V(part.positions,i*3));normals.Add(V(part.normals,i*3));uv.Add(new Vector2(part.uv[i*2],part.uv[i*2+1]));
                            if(bones.Length>0){var ix=i*4;weights.Add(new BoneWeight{boneIndex0=part.boneIndices[ix],boneIndex1=part.boneIndices[ix+1],boneIndex2=part.boneIndices[ix+2],boneIndex3=part.boneIndices[ix+3],weight0=part.boneWeights[ix],weight1=part.boneWeights[ix+1],weight2=part.boneWeights[ix+2],weight3=part.boneWeights[ix+3]});}
                        }
                    }
                    mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.subMeshCount=triangles.Count;var si=0;
                    foreach(var sub in triangles.Values)mesh.SetTriangles(sub,si++);
                    if(bones.Length>0){mesh.boneWeights=weights.ToArray();mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*root.transform.localToWorldMatrix).ToArray();}
                    mesh.RecalculateBounds();mesh.RecalculateTangents();
                    var meshPath=folder+"/"+mesh.name+".asset";Store(mesh,meshPath);var stored=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    var node=new GameObject("LOD"+li);node.transform.SetParent(root.transform,false);Renderer renderer;
                    if(bones.Length>0){var skin=node.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=stored;skin.bones=bones;skin.rootBone=bones[0];skin.localBounds=stored.bounds;skin.quality=SkinQuality.Bone4;renderer=skin;}
                    else{node.AddComponent<MeshFilter>().sharedMesh=stored;renderer=node.AddComponent<MeshRenderer>();}
                    renderer.sharedMaterials=triangles.Keys.Select(MaterialFor).ToArray();renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                    lods.Add(new LOD(li==0?.45f:li==1?.18f:.035f,new[]{renderer}));
                }
                var group=root.AddComponent<LODGroup>();group.SetLODs(lods.ToArray());group.RecalculateBounds();
                if(mount)
                {
                    var animator=root.AddComponent<Animator>();animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
                    var avatar=AvatarBuilder.BuildGenericAvatar(root,"MountRoot");avatar.name=source.assetId+"_Avatar";var avatarPath=folder+"/"+avatar.name+".asset";Store(avatar,avatarPath);
                    animator.avatar=AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);animator.runtimeAnimatorController=MountAnimations(root,transforms,folder);
                }
                else if(human)
                {
                    var avatar=HumanAvatar(root);avatar.name=source.assetId+"_Avatar";var avatarPath=folder+"/"+avatar.name+".asset";Store(avatar,avatarPath);
                    var animator=root.AddComponent<Animator>();animator.avatar=AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
                }
                PrefabUtility.SaveAsPrefabAsset(root,folder+"/"+source.assetId+".prefab");
                Debug.Log("FOC_14C_DRAFT_IMPORTED "+source.assetId+" vertices="+source.lods[0].parts.Sum(p=>p.positions.Length/3));
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        private static Avatar HumanAvatar(GameObject root)
        {
            var map=new Dictionary<string,string>{{"Pelvis","Hips"},{"Spine","Spine"},{"Chest","Chest"},{"Neck","Neck"},{"Head","Head"},{"UpperArm_L","LeftUpperArm"},{"LowerArm_L","LeftLowerArm"},{"Hand_L","LeftHand"},{"UpperArm_R","RightUpperArm"},{"LowerArm_R","RightLowerArm"},{"Hand_R","RightHand"},{"UpperLeg_L","LeftUpperLeg"},{"LowerLeg_L","LeftLowerLeg"},{"Foot_L","LeftFoot"},{"UpperLeg_R","RightUpperLeg"},{"LowerLeg_R","RightLowerLeg"},{"Foot_R","RightFoot"}};
            var description=new HumanDescription{human=map.Select(p=>new HumanBone{boneName=p.Key,humanName=p.Value,limit=new HumanLimit{useDefaultValues=true}}).ToArray(),skeleton=root.GetComponentsInChildren<Transform>().Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray(),upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,armStretch=.05f,legStretch=.05f,feetSpacing=0,hasTranslationDoF=false};
            var avatar=AvatarBuilder.BuildHumanAvatar(root,description);if(!avatar.isValid||!avatar.isHuman)throw new InvalidOperationException("Canonical humanoid avatar invalid: "+root.name);return avatar;
        }
        private static void AddHumanSockets(Dictionary<string,Transform> bones)
        {
            var locations=new[]{("Socket_RightHand","Hand_R",Vector3.zero),("Socket_LeftHand","Hand_L",Vector3.zero),("Socket_Head","Head",new Vector3(0,.06f,0)),("Socket_BackPrimary","Chest",new Vector3(0,.08f,-.15f)),("Socket_BackSecondary","Chest",new Vector3(.13f,.1f,-.15f)),("Socket_HipLeft","Pelvis",new Vector3(.22f,.02f,0)),("Socket_HipRight","Pelvis",new Vector3(-.22f,.02f,0)),("Socket_ShieldBack","Chest",new Vector3(0,.1f,-.2f))};
            foreach(var (name,parent,position) in locations){var t=new GameObject(name).transform;t.SetParent(bones[parent],false);t.localPosition=position;}
        }
        private static RuntimeAnimatorController MountAnimations(GameObject root,Dictionary<string,Transform> bones,string folder)
        {
            var path=folder+"/ANM_Horse.controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine=controller.layers[0].stateMachine;
            foreach(var gait in new[]{"Idle","Walk","Trot","Gallop","Turn"})
            {
                var duration=gait=="Idle"?3f:gait=="Walk"?1.3f:gait=="Trot"?.8f:.6f;var clip=new AnimationClip{name="ANM_Horse_"+gait,frameRate=30};
                foreach(var pair in bones)
                {
                    var name=pair.Key;var amplitude=name.Contains("LowerLeg")?30f:name.Contains("Hoof")?14f:name.Contains("Leg_")?22f:name=="MountNeck"?3f:name.StartsWith("Tail",StringComparison.Ordinal)?8f:0;
                    if(gait=="Idle")amplitude=name=="MountNeck"?1.8f:name.StartsWith("Tail",StringComparison.Ordinal)?4f:0;
                    if(amplitude==0)continue;
                    var phase=name.EndsWith("_R",StringComparison.Ordinal)?Mathf.PI:0f;if(name.StartsWith("Back",StringComparison.Ordinal))phase+=gait=="Walk"?Mathf.PI*.5f:gait=="Trot"?Mathf.PI:.8f;
                    var curve=new AnimationCurve();for(var k=0;k<=24;k++){var t=k/24f;var value=amplitude*Mathf.Sin(t*2*Mathf.PI+phase);if(name.Contains("LowerLeg"))value=Mathf.Max(0,value);curve.AddKey(t*duration,value);}
                    var bonePath=AnimationUtility.CalculateTransformPath(pair.Value,root.transform);clip.SetCurve(bonePath,typeof(Transform),"localEulerAnglesRaw.x",curve);
                }
                if(gait=="Turn")clip.SetCurve(AnimationUtility.CalculateTransformPath(bones["MountNeck"],root.transform),typeof(Transform),"localEulerAnglesRaw.y",AnimationCurve.EaseInOut(0,-12,duration,12));
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);clip.EnsureQuaternionContinuity();
                var clipPath=folder+"/"+clip.name+".anim";Store(clip,clipPath);var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==gait)??machine.AddState(gait);state.motion=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);if(gait=="Idle")machine.defaultState=state;
            }
            EditorUtility.SetDirty(controller);return controller;
        }
        private static Material MaterialFor(string name)
        {
            if(ImportedMaterials.TryGetValue(name,out var cached))return cached;
            var path=MaterialRoot+"/MAT_"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);var isNew=material==null;
            if(isNew)material=new Material(Shader.Find("Standard")){name="MAT_"+name};material.SetFloat("_Glossiness",.22f);
            if(name.StartsWith("Horse",StringComparison.Ordinal))
            {
                material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(SourceRoot+"/Mounts/"+name+"_D.png");
                var normal=AssetDatabase.LoadAssetAtPath<Texture2D>(SourceRoot+"/Mounts/"+name+"_N.png");if(normal!=null){material.SetTexture("_BumpMap",normal);material.EnableKeyword("_NORMALMAP");material.SetFloat("_BumpScale",.5f);}
            }
            else
            {
                var color=name=="Skin"?new Color(.69f,.48f,.36f):name=="Linen"?new Color(.65f,.57f,.41f):name=="ClothBlue"?new Color(.15f,.24f,.28f):name=="ClothRed"?new Color(.38f,.11f,.08f):name=="Mail"?new Color(.52f,.55f,.56f):name=="Steel"?new Color(.47f,.49f,.5f):name=="Wood"?new Color(.32f,.17f,.07f):new Color(.20f,.105f,.048f);
                if(name=="EyeWhite")color=new Color(.7f,.65f,.55f);if(name=="Hair")color=new Color(.035f,.025f,.017f);
                if(name=="LeatherSole")color=new Color(.065f,.042f,.025f);
                material.color=color;material.SetFloat("_Metallic",name=="Mail"?.62f:name=="Steel"?.85f:0);material.SetFloat("_Glossiness",name=="Steel"?.48f:name=="Skin"?.21f:name=="Leather"?.28f:.18f);
                var tex=HistoricalArtSurfaceAuthoring.Albedo(name);var texturePath=MaterialRoot+"/"+tex.name+".asset";Store(tex,texturePath);material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                var normal=HistoricalArtSurfaceAuthoring.Normal(name);var normalPath=MaterialRoot+"/"+normal.name+".asset";Store(normal,normalPath);material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));material.EnableKeyword("_NORMALMAP");material.SetFloat("_BumpScale",name=="Mail"?.65f:.18f);
                var tiling=name=="Mail"?12:name=="Skin"?5:name=="Wood"?1:6;material.mainTextureScale=new Vector2(tiling,tiling);material.SetTextureScale("_BumpMap",new Vector2(tiling,tiling));
                if(name=="Skin"||name=="SkinMature"||name.StartsWith("HairCards",StringComparison.Ordinal))
                {
                    var licensed=AssetDatabase.LoadAssetAtPath<Texture2D>(SourceRoot+"/Characters/"+name+"_D.png");
                    if(licensed==null)throw new InvalidOperationException("Versioned CC0 surface missing: "+name);
                    material.mainTexture=licensed;material.mainTextureScale=Vector2.one;
                    material.color=name.StartsWith("HairCards",StringComparison.Ordinal)?new Color(.45f,.35f,.26f):new Color(.93f,.87f,.80f);
                    material.SetFloat("_Metallic",0);material.SetFloat("_Glossiness",.18f);
                    if(name.StartsWith("HairCards",StringComparison.Ordinal))
                    {
                        material.SetFloat("_Mode",1);material.SetFloat("_Cutoff",.38f);material.EnableKeyword("_ALPHATEST_ON");material.SetOverrideTag("RenderType","TransparentCutout");material.renderQueue=2450;material.DisableKeyword("_NORMALMAP");
                    }
                }
            }
            if(isNew)AssetDatabase.CreateAsset(material,path);else EditorUtility.SetDirty(material);ImportedMaterials.Add(name,material);return material;
        }
        private static Vector3 V(float[] a,int i)=>new Vector3(a[i],a[i+1],a[i+2]);
        private static void Store(UnityEngine.Object value,string path){var old=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);if(old==null)AssetDatabase.CreateAsset(value,path);else{EditorUtility.CopySerialized(value,old);EditorUtility.SetDirty(old);UnityEngine.Object.DestroyImmediate(value);}}
    }
}
