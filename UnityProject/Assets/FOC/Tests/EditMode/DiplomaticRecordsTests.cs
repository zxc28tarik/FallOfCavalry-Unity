#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Application.Save;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Diplomacy;
using FOC.Domain.Time;
using FOC.Infrastructure.Save;
using FOC.Presentation.Core;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class DiplomaticRecordsTests
    {
        private static readonly FactionId Own = FactionId.Create("faction-ottoman-state"), Venice = FactionId.Create("faction-republic-of-venice"), Other = FactionId.Create("s3-foreign-actor");
        private static WorldTimestamp T(long at) => new WorldTimestamp(at);
        private static CampaignRuntimeState Campaign() => PlayableTradeTests.Campaign();
        private static PresentationViewerContext Viewer(bool debug = false) => new PresentationViewerContext(Own, new PresentationEntityRef(PresentationEntityKind.Character, "hasan-aga"),
            new[] { new PresentationEntityRef(PresentationEntityKind.DiplomaticAgreement, "secret") }, debug);
        private static string Payload(CampaignRuntimeState c) => new CampaignSaveTextSerializer().Serialize(CampaignSaveMapper.ToSaveData(c));
        private static DiplomaticAgreementState Add(CampaignRuntimeState c, string id = "s3-trade", long signed = 0, long effective = 0, long? expires = null, DiplomaticAgreementStatus status = DiplomaticAgreementStatus.Active, bool foreign = false)
        {
            var agreement = new DiplomaticAgreementState(DiplomaticAgreementId.Create(id), new DiplomaticActorPair(foreign ? Other : Own, Venice), DiplomaticAgreementKind.TradeAgreement,
                T(signed), T(effective), new[] { new DiplomaticAgreementTerm(DiplomaticAgreementTermKind.MarketAccess), new DiplomaticAgreementTerm(DiplomaticAgreementTermKind.CaravanProtection) },
                expires.HasValue ? T(expires.Value) : (WorldTimestamp?)null, status);
            c.Diplomacy.Agreements.Add(agreement); return agreement;
        }
        private static string Visible(DiplomaticRecordsSession s)
        {
            var x = s.Read(); return x.TotalKnown + "\n" + string.Join("\n", x.Rows.Select(r => r.Subject + " " + r.Text)) + "\n" + x.Detail?.Metadata + "\n" + string.Join("\n", x.Detail?.Entries ?? Array.Empty<string>());
        }
        [Test] public void HistoricalStartupShowsExistingRelationAndNoInventedTreaty()
        {
            var c = Campaign(); var before = Payload(c); using var s = new DiplomaticRecordsSession(c, Viewer());
            Assert.That(s.Read().Rows.Single().TypeIndex, Is.EqualTo(1)); Assert.That(s.Read().Detail!.Metadata, Does.Contain("Gergin"));
            s.Filter("", 2); Assert.That(s.Read().Rows, Is.Empty); Assert.That(s.Read().Detail, Is.Null); Assert.That(Payload(c), Is.EqualTo(before));
        }
        [TestCase(false)][TestCase(true)] public void ForeignRecordsAndExactDebugDoNotGrantGlobalKnowledge(bool debug)
        {
            var c = Campaign(); c.Diplomacy.Actors.Add(new DiplomaticActorState(Other, "Hidden name"));
            var hidden = new DiplomaticRelationState(new DiplomaticActorPair(Other, Venice), DiplomaticDisposition.Hostile, T(0)); c.Diplomacy.Relations.Add(hidden); Add(c, "secret", foreign: true);
            using var s = new DiplomaticRecordsSession(c, Viewer(debug)); var before = Visible(s);
            hidden.SetDisposition(DiplomaticDisposition.Trusted, T(0)); hidden.AddFactor(new DiplomaticFactor("secret-factor", DiplomaticFactorSource.Insult, DiplomaticFactorDirection.Negative, T(0)));
            Assert.That(Visible(s), Is.EqualTo(before)); Assert.That(s.Read().TotalKnown, Is.EqualTo(1));
            s.OpenRecord(new PresentationEntityRef(PresentationEntityKind.DiplomaticAgreement, "secret")); Assert.That(s.Read().Detail, Is.Null);
            s.Filter("secret", 0); Assert.That(s.Read().Rows, Is.Empty);
        }
        [TestCase(PresentationEntityKind.DiplomaticRelation)][TestCase(PresentationEntityKind.DiplomaticAgreement)][TestCase(PresentationEntityKind.EnvoyMission)]
        public void WrongOrMissingTypedSelectionDoesNotFallBack(PresentationEntityKind kind)
        {
            var c = Campaign(); var own = c.Diplomacy.Relations.OrderedRelations.Single(); Add(c, own.Id.Value);
            using var s = new DiplomaticRecordsSession(c, Viewer()); s.OpenRecord(new PresentationEntityRef(kind, "missing")); Assert.That(s.Read().Detail, Is.Null);
            s.OpenRecord(new PresentationEntityRef(PresentationEntityKind.EnvoyMission, own.Id.Value)); Assert.That(s.Read().Detail, Is.Null);
            s.OpenRecord(new PresentationEntityRef(PresentationEntityKind.DiplomaticAgreement, own.Id.Value)); Assert.That(s.Read().Detail!.Subject.Kind, Is.EqualTo(PresentationEntityKind.DiplomaticAgreement));
            s.OpenRecord(new PresentationEntityRef(PresentationEntityKind.DiplomaticRelation, own.Id.Value)); Assert.That(s.Read().Detail!.Subject.Kind, Is.EqualTo(PresentationEntityKind.DiplomaticRelation));
        }
        [Test] public void FutureSignedAndRelationUpdatedRecordsAreNotPresentKnowledge()
        {
            var c = Campaign(); Add(c, signed: 10, effective: 20); c.Diplomacy.Relations.OrderedRelations.Single().SetDisposition(DiplomaticDisposition.Trusted, T(10));
            using var s = new DiplomaticRecordsSession(c, Viewer()); Assert.That(s.Read().TotalKnown, Is.Zero);
            Assert.That(new CampaignPresentationQueries(c).BuildDiplomacy(Viewer()).State.Details.Take(2).SelectMany(x => x.Fields), Is.Empty);
        }
        [Test] public void FactorChronologySourceDirectionAndFutureBoundaryArePreserved()
        {
            var c = Campaign(); c.Clock.Advance(new WorldDuration(5)); var r = c.Diplomacy.Relations.OrderedRelations.Single();
            r.AddFactor(new DiplomaticFactor("z-ref", DiplomaticFactorSource.Aid, DiplomaticFactorDirection.Positive, T(4)));
            r.AddFactor(new DiplomaticFactor("a-ref", DiplomaticFactorSource.Insult, DiplomaticFactorDirection.Negative, T(4)));
            r.AddFactor(new DiplomaticFactor("older", DiplomaticFactorSource.Event, DiplomaticFactorDirection.Neutral, T(1)));
            r.AddFactor(new DiplomaticFactor("future", DiplomaticFactorSource.Treaty, DiplomaticFactorDirection.Positive, T(6)));
            using var s = new DiplomaticRecordsSession(c, Viewer()); var entries = s.Read().Detail!.Entries;
            Assert.That(entries.Count, Is.EqualTo(3)); Assert.That(entries[0], Does.Contain("a-ref").And.Contain("Olumsuz"));
            Assert.That(entries[1], Does.Contain("z-ref").And.Contain("Olumlu")); Assert.That(entries[2], Does.Contain("older"));
            Assert.That(new CampaignPresentationQueries(c).BuildDiplomacy(Viewer()).State.Details[1].Fields.Count, Is.EqualTo(3));
        }
        [TestCase(0, 10, null, DiplomaticAgreementStatus.Active, false)]
        [TestCase(0, 0, 5L, DiplomaticAgreementStatus.Active, true)]
        [TestCase(5, 0, 5L, DiplomaticAgreementStatus.Active, false)]
        [TestCase(0, 0, null, DiplomaticAgreementStatus.Expired, false)]
        [TestCase(0, 0, null, DiplomaticAgreementStatus.Terminated, false)]
        public void RegisteredStatusAndClockEffectivenessAreDistinct(long now, long effective, long? expires, DiplomaticAgreementStatus status, bool active)
        {
            var c = Campaign(); c.Clock.Advance(new WorldDuration(now)); Add(c, effective: effective, expires: expires, status: status);
            using var s = new DiplomaticRecordsSession(c, Viewer()); s.Filter("", 2);
            Assert.That(s.Read().Detail!.Metadata, Does.Contain("Kayıt durumu: " + status + " (kayıt)"));
            Assert.That(s.Read().Detail!.Metadata, Does.Contain("Şu anda yürürlükte: " + (active ? "Evet" : "Hayır")));
            Assert.That(s.Read().Detail!.Entries, Has.Count.EqualTo(2));
        }
        [Test] public void SearchUsesTurkishLabelsAndClearFiltersReleasePreviousDetail()
        {
            var c = Campaign(); Add(c); using var s = new DiplomaticRecordsSession(c, Viewer());
            s.Filter("TİCARET", 2); Assert.That(s.Read().Rows.Count, Is.EqualTo(1)); Assert.That(s.Read().Detail, Is.Not.Null);
            s.Filter("İLİŞKİ", 1); Assert.That(s.Read().Rows.Count, Is.EqualTo(1)); Assert.That(s.Read().Detail!.Subject.Kind, Is.EqualTo(PresentationEntityKind.DiplomaticRelation));
            s.Filter("does-not-exist", 0); Assert.That(s.Read().Rows, Is.Empty); Assert.That(s.Read().Detail, Is.Null);
        }
        [Test] public void DeterministicNewestThenTypedOrdinalOrdering()
        {
            var c = Campaign(); c.Clock.Advance(new WorldDuration(1)); Add(c, "b", signed: 1, effective: 1); Add(c, "a", signed: 1, effective: 1);
            using var s = new DiplomaticRecordsSession(c, Viewer()); Assert.That(s.Read().Rows.Select(x => x.Subject.Id).Take(2), Is.EqualTo(new[] { "a", "b" }));
        }
        [Test] public void ReadFilterAndLoadPreserveFullV14CampaignIncludingRandomTimeAndTerms()
        {
            var c = Campaign(); Add(c); var before = Payload(c); using var s = new DiplomaticRecordsSession(c, Viewer()); var projection = Visible(s);
            for (var i = 0; i < 100; i++) { s.Filter("", i % 3); s.Read(); }
            Assert.That(Payload(c), Is.EqualTo(before)); var serializer = new CampaignSaveTextSerializer(); var data = serializer.Deserialize(before).Data!;
            Assert.That(data.SaveVersion, Is.EqualTo(14)); Assert.That(new CampaignSaveValidator().Validate(data).IsValid, Is.True);
            using var loaded = new DiplomaticRecordsSession(CampaignSaveMapper.ToRuntimeState(data), Viewer()); Assert.That(Visible(loaded), Is.EqualTo(projection));
        }
        [Test] public void SnapshotsCannotMutateRuntimeAndRemainFrozenAfterChange()
        {
            var c = Campaign(); using var s = new DiplomaticRecordsSession(c, Viewer()); var first = s.Read();
            Assert.Throws<NotSupportedException>(() => ((IList<DiplomaticRecordRow>)first.Rows).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<string>)first.Detail!.Entries).Clear());
            c.Diplomacy.Relations.OrderedRelations.Single().SetDisposition(DiplomaticDisposition.Trusted, T(0));
            Assert.That(first.Detail!.Metadata, Does.Contain("Gergin")); Assert.That(s.Read().Detail!.Metadata, Does.Contain("Güvenilir"));
        }
        [Test] public void NoReligionOrReportInferredFactorOrTreaty()
        {
            var c = Campaign(); var before = Payload(c); using var s = new DiplomaticRecordsSession(c, Viewer());
            Assert.That(s.Read().Detail!.Entries.Single(), Does.Contain("etken kaydı yok"));
            Assert.That(s.Read().Rows.Count, Is.EqualTo(1)); Assert.That(Payload(c), Is.EqualTo(before));
        }
        [Test] public void DeliveredForeignDiplomaticObservationRemainsReportNotLiveRelationKnowledge()
        {
            var c = Campaign(); c.Diplomacy.Actors.Add(new DiplomaticActorState(Other, "Foreign"));
            var hidden = new DiplomaticRelationState(new DiplomaticActorPair(Other, Venice), DiplomaticDisposition.Hostile, T(0)); c.Diplomacy.Relations.Add(hidden);
            var report = new ReportState(ReportId.Create("s3-dated-observation"), ReportType.Diplomatic,
                ReportSourceRef.Character(ReportSourceKind.DiplomaticContact, CharacterId.Create("yusuf-katip")), Own, ReportQuality.High, ReportDetailLevel.Standard, T(0),
                new[] { new ReportObservation(new ReportSubjectRef(ReportSubjectKind.Diplomacy, hidden.Id.Value), ReportObservationKind.DiplomaticDisposition,
                    ObservationPrecision.Qualitative, qualitative: QualitativeObservation.Improving) }, CommunicationStatus.Delivered, T(0), T(2));
            c.Diplomacy.Reports.Add(report); c.Diplomacy.Information.GetRequired(Own).RestoreAvailable(report); c.Clock.Advance(new WorldDuration(2));
            var before = Payload(c); using var s = new DiplomaticRecordsSession(c, Viewer()); using var inbox = new ReportInboxSession(c, Viewer());
            Assert.That(inbox.Read().Rows.Single().Id, Is.EqualTo(report.Id.Value)); Assert.That(s.Read().TotalKnown, Is.EqualTo(1));
            s.OpenRecord(new PresentationEntityRef(PresentationEntityKind.DiplomaticRelation, hidden.Id.Value)); Assert.That(s.Read().Detail, Is.Null);
            Assert.That(Payload(c), Is.EqualTo(before));
        }
        [Test] public void DisposalAndFilterBoundsAreEnforced()
        {
            var s = new DiplomaticRecordsSession(Campaign(), Viewer()); Assert.Throws<ArgumentOutOfRangeException>(() => s.Filter("", -1)); Assert.Throws<ArgumentOutOfRangeException>(() => s.Filter("", 3));
            s.Dispose(); Assert.Throws<ObjectDisposedException>(() => s.Read()); Assert.Throws<ObjectDisposedException>(() => s.Filter("", 0));
        }
    }
}
