#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FOC.Domain.Common;
using FOC.Domain.Soldiers;
using FOC.Visuals.Core;

namespace FOC.Presentation.Visuals
{
    public sealed class VisualSoldier3DAssembler:MonoBehaviour
    {
        private sealed class VariantPool
        {
            public VariantPool(VisualSoldierSignature signature){Signature=signature;}
            public VisualSoldierSignature Signature { get; }
            public Stack<AssembledVisualInstance> Available { get; }=new Stack<AssembledVisualInstance>();
            public int Leased { get; set; }
            public bool Retired { get; set; }
        }

        [SerializeField]private VisualCatalogAsset catalogAsset=null!;
        [SerializeField]private int variantCacheCapacity=128;
        [SerializeField]private int pooledInstanceCapacity=512;
        private VisualCatalog? catalog;
        private int catalogRevision;
        private Transform? cacheRoot;
        private readonly Dictionary<VisualSoldierSignature,VariantPool> variants=new Dictionary<VisualSoldierSignature,VariantPool>();
        private readonly Queue<VariantPool> variantOrder=new Queue<VariantPool>();
        private int pooledInstances;
        private int activeLeases;
        private readonly VisualAssemblyMetrics metrics=new VisualAssemblyMetrics();

        public VisualAssemblyMetrics Metrics=>metrics;
        public int CachedVariantCount=>variants.Count;
        public int PooledInstanceCount=>pooledInstances;
        public int ActiveLeaseCount=>activeLeases;

        public void Configure(VisualCatalogAsset value,int maximumVariants=128,int maximumPooledInstances=512)
        {
            if(value==null)throw new ArgumentNullException(nameof(value));
            if(maximumVariants<1||maximumPooledInstances<1)throw new ArgumentOutOfRangeException(nameof(maximumVariants));
            if(catalogAsset!=value||catalogRevision!=value.CatalogRevision||variantCacheCapacity!=maximumVariants||pooledInstanceCapacity!=maximumPooledInstances)InvalidateCacheInternal(false);
            catalogAsset=value;catalogRevision=value.CatalogRevision;variantCacheCapacity=maximumVariants;pooledInstanceCapacity=maximumPooledInstances;catalog=null;
        }

        public VisualAssemblyPlan Assemble(VisualSoldier3D view,SoldierInstance soldier,TroopDefinition troop,IReadOnlyDictionary<EquipmentInstanceId,EquipmentInstance> equipment)
        {
            if(view==null||catalogAsset==null)throw new InvalidOperationException("Visual assembler is not configured.");
            EnsureCatalogCurrent();
            catalog??=catalogAsset.BuildCoreCatalog();
            var plan=new VisualSoldierPlanner(catalog).Plan(soldier,troop,equipment);
            view.ReleaseVisual();
            var pool=GetOrCreatePool(plan.Signature);
            AssembledVisualInstance instance;
            if(pool.Available.Count>0){instance=pool.Available.Pop();pooledInstances--;metrics.PoolHits++;metrics.ReusedRepresentations++;}
            else{instance=CreateRepresentation(plan);metrics.PoolMisses++;metrics.CreatedRepresentations++;}
            pool.Leased++;activeLeases++;instance.PoolToken=pool;
            ResetPresentationState(instance.Root);
            view.Attach(plan,instance,this);
            return plan;
        }

        public void Release(VisualSoldier3D view)
        {
            if(view==null)throw new ArgumentNullException(nameof(view));
            var instance=view.Detach(this);
            if(instance==null)return;
            var pool=instance.PoolToken as VariantPool??throw new InvalidOperationException("Visual representation has no cache owner.");
            if(pool.Leased<1||activeLeases<1)throw new InvalidOperationException("Visual representation lease accounting is invalid.");
            pool.Leased--;activeLeases--;instance.PoolToken=null;
            ResetPresentationState(instance.Root);
            instance.Root.transform.SetParent(EnsureCacheRoot(),false);
            instance.Root.SetActive(false);
            if(!pool.Retired&&variants.TryGetValue(pool.Signature,out var current)&&ReferenceEquals(current,pool)&&pooledInstances<pooledInstanceCapacity){pool.Available.Push(instance);pooledInstances++;metrics.ReleasedToPool++;}
            else DestroyRepresentation(instance);
        }

