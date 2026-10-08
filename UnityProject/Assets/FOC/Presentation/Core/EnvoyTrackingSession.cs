#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Domain.Campaign;
using FOC.Domain.Diplomacy;

namespace FOC.Presentation.Core
{
    public sealed class EnvoyTrackingRow
    {
        internal EnvoyTrackingRow(string id, string character, string target, string type, int stateIndex, string state, long created)
        { Id = id; CharacterReference = character; TargetReference = target; Type = type; StateIndex = stateIndex; State = state; CreatedAt = created; }
        public string Id { get; }
        public string CharacterReference { get; }
        public string TargetReference { get; }
        public string Type { get; }
        public int StateIndex { get; }
        public string State { get; }
        public long CreatedAt { get; }
        public string Text => CharacterReference + " · " + Type + " · " + State;
    }
    public sealed class EnvoyTrackingDetail
    {
        internal EnvoyTrackingDetail(string id, string metadata, IReadOnlyList<string> messages)
        { Id = id; Metadata = metadata; Messages = messages; }
        public string Id { get; }
        public string Metadata { get; }
        public IReadOnlyList<string> Messages { get; }
    }
    public sealed class EnvoyTrackingSnapshot
    {
        internal EnvoyTrackingSnapshot(IReadOnlyList<EnvoyTrackingRow> rows, int total, string search, int filter, string selected, EnvoyTrackingDetail? detail)
        { Rows = rows; TotalKnown = total; Search = search; FilterIndex = filter; SelectedId = selected; Detail = detail; }
        public IReadOnlyList<EnvoyTrackingRow> Rows { get; }
        public int TotalKnown { get; }
        public string Search { get; }
        public int FilterIndex { get; }
        public string SelectedId { get; }
        public EnvoyTrackingDetail? Detail { get; }
        public string EmptyMessage => TotalKnown == 0 ? "Henüz kendi aktörünüze ait elçi görevi yok. Bu ekran yeni görev göndermiyor." : "Bu arama ve bilinen durum filtresine uygun görev yok.";
    }

