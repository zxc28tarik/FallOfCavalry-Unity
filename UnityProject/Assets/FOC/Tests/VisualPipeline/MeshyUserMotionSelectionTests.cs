using System;
using FOC.Editor.Visuals;
using FOC.Presentation.Visuals;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FOC.Tests.VisualPipeline
{
    public sealed class MeshyUserMotionSelectionTests
    {
        [Test] public void UserSourceDoesNotOverlapProductionOrOriginalMeshyAssetRoots()
        {
            Assert.That(MeshyUserMotionIntake.SourceRoot,Does.Contain("/TestResults/"));
            Assert.That(MeshyUserMotionIntake.SourceRoot.StartsWith(MeshyMotionLibraryIntake.SourceRoot+"/",StringComparison.Ordinal),Is.False);
            Assert.That(MeshyUserMotionIntake.SourceRoot.StartsWith(MeshyHasanPilotPipeline.IntakeRoot+"/",StringComparison.Ordinal),Is.False);
            CollectionAssert.AreEqual(new[]{"Idle_12","Right_Hand_Sword_Slash"},MeshyUserMotionIntake.Names);
        }
        [TestCase("Crouch")][TestCase("Mounted")][TestCase("Walking")][TestCase("Right_Hand_Sword_Slash|baselayer")]
        public void InitialReviewCannotSilentlySelectExtraOrSubstringNamedMotion(string name)
        {
            var owner=new GameObject("Motion selector test");
            try {Assert.Throws<ArgumentException>(()=>owner.AddComponent<MeshyHasanPilotPlayer>().SelectedUserMotion(name));}
            finally {Object.DestroyImmediate(owner);}
        }
        [Test] public void MissingClipFailsInsteadOfFallingBackToDiagnosticAttack()
        {
            var owner=new GameObject("Motion selector test");
            try {Assert.Throws<InvalidOperationException>(()=>owner.AddComponent<MeshyHasanPilotPlayer>().SelectedUserMotion("Right_Hand_Sword_Slash"));}
            finally {Object.DestroyImmediate(owner);}
        }
        [Test] public void NamedGenericClipCannotPretendToBeActualHumanoidMotion()
        {
            var owner=new GameObject("Motion selector test");var clip=new AnimationClip{name="Right_Hand_Sword_Slash"};
            try
            {
                var player=owner.AddComponent<MeshyHasanPilotPlayer>();player.userMotionClips=new[]{clip};
                Assert.Throws<InvalidOperationException>(()=>player.SelectedUserMotion("Right_Hand_Sword_Slash"));
                player.userMotionClips=new[]{clip,clip};
                Assert.Throws<InvalidOperationException>(()=>player.SelectedUserMotion("Right_Hand_Sword_Slash"));
            }
            finally {Object.DestroyImmediate(clip);Object.DestroyImmediate(owner);}
        }
    }
}
