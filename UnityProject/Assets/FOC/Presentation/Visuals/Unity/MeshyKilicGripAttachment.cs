using System;
using UnityEngine;

namespace FOC.Presentation.Visuals
{
    /// <summary>
    /// Presentation-only alignment of the retained kilic's handle to a measured
    /// hand anchor. Attachment stability is not proof of a closed-finger grasp.
    /// </summary>
    public static class MeshyKilicGripAttachment
    {
        public const string CandidateStatus = "CANDIDATE_NOT_VISUAL_GRASP_ACCEPTANCE";
        public static readonly Vector3 SwordLocalGripAnchor = new Vector3(-.006f, -.080f, 0f);
        public static readonly Vector3 CandidatePalmInHand = new Vector3(0f, .061f, .018f);
        public static readonly Quaternion CandidateWeaponRotationInHand = Quaternion.Euler(0f, 0f, -90f);
        // Measured retained-hand frame: palm -X; finger row Z; thumb/index -Z.
        // These belong only to the explicitly selected right-hand corrective.
        public static readonly Vector3 CorrectivePalmInHand = new Vector3(-.026f, .123f, .010f);
        public static readonly Quaternion CorrectiveWeaponRotationInHand = Quaternion.LookRotation(Vector3.left, Vector3.back);

        /// <summary>
        /// The hand must be the actual Animator RightHand supplied by the caller.
        /// Palm and rotation are expressed in that hand's frame, not the socket's.
        /// Socket translation/rotation and the weapon's local scale are retained.
        /// No per-frame repair, rig alteration or gameplay position change occurs.
        /// </summary>
        public static void Apply(Transform weapon, Transform actualHand, Transform socket,
            Vector3 palmInHand, Quaternion weaponRotationInHand)
        {
            RequireTransforms(weapon, actualHand);
            if (socket == null) throw new ArgumentNullException(nameof(socket));
            if (socket.name != "Socket_RightHand" || socket == actualHand || !socket.IsChildOf(actualHand))
                throw new InvalidOperationException("Kilic attachment requires the actual right-hand descendant socket.");
            if (actualHand.IsChildOf(weapon) || socket.IsChildOf(weapon))
                throw new InvalidOperationException("A weapon cannot contain its attachment hand or socket.");
            RequireFinite(palmInHand, nameof(palmInHand));
            RequireFinite(weapon.localScale, "weapon.localScale");
            RequireFinite(weaponRotationInHand);

            // Reparenting preserves the intended local asset scale. Solve the
            // explicit handle anchor, not the guard-origin prefab pivot.
            weapon.SetParent(socket, false);
            weapon.rotation = actualHand.rotation * weaponRotationInHand.normalized;
            var palmWorld = actualHand.TransformPoint(palmInHand);
            var gripOffsetWorld = weapon.TransformVector(SwordLocalGripAnchor);
            weapon.position = palmWorld - gripOffsetWorld;
        }

        public static Vector3 MeasureGripInHand(Transform weapon, Transform actualHand)
        {
            RequireTransforms(weapon, actualHand);
            return actualHand.InverseTransformPoint(weapon.TransformPoint(SwordLocalGripAnchor));
        }

        /// <summary>Drift in the explicit hand frame; values use hand-local units.</summary>
        public static float MeasureDriftInHand(Transform weapon, Transform actualHand, Vector3 palmInHand)
        {
            RequireFinite(palmInHand, nameof(palmInHand));
            return Vector3.Distance(MeasureGripInHand(weapon, actualHand), palmInHand);
        }

        /// <summary>World-space anchor error, in Unity meters regardless of hand scale.</summary>
        public static float MeasureAnchorErrorMeters(Transform weapon, Transform actualHand, Vector3 palmInHand)
        {
            RequireTransforms(weapon, actualHand);
            RequireFinite(palmInHand, nameof(palmInHand));
            return Vector3.Distance(weapon.TransformPoint(SwordLocalGripAnchor), actualHand.TransformPoint(palmInHand));
        }

        private static void RequireTransforms(Transform weapon, Transform actualHand)
        {
            if (weapon == null) throw new ArgumentNullException(nameof(weapon));
            if (actualHand == null) throw new ArgumentNullException(nameof(actualHand));
        }

        private static void RequireFinite(Vector3 value, string name)
        {
            if (!Finite(value.x) || !Finite(value.y) || !Finite(value.z))
                throw new ArgumentException("A grip transform must be finite.", name);
        }

        private static void RequireFinite(Quaternion value)
        {
            if (!Finite(value.x) || !Finite(value.y) || !Finite(value.z) || !Finite(value.w) ||
                value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w < 1e-12f)
                throw new ArgumentException("The hand-relative weapon rotation must be a finite nonzero quaternion.", nameof(value));
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
