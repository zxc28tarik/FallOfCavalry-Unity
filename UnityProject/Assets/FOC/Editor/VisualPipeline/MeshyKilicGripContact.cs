#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Presentation.Visuals;
using UnityEditor;
using UnityEngine;

namespace FOC.Editor.Visuals
{
    /// <summary>Offline clearance against the UNCHANGED retained leather hilt, not a guessed cylinder.</summary>
    public sealed class MeshyKilicGripContact
    {
        private readonly Vector3[] hilt;
        private readonly int[] triangles;
        private readonly Dictionary<int,Segment[]> sections=new Dictionary<int,Segment[]>();
        private const float Clearance=.002f;
        private readonly struct Segment
        {
            public readonly Vector2 a,b;
            public Segment(Vector3 a,Vector3 b){this.a=new Vector2(a.x,a.y);this.b=new Vector2(b.x,b.y);}
        }
        public MeshyKilicGripContact(Vector3 palm,Quaternion rotation)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FOC/Presentation/Equipment/Ottoman1648/WPN_Kilic_01.prefab")
                ??throw new InvalidOperationException("Retained kilic missing.");
            var renderer=prefab.GetComponent<LODGroup>().GetLODs()[0].renderers.Single();
            var index=Array.FindIndex(renderer.sharedMaterials,m=>m.name.IndexOf("Leather",StringComparison.OrdinalIgnoreCase)>=0);
            if(index<0)throw new InvalidOperationException("Retained hilt leather submesh missing.");
            var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
            hilt=mesh.vertices.Select(v=>palm+rotation*(v-MeshyKilicGripAttachment.SwordLocalGripAnchor)).ToArray();
            triangles=mesh.GetTriangles(index);
        }
        public void Resolve(Vector3[] points,bool[] editable,int[] bodyTriangles)
        {
            // Weld by the original posed coordinate, not UV index. All seams
            // get the same correction; no new vertices or topology are added.
            var groups=new Dictionary<Vector3,List<int>>();
            for(var i=0;i<points.Length;i++)if(editable[i])
            {
                var key=Key(points[i]);if(!groups.TryGetValue(key,out var group))groups.Add(key,group=new List<int>());group.Add(i);
            }
            for(var pass=0;pass<6;pass++)
            {
                foreach(var group in groups.Values)
                {
                    var push=Push(points[group[0]],Clearance);
                    if(push.sqrMagnitude>0)foreach(var i in group)points[i]+=push;
                }
                for(var t=0;t<bodyTriangles.Length;t+=3)
                {
                    var a=bodyTriangles[t];var b=bodyTriangles[t+1];var c=bodyTriangles[t+2];
                    if(!editable[a]&&!editable[b]&&!editable[c])continue;
                    CorrectSample(points,editable,a,b,c,new Vector3(.5f,.5f,0));
                    CorrectSample(points,editable,a,b,c,new Vector3(0,.5f,.5f));
                    CorrectSample(points,editable,a,b,c,new Vector3(.5f,0,.5f));
                    CorrectSample(points,editable,a,b,c,new Vector3(1f/3,1f/3,1f/3));
                }
                foreach(var group in groups.Values)
                {
                    var average=Vector3.zero;foreach(var i in group)average+=points[i];average/=group.Count;
                    foreach(var i in group)points[i]=average;
                }
            }
        }
        private void CorrectSample(Vector3[] points,bool[] editable,int a,int b,int c,Vector3 bary)
        {
            var push=Push(points[a]*bary.x+points[b]*bary.y+points[c]*bary.z,Clearance*.5f);
            if(push.sqrMagnitude<1e-12f)return;
            push=Vector3.ClampMagnitude(push,.001f);
            var denominator=(editable[a]?bary.x*bary.x:0)+(editable[b]?bary.y*bary.y:0)+(editable[c]?bary.z*bary.z:0);
            if(denominator<1e-6f)return;
            if(editable[a])points[a]+=push*(bary.x/denominator);
            if(editable[b])points[b]+=push*(bary.y/denominator);
            if(editable[c])points[c]+=push*(bary.z/denominator);
        }
        public Vector3 Push(Vector3 p,float clearance)
        {
            var slice=Section(p.z);if(slice.Length<3)return Vector3.zero;
            var point=new Vector2(p.x,p.y);var inside=false;var nearest=Vector2.zero;var minimum=float.PositiveInfinity;var edgeDirection=Vector2.zero;
            var center=Vector2.zero;foreach(var s in slice)center+=s.a+s.b;center/=slice.Length*2;
            foreach(var segment in slice)
            {
                if((segment.a.y>point.y)!=(segment.b.y>point.y)&&point.x<(segment.b.x-segment.a.x)*(point.y-segment.a.y)/(segment.b.y-segment.a.y)+segment.a.x)inside=!inside;
                var d=segment.b-segment.a;var u=Mathf.Clamp01(Vector2.Dot(point-segment.a,d)/d.sqrMagnitude);
                var q=segment.a+d*u;var distance=(q-point).sqrMagnitude;
                if(distance<minimum){minimum=distance;nearest=q;edgeDirection=d;}
            }
            var length=Mathf.Sqrt(minimum);
            if(!inside&&length>=clearance)return Vector3.zero;
            var direction=inside?nearest-point:point-nearest;
            if(direction.sqrMagnitude<1e-12f)
            {
                direction=new Vector2(edgeDirection.y,-edgeDirection.x);
                if(Vector2.Dot(direction,nearest-center)<0)direction=-direction;
            }
            var target=nearest+direction.normalized*clearance;
            return new Vector3(target.x-p.x,target.y-p.y,0);
        }
        private Segment[] Section(float z)
        {
            var key=Mathf.RoundToInt(z*100000);if(sections.TryGetValue(key,out var cached))return cached;
            z=key/100000f;var result=new List<Segment>();
            for(var i=0;i<triangles.Length;i+=3)
            {
                var vertices=new[]{hilt[triangles[i]],hilt[triangles[i+1]],hilt[triangles[i+2]]};var hits=new List<Vector3>();
                for(var edge=0;edge<3;edge++)
                {
                    var a=vertices[edge];var b=vertices[(edge+1)%3];
                    if((a.z<z&&b.z>=z)||(b.z<z&&a.z>=z))hits.Add(Vector3.Lerp(a,b,(z-a.z)/(b.z-a.z)));
                }
                if(hits.Count==2&&(hits[0]-hits[1]).sqrMagnitude>1e-12f)result.Add(new Segment(hits[0],hits[1]));
            }
            cached=result.ToArray();sections.Add(key,cached);return cached;
        }
        private static Vector3 Key(Vector3 p)=>new Vector3(Mathf.Round(p.x*1000000),Mathf.Round(p.y*1000000),Mathf.Round(p.z*1000000));
    }
}
