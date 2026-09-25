using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FOC.Application.Save;

namespace FOC.Infrastructure.Save
{
    public sealed partial class CampaignSaveTextSerializer : ISaveSerializer
    {
        private const string Header = "FOC_CAMPAIGN_SAVE";
        public const int MaximumPayloadCharacters = 64 * 1024 * 1024;
        public const int MaximumCollectionEntries = 1_000_000;

        public string Serialize(CampaignSaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var builder = new StringBuilder();
            builder.AppendLine(Header);
            Append(builder, "SaveVersion", data.SaveVersion.ToString(CultureInfo.InvariantCulture));
            Append(builder, "CampaignId", Encode(data.CampaignId));
            Append(builder, "GameVersion", Encode(data.GameVersion));
            Append(builder, "ContentDataVersion", Encode(data.ContentDataVersion));
            Append(builder, "WorldSeed", data.WorldSeed.ToString(CultureInfo.InvariantCulture));
            Append(builder, "WorldGenRevision", data.WorldGenRevision.ToString(CultureInfo.InvariantCulture));
            Append(builder, "WorldTime", data.WorldTime.ToString(CultureInfo.InvariantCulture));
            Append(builder, "RngState", data.RngState.ToString(CultureInfo.InvariantCulture));
            Append(builder, "RngDrawCount", data.RngDrawCount.ToString(CultureInfo.InvariantCulture));
            if (data.SaveVersion >= 2)
            {
                Append(builder, "CharacterCount", data.Characters.Count.ToString(CultureInfo.InvariantCulture));
                for (var index = 0; index < data.Characters.Count; index++)
                {
                    Append(builder, $"Character.{index}", EncodeCharacter(data.Characters[index]));
                }

                Append(builder, "CharacterRelationCount", data.CharacterRelations.Count.ToString(CultureInfo.InvariantCulture));
                for (var index = 0; index < data.CharacterRelations.Count; index++)
                {
                    Append(builder, $"CharacterRelation.{index}", EncodeRelation(data.CharacterRelations[index]));
                }
            }
            if (data.SaveVersion >= 3)
            {
                Append(builder, "SocialState", EncodeSocial(data));
            }
            if (data.SaveVersion >= 4)
            {
                Append(builder, "ReligionState", EncodeReligion(data));
            }
            if (data.SaveVersion >= 5)
            {
                Append(builder, "CityState", EncodeCities(data));
            }
            if(data.SaveVersion>=6)Append(builder,"EconomyState",EncodeEconomy(data));
            if(data.SaveVersion>=7)Append(builder,"DiplomacyState",EncodeDiplomacy(data));
            if(data.SaveVersion>=8)Append(builder,"MilitaryState",EncodeMilitary(data));
            if(data.SaveVersion>=9)Append(builder,"SoldierState",EncodeSoldiers(data));
            if(data.SaveVersion>=10)Append(builder,"BattleState",EncodeBattles(data));
            if(data.SaveVersion>=11)Append(builder,"EncounterContractState",EncodeEncounterContracts(data));
            if(data.SaveVersion>=12)Append(builder,"AIState",EncodeAI(data));
            if(data.SaveVersion>=13)Append(builder,"GeographyTravelState",EncodeGeographyTravel(data));
            return builder.ToString();
        }

        public SaveReadResult Deserialize(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return SaveReadResult.Failed("Save content is empty.");
            }
            if (content.Length > MaximumPayloadCharacters)
            {
                return SaveReadResult.Failed("Save payload exceeds the technical safety limit.");
            }

            try
            {
                var normalized = content.Replace("\r\n", "\n");
                var lines = normalized.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0 || !string.Equals(lines[0], Header, StringComparison.Ordinal))
                {
                    return SaveReadResult.Failed("Save header is invalid.");
                }

                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                for (var index = 1; index < lines.Length; index++)
                {
                    var separator = lines[index].IndexOf('=');
                    if (separator <= 0)
                    {
                        return SaveReadResult.Failed($"Malformed save line {index + 1}.");
                    }

                    var key = lines[index].Substring(0, separator);
                    var value = lines[index].Substring(separator + 1);
                    if (values.ContainsKey(key))
                    {
                        return SaveReadResult.Failed($"Duplicate save field '{key}'.");
                    }

                    values.Add(key, value);
                }

                var data = new CampaignSaveData
                {
                    SaveVersion = ParseInt(Require(values, "SaveVersion")),
                    CampaignId = Decode(Require(values, "CampaignId")),
                    GameVersion = Decode(Require(values, "GameVersion")),
                    ContentDataVersion = Decode(Require(values, "ContentDataVersion")),
                    WorldSeed = ParseUlong(Require(values, "WorldSeed")),
                    WorldGenRevision = ParseInt(Require(values, "WorldGenRevision")),
                    WorldTime = ParseLong(Require(values, "WorldTime")),
                    RngState = ParseUlong(Require(values, "RngState")),
                    RngDrawCount = ParseUlong(Require(values, "RngDrawCount")),
                };

                if (data.SaveVersion >= 2)
                {
                    var characterCount = ParseNonNegativeCount(Require(values, "CharacterCount"), "CharacterCount");
                    for (var index = 0; index < characterCount; index++)
                    {
                        data.Characters.Add(DecodeCharacter(Require(values, $"Character.{index}")));
                    }

                    var relationCount = ParseNonNegativeCount(Require(values, "CharacterRelationCount"), "CharacterRelationCount");
                    for (var index = 0; index < relationCount; index++)
                    {
                        data.CharacterRelations.Add(DecodeRelation(Require(values, $"CharacterRelation.{index}")));
                    }
                }

                if (data.SaveVersion >= 3)
                {
                    DecodeSocial(Require(values, "SocialState"), data);
                }
                if (data.SaveVersion >= 4)
                {
                    DecodeReligion(Require(values, "ReligionState"), data);
                }
                if (data.SaveVersion >= 5)
                {
                    DecodeCities(Require(values, "CityState"), data);
                }
                if(data.SaveVersion>=6)DecodeEconomy(Require(values,"EconomyState"),data);
                if(data.SaveVersion>=7)DecodeDiplomacy(Require(values,"DiplomacyState"),data);
                if(data.SaveVersion>=8)DecodeMilitary(Require(values,"MilitaryState"),data);
                if(data.SaveVersion>=9)DecodeSoldiersV9(Require(values,"SoldierState"),data);
                if(data.SaveVersion>=10)DecodeBattles(Require(values,"BattleState"),data);
                if(data.SaveVersion>=11)DecodeEncounterContracts(Require(values,"EncounterContractState"),data);
                if(data.SaveVersion>=12)DecodeAI(Require(values,"AIState"),data);
                if(data.SaveVersion>=13)DecodeGeographyTravel(Require(values,"GeographyTravelState"),data);

