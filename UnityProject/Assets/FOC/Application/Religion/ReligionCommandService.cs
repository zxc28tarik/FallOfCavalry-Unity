using System;
using FOC.Domain.Characters;
using FOC.Domain.Cliques;
using FOC.Domain.Common;
using FOC.Domain.Religion;

namespace FOC.Application.Religion
{
    public sealed class ReligionCommandService
    {
        private readonly CharacterRoster _characters; private readonly CliqueRegistry _cliques; private readonly ReligionCampaignState _state;
        public ReligionCommandService(CharacterRoster characters, CliqueRegistry cliques, ReligionCampaignState state) { _characters=characters??throw new ArgumentNullException(nameof(characters)); _cliques=cliques??throw new ArgumentNullException(nameof(cliques)); _state=state??throw new ArgumentNullException(nameof(state)); }
        public void SetCharacterReligion(CharacterId characterId, ReligionId religionId, SectId? sectId=null) { _characters.GetRequired(characterId); _state.Definitions.RequireCompatible(religionId,sectId); _state.Characters.Set(new CharacterReligionState(characterId,religionId,sectId)); }
        public void AssociateReligiousClique(CliqueId cliqueId, ReligionId religionId, SectId? sectId=null) { var clique=_cliques.GetRequired(cliqueId); if(clique.Definition.Type!=CliqueType.Religious) throw new InvalidOperationException("Only a Religious Clique can carry a religious association."); _state.Definitions.RequireCompatible(religionId,sectId); _state.AddCliqueAssociation(new ReligiousCliqueAssociation(cliqueId,religionId,sectId)); }
    }
}
