using System;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Houses;

namespace FOC.Application.Houses
{
    public sealed class HouseCommandService
    {
        private readonly CharacterRoster _characters;
        private readonly HouseRegistry _houses;
        public HouseCommandService(CharacterRoster characters, HouseRegistry houses) { _characters = characters ?? throw new ArgumentNullException(nameof(characters)); _houses = houses ?? throw new ArgumentNullException(nameof(houses)); }
        public void AddMember(HouseId houseId, HouseMember member) { _characters.GetRequired(member.CharacterId); _houses.GetRequired(houseId).AddMember(member); }
        public void ChangeHead(HouseId houseId, CharacterId characterId) => _houses.GetRequired(houseId).SetHead(characterId, _characters);
        public void MarkSuccessionPending(HouseId houseId) => _houses.GetRequired(houseId).MarkSuccessionPending();
        public void Dissolve(HouseId houseId) => _houses.GetRequired(houseId).SetLifecycle(HouseLifecycle.Dissolved);
    }
}
