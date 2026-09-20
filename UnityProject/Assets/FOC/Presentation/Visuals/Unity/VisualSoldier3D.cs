#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using FOC.Visuals.Core;

namespace FOC.Presentation.Visuals
{
    public sealed class VisualSoldier3D:MonoBehaviour
    {
        [SerializeField]private Transform? moduleRoot;
        private readonly List<GameObject> spawned=new List<GameObject>();
        private readonly Dictionary<VisualSocket,Transform> sockets=new Dictionary<VisualSocket,Transform>();
        private readonly VisualSoldierBindingState binding=new VisualSoldierBindingState();
        private AssembledVisualInstance? activeVisual;
        private VisualSoldier3DAssembler? visualOwner;

        public VisualSoldierBindingState Binding=>binding;
        public Transform ModuleRoot=>moduleRoot==null?transform:moduleRoot;
        public IReadOnlyList<GameObject> SpawnedModules=>spawned;
        public GameObject? ActiveRepresentationRoot=>activeVisual?.Root;
        public VisualRepresentationKind? ActiveRepresentationKind=>activeVisual?.Kind;

        internal void Attach(VisualAssemblyPlan plan,AssembledVisualInstance instance,VisualSoldier3DAssembler owner)
        {
            if(activeVisual!=null)throw new InvalidOperationException("Visual view already owns an assembled representation.");
            activeVisual=instance??throw new ArgumentNullException(nameof(instance));
            visualOwner=owner??throw new ArgumentNullException(nameof(owner));
            instance.Root.transform.SetParent(ModuleRoot,false);
            instance.Root.SetActive(true);
            foreach(var module in instance.Modules)Register(module);
            binding.Bind(plan);
        }

        internal AssembledVisualInstance? Detach(VisualSoldier3DAssembler owner)
        {
            if(activeVisual==null){binding.Clear();spawned.Clear();sockets.Clear();visualOwner=null;return null;}
            if(!ReferenceEquals(visualOwner,owner))throw new InvalidOperationException("Visual representation owner mismatch.");
            var result=activeVisual;
            activeVisual=null;visualOwner=null;spawned.Clear();sockets.Clear();binding.Clear();
            return result;
        }

        public void ReleaseVisual()
        {
            if(visualOwner!=null){visualOwner.Release(this);return;}
            DestroyUnownedVisual();
        }

        public void ClearVisual()=>ReleaseVisual();
        public bool TryGetSocket(VisualSocket socket,out Transform? value)=>sockets.TryGetValue(socket,out value);

        private void Register(GameObject value)
        {
            if(value==null)throw new ArgumentNullException(nameof(value));
            spawned.Add(value);IndexSockets(value.transform);
        }

        private void DestroyUnownedVisual()
        {
            if(activeVisual!=null&&activeVisual.Root!=null)DestroyObject(activeVisual.Root);
            activeVisual=null;visualOwner=null;spawned.Clear();sockets.Clear();binding.Clear();
        }

        private void IndexSockets(Transform root)
        {
            foreach(var pair in CanonicalRig.HumanSockets){var found=FindRecursive(root,pair.Value);if(found!=null&&!sockets.ContainsKey(pair.Key))sockets.Add(pair.Key,found);}
            var rider=FindRecursive(root,CanonicalRig.RiderSocket);if(rider!=null&&!sockets.ContainsKey(VisualSocket.Rider))sockets.Add(VisualSocket.Rider,rider);
        }

        private static Transform? FindRecursive(Transform root,string name){if(StringComparer.Ordinal.Equals(root.name,name))return root;for(var i=0;i<root.childCount;i++){var found=FindRecursive(root.GetChild(i),name);if(found!=null)return found;}return null;}
        private static void DestroyObject(UnityEngine.Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
        private void OnDestroy(){if(activeVisual==null)return;if(visualOwner!=null)visualOwner.Release(this);else DestroyUnownedVisual();}
    }
}
