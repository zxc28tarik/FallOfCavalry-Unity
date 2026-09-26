using System.IO;
using System.Linq;
using FOC.Editor.Visuals;
using NUnit.Framework;
using UnityEngine;

namespace FOC.Tests.VisualPipeline
{
    public sealed class ClothingDonorTests
    {
        private const string Root="Assets/FOC/ArtSource/HistoricalSlice/DonorTests";
        private static readonly string[] Selected={"Male_Peasant_Body","Male_Peasant_Arms","Male_Peasant_Legs","Male_Ranger_Body","Male_Ranger_Arms","Male_Ranger_Legs","Male_Ranger_Feet_Boots","toigo_harem_pants","culturalibre_male_boots","donitz_monk_robe","rehmanpolanski_viking_tunic"};
        [Test]
        public void DonorInventoryIsExactlyTheElevenOperatorSelections()
        {
            var actual=Directory.GetFiles(Root,"CLTH_Donor_*.focmesh.json").Select(p=>Path.GetFileName(p).Replace("CLTH_Donor_","").Replace(".focmesh.json","")).ToArray();
            CollectionAssert.AreEquivalent(Selected,actual);
        }
        [TestCaseSource(nameof(Selected))]
        public void DonorPreservesCanonicalRigAndRemainsDraft(string donor)
        {
            var source=Read(Root+"/CLTH_Donor_"+donor+".focmesh.json");
            var body=Read("Assets/FOC/ArtSource/HistoricalSlice/Characters/BODY_OttomanMale_Standard.focmesh.json");
            Assert.That(source.status,Is.EqualTo("Draft"));
            Assert.That(source.source,Does.Contain("Upstream/ClothingDonors/"));
            Assert.That(source.sourceSha256.Length,Is.EqualTo(64));
            HistoricalArtCandidatePipeline.ValidateSource(source);
            Assert.That(source.bones.Length,Is.EqualTo(body.bones.Length));
            for(var i=0;i<body.bones.Length;i++)
            {
                Assert.That(source.bones[i].name,Is.EqualTo(body.bones[i].name));
                Assert.That(source.bones[i].parent,Is.EqualTo(body.bones[i].parent));
                CollectionAssert.AreEqual(body.bones[i].position,source.bones[i].position);
            }
        }
        [TestCase("Male_Ranger_Body",2998)]
        [TestCase("Male_Ranger_Arms",4928)]
        public void RangerSelectionIsMainGarmentNotAuxiliaryBeltOrBracer(string donor,int triangles)
        {
            Assert.That(Read(Root+"/CLTH_Donor_"+donor+".focmesh.json").lods[0].parts.Sum(p=>p.triangles.Length/3),Is.EqualTo(triangles));
        }
        [Test]
        public void DonorExperimentsAreNotActivatedInProductionCatalog()
        {
            Assert.That(File.ReadAllText("Assets/FOC/Content/Resources/FOC/Visuals/FOC_VisualCatalog.asset"),Does.Not.Contain("Donor_"));
        }
        private static HistoricalArtCandidatePipeline.SourceAsset Read(string path)=>JsonUtility.FromJson<HistoricalArtCandidatePipeline.SourceAsset>(File.ReadAllText(path));
    }
}
