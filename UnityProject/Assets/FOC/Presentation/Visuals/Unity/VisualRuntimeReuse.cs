#nullable enable
using System;
using UnityEngine;
using FOC.Visuals.Core;

namespace FOC.Presentation.Visuals
{
    public sealed class VisualAssemblyMetrics
    {
        public int CacheHits { get; internal set; }
        public int CacheMisses { get; internal set; }
        public int PoolHits { get; internal set; }
        public int PoolMisses { get; internal set; }
        public int CreatedRepresentations { get; internal set; }
        public int ReusedRepresentations { get; internal set; }
        public int CreatedModules { get; internal set; }
        public int ReleasedToPool { get; internal set; }
        public int DestroyedRepresentations { get; internal set; }
        public int Invalidations { get; internal set; }
        public int Evictions { get; internal set; }

        public void Reset()
        {
            CacheHits=0;CacheMisses=0;PoolHits=0;PoolMisses=0;CreatedRepresentations=0;ReusedRepresentations=0;CreatedModules=0;ReleasedToPool=0;DestroyedRepresentations=0;Invalidations=0;Evictions=0;
        }
    }

    public sealed class VisualRuntimeVariant:MonoBehaviour
    {
        [SerializeField]private string signature=string.Empty;
        [SerializeField]private VisualRepresentationKind representationKind;
        public string Signature=>signature;
        public VisualRepresentationKind RepresentationKind=>representationKind;
        internal void Initialize(VisualSoldierSignature value,VisualRepresentationKind kind){signature=value.Value;representationKind=kind;}
    }

    internal sealed class AssembledVisualInstance
    {
        public AssembledVisualInstance(GameObject root,GameObject[] modules,VisualRepresentationKind kind){Root=root??throw new ArgumentNullException(nameof(root));Modules=modules??throw new ArgumentNullException(nameof(modules));Kind=kind;}
        public GameObject Root { get; }
        public GameObject[] Modules { get; }
        public VisualRepresentationKind Kind { get; }
        public object? PoolToken { get; set; }
    }
}
