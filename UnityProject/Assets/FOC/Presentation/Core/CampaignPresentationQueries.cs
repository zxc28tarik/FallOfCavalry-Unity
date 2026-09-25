#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Domain.Battle;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Cities;
using FOC.Domain.Common;
using FOC.Domain.Diplomacy;
using FOC.Domain.Economy;
using FOC.Domain.EncountersContracts;
using FOC.Domain.Military;
using FOC.Domain.Organizations;
using FOC.Domain.Religion;
using FOC.Domain.Geography;

namespace FOC.Presentation.Core
{
    public interface IMapPresentationDataProvider
    {
        IReadOnlyList<MapMarkerPresentation> GetKnownMarkers(PresentationViewerContext viewer);
        IReadOnlyList<MapRoutePresentation> GetKnownRoutes(PresentationViewerContext viewer);
        IReadOnlyList<MapJourneyPresentation> GetKnownJourneys(PresentationViewerContext viewer);
    }

    public sealed class ProofOnlyMapPresentationDataProvider : IMapPresentationDataProvider
    {
        private readonly IReadOnlyList<MapMarkerPresentation> _markers;
        public ProofOnlyMapPresentationDataProvider(IEnumerable<MapMarkerPresentation> markers)
        {
            if (markers == null) throw new ArgumentNullException(nameof(markers));
            _markers = markers.Select(x => x.ProofOnly ? x : throw new InvalidOperationException("Proof map positions must be marked PROOF_ONLY.")).ToList().AsReadOnly();
        }
        public IReadOnlyList<MapMarkerPresentation> GetKnownMarkers(PresentationViewerContext viewer) { if (viewer == null) throw new ArgumentNullException(nameof(viewer)); return _markers; }
        public IReadOnlyList<MapRoutePresentation> GetKnownRoutes(PresentationViewerContext viewer) { if (viewer == null) throw new ArgumentNullException(nameof(viewer)); return Array.Empty<MapRoutePresentation>(); }
        public IReadOnlyList<MapJourneyPresentation> GetKnownJourneys(PresentationViewerContext viewer) { if (viewer == null) throw new ArgumentNullException(nameof(viewer)); return Array.Empty<MapJourneyPresentation>(); }
    }

    public sealed class CampaignPresentationQueries
    {
        private readonly CampaignRuntimeState _campaign;
        private readonly IPresentationFormatter _format;

