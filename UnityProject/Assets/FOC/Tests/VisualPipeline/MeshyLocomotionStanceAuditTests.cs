using System;
using FOC.Presentation.Visuals;
using NUnit.Framework;
using UnityEngine;

namespace FOC.Tests.VisualPipeline
{
    public sealed class MeshyLocomotionStanceAuditTests
    {
        [Test]
        public void ExternalDiagnosticSpeedIsFittedWithoutHidingHeldOutSliding()
        {
            var motion=Fixture();
            var stable=MeshyLocomotionStanceAudit.Measure(motion);
            Assert.That(stable.fittedNativeSpeedMetersPerSecond,Is.EqualTo(1.5f).Within(.00001f));
            Assert.That(stable.maximumHeldOutHorizontalDrift,Is.LessThan(.00001f));
            foreach(var window in stable.windows)Assert.That(window.heldOutSamples,Is.GreaterThan(0));
            for(var i=21;i<=49;i+=2)motion.frames[i].leftToeCenter+=Vector3.right*.04f;
            var sliding=MeshyLocomotionStanceAudit.Measure(motion);
            Assert.That(sliding.fittedNativeSpeedMetersPerSecond,Is.EqualTo(stable.fittedNativeSpeedMetersPerSecond).Within(.00001f));
            Assert.That(sliding.maximumHeldOutHorizontalDrift,Is.GreaterThanOrEqualTo(.03999f));
            Assert.That(sliding.status,Does.Contain("NOT_AUTOMATIC_VISUAL_ACCEPTANCE"));
        }
        [Test]
        public void MissingOrNonfiniteSupportCannotBecomeAnAcceptancePass()
        {
            var motion=Fixture();
            foreach(var frame in motion.frames)frame.leftHeel=frame.leftToe=.1f;
            Assert.That(MeshyLocomotionStanceAudit.Measure(motion).status,Is.EqualTo("NO_TWO_SIDED_RELIABLE_SUPPORT_WINDOWS"));
            motion.frames[30].rightToeCenter=new Vector3(float.NaN,0,0);
            Assert.Throws<ArgumentException>(()=>MeshyLocomotionStanceAudit.Measure(motion));
        }
        private static MeshyHasanPilotPlayer.LocomotionContactMeasurement Fixture()
        {
            var motion=new MeshyHasanPilotPlayer.LocomotionContactMeasurement{clip="Synthetic diagnostic only",durationSeconds=1.2f,
                frames=new MeshyHasanPilotPlayer.LocomotionContactFrame[121]};
            for(var i=0;i<=120;i++)
            {
                var point=Vector3.back*1.5f*i/120f*motion.durationSeconds;
                motion.frames[i]=new MeshyHasanPilotPlayer.LocomotionContactFrame{phase=i/120f,
                    leftHeel=i>=20&&i<=50?.001f:.1f,leftToe=i>=20&&i<=50?.001f:.1f,
                    rightHeel=i>=80&&i<=110?.001f:.1f,rightToe=i>=80&&i<=110?.001f:.1f,
                    leftHeelCenter=point,leftToeCenter=point,rightHeelCenter=point,rightToeCenter=point};
            }
            return motion;
        }
    }
}
