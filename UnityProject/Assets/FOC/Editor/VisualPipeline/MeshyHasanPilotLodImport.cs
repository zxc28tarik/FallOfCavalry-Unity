#nullable enable
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FOC.Editor.Visuals
{
    /// <summary>Derived rest meshes only. Original 23-bone source rig and LOD0 remain authoritative.</summary>
    public static class MeshyHasanPilotLodImport
    {
        public const string RuntimeHash="356dd92317ec1787f48aabe4e85157d31dfec2dd029168c856c5ffd6a125fadf";
        [Serializable] public sealed class Bone {public string name="",parent="";public float[] headWorld=Array.Empty<float>();}
        [Serializable] public sealed class LodData
        {
            public string format="",sourceSha256="",runtimeSourceSha256="",coordinates="";public int lod;
            public Bone[] bones=Array.Empty<Bone>();public float[] positions=Array.Empty<float>(),normals=Array.Empty<float>(),uv=Array.Empty<float>(),boneWeights=Array.Empty<float>();
            public int[] triangles=Array.Empty<int>(),boneIndices=Array.Empty<int>();
            public int[] triangleMaterialIndices=Array.Empty<int>();public string[] materialNames=Array.Empty<string>();
        }
        public static void ValidateRuntimeSource(string hash)
        {
            if(hash!=RuntimeHash)throw new InvalidOperationException("Runtime FBX is not the independently validated media-only derivative.");
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private static Vector3 V(float[] values,int offset=0)=>new Vector3(values[offset],values[offset+1],values[offset+2]);
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        private static Matrix4x4 Basis(Func<string,Vector3> position)
        {
            var origin=position("Hips");
            var x=(position("LeftUpperArm")-position("RightUpperArm")).normalized;
            var y=position("Head")-origin;y=(y-x*Vector3.Dot(y,x)).normalized;
            var z=position("headfront")-position("Head");z=(z-x*Vector3.Dot(z,x)-y*Vector3.Dot(z,y)).normalized;
            Require(x.sqrMagnitude>.9f&&y.sqrMagnitude>.9f&&z.sqrMagnitude>.9f,"Degenerate rest-pose alignment landmarks.");
            var matrix=Matrix4x4.identity;matrix.SetColumn(0,new Vector4(x.x,x.y,x.z,0));matrix.SetColumn(1,new Vector4(y.x,y.y,y.z,0));
            matrix.SetColumn(2,new Vector4(z.x,z.y,z.z,0));matrix.SetColumn(3,new Vector4(origin.x,origin.y,origin.z,1));return matrix;
        }
        public static void Attach(GameObject instance,Material material)
        {
            var original=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
            var all=instance.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name,StringComparer.Ordinal);
            var levels=new LOD[3];levels[0]=new LOD(.55f,new Renderer[]{original});
            for(var index=1;index<=2;index++)
            {
                var path=MeshyHasanPilotPipeline.SourceRoot+"/Hasan_Meshy_LOD"+index+".json";
                var data=JsonUtility.FromJson<LodData>(File.ReadAllText(path));
                Require(data.format=="FOC_MESHY_REST_LOD_MESH_V1"&&data.lod==index&&data.sourceSha256==MeshyHasanPilotPipeline.ExpectedSourceHash&&data.runtimeSourceSha256==RuntimeHash,"LOD provenance/schema mismatch.");
                Require(data.coordinates=="BLENDER_WORLD_METERS_Z_UP"&&data.bones.Length==23,"Unknown LOD space/rig.");
                var mesh=CreateDerivedMesh(instance,data,index);
                var bones=data.bones.Select(b=>all[b.name]).ToArray();
                var meshPath=MeshyHasanPilotPipeline.OutputRoot+"/MESH_HasanAga_MeshyPilot_LOD"+index+".asset";
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(existing==null){AssetDatabase.CreateAsset(mesh,meshPath);existing=mesh;}
                else{EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);}
                var go=new GameObject("MeshyDerivedLOD"+index);go.transform.SetParent(instance.transform,false);
                var skin=go.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=existing;skin.bones=bones;skin.rootBone=original.rootBone;skin.sharedMaterial=material;skin.quality=SkinQuality.Bone4;skin.updateWhenOffscreen=true;skin.localBounds=existing.bounds;
                levels[index]=new LOD(index==1?.25f:.025f,new Renderer[]{skin});
            }
            var group=instance.GetComponent<LODGroup>();if(group==null)group=instance.AddComponent<LODGroup>();
            group.SetLODs(levels);group.RecalculateBounds();
        }

        /// <summary>Shared import conversion for isolated LOD2 review/adoption. Does not edit the prefab or rig.</summary>
        public static Mesh CreateDerivedMesh(GameObject instance,LodData data,int index)
        {
                Require(data.format=="FOC_MESHY_REST_LOD_MESH_V1"&&data.lod==index&&data.sourceSha256==MeshyHasanPilotPipeline.ExpectedSourceHash&&data.runtimeSourceSha256==RuntimeHash,"LOD provenance/schema mismatch.");
                Require(data.coordinates=="BLENDER_WORLD_METERS_Z_UP"&&data.bones.Length==23,"Unknown LOD space/rig.");
                var all=instance.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name,StringComparer.Ordinal);
                var source=data.bones.ToDictionary(b=>b.name,b=>V(b.headWorld),StringComparer.Ordinal);
                var conversion=Basis(n=>all[n].position)*Basis(n=>source[n]).inverse;
                var worst=data.bones.Max(b=>Vector3.Distance(conversion.MultiplyPoint3x4(source[b.name]),all[b.name].position));
                Require(worst<.002f,"DCC rest skeleton does not align with source bind bones: "+worst+"m.");
                var count=data.positions.Length/3;
                Require(count>0&&data.positions.Length==count*3&&data.normals.Length==count*3&&data.uv.Length==count*2&&data.boneIndices.Length==count*4&&data.boneWeights.Length==count*4,"Malformed derived LOD arrays.");
                Require(data.positions.All(Finite)&&data.normals.All(Finite)&&data.uv.All(Finite)&&data.boneWeights.All(w=>Finite(w)&&w>=0),"Nonfinite LOD data.");
                Require(data.triangles.Length>0&&data.triangles.Length%3==0&&data.triangles.All(i=>i>=0&&i<count),"Invalid LOD triangles.");
                Require(data.materialNames.Length==1&&data.triangleMaterialIndices.Length==data.triangles.Length/3&&data.triangleMaterialIndices.All(i=>i==0),"LOD material topology must remain the one source atlas, not silently flatten multiple materials.");
                var localConversion=instance.transform.worldToLocalMatrix*conversion;
                var vertices=Enumerable.Range(0,count).Select(i=>localConversion.MultiplyPoint3x4(V(data.positions,i*3))).ToArray();
                var normals=Enumerable.Range(0,count).Select(i=>localConversion.inverse.transpose.MultiplyVector(V(data.normals,i*3)).normalized).ToArray();
                var triangles=(int[])data.triangles.Clone();
                if(localConversion.determinant<0)for(var i=0;i<triangles.Length;i+=3){var swap=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=swap;}
                var bones=data.bones.Select(b=>all[b.name]).ToArray();var weights=new BoneWeight[count];
                for(var i=0;i<count;i++)
                {
                    var j=i*4;Require(Mathf.Abs(data.boneWeights.Skip(j).Take(4).Sum()-1)<.0001f,"Derived LOD weights not normalized.");
                    Require(data.boneIndices.Skip(j).Take(4).All(b=>b>=0&&b<23),"Derived LOD weight bone missing.");
                    weights[i]=new BoneWeight{boneIndex0=data.boneIndices[j],boneIndex1=data.boneIndices[j+1],boneIndex2=data.boneIndices[j+2],boneIndex3=data.boneIndices[j+3],weight0=data.boneWeights[j],weight1=data.boneWeights[j+1],weight2=data.boneWeights[j+2],weight3=data.boneWeights[j+3]};
                }
                var mesh=new Mesh{name="MESH_HasanAga_MeshyPilot_LOD"+index};mesh.vertices=vertices;mesh.normals=normals;
                mesh.uv=Enumerable.Range(0,count).Select(i=>new Vector2(data.uv[i*2],data.uv[i*2+1])).ToArray();mesh.triangles=triangles;mesh.boneWeights=weights;
                mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*instance.transform.localToWorldMatrix).ToArray();mesh.RecalculateTangents();mesh.RecalculateBounds();
                Debug.Log("FOC_MESHY_LOD_IMPORT lod="+index+" triangles="+triangles.Length/3+" alignmentError="+worst);
                return mesh;
        }
    }
}
