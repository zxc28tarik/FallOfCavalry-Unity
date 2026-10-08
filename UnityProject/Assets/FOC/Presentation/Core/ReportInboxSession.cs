#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FOC.Domain.Campaign;
using FOC.Domain.Diplomacy;

namespace FOC.Presentation.Core
{
    public sealed class ReportInboxRow
    {
        internal ReportInboxRow(string id, int typeIndex, string type, string source, string reference, long observed, long arrived, long age)
        { Id = id; TypeIndex = typeIndex; Type = type; Source = source; SourceReference = reference; ObservedAt = observed; ArrivedAt = arrived; Age = age; }
        public string Id { get; }
        public int TypeIndex { get; }
        public string Type { get; }
        public string Source { get; }
        public string SourceReference { get; }
        public long ObservedAt { get; }
        public long ArrivedAt { get; }
        public long Age { get; }
        public string Text => Type + " · " + Source + " · teslim " + ReportInboxText.Timestamp(ArrivedAt) + " · bilgi yaşı " + ReportInboxText.Duration(Age);
    }
    public sealed class ReportInboxDetail
    {
        internal ReportInboxDetail(string id, string title, string metadata, IReadOnlyList<string> observations)
        { Id = id; Title = title; Metadata = metadata; Observations = observations; }
        public string Id { get; }
        public string Title { get; }
        public string Metadata { get; }
        public IReadOnlyList<string> Observations { get; }
    }
    public sealed class ReportInboxSnapshot
    {
        internal ReportInboxSnapshot(IReadOnlyList<ReportInboxRow> rows, int total, string search, int filter, string selected, ReportInboxDetail? detail)
        { Rows = rows; TotalDelivered = total; Search = search; FilterIndex = filter; SelectedId = selected; Detail = detail; }
        public IReadOnlyList<ReportInboxRow> Rows { get; }
        public int TotalDelivered { get; }
        public string Search { get; }
        public int FilterIndex { get; }
        public string SelectedId { get; }
        public ReportInboxDetail? Detail { get; }
        public string EmptyMessage => TotalDelivered == 0 ? "Henüz size teslim edilmiş rapor yok. Yoldaki raporlar okunamaz." : "Bu arama ve tür filtresine uygun rapor yok.";
    }

