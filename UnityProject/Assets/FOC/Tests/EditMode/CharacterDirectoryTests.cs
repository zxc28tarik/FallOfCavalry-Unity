#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Application.Save;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Infrastructure.Save;
using FOC.Presentation.Core;
using NUnit.Framework;

namespace FOC.Tests
{
    public sealed class CharacterDirectoryTests
    {
        private static FOC.Domain.Campaign.CampaignRuntimeState Campaign() => PlayableTradeTests.Campaign();
        private static PresentationViewerContext Viewer(params string[] known) => new PresentationViewerContext(FactionId.Create("faction-ottoman-state"),
            new PresentationEntityRef(PresentationEntityKind.Character, "hasan-aga"), known.Select(x => new PresentationEntityRef(PresentationEntityKind.Character, x)));
        private static string Payload(FOC.Domain.Campaign.CampaignRuntimeState c) => new CampaignSaveTextSerializer().Serialize(CampaignSaveMapper.ToSaveData(c));

        [Test] public void ListsOnlyExplicitlyKnownCharactersInStableNameOrder()
        {
            var c = Campaign(); using var s = new CharacterDirectorySession(c, Viewer("ali-cavus", "mehmed-celebi-tacir"));
            var snapshot = s.Read(); Assert.That(snapshot.TotalKnown, Is.EqualTo(3));
            Assert.That(snapshot.Rows.Select(x => x.Name), Is.Ordered.Using<string>(StringComparer.Ordinal));
            Assert.That(snapshot.Rows.Select(x => x.Id), Does.Contain("hasan-aga").And.Contain("ali-cavus").And.Contain("mehmed-celebi-tacir"));
            Assert.That(snapshot.Rows.Any(x => x.Id == "yusuf-katip"), Is.False);
        }

        [Test] public void TurkishSearchAndExplicitUnknownSelectionDoNotSubstituteOrLeak()
        {
            var c = Campaign(); using var s = new CharacterDirectorySession(c, Viewer("ali-cavus", "mehmed-celebi-tacir"));
            s.Filter("ÇAVUŞ"); Assert.That(s.Read().Rows.Single().Id, Is.EqualTo("ali-cavus"));
            s.OpenCharacter("yusuf-katip"); var hidden = s.Read(); Assert.That(hidden.SelectedId, Is.EqualTo("yusuf-katip")); Assert.That(hidden.Summary, Does.Contain("erişilemiyor"));
            Assert.That(hidden.Summary, Does.Not.Contain(c.Characters.GetRequired(CharacterId.Create("yusuf-katip")).Definition.DisplayName));
        }

        [Test] public void DetailShowsConcreteLocationHealthAndIdentityWithoutMutation()
        {
            var c = Campaign(); var before = Payload(c); using var s = new CharacterDirectorySession(c, Viewer("ali-cavus"));
            s.OpenCharacter("hasan-aga"); var snapshot = s.Read();
            Assert.That(snapshot.Summary, Does.Contain("Hasan Ağa").And.Contain("Şehir:").And.Contain("Yaralanma yok").And.Contain("Esir değil").And.Contain("Kimlik:"));
            var screen = new CampaignPresentationQueries(c).BuildCharacter(Viewer("ali-cavus"), CharacterId.Create("hasan-aga")).State;
            Assert.That(screen.Current.Fields.Single(x => x.LabelKey == "presentation.character.location").DisplayValue, Does.StartWith("Şehir:"));
            Assert.That(Payload(c), Is.EqualTo(before));
        }

        [Test] public void HiddenRelationIsNotExposedThroughKnownCharacterDetail()
        {
            var c = Campaign(); var viewer = Viewer(); var state = new CampaignPresentationQueries(c).BuildCharacter(viewer, CharacterId.Create("hasan-aga")).State;
            var relations = state.Details.Single(x => x.HeadingKey == "presentation.character.relations");
            Assert.That(relations.Fields.All(x => x.Link.HasValue && viewer.CanReadExact(x.Link.Value)), Is.True);
        }

        [Test] public void SnapshotIsFrozenAndSaveRoundtripPreservesProjection()
        {
            var c = Campaign(); var viewer = Viewer("ali-cavus", "mehmed-celebi-tacir"); using var s = new CharacterDirectorySession(c, viewer); var first = s.Read();
            Assert.Throws<NotSupportedException>(() => ((IList<CharacterDirectoryRow>)first.Rows).Clear());
            var serializer = new CampaignSaveTextSerializer(); var data = serializer.Deserialize(Payload(c)).Data!;
            Assert.That(new CampaignSaveValidator().Validate(data).IsValid, Is.True); using var loaded = new CharacterDirectorySession(CampaignSaveMapper.ToRuntimeState(data), viewer);
            Assert.That(loaded.Read().Rows.Select(x => x.Text), Is.EqualTo(first.Rows.Select(x => x.Text)));
        }

        [Test] public void DisposalIsEnforced()
        {
            var s = new CharacterDirectorySession(Campaign(), Viewer()); s.Dispose();
            Assert.Throws<ObjectDisposedException>(() => s.Read()); Assert.Throws<ObjectDisposedException>(() => s.Filter("x"));
        }
    }
}
