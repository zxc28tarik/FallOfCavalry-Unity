using System;
using System.Linq;
using FOC.Presentation.Visuals;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FOC.Tests.VisualPipeline
{
    public sealed class HasanGarmentPoseCorrectivesTests
    {
        [Test]
        public void FeaturesAreEighteenRootLocalDirectionsIndependentOfActorPlacement()
        {
            using(var fixture=new Fixture())
            {
                var before=HasanGarmentPoseCorrectives.CaptureFeatures(fixture.root);
                Assert.That(before.Length,Is.EqualTo(18));
                fixture.root.transform.position=new Vector3(4,-2,7);
                fixture.root.transform.rotation=Quaternion.Euler(17,113,-12);
                fixture.root.transform.localScale=Vector3.one*1.7f;
                var after=HasanGarmentPoseCorrectives.CaptureFeatures(fixture.root);
                for(var i=0;i<18;i++)Assert.That(after[i],Is.EqualTo(before[i]).Within(.00001f));
                for(var i=0;i<18;i+=3)Assert.That(new Vector3(after[i],after[i+1],after[i+2]).magnitude,Is.EqualTo(1).Within(.00001f));
            }
        }

        [TestCase(-45f)]
        [TestCase(45f)]
        [TestCase(120f)]
        public void RigidSkeletalRootYawDoesNotSelectAGarmentDeformation(float yaw)
        {
            using(var fixture=new Fixture())
            {
                var before=HasanGarmentPoseCorrectives.CaptureFeatures(fixture.root);
                fixture.Configure(Sample("Neutral",before),Sample("Bent",Shift(before,.5f)));
                fixture.driver.ApplyNow();
                fixture.skeletalRoot.localRotation=Quaternion.Euler(0,yaw,0);
                fixture.skeletalRoot.localPosition=new Vector3(.25f,.3f,-.4f);
                var after=HasanGarmentPoseCorrectives.CaptureFeatures(fixture.root);
                for(var i=0;i<18;i++)Assert.That(after[i],Is.EqualTo(before[i]).Within(.00001f));
                fixture.driver.ApplyNow();
                foreach(var skin in fixture.skins)
                {
                    Assert.That(Weight(fixture,skin,"Pose_Neutral"),Is.EqualTo(100));
                    Assert.That(Weight(fixture,skin,"Pose_Bent"),Is.Zero,"Rigid turning must not activate gait garment corrections.");
                }
            }
        }

        [Test]
        public void ExactSampleUsesSingleCorrectiveAndPreservesGripOnEveryLod()
        {
            using(var fixture=new Fixture())
            {
                var pose=HasanGarmentPoseCorrectives.CaptureFeatures(fixture.root);
                fixture.Configure(Sample("Neutral",pose),Sample("Bent",Shift(pose,1)));
                foreach(var skin in fixture.skins)
                {
                    skin.SetBlendShapeWeight(fixture.mesh.GetBlendShapeIndex("Grip_R"),73);
                    skin.SetBlendShapeWeight(fixture.mesh.GetBlendShapeIndex("Pose_Orphan"),57);
                    skin.SetBlendShapeWeight(fixture.mesh.GetBlendShapeIndex("Pose_Bent"),88);
                }
                fixture.driver.ApplyNow();
                foreach(var skin in fixture.skins)
                {
                    Assert.That(Weight(fixture,skin,"Pose_Neutral"),Is.EqualTo(100));
                    Assert.That(Weight(fixture,skin,"Pose_Bent"),Is.Zero);
                    Assert.That(Weight(fixture,skin,"Pose_Orphan"),Is.Zero);
                    Assert.That(Weight(fixture,skin,"Grip_R"),Is.EqualTo(73));
                }
            }
        }

        [Test]
        public void NearestThreeUseNormalizedInverseSquaredFeatureDistance()
        {
            using(var fixture=new Fixture())
            {
                var pose=HasanGarmentPoseCorrectives.CaptureFeatures(fixture.root);
                fixture.Configure(Sample("Neutral",Shift(pose,-1)),Sample("One",Shift(pose,1)),Sample("Two",Shift(pose,2)),Sample("Far",Shift(pose,3)));
                fixture.driver.ApplyNow();
                foreach(var skin in fixture.skins)
                {
                    Assert.That(Weight(fixture,skin,"Pose_Neutral"),Is.EqualTo(100f/2.25f).Within(.0001f));
                    Assert.That(Weight(fixture,skin,"Pose_One"),Is.EqualTo(100f/2.25f).Within(.0001f));
                    Assert.That(Weight(fixture,skin,"Pose_Two"),Is.EqualTo(25f/2.25f).Within(.0001f));
                    Assert.That(Weight(fixture,skin,"Pose_Far"),Is.Zero);
                }
            }
        }

        [Test]
        public void SelectionRespondsToActualPoseWithoutAnimatorTimeOrClipState()
        {
            using(var fixture=new Fixture())
            {
                var neutral=HasanGarmentPoseCorrectives.CaptureFeatures(fixture.root);
                var chest=fixture.skeletalRoot.Find("Chest");chest.localRotation=Quaternion.Euler(15,22,0);
                var bent=HasanGarmentPoseCorrectives.CaptureFeatures(fixture.root);chest.localRotation=Quaternion.identity;
                fixture.Configure(Sample("Neutral",neutral),Sample("Bent",bent));
                fixture.driver.ApplyNow();Assert.That(Weight(fixture,fixture.skins[0],"Pose_Neutral"),Is.EqualTo(100));
                chest.localRotation=Quaternion.Euler(15,22,0);
                fixture.driver.ApplyNow();Assert.That(Weight(fixture,fixture.skins[0],"Pose_Bent"),Is.EqualTo(100));
                Assert.That(fixture.root.GetComponent<Animator>(),Is.Null,"Fixture proves no animation state dependency.");
            }
        }

        [TestCase("wrong-length")]
        [TestCase("null-features")]
        [TestCase("nan")]
        [TestCase("infinity")]
        [TestCase("duplicate")]
        [TestCase("padded-name")]
        [TestCase("prefixed-name")]
        [TestCase("missing-neutral")]
        public void InvalidSourceMetadataIsRejectedBeforeRendererMutation(string fault)
        {
            using(var fixture=new Fixture())
            {
                var pose=HasanGarmentPoseCorrectives.CaptureFeatures(fixture.root);
                fixture.Configure(Sample("Neutral",pose),Sample("Bent",Shift(pose,1)));
                var sample=fixture.driver.samples[1];
                switch(fault)
                {
                    case "wrong-length":sample.features=new float[17];break;
                    case "null-features":sample.features=null;break;
                    case "nan":sample.features[0]=float.NaN;break;
                    case "infinity":sample.features[0]=float.PositiveInfinity;break;
                    case "duplicate":sample.name="Neutral";break;
                    case "padded-name":sample.name=" Bent";break;
                    case "prefixed-name":sample.name="Pose_Bent";break;
                    case "missing-neutral":fixture.driver.samples[0].name="Other";break;
                }
                Assert.Throws<InvalidOperationException>(()=>fixture.driver.Initialize());
                foreach(var skin in fixture.skins)Assert.That(Weight(fixture,skin,"Pose_Neutral"),Is.Zero);
            }
        }

        [Test]
        public void SourceShapeNameMismatchIsRejectedInsteadOfSilentlyIgnoringALod()
        {
            using(var fixture=new Fixture())
            {
                var pose=HasanGarmentPoseCorrectives.CaptureFeatures(fixture.root);
                fixture.Configure(Sample("Neutral",pose),Sample("Bent",Shift(pose,1)));
                fixture.driver.samples[1].name="Missing";
                Assert.Throws<InvalidOperationException>(()=>fixture.driver.Initialize());
            }
        }

        [Test]
        public void AmbiguousOrDegenerateRigCannotGeneratePoseFeatures()
        {
            using(var fixture=new Fixture())
            {
                var duplicate=new GameObject("Chest");duplicate.transform.SetParent(fixture.root.transform,false);
                Assert.Throws<InvalidOperationException>(()=>HasanGarmentPoseCorrectives.CaptureFeatures(fixture.root));
                Object.DestroyImmediate(duplicate);
                fixture.skeletalRoot.Find("LowerLeg_L").position=fixture.skeletalRoot.Find("UpperLeg_L").position;
                Assert.Throws<InvalidOperationException>(()=>HasanGarmentPoseCorrectives.CaptureFeatures(fixture.root));
            }
        }

        private static HasanGarmentPoseCorrectives.Sample Sample(string name,float[] features)=>new HasanGarmentPoseCorrectives.Sample{name=name,features=features};
        private static float[] Shift(float[] source,float value){var result=(float[])source.Clone();result[0]+=value;return result;}
        private static float Weight(Fixture fixture,SkinnedMeshRenderer skin,string shape)=>skin.GetBlendShapeWeight(fixture.mesh.GetBlendShapeIndex(shape));

        private sealed class Fixture:IDisposable
        {
            public readonly GameObject root;
            public readonly Transform skeletalRoot;
            public readonly HasanGarmentPoseCorrectives driver;
            public readonly SkinnedMeshRenderer[] skins;
            public Mesh mesh;
            public Fixture()
            {
                root=new GameObject("Corrective fixture");root.SetActive(false);
                skeletalRoot=new GameObject("Root").transform;skeletalRoot.SetParent(root.transform,false);
                foreach(var side in new[]{"L","R"})
                {
                    var sign=side=="L"?1f:-1f;
                    AddBone("UpperLeg_"+side,new Vector3(sign*.11f,.94f,0));
                    AddBone("LowerLeg_"+side,new Vector3(sign*.15f,.51f,.03f));
                    AddBone("Foot_"+side,new Vector3(sign*.20f,.073f,.019f));
                }
                AddBone("Spine",new Vector3(0,1.03f,-.025f));AddBone("Chest",new Vector3(0,1.17f,-.024f));
                skins=Enumerable.Range(0,3).Select(i=>
                {
                    var child=new GameObject("LOD"+i);child.transform.SetParent(root.transform,false);return child.AddComponent<SkinnedMeshRenderer>();
                }).ToArray();
                driver=root.AddComponent<HasanGarmentPoseCorrectives>();
            }
            public void Configure(params HasanGarmentPoseCorrectives.Sample[] samples)
            {
                mesh=new Mesh{name="Corrective test mesh",vertices=new[]{Vector3.zero,Vector3.up,Vector3.right},triangles=new[]{0,1,2}};
                foreach(var sample in samples)mesh.AddBlendShapeFrame("Pose_"+sample.name,100,new Vector3[3],new Vector3[3],new Vector3[3]);
                mesh.AddBlendShapeFrame("Pose_Orphan",100,new Vector3[3],new Vector3[3],new Vector3[3]);
                mesh.AddBlendShapeFrame("Grip_R",100,new Vector3[3],new Vector3[3],new Vector3[3]);
                foreach(var skin in skins)skin.sharedMesh=mesh;
                driver.samples=samples;driver.Initialize();
            }
            private void AddBone(string name,Vector3 position){var bone=new GameObject(name);bone.transform.SetParent(skeletalRoot,false);bone.transform.localPosition=position;}
            public void Dispose(){Object.DestroyImmediate(root);if(mesh!=null)Object.DestroyImmediate(mesh);}
        }
    }
}
