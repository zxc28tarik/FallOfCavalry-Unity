using System;
using System.Security.Cryptography;
using System.Text;

namespace FOC.Application.Save
{
    public static class SavePayloadFingerprint
    {
        public static string Compute(string canonicalPayload)
        {
            if (canonicalPayload == null) throw new ArgumentNullException(nameof(canonicalPayload));
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonicalPayload));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (var value in bytes) builder.Append(value.ToString("x2"));
                return builder.ToString();
            }
        }

        public static string Compute(ISaveSerializer serializer, CampaignSaveData data)
        {
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));
            return Compute(serializer.Serialize(data ?? throw new ArgumentNullException(nameof(data))));
        }
    }
}
