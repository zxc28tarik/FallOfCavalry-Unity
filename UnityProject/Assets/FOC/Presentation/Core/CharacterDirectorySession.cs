#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;

namespace FOC.Presentation.Core
{
    public sealed class CharacterDirectoryRow
    {
        internal CharacterDirectoryRow(string id, string name, string status)
        { Id = id; Name = name; Status = status; }
        public string Id { get; }
        public string Name { get; }
        public string Status { get; }
        public string Text => Name + " · " + Status;
    }

    public sealed class CharacterDirectorySnapshot
    {
        internal CharacterDirectorySnapshot(IReadOnlyList<CharacterDirectoryRow> rows, int total, string search, string? selectedId, string summary)
        { Rows = rows; TotalKnown = total; Search = search; SelectedId = selectedId; Summary = summary; }
        public IReadOnlyList<CharacterDirectoryRow> Rows { get; }
        public int TotalKnown { get; }
        public string Search { get; }
        public string? SelectedId { get; }
        public string Summary { get; }
        public string EmptyMessage => TotalKnown == 0 ? "Kesin bilgisi erişilebilir karakter yok." : "Bu aramaya uygun bilinen karakter yok.";
    }

    /// <summary>Read-only directory over characters explicitly granted as exact-readable to the viewer.</summary>
    public sealed class CharacterDirectorySession : IDisposable
    {
        private readonly CampaignRuntimeState _campaign;
        private readonly PresentationViewerContext _viewer;
        private string _search = "";
        private string? _selected;
        private bool _disposed;

        public CharacterDirectorySession(CampaignRuntimeState campaign, PresentationViewerContext viewer)
        { _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign)); _viewer = viewer ?? throw new ArgumentNullException(nameof(viewer)); }

        public void Filter(string? search) { Check(); _search = search ?? ""; }
        public void OpenCharacter(string? id) { Check(); if (id != null) _selected = id; }

        public CharacterDirectorySnapshot Read()
        {
            Check();
            var known = _campaign.Characters.OrderedCharacters
                .Where(x => _viewer.CanReadExact(new PresentationEntityRef(PresentationEntityKind.Character, x.Id.Value)))
                .OrderBy(x => x.Definition.DisplayName, StringComparer.Ordinal).ThenBy(x => x.Id.Value, StringComparer.Ordinal).ToArray();
            var key = ReportInboxText.SearchKey(_search.Trim());
            var rows = known.Select(x => new CharacterDirectoryRow(x.Id.Value, x.Definition.DisplayName, CharacterDirectoryText.Status(x)))
                .Where(x => ReportInboxText.SearchKey(x.Name + " " + x.Id + " " + x.Status).IndexOf(key, StringComparison.Ordinal) >= 0).ToArray();
            if (_selected == null) _selected = rows.FirstOrDefault()?.Id;
            var selected = known.FirstOrDefault(x => x.Id.Value == _selected);
            var summary = selected == null
                ? (_selected == null ? "Bir karakter seçin. Yalnız kesin erişim verilen kişiler listelenir." : "Seçilen karakter erişilemiyor; başka bir kayıt gösterilmedi.")
                : selected.Definition.DisplayName + " · Ref: " + selected.Id.Value + "\n" + CharacterDirectoryText.Identity(selected)
                    + "\n" + CharacterDirectoryText.Location(selected) + " · " + CharacterDirectoryText.Health(selected);
            return new CharacterDirectorySnapshot(Array.AsReadOnly(rows), known.Length, _search, _selected, summary);
        }

        public void Dispose() { _disposed = true; }
        private void Check() { if (_disposed) throw new ObjectDisposedException(nameof(CharacterDirectorySession)); }
    }

    public static class CharacterDirectoryText
    {
        internal static string Status(CharacterState x) => (x.IsDead ? "Ölü" : "Hayatta") + " · " + Location(x);
        internal static string Identity(CharacterState x) => "Kimlik: " + new[] { "Kayıtlı kişi", "Adlandırılmış karakter", "Üretilmiş kişi" }[(int)x.Definition.IdentityKind]
            + " · Kaynak: " + new[] { "Kayıtlı", "Tarihsel", "Üretilmiş", "Üretilmiş kişiden yükseltilmiş" }[(int)x.Definition.Provenance]
            + " · Önem: " + (x.Importance.HasValue ? x.Importance.Value.ToString() : "Uygulanamaz");
        internal static string Health(CharacterState x) => Injury(x) + " · " + Captivity(x);
        internal static string Injury(CharacterState x)
        {
            return x.Injury == null ? "Yaralanma yok" : "Yaralanma: " + new[] { "Hafif", "Ciddi", "Kalıcı", "Görev yapamaz" }[(int)x.Injury.Severity]
                + " (olay T+" + x.Injury.OccurredAt.Ticks + (x.Injury.ExpectedRecoveryAt.HasValue ? ", beklenen iyileşme T+" + x.Injury.ExpectedRecoveryAt.Value.Ticks : ", iyileşme tarihi tanımlı değil") + ")";
        }
        internal static string Captivity(CharacterState x) => x.Captivity == null ? "Esir değil" : "Esir · Tutan karakter ref: " + x.Captivity.CaptorId.Value + " · Yer: " + CaptivitySite(x.Captivity.Site);
        internal static string Location(CharacterState x)
        {
            var p = x.Location.Position;
            return x.Location.Kind switch
            {
                CharacterLocationKind.City => "Şehir: " + x.Location.CityId!.Value.Value,
                CharacterLocationKind.Army => "Ordu: " + x.Location.ArmyId!.Value.Value,
                CharacterLocationKind.Caravan => "Kervan: " + x.Location.CaravanId!.Value.Value,
                CharacterLocationKind.WorldPosition => "Dünya konumu: " + p.X + ", " + p.Y,
                CharacterLocationKind.Travelling => "Yolculukta: " + p.X + ", " + p.Y,
                CharacterLocationKind.Captivity => "Esaret: " + CaptivitySite(x.Captivity!.Site),
                _ => "Konum bilinmiyor"
            };
        }
        private static string CaptivitySite(CaptivitySite site) => site.Kind switch
        {
            CaptivitySiteKind.City => "şehir " + site.CityId!.Value.Value,
            CaptivitySiteKind.Army => "ordu " + site.ArmyId!.Value.Value,
            CaptivitySiteKind.Caravan => "kervan " + site.CaravanId!.Value.Value,
            CaptivitySiteKind.WorldPosition => "dünya konumu " + site.Position.X + ", " + site.Position.Y,
            _ => "bilinmeyen yer"
        };
    }
}
