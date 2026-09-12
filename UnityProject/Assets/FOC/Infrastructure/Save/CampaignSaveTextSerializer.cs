using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FOC.Application.Save;

namespace FOC.Infrastructure.Save
{
    public sealed class CampaignSaveTextSerializer : ISaveSerializer
    {
        private const string Header = "FOC_CAMPAIGN_SAVE";

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
            return builder.ToString();
        }

        public SaveReadResult Deserialize(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return SaveReadResult.Failed("Save content is empty.");
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
    }
}
