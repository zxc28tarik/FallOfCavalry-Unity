#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Domain.Campaign;
using FOC.Domain.Diplomacy;

namespace FOC.Presentation.Core
{
    public sealed class DiplomaticRecordRow
    {
        internal DiplomaticRecordRow(PresentationEntityRef subject, int type, string text, long at)
        { Subject = subject; TypeIndex = type; Text = text; RecordedAt = at; }
        public PresentationEntityRef Subject { get; }
        public int TypeIndex { get; }
        public string Text { get; }
        public long RecordedAt { get; }
    }
    public sealed class DiplomaticRecordDetail
    {
        internal DiplomaticRecordDetail(PresentationEntityRef subject, string metadata, IReadOnlyList<string> entries)
        { Subject = subject; Metadata = metadata; Entries = entries; }
        public PresentationEntityRef Subject { get; }
        public string Metadata { get; }
        public IReadOnlyList<string> Entries { get; }
    }
    public sealed class DiplomaticRecordsSnapshot
    {
        internal DiplomaticRecordsSnapshot(IReadOnlyList<DiplomaticRecordRow> rows, int total, string search, int filter, PresentationEntityRef? selected, DiplomaticRecordDetail? detail)
        { Rows = rows; TotalKnown = total; Search = search; FilterIndex = filter; Selected = selected; Detail = detail; }
        public IReadOnlyList<DiplomaticRecordRow> Rows { get; }
        public int TotalKnown { get; }
        public string Search { get; }
        public int FilterIndex { get; }
        public PresentationEntityRef? Selected { get; }
        public DiplomaticRecordDetail? Detail { get; }
        public string EmptyMessage => TotalKnown == 0 ? "Kendi aktörünüzün bilinen ilişki veya imzalı anlaşma kaydı yok." : "Bu arama ve kayıt türüne uygun bilinen kayıt yok.";
    }
    /// <summary>Local own-party records only. No global foreign knowledge or treaty effects.</summary>
    public sealed class DiplomaticRecordsSession : IDisposable
    {
        private readonly CampaignRuntimeState _campaign;
        private readonly PresentationViewerContext _viewer;
        private string _search = "";
        private int _filter;
        private PresentationEntityRef? _selected;
        private bool _disposed;
        public DiplomaticRecordsSession(CampaignRuntimeState campaign, PresentationViewerContext viewer)
        { _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign)); _viewer = viewer ?? throw new ArgumentNullException(nameof(viewer)); }
        public void Filter(string? search, int typeIndex)
        {
            Check(); if (typeIndex < 0 || typeIndex >= DiplomaticRecordsText.FilterLabels.Count) throw new ArgumentOutOfRangeException(nameof(typeIndex));
            _search = search ?? ""; _filter = typeIndex; _selected = null;
        }
        public void OpenRecord(PresentationEntityRef? subject) { Check(); if (subject.HasValue) _selected = subject; }
        public DiplomaticRecordsSnapshot Read()
        {
            Check(); var now = _campaign.Clock.Now;
            var relations = _campaign.Diplomacy.Relations.OrderedRelations.Where(x => Own(x.Pair) && x.UpdatedAt.Ticks <= now.Ticks).ToArray();
            var agreements = _campaign.Diplomacy.Agreements.OrderedAgreements.Where(x => Own(x.Parties) && x.SignedAt.Ticks <= now.Ticks).ToArray();
            var all = relations.Select(x => new DiplomaticRecordRow(RelationRef(x), 1, "İlişki · " + Other(x.Pair) + " · " + DiplomaticRecordsText.Disposition(x.Disposition), x.UpdatedAt.Ticks))
                .Concat(agreements.Select(x => new DiplomaticRecordRow(AgreementRef(x), 2, "Anlaşma · " + Other(x.Parties) + " · " + DiplomaticRecordsText.Agreement(x.Kind), x.SignedAt.Ticks)))
                .OrderByDescending(x => x.RecordedAt).ThenBy(x => x.Subject).ToArray();
            var key = ReportInboxText.SearchKey(_search.Trim());
            var rows = all.Where(x => (_filter == 0 || x.TypeIndex == _filter) && ReportInboxText.SearchKey(x.Text + " " + x.Subject.Id).IndexOf(key, StringComparison.Ordinal) >= 0).ToArray();
            if (!_selected.HasValue) _selected = rows.FirstOrDefault()?.Subject;
            DiplomaticRecordDetail? detail = null;
            if (rows.Any(x => x.Subject.Equals(_selected)))
            {
                var relation = relations.FirstOrDefault(x => RelationRef(x).Equals(_selected));
                var agreement = agreements.FirstOrDefault(x => AgreementRef(x).Equals(_selected));
                if (relation != null)
                {
                    var entries = relation.OrderedFactors.Where(x => x.OccurredAt.Ticks <= now.Ticks).OrderByDescending(x => x.OccurredAt.Ticks)
                        .ThenBy(x => x.Source).ThenBy(x => x.SourceId, StringComparer.Ordinal)
                        .Select(x => DiplomaticRecordsText.Factor(x.Source) + " · " + DiplomaticRecordsText.Direction(x.Direction)
                            + "\nKaynak ref: " + x.SourceId + " · Olay: " + ReportInboxText.Timestamp(x.OccurredAt.Ticks)).ToArray();
                    detail = new DiplomaticRecordDetail(RelationRef(relation), Parties(relation.Pair) + "\nYerel ilişki kaydı · Tutum: " + DiplomaticRecordsText.Disposition(relation.Disposition)
                        + " · Güncelleme: " + ReportInboxText.Timestamp(relation.UpdatedAt.Ticks)
                        + "\nEtkenler ayrı kayıtlardır; sayısal puana veya otomatik din/savaş bonusuna çevrilmez.",
                        Array.AsReadOnly(entries.Length == 0 ? new[] { "Bu ilişki için bilinen tarihli etken kaydı yok." } : entries));
                }
                else if (agreement != null)
                {
                    detail = new DiplomaticRecordDetail(AgreementRef(agreement), Parties(agreement.Parties)
                        + "\nİmzalı taraf kaydı · Tür: " + DiplomaticRecordsText.Agreement(agreement.Kind)
                        + "\nİmza: " + ReportInboxText.Timestamp(agreement.SignedAt.Ticks) + " · Yürürlük başlangıcı: " + ReportInboxText.Timestamp(agreement.EffectiveAt.Ticks)
                        + "\nBitiş: " + (agreement.ExpiresAt.HasValue ? ReportInboxText.Timestamp(agreement.ExpiresAt.Value.Ticks) : "Tarih tanımlanmamış")
                        + " · Kayıt durumu: " + DiplomaticRecordsText.Status(agreement.Status)
                        + "\nŞu anda yürürlükte: " + (agreement.IsEffectiveAt(now) ? "Evet" : "Hayır")
                        + "\nMaddeler icra/teslim teyidi veya otomatik ekonomik/askerî etki değildir; tutar ve yaptırım tanımlı değil.",
                        Array.AsReadOnly(agreement.OrderedTerms.Select(x => "Madde · " + DiplomaticRecordsText.Term(x)).ToArray()));
                }
            }
            return new DiplomaticRecordsSnapshot(Array.AsReadOnly(rows), all.Length, _search, _filter, _selected, detail);
        }
        private bool Own(DiplomaticActorPair pair) => pair.First.Equals(_viewer.ActorId) || pair.Second.Equals(_viewer.ActorId);
        private string Other(DiplomaticActorPair pair) => pair.First.Equals(_viewer.ActorId) ? pair.Second.Value : pair.First.Value;
        private static string Parties(DiplomaticActorPair pair) => "Taraf ref: " + pair.First.Value + " / " + pair.Second.Value;
        private static PresentationEntityRef RelationRef(DiplomaticRelationState relation) => new PresentationEntityRef(PresentationEntityKind.DiplomaticRelation, relation.Id.Value);
        private static PresentationEntityRef AgreementRef(DiplomaticAgreementState agreement) => new PresentationEntityRef(PresentationEntityKind.DiplomaticAgreement, agreement.Id.Value);
        public void Dispose() { _disposed = true; }
        private void Check() { if (_disposed) throw new ObjectDisposedException(nameof(DiplomaticRecordsSession)); }
    }
    public static class DiplomaticRecordsText
    {
        public static IReadOnlyList<string> FilterLabels { get; } = Array.AsReadOnly(new[] { "Tüm bilinen kayıtlar", "İlişkiler", "Anlaşmalar" });
        internal static string Disposition(DiplomaticDisposition value) => new[] { "Düşmanca", "Gergin", "Tarafsız", "Dostane", "Güvenilir" }[(int)value];
        internal static string Direction(DiplomaticFactorDirection value) => new[] { "Olumsuz", "Nötr", "Olumlu" }[(int)value];
        internal static string Factor(DiplomaticFactorSource value) => new[] { "Yakın savaş", "Ticaret ilişkisi", "Antlaşma", "Hanedan bağı", "Din politikası", "Hakaret", "Sınır çatışması", "Ortak düşman", "Yardım", "Diplomatik görev", "Ödenmemiş yükümlülük", "Olay", "Hane bağı", "Clique baskısı" }[(int)value];
        internal static string Agreement(DiplomaticAgreementKind value) => new[] { "İttifak", "Ticaret anlaşması", "Geçiş anlaşması", "Ateşkes", "Barış antlaşması", "İmtiyaz anlaşması" }[(int)value];
        internal static string Term(DiplomaticAgreementTermKind value) => new[] { "Pazar erişimi", "Kervan koruması", "Geçiş", "Çatışmaları durdurma", "Karşılıklı destek", "İmtiyaz" }[(int)value];
        internal static string Status(DiplomaticAgreementStatus value) => new[] { "Active (kayıt)", "Expired (kayıt)", "Terminated (kayıt)" }[(int)value];
    }
}