        public CampaignPresentationQueries(CampaignRuntimeState campaign, IPresentationFormatter? formatter = null)
        { _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign)); _format = formatter ?? new InvariantPresentationFormatter(); }

        public MapReadModel BuildMap(PresentationViewerContext viewer, IMapPresentationDataProvider provider)
        {
            if (viewer == null || provider == null) throw new ArgumentNullException();
            var markers = provider.GetKnownMarkers(viewer).OrderBy(x => x.Entity).ToList().AsReadOnly();
            var routes = provider.GetKnownRoutes(viewer).OrderBy(x=>x.RouteId,StringComparer.Ordinal).ToList().AsReadOnly();
            var journeys = provider.GetKnownJourneys(viewer).OrderBy(x=>x.JourneyId,StringComparer.Ordinal).ToList().AsReadOnly();
            var snapshot=new MapPresentationSnapshot(markers,routes,journeys);
            var fields = new[] { Field("presentation.map.slice", "İstanbul · Marmara · Trakya", PresentationKnowledge.ExactSelf), Field("presentation.map.known-markers", markers.Count.ToString(), PresentationKnowledge.ExactSelf), Field("presentation.map.routes", routes.Count.ToString(), PresentationKnowledge.ExactSelf), Field("presentation.map.active-journeys", journeys.Count.ToString(), PresentationKnowledge.ExactSelf), Field("presentation.map.selected", "İstanbul", PresentationKnowledge.ExactSelf, Entity(PresentationEntityKind.City,"city-home")) };
            var details=new[]{new PresentationSection("presentation.map.locations",PresentationAvailability.Available,markers.Select(x=>Field("presentation.map.location",x.LabelKey,x.Knowledge,x.NavigationTarget))),new PresentationSection("presentation.map.route-graph",PresentationAvailability.Available,routes.Select(x=>Field("presentation.map.route",x.RouteId+" · "+x.ModeKey,PresentationKnowledge.ExactSelf))),new PresentationSection("presentation.map.travel-progress",PresentationAvailability.Available,journeys.Select(x=>Field("presentation.map.journey",x.ActorLabel+" · "+x.OriginLabel+" → "+x.DestinationLabel+" · "+x.SegmentIndex+"/"+x.SegmentCount,PresentationKnowledge.ExactSelf)))};
            var actions=markers.Where(x=>x.Entity.Kind==PresentationEntityKind.WorldLocation&&!StringComparer.Ordinal.Equals(x.Entity.Id,"istanbul")).Select(x=>new PresentationActionDescriptor("travel.start:"+x.Entity.Id,"presentation.action.travel-to."+x.LabelKey,journeys.Count==0,journeys.Count==0?string.Empty:"presentation.action.already-travelling",x.Entity,PresentationConfirmationPolicy.None,"travel-command-adapter")).Concat(new[]{new PresentationActionDescriptor("travel.advance-one-hour","presentation.action.advance-one-hour",journeys.Count>0,journeys.Count>0?string.Empty:"presentation.action.no-active-journey",new PresentationEntityRef(PresentationEntityKind.Character,"slice-player-sipahi"),PresentationConfirmationPolicy.None,"travel-command-adapter")});
            var state=new ScreenPresentationState(PresentationScreenId.Map,"presentation.screen.map",new PresentationEntityRef(PresentationEntityKind.WorldLocation,"istanbul"),new PresentationSection("presentation.section.current",PresentationAvailability.Available,fields),PresentationTrend.InsufficientHistory,PresentationAvailability.Unavailable,null,"presentation.why.not-exposed-by-gameplay",null,null,actions,null,details,snapshot);
            return new MapReadModel(state,snapshot);
        }

        public CityReadModel BuildCity(PresentationViewerContext viewer, CityId cityId)
        {
            var subject = Entity(PresentationEntityKind.City, cityId.Value);
            if (!viewer.CanReadExact(subject)) return new CityReadModel(ForeignFromReports(PresentationScreenId.City, "presentation.screen.city", subject, viewer, ReportSubjectKind.City));
            var city = _campaign.Cities.GetRequired(cityId);
            var current = new List<PresentationField>
            {
                Field("presentation.city.name", city.Definition.Name, PresentationKnowledge.ExactSelf),
                Field("presentation.city.population", _format.Integer(city.Metrics.PopulationCount), PresentationKnowledge.ExactSelf)
            };
            foreach (var area in city.OrderedAreas)
                current.Add(Field("presentation.city.area." + Key(area.Type), "presentation.city.fullness." + Key(area.Fullness), PresentationKnowledge.ExactSelf));
            var details = new List<PresentationSection>
            {
                new PresentationSection("presentation.city.areas", PresentationAvailability.Available,
                    city.OrderedAreas.Select(x => Field("presentation.city.area." + Key(x.Type), x.OrderedActiveBuildings.Count + " / " + "presentation.city.fullness." + Key(x.Fullness), PresentationKnowledge.ExactSelf))),
                new PresentationSection("presentation.city.infrastructure", PresentationAvailability.Available,
                    city.OrderedInfrastructure.Select(x => Field("presentation.city.infrastructure." + Key(x.Type), x.Installed ? "presentation.state.installed" : "presentation.state.not-installed", PresentationKnowledge.ExactSelf))),
                new PresentationSection("presentation.city.officials", PresentationAvailability.Available,
                    city.OrderedOfficials.Select(x => Field("presentation.city.official." + Key(x.Role), x.AssignmentId.Value, PresentationKnowledge.ExactSelf, Entity(PresentationEntityKind.Organization, x.OrganizationId.Value)))),
                new PresentationSection("presentation.city.production", PresentationAvailability.Available,
                    _campaign.Economy.Recipes.OrderedRecipes.Where(recipe => city.GetRequiredArea(CityBuildingRules.RequiredArea(recipe.BuildingKind)).OrderedActiveBuildings.Any(building => building.Kind == recipe.BuildingKind)).Select(recipe => Field("presentation.production." + recipe.Id.Value, recipe.Name, PresentationKnowledge.ExactSelf)))
            };
            var religionProfile = _campaign.Religion.OrderedProfiles.FirstOrDefault(x => x.Target.Kind == ReligionProfileTargetKind.City && StringComparer.Ordinal.Equals(x.Target.Id, cityId.Value));
            details.Add(religionProfile == null
                ? PresentationSection.Unavailable("presentation.city.religion", "presentation.reason.religion-profile-unavailable")
                : new PresentationSection("presentation.city.religion", PresentationAvailability.Available, religionProfile.Entries.Select(x => Field("presentation.religion." + x.ReligionId.Value, x.SectId.HasValue ? x.SectId.Value.Value + " · " + x.RelativePresence : x.RelativePresence.ToString(), PresentationKnowledge.ExactSelf))));
            var market = _campaign.Economy.OrderedMarkets.FirstOrDefault(x => x.CityId.Equals(cityId));
            if (market != null)
                details.Add(new PresentationSection("presentation.city.market", PresentationAvailability.Available,
                    market.Stock.OrderedStocks.Select(x => Field("presentation.good." + x.GoodId.Value, _format.Integer(x.Quantity.Value), PresentationKnowledge.ExactSelf))));
            else details.Add(PresentationSection.Unavailable("presentation.city.market", "presentation.reason.market-not-configured"));
            return new CityReadModel(Standard(PresentationScreenId.City, "presentation.screen.city", subject, current, null, null, details));
        }

        public CharacterReadModel BuildCharacter(PresentationViewerContext viewer, CharacterId characterId)
        {
            var subject = Entity(PresentationEntityKind.Character, characterId.Value);
            if (!viewer.CanReadExact(subject)) return new CharacterReadModel(Unknown(PresentationScreenId.Character, "presentation.screen.character", subject));
            var character = _campaign.Characters.GetRequired(characterId);
            var fields = new List<PresentationField>
            {
                Field("presentation.character.name", character.Definition.DisplayName, PresentationKnowledge.ExactSelf),
                Field("presentation.character.importance", character.Importance.HasValue ? "presentation.importance." + Key(character.Importance.Value) : "presentation.value.not-applicable", PresentationKnowledge.ExactSelf),
                Field("presentation.character.location", "presentation.location." + Key(character.Location.Kind), PresentationKnowledge.ExactSelf),
                Field("presentation.character.loyalty", character.CurrentLoyalty.Value.ToString(), PresentationKnowledge.ExactSelf),
                Field("presentation.character.satisfaction", character.Satisfaction.Value.ToString(), PresentationKnowledge.ExactSelf),
                Field("presentation.character.reputation", character.BaseReputation.Value.ToString(), PresentationKnowledge.ExactSelf),
                Field("presentation.character.standing", character.CurrentStanding.Value.ToString(), PresentationKnowledge.ExactSelf),
                Field("presentation.character.life", "presentation.life." + Key(character.LifeStatus), PresentationKnowledge.ExactSelf),
                Field("presentation.character.injury", character.Injury == null ? "presentation.state.none" : "presentation.injury." + Key(character.Injury.Severity), PresentationKnowledge.ExactSelf),
                Field("presentation.character.captivity", character.Captivity == null ? "presentation.state.none" : "presentation.state.captive", PresentationKnowledge.ExactSelf)
            };
            var memberships = _campaign.Organizations.OrderedOrganizations.SelectMany(o => o.Memberships.Where(m => m.CharacterId.Equals(characterId) && m.IsActive).Select(m => Field("presentation.organization.membership", o.Name + " · presentation.branch." + Key(m.Branch), PresentationKnowledge.ExactSelf, Entity(PresentationEntityKind.Organization, o.Id.Value))));
            var assignments = _campaign.Organizations.OrderedOrganizations.SelectMany(o => o.OrderedAssignments.Where(a => a.CharacterId.Equals(characterId) && a.IsActive).Select(a => Field("presentation.organization.assignment", a.RoleCode + " · presentation.authority." + Key(a.Authority), PresentationKnowledge.ExactSelf, Entity(PresentationEntityKind.Organization, o.Id.Value))));
            var houses = _campaign.Houses.OrderedHouses.Where(h => h.OrderedMembers.Any(m => m.CharacterId.Equals(characterId) && m.IsActive)).Select(h => Field("presentation.character.house", h.Definition.Name, PresentationKnowledge.ExactSelf, Entity(PresentationEntityKind.House, h.Id.Value)));
            var cliques = _campaign.Cliques.OrderedCliques.Where(c => c.OrderedMemberships.Any(m => m.CharacterId.Equals(characterId) && m.IsActive)).Select(c => Field("presentation.character.clique", c.Definition.Name, PresentationKnowledge.ExactSelf, Entity(PresentationEntityKind.Clique, c.Id.Value)));
            var religion = _campaign.Religion.Characters.OrderedStates.FirstOrDefault(x => x.CharacterId.Equals(characterId));
            var relationFields = _campaign.Characters.Relations.OrderedRelations.Where(x => x.Key.First.Equals(characterId) || x.Key.Second.Equals(characterId)).Select(x =>
            {
                var other = x.Key.First.Equals(characterId) ? x.Key.Second : x.Key.First;
                return Field("presentation.character.relation", x.Value.ToString(), PresentationKnowledge.ExactSelf, Entity(PresentationEntityKind.Character, other.Value));
            });
            var contracts = _campaign.EncounterContracts.Contracts.OrderedContracts.Where(x => x.AssigneeId.HasValue && x.AssigneeId.Value.Equals(characterId)).Select(x => Field("presentation.contract-category." + Key(x.Category), "presentation.contract-lifecycle." + Key(x.Lifecycle), PresentationKnowledge.ExactSelf, Entity(PresentationEntityKind.Contract, x.Id.Value)));
            var details = new[]
            {
                new PresentationSection("presentation.character.memberships", PresentationAvailability.Available, memberships),
                new PresentationSection("presentation.character.assignments", PresentationAvailability.Available, assignments),
                new PresentationSection("presentation.character.houses", PresentationAvailability.Available, houses),
                new PresentationSection("presentation.character.cliques", PresentationAvailability.Available, cliques),
                religion == null ? PresentationSection.Unavailable("presentation.character.religion", "presentation.reason.religion-unassigned") : new PresentationSection("presentation.character.religion", PresentationAvailability.Available, new[] { Field("presentation.character.religion", religion.ReligionId.Value, PresentationKnowledge.ExactSelf), Field("presentation.character.sect", religion.SectId.HasValue ? religion.SectId.Value.Value : "presentation.value.none", PresentationKnowledge.ExactSelf) }),
                new PresentationSection("presentation.character.relations", PresentationAvailability.Available, relationFields),
                new PresentationSection("presentation.character.contracts", PresentationAvailability.Available, contracts),
                new PresentationSection("presentation.character.stats", PresentationAvailability.Available, CharacterStats(character))
            };
            return new CharacterReadModel(Standard(PresentationScreenId.Character, "presentation.screen.character", subject, fields, null, null, details));
        }

        public OrganizationReadModel BuildOrganization(PresentationViewerContext viewer, OrganizationId organizationId)
        {
            var subject = Entity(PresentationEntityKind.Organization, organizationId.Value);
            if (!viewer.CanReadExact(subject)) return new OrganizationReadModel(Unknown(PresentationScreenId.Organization, "presentation.screen.organization", subject));
            var organization = _campaign.Organizations.GetRequired(organizationId);
            var fields = new List<PresentationField> { Field("presentation.organization.name", organization.Name, PresentationKnowledge.ExactSelf) };
            foreach (OrganizationBranch branch in Enum.GetValues(typeof(OrganizationBranch)))
            {
                var members = organization.Memberships.Count(x => x.IsActive && x.Branch == branch);
                var assignments = organization.OrderedAssignments.Count(x => x.IsActive && x.Branch == branch);
                fields.Add(Field("presentation.branch." + Key(branch), members + " · " + assignments, PresentationKnowledge.ExactSelf));
            }
            var details = new[]
            {
                new PresentationSection("presentation.organization.memberships", PresentationAvailability.Available, organization.Memberships.Where(x => x.IsActive).Select(x => Field("presentation.branch." + Key(x.Branch), x.CharacterId.Value + " · presentation.membership." + Key(x.MembershipType), PresentationKnowledge.ExactSelf, Entity(PresentationEntityKind.Character, x.CharacterId.Value)))),
                new PresentationSection("presentation.organization.assignments", PresentationAvailability.Available, organization.OrderedAssignments.Where(x => x.IsActive).Select(x => Field(x.RoleCode, x.CharacterId.Value + " · presentation.authority." + Key(x.Authority), PresentationKnowledge.ExactSelf, Entity(PresentationEntityKind.Character, x.CharacterId.Value))))
            };
            return new OrganizationReadModel(Standard(PresentationScreenId.Organization, "presentation.screen.organization", subject, fields, null, null, details));
        }

        public TradeReadModel BuildTrade(PresentationViewerContext viewer, CityId cityId, PriceQuote? selectedQuote = null)
        {
            var subject = Entity(PresentationEntityKind.City, cityId.Value);
            if (!viewer.CanReadExact(subject)) return new TradeReadModel(ForeignFromReports(PresentationScreenId.Trade, "presentation.screen.trade", subject, viewer, ReportSubjectKind.Market));
            var market = _campaign.Economy.GetRequiredMarket(cityId);
            var fields = new List<PresentationField> { Field("presentation.trade.cash", _format.Money(market.CashBalance.Value), PresentationKnowledge.ExactSelf) };
            fields.AddRange(market.Stock.OrderedStocks.Select(x => Field("presentation.good." + x.GoodId.Value, _format.Integer(x.Quantity.Value), PresentationKnowledge.ExactSelf)));
            var factors = selectedQuote == null ? Array.Empty<PresentationFactor>() : selectedQuote.OrderedAdjustments.Select(x => new PresentationFactor("presentation.price-factor." + Key(x.Source), x.SignedUnitAmount.ToString(), x.SourceId, PresentationKnowledge.ExactSelf)).ToArray();
            return new TradeReadModel(Standard(PresentationScreenId.Trade, "presentation.screen.trade", subject, fields, factors.Length == 0 ? null : factors, null, new[]
            {
                new PresentationSection("presentation.trade.demand", PresentationAvailability.Available, market.Demand.OrderedSources.Select(x => Field("presentation.good." + x.GoodId.Value, _format.Integer(x.Quantity.Value), PresentationKnowledge.ExactSelf))),
                new PresentationSection("presentation.trade.caravans", PresentationAvailability.Available, _campaign.Economy.Caravans.OrderedCaravans.Where(x => x.OriginCityId.Equals(cityId) || x.DestinationCityId.Equals(cityId)).Select(x => Field("presentation.caravan." + x.Id.Value, x.LocationStage + " · " + x.Cargo.UsedWeight(_campaign.Economy.Goods) + "/" + x.WeightCapacity, PresentationKnowledge.ExactSelf, Entity(PresentationEntityKind.Caravan, x.Id.Value))))
            }));
        }

        public ArmyReadModel BuildArmy(PresentationViewerContext viewer, ArmyId armyId)
        {
            var subject = Entity(PresentationEntityKind.Army, armyId.Value);
            if (!viewer.CanReadExact(subject)) return new ArmyReadModel(ForeignFromReports(PresentationScreenId.Army, "presentation.screen.army", subject, viewer, ReportSubjectKind.Army));
            var army = _campaign.Military.Armies.GetRequired(armyId);
            var fields = new[]
            {
                Field("presentation.army.name", army.Name, PresentationKnowledge.ExactSelf),
                Field("presentation.army.owner", "presentation.army-owner." + Key(army.Owner.Kind) + ":" + army.Owner.Id, PresentationKnowledge.ExactSelf),
                Field("presentation.army.controller", "presentation.army-owner." + Key(army.Controller.Kind) + ":" + army.Controller.Id, PresentationKnowledge.ExactSelf),
                Field("presentation.army.commander", army.Commander == null ? "presentation.state.unassigned" : army.Commander.CharacterId.Value, PresentationKnowledge.ExactSelf, army.Commander == null ? (PresentationEntityRef?)null : Entity(PresentationEntityKind.Character, army.Commander.CharacterId.Value)),
                Field("presentation.army.headcount", _format.Integer(army.Headcount), PresentationKnowledge.ExactSelf),
                Field("presentation.army.morale", "presentation.morale." + Key(army.Morale.Assessment), PresentationKnowledge.ExactSelf),
                Field("presentation.army.fatigue", "presentation.fatigue." + Key(army.Fatigue.Assessment), PresentationKnowledge.ExactSelf),
                Field("presentation.army.discipline", "presentation.discipline." + Key(army.Discipline.Assessment), PresentationKnowledge.ExactSelf)
            };
            var risks = army.OrderedSupplyRequirements.Where(x => x.Assess(army.Supply) == SupplyCondition.Exhausted).Select(x => new PresentationAttentionItem("presentation.risk.supply-exhausted", PresentationSeverity.Critical, "presentation.reason.supply-exhausted", subject, "army-supply-transfer"));
            var details = new[]
            {
                new PresentationSection("presentation.army.units", PresentationAvailability.Available, army.OrderedUnits.Select(x => Field("presentation.unit." + x.Id.Value, _format.Integer(x.Headcount), PresentationKnowledge.ExactSelf, Entity(PresentationEntityKind.UnitGroup, x.Id.Value)))),
                new PresentationSection("presentation.army.soldiers", PresentationAvailability.Available, _campaign.Soldiers.Soldiers.OrderedSoldiers.Where(x => army.OrderedUnits.Any(unit => unit.Id.Equals(x.UnitGroupId))).Select(x => Field("presentation.soldier." + x.Id.Value, x.TroopDefinitionId.Value + " · " + x.CombatRoleId.Value + " · presentation.soldier-lifecycle." + Key(x.Lifecycle) + " · equipment=" + (x.Loadout.OrderedWeapons.Count + x.Loadout.OrderedArmor.Count + (x.Loadout.Shield.HasValue ? 1 : 0) + (x.Loadout.Mount.HasValue ? 1 : 0)), PresentationKnowledge.ExactSelf, Entity(PresentationEntityKind.Soldier, x.Id.Value)))),
                new PresentationSection("presentation.army.supply", PresentationAvailability.Available, army.Supply.OrderedGoods.Select(x => Field("presentation.good." + x.GoodId.Value, _format.Integer(x.Quantity.Value), PresentationKnowledge.ExactSelf))),
                new PresentationSection("presentation.army.payroll", PresentationAvailability.Available, army.Payroll.OrderedObligations.Select(x => Field("presentation.payroll." + x.Id.Value, _format.Money(x.Arrears), PresentationKnowledge.ExactSelf)))
            };
            return new ArmyReadModel(Standard(PresentationScreenId.Army, "presentation.screen.army", subject, fields, null, risks, details));
        }

        public DiplomacyReadModel BuildDiplomacy(PresentationViewerContext viewer)
        {
            var actions = _campaign.Diplomacy.Actions.OrderedActions.Where(x => x.SourceActorId.Equals(viewer.ActorId));
            var missions = _campaign.Diplomacy.Missions.OrderedMissions.Where(x => x.SourceActorId.Equals(viewer.ActorId));
            var fields = new[]
            {
                Field("presentation.diplomacy.actor", viewer.ActorId.Value, PresentationKnowledge.ExactSelf),
                Field("presentation.diplomacy.actions", actions.Count().ToString(), PresentationKnowledge.ExactSelf),
                Field("presentation.diplomacy.missions", missions.Count().ToString(), PresentationKnowledge.ExactSelf)
            };
            var details = new[]
            {
                new PresentationSection("presentation.diplomacy.relations", PresentationAvailability.Available, _campaign.Diplomacy.Relations.OrderedRelations.Where(x => x.Pair.First.Equals(viewer.ActorId) || x.Pair.Second.Equals(viewer.ActorId)).Select(x => Field("presentation.diplomacy.relation", "presentation.disposition." + Key(x.Disposition), PresentationKnowledge.ExactSelf, Entity(PresentationEntityKind.Faction, x.Pair.First.Equals(viewer.ActorId) ? x.Pair.Second.Value : x.Pair.First.Value)))),
                new PresentationSection("presentation.diplomacy.factors", PresentationAvailability.Available, _campaign.Diplomacy.Relations.OrderedRelations.Where(x => x.Pair.First.Equals(viewer.ActorId) || x.Pair.Second.Equals(viewer.ActorId)).SelectMany(x => x.OrderedFactors).Select(x => Field("presentation.diplomatic-factor." + Key(x.Source), "presentation.factor-direction." + Key(x.Direction), PresentationKnowledge.ExactSelf))),
                new PresentationSection("presentation.diplomacy.actions", PresentationAvailability.Available, actions.Select(x => Field("presentation.diplomatic-action." + Key(x.Kind), "presentation.action-status." + Key(x.Status), PresentationKnowledge.ExactSelf, Entity(PresentationEntityKind.Faction, x.TargetActorId.Value)))),
                new PresentationSection("presentation.diplomacy.reports", PresentationAvailability.Available, AvailableReports(viewer).Select(x => Field("presentation.report." + x.Id.Value, "presentation.report-quality." + Key(x.Quality), ReportKnowledge(x))))
            };
            var availableActions = actions.Where(x => x.Status == DiplomaticActionStatus.Ordered).Select(x => new PresentationActionDescriptor(
                "diplomacy.dispatch:" + x.Id.Value, "presentation.action.dispatch-envoy", true, string.Empty,
                Entity(PresentationEntityKind.Faction, x.TargetActorId.Value), PresentationConfirmationPolicy.None, "diplomacy-command-adapter"));
            return new DiplomacyReadModel(Standard(PresentationScreenId.Diplomacy, "presentation.screen.diplomacy", Entity(PresentationEntityKind.Faction, viewer.ActorId.Value), fields, null, null, details, availableActions));
        }

        public BattleReadModel BuildBattle(PresentationViewerContext viewer, BattleId battleId)
        {
            var subject = Entity(PresentationEntityKind.Battle, battleId.Value);
            if (!viewer.CanReadExact(subject)) return new BattleReadModel(Unknown(PresentationScreenId.Battle, "presentation.screen.battle", subject));
            var battle = _campaign.Battles.Battles.GetRequired(battleId);
            var fields = new[]
            {
                Field("presentation.battle.lifecycle", "presentation.battle-lifecycle." + Key(battle.Lifecycle), PresentationKnowledge.ExactSelf),
                Field("presentation.battle.step", battle.Step.ToString(), PresentationKnowledge.ExactSelf),
                Field("presentation.battle.sides", battle.OrderedSides.Count.ToString(), PresentationKnowledge.ExactSelf),
                Field("presentation.battle.sectors", battle.Sectors.OrderedSectors.Count.ToString(), PresentationKnowledge.ExactSelf),
                Field("presentation.battle.result", battle.Result == null ? "presentation.state.pending" : "presentation.battle-result.available", PresentationKnowledge.ExactSelf)
            };
            var details = new[]
            {
                new PresentationSection("presentation.battle.sectors", PresentationAvailability.Available, battle.Sectors.OrderedSectors.Select(x => Field("presentation.sector." + x.Id.Value, x.OrderedAdjacent.Count + " · " + x.OrderedEligibleSides.Count, PresentationKnowledge.ExactSelf))),
                new PresentationSection("presentation.battle.deployments", PresentationAvailability.Available, battle.OrderedDeployments.Select(x => Field("presentation.deployment." + x.Id.Value, x.SectorId.Value + " · presentation.formation." + Key(x.Formation), PresentationKnowledge.ExactSelf, Entity(PresentationEntityKind.UnitGroup, x.UnitGroupId.Value)))),
                new PresentationSection("presentation.battle.orders", PresentationAvailability.Available, battle.OrderedOrders.Select(x => Field("presentation.battle-order." + Key(x.Kind), x.GroupId.Value + " · " + (x.TargetSector.HasValue ? x.TargetSector.Value.Value : "presentation.value.none"), PresentationKnowledge.ExactSelf)))
            };
            var resultDetails = details.ToList();
            if (battle.Result != null)
                resultDetails.Add(new PresentationSection("presentation.battle.result", PresentationAvailability.Available, battle.Result.OrderedUnitOutcomes.Select(x => Field("presentation.unit." + x.UnitGroupId.Value, "losses=" + x.AggregateLosses + " · morale=" + Key(x.Morale) + " · fatigue=" + Key(x.Fatigue) + " · discipline=" + Key(x.Discipline), PresentationKnowledge.ExactSelf, Entity(PresentationEntityKind.UnitGroup, x.UnitGroupId.Value)))));
            return new BattleReadModel(Standard(PresentationScreenId.Battle, "presentation.screen.battle", subject, fields, null, null, resultDetails));
        }

        public ReportsReadModel BuildReports(PresentationViewerContext viewer)
        {
            var reports = AvailableReports(viewer).OrderByDescending(x => x.ArrivedAt!.Value.Ticks).ThenBy(x => x.Id).Select(x => new ReportRowPresentation(
                Entity(PresentationEntityKind.Report, x.Id.Value), "presentation.report-type." + Key(x.Type), "presentation.report-source." + Key(x.Source.Kind),
                x.ObservedAt.Ticks, x.ArrivedAt?.Ticks, x.StalenessAt(_campaign.Clock.Now), "presentation.report-quality." + Key(x.Quality),
                "presentation.report-precision." + Key(ReportPrecision(x)), "presentation.report-detail." + Key(x.DetailLevel))).ToList().AsReadOnly();
            var fields = new[] { Field("presentation.reports.delivered", reports.Count.ToString(), PresentationKnowledge.ExactSelf) };
            return new ReportsReadModel(Standard(PresentationScreenId.Reports, "presentation.screen.reports", Entity(PresentationEntityKind.Faction, viewer.ActorId.Value), fields, null, null, null), reports);
        }

        public EncounterContractReadModel BuildEncounterContracts(PresentationViewerContext viewer)
        {
            var encounters = _campaign.EncounterContracts.Encounters.OrderedEncounters;
            var contracts = _campaign.EncounterContracts.Contracts.OrderedContracts;
            var fields = new[]
            {
                Field("presentation.encounter.count", encounters.Count.ToString(), PresentationKnowledge.ExactSelf),
                Field("presentation.contract.count", contracts.Count.ToString(), PresentationKnowledge.ExactSelf)
            };
            var details = new[]
            {
                new PresentationSection("presentation.encounters", PresentationAvailability.Available, encounters.Select(x => Field("presentation.encounter-type." + Key(x.Type), "presentation.encounter-lifecycle." + Key(x.Lifecycle), PresentationKnowledge.ExactSelf, Entity(PresentationEntityKind.Encounter, x.Id.Value)))),
                new PresentationSection("presentation.contracts", PresentationAvailability.Available, contracts.Select(x => Field("presentation.contract-category." + Key(x.Category), "presentation.contract-lifecycle." + Key(x.Lifecycle), PresentationKnowledge.ExactSelf, Entity(PresentationEntityKind.Contract, x.Id.Value))))
            };
            var actions = encounters.Where(x => x.Lifecycle == EncounterLifecycle.Available).Select(x => new PresentationActionDescriptor(
                    "encounter.engage:" + x.Id.Value, "presentation.action.engage-encounter", true, string.Empty,
                    Entity(PresentationEntityKind.Encounter, x.Id.Value), PresentationConfirmationPolicy.None, "encounter-command-adapter"))
                .Concat(contracts.Where(x => x.Lifecycle == ContractLifecycle.Offered).Select(x => new PresentationActionDescriptor(
                    "contract.accept:" + x.Id.Value, "presentation.action.accept-contract", true, string.Empty,
                    Entity(PresentationEntityKind.Contract, x.Id.Value), PresentationConfirmationPolicy.None, "contract-command-adapter")));
            return new EncounterContractReadModel(Standard(PresentationScreenId.EncounterContract, "presentation.screen.encounter-contract", null, fields, null, null, details, actions));
        }

        public ScreenPresentationState BuildEmpty(PresentationScreenId screen) => Unknown(screen, "presentation.screen." + Key(screen), null, "presentation.reason.no-selection");

        public ScreenPresentationState BuildHouse(PresentationViewerContext viewer, HouseId houseId)
        {
            var subject = Entity(PresentationEntityKind.House, houseId.Value); if (!viewer.CanReadExact(subject)) return Unknown(PresentationScreenId.Organization, "presentation.screen.house", subject);
            var house = _campaign.Houses.GetRequired(houseId);
            var fields = new[] { Field("presentation.house.name", house.Definition.Name, PresentationKnowledge.ExactSelf), Field("presentation.house.head", house.HeadId.HasValue ? house.HeadId.Value.Value : "presentation.state.unassigned", PresentationKnowledge.ExactSelf), Field("presentation.house.wealth", _format.Money(house.Wealth.Value), PresentationKnowledge.ExactSelf), Field("presentation.house.prestige", house.Prestige.Value.ToString(), PresentationKnowledge.ExactSelf), Field("presentation.house.lifecycle", "presentation.house-lifecycle." + Key(house.Lifecycle), PresentationKnowledge.ExactSelf) };
            var details = new[] { new PresentationSection("presentation.house.members", PresentationAvailability.Available, house.OrderedMembers.Select(x => Field("presentation.house.member", x.IsActive ? "presentation.state.active" : "presentation.state.inactive", PresentationKnowledge.ExactSelf, Entity(PresentationEntityKind.Character, x.CharacterId.Value)))), new PresentationSection("presentation.house.properties", PresentationAvailability.Available, house.Properties.Select(x => Field("presentation.house-property." + Key(x.Kind), x.AssetId, PresentationKnowledge.ExactSelf))) };
            return Standard(PresentationScreenId.Organization, "presentation.screen.house", subject, fields, null, null, details);
        }

        public ScreenPresentationState BuildClique(PresentationViewerContext viewer, CliqueId cliqueId)
        {
            var subject = Entity(PresentationEntityKind.Clique, cliqueId.Value); if (!viewer.CanReadExact(subject)) return Unknown(PresentationScreenId.Organization, "presentation.screen.clique", subject);
            var clique = _campaign.Cliques.GetRequired(cliqueId);
            var fields = new[] { Field("presentation.clique.name", clique.Definition.Name, PresentationKnowledge.ExactSelf), Field("presentation.clique.type", "presentation.clique-type." + Key(clique.Definition.Type), PresentationKnowledge.ExactSelf), Field("presentation.clique.leader", clique.LeaderId.HasValue ? clique.LeaderId.Value.Value : "presentation.state.unassigned", PresentationKnowledge.ExactSelf), Field("presentation.clique.influence", clique.Influence.ToString(), PresentationKnowledge.ExactSelf), Field("presentation.clique.ownership", "presentation.clique.no-army-soldier-goods-ownership", PresentationKnowledge.ExactSelf) };
            var details = new[] { new PresentationSection("presentation.clique.members", PresentationAvailability.Available, clique.OrderedMemberships.Select(x => Field("presentation.clique.member", x.RoleCode, PresentationKnowledge.ExactSelf, Entity(PresentationEntityKind.Character, x.CharacterId.Value)))), new PresentationSection("presentation.clique.influence-sources", PresentationAvailability.Available, clique.InfluenceSources.Select(x => Field("presentation.influence-source." + Key(x.Kind), x.Contribution.ToString(), PresentationKnowledge.ExactSelf, Entity(PresentationEntityKind.Character, x.CharacterId.Value)))) };
            return Standard(PresentationScreenId.Organization, "presentation.screen.clique", subject, fields, null, null, details);
        }

        public ScreenPresentationState BuildLedger(PresentationViewerContext viewer)
        {
            var rows = _campaign.Economy.Caravans.OrderedCaravans.Where(x => viewer.CanReadExact(Entity(PresentationEntityKind.Caravan, x.Id.Value))).Select(x => Field("presentation.caravan." + x.Id.Value, "purchase=" + x.Accounting.PurchaseCost.Value + " · sale=" + x.Accounting.SaleRevenue.Value + " · operating=" + x.Accounting.OperatingCost.Value + " · tariffs=" + x.Accounting.Tariffs.Value + " · losses=" + x.Accounting.Losses.Value + " · net=" + x.Accounting.NetResult, PresentationKnowledge.ExactSelf, Entity(PresentationEntityKind.Caravan, x.Id.Value)));
            return Standard(PresentationScreenId.Ledger, "presentation.screen.ledger", viewer.ControlledIdentity, rows, null, null, null);
        }

        private ScreenPresentationState ForeignFromReports(PresentationScreenId screen, string titleKey, PresentationEntityRef subject, PresentationViewerContext viewer, ReportSubjectKind subjectKind)
        {
            var observations = AvailableReports(viewer).SelectMany(report => report.OrderedObservations.Where(x => x.Subject.Kind == subjectKind && StringComparer.Ordinal.Equals(x.Subject.Id, subject.Id)).Select(x => new { Report = report, Observation = x })).OrderByDescending(x => x.Report.ObservedAt.Ticks).ToList();
            if (observations.Count == 0) return Unknown(screen, titleKey, subject);
            var fields = observations.Select(x => Field("presentation.observation." + Key(x.Observation.Kind), ObservationValue(x.Observation), ReportKnowledge(x.Report, x.Observation.Precision))).ToList();
            return Standard(screen, titleKey, subject, fields, null, null, new[] { new PresentationSection("presentation.information.provenance", PresentationAvailability.Available, observations.Select(x => Field("presentation.report." + x.Report.Id.Value, _format.WorldTime(x.Report.ObservedAt.Ticks), ReportKnowledge(x.Report)))) });
        }

        private IReadOnlyCollection<ReportState> AvailableReports(PresentationViewerContext viewer)
        {
            var information = _campaign.Diplomacy.Information.OrderedInformation.FirstOrDefault(x => x.ActorId.Equals(viewer.ActorId));
            return information == null ? Array.Empty<ReportState>() : information.OrderedAvailableReports;
        }

        private ScreenPresentationState Standard(PresentationScreenId screen, string titleKey, PresentationEntityRef? subject, IEnumerable<PresentationField> fields, IEnumerable<PresentationFactor>? why, IEnumerable<PresentationAttentionItem>? risks, IEnumerable<PresentationSection>? details, IEnumerable<PresentationActionDescriptor>? actions = null)
        {
            var whyList = (why ?? Array.Empty<PresentationFactor>()).ToList();
            return new ScreenPresentationState(screen, titleKey, subject,
                new PresentationSection("presentation.section.current", PresentationAvailability.Available, fields),
                PresentationTrend.InsufficientHistory,
                whyList.Count == 0 ? PresentationAvailability.Unavailable : PresentationAvailability.Available,
                whyList,
                whyList.Count == 0 ? "presentation.why.not-exposed-by-gameplay" : string.Empty,
                risks,
                Array.Empty<PresentationAttentionItem>(),
                actions,
                risks,
                details);
        }

        private static ScreenPresentationState Unknown(PresentationScreenId screen, string titleKey, PresentationEntityRef? subject, string reason = "presentation.reason.unknown-to-viewer") =>
            new ScreenPresentationState(screen, titleKey, subject,
                PresentationSection.Unavailable("presentation.section.current", reason, PresentationAvailability.Unknown),
                PresentationTrend.InsufficientHistory,
                PresentationAvailability.Unknown, null, reason, null, null, null);

        private static IEnumerable<PresentationField> CharacterStats(CharacterState x)
        {
            yield return Field("presentation.stat.intelligence", x.Stats.Intelligence.Value.ToString(), PresentationKnowledge.ExactSelf);
            yield return Field("presentation.stat.observation", x.Stats.Observation.Value.ToString(), PresentationKnowledge.ExactSelf);
            yield return Field("presentation.stat.persuasion", x.Stats.Persuasion.Value.ToString(), PresentationKnowledge.ExactSelf);
            yield return Field("presentation.stat.leadership", x.Stats.Leadership.Value.ToString(), PresentationKnowledge.ExactSelf);
            yield return Field("presentation.stat.command", x.Stats.Command.Value.ToString(), PresentationKnowledge.ExactSelf);
            yield return Field("presentation.stat.trade", x.Stats.Trade.Value.ToString(), PresentationKnowledge.ExactSelf);
            yield return Field("presentation.stat.administration", x.Stats.Administration.Value.ToString(), PresentationKnowledge.ExactSelf);
            yield return Field("presentation.stat.courage", x.Stats.Courage.Value.ToString(), PresentationKnowledge.ExactSelf);
            yield return Field("presentation.stat.experience", x.Stats.Experience.Value.ToString(), PresentationKnowledge.ExactSelf);
        }

        private PresentationKnowledge ReportKnowledge(ReportState report, ObservationPrecision? precision = null) => new PresentationKnowledge(
            report.StalenessAt(_campaign.Clock.Now) > 0 ? PresentationAvailability.Stale : PresentationAvailability.Available,
            precision.HasValue ? ToPrecision(precision.Value) : ToPrecision(ReportPrecision(report)),
            "presentation.knowledge.delivered-report", report.ObservedAt.Ticks, report.ArrivedAt?.Ticks, report.StalenessAt(_campaign.Clock.Now));

        private static ObservationPrecision ReportPrecision(ReportState report)
        {
            var precisions = report.OrderedObservations.Select(x => x.Precision).Distinct().ToList();
            return precisions.Count == 1 ? precisions[0] : ObservationPrecision.Qualitative;
        }
        private static PresentationPrecision ToPrecision(ObservationPrecision value)
        {
            switch (value)
            {
                case ObservationPrecision.Exact: return PresentationPrecision.Exact;
                case ObservationPrecision.Approximate: return PresentationPrecision.Approximate;
                case ObservationPrecision.Range: return PresentationPrecision.Range;
                case ObservationPrecision.Qualitative: return PresentationPrecision.Qualitative;
                default: return PresentationPrecision.Unknown;
            }
        }
        private string ObservationValue(ReportObservation observation)
        {
            switch (observation.Precision)
            {
                case ObservationPrecision.Exact: return _format.Integer(observation.Lower!.Value);
                case ObservationPrecision.Approximate: return "~" + _format.Integer(observation.Lower!.Value);
                case ObservationPrecision.Range: return _format.Integer(observation.Lower!.Value) + "–" + _format.Integer(observation.Upper!.Value);
                case ObservationPrecision.Qualitative: return "presentation.qualitative." + Key(observation.Qualitative);
                default: return "presentation.value.unknown";
            }
        }
        private static PresentationField Field(string key, string value, PresentationKnowledge knowledge, PresentationEntityRef? link = null) => new PresentationField(key, value, knowledge, link);
        private static PresentationEntityRef Entity(PresentationEntityKind kind, string id) => new PresentationEntityRef(kind, id);
        private static string Key(object value) => value.ToString()!.Replace("_", "-").ToLowerInvariant();
    }
}
