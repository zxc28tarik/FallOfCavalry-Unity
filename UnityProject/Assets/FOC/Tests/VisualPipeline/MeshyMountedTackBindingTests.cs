using System;
using System.Linq;
using FOC.Presentation.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FOC.Tests.VisualPipeline
{
    public sealed class MeshyMountedTackBindingTests
    {
        private const string HorsePath="Assets/FOC/Presentation/Mounts/Ottoman1648/MNT_Horse_Anatolian_01.prefab";
        private const string TackPath="Assets/FOC/Presentation/Equipment/Ottoman1648/HAR_SipahiHarness_01.prefab";

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void OnlyRigidLeatherReinsAreRemovedAllOtherSubmeshesAndVerticesRemain(int lod)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(TackPath);
            Assert.That(source,Is.Not.Null);
            var filter=source.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="LOD"+lod);
            var materials=filter.GetComponent<MeshRenderer>().sharedMaterials;
            var leather=Array.FindIndex(materials,m=>m.name.IndexOf("Leather",StringComparison.OrdinalIgnoreCase)>=0);
            var original=filter.sharedMesh;var originalTriangles=original.triangles;
            var derived=MeshyMountedTackBinding.StripRigidReins(original,leather,out var removed);
            try
            {
                Assert.That(removed,Is.GreaterThan(0));
                if(lod==0)Assert.That(removed,Is.EqualTo(96),"Exactly the two source 48-triangle reins, not the blanket.");
                Assert.That(derived.vertices,Is.EqualTo(original.vertices));
                Assert.That(derived.normals,Is.EqualTo(original.normals));
                Assert.That(derived.uv,Is.EqualTo(original.uv));
                for(var i=0;i<original.subMeshCount;i++)
                    if(i!=leather)Assert.That(derived.GetTriangles(i),Is.EqualTo(original.GetTriangles(i)),"Blanket/iron topology must be retained.");
                Assert.That(original.triangles,Is.EqualTo(originalTriangles),"Shared source asset must remain unmodified.");
            }
            finally{Object.DestroyImmediate(derived);}
        }

        [Test]
        public void SaddleFollowsSpineAndBothReinsFollowMovingHandsAndHead()
        {
            var horse=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HorsePath));
            var tack=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TackPath),horse.transform);
            var left=new GameObject("Left hand fixture");var right=new GameObject("Right hand fixture");
            MeshyMountedTackBinding binding=null;
            try
            {
                horse.GetComponent<Animator>().enabled=false;
                var head=horse.GetComponentsInChildren<Transform>().Single(t=>t.name=="MountHead");
                var spine=horse.GetComponentsInChildren<Transform>().Single(t=>t.name=="MountSpine");
                var before=tack.transform.position;binding=new MeshyMountedTackBinding(horse,tack);
                Assert.That(tack.transform.parent,Is.SameAs(spine));
                Assert.That(Vector3.Distance(tack.transform.position,before),Is.LessThan(.00001f));
                left.transform.position=new Vector3(-.1f,2f,-.1f);right.transform.position=new Vector3(.1f,2f,-.1f);
                binding.Update(left.transform,right.transform);
                var line=tack.GetComponentsInChildren<LineRenderer>().Single(l=>l.name=="Rein_Right_HandToBit");
                var bitBefore=line.GetPosition(line.positionCount-1);
                spine.position+=new Vector3(.05f,.03f,.08f);head.rotation=Quaternion.AngleAxis(15,Vector3.right)*head.rotation;
                right.transform.position+=Vector3.right*.08f;
                binding.Update(left.transform,right.transform);
                Assert.That(Vector3.Distance(line.GetPosition(0),right.transform.TransformPoint(new Vector3(-.012f,.092f,.018f))),Is.LessThan(.00001f));
                Assert.That(Vector3.Distance(bitBefore,line.GetPosition(line.positionCount-1)),Is.GreaterThan(.01f));
                Assert.That(binding.LeftReinEndpointError+binding.RightReinEndpointError,Is.LessThan(.00001f));
                binding.Dispose();binding=null;
                Assert.That(horse.GetComponentsInChildren<LineRenderer>(true),Is.Empty);
            }
            finally{binding?.Dispose();Object.DestroyImmediate(left);Object.DestroyImmediate(right);Object.DestroyImmediate(horse);}
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void ShorterStirrupsPreserveBlanketAndSaddleWhileRaisingTheIron(int lod)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(TackPath);
            var filter=source.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="LOD"+lod);
            var materials=filter.GetComponent<MeshRenderer>().sharedMaterials;
            var leather=Array.FindIndex(materials,m=>m.name.IndexOf("Leather",StringComparison.OrdinalIgnoreCase)>=0);
            var iron=Array.FindIndex(materials,m=>m.name.IndexOf("Steel",StringComparison.OrdinalIgnoreCase)>=0);
            var original=filter.sharedMesh;var before=original.vertices;
            var derived=MeshyMountedTackBinding.StripRigidReins(original,leather,out _);
            try
            {
                MeshyMountedTackBinding.FitStirrupLength(derived,leather,iron,MeshyMountedTackBinding.StirrupRise);
                var after=derived.vertices;
                Assert.That(derived.normals,Is.EqualTo(original.normals),"Untouched saddle/blanket shading must remain smooth.");
                foreach(var index in original.GetTriangles(iron).Distinct())
                    Assert.That(Vector3.Distance(after[index],before[index]+Vector3.up*MeshyMountedTackBinding.StirrupRise),Is.LessThan(.000001f));
                foreach(var index in original.GetTriangles(0).Distinct())Assert.That(after[index],Is.EqualTo(before[index]),"Saddle blanket surface must stay fitted to the horse.");
                foreach(var index in original.GetTriangles(leather).Distinct().Where(i=>before[i].z<-.5f&&before[i].y>1.7f))
                    Assert.That(after[index],Is.EqualTo(before[index]),"Cantle/seat must not move with the stirrup leathers.");
                Assert.That(original.vertices,Is.EqualTo(before));
            }
            finally{Object.DestroyImmediate(derived);}
        }
    }
}
