#nullable enable
using System;
using System.Linq;
using FOC.Editor.Visuals;
using NUnit.Framework;
using UnityEngine;
using Support=FOC.Editor.Visuals.MeshyTargetContact.Support;

namespace FOC.Tests.VisualPipeline
{
    /// <summary>Pure contact-policy/solver integrity, not production contact acceptance.</summary>
    public sealed class MeshyTargetContactTests
    {
        private static MeshyTargetContact.ContactPolicy Grounded()=>new MeshyTargetContact.ContactPolicy
        {
            id="test-explicit-grounded",loop=true,requireHeelAndToeSupport=true,
            supportKeys=new[]{new MeshyTargetContact.SupportKey{phase=0,support=Support.Both},new MeshyTargetContact.SupportKey{phase=1,support=Support.Both}}
        };

        [Test] public void GroundedCorrectionFollowsMeasuredPerPhasePenetrationNotGlobalLift()
        {
            var left=new[]{-.01f,-.03f,.02f,-.01f};var right=new[]{.004f,-.01f,.03f,.004f};var support=Enumerable.Repeat(Support.Both,4).ToArray();
            var correction=MeshyTargetContact.SolveCorrections(left,right,support,true,0,.12f);
            Assert.That(correction,Is.EqualTo(new[]{.01f,.03f,-.02f,.01f}).Within(.000001f));
            Assert.That(correction.Distinct().Count(),Is.GreaterThan(1));
            for(var i=0;i<left.Length;i++)Assert.That(Mathf.Min(left[i],right[i])+correction[i],Is.EqualTo(0).Within(.000001f));
        }

        [Test] public void TwoFootRootingDoesNotPretendTheHigherFootAlsoPlants()
        {
            var correction=MeshyTargetContact.SolveCorrections(new[]{-.03f,-.03f,-.03f},new[]{.02f,.02f,.02f},new[]{Support.Both,Support.Both,Support.Both},true,0,.12f);
            Assert.That(-.03f+correction[1],Is.EqualTo(0).Within(.000001f));
            Assert.That(.02f+correction[1],Is.EqualTo(.05f).Within(.000001f),"A body-Y correction alone leaves genuine opposite-foot hover; later audit must FAIL it.");
        }

        [Test] public void AirborneRunPreservesFlightRatherThanSnappingLowestFootDown()
        {
            var left=new[]{-.02f,.12f,.28f,.11f,-.02f};var right=new[]{.05f,.15f,.24f,.16f,.05f};var support=new[]{Support.Left,Support.Airborne,Support.Airborne,Support.Airborne,Support.Left};
            var correction=MeshyTargetContact.SolveCorrections(left,right,support,true,0,.12f);
            Assert.That(correction.All(v=>Mathf.Abs(v-.02f)<.000001f),Is.True);
            Assert.That(Mathf.Min(left[2],right[2])+correction[2],Is.GreaterThan(.2f));
        }

        [Test] public void AirborneBaselineInterpolatesNeighbouringSupportCorrectionsDeterministically()
        {
            var left=new[]{-.01f,.2f,.3f,.2f,-.03f};var right=new[]{.04f,.24f,.34f,.22f,.04f};var support=new[]{Support.Left,Support.Airborne,Support.Airborne,Support.Airborne,Support.Left};
            var first=MeshyTargetContact.SolveCorrections(left,right,support,false,0,.12f);var second=MeshyTargetContact.SolveCorrections(left,right,support,false,0,.12f);
            Assert.That(first,Is.EqualTo(second));Assert.That(first[0],Is.EqualTo(.01f).Within(.000001f));Assert.That(first[2],Is.EqualTo(.02f).Within(.000001f));Assert.That(first[4],Is.EqualTo(.03f).Within(.000001f));
            Assert.That(first[1],Is.GreaterThan(.01f).And.LessThan(.02f));Assert.That(first[3],Is.GreaterThan(.02f).And.LessThan(.03f));
        }

