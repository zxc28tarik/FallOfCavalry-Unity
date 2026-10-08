using System;
using FOC.Presentation.Visuals;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FOC.Tests.VisualPipeline
{
    public sealed class MeshyKilicGripAttachmentTests
    {
        private GameObject owner;
        private Transform hand;
        private Transform socket;
        private Transform weapon;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Grip attachment test");
            hand = Child(owner.transform, "RightHand");
            socket = Child(hand, "Socket_RightHand");
            weapon = Child(owner.transform, "Kilic");
            owner.transform.SetPositionAndRotation(new Vector3(3f, 2f, -4f), Quaternion.Euler(13f, 37f, -11f));
            hand.localPosition = new Vector3(.42f, 1.12f, .18f);
            hand.localRotation = Quaternion.Euler(41f, -23f, 17f);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(owner);

        [Test]
        public void CandidateUsesHandleAnchorRatherThanGuardPivotAndDoesNotClaimGraspAcceptance()
        {
            ApplyCandidate();
            Assert.That(MeshyKilicGripAttachment.MeasureDriftInHand(weapon, hand,
                MeshyKilicGripAttachment.CandidatePalmInHand), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(weapon.position,
                hand.TransformPoint(MeshyKilicGripAttachment.CandidatePalmInHand)), Is.GreaterThan(.07f));
            Assert.That(MeshyKilicGripAttachment.CandidateStatus, Is.EqualTo("CANDIDATE_NOT_VISUAL_GRASP_ACCEPTANCE"));
        }

        [Test]
        public void TranslatedRotatedSocketCannotMoveHandleAwayFromActualHandAnchor()
        {
            socket.localPosition = new Vector3(.032f, -.015f, .049f);
            socket.localRotation = Quaternion.Euler(-18f, 59f, 27f);
            ApplyCandidate();
            Assert.That(weapon.parent, Is.SameAs(socket));
            Assert.That(MeshyKilicGripAttachment.MeasureAnchorErrorMeters(weapon, hand,
                MeshyKilicGripAttachment.CandidatePalmInHand), Is.LessThan(1e-5f));
            Assert.That(Quaternion.Angle(weapon.rotation, hand.rotation *
                MeshyKilicGripAttachment.CandidateWeaponRotationInHand), Is.LessThan(.01f));
        }

        [TestCase(.5f, .5f, .5f)]
        [TestCase(.6f, 1.4f, .8f)]
        [TestCase(1.25f, .75f, 1.1f)]
        public void NonunitWeaponScaleIsRetainedAndIncludedInGripSolution(float x, float y, float z)
        {
            var scale = new Vector3(x, y, z);
            weapon.localScale = scale;
            ApplyCandidate();
            Assert.That(Vector3.Distance(weapon.localScale, scale), Is.LessThan(1e-6f));
            Assert.That(MeshyKilicGripAttachment.MeasureAnchorErrorMeters(weapon, hand,
                MeshyKilicGripAttachment.CandidatePalmInHand), Is.LessThan(1e-5f));
        }

        [Test]
        public void NonunitSocketScaleIsIncludedWithoutChangingSocketOrHand()
        {
            socket.localScale = new Vector3(.7f, 1.2f, .85f);
            socket.localRotation = Quaternion.Euler(14f, 26f, -35f);
            var handPosition = hand.localPosition;
            var socketRotation = socket.localRotation;
            var socketScale = socket.localScale;
            ApplyCandidate();
            Assert.That(MeshyKilicGripAttachment.MeasureAnchorErrorMeters(weapon, hand,
                MeshyKilicGripAttachment.CandidatePalmInHand), Is.LessThan(1e-5f));
            Assert.That(hand.localPosition, Is.EqualTo(handPosition));
            Assert.That(socket.localRotation, Is.EqualTo(socketRotation));
            Assert.That(socket.localScale, Is.EqualTo(socketScale));
        }

        [Test]
        public void LeftNamedSocketIsRejectedEvenIfPlacedBelowActualRightHand()
        {
            socket.name = "Socket_LeftHand";
            var originalParent = weapon.parent;
            Assert.Throws<InvalidOperationException>(ApplyCandidate);
            Assert.That(weapon.parent, Is.SameAs(originalParent));
        }

        [Test]
        public void RightNamedSocketOnOtherHandIsRejected()
        {
            var leftHand = Child(owner.transform, "LeftHand");
            socket.SetParent(leftHand, false);
            Assert.Throws<InvalidOperationException>(ApplyCandidate);
        }

        [Test]
        public void GripStaysInSameHandFrameUnderAnimatedTransformsWithoutReapplying()
        {
            socket.localPosition = new Vector3(.026f, -.011f, .034f);
            socket.localRotation = Quaternion.Euler(17f, -28f, 49f);
            weapon.localScale = new Vector3(.8f, 1.1f, .9f);
            ApplyCandidate();
            var localPosition = weapon.localPosition;
            var localRotation = weapon.localRotation;
            for (var phase = 0; phase < 12; phase++)
            {
                hand.localPosition = new Vector3(.25f + phase * .015f, .8f + phase * .05f, -.3f + phase * .021f);
                hand.localRotation = Quaternion.Euler(phase * 29f, phase * -17f, phase * 11f);
                owner.transform.rotation = Quaternion.Euler(phase * 3f, phase * 7f, 0f);
                Assert.That(MeshyKilicGripAttachment.MeasureDriftInHand(weapon, hand,
                    MeshyKilicGripAttachment.CandidatePalmInHand), Is.LessThan(1e-5f), "phase " + phase);
                Assert.That(weapon.localPosition, Is.EqualTo(localPosition));
                Assert.That(weapon.localRotation, Is.EqualTo(localRotation));
            }
        }

        [Test]
        public void ExplicitMeasuredPalmAndRotationCanReplaceCandidateWithoutChangingHelper()
        {
            var palm = new Vector3(.009f, .072f, -.021f);
            var rotation = Quaternion.Euler(12f, 34f, -85f);
            MeshyKilicGripAttachment.Apply(weapon, hand, socket, palm, rotation);
            Assert.That(Vector3.Distance(MeshyKilicGripAttachment.MeasureGripInHand(weapon, hand), palm), Is.LessThan(1e-5f));
        }

        [Test]
        public void NonfiniteAnchorAndZeroRotationFailBeforeReparenting()
        {
            var originalParent = weapon.parent;
            Assert.Throws<ArgumentException>(() => MeshyKilicGripAttachment.Apply(weapon, hand, socket,
                new Vector3(float.NaN, 0f, 0f), Quaternion.identity));
            Assert.Throws<ArgumentException>(() => MeshyKilicGripAttachment.Apply(weapon, hand, socket,
                Vector3.zero, new Quaternion(0f, 0f, 0f, 0f)));
            Assert.That(weapon.parent, Is.SameAs(originalParent));
        }

        private void ApplyCandidate() => MeshyKilicGripAttachment.Apply(weapon, hand, socket,
            MeshyKilicGripAttachment.CandidatePalmInHand, MeshyKilicGripAttachment.CandidateWeaponRotationInHand);

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }
    }
}