        public void InvalidateCache(){InvalidateCacheInternal(true);if(catalogAsset!=null)catalogRevision=catalogAsset.CatalogRevision;catalog=null;}
        public void DisposeCache()=>InvalidateCacheInternal(true);

        private void EnsureCatalogCurrent()
        {
            if(catalogRevision==catalogAsset.CatalogRevision)return;
            InvalidateCacheInternal(true);catalogRevision=catalogAsset.CatalogRevision;catalog=null;
        }

        private VariantPool GetOrCreatePool(VisualSoldierSignature signature)
        {
            if(variants.TryGetValue(signature,out var found)){metrics.CacheHits++;return found;}
            metrics.CacheMisses++;
            while(variants.Count>=variantCacheCapacity)EvictOldest();
            var value=new VariantPool(signature);variants.Add(signature,value);variantOrder.Enqueue(value);return value;
        }

        private void EvictOldest()
        {
            while(variantOrder.Count>0)
            {
                var candidate=variantOrder.Dequeue();
                if(!variants.TryGetValue(candidate.Signature,out var current)||!ReferenceEquals(candidate,current))continue;
                variants.Remove(candidate.Signature);candidate.Retired=true;metrics.Evictions++;
                while(candidate.Available.Count>0){var instance=candidate.Available.Pop();pooledInstances--;DestroyRepresentation(instance);}
                return;
            }
            throw new InvalidOperationException("Visual variant cache eviction order is empty.");
        }

        private AssembledVisualInstance CreateRepresentation(VisualAssemblyPlan plan)
        {
            var root=new GameObject("VIS_"+plan.Signature.Value);root.AddComponent<VisualRuntimeVariant>().Initialize(plan.Signature,plan.RepresentationKind);
            var modules=new List<GameObject>();var sockets=new Dictionary<VisualSocket,Transform>();Transform? mountRoot=null;
            foreach(var module in ResolveRuntimeModules(plan).OrderBy(ModuleOrder).ThenBy(x=>x.Category).ThenBy(x=>x.AssetId))
            {
                var prefab=catalogAsset.GetPrefab(module.AssetId);var parent=root.transform;
                if((module.Category==VisualAssetCategory.Harness||module.Category==VisualAssetCategory.MountArmor)&&mountRoot!=null)parent=mountRoot;
                else if(module.Category!=VisualAssetCategory.Mount&&module.Socket.HasValue&&sockets.TryGetValue(module.Socket.Value,out var socket))parent=socket;
                else if(module.Category!=VisualAssetCategory.Mount&&!module.Socket.HasValue&&sockets.TryGetValue(VisualSocket.Rider,out var rider))parent=rider;
                var created=Instantiate(prefab,parent,false);created.name=module.AssetId.Value;if(module.Category==VisualAssetCategory.Harness||module.Category==VisualAssetCategory.MountArmor)created.transform.localPosition=new Vector3(0f,1.28f,0f);modules.Add(created);metrics.CreatedModules++;if(module.Category==VisualAssetCategory.Mount)mountRoot=created.transform;IndexSockets(created.transform,sockets);
            }
            return new AssembledVisualInstance(root,modules.ToArray(),plan.RepresentationKind);
        }