                return SaveReadResult.Succeeded(data);
            }
            catch (Exception exception) when (
                exception is FormatException ||
                exception is OverflowException ||
                exception is KeyNotFoundException ||
                exception is IOException)
            {
                return SaveReadResult.Failed(exception.Message);
            }
        }

        private static void Append(StringBuilder builder, string key, string value) =>
            builder.Append(key).Append('=').Append(value).Append('\n');

        private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));

        private static string Decode(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));

        private static string Require(Dictionary<string, string> values, string key)
        {
            if (!values.TryGetValue(key, out var value))
            {
                throw new KeyNotFoundException($"Required save field '{key}' is missing.");
            }

            return value;
        }

        private static int ParseInt(string value) => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);

        private static long ParseLong(string value) => long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);

        private static ulong ParseUlong(string value) => ulong.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);

        private static int ParseNonNegativeCount(string value, string fieldName)
        {
            var count = ParseInt(value);
            if (count < 0) throw new FormatException($"{fieldName} cannot be negative.");
            if (count > MaximumCollectionEntries) throw new FormatException($"{fieldName} exceeds the technical safety limit.");
            return count;
        }

        private static string EncodeCharacter(CharacterSaveData data)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(data.CharacterId);
                writer.Write(data.IdentityKind);
                writer.Write(data.DisplayName);
                writer.Write(data.Provenance);
                writer.Write(data.Importance.HasValue);
                if (data.Importance.HasValue) writer.Write(data.Importance.Value);
                writer.Write(data.Intelligence);
                writer.Write(data.Observation);
                writer.Write(data.Persuasion);
                writer.Write(data.Leadership);
                writer.Write(data.Command);
                writer.Write(data.Trade);
                writer.Write(data.Administration);
                writer.Write(data.Courage);
                writer.Write(data.Experience);
                writer.Write(data.BaseLoyalty);
                writer.Write(data.CurrentLoyalty);
                writer.Write(data.Satisfaction);
                writer.Write(data.BaseReputation);
                writer.Write(data.CurrentStanding);
                WriteLocation(writer, data.Location);
                writer.Write(data.Injury != null);
                if (data.Injury != null)
                {
                    writer.Write(data.Injury.Severity);
                    writer.Write(data.Injury.OccurredAt);
                    writer.Write(data.Injury.ExpectedRecoveryAt.HasValue);
                    if (data.Injury.ExpectedRecoveryAt.HasValue) writer.Write(data.Injury.ExpectedRecoveryAt.Value);
                }
                writer.Write(data.Death != null);
                if (data.Death != null)
                {
                    writer.Write(data.Death.Cause);
                    writer.Write(data.Death.OccurredAt);
                    writer.Write(data.Death.Summary);
                }
                writer.Write(data.HistoryCapacity);
                writer.Write(data.History.Count);
                foreach (var entry in data.History)
                {
                    writer.Write(entry.Sequence);
                    writer.Write(entry.OccurredAt);
                    writer.Write(entry.Kind);
                    writer.Write(entry.Summary);
                }
                writer.Flush();
                return Convert.ToBase64String(stream.ToArray());
            }
        }

        private static CharacterSaveData DecodeCharacter(string value)
        {
            using (var stream = new MemoryStream(Convert.FromBase64String(value)))
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                var data = new CharacterSaveData
                {
                    CharacterId = reader.ReadString(),
                    IdentityKind = reader.ReadInt32(),
                    DisplayName = reader.ReadString(),
                    Provenance = reader.ReadInt32(),
                };
                if (reader.ReadBoolean()) data.Importance = reader.ReadInt32();
                data.Intelligence = reader.ReadInt32();
                data.Observation = reader.ReadInt32();
                data.Persuasion = reader.ReadInt32();
                data.Leadership = reader.ReadInt32();
                data.Command = reader.ReadInt32();
                data.Trade = reader.ReadInt32();
                data.Administration = reader.ReadInt32();
                data.Courage = reader.ReadInt32();
                data.Experience = reader.ReadInt32();
                data.BaseLoyalty = reader.ReadInt32();
                data.CurrentLoyalty = reader.ReadInt32();
                data.Satisfaction = reader.ReadInt32();
                data.BaseReputation = reader.ReadInt32();
                data.CurrentStanding = reader.ReadInt32();
                data.Location = ReadLocation(reader);
                if (reader.ReadBoolean())
                {
                    var injury = new InjurySaveData { Severity = reader.ReadInt32(), OccurredAt = reader.ReadInt64() };
                    if (reader.ReadBoolean()) injury.ExpectedRecoveryAt = reader.ReadInt64();
                    data.Injury = injury;
                }
                if (reader.ReadBoolean())
                {
                    data.Death = new DeathSaveData { Cause = reader.ReadInt32(), OccurredAt = reader.ReadInt64(), Summary = reader.ReadString() };
                }
                data.HistoryCapacity = reader.ReadInt32();
                var historyCount = reader.ReadInt32();
                if (historyCount < 0) throw new FormatException("Character history count cannot be negative.");
                for (var index = 0; index < historyCount; index++)
                {
                    data.History.Add(new CharacterHistorySaveData
                    {
                        Sequence = reader.ReadInt64(),
                        OccurredAt = reader.ReadInt64(),
                        Kind = reader.ReadInt32(),
                        Summary = reader.ReadString(),
                    });
                }
                RequireFullyConsumed(stream);
                return data;
            }
        }

        private static string EncodeRelation(CharacterRelationSaveData data)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(data.FirstCharacterId);
                writer.Write(data.SecondCharacterId);
                writer.Write(data.Value);
                writer.Flush();
                return Convert.ToBase64String(stream.ToArray());
            }
        }

        private static CharacterRelationSaveData DecodeRelation(string value)
        {
            using (var stream = new MemoryStream(Convert.FromBase64String(value)))
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                var data = new CharacterRelationSaveData
                {
                    FirstCharacterId = reader.ReadString(),
                    SecondCharacterId = reader.ReadString(),
                    Value = reader.ReadInt32(),
                };
                RequireFullyConsumed(stream);
                return data;
            }
        }

        private static void WriteLocation(BinaryWriter writer, CharacterLocationSaveData data)
        {
            writer.Write(data.Kind);
            writer.Write(data.TargetId);
            writer.Write(data.X);
            writer.Write(data.Y);
            writer.Write(data.CaptorId);
            writer.Write(data.CaptivityStatus);
            writer.Write(data.CaptivitySiteKind);
            writer.Write(data.CaptivitySiteTargetId);
            writer.Write(data.CaptivitySiteX);
            writer.Write(data.CaptivitySiteY);
        }

        private static CharacterLocationSaveData ReadLocation(BinaryReader reader) => new CharacterLocationSaveData
        {
            Kind = reader.ReadInt32(),
            TargetId = reader.ReadString(),
            X = reader.ReadInt64(),
            Y = reader.ReadInt64(),
            CaptorId = reader.ReadString(),
            CaptivityStatus = reader.ReadInt32(),
            CaptivitySiteKind = reader.ReadInt32(),
            CaptivitySiteTargetId = reader.ReadString(),
            CaptivitySiteX = reader.ReadInt64(),
            CaptivitySiteY = reader.ReadInt64(),
        };

        private static void RequireFullyConsumed(MemoryStream stream)
        {
            if (stream.Position != stream.Length) throw new FormatException("Encoded character save payload contains trailing data.");
        }

        private static string EncodeSocial(CampaignSaveData data)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(data.Organizations.Count);
                foreach (var organization in data.Organizations)
                {
                    writer.Write(organization.OrganizationId); writer.Write(organization.Name);
                    writer.Write(organization.Memberships.Count);
                    foreach (var member in organization.Memberships) { writer.Write(member.CharacterId); writer.Write(member.Branch); writer.Write(member.MembershipType); writer.Write(member.StartedAt); writer.Write(member.IsActive); }
                    writer.Write(organization.Assignments.Count);
                    foreach (var assignment in organization.Assignments) { writer.Write(assignment.AssignmentId); writer.Write(assignment.CharacterId); writer.Write(assignment.Branch); writer.Write(assignment.RoleCode); writer.Write(assignment.Authority); writer.Write(assignment.TargetKind); writer.Write(assignment.TargetId); writer.Write(assignment.TargetX); writer.Write(assignment.TargetY); writer.Write(assignment.Presence); writer.Write(assignment.StartedAt); writer.Write(assignment.Status); }
                }
                writer.Write(data.Houses.Count);
                foreach (var house in data.Houses)
                {
                    writer.Write(house.HouseId); writer.Write(house.Name); writer.Write(house.HeadCharacterId); writer.Write(house.SuccessionPending); writer.Write(house.Prestige); writer.Write(house.Wealth); writer.Write(house.Lifecycle);
                    writer.Write(house.Members.Count); foreach (var member in house.Members) { writer.Write(member.CharacterId); writer.Write(member.JoinedAt); writer.Write(member.IsActive); }
                    writer.Write(house.Marriages.Count); foreach (var link in house.Marriages) { writer.Write(link.FirstCharacterId); writer.Write(link.SecondCharacterId); writer.Write(link.StartedAt); writer.Write(link.IsActive); }
                    writer.Write(house.FamilyLinks.Count); foreach (var link in house.FamilyLinks) { writer.Write(link.FirstCharacterId); writer.Write(link.SecondCharacterId); writer.Write(link.Kind); }
                    writer.Write(house.Properties.Count); foreach (var property in house.Properties) { writer.Write(property.AssetId); writer.Write(property.Kind); }
                    writer.Write(house.Inheritances.Count); foreach (var inheritance in house.Inheritances) { writer.Write(inheritance.AssetId); writer.Write(inheritance.Kind); writer.Write(inheritance.HeirCharacterId); writer.Write(inheritance.Status); }
                }
                writer.Write(data.Cliques.Count);
                foreach (var clique in data.Cliques)
                {
                    writer.Write(clique.CliqueId); writer.Write(clique.Name); writer.Write(clique.Type); writer.Write(clique.Lifecycle); writer.Write(clique.Attitude); writer.Write(clique.ParentCliqueId); writer.Write(clique.LeaderCharacterId);
                    writer.Write(clique.Memberships.Count); foreach (var member in clique.Memberships) { writer.Write(member.CharacterId); writer.Write(member.RoleCode); writer.Write(member.JoinedAt); writer.Write(member.IsActive); }
                    writer.Write(clique.InfluenceSources.Count); foreach (var source in clique.InfluenceSources) { writer.Write(source.CharacterId); writer.Write(source.Kind); writer.Write(source.Contribution); }
                }
                writer.Flush(); return Convert.ToBase64String(stream.ToArray());
            }
        }

        private static void DecodeSocial(string value, CampaignSaveData data)
        {
            using (var stream = new MemoryStream(Convert.FromBase64String(value)))
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                var organizationCount = ReadCount(reader, "OrganizationCount");
                for (var i = 0; i < organizationCount; i++)
                {
                    var organization = new OrganizationSaveData { OrganizationId = reader.ReadString(), Name = reader.ReadString() };
                    var memberCount = ReadCount(reader, "OrganizationMembershipCount");
                    for (var j = 0; j < memberCount; j++) organization.Memberships.Add(new OrganizationMembershipSaveData { CharacterId = reader.ReadString(), Branch = reader.ReadInt32(), MembershipType = reader.ReadInt32(), StartedAt = reader.ReadInt64(), IsActive = reader.ReadBoolean() });
                    var assignmentCount = ReadCount(reader, "AssignmentCount");
                    for (var j = 0; j < assignmentCount; j++) organization.Assignments.Add(new AssignmentSaveData { AssignmentId = reader.ReadString(), CharacterId = reader.ReadString(), Branch = reader.ReadInt32(), RoleCode = reader.ReadString(), Authority = reader.ReadInt32(), TargetKind = reader.ReadInt32(), TargetId = reader.ReadString(), TargetX = reader.ReadInt64(), TargetY = reader.ReadInt64(), Presence = reader.ReadInt32(), StartedAt = reader.ReadInt64(), Status = reader.ReadInt32() });
                    data.Organizations.Add(organization);
                }
                var houseCount = ReadCount(reader, "HouseCount");
                for (var i = 0; i < houseCount; i++)
                {
                    var house = new HouseSaveData { HouseId = reader.ReadString(), Name = reader.ReadString(), HeadCharacterId = reader.ReadString(), SuccessionPending = reader.ReadBoolean(), Prestige = reader.ReadInt32(), Wealth = reader.ReadInt64(), Lifecycle = reader.ReadInt32() };
                    var memberCount = ReadCount(reader, "HouseMemberCount"); for (var j = 0; j < memberCount; j++) house.Members.Add(new HouseMemberSaveData { CharacterId = reader.ReadString(), JoinedAt = reader.ReadInt64(), IsActive = reader.ReadBoolean() });
                    var marriageCount = ReadCount(reader, "MarriageCount"); for (var j = 0; j < marriageCount; j++) house.Marriages.Add(new MarriageSaveData { FirstCharacterId = reader.ReadString(), SecondCharacterId = reader.ReadString(), StartedAt = reader.ReadInt64(), IsActive = reader.ReadBoolean() });
                    var familyCount = ReadCount(reader, "FamilyLinkCount"); for (var j = 0; j < familyCount; j++) house.FamilyLinks.Add(new FamilyLinkSaveData { FirstCharacterId = reader.ReadString(), SecondCharacterId = reader.ReadString(), Kind = reader.ReadInt32() });
                    var propertyCount = ReadCount(reader, "HousePropertyCount"); for (var j = 0; j < propertyCount; j++) house.Properties.Add(new HousePropertySaveData { AssetId = reader.ReadString(), Kind = reader.ReadInt32() });
                    var inheritanceCount = ReadCount(reader, "InheritanceCount"); for (var j = 0; j < inheritanceCount; j++) house.Inheritances.Add(new InheritanceSaveData { AssetId = reader.ReadString(), Kind = reader.ReadInt32(), HeirCharacterId = reader.ReadString(), Status = reader.ReadInt32() });
                    data.Houses.Add(house);
                }
                var cliqueCount = ReadCount(reader, "CliqueCount");
                for (var i = 0; i < cliqueCount; i++)
                {
                    var clique = new CliqueSaveData { CliqueId = reader.ReadString(), Name = reader.ReadString(), Type = reader.ReadInt32(), Lifecycle = reader.ReadInt32(), Attitude = reader.ReadInt32(), ParentCliqueId = reader.ReadString(), LeaderCharacterId = reader.ReadString() };
                    var memberCount = ReadCount(reader, "CliqueMembershipCount"); for (var j = 0; j < memberCount; j++) clique.Memberships.Add(new CliqueMembershipSaveData { CharacterId = reader.ReadString(), RoleCode = reader.ReadString(), JoinedAt = reader.ReadInt64(), IsActive = reader.ReadBoolean() });
                    var sourceCount = ReadCount(reader, "CliqueInfluenceSourceCount"); for (var j = 0; j < sourceCount; j++) clique.InfluenceSources.Add(new CliqueInfluenceSourceSaveData { CharacterId = reader.ReadString(), Kind = reader.ReadInt32(), Contribution = reader.ReadInt32() });
                    data.Cliques.Add(clique);
                }
                RequireFullyConsumed(stream);
            }
        }

        private static int ReadCount(BinaryReader reader, string fieldName)
        {
            var count = reader.ReadInt32(); if (count < 0) throw new FormatException(fieldName + " cannot be negative."); return count;
        }

        private static string EncodeReligion(CampaignSaveData data)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            {
                writer.Write(data.Religions.Count);foreach(var x in data.Religions){writer.Write(x.ReligionId);writer.Write(x.Name);writer.Write(x.Status);}
                writer.Write(data.Sects.Count);foreach(var x in data.Sects){writer.Write(x.SectId);writer.Write(x.ParentReligionId);writer.Write(x.Name);writer.Write(x.Status);}
                writer.Write(data.CharacterReligions.Count);foreach(var x in data.CharacterReligions){writer.Write(x.CharacterId);writer.Write(x.ReligionId);writer.Write(x.SectId);}
                writer.Write(data.ReligionProfiles.Count);foreach(var x in data.ReligionProfiles){writer.Write(x.TargetKind);writer.Write(x.TargetId);writer.Write(x.Entries.Count);foreach(var e in x.Entries){writer.Write(e.ReligionId);writer.Write(e.SectId);writer.Write(e.RelativePresence);}}
                writer.Write(data.ReligionPolicies.Count);foreach(var x in data.ReligionPolicies){writer.Write(x.TargetKind);writer.Write(x.TargetId);writer.Write(x.Rules.Count);foreach(var e in x.Rules){writer.Write(e.ReligionId);writer.Write(e.SectId);writer.Write(e.Recognition);writer.Write(e.Treatment);writer.Write(e.Enforcement);}}
                writer.Write(data.ReligiousCliqueAssociations.Count);foreach(var x in data.ReligiousCliqueAssociations){writer.Write(x.CliqueId);writer.Write(x.ReligionId);writer.Write(x.SectId);}writer.Flush();return Convert.ToBase64String(stream.ToArray());
            }
        }
        private static void DecodeReligion(string value,CampaignSaveData data)
        {
            using(var stream=new MemoryStream(Convert.FromBase64String(value)))using(var reader=new BinaryReader(stream,Encoding.UTF8,true))
            {
                var count=ReadCount(reader,"ReligionCount");for(var i=0;i<count;i++)data.Religions.Add(new ReligionDefinitionSaveData{ReligionId=reader.ReadString(),Name=reader.ReadString(),Status=reader.ReadInt32()});
                count=ReadCount(reader,"SectCount");for(var i=0;i<count;i++)data.Sects.Add(new SectDefinitionSaveData{SectId=reader.ReadString(),ParentReligionId=reader.ReadString(),Name=reader.ReadString(),Status=reader.ReadInt32()});
                count=ReadCount(reader,"CharacterReligionCount");for(var i=0;i<count;i++)data.CharacterReligions.Add(new CharacterReligionSaveData{CharacterId=reader.ReadString(),ReligionId=reader.ReadString(),SectId=reader.ReadString()});
                count=ReadCount(reader,"ReligionProfileCount");for(var i=0;i<count;i++){var x=new ReligionProfileSaveData{TargetKind=reader.ReadInt32(),TargetId=reader.ReadString()};var inner=ReadCount(reader,"ReligionProfileEntryCount");for(var j=0;j<inner;j++)x.Entries.Add(new ReligionProfileEntrySaveData{ReligionId=reader.ReadString(),SectId=reader.ReadString(),RelativePresence=reader.ReadInt32()});data.ReligionProfiles.Add(x);}
                count=ReadCount(reader,"ReligionPolicyCount");for(var i=0;i<count;i++){var x=new ReligionPolicySaveData{TargetKind=reader.ReadInt32(),TargetId=reader.ReadString()};var inner=ReadCount(reader,"ReligionPolicyRuleCount");for(var j=0;j<inner;j++)x.Rules.Add(new ReligionPolicyRuleSaveData{ReligionId=reader.ReadString(),SectId=reader.ReadString(),Recognition=reader.ReadInt32(),Treatment=reader.ReadInt32(),Enforcement=reader.ReadInt32()});data.ReligionPolicies.Add(x);}
                count=ReadCount(reader,"ReligiousCliqueAssociationCount");for(var i=0;i<count;i++)data.ReligiousCliqueAssociations.Add(new ReligiousCliqueAssociationSaveData{CliqueId=reader.ReadString(),ReligionId=reader.ReadString(),SectId=reader.ReadString()});RequireFullyConsumed(stream);
            }
        }

        private static string EncodeCities(CampaignSaveData data)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            {
                writer.Write(data.Cities.Count);foreach(var city in data.Cities){writer.Write(city.CityId);writer.Write(city.Name);writer.Write(city.PopulationCount);writer.Write(city.Wealth);writer.Write(city.Order);writer.Write(city.Health);writer.Write(city.Security);writer.Write(city.Areas.Count);foreach(var area in city.Areas){writer.Write(area.Type);writer.Write(area.Fullness);writer.Write(area.VisualVariantHook);writer.Write(area.BuildingPool.Count);foreach(var b in area.BuildingPool){writer.Write(b.CityBuildingId);writer.Write(b.Name);writer.Write(b.Kind);writer.Write(b.AreaType);writer.Write(b.Status);writer.Write(b.EffectTags.Count);foreach(var tag in b.EffectTags)writer.Write(tag);}writer.Write(area.ActiveBuildingIds.Count);foreach(var id in area.ActiveBuildingIds)writer.Write(id);writer.Write(area.LockedBuildingIds.Count);foreach(var id in area.LockedBuildingIds)writer.Write(id);}writer.Write(city.Infrastructure.Count);foreach(var x in city.Infrastructure){writer.Write(x.Type);writer.Write(x.Installed);writer.Write(x.Condition);}writer.Write(city.Officials.Count);foreach(var x in city.Officials){writer.Write(x.Role);writer.Write(x.OrganizationId);writer.Write(x.AssignmentId);}}writer.Flush();return Convert.ToBase64String(stream.ToArray());
            }
        }
        private static void DecodeCities(string value,CampaignSaveData data)
        {
            using(var stream=new MemoryStream(Convert.FromBase64String(value)))using(var reader=new BinaryReader(stream,Encoding.UTF8,true))
            {
                var count=ReadCount(reader,"CityCount");for(var i=0;i<count;i++){var city=new CitySaveData{CityId=reader.ReadString(),Name=reader.ReadString(),PopulationCount=reader.ReadInt64(),Wealth=reader.ReadInt32(),Order=reader.ReadInt32(),Health=reader.ReadInt32(),Security=reader.ReadInt32()};var areaCount=ReadCount(reader,"CityAreaCount");for(var j=0;j<areaCount;j++){var area=new CityAreaSaveData{Type=reader.ReadInt32(),Fullness=reader.ReadInt32(),VisualVariantHook=reader.ReadString()};var poolCount=ReadCount(reader,"CityBuildingPoolCount");for(var k=0;k<poolCount;k++){var b=new CityBuildingSaveData{CityBuildingId=reader.ReadString(),Name=reader.ReadString(),Kind=reader.ReadInt32(),AreaType=reader.ReadInt32(),Status=reader.ReadInt32()};var tagCount=ReadCount(reader,"CityBuildingEffectTagCount");for(var n=0;n<tagCount;n++)b.EffectTags.Add(reader.ReadInt32());area.BuildingPool.Add(b);}var activeCount=ReadCount(reader,"CityActiveBuildingCount");for(var k=0;k<activeCount;k++)area.ActiveBuildingIds.Add(reader.ReadString());var lockedCount=ReadCount(reader,"CityLockedBuildingCount");for(var k=0;k<lockedCount;k++)area.LockedBuildingIds.Add(reader.ReadString());city.Areas.Add(area);}var infrastructureCount=ReadCount(reader,"CityInfrastructureCount");for(var j=0;j<infrastructureCount;j++)city.Infrastructure.Add(new CityInfrastructureSaveData{Type=reader.ReadInt32(),Installed=reader.ReadBoolean(),Condition=reader.ReadInt32()});var officialCount=ReadCount(reader,"CityOfficialCount");for(var j=0;j<officialCount;j++)city.Officials.Add(new CityOfficialSaveData{Role=reader.ReadInt32(),OrganizationId=reader.ReadString(),AssignmentId=reader.ReadString()});data.Cities.Add(city);}RequireFullyConsumed(stream);
            }
        }

        private static string EncodeEconomy(CampaignSaveData data)
        {
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream,Encoding.UTF8,true))
            {
                w.Write(data.TradeGoods.Count);foreach(var x in data.TradeGoods){w.Write(x.TradeGoodId);w.Write(x.Name);w.Write(x.Category);w.Write(x.UnitWeight);w.Write(x.IsFood);w.Write(x.IsMilitaryGood);w.Write(x.IsLuxury);w.Write(x.ReferenceUnitValue.HasValue);if(x.ReferenceUnitValue.HasValue)w.Write(x.ReferenceUnitValue.Value);}w.Write(data.ProductionRecipes.Count);foreach(var x in data.ProductionRecipes){w.Write(x.ProductionRecipeId);w.Write(x.Name);w.Write(x.BuildingKind);w.Write(x.Inputs.Count);foreach(var l in x.Inputs){w.Write(l.TradeGoodId);w.Write(l.Quantity);}w.Write(x.Outputs.Count);foreach(var l in x.Outputs){w.Write(l.TradeGoodId);w.Write(l.Quantity);}}w.Write(data.CityMarkets.Count);foreach(var x in data.CityMarkets){w.Write(x.CityId);w.Write(x.CashBalance);w.Write(x.Stocks.Count);foreach(var s in x.Stocks){w.Write(s.TradeGoodId);w.Write(s.Quantity);}w.Write(x.DemandSources.Count);foreach(var d in x.DemandSources){w.Write(d.SourceId);w.Write(d.Kind);w.Write(d.TradeGoodId);w.Write(d.Quantity);}}w.Write(data.Caravans.Count);foreach(var x in data.Caravans){w.Write(x.CaravanId);w.Write(x.OwnerKind);w.Write(x.OwnerId);w.Write(x.ManagerCharacterId);w.Write(x.RepresentativeCharacterId);w.Write(x.RepresentativeOrganizationId);w.Write(x.RepresentativeAssignmentId);w.Write(x.OriginCityId);w.Write(x.DestinationCityId);w.Write(x.RouteId);w.Write(x.WeightCapacity);w.Write(x.CashBalance);w.Write(x.Lifecycle);w.Write(x.LocationStage);w.Write(x.PurchaseCost);w.Write(x.SaleRevenue);w.Write(x.OperatingCost);w.Write(x.Tariffs);w.Write(x.Losses);w.Write(x.Cargo.Count);foreach(var c in x.Cargo){w.Write(c.TradeGoodId);w.Write(c.Quantity);}w.Write(x.RiskInputs.Count);foreach(var risk in x.RiskInputs){w.Write(risk.SourceId);w.Write(risk.Source);}}w.Flush();return Convert.ToBase64String(stream.ToArray());
            }
        }
        private static void DecodeEconomy(string value,CampaignSaveData data)
        {
            using(var stream=new MemoryStream(Convert.FromBase64String(value)))using(var r=new BinaryReader(stream,Encoding.UTF8,true))
            {
                var count=ReadCount(r,"TradeGoodCount");for(var i=0;i<count;i++){var x=new TradeGoodSaveData{TradeGoodId=r.ReadString(),Name=r.ReadString(),Category=r.ReadInt32(),UnitWeight=r.ReadInt64(),IsFood=r.ReadBoolean(),IsMilitaryGood=r.ReadBoolean(),IsLuxury=r.ReadBoolean()};if(r.ReadBoolean())x.ReferenceUnitValue=r.ReadInt64();data.TradeGoods.Add(x);}count=ReadCount(r,"ProductionRecipeCount");for(var i=0;i<count;i++){var x=new ProductionRecipeSaveData{ProductionRecipeId=r.ReadString(),Name=r.ReadString(),BuildingKind=r.ReadInt32()};var n=ReadCount(r,"RecipeInputCount");for(var j=0;j<n;j++)x.Inputs.Add(new RecipeGoodsLineSaveData{TradeGoodId=r.ReadString(),Quantity=r.ReadInt64()});n=ReadCount(r,"RecipeOutputCount");for(var j=0;j<n;j++)x.Outputs.Add(new RecipeGoodsLineSaveData{TradeGoodId=r.ReadString(),Quantity=r.ReadInt64()});data.ProductionRecipes.Add(x);}count=ReadCount(r,"CityMarketCount");for(var i=0;i<count;i++){var x=new CityMarketSaveData{CityId=r.ReadString(),CashBalance=r.ReadInt64()};var n=ReadCount(r,"MarketStockCount");for(var j=0;j<n;j++)x.Stocks.Add(new TradeGoodStockSaveData{TradeGoodId=r.ReadString(),Quantity=r.ReadInt64()});n=ReadCount(r,"DemandSourceCount");for(var j=0;j<n;j++)x.DemandSources.Add(new DemandSourceSaveData{SourceId=r.ReadString(),Kind=r.ReadInt32(),TradeGoodId=r.ReadString(),Quantity=r.ReadInt64()});data.CityMarkets.Add(x);}count=ReadCount(r,"CaravanCount");for(var i=0;i<count;i++){var x=new CaravanSaveData{CaravanId=r.ReadString(),OwnerKind=r.ReadInt32(),OwnerId=r.ReadString(),ManagerCharacterId=r.ReadString(),RepresentativeCharacterId=r.ReadString(),RepresentativeOrganizationId=r.ReadString(),RepresentativeAssignmentId=r.ReadString(),OriginCityId=r.ReadString(),DestinationCityId=r.ReadString(),RouteId=r.ReadString(),WeightCapacity=r.ReadInt64(),CashBalance=r.ReadInt64(),Lifecycle=r.ReadInt32(),LocationStage=r.ReadInt32(),PurchaseCost=r.ReadInt64(),SaleRevenue=r.ReadInt64(),OperatingCost=r.ReadInt64(),Tariffs=r.ReadInt64(),Losses=r.ReadInt64()};var n=ReadCount(r,"CaravanCargoCount");for(var j=0;j<n;j++)x.Cargo.Add(new TradeGoodStockSaveData{TradeGoodId=r.ReadString(),Quantity=r.ReadInt64()});n=ReadCount(r,"RouteRiskCount");for(var j=0;j<n;j++)x.RiskInputs.Add(new RouteRiskSaveData{SourceId=r.ReadString(),Source=r.ReadInt32()});data.Caravans.Add(x);}RequireFullyConsumed(stream);
            }
        }

        private static string EncodeDiplomacy(CampaignSaveData d)
        {
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Encoding.UTF8,true))
            {
                w.Write(d.DiplomaticActors.Count);foreach(var x in d.DiplomaticActors){w.Write(x.FactionId);w.Write(x.Name);w.Write(x.Lifecycle);}w.Write(d.DiplomaticRelations.Count);foreach(var x in d.DiplomaticRelations){w.Write(x.FirstActorId);w.Write(x.SecondActorId);w.Write(x.Disposition);w.Write(x.UpdatedAt);w.Write(x.Factors.Count);foreach(var f in x.Factors){w.Write(f.SourceId);w.Write(f.Source);w.Write(f.Direction);w.Write(f.OccurredAt);}}w.Write(d.EnvoyMissions.Count);foreach(var x in d.EnvoyMissions){w.Write(x.EnvoyMissionId);w.Write(x.CharacterId);w.Write(x.OrganizationId);w.Write(x.AssignmentId);w.Write(x.SourceActorId);w.Write(x.TargetActorId);w.Write(x.MissionType);w.Write(x.AuthorityScope);w.Write(x.AllowedActions.Count);foreach(var a in x.AllowedActions)w.Write(a);w.Write(x.CreatedAt);WriteNullable(w,x.DepartedAt);WriteNullable(w,x.ArrivedAt);WriteNullable(w,x.CompletedAt);w.Write(x.Phase);}w.Write(d.DiplomaticMessages.Count);foreach(var x in d.DiplomaticMessages){w.Write(x.DiplomaticMessageId);w.Write(x.SenderActorId);w.Write(x.RecipientActorId);w.Write(x.Kind);w.Write(x.CarrierKind);w.Write(x.CarrierCharacterId);w.Write(x.EnvoyMissionId);w.Write(x.ResponseToId);w.Write(x.CreatedAt);WriteNullable(w,x.DispatchedAt);WriteNullable(w,x.DeliveredAt);w.Write(x.Status);}w.Write(d.DiplomaticActions.Count);foreach(var x in d.DiplomaticActions){w.Write(x.DiplomaticActionId);w.Write(x.SourceActorId);w.Write(x.TargetActorId);w.Write(x.Kind);w.Write(x.EnvoyMissionId);w.Write(x.DiplomaticMessageId);w.Write(x.OrderedAt);w.Write(x.Status);w.Write(x.Outcome.HasValue);if(x.Outcome.HasValue)w.Write(x.Outcome.Value);WriteNullable(w,x.ResolvedAt);}w.Write(d.Reports.Count);foreach(var x in d.Reports){w.Write(x.ReportId);w.Write(x.Type);w.Write(x.SourceKind);w.Write(x.SourceId);w.Write(x.SourceCityId);w.Write(x.SourceBuildingId);w.Write(x.RecipientActorId);w.Write(x.Quality);w.Write(x.DetailLevel);w.Write(x.ObservedAt);WriteNullable(w,x.DispatchedAt);WriteNullable(w,x.ArrivedAt);w.Write(x.Status);w.Write(x.Observations.Count);foreach(var o in x.Observations){w.Write(o.SubjectKind);w.Write(o.SubjectId);w.Write(o.Kind);w.Write(o.Precision);WriteNullable(w,o.Lower);WriteNullable(w,o.Upper);w.Write(o.Qualitative);}}w.Write(d.ActorInformation.Count);foreach(var x in d.ActorInformation){w.Write(x.ActorId);w.Write(x.AvailableReportIds.Count);foreach(var id in x.AvailableReportIds)w.Write(id);}w.Write(d.DiplomaticAgreements.Count);foreach(var x in d.DiplomaticAgreements){w.Write(x.AgreementId);w.Write(x.FirstActorId);w.Write(x.SecondActorId);w.Write(x.Kind);w.Write(x.SignedAt);w.Write(x.EffectiveAt);WriteNullable(w,x.ExpiresAt);w.Write(x.Status);w.Write(x.Terms.Count);foreach(var t in x.Terms)w.Write(t);}w.Flush();return Convert.ToBase64String(s.ToArray());
            }
        }
        private static void DecodeDiplomacy(string value,CampaignSaveData d)
        {
            using(var s=new MemoryStream(Convert.FromBase64String(value)))using(var r=new BinaryReader(s,Encoding.UTF8,true))
            {
                var n=ReadCount(r,"DiplomaticActorCount");for(var i=0;i<n;i++)d.DiplomaticActors.Add(new DiplomaticActorSaveData{FactionId=r.ReadString(),Name=r.ReadString(),Lifecycle=r.ReadInt32()});n=ReadCount(r,"DiplomaticRelationCount");for(var i=0;i<n;i++){var x=new DiplomaticRelationSaveData{FirstActorId=r.ReadString(),SecondActorId=r.ReadString(),Disposition=r.ReadInt32(),UpdatedAt=r.ReadInt64()};var m=ReadCount(r,"DiplomaticFactorCount");for(var j=0;j<m;j++)x.Factors.Add(new DiplomaticFactorSaveData{SourceId=r.ReadString(),Source=r.ReadInt32(),Direction=r.ReadInt32(),OccurredAt=r.ReadInt64()});d.DiplomaticRelations.Add(x);}n=ReadCount(r,"EnvoyMissionCount");for(var i=0;i<n;i++){var x=new EnvoyMissionSaveData{EnvoyMissionId=r.ReadString(),CharacterId=r.ReadString(),OrganizationId=r.ReadString(),AssignmentId=r.ReadString(),SourceActorId=r.ReadString(),TargetActorId=r.ReadString(),MissionType=r.ReadInt32(),AuthorityScope=r.ReadInt32()};var m=ReadCount(r,"MandateActionCount");for(var j=0;j<m;j++)x.AllowedActions.Add(r.ReadInt32());x.CreatedAt=r.ReadInt64();x.DepartedAt=ReadNullable(r);x.ArrivedAt=ReadNullable(r);x.CompletedAt=ReadNullable(r);x.Phase=r.ReadInt32();d.EnvoyMissions.Add(x);}n=ReadCount(r,"DiplomaticMessageCount");for(var i=0;i<n;i++)d.DiplomaticMessages.Add(new DiplomaticMessageSaveData{DiplomaticMessageId=r.ReadString(),SenderActorId=r.ReadString(),RecipientActorId=r.ReadString(),Kind=r.ReadInt32(),CarrierKind=r.ReadInt32(),CarrierCharacterId=r.ReadString(),EnvoyMissionId=r.ReadString(),ResponseToId=r.ReadString(),CreatedAt=r.ReadInt64(),DispatchedAt=ReadNullable(r),DeliveredAt=ReadNullable(r),Status=r.ReadInt32()});n=ReadCount(r,"DiplomaticActionCount");for(var i=0;i<n;i++){var x=new DiplomaticActionSaveData{DiplomaticActionId=r.ReadString(),SourceActorId=r.ReadString(),TargetActorId=r.ReadString(),Kind=r.ReadInt32(),EnvoyMissionId=r.ReadString(),DiplomaticMessageId=r.ReadString(),OrderedAt=r.ReadInt64(),Status=r.ReadInt32()};if(r.ReadBoolean())x.Outcome=r.ReadInt32();x.ResolvedAt=ReadNullable(r);d.DiplomaticActions.Add(x);}n=ReadCount(r,"ReportCount");for(var i=0;i<n;i++){var x=new ReportSaveData{ReportId=r.ReadString(),Type=r.ReadInt32(),SourceKind=r.ReadInt32(),SourceId=r.ReadString(),SourceCityId=r.ReadString(),SourceBuildingId=r.ReadString(),RecipientActorId=r.ReadString(),Quality=r.ReadInt32(),DetailLevel=r.ReadInt32(),ObservedAt=r.ReadInt64(),DispatchedAt=ReadNullable(r),ArrivedAt=ReadNullable(r),Status=r.ReadInt32()};var m=ReadCount(r,"ReportObservationCount");for(var j=0;j<m;j++)x.Observations.Add(new ReportObservationSaveData{SubjectKind=r.ReadInt32(),SubjectId=r.ReadString(),Kind=r.ReadInt32(),Precision=r.ReadInt32(),Lower=ReadNullable(r),Upper=ReadNullable(r),Qualitative=r.ReadInt32()});d.Reports.Add(x);}n=ReadCount(r,"ActorInformationCount");for(var i=0;i<n;i++){var x=new ActorInformationSaveData{ActorId=r.ReadString()};var m=ReadCount(r,"AvailableReportCount");for(var j=0;j<m;j++)x.AvailableReportIds.Add(r.ReadString());d.ActorInformation.Add(x);}n=ReadCount(r,"DiplomaticAgreementCount");for(var i=0;i<n;i++){var x=new DiplomaticAgreementSaveData{AgreementId=r.ReadString(),FirstActorId=r.ReadString(),SecondActorId=r.ReadString(),Kind=r.ReadInt32(),SignedAt=r.ReadInt64(),EffectiveAt=r.ReadInt64(),ExpiresAt=ReadNullable(r),Status=r.ReadInt32()};var m=ReadCount(r,"AgreementTermCount");for(var j=0;j<m;j++)x.Terms.Add(r.ReadInt32());d.DiplomaticAgreements.Add(x);}RequireFullyConsumed(s);
            }
        }
        private static string EncodeMilitary(CampaignSaveData d)
        {
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Encoding.UTF8,true))
            {
                w.Write(d.RecruitmentSources.Count);foreach(var x in d.RecruitmentSources){w.Write(x.RecruitmentSourceId);w.Write(x.Type);w.Write(x.AvailableHeadcount);w.Write(x.AuthorityCharacterId);w.Write(x.AuthorityOrganizationId);w.Write(x.AuthorityAssignmentId);w.Write(x.CityId);w.Write(x.InstitutionId);w.Write(x.ObligationOrContractId);w.Write(x.IsActive);}
                w.Write(d.Armies.Count);foreach(var x in d.Armies){w.Write(x.ArmyId);w.Write(x.Name);w.Write(x.OwnerKind);w.Write(x.OwnerId);w.Write(x.ControllerKind);w.Write(x.ControllerId);w.Write(x.LocationKind);w.Write(x.CityId);w.Write(x.LocationX);w.Write(x.LocationY);w.Write(x.CommanderCharacterId);w.Write(x.CommanderOrganizationId);w.Write(x.CommanderAssignmentId);w.Write(x.Lifecycle);w.Write(x.Morale);w.Write(x.Fatigue);w.Write(x.Discipline);w.Write(x.Units.Count);foreach(var u in x.Units){w.Write(u.UnitGroupId);w.Write(u.RecruitmentSourceId);w.Write(u.TroopDefinitionId);w.Write(u.Headcount);w.Write(u.CommanderCharacterId);w.Write(u.Morale);w.Write(u.Fatigue);w.Write(u.Discipline);}w.Write(x.CommandRelationships.Count);foreach(var c in x.CommandRelationships){w.Write(c.ParentKind);w.Write(c.ParentId);w.Write(c.ChildKind);w.Write(c.ChildId);}w.Write(x.Supply.Count);foreach(var q in x.Supply){w.Write(q.TradeGoodId);w.Write(q.Quantity);}w.Write(x.SupplyRequirements.Count);foreach(var q in x.SupplyRequirements){w.Write(q.TradeGoodId);w.Write(q.Purpose);w.Write(q.RequiredQuantity);}w.Write(x.PayrollObligations.Count);foreach(var o in x.PayrollObligations){w.Write(o.PayrollObligationId);w.Write(o.AmountOwed);w.Write(o.AmountPaid);w.Write(o.DueAt);w.Write(o.FundingSourceKind);w.Write(o.FundingSourceId);}w.Write(x.PayrollPayments.Count);foreach(var p in x.PayrollPayments){w.Write(p.PayrollPaymentId);w.Write(p.PayrollObligationId);w.Write(p.Amount);w.Write(p.PaidAt);w.Write(p.FundingSourceKind);w.Write(p.FundingSourceId);}}
                w.Write(d.RecruitmentRecords.Count);foreach(var x in d.RecruitmentRecords){w.Write(x.RecruitmentRecordId);w.Write(x.RecruitmentSourceId);w.Write(x.ArmyId);w.Write(x.UnitGroupId);w.Write(x.Headcount);w.Write(x.OccurredAt);}w.Flush();return Convert.ToBase64String(s.ToArray());
            }
        }
        private static void DecodeMilitary(string value,CampaignSaveData d)
        {
            using(var s=new MemoryStream(Convert.FromBase64String(value)))using(var r=new BinaryReader(s,Encoding.UTF8,true))
            {
                var n=ReadCount(r,"RecruitmentSourceCount");for(var i=0;i<n;i++)d.RecruitmentSources.Add(new RecruitmentSourceSaveData{RecruitmentSourceId=r.ReadString(),Type=r.ReadInt32(),AvailableHeadcount=r.ReadInt64(),AuthorityCharacterId=r.ReadString(),AuthorityOrganizationId=r.ReadString(),AuthorityAssignmentId=r.ReadString(),CityId=r.ReadString(),InstitutionId=r.ReadString(),ObligationOrContractId=r.ReadString(),IsActive=r.ReadBoolean()});
                n=ReadCount(r,"ArmyCount");for(var i=0;i<n;i++){var x=new ArmySaveData{ArmyId=r.ReadString(),Name=r.ReadString(),OwnerKind=r.ReadInt32(),OwnerId=r.ReadString(),ControllerKind=r.ReadInt32(),ControllerId=r.ReadString(),LocationKind=r.ReadInt32(),CityId=r.ReadString(),LocationX=r.ReadInt64(),LocationY=r.ReadInt64(),CommanderCharacterId=r.ReadString(),CommanderOrganizationId=r.ReadString(),CommanderAssignmentId=r.ReadString(),Lifecycle=r.ReadInt32(),Morale=r.ReadInt32(),Fatigue=r.ReadInt32(),Discipline=r.ReadInt32()};var m=ReadCount(r,"UnitGroupCount");for(var j=0;j<m;j++)x.Units.Add(new UnitGroupSaveData{UnitGroupId=r.ReadString(),RecruitmentSourceId=r.ReadString(),TroopDefinitionId=r.ReadString(),Headcount=r.ReadInt64(),CommanderCharacterId=r.ReadString(),Morale=r.ReadInt32(),Fatigue=r.ReadInt32(),Discipline=r.ReadInt32()});m=ReadCount(r,"CommandRelationshipCount");for(var j=0;j<m;j++)x.CommandRelationships.Add(new CommandRelationshipSaveData{ParentKind=r.ReadInt32(),ParentId=r.ReadString(),ChildKind=r.ReadInt32(),ChildId=r.ReadString()});m=ReadCount(r,"ArmySupplyCount");for(var j=0;j<m;j++)x.Supply.Add(new ArmySupplySaveData{TradeGoodId=r.ReadString(),Quantity=r.ReadInt64()});m=ReadCount(r,"SupplyRequirementCount");for(var j=0;j<m;j++)x.SupplyRequirements.Add(new SupplyRequirementSaveData{TradeGoodId=r.ReadString(),Purpose=r.ReadInt32(),RequiredQuantity=r.ReadInt64()});m=ReadCount(r,"PayrollObligationCount");for(var j=0;j<m;j++)x.PayrollObligations.Add(new PayrollObligationSaveData{PayrollObligationId=r.ReadString(),AmountOwed=r.ReadInt64(),AmountPaid=r.ReadInt64(),DueAt=r.ReadInt64(),FundingSourceKind=r.ReadInt32(),FundingSourceId=r.ReadString()});m=ReadCount(r,"PayrollPaymentCount");for(var j=0;j<m;j++)x.PayrollPayments.Add(new PayrollPaymentSaveData{PayrollPaymentId=r.ReadString(),PayrollObligationId=r.ReadString(),Amount=r.ReadInt64(),PaidAt=r.ReadInt64(),FundingSourceKind=r.ReadInt32(),FundingSourceId=r.ReadString()});d.Armies.Add(x);}
                n=ReadCount(r,"RecruitmentRecordCount");for(var i=0;i<n;i++)d.RecruitmentRecords.Add(new RecruitmentRecordSaveData{RecruitmentRecordId=r.ReadString(),RecruitmentSourceId=r.ReadString(),ArmyId=r.ReadString(),UnitGroupId=r.ReadString(),Headcount=r.ReadInt64(),OccurredAt=r.ReadInt64()});RequireFullyConsumed(s);
            }
        }
        private static string EncodeSoldiers(CampaignSaveData d)
        {
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Encoding.UTF8,true))
            {
                w.Write(d.TroopDefinitions.Count);foreach(var x in d.TroopDefinitions){w.Write(x.TroopDefinitionId);w.Write(x.Name);w.Write(x.UnitClassId);w.Write(x.DefaultCombatRoleId);w.Write(x.MountContext);w.Write(x.VisualProfileId);w.Write(x.AllowedWeaponFamilies.Count);foreach(var f in x.AllowedWeaponFamilies)w.Write(f);w.Write(x.AllowsArmor);w.Write(x.AllowsShield);}
                w.Write(d.WeaponDefinitions.Count);foreach(var x in d.WeaponDefinitions){w.Write(x.WeaponDefinitionId);w.Write(x.Name);w.Write(x.Family);w.Write(x.AllowedSlots.Count);foreach(var v in x.AllowedSlots)w.Write(v);w.Write(x.AttackOptions.Count);foreach(var a in x.AttackOptions){w.Write(a.Mode);w.Write(a.DamageType);}w.Write(x.MountContext);w.Write(x.EconomicGoodId);w.Write(x.VisualProfileId);w.Write(x.IsRanged);w.Write(x.AmmoFamilyId);}w.Write(d.ArmorDefinitions.Count);foreach(var x in d.ArmorDefinitions){w.Write(x.ArmorDefinitionId);w.Write(x.Name);w.Write(x.Slot);w.Write(x.EconomicGoodId);w.Write(x.VisualProfileId);w.Write(x.QualityCode);}w.Write(d.ShieldDefinitions.Count);foreach(var x in d.ShieldDefinitions){w.Write(x.ShieldDefinitionId);w.Write(x.Name);w.Write(x.EconomicGoodId);w.Write(x.VisualProfileId);w.Write(x.Classification);}w.Write(d.MountDefinitions.Count);foreach(var x in d.MountDefinitions){w.Write(x.MountDefinitionId);w.Write(x.Name);w.Write(x.EconomicGoodId);w.Write(x.VisualProfileId);w.Write(x.MobilityClass);}w.Write(d.AuxiliaryEquipmentDefinitions.Count);foreach(var x in d.AuxiliaryEquipmentDefinitions){w.Write(x.AuxiliaryEquipmentDefinitionId);w.Write(x.Name);w.Write(x.Kind);w.Write(x.EconomicGoodId);w.Write(x.VisualProfileId);w.Write(x.AmmoFamilyId);}w.Write(d.EquipmentInstances.Count);foreach(var x in d.EquipmentInstances){w.Write(x.EquipmentInstanceId);w.Write(x.DefinitionKind);w.Write(x.DefinitionId);w.Write(x.OwnerKind);w.Write(x.OwnerId);w.Write(x.AcquisitionKind);w.Write(x.AcquisitionSourceId);w.Write(x.EconomicGoodId);w.Write(x.AcquiredAt);w.Write(x.QualityCode);}w.Write(d.Soldiers.Count);foreach(var x in d.Soldiers){w.Write(x.SoldierId);w.Write(x.UnitGroupId);w.Write(x.TroopDefinitionId);w.Write(x.RecruitmentSourceId);w.Write(x.RecruitmentRecordId);w.Write(x.CombatRoleId);w.Write(x.Experience);w.Write(x.Training);w.Write(x.Lifecycle);w.Write(x.Weapons.Count);foreach(var q in x.Weapons){w.Write(q.Slot);w.Write(q.EquipmentInstanceId);}w.Write(x.Armor.Count);foreach(var q in x.Armor){w.Write(q.Slot);w.Write(q.EquipmentInstanceId);}w.Write(x.ShieldEquipmentId);w.Write(x.MountEquipmentId);}w.Flush();return Convert.ToBase64String(s.ToArray());
            }
        }
        private static void DecodeSoldiers(string value,CampaignSaveData d)
        {
            using(var s=new MemoryStream(Convert.FromBase64String(value)))using(var r=new BinaryReader(s,Encoding.UTF8,true))
            {
                var n=ReadCount(r,"TroopDefinitionCount");for(var i=0;i<n;i++)d.TroopDefinitions.Add(new TroopDefinitionSaveData{TroopDefinitionId=r.ReadString(),Name=r.ReadString(),UnitClassId=r.ReadString(),DefaultCombatRoleId=r.ReadString(),MountContext=r.ReadInt32(),VisualProfileId=r.ReadString()});n=ReadCount(r,"WeaponDefinitionCount");for(var i=0;i<n;i++){var x=new WeaponDefinitionSaveData{WeaponDefinitionId=r.ReadString(),Name=r.ReadString(),Family=r.ReadInt32()};var m=ReadCount(r,"WeaponSlotCount");for(var j=0;j<m;j++)x.AllowedSlots.Add(r.ReadInt32());m=ReadCount(r,"WeaponAttackCount");for(var j=0;j<m;j++)x.AttackOptions.Add(new WeaponAttackOptionSaveData{Mode=r.ReadInt32(),DamageType=r.ReadInt32()});x.MountContext=r.ReadInt32();x.EconomicGoodId=r.ReadString();x.VisualProfileId=r.ReadString();x.IsRanged=r.ReadBoolean();x.AmmoFamilyId=r.ReadString();d.WeaponDefinitions.Add(x);}n=ReadCount(r,"ArmorDefinitionCount");for(var i=0;i<n;i++)d.ArmorDefinitions.Add(new ArmorDefinitionSaveData{ArmorDefinitionId=r.ReadString(),Name=r.ReadString(),Slot=r.ReadInt32(),EconomicGoodId=r.ReadString(),VisualProfileId=r.ReadString(),QualityCode=r.ReadString()});n=ReadCount(r,"ShieldDefinitionCount");for(var i=0;i<n;i++)d.ShieldDefinitions.Add(new ShieldDefinitionSaveData{ShieldDefinitionId=r.ReadString(),Name=r.ReadString(),EconomicGoodId=r.ReadString(),VisualProfileId=r.ReadString(),Classification=r.ReadString()});n=ReadCount(r,"MountDefinitionCount");for(var i=0;i<n;i++)d.MountDefinitions.Add(new MountDefinitionSaveData{MountDefinitionId=r.ReadString(),Name=r.ReadString(),EconomicGoodId=r.ReadString(),VisualProfileId=r.ReadString(),MobilityClass=r.ReadString()});n=ReadCount(r,"AuxiliaryEquipmentDefinitionCount");for(var i=0;i<n;i++)d.AuxiliaryEquipmentDefinitions.Add(new AuxiliaryEquipmentDefinitionSaveData{AuxiliaryEquipmentDefinitionId=r.ReadString(),Name=r.ReadString(),Kind=r.ReadInt32(),EconomicGoodId=r.ReadString(),VisualProfileId=r.ReadString(),AmmoFamilyId=r.ReadString()});n=ReadCount(r,"EquipmentInstanceCount");for(var i=0;i<n;i++)d.EquipmentInstances.Add(new EquipmentInstanceSaveData{EquipmentInstanceId=r.ReadString(),DefinitionKind=r.ReadInt32(),DefinitionId=r.ReadString(),OwnerKind=r.ReadInt32(),OwnerId=r.ReadString(),AcquisitionKind=r.ReadInt32(),AcquisitionSourceId=r.ReadString(),EconomicGoodId=r.ReadString(),AcquiredAt=r.ReadInt64(),QualityCode=r.ReadString()});n=ReadCount(r,"SoldierCount");for(var i=0;i<n;i++){var x=new SoldierSaveData{SoldierId=r.ReadString(),UnitGroupId=r.ReadString(),TroopDefinitionId=r.ReadString(),RecruitmentSourceId=r.ReadString(),RecruitmentRecordId=r.ReadString(),CombatRoleId=r.ReadString(),Experience=r.ReadInt32(),Training=r.ReadInt32(),Lifecycle=r.ReadInt32()};var m=ReadCount(r,"SoldierWeaponCount");for(var j=0;j<m;j++)x.Weapons.Add(new WeaponSlotAssignmentSaveData{Slot=r.ReadInt32(),EquipmentInstanceId=r.ReadString()});m=ReadCount(r,"SoldierArmorCount");for(var j=0;j<m;j++)x.Armor.Add(new ArmorSlotAssignmentSaveData{Slot=r.ReadInt32(),EquipmentInstanceId=r.ReadString()});x.ShieldEquipmentId=r.ReadString();x.MountEquipmentId=r.ReadString();d.Soldiers.Add(x);}RequireFullyConsumed(s);
            }
        }
        private static void DecodeSoldiersV9(string value,CampaignSaveData d)
        {
            using(var s=new MemoryStream(Convert.FromBase64String(value)))using(var r=new BinaryReader(s,Encoding.UTF8,true))
            {
                var n=ReadCount(r,"TroopDefinitionCount");for(var i=0;i<n;i++){var x=new TroopDefinitionSaveData{TroopDefinitionId=r.ReadString(),Name=r.ReadString(),UnitClassId=r.ReadString(),DefaultCombatRoleId=r.ReadString(),MountContext=r.ReadInt32(),VisualProfileId=r.ReadString()};var m=ReadCount(r,"AllowedWeaponFamilyCount");for(var j=0;j<m;j++)x.AllowedWeaponFamilies.Add(r.ReadInt32());x.AllowsArmor=r.ReadBoolean();x.AllowsShield=r.ReadBoolean();d.TroopDefinitions.Add(x);}
                n=ReadCount(r,"WeaponDefinitionCount");for(var i=0;i<n;i++){var x=new WeaponDefinitionSaveData{WeaponDefinitionId=r.ReadString(),Name=r.ReadString(),Family=r.ReadInt32()};var m=ReadCount(r,"WeaponSlotCount");for(var j=0;j<m;j++)x.AllowedSlots.Add(r.ReadInt32());m=ReadCount(r,"WeaponAttackCount");for(var j=0;j<m;j++)x.AttackOptions.Add(new WeaponAttackOptionSaveData{Mode=r.ReadInt32(),DamageType=r.ReadInt32()});x.MountContext=r.ReadInt32();x.EconomicGoodId=r.ReadString();x.VisualProfileId=r.ReadString();x.IsRanged=r.ReadBoolean();x.AmmoFamilyId=r.ReadString();d.WeaponDefinitions.Add(x);}n=ReadCount(r,"ArmorDefinitionCount");for(var i=0;i<n;i++)d.ArmorDefinitions.Add(new ArmorDefinitionSaveData{ArmorDefinitionId=r.ReadString(),Name=r.ReadString(),Slot=r.ReadInt32(),EconomicGoodId=r.ReadString(),VisualProfileId=r.ReadString(),QualityCode=r.ReadString()});n=ReadCount(r,"ShieldDefinitionCount");for(var i=0;i<n;i++)d.ShieldDefinitions.Add(new ShieldDefinitionSaveData{ShieldDefinitionId=r.ReadString(),Name=r.ReadString(),EconomicGoodId=r.ReadString(),VisualProfileId=r.ReadString(),Classification=r.ReadString()});n=ReadCount(r,"MountDefinitionCount");for(var i=0;i<n;i++)d.MountDefinitions.Add(new MountDefinitionSaveData{MountDefinitionId=r.ReadString(),Name=r.ReadString(),EconomicGoodId=r.ReadString(),VisualProfileId=r.ReadString(),MobilityClass=r.ReadString()});n=ReadCount(r,"AuxiliaryEquipmentDefinitionCount");for(var i=0;i<n;i++)d.AuxiliaryEquipmentDefinitions.Add(new AuxiliaryEquipmentDefinitionSaveData{AuxiliaryEquipmentDefinitionId=r.ReadString(),Name=r.ReadString(),Kind=r.ReadInt32(),EconomicGoodId=r.ReadString(),VisualProfileId=r.ReadString(),AmmoFamilyId=r.ReadString()});n=ReadCount(r,"EquipmentInstanceCount");for(var i=0;i<n;i++)d.EquipmentInstances.Add(new EquipmentInstanceSaveData{EquipmentInstanceId=r.ReadString(),DefinitionKind=r.ReadInt32(),DefinitionId=r.ReadString(),OwnerKind=r.ReadInt32(),OwnerId=r.ReadString(),AcquisitionKind=r.ReadInt32(),AcquisitionSourceId=r.ReadString(),EconomicGoodId=r.ReadString(),AcquiredAt=r.ReadInt64(),QualityCode=r.ReadString()});n=ReadCount(r,"SoldierCount");for(var i=0;i<n;i++){var x=new SoldierSaveData{SoldierId=r.ReadString(),UnitGroupId=r.ReadString(),TroopDefinitionId=r.ReadString(),RecruitmentSourceId=r.ReadString(),RecruitmentRecordId=r.ReadString(),CombatRoleId=r.ReadString(),Experience=r.ReadInt32(),Training=r.ReadInt32(),Lifecycle=r.ReadInt32()};var m=ReadCount(r,"SoldierWeaponCount");for(var j=0;j<m;j++)x.Weapons.Add(new WeaponSlotAssignmentSaveData{Slot=r.ReadInt32(),EquipmentInstanceId=r.ReadString()});m=ReadCount(r,"SoldierArmorCount");for(var j=0;j<m;j++)x.Armor.Add(new ArmorSlotAssignmentSaveData{Slot=r.ReadInt32(),EquipmentInstanceId=r.ReadString()});x.ShieldEquipmentId=r.ReadString();x.MountEquipmentId=r.ReadString();d.Soldiers.Add(x);}RequireFullyConsumed(s);
            }
        }
        private static string EncodeBattles(CampaignSaveData d)
        {
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Encoding.UTF8,true))
            {
                w.Write(d.Battles.Count);foreach(var b in d.Battles){w.Write(b.BattleId);w.Write(b.StartedAt);w.Write(b.Lifecycle);w.Write(b.Step);w.Write(b.RngState);w.Write(b.RngDrawCount);w.Write(b.IsReconciled);w.Write(b.Sides.Count);foreach(var side in b.Sides){w.Write(side.SideId);w.Write(side.CommanderCharacterId);w.Write(side.Participants.Count);foreach(var p in side.Participants){w.Write(p.ArmyId);w.Write(p.CommanderCharacterId);w.Write(p.Units.Count);foreach(var u in p.Units){w.Write(u.ArmyId);w.Write(u.UnitGroupId);w.Write(u.CommanderCharacterId);w.Write(u.CampaignHeadcount);w.Write(u.Morale);w.Write(u.Fatigue);w.Write(u.Discipline);w.Write(u.Combatants.Count);foreach(var c in u.Combatants){w.Write(c.SoldierId);w.Write(c.UnitGroupId);w.Write(c.TroopDefinitionId);w.Write(c.CombatRoleId);w.Write(c.IsMounted);w.Write(c.EquipmentInstanceIds.Count);foreach(var id in c.EquipmentInstanceIds)w.Write(id);}}w.Write(p.Supply.Count);foreach(var q in p.Supply){w.Write(q.TradeGoodId);w.Write(q.Quantity);}}}w.Write(b.Sectors.Count);foreach(var x in b.Sectors){w.Write(x.SectorId);w.Write(x.Terrain.Count);foreach(var v in x.Terrain)w.Write(v);w.Write(x.EligibleSideIds.Count);foreach(var id in x.EligibleSideIds)w.Write(id);w.Write(x.AdjacentSectorIds.Count);foreach(var id in x.AdjacentSectorIds)w.Write(id);}w.Write(b.Deployments.Count);foreach(var x in b.Deployments){w.Write(x.DeploymentGroupId);w.Write(x.SideId);w.Write(x.ArmyId);w.Write(x.UnitGroupId);w.Write(x.SectorId);w.Write(x.Formation);w.Write(x.IsReserve);}w.Write(b.Orders.Count);foreach(var x in b.Orders){w.Write(x.BattleOrderId);w.Write(x.Sequence);w.Write(x.Kind);w.Write(x.SideId);w.Write(x.IssuerCharacterId);w.Write(x.DeploymentGroupId);w.Write(x.TargetSectorId);w.Write(x.TargetGroupId);w.Write(x.Formation.HasValue);if(x.Formation.HasValue)w.Write(x.Formation.Value);}w.Write(b.Events.Count);foreach(var x in b.Events){w.Write(x.BattleEventId);w.Write(x.Sequence);w.Write(x.TargetSoldierId);w.Write(x.Outcome);}w.Write(b.Ammunition.Count);foreach(var x in b.Ammunition){w.Write(x.SoldierId);w.Write(x.AmmoFamilyId);w.Write(x.Quantity);}w.Write(b.Result!=null);if(b.Result!=null)WriteBattleResult(w,b.Result);}w.Flush();return Convert.ToBase64String(s.ToArray());
            }
        }
        private static void WriteBattleResult(BinaryWriter w,BattleResultSaveData r){w.Write(r.EndReason);w.Write(r.CompletedAt);w.Write(r.ElapsedSteps);w.Write(r.WinningSideId);w.Write(r.ParticipatingSideIds.Count);foreach(var id in r.ParticipatingSideIds)w.Write(id);w.Write(r.Units.Count);foreach(var x in r.Units){w.Write(x.UnitGroupId);w.Write(x.AggregateLosses);w.Write(x.Morale);w.Write(x.Fatigue);w.Write(x.Discipline);}w.Write(r.Soldiers.Count);foreach(var x in r.Soldiers){w.Write(x.SoldierId);w.Write(x.Outcome);}w.Write(r.Characters.Count);foreach(var x in r.Characters){w.Write(x.CharacterId);w.Write(x.Outcome);w.Write(x.InjurySeverity.HasValue);if(x.InjurySeverity.HasValue)w.Write(x.InjurySeverity.Value);w.Write(x.CaptorCharacterId);w.Write(x.CaptorArmyId);}w.Write(r.Ammunition.Count);foreach(var x in r.Ammunition){w.Write(x.SoldierId);w.Write(x.AmmoFamilyId);w.Write(x.RemainingQuantity);}}
        private static void DecodeBattles(string value,CampaignSaveData d)
        {
            using(var s=new MemoryStream(Convert.FromBase64String(value)))using(var r=new BinaryReader(s,Encoding.UTF8,true))
            {
                var count=ReadCount(r,"BattleCount");for(var i=0;i<count;i++){var b=new BattleSaveData{BattleId=r.ReadString(),StartedAt=r.ReadInt64(),Lifecycle=r.ReadInt32(),Step=r.ReadInt64(),RngState=r.ReadUInt64(),RngDrawCount=r.ReadUInt64(),IsReconciled=r.ReadBoolean()};var n=ReadCount(r,"BattleSideCount");for(var j=0;j<n;j++){var side=new BattleSideSaveData{SideId=r.ReadString(),CommanderCharacterId=r.ReadString()};var m=ReadCount(r,"BattleParticipantCount");for(var k=0;k<m;k++){var p=new BattleParticipantSaveData{ArmyId=r.ReadString(),CommanderCharacterId=r.ReadString()};var z=ReadCount(r,"BattleUnitCount");for(var q=0;q<z;q++){var u=new BattleUnitSnapshotSaveData{ArmyId=r.ReadString(),UnitGroupId=r.ReadString(),CommanderCharacterId=r.ReadString(),CampaignHeadcount=r.ReadInt64(),Morale=r.ReadInt32(),Fatigue=r.ReadInt32(),Discipline=r.ReadInt32()};var a=ReadCount(r,"BattleCombatantCount");for(var h=0;h<a;h++){var c=new BattleCombatantSaveData{SoldierId=r.ReadString(),UnitGroupId=r.ReadString(),TroopDefinitionId=r.ReadString(),CombatRoleId=r.ReadString(),IsMounted=r.ReadBoolean()};var e=ReadCount(r,"BattleEquipmentCount");for(var y=0;y<e;y++)c.EquipmentInstanceIds.Add(r.ReadString());u.Combatants.Add(c);}p.Units.Add(u);}z=ReadCount(r,"BattleSupplyCount");for(var q=0;q<z;q++)p.Supply.Add(new ArmySupplySaveData{TradeGoodId=r.ReadString(),Quantity=r.ReadInt64()});side.Participants.Add(p);}b.Sides.Add(side);}n=ReadCount(r,"BattleSectorCount");for(var j=0;j<n;j++){var x=new BattleSectorSaveData{SectorId=r.ReadString()};var m=ReadCount(r,"BattleTerrainCount");for(var k=0;k<m;k++)x.Terrain.Add(r.ReadInt32());m=ReadCount(r,"BattleEligibleSideCount");for(var k=0;k<m;k++)x.EligibleSideIds.Add(r.ReadString());m=ReadCount(r,"BattleAdjacentCount");for(var k=0;k<m;k++)x.AdjacentSectorIds.Add(r.ReadString());b.Sectors.Add(x);}n=ReadCount(r,"BattleDeploymentCount");for(var j=0;j<n;j++)b.Deployments.Add(new BattleDeploymentSaveData{DeploymentGroupId=r.ReadString(),SideId=r.ReadString(),ArmyId=r.ReadString(),UnitGroupId=r.ReadString(),SectorId=r.ReadString(),Formation=r.ReadInt32(),IsReserve=r.ReadBoolean()});n=ReadCount(r,"BattleOrderCount");for(var j=0;j<n;j++){var x=new BattleOrderSaveData{BattleOrderId=r.ReadString(),Sequence=r.ReadInt64(),Kind=r.ReadInt32(),SideId=r.ReadString(),IssuerCharacterId=r.ReadString(),DeploymentGroupId=r.ReadString(),TargetSectorId=r.ReadString(),TargetGroupId=r.ReadString()};if(r.ReadBoolean())x.Formation=r.ReadInt32();b.Orders.Add(x);}n=ReadCount(r,"BattleEventCount");for(var j=0;j<n;j++)b.Events.Add(new BattleEventSaveData{BattleEventId=r.ReadString(),Sequence=r.ReadInt64(),TargetSoldierId=r.ReadString(),Outcome=r.ReadInt32()});n=ReadCount(r,"BattleAmmoCount");for(var j=0;j<n;j++)b.Ammunition.Add(new BattleAmmoSaveData{SoldierId=r.ReadString(),AmmoFamilyId=r.ReadString(),Quantity=r.ReadInt64()});if(r.ReadBoolean())b.Result=ReadBattleResult(r);d.Battles.Add(b);}RequireFullyConsumed(s);
            }
        }
        private static BattleResultSaveData ReadBattleResult(BinaryReader r){var x=new BattleResultSaveData{EndReason=r.ReadInt32(),CompletedAt=r.ReadInt64(),ElapsedSteps=r.ReadInt64(),WinningSideId=r.ReadString()};var n=ReadCount(r,"BattleResultSideCount");for(var i=0;i<n;i++)x.ParticipatingSideIds.Add(r.ReadString());n=ReadCount(r,"BattleUnitOutcomeCount");for(var i=0;i<n;i++)x.Units.Add(new BattleUnitOutcomeSaveData{UnitGroupId=r.ReadString(),AggregateLosses=r.ReadInt64(),Morale=r.ReadInt32(),Fatigue=r.ReadInt32(),Discipline=r.ReadInt32()});n=ReadCount(r,"BattleSoldierOutcomeCount");for(var i=0;i<n;i++)x.Soldiers.Add(new BattleSoldierOutcomeSaveData{SoldierId=r.ReadString(),Outcome=r.ReadInt32()});n=ReadCount(r,"BattleCharacterOutcomeCount");for(var i=0;i<n;i++){var c=new BattleCharacterOutcomeSaveData{CharacterId=r.ReadString(),Outcome=r.ReadInt32()};if(r.ReadBoolean())c.InjurySeverity=r.ReadInt32();c.CaptorCharacterId=r.ReadString();c.CaptorArmyId=r.ReadString();x.Characters.Add(c);}n=ReadCount(r,"BattleAmmoOutcomeCount");for(var i=0;i<n;i++)x.Ammunition.Add(new BattleAmmoOutcomeSaveData{SoldierId=r.ReadString(),AmmoFamilyId=r.ReadString(),RemainingQuantity=r.ReadInt64()});return x;}
        private static string EncodeEncounterContracts(CampaignSaveData d){using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Encoding.UTF8,true)){w.Write(d.Encounters.Count);foreach(var x in d.Encounters){w.Write(x.EncounterId);w.Write(x.DefinitionId);w.Write(x.Family);w.Write(x.Type);w.Write(x.CreatedAt);w.Write(x.SourceKind);w.Write(x.SourceId);w.Write(x.Participants.Count);foreach(var p in x.Participants){w.Write(p.Kind);w.Write(p.Id);}w.Write(x.RngState);w.Write(x.RngDrawCount);w.Write(x.Lifecycle);WriteNullable(w,x.EngagedAt);w.Write(x.SelectedChoiceId);w.Write(x.Resolution!=null);if(x.Resolution!=null){w.Write(x.Resolution.OutcomeId);w.Write(x.Resolution.SelectedChoiceId);w.Write(x.Resolution.PolicyId);w.Write(x.Resolution.ResolvedAt);w.Write(x.Resolution.RngState);w.Write(x.Resolution.RngDrawCount);}w.Write(x.LinkedContractId);w.Write(x.LinkedBattleId);w.Write(x.OutcomeApplied);}w.Write(d.Contracts.Count);foreach(var x in d.Contracts){w.Write(x.ContractId);w.Write(x.DefinitionId);w.Write(x.Category);w.Write(x.IssuerKind);w.Write(x.IssuerId);w.Write(x.Targets.Count);foreach(var t in x.Targets){w.Write(t.SlotId);w.Write(t.Kind);w.Write(t.TargetId);}w.Write(x.Objectives.Count);foreach(var o in x.Objectives){w.Write(o.ObjectiveId);w.Write(o.EvidenceKind);w.Write(o.TargetSlotId);w.Write(o.RequiredEvidenceCount);w.Write(o.Evidence.Count);foreach(var e in o.Evidence){w.Write(e.EvidenceId);w.Write(e.Kind);w.Write(e.TargetKind);w.Write(e.TargetId);w.Write(e.OccurredAt);}}w.Write(x.OfferedAt);w.Write(x.AssigneeCharacterId);WriteNullable(w,x.AcceptedAt);WriteNullable(w,x.Deadline);WriteNullable(w,x.TerminalAt);w.Write(x.LinkedEncounterId);w.Write(x.LinkedBattleId);w.Write(x.Lifecycle);w.Write(x.OutcomeApplied);}w.Flush();return Convert.ToBase64String(s.ToArray());}}
        private static void DecodeEncounterContracts(string value,CampaignSaveData d){using(var s=new MemoryStream(Convert.FromBase64String(value)))using(var r=new BinaryReader(s,Encoding.UTF8,true)){var n=ReadCount(r,"EncounterCount");for(var i=0;i<n;i++){var x=new EncounterSaveData{EncounterId=r.ReadString(),DefinitionId=r.ReadString(),Family=r.ReadInt32(),Type=r.ReadInt32(),CreatedAt=r.ReadInt64(),SourceKind=r.ReadInt32(),SourceId=r.ReadString()};var m=ReadCount(r,"EncounterParticipantCount");for(var j=0;j<m;j++)x.Participants.Add(new EncounterEntityRefSaveData{Kind=r.ReadInt32(),Id=r.ReadString()});x.RngState=r.ReadUInt64();x.RngDrawCount=r.ReadUInt64();x.Lifecycle=r.ReadInt32();x.EngagedAt=ReadNullable(r);x.SelectedChoiceId=r.ReadString();if(r.ReadBoolean())x.Resolution=new EncounterResolutionSaveData{OutcomeId=r.ReadString(),SelectedChoiceId=r.ReadString(),PolicyId=r.ReadString(),ResolvedAt=r.ReadInt64(),RngState=r.ReadUInt64(),RngDrawCount=r.ReadUInt64()};x.LinkedContractId=r.ReadString();x.LinkedBattleId=r.ReadString();x.OutcomeApplied=r.ReadBoolean();d.Encounters.Add(x);}n=ReadCount(r,"ContractCount");for(var i=0;i<n;i++){var x=new ContractSaveData{ContractId=r.ReadString(),DefinitionId=r.ReadString(),Category=r.ReadInt32(),IssuerKind=r.ReadInt32(),IssuerId=r.ReadString()};var m=ReadCount(r,"ContractTargetCount");for(var j=0;j<m;j++)x.Targets.Add(new ContractTargetSaveData{SlotId=r.ReadString(),Kind=r.ReadInt32(),TargetId=r.ReadString()});m=ReadCount(r,"ContractObjectiveCount");for(var j=0;j<m;j++){var o=new ContractObjectiveSaveData{ObjectiveId=r.ReadString(),EvidenceKind=r.ReadInt32(),TargetSlotId=r.ReadString(),RequiredEvidenceCount=r.ReadInt32()};var q=ReadCount(r,"ContractEvidenceCount");for(var k=0;k<q;k++)o.Evidence.Add(new ContractEvidenceSaveData{EvidenceId=r.ReadString(),Kind=r.ReadInt32(),TargetKind=r.ReadInt32(),TargetId=r.ReadString(),OccurredAt=r.ReadInt64()});x.Objectives.Add(o);}x.OfferedAt=r.ReadInt64();x.AssigneeCharacterId=r.ReadString();x.AcceptedAt=ReadNullable(r);x.Deadline=ReadNullable(r);x.TerminalAt=ReadNullable(r);x.LinkedEncounterId=r.ReadString();x.LinkedBattleId=r.ReadString();x.Lifecycle=r.ReadInt32();x.OutcomeApplied=r.ReadBoolean();d.Contracts.Add(x);}RequireFullyConsumed(s);}}
        private static string EncodeAI(CampaignSaveData d){using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Encoding.UTF8,true)){w.Write(d.AIControllers.Count);foreach(var x in d.AIControllers){w.Write(x.ControllerId);w.Write(x.OwnerKind);w.Write(x.OwnerId);w.Write(x.PriorityProfileId);w.Write(x.CharacterProfileId);w.Write(x.QualityProfileId);w.Write(x.SchedulingProfileId);w.Write(x.RngState);w.Write(x.RngDrawCount);WriteNullable(w,x.LastDecisionAt);WriteNullable(w,x.NextDecisionAt);w.Write(x.Lifecycle);w.Write(x.CurrentPlan!=null);if(x.CurrentPlan!=null){var p=x.CurrentPlan;w.Write(p.PlanId);w.Write(p.GoalId);w.Write(p.CandidateId);w.Write(p.Domain);w.Write(p.PolicyId);w.Write(p.HasTarget);w.Write(p.TargetKind);w.Write(p.TargetId);w.Write(p.CreatedAt);WriteNullable(w,p.ReconsiderAt);w.Write(p.Lifecycle);WriteNullable(w,p.TerminalAt);w.Write(p.TerminalReason.HasValue);if(p.TerminalReason.HasValue)w.Write(p.TerminalReason.Value);}}w.Flush();return Convert.ToBase64String(s.ToArray());}}
        private static void DecodeAI(string value,CampaignSaveData d){using(var s=new MemoryStream(Convert.FromBase64String(value)))using(var r=new BinaryReader(s,Encoding.UTF8,true)){var n=ReadCount(r,"AIControllerCount");for(var i=0;i<n;i++){var x=new AIControllerSaveData{ControllerId=r.ReadString(),OwnerKind=r.ReadInt32(),OwnerId=r.ReadString(),PriorityProfileId=r.ReadString(),CharacterProfileId=r.ReadString(),QualityProfileId=r.ReadString(),SchedulingProfileId=r.ReadString(),RngState=r.ReadUInt64(),RngDrawCount=r.ReadUInt64(),LastDecisionAt=ReadNullable(r),NextDecisionAt=ReadNullable(r),Lifecycle=r.ReadInt32()};if(r.ReadBoolean()){var p=new AIPlanSaveData{PlanId=r.ReadString(),GoalId=r.ReadString(),CandidateId=r.ReadString(),Domain=r.ReadInt32(),PolicyId=r.ReadString(),HasTarget=r.ReadBoolean(),TargetKind=r.ReadInt32(),TargetId=r.ReadString(),CreatedAt=r.ReadInt64(),ReconsiderAt=ReadNullable(r),Lifecycle=r.ReadInt32(),TerminalAt=ReadNullable(r)};if(r.ReadBoolean())p.TerminalReason=r.ReadInt32();x.CurrentPlan=p;}d.AIControllers.Add(x);}RequireFullyConsumed(s);}}
        private static void WriteNullable(BinaryWriter w,long? value){w.Write(value.HasValue);if(value.HasValue)w.Write(value.Value);}
        private static long? ReadNullable(BinaryReader r)=>r.ReadBoolean()?(long?)r.ReadInt64():null;
    }
}
