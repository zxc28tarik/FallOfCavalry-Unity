#nullable enable
using System;
using System.Linq;
using FOC.Domain.Battle;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Cities;
using FOC.Domain.Common;
using FOC.Domain.Military;
using FOC.Domain.Organizations;

namespace FOC.Presentation.Core
{
    public sealed class CampaignPresentationScreenSource : IPresentationScreenSource
    {
        private readonly CampaignRuntimeState _campaign;
        private readonly CampaignPresentationQueries _queries;
        private readonly PresentationViewerContext _viewer;
        private readonly IMapPresentationDataProvider _map;

        public CampaignPresentationScreenSource(CampaignRuntimeState campaign, PresentationViewerContext viewer, IMapPresentationDataProvider map)
        {
            _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            _viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _queries = new CampaignPresentationQueries(campaign);
        }

        public ScreenPresentationState Get(PresentationRoute route)
        {
            switch (route.Screen)
            {
                case PresentationScreenId.Map: return _queries.BuildMap(_viewer, _map).State;
                case PresentationScreenId.City:
                {
                    var id = Subject(route, PresentationEntityKind.City) ?? _campaign.Cities.OrderedCities.FirstOrDefault()?.Id.Value;
                    return id == null ? _queries.BuildEmpty(route.Screen) : _queries.BuildCity(_viewer, CityId.Create(id)).State;
                }
                case PresentationScreenId.Character:
                {
                    var id = Subject(route, PresentationEntityKind.Character) ?? _campaign.Characters.OrderedCharacters.FirstOrDefault()?.Id.Value;
                    return id == null ? _queries.BuildEmpty(route.Screen) : _queries.BuildCharacter(_viewer, CharacterId.Create(id)).State;
                }
                case PresentationScreenId.Organization:
                {
                    var id = Subject(route, PresentationEntityKind.Organization) ?? _campaign.Organizations.OrderedOrganizations.FirstOrDefault()?.Id.Value;
                    return id == null ? _queries.BuildEmpty(route.Screen) : _queries.BuildOrganization(_viewer, OrganizationId.Create(id)).State;
                }
                case PresentationScreenId.Trade:
                {
                    var id = Subject(route, PresentationEntityKind.City) ?? _campaign.Economy.OrderedMarkets.FirstOrDefault()?.CityId.Value;
                    return id == null ? _queries.BuildEmpty(route.Screen) : _queries.BuildTrade(_viewer, CityId.Create(id)).State;
                }
                case PresentationScreenId.Army:
                {
                    var id = Subject(route, PresentationEntityKind.Army) ?? _campaign.Military.Armies.OrderedArmies.FirstOrDefault()?.Id.Value;
                    return id == null ? _queries.BuildEmpty(route.Screen) : _queries.BuildArmy(_viewer, ArmyId.Create(id)).State;
                }
                case PresentationScreenId.Diplomacy: return _queries.BuildDiplomacy(_viewer).State;
                case PresentationScreenId.Battle:
                {
                    var id = Subject(route, PresentationEntityKind.Battle) ?? _campaign.Battles.Battles.OrderedBattles.FirstOrDefault()?.Id.Value;
                    return id == null ? _queries.BuildEmpty(route.Screen) : _queries.BuildBattle(_viewer, BattleId.Create(id)).State;
                }
                case PresentationScreenId.Reports: return _queries.BuildReports(_viewer).State;
                case PresentationScreenId.EncounterContract: return _queries.BuildEncounterContracts(_viewer).State;
                case PresentationScreenId.Ledger: return _queries.BuildLedger(_viewer);
                default: return _queries.BuildEmpty(route.Screen);
            }
        }

        private static string? Subject(PresentationRoute route, PresentationEntityKind kind) => route.Subject.HasValue && route.Subject.Value.Kind == kind ? route.Subject.Value.Id : null;
    }
}
