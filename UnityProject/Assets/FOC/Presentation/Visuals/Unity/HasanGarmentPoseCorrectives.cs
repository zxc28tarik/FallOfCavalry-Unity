using System;
using System.Collections.Generic;
using UnityEngine;

namespace FOC.Presentation.Visuals
{
    /// <summary>
    /// Candidate garment pose-space interpolation on the unchanged canonical
    /// rig. Reads actual joint pose, not animation state, time or review flags.
    /// No runtime cloth/vertex solve and no gameplay state is involved.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public sealed class HasanGarmentPoseCorrectives:MonoBehaviour
    {
        public const int FeatureCount=18;
        public const string ShapePrefix="Pose_";
        private const double ExactDistanceSquared=1e-10;
        private static readonly string[] BoneNames={"UpperLeg_L","LowerLeg_L","Foot_L","UpperLeg_R","LowerLeg_R","Foot_R","Spine","Chest","Root"};

        [Serializable]
        public sealed class Sample
        {
            // Raw identifier, e.g. "Neutral". Corresponding shape is
            // "Pose_Neutral" on each of the three consolidated LOD meshes.
            public string name=string.Empty;
            public float[] features=Array.Empty<float>();
        }

        public Sample[] samples=Array.Empty<Sample>();

        private sealed class RendererBinding
        {
            public SkinnedMeshRenderer renderer;
            public int[] resetIndices;
            public int[] sampleIndices;
        }

        private Transform[] bones;
        private RendererBinding[] renderers;
        private float[][] sampleFeatures;
        private readonly float[] currentFeatures=new float[FeatureCount];
        private readonly int[] nearestIndices=new int[3];
        private readonly double[] nearestDistances=new double[3];
        private readonly float[] nearestWeights=new float[3];
        private bool initialized;

        private void Awake()=>Initialize();
        private void LateUpdate()=>ApplyNow();

        /// <summary>Validate serialized data and cache rig/renderers once.</summary>
        public void Initialize()
        {
            initialized=false;
            if(samples==null||samples.Length==0)throw new InvalidOperationException("Garment correctives require at least the Neutral sample.");
            var names=new HashSet<string>(StringComparer.Ordinal);
            var copiedFeatures=new float[samples.Length][];
            for(var i=0;i<samples.Length;i++)
            {
                var sample=samples[i];
                if(sample==null||string.IsNullOrWhiteSpace(sample.name)||sample.name!=sample.name.Trim()||sample.name.StartsWith(ShapePrefix,StringComparison.Ordinal))
                    throw new InvalidOperationException("Corrective sample needs a raw, nonempty, unpadded identifier.");
                if(!names.Add(sample.name))throw new InvalidOperationException("Duplicate garment corrective sample: "+sample.name);
                if(sample.features==null||sample.features.Length!=FeatureCount)throw new InvalidOperationException("Corrective sample must contain exactly 18 features: "+sample.name);
                copiedFeatures[i]=new float[FeatureCount];
                for(var feature=0;feature<FeatureCount;feature++)
                {
                    var value=sample.features[feature];
                    if(!Finite(value))throw new InvalidOperationException("Nonfinite garment corrective feature: "+sample.name);
                    copiedFeatures[i][feature]=value;
                }
            }
            if(!names.Contains("Neutral"))throw new InvalidOperationException("Garment correctives must include the unchanged Neutral pose sample.");

            var cachedBones=FindBones(gameObject);
            var skins=GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if(skins.Length==0)throw new InvalidOperationException("Garment corrective source has no skinned renderers.");
            var bindings=new RendererBinding[skins.Length];
            for(var r=0;r<skins.Length;r++)
            {
                var mesh=skins[r].sharedMesh;
                if(mesh==null)throw new InvalidOperationException("Garment corrective renderer has no mesh: "+skins[r].name);
                var reset=new List<int>();
                for(var shape=0;shape<mesh.blendShapeCount;shape++)
                    if(mesh.GetBlendShapeName(shape).StartsWith(ShapePrefix,StringComparison.Ordinal))reset.Add(shape);
                var indices=new int[samples.Length];
                for(var s=0;s<samples.Length;s++)
                {
                    indices[s]=mesh.GetBlendShapeIndex(ShapePrefix+samples[s].name);
                    if(indices[s]<0)throw new InvalidOperationException("Missing garment shape "+ShapePrefix+samples[s].name+" on "+skins[r].name);
                }
                bindings[r]=new RendererBinding{renderer=skins[r],resetIndices=reset.ToArray(),sampleIndices=indices};
            }
            // Reject a degenerate rig at initialization, not after partial
            // renderer mutation. Feature buffers are reused every frame.
            CaptureInto(cachedBones,currentFeatures);
            bones=cachedBones;renderers=bindings;sampleFeatures=copiedFeatures;initialized=true;
        }

        /// <summary>Apply current pose immediately for tests/manual authoring preview.</summary>
        public void ApplyNow()
        {
            if(!initialized)Initialize();
            CaptureInto(bones,currentFeatures);
            for(var k=0;k<3;k++){nearestIndices[k]=-1;nearestDistances[k]=double.MaxValue;nearestWeights[k]=0;}
            var exact=-1;
            for(var sample=0;sample<sampleFeatures.Length;sample++)
            {
                var distance=0d;
                for(var feature=0;feature<FeatureCount;feature++)
                {
                    var difference=(double)currentFeatures[feature]-sampleFeatures[sample][feature];
                    distance+=difference*difference;
                }
                if(distance<=ExactDistanceSquared){exact=sample;break;}
                // Strict comparison preserves serialized order for exact ties.
                for(var rank=0;rank<3;rank++)
                {
                    if(distance>=nearestDistances[rank])continue;
                    for(var move=2;move>rank;move--){nearestDistances[move]=nearestDistances[move-1];nearestIndices[move]=nearestIndices[move-1];}
                    nearestDistances[rank]=distance;nearestIndices[rank]=sample;break;
                }
            }
            if(exact>=0)
            {
                for(var k=0;k<3;k++){nearestIndices[k]=-1;nearestWeights[k]=0;}
                nearestIndices[0]=exact;nearestWeights[0]=100;
            }
            else
            {
                var total=0d;
                for(var rank=0;rank<3;rank++)if(nearestIndices[rank]>=0)total+=1d/nearestDistances[rank];
                for(var rank=0;rank<3;rank++)if(nearestIndices[rank]>=0)nearestWeights[rank]=(float)(100d/nearestDistances[rank]/total);
            }
            foreach(var binding in renderers)
            {
                // Only Pose_ is ours. Grip and any future face/equipment
                // correctives retain the weights written by their own systems.
                foreach(var shape in binding.resetIndices)binding.renderer.SetBlendShapeWeight(shape,0);
                for(var rank=0;rank<3;rank++)if(nearestIndices[rank]>=0)
                    binding.renderer.SetBlendShapeWeight(binding.sampleIndices[nearestIndices[rank]],nearestWeights[rank]);
            }
        }

        /// <summary>
        /// Shared authoring feature extraction: left thigh/shin, right
        /// thigh/shin, Spine.up, Chest.forward; six canonical skeletal-Root
        /// local unit vectors. A rigid root turn is not garment articulation.
        /// </summary>
        public static float[] CaptureFeatures(GameObject root)
        {
            if(root==null)throw new ArgumentNullException(nameof(root));
            var result=new float[FeatureCount];CaptureInto(FindBones(root),result);return result;
        }

        private static Transform[] FindBones(GameObject root)
        {
            var result=new Transform[BoneNames.Length];
            foreach(var child in root.GetComponentsInChildren<Transform>(true))
            for(var i=0;i<BoneNames.Length;i++)if(child.name==BoneNames[i])
            {
                if(result[i]!=null)throw new InvalidOperationException("Ambiguous garment corrective bone: "+BoneNames[i]);
                result[i]=child;
            }
            for(var i=0;i<result.Length;i++)if(result[i]==null)throw new InvalidOperationException("Missing garment corrective bone: "+BoneNames[i]);
            return result;
        }

        private static void CaptureInto(Transform[] cachedBones,float[] output)
        {
            // Shared by authoring/export and runtime. Animator transform curves
            // may turn the skeletal Root beneath an unmoved actor GameObject;
            // removing only the latter's rotation selected unrelated gait poses.
            var root=cachedBones[8];
            WriteVector(root,cachedBones[1].position-cachedBones[0].position,output,0);
            WriteVector(root,cachedBones[2].position-cachedBones[1].position,output,3);
            WriteVector(root,cachedBones[4].position-cachedBones[3].position,output,6);
            WriteVector(root,cachedBones[5].position-cachedBones[4].position,output,9);
            WriteVector(root,cachedBones[6].up,output,12);
            WriteVector(root,cachedBones[7].forward,output,15);
        }

        private static void WriteVector(Transform root,Vector3 direction,float[] output,int offset)
        {
            if(!Finite(direction.x)||!Finite(direction.y)||!Finite(direction.z)||direction.sqrMagnitude<1e-12f)
                throw new InvalidOperationException("Degenerate/nonfinite garment corrective bone direction.");
            var local=root.InverseTransformDirection(direction.normalized).normalized;
            if(!Finite(local.x)||!Finite(local.y)||!Finite(local.z))throw new InvalidOperationException("Nonfinite garment corrective root transform.");
            output[offset]=local.x;output[offset+1]=local.y;output[offset+2]=local.z;
        }
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
