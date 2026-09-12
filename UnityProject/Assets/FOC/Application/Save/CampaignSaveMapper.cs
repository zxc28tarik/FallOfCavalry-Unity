using System;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Random;
using FOC.Domain.Time;

namespace FOC.Application.Save
{
    public static partial class CampaignSaveMapper
    {
        public static CampaignSaveData ToSaveData(CampaignRuntimeState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var randomState = state.Random.CaptureState();
            var data = new CampaignSaveData
            {
                SaveVersion = CampaignSaveData.CurrentSaveVersion,
                CampaignId = state.CampaignId.Value,
                GameVersion = state.GameVersion,
                ContentDataVersion = state.ContentDataVersion,
                WorldSeed = state.WorldSeed,
                WorldGenRevision = state.WorldGenRevision,
                WorldTime = state.Clock.Now.Ticks,
                RngState = randomState.State,
                RngDrawCount = randomState.DrawCount,
            };

            foreach (var character in state.Characters.OrderedCharacters)
            {
                data.Characters.Add(ToCharacterSaveData(character));
            }

            foreach (var relation in state.Characters.Relations.OrderedRelations)
            {
                data.CharacterRelations.Add(new CharacterRelationSaveData
                {
                    FirstCharacterId = relation.Key.First.Value,
                    SecondCharacterId = relation.Key.Second.Value,
                    Value = relation.Value,
                });
            }

            AddSocialSaveData(state, data);
            AddReligionSaveData(state, data);
            AddCitySaveData(state, data);

            return data;
        }

        public static CampaignRuntimeState ToRuntimeState(CampaignSaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (!new CampaignSaveValidator().Validate(data).IsValid)
            {
                throw new InvalidOperationException("Campaign save data violates invariants and cannot become runtime state.");
            }

            if (data.SaveVersion != CampaignSaveData.CurrentSaveVersion)
            {
                throw new InvalidOperationException("Save data must be migrated to the current schema before runtime reconstruction.");
            }

            var roster = new CharacterRoster();
            foreach (var character in data.Characters)
            {
                roster.Add(ToCharacterState(character));
            }

            foreach (var relation in data.CharacterRelations)
            {
                roster.SetRelation(CharacterId.Create(relation.FirstCharacterId), CharacterId.Create(relation.SecondCharacterId), relation.Value);
            }

            var social = RestoreSocial(data, roster);
            var religion = RestoreReligion(data);
            var cities = RestoreCities(data);
            return new CampaignRuntimeState(
                StableId<CampaignTag>.Create(data.CampaignId),
                data.GameVersion,
                data.ContentDataVersion,
                data.WorldSeed,
                data.WorldGenRevision,
                new WorldClock(new WorldTimestamp(data.WorldTime)),
                new SeededRandomSource(new RandomState(data.RngState, data.RngDrawCount)),
                roster,
                social.Organizations,
                social.Houses,
                social.Cliques,
                religion,
                cities);
        }

        private static CharacterSaveData ToCharacterSaveData(CharacterState state)
        {
            var data = new CharacterSaveData
            {
                CharacterId = state.Id.Value,
                IdentityKind = (int)state.Definition.IdentityKind,
                DisplayName = state.Definition.DisplayName,
                Provenance = (int)state.Definition.Provenance,
                Importance = state.Importance.HasValue ? (int?)state.Importance.Value : null,
                Intelligence = state.Stats.Intelligence.Value,
                Observation = state.Stats.Observation.Value,
                Persuasion = state.Stats.Persuasion.Value,
                Leadership = state.Stats.Leadership.Value,
                Command = state.Stats.Command.Value,
                Trade = state.Stats.Trade.Value,
                Administration = state.Stats.Administration.Value,
                Courage = state.Stats.Courage.Value,
                Experience = state.Stats.Experience.Value,
                BaseLoyalty = state.BaseLoyalty.Value,
                CurrentLoyalty = state.CurrentLoyalty.Value,
                Satisfaction = state.Satisfaction.Value,
                BaseReputation = state.BaseReputation.Value,
                CurrentStanding = state.CurrentStanding.Value,
                Location = ToLocationSaveData(state.Location),
                Injury = state.Injury == null ? null : new InjurySaveData
                {
                    Severity = (int)state.Injury.Severity,
                    OccurredAt = state.Injury.OccurredAt.Ticks,
                    ExpectedRecoveryAt = state.Injury.ExpectedRecoveryAt?.Ticks,
                },
                Death = state.Death == null ? null : new DeathSaveData
                {
                    Cause = (int)state.Death.Cause,
                    OccurredAt = state.Death.OccurredAt.Ticks,
                    Summary = state.Death.Summary,
                },
                HistoryCapacity = state.History.Capacity,
            };

            foreach (var entry in state.History.Entries)
            {
                data.History.Add(new CharacterHistorySaveData
                {
                    Sequence = entry.Sequence,
                    OccurredAt = entry.OccurredAt.Ticks,
                    Kind = (int)entry.Kind,
                    Summary = entry.Summary,
                });
            }
            return data;
        }

