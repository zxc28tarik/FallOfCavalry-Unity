using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Domain.Battle;
using FOC.Domain.Common;
using FOC.Domain.Soldiers;

namespace FOC.Visuals.Core
{
    public static class BattleVisualBinding
    {
        public static void Validate(BattleCombatantSnapshot combatant,SoldierInstance soldier,IReadOnlyDictionary<EquipmentInstanceId,EquipmentInstance> equipment)
        {
            if(combatant==null||soldier==null||equipment==null)throw new ArgumentNullException();if(!combatant.SoldierId.Equals(soldier.Id)||!combatant.UnitGroupId.Equals(soldier.UnitGroupId)||!combatant.TroopDefinitionId.Equals(soldier.TroopDefinitionId))throw new InvalidOperationException("Battle visual binding identity conflicts with persistent Soldier.");var ids=soldier.Loadout.OrderedWeapons.Select(x=>x.EquipmentId).Concat(soldier.Loadout.OrderedArmor.Select(x=>x.EquipmentId)).ToList();if(soldier.Loadout.Shield.HasValue)ids.Add(soldier.Loadout.Shield.Value);if(soldier.Loadout.Mount.HasValue)ids.Add(soldier.Loadout.Mount.Value);if(!ids.OrderBy(x=>x).SequenceEqual(combatant.OrderedEquipment))throw new InvalidOperationException("Battle visual binding cannot reroll or replace persistent equipment.");foreach(var id in combatant.OrderedEquipment)if(!equipment.TryGetValue(id,out var instance)||instance.Owner.Kind!=EquipmentOwnerKind.Soldier||!StringComparer.Ordinal.Equals(instance.Owner.Id,soldier.Id.Value))throw new InvalidOperationException("Battle visual equipment reference is missing or owned by another entity.");
        }
    }
}
