#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using FOC.Domain.Common;
using FOC.Domain.Soldiers;
using FOC.Visuals.Core;

namespace FOC.Presentation.Visuals
{
    public sealed class VisualSoldier3D:MonoBehaviour
    {
        [SerializeField]private Transform? moduleRoot;private readonly List<GameObject> spawned=new List<GameObject>();private readonly Dictionary<VisualSocket,Transform> sockets=new Dictionary<VisualSocket,Transform>();private VisualSoldierBindingState binding=new VisualSoldierBindingState();
        public VisualSoldierBindingState Binding=>binding;public Transform ModuleRoot=>moduleRoot==null?transform:moduleRoot;public IReadOnlyList<GameObject> SpawnedModules=>spawned;
        public void Bind(VisualAssemblyPlan plan){binding.Bind(plan);}public void Register(GameObject value){if(value==null)throw new ArgumentNullException(nameof(value));spawned.Add(value);IndexSockets(value.transform);}public bool TryGetSocket(VisualSocket socket,out Transform? value)=>sockets.TryGetValue(socket,out value);
        public void ClearVisual(){for(var i=spawned.Count-1;i>=0;i--){if(spawned[i]!=null){if(Application.isPlaying)Destroy(spawned[i]);else DestroyImmediate(spawned[i]);}}spawned.Clear();sockets.Clear();binding.Clear();}
        private void IndexSockets(Transform root){foreach(var pair in CanonicalRig.HumanSockets){var found=FindRecursive(root,pair.Value);if(found!=null&&!sockets.ContainsKey(pair.Key))sockets.Add(pair.Key,found);}var rider=FindRecursive(root,CanonicalRig.RiderSocket);if(rider!=null&&!sockets.ContainsKey(VisualSocket.Rider))sockets.Add(VisualSocket.Rider,rider);}
        private static Transform? FindRecursive(Transform root,string name){if(StringComparer.Ordinal.Equals(root.name,name))return root;for(var i=0;i<root.childCount;i++){var found=FindRecursive(root.GetChild(i),name);if(found!=null)return found;}return null;}
    }

}