        [Test] public void LoopEndpointDisagreementIsMeasuredNotOverwrittenToFakeSeamlessContact()
        {
            var correction=MeshyTargetContact.SolveCorrections(new[]{-.01f,-.02f,-.04f},new[]{.03f,.02f,.03f},new[]{Support.Left,Support.Left,Support.Left},true,0,.12f);
            Assert.That(Mathf.Abs(correction[2]-correction[0]),Is.EqualTo(.03f).Within(.000001f));
        }

        [Test] public void AllAirborneInputCannotInventGroundSupport()
        {
            Assert.Throws<InvalidOperationException>(()=>MeshyTargetContact.SolveCorrections(new[]{.2f,.3f,.2f},new[]{.2f,.4f,.2f},new[]{Support.Airborne,Support.Airborne,Support.Airborne},true,0,.12f));
        }

        [Test] public void ImplausiblyLargeCorrectionRejectsRatherThanHidesBadAvatarOrSource()
        {
            Assert.Throws<InvalidOperationException>(()=>MeshyTargetContact.SolveCorrections(new[]{-.5f,-.4f,-.5f},new[]{0f,0f,0f},new[]{Support.Both,Support.Both,Support.Both},true,0,.12f));
        }

        [Test] public void NonfiniteContactMeasurementIsNotSilentlyClamped()
        {
            Assert.Throws<InvalidOperationException>(()=>MeshyTargetContact.SolveCorrections(new[]{0f,float.NaN,0f},new[]{0f,0f,0f},new[]{Support.Both,Support.Both,Support.Both},true,0,.12f));
        }

        [Test] public void ContactScheduleRequiresExplicitOrderedEndpointsAndLoopSupportAgreement()
        {
            var policy=Grounded();Assert.DoesNotThrow(()=>MeshyTargetContact.ValidatePolicy(policy));
            policy.supportKeys[1].phase=.9f;Assert.Throws<InvalidOperationException>(()=>MeshyTargetContact.ValidatePolicy(policy));
            policy.supportKeys[1].phase=1;policy.supportKeys[1].support=Support.Right;Assert.Throws<InvalidOperationException>(()=>MeshyTargetContact.ValidatePolicy(policy));
        }

        [Test] public void RunScheduleKeepsExplicitAirborneIntervalAndBoundaryOwnership()
        {
            var policy=Grounded();policy.requireHeelAndToeSupport=false;policy.supportKeys=new[]{new MeshyTargetContact.SupportKey{phase=0,support=Support.Left},new MeshyTargetContact.SupportKey{phase=.2f,support=Support.Airborne},new MeshyTargetContact.SupportKey{phase=.5f,support=Support.Right},new MeshyTargetContact.SupportKey{phase=.7f,support=Support.Airborne},new MeshyTargetContact.SupportKey{phase=1,support=Support.Left}};
            MeshyTargetContact.ValidatePolicy(policy);Assert.That(MeshyTargetContact.SupportAt(policy,.19f),Is.EqualTo(Support.Left));Assert.That(MeshyTargetContact.SupportAt(policy,.2f),Is.EqualTo(Support.Airborne));Assert.That(MeshyTargetContact.SupportAt(policy,.5f),Is.EqualTo(Support.Right));Assert.That(MeshyTargetContact.SupportAt(policy,.7f),Is.EqualTo(Support.Airborne));
        }

        [Test] public void LowerSwingFootCollisionCannotBeHiddenByOnlyReadingSupportFoot()
        {
            var correction=MeshyTargetContact.SolveCorrections(new[]{0f,0f,0f},new[]{-.025f,-.025f,-.025f},new[]{Support.Left,Support.Left,Support.Left},true,0,.12f);
            Assert.That(correction[1],Is.EqualTo(.025f).Within(.000001f));Assert.That(correction[1],Is.GreaterThan(.002f),"Supported foot now hovers and must FAIL the later quality audit; no IK is faked.");
        }

        [Test] public void ExternalComparisonMotionMustBePlanarAndNeverSmugglesHeightCorrection()
        {
            var policy=Grounded();policy.externalPlanarMotionKnown=true;policy.expectedPlanarDisplacementPerCycle=new Vector3(0,.1f,1f);Assert.Throws<InvalidOperationException>(()=>MeshyTargetContact.ValidatePolicy(policy));
        }
    }
}
