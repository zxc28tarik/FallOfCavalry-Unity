#nullable enable
using System;
using UnityEngine;

namespace FOC.Presentation.Visuals
{
    /// <summary>
    /// Presentation-only closure of the approved derivative right-hand shape.
    /// The original source mesh/rig is never modified, and other shapes belong
    /// to their own systems. Equipping a weapon is not itself visual acceptance.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10010)]
    public sealed class MeshyRightHandGripVisual : MonoBehaviour
    {
        public const string ShapeName = "Grip_Kilic_R";
        public const int RequiredLodCount = 3;

        [SerializeField] private SkinnedMeshRenderer[] skins = Array.Empty<SkinnedMeshRenderer>();
        private Mesh[] meshes = Array.Empty<Mesh>();
        private int[] shapeIndices = Array.Empty<int>();
        private bool initialized;
        private bool kilicEquipped;

        public bool KilicEquipped => kilicEquipped;

        /// <summary>
        /// Cache the three approved derivative LODs atomically. No cloned meshes,
        /// new renderers or per-frame discovery are needed at runtime.
        /// </summary>
        public void Configure(SkinnedMeshRenderer[] renderers)
        {
            if (renderers == null) throw new ArgumentNullException(nameof(renderers));
            var copied = (SkinnedMeshRenderer[])renderers.Clone();
            Validate(copied, out var newMeshes, out var newIndices);
            ResetForPool();
            skins = copied;
            meshes = newMeshes;
            shapeIndices = newIndices;
            initialized = true;
            ResetForPool();
        }

        /// <summary>Only the separate kılıç attachment may request closure.</summary>
        public void SetKilicEquipped(bool equipped)
        {
            EnsureInitialized();
            ValidateCachedBindings();
            kilicEquipped = equipped;
            ApplyCurrentState();
        }

        /// <summary>
        /// Reapply after animation evaluation without allocations. This writes
        /// one shape weight per LOD, not bone transforms or source vertices.
        /// </summary>
        public void ApplyCurrentState()
        {
            EnsureInitialized();
            ValidateCachedBindings();
            var weight = kilicEquipped ? 100f : 0f;
            for (var i = 0; i < skins.Length; i++) skins[i].SetBlendShapeWeight(shapeIndices[i], weight);
        }

        /// <summary>
        /// Called on assembler acquire/return as well as activation transitions.
        /// Clears the flag too, so a later LateUpdate cannot resurrect a grip.
        /// </summary>
        public void ResetForPool()
        {
            kilicEquipped = false;
            // AddComponent can run OnEnable before a pipeline has configured it.
            // An unconfigured component remains inert until explicitly used.
            if (!initialized && skins.Length == 0) return;
            EnsureInitialized();
            ValidateCachedBindings();
            for (var i = 0; i < skins.Length; i++) skins[i].SetBlendShapeWeight(shapeIndices[i], 0f);
        }

        private void OnEnable() => ResetForPool();
        private void OnDisable() => ResetForPool();

        private void LateUpdate()
        {
            if (initialized || skins.Length != 0) ApplyCurrentState();
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            Validate(skins, out var foundMeshes, out var foundIndices);
            meshes = foundMeshes;
            shapeIndices = foundIndices;
            initialized = true;
        }

        private void ValidateCachedBindings()
        {
            // Validate all targets before any write, avoiding a partly closed
            // character if a derivative mesh was unexpectedly swapped later.
            for (var i = 0; i < skins.Length; i++)
                if (skins[i] == null || skins[i].sharedMesh != meshes[i] ||
                    meshes[i] == null || shapeIndices[i] >= meshes[i].blendShapeCount)
                    throw new InvalidOperationException("Kilic grip derivative binding changed; reconfigure explicitly.");
        }

        private static void Validate(SkinnedMeshRenderer[] renderers, out Mesh[] foundMeshes, out int[] foundIndices)
        {
            if (renderers.Length != RequiredLodCount)
                throw new InvalidOperationException("Kilic grip requires exactly three approved derivative LOD renderers.");
            foundMeshes = new Mesh[RequiredLodCount];
            foundIndices = new int[RequiredLodCount];
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.sharedMesh == null)
                    throw new InvalidOperationException("Kilic grip derivative renderer/mesh is missing.");
                for (var previous = 0; previous < i; previous++)
                    if (renderers[previous] == renderer)
                        throw new InvalidOperationException("Kilic grip LOD renderers must be distinct.");
                var mesh = renderer.sharedMesh;
                var index = mesh.GetBlendShapeIndex(ShapeName);
                if (index < 0)
                    throw new InvalidOperationException("Kilic grip requires the approved derivative shape " + ShapeName + ".");
                foundMeshes[i] = mesh;
                foundIndices[i] = index;
            }
        }
    }
}