        private static CharacterState ToCharacterState(CharacterSaveData data)
        {
            var id = CharacterId.Create(data.CharacterId);
            CharacterDefinition definition;
            var kind = (CharacterIdentityKind)data.IdentityKind;
            switch (kind)
            {
                case CharacterIdentityKind.Registered: definition = new RegisteredPerson(id, data.DisplayName); break;
                case CharacterIdentityKind.Generated: definition = new GeneratedPerson(id, data.DisplayName); break;
                case CharacterIdentityKind.Named: definition = new NamedCharacter(id, data.DisplayName, (CharacterProvenance)data.Provenance); break;
                default: throw new InvalidOperationException("Unknown character identity kind in save data.");
            }

            var historyEntries = new System.Collections.Generic.List<CharacterHistoryEntry>();
            foreach (var entry in data.History)
            {
                historyEntries.Add(new CharacterHistoryEntry(entry.Sequence, new WorldTimestamp(entry.OccurredAt), (CharacterHistoryEventKind)entry.Kind, entry.Summary));
            }

            return new CharacterState(
                definition,
                new CharacterStats(data.Intelligence, data.Observation, data.Persuasion, data.Leadership, data.Command, data.Trade, data.Administration, data.Courage, data.Experience),
                ToCharacterLocation(data.Location),
                data.BaseLoyalty,
                data.CurrentLoyalty,
                data.Satisfaction,
                data.BaseReputation,
                data.CurrentStanding,
                data.Importance.HasValue ? (CharacterImportance?)data.Importance.Value : null,
                data.Injury == null ? null : new InjuryState((CharacterInjurySeverity)data.Injury.Severity, new WorldTimestamp(data.Injury.OccurredAt), data.Injury.ExpectedRecoveryAt.HasValue ? new WorldTimestamp(data.Injury.ExpectedRecoveryAt.Value) : (WorldTimestamp?)null),
                data.Death == null ? null : new DeathState(new WorldTimestamp(data.Death.OccurredAt), (CharacterDeathCause)data.Death.Cause, data.Death.Summary),
                new CharacterHistory(data.HistoryCapacity, historyEntries));
        }

        private static CharacterLocationSaveData ToLocationSaveData(CharacterLocation location)
        {
            var data = new CharacterLocationSaveData
            {
                Kind = (int)location.Kind,
                TargetId = GetLocationTargetId(location),
                X = location.Position.X,
                Y = location.Position.Y,
            };
            if (location.Captivity != null)
            {
                data.CaptorId = location.Captivity.CaptorId.Value;
                data.CaptivityStatus = (int)location.Captivity.Status;
                data.CaptivitySiteKind = (int)location.Captivity.Site.Kind;
                data.CaptivitySiteTargetId = GetCaptivitySiteTargetId(location.Captivity.Site);
                data.CaptivitySiteX = location.Captivity.Site.Position.X;
                data.CaptivitySiteY = location.Captivity.Site.Position.Y;
            }
            return data;
        }

        private static CharacterLocation ToCharacterLocation(CharacterLocationSaveData data)
        {
            switch ((CharacterLocationKind)data.Kind)
            {
                case CharacterLocationKind.City: return CharacterLocation.InCity(CityId.Create(data.TargetId));
                case CharacterLocationKind.Army: return CharacterLocation.WithArmy(ArmyId.Create(data.TargetId));
                case CharacterLocationKind.Caravan: return CharacterLocation.WithCaravan(CaravanId.Create(data.TargetId));
                case CharacterLocationKind.WorldPosition: return CharacterLocation.At(new WorldPosition(data.X, data.Y));
                case CharacterLocationKind.Travelling: return CharacterLocation.TravellingAt(new WorldPosition(data.X, data.Y));
                case CharacterLocationKind.Captivity:
                    return CharacterLocation.Captive(new CaptivityState(
                        CharacterId.Create(data.CaptorId),
                        ToCaptivitySite(data),
                        (CaptivityStatus)data.CaptivityStatus));
                default: throw new InvalidOperationException("Unknown character location kind in save data.");
            }
        }

        private static CaptivitySite ToCaptivitySite(CharacterLocationSaveData data)
        {
            switch ((CaptivitySiteKind)data.CaptivitySiteKind)
            {
                case CaptivitySiteKind.City: return CaptivitySite.InCity(CityId.Create(data.CaptivitySiteTargetId));
                case CaptivitySiteKind.Army: return CaptivitySite.WithArmy(ArmyId.Create(data.CaptivitySiteTargetId));
                case CaptivitySiteKind.Caravan: return CaptivitySite.WithCaravan(CaravanId.Create(data.CaptivitySiteTargetId));
                case CaptivitySiteKind.WorldPosition: return CaptivitySite.At(new WorldPosition(data.CaptivitySiteX, data.CaptivitySiteY));
                default: throw new InvalidOperationException("Unknown captivity site kind in save data.");
            }
        }

        private static string GetLocationTargetId(CharacterLocation location)
        {
            switch (location.Kind)
            {
                case CharacterLocationKind.City: return location.CityId!.Value.Value;
                case CharacterLocationKind.Army: return location.ArmyId!.Value.Value;
                case CharacterLocationKind.Caravan: return location.CaravanId!.Value.Value;
                default: return string.Empty;
            }
        }

        private static string GetCaptivitySiteTargetId(CaptivitySite site)
        {
            switch (site.Kind)
            {
                case CaptivitySiteKind.City: return site.CityId!.Value.Value;
                case CaptivitySiteKind.Army: return site.ArmyId!.Value.Value;
                case CaptivitySiteKind.Caravan: return site.CaravanId!.Value.Value;
                default: return string.Empty;
            }
        }
    }
}
