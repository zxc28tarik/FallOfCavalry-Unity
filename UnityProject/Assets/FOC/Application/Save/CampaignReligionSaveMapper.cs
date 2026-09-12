using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Religion;

namespace FOC.Application.Save
{
    public static partial class CampaignSaveMapper
    {
        private static void AddReligionSaveData(CampaignRuntimeState state,CampaignSaveData data)
        {
            foreach(var x in state.Religion.Definitions.OrderedReligions)data.Religions.Add(new ReligionDefinitionSaveData{ReligionId=x.Id.Value,Name=x.Name,Status=(int)x.Status});
            foreach(var x in state.Religion.Definitions.OrderedSects)data.Sects.Add(new SectDefinitionSaveData{SectId=x.Id.Value,ParentReligionId=x.ParentReligionId.Value,Name=x.Name,Status=(int)x.Status});
            foreach(var x in state.Religion.Characters.OrderedStates)data.CharacterReligions.Add(new CharacterReligionSaveData{CharacterId=x.CharacterId.Value,ReligionId=x.ReligionId.Value,SectId=x.SectId?.Value??string.Empty});
            foreach(var x in state.Religion.OrderedProfiles){var dto=new ReligionProfileSaveData{TargetKind=(int)x.Target.Kind,TargetId=x.Target.Id};foreach(var e in x.Entries)dto.Entries.Add(new ReligionProfileEntrySaveData{ReligionId=e.ReligionId.Value,SectId=e.SectId?.Value??string.Empty,RelativePresence=e.RelativePresence});data.ReligionProfiles.Add(dto);}
            foreach(var x in state.Religion.OrderedPolicies){var dto=new ReligionPolicySaveData{TargetKind=(int)x.Target.Kind,TargetId=x.Target.Id};foreach(var rule in x.Rules)dto.Rules.Add(new ReligionPolicyRuleSaveData{ReligionId=rule.ReligionId.Value,SectId=rule.SectId?.Value??string.Empty,Recognition=(int)rule.Recognition,Treatment=(int)rule.Treatment,Enforcement=(int)rule.Enforcement});data.ReligionPolicies.Add(dto);}
            foreach(var x in state.Religion.OrderedCliqueAssociations)data.ReligiousCliqueAssociations.Add(new ReligiousCliqueAssociationSaveData{CliqueId=x.CliqueId.Value,ReligionId=x.ReligionId.Value,SectId=x.SectId?.Value??string.Empty});
        }
        private static ReligionCampaignState RestoreReligion(CampaignSaveData data)
        {
            var definitions=new ReligionRegistry();foreach(var x in data.Religions)definitions.AddReligion(new ReligionDefinition(ReligionId.Create(x.ReligionId),x.Name,(ContentStatus)x.Status));foreach(var x in data.Sects)definitions.AddSect(new SectDefinition(SectId.Create(x.SectId),ReligionId.Create(x.ParentReligionId),x.Name,(ContentStatus)x.Status));
            var characters=new CharacterReligionRegistry();foreach(var x in data.CharacterReligions)characters.Add(new CharacterReligionState(CharacterId.Create(x.CharacterId),ReligionId.Create(x.ReligionId),string.IsNullOrEmpty(x.SectId)?(SectId?)null:SectId.Create(x.SectId)));
            var result=new ReligionCampaignState(definitions,characters);foreach(var x in data.ReligionProfiles){var p=new ReligionProfile(ToTarget(x.TargetKind,x.TargetId));foreach(var e in x.Entries)p.Add(new ReligionProfileEntry(ReligionId.Create(e.ReligionId),string.IsNullOrEmpty(e.SectId)?(SectId?)null:SectId.Create(e.SectId),e.RelativePresence));result.AddProfile(p);}foreach(var x in data.ReligionPolicies){var p=new ReligionPolicy(ToTarget(x.TargetKind,x.TargetId));foreach(var e in x.Rules)p.Add(new ReligionPolicyRule(ReligionId.Create(e.ReligionId),string.IsNullOrEmpty(e.SectId)?(SectId?)null:SectId.Create(e.SectId),(ReligionRecognition)e.Recognition,(ReligionTreatment)e.Treatment,(ReligionEnforcement)e.Enforcement));result.AddPolicy(p);}foreach(var x in data.ReligiousCliqueAssociations)result.AddCliqueAssociation(new ReligiousCliqueAssociation(CliqueId.Create(x.CliqueId),ReligionId.Create(x.ReligionId),string.IsNullOrEmpty(x.SectId)?(SectId?)null:SectId.Create(x.SectId)));return result;
        }
        private static ReligionProfileTarget ToTarget(int kind,string id){switch((ReligionProfileTargetKind)kind){case ReligionProfileTargetKind.City:return ReligionProfileTarget.City(CityId.Create(id));case ReligionProfileTargetKind.Faction:return ReligionProfileTarget.Faction(FactionId.Create(id));case ReligionProfileTargetKind.Region:return ReligionProfileTarget.Region(RegionId.Create(id));default:throw new System.InvalidOperationException("Unknown Religion profile target kind.");}}
    }
}
