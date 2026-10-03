#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FOC.Editor.Visuals
{
    /// <summary>
    /// Isolated, foreign-rig intake. This never edits production catalogs or
    /// canonical bones and does not turn a technical import into art approval.
    /// </summary>
    public static class MeshyHasanPilotPipeline
    {
        public const string IntakeRoot="Assets/FOC/ArtSource/HistoricalSlice/MeshyPilot";
        public const string SourceRoot=IntakeRoot+"/Source";
        public const string ModelPath=SourceRoot+"/Hasan_Meshy.fbx";
        public const string OutputRoot="Assets/FOC/Presentation/Characters/Ottoman1648/MeshyPilot";
        public const string PrefabPath=OutputRoot+"/CHR_HasanAga_MeshyPilot.prefab";
        public const string MaterialPath=OutputRoot+"/MAT_HasanAga_MeshyPilot.mat";
        public const string ControllerPath=OutputRoot+"/ANM_HasanAga_MeshyPilot.controller";
        public const string ExpectedSourceHash="0ee0076119289b4748fa0eeb11fa631d9a03e55b9b1208db6afe19beba410b27";
        public static string ReportPath=>Path.GetFullPath("../TestResults/MeshyPilot/unity-import.json");

        // Source names are not canonical aliases. Chest and UpperChest are the
        // source's two torso joints, mapped to required Humanoid Spine/Chest.
        private static readonly string[] SourceBones={"Hips","LeftUpperLeg","LeftLowerLeg","LeftFoot","LeftToes","RightUpperLeg","RightLowerLeg","RightFoot","RightToes","Chest","UpperChest","LeftShoulder","LeftUpperArm","LeftLowerArm","LeftHand","RightShoulder","RightUpperArm","RightLowerArm","RightHand","Neck","Head","head_end","headfront"};
        private static readonly string[] HumanBones={"Hips","LeftUpperLeg","LeftLowerLeg","LeftFoot","LeftToes","RightUpperLeg","RightLowerLeg","RightFoot","RightToes","Spine","Chest","LeftShoulder","LeftUpperArm","LeftLowerArm","LeftHand","RightShoulder","RightUpperArm","RightLowerArm","RightHand","Neck","Head"};

        [Serializable] public sealed class ClipAudit
        {
            public string name="";public float duration;public float frameRate;public bool humanMotion;
        }
        [Serializable] public sealed class MeshAudit
        {
            public string name="";public int vertices;public int triangles;public int skinBones;public int bindPoses;public int maximumInfluences;public int invalidWeightVertices;
        }
        [Serializable] public sealed class ImportReport
        {
            public string status="NOT_READY";public string utc="";public string unityVersion="";public string operation="";public string source=ModelPath;public string sourceSha256="";
            public string prefab=PrefabPath;public string error="";public bool avatarHuman;public bool avatarValid;public int preservedSourceBones;
            public float heightMeters;public int suppliedLods;public string lodStatus="LOD0 only; LOD1/LOD2 NOT RUN; no duplicate geometry presented as LOD reduction.";
            public string restPoseSource="Original FBX skin bind matrices, not FBX saved Running take or controller playback.";
            public float maximumBindMatrixError;
            public string skinWeightStatus="Imported weights validated finite/normalized with at most four influences; not asserted bit-identical to source float values. Unity importer minimum influence threshold may clamp to 0.001.";
            public string materialStatus="";public string visualAcceptance="NOT EVALUATED: requires actual player screenshots and deformation review.";
            public string animationLimitations="Only supplied Walking/Running. No supplied Idle, Attack, Crouch or Mounted animation; shared FOC clips not retargeted by this pilot.";
            public string rigStatus="Foreign 23-bone hierarchy retained; not canonical 18-bone compatible merely because Avatar is Humanoid.";
            public string provenance="User-confirmed Meshy Pro creation; not CC0; generation/reference provenance remains in intake ledger.";
            public string[] boneNames=Array.Empty<string>();public string[] sockets=Array.Empty<string>();public ClipAudit[] clips=Array.Empty<ClipAudit>();public MeshAudit[] meshes=Array.Empty<MeshAudit>();
        }

        public static void Run(){Batch(true);}
        public static void RunVerify(){Batch(false);}
        private static void Batch(bool generate)
        {
            try
            {
                if(generate)Generate();else Verify();
                Debug.Log("FOC_MESHY_HASAN_PILOT_TECHNICAL_PASS: no production/visual acceptance asserted.");
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }

        [MenuItem("FOC/Visuals/Import Isolated Meshy Hasan Pilot")]
        public static ImportReport Generate()
        {
            var report=NewReport("Generate");
            try
            {
                ValidateSourceFile(report);
                Directory.CreateDirectory(OutputRoot);AssetDatabase.Refresh();
                ConfigureModel();
                ConfigureTexture(SourceRoot+"/BaseColor.png",true);
                ConfigureTexture(SourceRoot+"/MetallicSmoothness.png",false);
                var material=CreateMaterial();
                var clips=LoadClips();var controller=CreateController(clips);
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath)??throw new InvalidOperationException("FBX model did not import.");
                var instance=UnityEngine.Object.Instantiate(model);
                try
                {
                    instance.name="CHR_HasanAga_MeshyPilot";
                    var animator=instance.GetComponent<Animator>();
                    if(animator==null)animator=instance.AddComponent<Animator>();
                    // Avatar/controller assignment can reset a Humanoid's
                    // transforms to FBX saved motion. Author with it disabled,
                    // restore bind afterwards, and let the pilot player enable
                    // it explicitly only when evaluating an actual clip.
                    animator.enabled=false;
                    animator.avatar=LoadAvatar();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
                    animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    RestoreSourceBindPose(instance);
                    foreach(var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        renderer.sharedMaterials=Enumerable.Repeat(material,renderer.sharedMesh.subMeshCount).ToArray();
                        renderer.quality=SkinQuality.Bone4;renderer.updateWhenOffscreen=true;
                    }
                    AddSockets(instance);
                    // One genuinely supplied resolution. Reduction is deferred,
                    // not simulated by repeating this mesh in three LOD slots.
                    var lod=instance.GetComponent<LODGroup>();
                    if(lod==null)lod=instance.AddComponent<LODGroup>();
                    lod.SetLODs(new[]{new LOD(.025f,instance.GetComponentsInChildren<Renderer>(true))});lod.RecalculateBounds();
                    RestoreSourceBindPose(instance);
                    PrefabUtility.SaveAsPrefabAsset(instance,PrefabPath);
                }
                finally{UnityEngine.Object.DestroyImmediate(instance);}
                AssetDatabase.SaveAssets();
                Audit(report);report.status="TECHNICAL_IMPORT_PASS_VISUAL_NOT_EVALUATED";
                return report;
            }
            catch(Exception e){report.error=e.ToString();throw;}
            finally{WriteReport(report);}
        }

        // Read-only validation of committed assets. No importer, prefab,
        // controller, material or source writes; report is outside Assets.
        public static ImportReport Verify()
        {
            var report=NewReport("Verify");
            try
            {
                ValidateSourceFile(report);Audit(report);
                report.status="TECHNICAL_IMPORT_PASS_VISUAL_NOT_EVALUATED";return report;
            }
            catch(Exception e){report.error=e.ToString();throw;}
            finally{WriteReport(report);}
        }

        private static ImportReport NewReport(string operation)=>new ImportReport{operation=operation,utc=DateTime.UtcNow.ToString("O"),unityVersion=Application.unityVersion};
        private static void WriteReport(ImportReport report)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private static void ValidateSourceFile(ImportReport report)
        {
            Require(File.Exists(ModelPath),"Supplied Meshy FBX is missing: "+ModelPath);
            using(var sha=SHA256.Create())using(var stream=File.OpenRead(ModelPath))report.sourceSha256=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
            Require(report.sourceSha256==ExpectedSourceHash,"Source FBX differs from preserved user intake.");
        }

        private static void ConfigureModel()
        {
            var importer=AssetImporter.GetAtPath(ModelPath) as ModelImporter??throw new InvalidOperationException("Missing FBX importer.");
            importer.globalScale=1f;importer.useFileScale=true;importer.importCameras=false;importer.importLights=false;
            importer.importVisibility=false;importer.importBlendShapes=true;importer.importAnimation=true;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.meshCompression=ModelImporterMeshCompression.Off;
            importer.optimizeGameObjects=false;importer.optimizeMeshPolygons=false;importer.optimizeMeshVertices=false;importer.isReadable=true;
            importer.weldVertices=false;
            importer.skinWeights=ModelImporterSkinWeights.Custom;importer.maxBonesPerVertex=4;importer.minBoneWeight=0f;
            importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;
            importer.animationCompression=ModelImporterAnimationCompression.Off;
            // First expose the unchanged hierarchy even if the automatic human
            // mapper fails on Meshy's unusual two-joint torso naming.
            importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath)??throw new InvalidOperationException("Generic FBX import failed.");
            ValidateRig(model);
            var bindModel=UnityEngine.Object.Instantiate(model);
            HumanDescription description;
            try
            {
                bindModel.name=model.name;
                RestoreSourceBindPose(bindModel);
                description=new HumanDescription
                {
                    human=HumanBones.Select((name,i)=>new HumanBone{boneName=SourceBones[i],humanName=name,limit=new HumanLimit{useDefaultValues=true}}).ToArray(),
                    skeleton=bindModel.GetComponentsInChildren<Transform>(true).Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray(),
                    upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,armStretch=.05f,legStretch=.05f,feetSpacing=0f,hasTranslationDoF=false
                };
            }
            finally{UnityEngine.Object.DestroyImmediate(bindModel);}
            importer=(ModelImporter)AssetImporter.GetAtPath(ModelPath);importer.animationType=ModelImporterAnimationType.Human;importer.humanDescription=description;
            var clips=importer.defaultClipAnimations;
            Require(clips.Length==2,"Intake expected exactly two supplied animation takes.");
            foreach(var clip in clips)
            {
                var name=clip.name.Split('|').Last();
                Require(name=="Walking"||name=="Running","Unexpected supplied animation take: "+clip.name);
                clip.name=name;clip.loopTime=true;clip.loopPose=false;
            }
            importer.clipAnimations=clips;importer.SaveAndReimport();
        }

        private static void ConfigureTexture(string path,bool srgb)
        {
            Require(File.Exists(path),"Required prepared PBR texture missing: "+path);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter??throw new InvalidOperationException("Texture importer missing: "+path);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=srgb;importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency=false;importer.mipmapEnabled=true;importer.maxTextureSize=2048;
            importer.textureCompression=TextureImporterCompression.Compressed;importer.compressionQuality=100;importer.isReadable=false;importer.SaveAndReimport();
        }

        private static Material CreateMaterial()
        {
            var shader=Shader.Find("Standard")??throw new InvalidOperationException("Built-in Standard PBR shader is unavailable.");
            var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(material==null){material=new Material(shader){name="MAT_HasanAga_MeshyPilot"};AssetDatabase.CreateAsset(material,MaterialPath);}
            material.shader=shader;material.SetColor("_Color",Color.white);material.SetFloat("_Mode",0f);material.SetFloat("_Metallic",1f);
            material.SetFloat("_GlossMapScale",1f);material.SetFloat("_SmoothnessTextureChannel",0f);
            material.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(SourceRoot+"/BaseColor.png"));
            material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(SourceRoot+"/MetallicSmoothness.png"));
            material.SetTexture("_BumpMap",null);material.DisableKeyword("_NORMALMAP");material.EnableKeyword("_METALLICGLOSSMAP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");material.DisableKeyword("_EMISSION");EditorUtility.SetDirty(material);return material;
        }
        public static AnimationClip[] LoadClips()
        {
            return AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__",StringComparison.Ordinal)).OrderBy(c=>c.name,StringComparer.Ordinal).ToArray();
        }
        private static Avatar LoadAvatar()=>AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().SingleOrDefault()??throw new InvalidOperationException("Source model has no unique Avatar.");
        private static AnimatorController CreateController(AnimationClip[] clips)
        {
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)??AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine=controller.layers[0].stateMachine;
            foreach(var clip in clips)
            {
                var state=machine.states.Select(s=>s.state).SingleOrDefault(s=>s.name==clip.name)??machine.AddState(clip.name);
                state.motion=clip;state.writeDefaultValues=true;if(clip.name=="Walking")machine.defaultState=state;
            }
            EditorUtility.SetDirty(controller);return controller;
        }

        private static Transform[] ValidateRig(GameObject root)
        {
            var all=root.GetComponentsInChildren<Transform>(true);
            foreach(var name in SourceBones)Require(all.Count(t=>t.name==name)==1,"Source skeletal joint missing or duplicated: "+name);
            var bones=SourceBones.Select(name=>all.Single(t=>t.name==name)).ToArray();
            Require(bones[9].parent==bones[0]&&bones[10].parent==bones[9],"Source torso hierarchy changed.");
            return bones;
        }

        /// <summary>
        /// Restore the source skin's actual rest transforms on an ephemeral
        /// model instance, without editing the source mesh, weights or rig.
        /// FBX's saved object pose is Running in this supplied export; it is not
        /// a valid substitute for the authored bind/reference skeleton.
        /// </summary>
        public static void RestoreSourceBindPose(GameObject instance)
        {
            var expected=BindWorldMatrices(instance);
            // Compute every local from the original bind-world table before
            // changing any Transform, so traversal order cannot contaminate it.
            var locals=expected.ToDictionary(pair=>pair.Key,pair=>
                (pair.Key.parent!=null&&expected.TryGetValue(pair.Key.parent,out var parentBind)?parentBind.inverse:pair.Key.parent!=null?pair.Key.parent.worldToLocalMatrix:Matrix4x4.identity)*pair.Value);
            foreach(var pair in locals)
            {
                var local=pair.Value;var position=(Vector3)local.GetColumn(3);var rotation=local.rotation;var scale=local.lossyScale;
                Require(IsFinite(position)&&IsFinite(scale)&&IsFinite(new Vector3(rotation.x,rotation.y,rotation.z))&&!float.IsNaN(rotation.w)&&!float.IsInfinity(rotation.w),"Nonfinite bind transform: "+pair.Key.name);
                pair.Key.localPosition=position;pair.Key.localRotation=rotation;pair.Key.localScale=scale;
            }
            Require(BindMatrixError(instance)<.0001f,"Could not reconstruct the source bind matrices without changing the skeleton.");
        }

        private static Dictionary<Transform,Matrix4x4> BindWorldMatrices(GameObject root)
        {
            var renderers=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Require(renderers.Length==1,"Bind reconstruction expects the supplied single mesh.");
            var renderer=renderers[0];var bindposes=renderer.sharedMesh.bindposes;var bones=renderer.bones;
            Require(bindposes.Length==bones.Length&&bones.Length==23,"All23 supplied bind bones must be present; no inferred reconstruction.");
            var result=new Dictionary<Transform,Matrix4x4>();
            for(var i=0;i<bones.Length;i++)
            {
                Require(bones[i]!=null,"Missing source bind bone.");
                result.Add(bones[i],renderer.transform.localToWorldMatrix*bindposes[i].inverse);
            }
            foreach(var bone in ValidateRig(root))Require(result.ContainsKey(bone),"Source bone has no bind matrix: "+bone.name);
            return result;
        }
        private static float BindMatrixError(GameObject root)
        {
            var maximum=0f;
            foreach(var pair in BindWorldMatrices(root))
            {
                var actual=pair.Key.localToWorldMatrix;
                for(var i=0;i<16;i++)maximum=Mathf.Max(maximum,Mathf.Abs(actual[i]-pair.Value[i]));
            }
            return maximum;
        }

        private static void AddSockets(GameObject root)
        {
            var bones=ValidateRig(root).ToDictionary(t=>t.name,StringComparer.Ordinal);
            AddSocket(root,bones["RightHand"],"Socket_RightHand",Vector3.zero);
            AddSocket(root,bones["LeftHand"],"Socket_LeftHand",Vector3.zero);
            AddSocket(root,bones["Head"],"Socket_Head",Vector3.zero);
            AddSocket(root,bones["UpperChest"],"Socket_BackPrimary",new Vector3(.12f,0f,-.11f));
            AddSocket(root,bones["UpperChest"],"Socket_BackSecondary",new Vector3(-.12f,0f,-.11f));
            AddSocket(root,bones["UpperChest"],"Socket_ShieldBack",new Vector3(0f,0f,-.14f));
            AddSocket(root,bones["Hips"],"Socket_HipLeft",new Vector3(-.15f,0f,0f));
            AddSocket(root,bones["Hips"],"Socket_HipRight",new Vector3(.15f,0f,0f));
        }
        private static void AddSocket(GameObject root,Transform bone,string name,Vector3 rootFrameOffset)
        {
            var socket=new GameObject(name).transform;socket.SetParent(bone,false);
            // Source armature may carry its FBX unit conversion. Convert meter
            // offsets through world space rather than assuming bone scale one.
            socket.position=bone.position+root.transform.TransformVector(rootFrameOffset);socket.localRotation=Quaternion.identity;
            var parentScale=bone.lossyScale;var rootScale=root.transform.lossyScale;
            Require(Mathf.Abs(parentScale.x)>1e-6f&&Mathf.Abs(parentScale.y)>1e-6f&&Mathf.Abs(parentScale.z)>1e-6f,"Cannot attach a socket below a zero-scale source bone.");
            socket.localScale=new Vector3(rootScale.x/parentScale.x,rootScale.y/parentScale.y,rootScale.z/parentScale.z);
        }

        private static void Audit(ImportReport report)
        {
            var importer=AssetImporter.GetAtPath(ModelPath) as ModelImporter??throw new InvalidOperationException("Model importer missing.");
            Require(importer.animationType==ModelImporterAnimationType.Human&&!importer.optimizeGameObjects&&!importer.weldVertices&&importer.meshCompression==ModelImporterMeshCompression.Off&&importer.isReadable,"Pilot model settings were overridden, welded or compressed.");
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath)??throw new InvalidOperationException("Model asset missing.");
            var sourceBones=ValidateRig(model);report.preservedSourceBones=sourceBones.Length;report.boneNames=sourceBones.Select(t=>t.name).ToArray();
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)??throw new InvalidOperationException("Pilot prefab missing.");
            ValidateRig(prefab);var animator=prefab.GetComponent<Animator>();
            report.maximumBindMatrixError=BindMatrixError(prefab);
            Require(report.maximumBindMatrixError<.0001f,"Prefab is not stored in original source bind pose.");
            Require(animator!=null&&animator.avatar!=null,"Pilot prefab lacks Animator/Avatar.");
            report.avatarHuman=animator!.avatar.isHuman;report.avatarValid=animator.avatar.isValid;
            Require(report.avatarHuman&&report.avatarValid,"Unity could not validate the supplied rig as Humanoid.");
            Require(animator.avatar==LoadAvatar()&&animator.runtimeAnimatorController==AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath),"Pilot does not reference the supplied source Avatar/controller.");
            Require(!animator.applyRootMotion,"Pilot unexpectedly applies locomotion root motion.");
            var clips=LoadClips();report.clips=clips.Select(c=>new ClipAudit{name=c.name,duration=c.length,frameRate=c.frameRate,humanMotion=c.humanMotion}).ToArray();
            Require(clips.Length==2&&clips.Any(c=>c.name=="Walking")&&clips.Any(c=>c.name=="Running"),"Expected supplied Walking and Running clips are missing.");
            Require(clips.All(c=>c.humanMotion&&c.length>0f&&!float.IsInfinity(c.length)),"A supplied animation is empty or did not import as Humanoid motion.");
            var skinned=prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);Require(skinned.Length==1,"Intake expected one authored skinned mesh.");
            report.meshes=skinned.Select(AuditMesh).ToArray();
            var localBounds=skinned[0].localBounds;var matrix=prefab.transform.worldToLocalMatrix*skinned[0].transform.localToWorldMatrix;
            report.heightMeters=matrix.MultiplyVector(new Vector3(0f,localBounds.size.y,0f)).magnitude;
            // Bake a rest-pose copy for scale, instead of animated import bounds
            // that can be expanded by a clip's root motion or arm swing.
            var instance=UnityEngine.Object.Instantiate(prefab);
            try
            {
                instance.GetComponent<Animator>().enabled=false;
                var renderer=instance.GetComponentInChildren<SkinnedMeshRenderer>();var baked=new Mesh();
                try
                {
                    renderer.BakeMesh(baked);var vertices=baked.vertices.Select(v=>instance.transform.InverseTransformPoint(renderer.transform.TransformPoint(v))).ToArray();
                    Require(vertices.Length>0&&vertices.All(IsFinite),"Nonfinite or empty baked rest mesh.");
                    report.heightMeters=vertices.Max(v=>v.y)-vertices.Min(v=>v.y);
                }
                finally{UnityEngine.Object.DestroyImmediate(baked);}
            }
            finally{UnityEngine.Object.DestroyImmediate(instance);}
            Require(Mathf.Abs(report.heightMeters-1.7800007f)<.002f,"Unexpected source bind height: "+report.heightMeters+"m; expected1.78m from independent DCC rest audit.");
            var lod=prefab.GetComponent<LODGroup>();Require(lod!=null,"Pilot lacks explicit LOD declaration.");report.suppliedLods=lod!.lodCount;
            Require(report.suppliedLods==1&&lod.GetLODs()[0].renderers.Length==1,"Pilot must honestly expose one supplied LOD.");
            var transforms=prefab.GetComponentsInChildren<Transform>(true);report.sockets=transforms.Where(t=>t.name.StartsWith("Socket_",StringComparison.Ordinal)).Select(t=>t.name).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            foreach(var socket in FOC.Visuals.Core.CanonicalRig.HumanSockets.Values)Require(report.sockets.Count(s=>s==socket)==1,"Missing/duplicated semantic socket: "+socket);
            ValidateMaterial(skinned);report.materialStatus="Standard PBR: sRGB BaseColor, linear RGB metallic / A=1-roughness, 2048 texture cap; no supplied normal map.";
        }
        private static bool IsFinite(Vector3 v)=>!(float.IsNaN(v.x)||float.IsInfinity(v.x)||float.IsNaN(v.y)||float.IsInfinity(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.z));
        private static MeshAudit AuditMesh(SkinnedMeshRenderer renderer)
        {
            var mesh=renderer.sharedMesh??throw new InvalidOperationException("Missing skinned mesh.");
            var result=new MeshAudit{name=mesh.name,vertices=mesh.vertexCount,triangles=mesh.triangles.Length/3,skinBones=renderer.bones.Length,bindPoses=mesh.bindposes.Length};
            Require(result.triangles==9586,"Source triangle count differs from audited user mesh.");
            Require(renderer.bones.All(t=>t!=null)&&result.skinBones==result.bindPoses&&result.skinBones>0,"Skin bones or bindposes are missing.");
            Require(mesh.vertices.All(IsFinite),"Source mesh contains nonfinite positions.");
            var weights=mesh.boneWeights;Require(weights.Length==mesh.vertexCount,"Missing four-influence skin data.");
            foreach(var weight in weights)
            {
                var values=new[]{weight.weight0,weight.weight1,weight.weight2,weight.weight3};
                var indices=new[]{weight.boneIndex0,weight.boneIndex1,weight.boneIndex2,weight.boneIndex3};
                var influences=values.Count(v=>v>0f);result.maximumInfluences=Math.Max(result.maximumInfluences,influences);
                if(influences==0||values.Any(v=>float.IsNaN(v)||float.IsInfinity(v)||v<0f)||Mathf.Abs(values.Sum()-1f)>.002f||indices.Where((_,i)=>values[i]>0f).Any(i=>i<0||i>=result.skinBones))result.invalidWeightVertices++;
            }
            Require(result.invalidWeightVertices==0,"Invalid imported skin weights: "+result.invalidWeightVertices);return result;
        }
        private static void ValidateMaterial(SkinnedMeshRenderer[] renderers)
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath)??throw new InvalidOperationException("Pilot PBR material missing.");
            Require(material.shader.name=="Standard"&&material.IsKeywordEnabled("_METALLICGLOSSMAP"),"Pilot PBR shader or metallic map keyword incorrect.");
            foreach(var pair in new[]{new[]{"BaseColor.png","_MainTex"},new[]{"MetallicSmoothness.png","_MetallicGlossMap"}})
            {
                var path=SourceRoot+"/"+pair[0];var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                Require(texture!=null&&importer!=null&&material.GetTexture(pair[1])==texture,"Explicit supplied PBR texture is not connected: "+path);
                Require(importer!.sRGBTexture==(pair[0]=="BaseColor.png")&&importer.maxTextureSize==2048,"Pilot texture color-space/cap overridden: "+path);
            }
            Require(material.GetTexture("_BumpMap")==null&&!material.IsKeywordEnabled("_NORMALMAP"),"No source normal map was supplied; do not invent one.");
            Require(renderers.All(r=>r.sharedMaterials.All(m=>m==material)),"Renderer still uses ambiguous embedded FBX material.");
        }
    }
}
