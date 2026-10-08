#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FOC.Presentation.Visuals
{
    /// <summary>
    /// Isolated mounted-pilot tack fitting. Retains the horse and saddle geometry;
    /// replaces the old rigid reins with hand-to-bit connections. Source assets
    /// are never changed. All coordinates below are measured in the retained
    /// horse's bind frame, not in world space.
    /// </summary>
    public sealed class MeshyMountedTackBinding : IDisposable
    {
        public static readonly Vector3 SeatBind = new Vector3(0, 1.81f, -.42f);
        public const float StirrupRise = .14f;
        public static readonly Vector3 LeftBitBind = new Vector3(-.083f, 1.405f, 1.335f);
        public static readonly Vector3 RightBitBind = new Vector3(.083f, 1.405f, 1.335f);
        private readonly GameObject harness;
        private readonly Transform head;
        private readonly Vector3 leftBit, rightBit;
        private readonly LineRenderer leftRein, rightRein;
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private readonly List<GameObject> ownedObjects = new List<GameObject>();
        private readonly Vector3[] reinPoints = new Vector3[9];
        public int RemovedRigidReinTriangles { get; private set; }
        public float LeftReinEndpointError { get; private set; }
        public float RightReinEndpointError { get; private set; }

        public MeshyMountedTackBinding(GameObject horse, GameObject retainedHarness)
        {
            harness = retainedHarness;
            head = horse.GetComponentsInChildren<Transform>(true).Single(t => t.name == "MountHead");
            var spine = horse.GetComponentsInChildren<Transform>(true).Single(t => t.name == "MountSpine");
            var leather = harness.GetComponentsInChildren<MeshRenderer>(true).First().sharedMaterials
                .Single(m => m.name.IndexOf("Leather", StringComparison.OrdinalIgnoreCase) >= 0);
            // Tack is authored in horse-root bind coordinates. Preserve its
            // current world fit while binding it to the animated trunk.
            harness.transform.SetParent(spine, true);
            foreach (var filter in harness.GetComponentsInChildren<MeshFilter>(true))
            {
                var leatherIndex = Array.IndexOf(filter.GetComponent<MeshRenderer>().sharedMaterials, leather);
                var copy = StripRigidReins(filter.sharedMesh, leatherIndex, out var removed);
                var ironIndex = Array.FindIndex(filter.GetComponent<MeshRenderer>().sharedMaterials,
                    m => m.name.IndexOf("Steel",StringComparison.OrdinalIgnoreCase)>=0);
                FitStirrupLength(copy,leatherIndex,ironIndex,StirrupRise);
                RemovedRigidReinTriangles += removed;
                ownedMeshes.Add(copy); filter.sharedMesh = copy;
            }
            if (RemovedRigidReinTriangles == 0)
                throw new InvalidOperationException("Retained harness rigid reins were not identified; refusing duplicate reins.");
            leftBit = head.InverseTransformPoint(horse.transform.TransformPoint(LeftBitBind));
            rightBit = head.InverseTransformPoint(horse.transform.TransformPoint(RightBitBind));
            leftRein = MakeLine("Rein_Left_HandToBit", harness.transform, leather, .009f, true);
            rightRein = MakeLine("Rein_Right_HandToBit", harness.transform, leather, .009f, true);
            // Noseband and cheek straps follow the original head bone. The
            // old rigid rein ended beyond the muzzle and had no bit connection.
            MakeHeadStrap("Noseband", horse.transform, leather, .018f, new[] {
                new Vector3(-.083f,1.405f,1.335f),new Vector3(-.067f,1.465f,1.355f),
                new Vector3(0,1.483f,1.371f),new Vector3(.067f,1.465f,1.355f),
                new Vector3(.083f,1.405f,1.335f),new Vector3(.063f,1.348f,1.29f),
                new Vector3(0,1.338f,1.285f),new Vector3(-.063f,1.348f,1.29f),
                new Vector3(-.083f,1.405f,1.335f) });
            foreach (var side in new[] {-1f, 1f})
                MakeHeadStrap(side < 0 ? "Cheek_Left" : "Cheek_Right", horse.transform, leather, .016f, new[] {
                    new Vector3(side*.083f,1.405f,1.335f),new Vector3(side*.146f,1.64f,1.225f),
                    new Vector3(side*.158f,1.85f,1.11f),new Vector3(side*.10f,1.965f,1.015f),
                    new Vector3(0,1.992f,.99f) });
        }

        public static Mesh StripRigidReins(Mesh source, int leatherSubmesh, out int removedTriangles)
        {
            if (source == null || !source.isReadable) throw new InvalidOperationException("Readable retained tack mesh required.");
            if (leatherSubmesh < 0 || leatherSubmesh >= source.subMeshCount) throw new ArgumentOutOfRangeException(nameof(leatherSubmesh));
            var copy = UnityEngine.Object.Instantiate(source); copy.name = source.name + "_MountedFit_NoRigidReins";
            var vertices = source.vertices; removedTriangles = 0;
            for (var sub = 0; sub < source.subMeshCount; sub++)
            {
                if (sub != leatherSubmesh) continue; // Never trim the fitted cloth blanket or stirrup iron.
                var indices = source.GetTriangles(sub); var keep = new List<int>(indices.Length);
                for (var i = 0; i < indices.Length; i += 3)
                {
                    // Audited retained authoring: reins alone occupy z>-.14,
                    // y>1.49. Saddle front ends at z=-.15; girth/stirrups behind.
                    bool Rein(int index) => vertices[index].z > -.14f && vertices[index].y > 1.49f;
                    if (Rein(indices[i]) && Rein(indices[i+1]) && Rein(indices[i+2])) { removedTriangles++; continue; }
                    keep.Add(indices[i]); keep.Add(indices[i+1]); keep.Add(indices[i+2]);
                }
                copy.SetTriangles(keep, sub, false);
            }
            copy.RecalculateBounds(); return copy;
        }

        public static void FitStirrupLength(Mesh mesh,int leatherSubmesh,int ironSubmesh,float rise)
        {
            if(ironSubmesh<0||ironSubmesh>=mesh.subMeshCount)throw new InvalidOperationException("Retained stirrup iron material missing.");
            if(float.IsNaN(rise)||float.IsInfinity(rise)||rise<0||rise>.20f)throw new ArgumentOutOfRangeException(nameof(rise));
            var positions=mesh.vertices;var original=(Vector3[])positions.Clone();
            // The original export duplicates vertices at face/UV boundaries.
            // Weld position keys only for component classification, never alter
            // topology. This distinguishes the two leathers from saddle/girth.
            var parent=Enumerable.Range(0,positions.Length).ToArray();
            int Root(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
            void Union(int a,int b){a=Root(a);b=Root(b);if(a!=b)parent[b]=a;}
            var keys=new Dictionary<Vector3Int,int>();
            var leatherIndices=mesh.GetTriangles(leatherSubmesh);
            foreach(var i in leatherIndices)
            {
                var key=Vector3Int.RoundToInt(positions[i]*100000f);
                if(keys.TryGetValue(key,out var prior))Union(i,prior);else keys.Add(key,i);
            }
            for(var i=0;i<leatherIndices.Length;i+=3){Union(leatherIndices[i],leatherIndices[i+1]);Union(leatherIndices[i],leatherIndices[i+2]);}
            var leathers=0;
            foreach(var component in leatherIndices.Distinct().GroupBy(Root))
            {
                var indices=component.ToArray();var bounds=new Bounds(positions[indices[0]],Vector3.zero);
                foreach(var i in indices)bounds.Encapsulate(positions[i]);
                if(bounds.min.y<1.20f&&bounds.max.y>1.79f&&bounds.min.z>-.425f&&bounds.max.z<-.25f&&Mathf.Abs(bounds.center.x)>.2f)
                {
                    leathers++;
                    foreach(var i in indices)positions[i].y+=rise*Mathf.Clamp01((1.81f-original[i].y)/.66f);
                }
            }
            if(leathers!=2)throw new InvalidOperationException("Expected exactly two retained stirrup leathers, found "+leathers);
            foreach(var i in mesh.GetTriangles(ironSubmesh).Distinct())positions[i].y+=rise;
            // Keep authored split normals. Recalculating all normals on this
            // face-expanded export would facet the untouched saddle/blanket.
            mesh.vertices=positions;mesh.RecalculateBounds();
        }

        private LineRenderer MakeLine(string name, Transform parent, Material material, float width, bool world)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); ownedObjects.Add(go);
            var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = material;
            line.useWorldSpace = world; line.widthMultiplier = width; line.numCornerVertices = 2; line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = true; line.textureMode = LineTextureMode.Tile; line.generateLightingData = true;
            return line;
        }

        private void MakeHeadStrap(string name, Transform horse, Material leather, float width, Vector3[] bindPoints)
        {
            var strap = MakeLine(name, head, leather, width, false);
            strap.positionCount = bindPoints.Length;
            for (var i=0;i<bindPoints.Length;i++) strap.SetPosition(i, head.InverseTransformPoint(horse.TransformPoint(bindPoints[i])));
        }

        public void Update(Transform leftHand, Transform rightHand)
        {
            LeftReinEndpointError = UpdateRein(leftRein, leftHand, head.TransformPoint(leftBit));
            RightReinEndpointError = UpdateRein(rightRein, rightHand, head.TransformPoint(rightBit));
        }

        private float UpdateRein(LineRenderer line, Transform hand, Vector3 bit)
        {
            var palm = hand.TransformPoint(new Vector3(-.012f, .092f, .018f));
            for (var i=0;i<reinPoints.Length;i++)
            {
                var t=i/(float)(reinPoints.Length-1);
                reinPoints[i]=Vector3.Lerp(palm,bit,t)+Vector3.down*(.045f*4*t*(1-t));
            }
            line.positionCount=reinPoints.Length; line.SetPositions(reinPoints);
            return Mathf.Max(Vector3.Distance(line.GetPosition(0),palm),Vector3.Distance(line.GetPosition(reinPoints.Length-1),bit));
        }

        public void Dispose()
        {
            foreach (var go in ownedObjects) if (go != null) { go.SetActive(false); DestroyOwned(go); }
            foreach (var mesh in ownedMeshes) DestroyOwned(mesh);
            ownedObjects.Clear(); ownedMeshes.Clear();
        }
        private static void DestroyOwned(UnityEngine.Object item)
        { if (Application.isPlaying) UnityEngine.Object.Destroy(item); else UnityEngine.Object.DestroyImmediate(item); }
    }
}
