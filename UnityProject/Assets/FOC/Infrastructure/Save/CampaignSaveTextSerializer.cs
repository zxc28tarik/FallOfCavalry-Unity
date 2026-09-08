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
    }
}