    /// <summary>Read-only dated information. Never resolves source or subject against live world state.</summary>
    public sealed class ReportInboxSession : IDisposable
    {
        private readonly CampaignRuntimeState _campaign;
        private readonly PresentationViewerContext _viewer;
        private string _search = "", _selected = "";
        private int _filter;
        private bool _disposed;
        public ReportInboxSession(CampaignRuntimeState campaign, PresentationViewerContext viewer)
        { _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign)); _viewer = viewer ?? throw new ArgumentNullException(nameof(viewer)); }
        public void Filter(string? search, int typeIndex)
        {
            Check();
            if (typeIndex < 0 || typeIndex >= ReportInboxText.FilterLabels.Count) throw new ArgumentOutOfRangeException(nameof(typeIndex));
            _search = search ?? ""; _filter = typeIndex; _selected = "";
        }
        public void OpenReport(string? id) { Check(); if (id != null) _selected = id; }
        public ReportInboxSnapshot Read()
        {
            Check();
            var now = _campaign.Clock.Now;
            var information = _campaign.Diplomacy.Information.OrderedInformation.FirstOrDefault(x => x.ActorId.Equals(_viewer.ActorId));
            var available = (information?.OrderedAvailableReports ?? Array.Empty<ReportState>())
                .Where(x => x.RecipientActorId.Equals(_viewer.ActorId) && x.Status == CommunicationStatus.Delivered
                    && x.ArrivedAt.HasValue && x.ArrivedAt.Value.Ticks <= now.Ticks)
                .OrderByDescending(x => x.ArrivedAt!.Value.Ticks).ThenBy(x => x.Id.Value, StringComparer.Ordinal).ToArray();
            var key = ReportInboxText.SearchKey(_search.Trim());
            var rows = available.Select(x => new ReportInboxRow(x.Id.Value, (int)x.Type + 1, ReportInboxText.Type(x.Type),
                    ReportInboxText.Source(x.Source.Kind), SourceReference(x), x.ObservedAt.Ticks, x.ArrivedAt!.Value.Ticks, x.StalenessAt(now)))
                .Where(x => (_filter == 0 || x.TypeIndex == _filter) && ReportInboxText.SearchKey(x.Id + " " + x.Type + " " + x.Source + " " + x.SourceReference).IndexOf(key, StringComparison.Ordinal) >= 0).ToArray();
            if (_selected.Length == 0) _selected = rows.FirstOrDefault()?.Id ?? "";
            var report = rows.Any(x => StringComparer.Ordinal.Equals(x.Id, _selected))
                ? available.FirstOrDefault(x => StringComparer.Ordinal.Equals(x.Id.Value, _selected)) : null;
            ReportInboxDetail? detail = null;
            if (report != null)
            {
                var precisions = report.OrderedObservations.Select(x => x.Precision).Distinct().ToArray();
                var metadata = "Kaynak: " + ReportInboxText.Source(report.Source.Kind) + " · Rapor kaynak referansı: " + SourceReference(report)
                    + "\nGözlem: " + ReportInboxText.Timestamp(report.ObservedAt.Ticks)
                    + " · Gönderim: " + ReportInboxText.Timestamp(report.DispatchedAt!.Value.Ticks)
                    + " · Teslim: " + ReportInboxText.Timestamp(report.ArrivedAt!.Value.Ticks)
                    + "\nBilgi yaşı: " + ReportInboxText.Duration(report.StalenessAt(now)) + " (gözlemden itibaren)"
                    + " · Kalite: " + ReportInboxText.Quality(report.Quality) + " · Ayrıntı: " + ReportInboxText.Detail(report.DetailLevel)
                    + "\nKesinlik: " + (precisions.Length == 1 ? ReportInboxText.Precision(precisions[0]) : "Karışık; her gözlemde ayrı belirtilir")
                    + "\nBu içerik tarihli bir gözlemdir; güncel dünya gerçeği değildir. Kaynak ve konu referansları canlı durum sorgulamaz.";
                var observations = report.OrderedObservations.Select(x => ReportInboxText.Subject(x.Subject.Kind) + " · " + x.Subject.Id
                    + "\n" + ReportInboxText.Observation(x.Kind) + " · " + ReportInboxText.Precision(x.Precision) + " · " + ReportInboxText.Value(x)).ToArray();
                detail = new ReportInboxDetail(report.Id.Value, ReportInboxText.Type(report.Type) + " · " + report.Id.Value, metadata, Array.AsReadOnly(observations));
            }
            return new ReportInboxSnapshot(Array.AsReadOnly(rows), available.Length, _search, _filter, _selected, detail);
        }
        private static string SourceReference(ReportState report) => report.Source.Kind == ReportSourceKind.CityInstitution
            ? report.Source.CityId!.Value.Value + " / " + report.Source.BuildingId!.Value.Value : report.Source.Id;
        public void Dispose() { _disposed = true; }
        private void Check() { if (_disposed) throw new ObjectDisposedException(nameof(ReportInboxSession)); }
    }

    public static class ReportInboxText
    {
        public static IReadOnlyList<string> FilterLabels { get; } = Array.AsReadOnly(new[] { "Tüm türler", "Şehir", "Ticaret", "Diplomasi", "Karakter", "Güvenlik", "Askerî" });
        public static string Timestamp(long ticks) => "T+" + ticks.ToString(CultureInfo.InvariantCulture) + " sn";
        public static string Duration(long ticks) => (ticks / 86400).ToString(CultureInfo.InvariantCulture) + " gün " + ((ticks / 3600) % 24).ToString(CultureInfo.InvariantCulture) + " sa " + ((ticks / 60) % 60).ToString(CultureInfo.InvariantCulture) + " dk " + (ticks % 60).ToString(CultureInfo.InvariantCulture) + " sn";
        internal static string SearchKey(string text) => text.Normalize().Replace('ı', 'I').Replace('i', 'İ').ToUpperInvariant();
        internal static string Type(ReportType x) => FilterLabels[(int)x + 1];
        internal static string Source(ReportSourceKind x) => new[] { "Elçi", "Haberci", "Görevli", "Tüccar", "Kervan", "Şehir kurumu", "Diplomatik temas" }[(int)x];
        internal static string Quality(ReportQuality x) => new[] { "Düşük", "Orta", "Yüksek" }[(int)x];
        internal static string Detail(ReportDetailLevel x) => new[] { "Özet", "Standart", "Ayrıntılı" }[(int)x];
        internal static string Precision(ObservationPrecision x) => new[] { "Kesin", "Yaklaşık", "Aralık", "Nitel", "Bilinmiyor" }[(int)x];
        internal static string Subject(ReportSubjectKind x) => new[] { "Devlet", "Şehir", "Kervan", "Karakter", "Pazar", "Diplomatik ilişki", "Ordu" }[(int)x];
        internal static string Observation(ReportObservationKind x) => new[] { "Şehir durumu", "Pazar durumu", "Diplomatik tutum", "Kervan durumu", "Karakter durumu", "Güvenlik durumu", "Müzakere ilerlemesi", "Tahminî ordu gücü", "Ordu konumu", "Ordu ikmali" }[(int)x];
        internal static string Value(ReportObservation x) => x.Precision switch {
            ObservationPrecision.Exact => x.Lower!.Value.ToString(CultureInfo.InvariantCulture),
            ObservationPrecision.Approximate => "yaklaşık " + x.Lower!.Value.ToString(CultureInfo.InvariantCulture),
            ObservationPrecision.Range => x.Lower!.Value.ToString(CultureInfo.InvariantCulture) + "–" + x.Upper!.Value.ToString(CultureInfo.InvariantCulture),
            ObservationPrecision.Qualitative => new[] { "Bilinmiyor", "Çok düşük", "Düşük", "Orta", "Yüksek", "Çok yüksek", "İyileşiyor", "Değişmiyor", "Kötüleşiyor" }[(int)x.Qualitative],
            _ => "Bilinmiyor (sıfır değildir)" };
    }
}
