#nullable enable
using System;
using System.Linq;
using System.Reflection;
using FOC.Application.Save;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Cities;
using FOC.Domain.Common;
using FOC.Domain.Diplomacy;
using FOC.Domain.Economy;
using FOC.Domain.Time;
using FOC.Infrastructure.Save;
using FOC.Presentation.Core;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class ReportInboxTests
    {
        private static readonly FactionId Own = FactionId.Create("faction-ottoman-state"), Foreign = FactionId.Create("faction-republic-of-venice");
        private static CampaignRuntimeState Campaign() => PlayableTradeTests.Campaign();
        private static string State(CampaignRuntimeState c) => new CampaignSaveTextSerializer().Serialize(CampaignSaveMapper.ToSaveData(c));
        private static PresentationViewerContext Viewer(FactionId? actor = null, bool debug = false) => new PresentationViewerContext(actor ?? Own,
            new PresentationEntityRef(PresentationEntityKind.Character, "hasan-aga"), developmentDebug: debug);
        private static ReportState Add(CampaignRuntimeState c, string id = "delivered", ReportType type = ReportType.City,
            ObservationPrecision precision = ObservationPrecision.Qualitative, long? lower = null, long? upper = null,
            FactionId? recipient = null, long observed = 0, long arrived = 2, ReportSourceRef? source = null)
        {
            var report = new ReportState(ReportId.Create(id), type, source ?? ReportSourceRef.Character(ReportSourceKind.Official, CharacterId.Create("yusuf-katip")),
                recipient ?? Own, ReportQuality.Low, ReportDetailLevel.Detailed, new WorldTimestamp(observed),
                new[] { new ReportObservation(new ReportSubjectRef(ReportSubjectKind.City, "city-istanbul"), ReportObservationKind.CityCondition, precision, lower, upper,
                    precision == ObservationPrecision.Qualitative ? QualitativeObservation.Stable : QualitativeObservation.Unknown) },
                CommunicationStatus.Delivered, new WorldTimestamp(observed), new WorldTimestamp(arrived));
            c.Diplomacy.Reports.Add(report); c.Diplomacy.Information.GetRequired(recipient ?? Own).RestoreAvailable(report);
            if (c.Clock.Now.Ticks < arrived) c.Clock.Advance(new WorldDuration(arrived - c.Clock.Now.Ticks));
            return report;
        }
        [Test]
        public void StartingUndeliveredReportDoesNotEnterInboxOrDetailEvenWithDebugOrExactEntity()
        {
            var c = Campaign(); var id = c.Diplomacy.Reports.OrderedReports.Single().Id.Value;
            var viewer = new PresentationViewerContext(Own, new PresentationEntityRef(PresentationEntityKind.Character, "hasan-aga"),
                new[] { new PresentationEntityRef(PresentationEntityKind.Report, id) }, true);
            using var s = new ReportInboxSession(c, viewer); s.OpenReport(id); var r = s.Read();
            Assert.That(r.Rows, Is.Empty); Assert.That(r.Detail, Is.Null); Assert.That(r.TotalDelivered, Is.Zero);
            Assert.That(r.EmptyMessage, Does.Contain("Henüz"));
        }
        [Test]
        public void DeliveredForeignAndDispatchedReportsAreNeverAvailableToOwnActor()
        {
            var c = Campaign(); Add(c, "foreign", recipient: Foreign);
            var original = c.Diplomacy.Reports.OrderedReports.Single(x => x.Status == CommunicationStatus.Created); original.Dispatch(c.Clock.Now);
            using var s = new ReportInboxSession(c, Viewer(debug: true)); s.OpenReport("foreign");
            Assert.That(s.Read().Rows, Is.Empty); Assert.That(s.Read().Detail, Is.Null);
            using var other = new ReportInboxSession(c, Viewer(Foreign)); Assert.That(other.Read().Rows.Single().Id, Is.EqualTo("foreign"));
        }
        [TestCase("missing")][TestCase("report-istanbul-city-condition")][TestCase("foreign")]
        public void UnknownOrUnavailableSelectionDoesNotSilentlyFallBack(string id)
        {
            var c = Campaign(); Add(c); Add(c, "foreign", recipient: Foreign);
            using var s = new ReportInboxSession(c, Viewer()); s.OpenReport(id); var r = s.Read();
            Assert.That(r.SelectedId, Is.EqualTo(id)); Assert.That(r.Detail, Is.Null); Assert.That(r.Rows.Single().Id, Is.EqualTo("delivered"));
        }
        [TestCase(ObservationPrecision.Exact, 0L, null, "Kesin · 0")]
        [TestCase(ObservationPrecision.Approximate, 15L, null, "Yaklaşık · yaklaşık 15")]
        [TestCase(ObservationPrecision.Range, 10L, 20L, "Aralık · 10–20")]
        [TestCase(ObservationPrecision.Qualitative, null, null, "Nitel · Değişmiyor")]
        [TestCase(ObservationPrecision.Unknown, null, null, "Bilinmiyor (sıfır değildir)")]
        public void ObservationPrecisionIsPreservedAndUnknownIsNotZero(ObservationPrecision precision, long? lower, long? upper, string expected)
        {
            var c = Campaign(); Add(c, precision: precision, lower: lower, upper: upper);
            using var s = new ReportInboxSession(c, Viewer()); var detail = s.Read().Detail!;
            Assert.That(detail.Observations.Single(), Does.Contain(expected)); Assert.That(detail.Metadata, Does.Contain("Kalite: Düşük"));
        }
        [Test]
        public void MixedPrecisionsAreNotLabeledAsOneExactObservation()
        {
            var c = Campaign(); var report = new ReportState(ReportId.Create("mixed"), ReportType.City,
                ReportSourceRef.Character(ReportSourceKind.Official, CharacterId.Create("yusuf-katip")), Own, ReportQuality.High, ReportDetailLevel.Standard,
                new WorldTimestamp(0), new[] {
                    new ReportObservation(new ReportSubjectRef(ReportSubjectKind.City, "city-istanbul"), ReportObservationKind.CityCondition, ObservationPrecision.Exact, 9),
                    new ReportObservation(new ReportSubjectRef(ReportSubjectKind.City, "city-istanbul"), ReportObservationKind.SecurityCondition, ObservationPrecision.Unknown) },
                CommunicationStatus.Delivered, new WorldTimestamp(0), new WorldTimestamp(2));
            c.Diplomacy.Reports.Add(report); c.Diplomacy.Information.GetRequired(Own).RestoreAvailable(report); c.Clock.Advance(new WorldDuration(2));
            using var s = new ReportInboxSession(c, Viewer()); Assert.That(s.Read().Detail!.Metadata, Does.Contain("Karışık"));
            Assert.That(s.Read().Detail!.Observations.Count, Is.EqualTo(2));
        }
        [Test]
        public void AllFiltersHaveExplicitMatchingReportTypes()
        {
            var c = Campaign(); foreach (ReportType type in Enum.GetValues(typeof(ReportType))) Add(c, "type-" + type, type);
            using var s = new ReportInboxSession(c, Viewer()); Assert.That(s.Read().Rows.Count, Is.EqualTo(6));
            for (var i = 1; i <= 6; i++) { s.Filter("", i); Assert.That(s.Read().Rows.Single().TypeIndex, Is.EqualTo(i)); }
        }
        [TestCase("İSTANBUL", 0)][TestCase("istanbul", 0)][TestCase("ŞEHİR", 1)][TestCase("şehir", 1)][TestCase("görevlİ", 0)]
        public void TurkishSearchIsStableOnDesktopAndUnity(string search, int filter)
        {
            var c = Campaign(); Add(c, "istanbul-report"); using var s = new ReportInboxSession(c, Viewer()); s.Filter(search, filter);
            Assert.That(s.Read().Rows.Single().Id, Is.EqualTo("istanbul-report"));
        }
        [Test]
        public void SearchCannotFindForeignOrUndeliveredMetadataAndNoMatchIsNotNoDelivery()
        {
            var c = Campaign(); Add(c); Add(c, "secret-foreign", recipient: Foreign);
            using var s = new ReportInboxSession(c, Viewer()); s.Filter("secret", 0); var r = s.Read();
            Assert.That(r.Rows, Is.Empty); Assert.That(r.TotalDelivered, Is.EqualTo(1)); Assert.That(r.Detail, Is.Null);
            Assert.That(r.EmptyMessage, Does.Contain("filtre"));
        }
        [Test]
        public void RefreshOfExplicitRouteDoesNotRestoreFilteredOutDetail()
        {
            var c = Campaign(); Add(c); using var s = new ReportInboxSession(c, Viewer());
            s.Filter("", 6); s.OpenReport("delivered"); Assert.That(s.Read().Rows, Is.Empty); Assert.That(s.Read().Detail, Is.Null);
        }
        [Test]
        public void SortUsesDeliveryThenOrdinalIdRatherThanObservationTimeOrInsertionOrder()
        {
            var c = Campaign(); Add(c, "z", observed: 2, arrived: 3); Add(c, "b", observed: 0, arrived: 10); Add(c, "a", observed: 1, arrived: 10);
            using var s = new ReportInboxSession(c, Viewer()); Assert.That(s.Read().Rows.Select(x => x.Id), Is.EqualTo(new[] { "a", "b", "z" }));
        }
        [Test]
        public void StalenessUsesObservationNotArrivalAndRefreshUsesCampaignTimeOnly()
        {
            var c = Campaign(); Add(c, observed: 2, arrived: 10); using var s = new ReportInboxSession(c, Viewer()); var old = s.Read();
            Assert.That(old.Rows.Single().Age, Is.EqualTo(8)); c.Clock.Advance(new WorldDuration(60));
            Assert.That(s.Read().Rows.Single().Age, Is.EqualTo(68)); Assert.That(old.Rows.Single().Age, Is.EqualTo(8));
            c.Clock.Pause(); c.Clock.Advance(new WorldDuration(3600)); Assert.That(s.Read().Rows.Single().Age, Is.EqualTo(68));
        }
        [Test]
        public void ReportReferencesAndValuesAreNeverEnrichedFromCurrentWorldTruth()
        {
            var c = Campaign(); Add(c, precision: ObservationPrecision.Exact, lower: 7); using var s = new ReportInboxSession(c, Viewer()); var before = s.Read().Detail!;
            c.Economy.GetRequiredMarket(CityId.Create("city-istanbul")).Stock.Add(TradeGoodId.Create("grain"), 40);
            var after = s.Read().Detail!; Assert.That(after.Metadata, Is.EqualTo(before.Metadata)); Assert.That(after.Observations, Is.EqualTo(before.Observations));
            Assert.That(after.Metadata, Does.Contain("yusuf-katip")); Assert.That(after.Metadata, Does.Contain("güncel dünya gerçeği değildir"));
        }
        [Test]
        public void RepeatedSelectionsAndFiltersPreserveEntireSaveAndRng()
        {
            var c = Campaign(); Add(c); var before = State(c); using var s = new ReportInboxSession(c, Viewer());
            for (var i = 0; i < 100; i++) { s.Filter(i % 2 == 0 ? "ŞEHİR" : "", i % 7); s.Read(); s.OpenReport("missing"); s.Read(); s.OpenReport("delivered"); s.Read(); }
            Assert.That(State(c), Is.EqualTo(before)); Assert.That(CampaignSaveMapper.ToSaveData(c).SaveVersion, Is.EqualTo(14));
        }
        [Test]
        public void SaveRoundtripRebindsInboxAndPreservesDatedDetailWithoutPersistingUiFilter()
        {
            var c = Campaign(); Add(c); var serializer = new CampaignSaveTextSerializer(); var save = serializer.Deserialize(State(c));
            Assert.That(new CampaignSaveValidator().Validate(save.Data!).IsValid, Is.True);
            var restored = CampaignSaveMapper.ToRuntimeState(save.Data!); using var a = new ReportInboxSession(c, Viewer()); using var b = new ReportInboxSession(restored, Viewer());
            a.Filter("CITY-NOT-A-MATCH", 6); Assert.That(b.Read().FilterIndex, Is.Zero); Assert.That(b.Read().Search, Is.Empty);
            a.Filter("", 0); Assert.That(b.Read().Detail!.Metadata, Is.EqualTo(a.Read().Detail!.Metadata));
            Assert.That(State(restored), Is.EqualTo(State(c)));
        }
        [Test]
        public void MissingActorInformationIsEmptyAndNeverFallsBackToAnotherActor()
        {
            var c = Campaign(); Add(c); using var s = new ReportInboxSession(c, Viewer(FactionId.Create("missing-actor")));
            Assert.That(s.Read().Rows, Is.Empty); Assert.That(s.Read().Detail, Is.Null);
        }
        [Test]
        public void SnapshotsExposeNoDomainStateOrUnityTypes()
        {
            foreach (var t in new[] { typeof(ReportInboxSnapshot), typeof(ReportInboxDetail), typeof(ReportInboxRow) })
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                Assert.That(p.PropertyType.FullName, Does.Not.Contain("FOC.Domain").And.Not.Contain("UnityEngine"));
        }
        [TestCase(-1)][TestCase(7)]
        public void InvalidFilterIsRejectedWithoutChangingState(int filter)
        { using var s = new ReportInboxSession(Campaign(), Viewer()); Assert.Throws<ArgumentOutOfRangeException>(() => s.Filter("", filter)); Assert.That(s.Read().FilterIndex, Is.Zero); }
        [Test]
        public void DisposedSessionCannotBeReadOrChanged()
        {
            var s = new ReportInboxSession(Campaign(), Viewer()); s.Dispose(); s.Dispose();
            Assert.Throws<ObjectDisposedException>(() => s.Read()); Assert.Throws<ObjectDisposedException>(() => s.Filter("", 0)); Assert.Throws<ObjectDisposedException>(() => s.OpenReport("x"));
        }
    }
}