    /// <summary>Own order metadata, explicitly authorized phase, and received linked responses. Never a remote live tracker.</summary>
    public sealed class EnvoyTrackingSession : IDisposable
    {
        private readonly CampaignRuntimeState _campaign;
        private readonly PresentationViewerContext _viewer;
        private string _search = "", _selected = "";
        private int _filter;
        private bool _disposed;
        public EnvoyTrackingSession(CampaignRuntimeState campaign, PresentationViewerContext viewer)
        { _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign)); _viewer = viewer ?? throw new ArgumentNullException(nameof(viewer)); }
        public void Filter(string? search, int stateIndex)
        {
            Check();
            if (stateIndex < 0 || stateIndex >= EnvoyTrackingText.FilterLabels.Count) throw new ArgumentOutOfRangeException(nameof(stateIndex));
            _search = search ?? ""; _filter = stateIndex; _selected = "";
        }
        public void OpenMission(string? id) { Check(); if (id != null) _selected = id; }
        public EnvoyTrackingSnapshot Read()
        {
            Check(); var now = _campaign.Clock.Now.Ticks;
            var known = _campaign.Diplomacy.Missions.OrderedMissions.Where(x => x.SourceActorId.Equals(_viewer.ActorId) && x.CreatedAt.Ticks <= now)
                .OrderByDescending(x => x.CreatedAt.Ticks).ThenBy(x => x.Id.Value, StringComparer.Ordinal).ToArray();
            var key = ReportInboxText.SearchKey(_search.Trim());
            var rows = known.Select(x => new EnvoyTrackingRow(x.Id.Value, x.CharacterId.Value, x.TargetActorId.Value, EnvoyTrackingText.Type(x.MissionType),
                    KnownState(x, now), EnvoyTrackingText.State(KnownState(x, now)), x.CreatedAt.Ticks))
                .Where(x => (_filter == 0 || x.StateIndex == _filter) && ReportInboxText.SearchKey(x.Id + " " + x.CharacterReference + " " + x.TargetReference + " " + x.Type + " " + x.State).IndexOf(key, StringComparison.Ordinal) >= 0).ToArray();
            if (_selected.Length == 0) _selected = rows.FirstOrDefault()?.Id ?? "";
            var mission = rows.Any(x => x.Id == _selected) ? known.FirstOrDefault(x => x.Id.Value == _selected) : null;
            EnvoyTrackingDetail? detail = null;
            if (mission != null)
            {
                var exact = _viewer.CanReadExact(new PresentationEntityRef(PresentationEntityKind.EnvoyMission, mission.Id.Value));
                var metadata = "Elçi referansı: " + mission.CharacterId.Value + " · Hedef: " + mission.TargetActorId.Value
                    + "\nOrganizasyon: " + mission.OrganizationId.Value + " · Atama: " + mission.AssignmentId.Value
                    + "\nGörev: " + EnvoyTrackingText.Type(mission.MissionType) + " · Yetki sınırı: " + EnvoyTrackingText.Scope(mission.Mandate.Scope)
                    + "\nİzinli işlemler: " + string.Join(", ", mission.Mandate.OrderedAllowedActions.Select(EnvoyTrackingText.Action))
                    + "\nOluşturma: " + ReportInboxText.Timestamp(mission.CreatedAt.Ticks) + " · Yerel sevk: " + Time(mission.DepartedAt?.Ticks, now)
                    + "\nBilinen durum: " + EnvoyTrackingText.State(KnownState(mission, now));
                if (exact) metadata += "\nYetkili görev kaydı · Varış: " + Time(mission.ArrivedAt?.Ticks, now) + " · Kapanış: " + Time(mission.CompletedAt?.Ticks, now);
                else metadata += "\nUzak varış, görüşme, dönüş ve sonuç teyidi yok; canlı konum veya tahminî varış açılmaz.";
                metadata += "\nReferanslar canlı karakter durumunu sorgulamaz. Yanıtın ulaşması kabul veya görev tamamlanması değildir.";
                var outbound = _campaign.Diplomacy.Messages.OrderedMessages.Where(x => x.SenderActorId.Equals(_viewer.ActorId)
                    && x.RecipientActorId.Equals(mission.TargetActorId) && x.EnvoyMissionId.HasValue && x.EnvoyMissionId.Value.Equals(mission.Id)
                    && x.CreatedAt.Ticks <= now).OrderBy(x => x.Id.Value, StringComparer.Ordinal).ToArray();
                var messages = new List<string>();
                foreach (var message in outbound)
                {
                    var dispatched = message.DispatchedAt.HasValue && message.DispatchedAt.Value.Ticks <= now;
                    messages.Add("Giden mesaj: " + message.Id.Value + " · " + EnvoyTrackingText.Message(message.Kind)
                        + "\n" + (dispatched ? "Sevk: " + ReportInboxText.Timestamp(message.DispatchedAt!.Value.Ticks) : "Henüz sevk edilmedi")
                        + " · Yabancı alıcıya teslim teyidi: bilinmiyor");
                    var replies = _campaign.Diplomacy.Messages.OrderedMessages.Where(x => x.ResponseTo.HasValue && x.ResponseTo.Value.Equals(message.Id)
                        && x.RecipientActorId.Equals(_viewer.ActorId) && x.SenderActorId.Equals(mission.TargetActorId)
                        && x.Status == CommunicationStatus.Delivered && x.DeliveredAt.HasValue && x.DeliveredAt.Value.Ticks <= now)
                        .OrderByDescending(x => x.DeliveredAt!.Value.Ticks).ThenBy(x => x.Id.Value, StringComparer.Ordinal);
                    foreach (var reply in replies) messages.Add("Ulaşan bağlı yanıt: " + reply.Id.Value + " · " + EnvoyTrackingText.Message(reply.Kind)
                        + "\nTeslim: " + ReportInboxText.Timestamp(reply.DeliveredAt!.Value.Ticks) + " · Yanıt verilen mesaj: " + message.Id.Value);
                }
                if (messages.Count == 0) messages.Add("Bu göreve bağlı bilinen giden mesaj yok.");
                detail = new EnvoyTrackingDetail(mission.Id.Value, metadata, Array.AsReadOnly(messages.ToArray()));
            }
            return new EnvoyTrackingSnapshot(Array.AsReadOnly(rows), known.Length, _search, _filter, _selected, detail);
        }
        private int KnownState(EnvoyMissionState mission, long now)
        {
            if (_viewer.CanReadExact(new PresentationEntityRef(PresentationEntityKind.EnvoyMission, mission.Id.Value))) return (int)mission.Phase + 1;
            return mission.DepartedAt.HasValue && mission.DepartedAt.Value.Ticks <= now ? 8 : 1;
        }
        private static string Time(long? ticks, long now) => ticks.HasValue && ticks.Value <= now ? ReportInboxText.Timestamp(ticks.Value) : "Kayıt yok";
        public void Dispose() { _disposed = true; }
        private void Check() { if (_disposed) throw new ObjectDisposedException(nameof(EnvoyTrackingSession)); }
    }
    public static class EnvoyTrackingText
    {
        public static IReadOnlyList<string> FilterLabels { get; } = Array.AsReadOnly(new[] { "Tüm bilinen durumlar", "Sevk bekliyor", "Yolda", "Vardı", "Görüşme bekliyor", "Dönüyor", "Tamamlandı", "Başarısız", "Sevk edildi; uzak durum bilinmiyor" });
        internal static string State(int index) => FilterLabels[index];
        internal static string Type(EnvoyMissionType type) => new[] { "Ticaret müzakeresi", "Barış arayışı", "Esir değişimi", "Talep iletme", "Görüşme talebi", "Mesaj iletme", "Bilgi isteme", "Arabuluculuk" }[(int)type];
        internal static string Scope(DiplomaticAuthorityScope scope) => new[] { "Yalnız iletme", "Müzakere", "Sonuçlandırma" }[(int)scope];
        internal static string Action(DiplomaticActionKind kind) => new[] { "İttifak", "Ticaret anlaşması", "Geçiş", "Ateşkes", "Barış", "Savaş", "Fidye", "Esir değişimi", "Tazminat", "Talep", "Tehdit", "Hediye", "Arabuluculuk", "İmtiyaz", "Görüşme talebi", "Mesaj iletme", "Bilgi isteme" }[(int)kind];
        internal static string Message(MessageKind kind) => new[] { "Diplomatik emir", "Teklif", "Talep", "Uyarı", "Görüşme talebi", "Bilgi talebi", "Yanıt", "Rapor teslimi" }[(int)kind];
    }
}
