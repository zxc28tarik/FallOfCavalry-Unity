using System;
using System.Linq;
using FOC.Presentation.Visuals;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FOC.Tests.VisualPipeline
{
    public sealed class MeshyHasanPilotBenchmarkTests
    {
        [Test]
        public void TriangleInventoryCountsTriangleTopologyOnly()
        {
            var mesh = new Mesh();
            try
            {
                mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.one };
                mesh.subMeshCount = 2;
                mesh.SetIndices(new[] { 0, 1, 2 }, MeshTopology.Triangles, 0);
                mesh.SetIndices(new[] { 2, 3 }, MeshTopology.Lines, 1);
                Assert.That(MeshyHasanPilotBenchmark.TriangleCount(mesh), Is.EqualTo(1));
                Assert.That(MeshyHasanPilotBenchmark.TriangleCount(null), Is.Zero);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void GeometryInventorySeparatesPerActorLodCostFromSharedAssetMemory()
        {
            var host = new GameObject("Benchmark inventory test host");
            var nearMesh = new Mesh(); var farMesh = new Mesh();
            var texture = new Texture2D(2, 2);
            var shader = Shader.Find("Standard");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader) { mainTexture = texture };
            try
            {
                nearMesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.one };
                nearMesh.triangles = new[] { 0, 1, 2, 1, 3, 2 };
                farMesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
                farMesh.triangles = new[] { 0, 1, 2 };
                var views = Enumerable.Range(0, 2).Select(i =>
                {
                    var view = new GameObject("Actor" + i).AddComponent<VisualSoldier3D>();
                    view.transform.SetParent(host.transform, false);
                    var near = Skin(view.transform, "LOD0", nearMesh, material);
                    var far = Skin(view.transform, "LOD1", farMesh, material);
                    view.gameObject.AddComponent<LODGroup>().SetLODs(new[] { new LOD(.6f, new Renderer[] { near }), new LOD(.1f, new Renderer[] { far }) });
                    return view;
                }).ToArray();
                var inventory = MeshyHasanPilotBenchmark.InspectGeometry(views);
                Assert.That(inventory.actorCount, Is.EqualTo(2));
                Assert.That(inventory.rendererComponents, Is.EqualTo(4));
                Assert.That(inventory.skinnedRendererComponents, Is.EqualTo(4));
                Assert.That(inventory.materialSlots, Is.EqualTo(4));
                Assert.That(inventory.uniqueMaterials, Is.EqualTo(1));
                Assert.That(inventory.uniqueTextures, Is.EqualTo(1));
                Assert.That(inventory.uniqueMeshes, Is.EqualTo(2));
                Assert.That(inventory.lods.Select(lod => lod.level), Is.EqualTo(new[] { 0, 1 }));
                Assert.That(inventory.lods.Select(lod => lod.triangles), Is.EqualTo(new long[] { 4, 2 }));
                Assert.That(inventory.lods.All(lod => lod.rendererReferences == 2), Is.True);
                Assert.That(inventory.unityReportedSharedTextureMemoryBytes, Is.GreaterThan(0));
                Assert.That(inventory.unityReportedSharedMeshMemoryBytes, Is.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(host); Object.DestroyImmediate(material);
                Object.DestroyImmediate(texture); Object.DestroyImmediate(nearMesh); Object.DestroyImmediate(farMesh);
            }
        }

        [Test]
        public void UnavailableTimingIsNotSerializedAsZeroCostOrAvailable()
        {
            var timing = new MeshyHasanPilotBenchmark.FrameSamples();
            Assert.That(timing.cpuFrameTimingAvailable || timing.gpuFrameTimingAvailable || timing.unityFrameTimingFeatureEnabled, Is.False);
            Assert.That(timing.meanCpuFrameMilliseconds, Is.EqualTo(-1));
            Assert.That(timing.meanGpuFrameMilliseconds, Is.EqualTo(-1));
            Assert.That(new MeshyHasanPilotBenchmark.Operation().managedAllocatedBytes, Is.EqualTo(-1));
            var json = JsonUtility.ToJson(new MeshyHasanPilotBenchmark.Report());
            Assert.That(json, Does.Contain("TECHNICAL_BENCHMARK_NOT_PRODUCTION_ACCEPTANCE"));
        }

        private static SkinnedMeshRenderer Skin(Transform parent, string name, Mesh mesh, Material material)
        {
            var skin = new GameObject(name).AddComponent<SkinnedMeshRenderer>();
            skin.transform.SetParent(parent, false); skin.sharedMesh = mesh; skin.sharedMaterial = material;
            return skin;
        }
    }
}
