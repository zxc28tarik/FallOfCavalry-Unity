#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using FOC.Domain.Common;
using FOC.Domain.Soldiers;
using FOC.Visuals.Core;

namespace FOC.Presentation.Visuals
{
    public sealed class VisualSoldier3DAssembler:MonoBehaviour
    {
        [SerializeField]private VisualCatalogAsset catalogAsset=null!;private VisualCatalog? catalog;private readonly BoundedSignatureCache<GameObject> prototypeCache=new BoundedSignatureCache<GameObject>(128);
        public void Configure(VisualCatalogAsset value){catalogAsset=value??throw new ArgumentNullException(nameof(value));catalog=null;}
        public VisualAssemblyPlan Assemble(VisualSoldier3D view,SoldierInstance soldier,TroopDefinition troop,IReadOnlyDictionary<EquipmentInstanceId,EquipmentInstance> equipment){if(view==null||catalogAsset==null)throw new InvalidOperationException("Visual assembler is not configured.");catalog??=catalogAsset.BuildCoreCatalog();var plan=new VisualSoldierPlanner(catalog).Plan(soldier,troop,equipment);view.ClearVisual();for(var pass=0;pass<2;pass++){foreach(var module in plan.Modules){var isMount=module.Category==VisualAssetCategory.Mount;if((pass==0)!=isMount)continue;var prefab=catalogAsset.GetPrefab(module.AssetId);var parent=view.ModuleRoot;if(!isMount&&module.Socket.HasValue&&view.TryGetSocket(module.Socket.Value,out var socket)&&socket!=null)parent=socket;else if(!isMount&&!module.Socket.HasValue&&view.TryGetSocket(VisualSocket.Rider,out var rider)&&rider!=null)parent=rider;var instance=Instantiate(prefab,parent,false);instance.name=module.AssetId.Value;view.Register(instance);}}view.Bind(plan);return plan;}
        public bool TryGetCachedPrototype(VisualSoldierSignature signature,out GameObject? value)=>prototypeCache.TryGet(signature,out value);public void CachePrototype(VisualSoldierSignature signature,GameObject prototype)=>prototypeCache.Put(signature,prototype);
    }
}
