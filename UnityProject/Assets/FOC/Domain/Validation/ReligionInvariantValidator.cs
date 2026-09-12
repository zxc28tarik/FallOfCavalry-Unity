using System;
using System.Collections.Generic;
using FOC.Domain.Campaign;
using FOC.Domain.Cliques;

namespace FOC.Domain.Validation
{
    public sealed class ReligionInvariantValidator : IInvariantValidator<CampaignRuntimeState>
    {
        public ValidationResult Validate(CampaignRuntimeState subject)
        {
            var result=new ValidationResult(); if(subject==null){result.AddError("CAMPAIGN_NULL","Campaign is required.");return result;}
            foreach(var state in subject.Religion.Characters.OrderedStates) Try(result,"RELIGION_CHARACTER_INVALID",()=>{subject.Characters.GetRequired(state.CharacterId);subject.Religion.Definitions.RequireCompatible(state.ReligionId,state.SectId);});
            foreach(var profile in subject.Religion.OrderedProfiles) foreach(var entry in profile.Entries) Try(result,"RELIGION_PROFILE_INVALID",()=>subject.Religion.Definitions.RequireCompatible(entry.ReligionId,entry.SectId));
            foreach(var policy in subject.Religion.OrderedPolicies) foreach(var rule in policy.Rules) Try(result,"RELIGION_POLICY_INVALID",()=>subject.Religion.Definitions.RequireCompatible(rule.ReligionId,rule.SectId));
            foreach(var association in subject.Religion.OrderedCliqueAssociations) Try(result,"RELIGIOUS_CLIQUE_INVALID",()=>{var clique=subject.Cliques.GetRequired(association.CliqueId);if(clique.Definition.Type!=CliqueType.Religious)throw new InvalidOperationException();subject.Religion.Definitions.RequireCompatible(association.ReligionId,association.SectId);}); return result;
        }
        private static void Try(ValidationResult result,string code,Action action){try{action();}catch(Exception){result.AddError(code,"Religion invariant is invalid.");}}
    }
}
