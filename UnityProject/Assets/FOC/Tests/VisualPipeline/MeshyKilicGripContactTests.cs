using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Editor.Visuals;
using FOC.Presentation.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FOC.Tests.VisualPipeline
{
    /// <summary>Independent queries of the retained leather triangles, never a cylinder proxy.</summary>
    public sealed class MeshyKilicGripContactTests
    {
        private const float RequestedClearance = .002f;

        [Test]
        public void InteriorAnchorIsPushedOutsideActualLeatherSurfaceWithClearance()
        {
            var geometry = RetainedLeather();
            var palm = MeshyKilicGripAttachment.CorrectivePalmInHand;
            Assert.That(Inside(palm, geometry), Is.True, "Known authored leather-ring center.");
            var contact = Contact();
            var pushed = palm + contact.Push(palm, RequestedClearance);
            Assert.That(Inside(pushed, geometry), Is.False);
            Assert.That(SurfaceDistance(pushed, geometry), Is.GreaterThanOrEqualTo(.0019f));
        }

        [Test]
        public void DistantExteriorPointIsNotMoved()
        {
            var geometry = RetainedLeather();
            var exterior = MeshyKilicGripAttachment.CorrectivePalmInHand + Vector3.right * .2f;
            Assert.That(Inside(exterior, geometry), Is.False);
            Assert.That(Contact().Push(exterior, RequestedClearance), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ExactRetainedSurfacePointMustNotSilentlyBypassRequestedClearance()
        {
            var geometry = RetainedLeather();
            var vertex = geometry.sourceVertices.Where((v, i) => geometry.usedIndices.Contains(i))
                .OrderBy(v => Mathf.Abs(v.y - MeshyKilicGripAttachment.SwordLocalGripAnchor.y))
                .ThenByDescending(v => Mathf.Abs(v.z)).First();
            var surface = ToHand(vertex);
            Assert.That(SurfaceDistance(surface, geometry), Is.LessThan(1e-6f));
            var pushed = surface + Contact().Push(surface, RequestedClearance);
            Assert.That(Inside(pushed, geometry), Is.False);
            Assert.That(SurfaceDistance(pushed, geometry), Is.GreaterThanOrEqualTo(.0019f),
                "A zero nearest-direction at the exact boundary is not a valid no-op.");
        }

        [Test]
        public void ResolveKeepsCoincidentSeamsTogetherAndProtectedVerticesUntouched()
        {
            var palm = MeshyKilicGripAttachment.CorrectivePalmInHand;
            var protectedPoint = palm + Vector3.up * .1f;
            var points = new[] { palm, palm, protectedPoint };
            Contact().Resolve(points, new[] { true, true, false }, Array.Empty<int>());
            Assert.That(points[0], Is.EqualTo(points[1]));
            Assert.That(points[2], Is.EqualTo(protectedPoint));
            Assert.That(Inside(points[0], RetainedLeather()), Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void ActualClosedCandidateHasNoChangedVertexInsideRetainedLeather(int lod)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeshyKilicGripCorrectivePipeline.PrefabPath);
            Assert.That(prefab, Is.Not.Null, "Missing candidate fails, not skips.");
            var skin = prefab.GetComponent<LODGroup>().GetLODs()[lod].renderers.OfType<SkinnedMeshRenderer>().Single();
            var mesh = skin.sharedMesh;
            var shape = mesh.GetBlendShapeIndex(MeshyRightHandGripVisual.ShapeName);
            Assert.That(shape, Is.GreaterThanOrEqualTo(0));
            var deltas = new Vector3[mesh.vertexCount];
            mesh.GetBlendShapeFrameVertices(shape, 0, deltas, null, null);
            var vertices = mesh.vertices;
            var hand = Array.FindIndex(skin.bones, bone => bone.name == "RightHand");
            Assert.That(hand, Is.GreaterThanOrEqualTo(0));
            var geometry = RetainedLeather();
            var changed = 0;
            for (var i = 0; i < vertices.Length; i++)
            {
                if (deltas[i].sqrMagnitude < 1e-12f) continue;
                changed++;
                var closed = mesh.bindposes[hand].MultiplyPoint3x4(vertices[i] + deltas[i]);
                Assert.That(Inside(closed, geometry), Is.False, "Actual hilt overlap LOD" + lod + " vertex " + i);
                Assert.That(SurfaceDistance(closed, geometry), Is.GreaterThanOrEqualTo(.0009f),
                    "At least 1mm actual-surface contact margin, including hilt end caps; LOD" + lod + " vertex " + i);
            }
            Assert.That(changed, Is.GreaterThanOrEqualTo(8));
        }

        private static MeshyKilicGripContact Contact() => new MeshyKilicGripContact(
            MeshyKilicGripAttachment.CorrectivePalmInHand, MeshyKilicGripAttachment.CorrectiveWeaponRotationInHand);

        private sealed class Geometry
        {
            public Vector3[] sourceVertices;
            public Vector3[] vertices;
            public int[] triangles;
            public HashSet<int> usedIndices;
        }

        private static Geometry RetainedLeather()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FOC/Presentation/Equipment/Ottoman1648/WPN_Kilic_01.prefab");
            Assert.That(prefab, Is.Not.Null);
            var renderer = prefab.GetComponent<LODGroup>().GetLODs()[0].renderers.Single();
            var material = Array.FindIndex(renderer.sharedMaterials,
                m => m.name.IndexOf("Leather", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.That(material, Is.GreaterThanOrEqualTo(0));
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            var sourceVertices = mesh.vertices;
            var triangles = mesh.GetTriangles(material);
            Assert.That(triangles.Length, Is.GreaterThan(0));
            return new Geometry { sourceVertices = sourceVertices, vertices = sourceVertices.Select(ToHand).ToArray(),
                triangles = triangles, usedIndices = new HashSet<int>(triangles) };
        }

        private static Vector3 ToHand(Vector3 vertex) => MeshyKilicGripAttachment.CorrectivePalmInHand +
            MeshyKilicGripAttachment.CorrectiveWeaponRotationInHand * (vertex - MeshyKilicGripAttachment.SwordLocalGripAnchor);

        private static bool Inside(Vector3 point, Geometry geometry)
        {
            // Irrational-looking ray direction avoids the authored ring seams.
            // Deduplicate equal-distance hits where two triangles share an edge.
            var direction = new Vector3(1f, .371f, .129f).normalized;
            var hits = new List<float>();
            for (var i = 0; i < geometry.triangles.Length; i += 3)
            {
                var a = geometry.vertices[geometry.triangles[i]];
                var b = geometry.vertices[geometry.triangles[i + 1]];
                var c = geometry.vertices[geometry.triangles[i + 2]];
                var edge1 = b - a;
                var edge2 = c - a;
                var h = Vector3.Cross(direction, edge2);
                var determinant = Vector3.Dot(edge1, h);
                if (Mathf.Abs(determinant) < 1e-10f) continue;
                var inverse = 1f / determinant;
                var s = point - a;
                var u = inverse * Vector3.Dot(s, h);
                if (u < 0f || u > 1f) continue;
                var q = Vector3.Cross(s, edge1);
                var v = inverse * Vector3.Dot(direction, q);
                if (v < 0f || u + v > 1f) continue;
                var distance = inverse * Vector3.Dot(edge2, q);
                if (distance > 1e-7f && !hits.Any(hit => Mathf.Abs(hit - distance) < 1e-6f)) hits.Add(distance);
            }
            return hits.Count % 2 != 0;
        }

        private static float SurfaceDistance(Vector3 point, Geometry geometry)
        {
            var minimum = float.PositiveInfinity;
            for (var i = 0; i < geometry.triangles.Length; i += 3)
            {
                var a = geometry.vertices[geometry.triangles[i]];
                var b = geometry.vertices[geometry.triangles[i + 1]];
                var c = geometry.vertices[geometry.triangles[i + 2]];
                minimum = Mathf.Min(minimum, Vector3.Distance(point, ClosestTrianglePoint(point, a, b, c)));
            }
            return minimum;
        }

        private static Vector3 ClosestTrianglePoint(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            var ab = b - a; var ac = c - a; var ap = p - a;
            var d1 = Vector3.Dot(ab, ap); var d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;
            var bp = p - b; var d3 = Vector3.Dot(ab, bp); var d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;
            var vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));
            var cp = p - c; var d5 = Vector3.Dot(ab, cp); var d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;
            var vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));
            var va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
                return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
            var denominator = va + vb + vc;
            if (Mathf.Abs(denominator) < 1e-20f) return a;
            return a + ab * (vb / denominator) + ac * (vc / denominator);
        }
    }
}