        private IReadOnlyList<VisualModule> ResolveRuntimeModules(VisualAssemblyPlan plan)
        {
            if(plan.RepresentationKind==VisualRepresentationKind.Crowd)return new[]{new VisualModule(catalogAsset.CrowdAssetId,VisualAssetCategory.Crowd,null,"runtime:crowd")};
            var result=plan.RepresentationKind==VisualRepresentationKind.Modular?new List<VisualModule>(plan.Modules):new List<VisualModule>{new VisualModule(catalogAsset.ConsolidatedCharacterAssetId,VisualAssetCategory.ConsolidatedCharacter,null,"runtime:consolidated")};
            if(plan.RepresentationKind==VisualRepresentationKind.Consolidated)result.AddRange(plan.Modules.Where(x=>x.Category!=VisualAssetCategory.Body&&x.Category!=VisualAssetCategory.Head&&x.Category!=VisualAssetCategory.Clothing&&x.Category!=VisualAssetCategory.BodyArmor));
            if(result.Any(x=>x.Category==VisualAssetCategory.Mount)&&result.All(x=>x.Category!=VisualAssetCategory.Harness)&&catalogAsset.TryGetFirstAssetId(VisualAssetCategory.Harness,out var harness))result.Add(new VisualModule(harness,VisualAssetCategory.Harness,null,"runtime:default-harness"));
            return result;
        }

        private static int ModuleOrder(VisualModule module)
        {
            if(module.Category==VisualAssetCategory.Mount)return 0;
            if(module.Category==VisualAssetCategory.Body||module.Category==VisualAssetCategory.ConsolidatedCharacter||module.Category==VisualAssetCategory.Crowd)return 1;
            return 2;
        }

        private static void IndexSockets(Transform root,Dictionary<VisualSocket,Transform> sockets)
        {
            foreach(var pair in CanonicalRig.HumanSockets){var found=FindRecursive(root,pair.Value);if(found!=null&&!sockets.ContainsKey(pair.Key))sockets.Add(pair.Key,found);}
            var rider=FindRecursive(root,CanonicalRig.RiderSocket);if(rider!=null&&!sockets.ContainsKey(VisualSocket.Rider))sockets.Add(VisualSocket.Rider,rider);
        }

        private static Transform? FindRecursive(Transform root,string name){if(StringComparer.Ordinal.Equals(root.name,name))return root;for(var i=0;i<root.childCount;i++){var found=FindRecursive(root.GetChild(i),name);if(found!=null)return found;}return null;}

        private Transform EnsureCacheRoot()
        {
            if(cacheRoot!=null)return cacheRoot;
            var root=new GameObject("VisualVariantPool");root.transform.SetParent(transform,false);root.SetActive(false);cacheRoot=root.transform;return cacheRoot;
        }

        private static void ResetPresentationState(GameObject root)
        {
            root.transform.localPosition=Vector3.zero;root.transform.localRotation=Quaternion.identity;root.transform.localScale=Vector3.one;
            foreach(var animator in root.GetComponentsInChildren<Animator>(true))
            {
                animator.speed=1f;animator.applyRootMotion=false;
                if(animator.runtimeAnimatorController==null)continue;
                foreach(var parameter in animator.parameters){if(parameter.type==AnimatorControllerParameterType.Trigger)animator.ResetTrigger(parameter.name);else if(parameter.type==AnimatorControllerParameterType.Bool)animator.SetBool(parameter.name,false);else if(parameter.type==AnimatorControllerParameterType.Float)animator.SetFloat(parameter.name,0f);else if(parameter.type==AnimatorControllerParameterType.Int)animator.SetInteger(parameter.name,0);}
                animator.Rebind();animator.Update(0f);
            }
        }

        private void InvalidateCacheInternal(bool count)
        {
            if(count)metrics.Invalidations++;
            foreach(var pool in variants.Values){pool.Retired=true;while(pool.Available.Count>0){var instance=pool.Available.Pop();pooledInstances--;DestroyRepresentation(instance);}}
            variants.Clear();variantOrder.Clear();
        }

        private void DestroyRepresentation(AssembledVisualInstance instance){metrics.DestroyedRepresentations++;DestroyObject(instance.Root);}
        private static void DestroyObject(UnityEngine.Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
        private void OnDestroy(){InvalidateCacheInternal(false);}
    }
}
