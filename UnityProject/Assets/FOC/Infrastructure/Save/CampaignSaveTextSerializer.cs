using System;
using System.Collections.Generic;
using System.Globalization;
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

                return SaveReadResult.Succeeded(data);
            }
            catch (Exception exception) when (
                exception is FormatException ||
                exception is OverflowException ||
                exception is KeyNotFoundException)
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
    }
}

