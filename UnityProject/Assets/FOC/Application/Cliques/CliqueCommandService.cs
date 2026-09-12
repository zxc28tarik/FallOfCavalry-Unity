using System;
using FOC.Domain.Characters;
using FOC.Domain.Cliques;
using FOC.Domain.Common;

namespace FOC.Application.Cliques
{
    public sealed class CliqueCommandService
    {
        private readonly CharacterRoster _characters;
        private readonly CliqueRegistry _cliques;
        public CliqueCommandService(CharacterRoster characters, CliqueRegistry cliques) { _characters = characters ?? throw new ArgumentNullException(nameof(characters)); _cliques = cliques ?? throw new ArgumentNullException(nameof(cliques)); }
        public void AddMember(CliqueId cliqueId, CliqueMembership membership) { _characters.GetRequired(membership.CharacterId); _cliques.GetRequired(cliqueId).AddMembership(membership); }
        public void SetLeader(CliqueId cliqueId, CharacterId? leaderId) { if (leaderId.HasValue && _characters.GetRequired(leaderId.Value).IsDead) throw new InvalidOperationException("Clique leader must be living."); _cliques.GetRequired(cliqueId).SetLeader(leaderId); }
        public void Dissolve(CliqueId cliqueId) => _cliques.GetRequired(cliqueId).SetLifecycle(CliqueLifecycle.Dissolved);
    }
}
