#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using FOC.Domain.Battle;
using FOC.Domain.Common;
using FOC.Domain.Soldiers;
using FOC.Visuals.Core;

namespace FOC.Presentation.Visuals
{
    public sealed class BattleCombatantViewBinding:MonoBehaviour
    {
        [SerializeField]private VisualSoldier3D view=null!;[SerializeField]private VisualSoldier3DAssembler assembler=null!;public SoldierId? SoldierId{get;private set;}public void Configure(VisualSoldier3D value,VisualSoldier3DAssembler visualAssembler){view=value??throw new ArgumentNullException(nameof(value));assembler=visualAssembler??throw new ArgumentNullException(nameof(visualAssembler));}public VisualAssemblyPlan Bind(BattleCombatantSnapshot combatant,SoldierInstance soldier,TroopDefinition troop,IReadOnlyDictionary<EquipmentInstanceId,EquipmentInstance> equipment){if(view==null||assembler==null)throw new InvalidOperationException("Battle combatant view binding is not configured.");BattleVisualBinding.Validate(combatant,soldier,equipment);var plan=assembler.Assemble(view,soldier,troop,equipment);SoldierId=soldier.Id;return plan;}public void Despawn(){if(view!=null)view.ReleaseVisual();SoldierId=null;}
    }

    public sealed class BattleSectorAnchor:MonoBehaviour
    {
        [SerializeField]private string sectorId=string.Empty;public string SectorId=>sectorId;public void Configure(BattleSectorId id){if(!id.IsValid)throw new ArgumentException(nameof(id));sectorId=id.Value;}
    }

    public sealed class BattleProofSceneState:MonoBehaviour
    {
        [SerializeField]private string lifecycle="Active";[SerializeField]private int sideCount=2;[SerializeField]private int sectorCount=3;public string Lifecycle=>lifecycle;public int SideCount=>sideCount;public int SectorCount=>sectorCount;
    }
}
