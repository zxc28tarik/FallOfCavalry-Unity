#nullable enable
using System;
using System.Linq;
using FOC.Application.Geography;
using FOC.Application.HistoricalContent;
using FOC.Application.Save;
using FOC.Domain.Common;
using FOC.Editor.Visuals;
using FOC.Infrastructure.Save;
using FOC.Presentation.Visuals;
using FOC.Visuals.Core;
using UnityEditor;
using UnityEngine;

namespace FOC.Editor.Integration
{
    public static class HistoricalContentPipelineBatch
    {
        public static void GenerateAssets()
        {
            try{VisualProofAssetGenerator.GenerateAll();Debug.Log("FOC_HISTORICAL_CONTENT_ASSETS_PASS");EditorApplication.Exit(0);}
            catch(Exception exception){Debug.LogException(exception);EditorApplication.Exit(1);}
        }

        public static void Run()
        {
            try
            {
                var locations=Load("FOC/Geography/vertical-slice-locations");var routes=Load("FOC/Geography/vertical-slice-routes");var historical=Load("FOC/HistoricalSlice/historical-slice-content");
                var campaign=VerticalSliceCampaignFactory.Create(locations.text,routes.text,historical.text);var validation=new HistoricalCampaignValidator().Validate(campaign);if(!validation.IsValid)throw new InvalidOperationException(string.Join("; ",validation.Issues.Select(x=>x.Code+":"+x.Message)));
                var serializer=new CampaignSaveTextSerializer();var first=serializer.Serialize(CampaignSaveMapper.ToSaveData(campaign));var read=serializer.Deserialize(first);if(!read.Success||read.Data==null)throw new InvalidOperationException(read.Error);var restored=CampaignSaveMapper.ToRuntimeState(read.Data);var second=serializer.Serialize(CampaignSaveMapper.ToSaveData(restored));if(!StringComparer.Ordinal.Equals(first,second))throw new InvalidOperationException("Historical save roundtrip drifted.");
                const string catalogPath="Assets/FOC/Generated/VisualProof/Catalogs/FOC_VisualCatalog.asset";var asset=AssetDatabase.LoadAssetAtPath<VisualCatalogAsset>(catalogPath)??throw new InvalidOperationException("Visual catalog was not generated.");var catalog=asset.BuildCoreCatalog();var visualValidation=catalog.Validate();if(!visualValidation.IsValid)throw new InvalidOperationException(string.Join("; ",visualValidation.Errors));
                foreach(var id in new[]{"sipahi","cebeli","tufekci"})catalog.GetProfile(VisualProfileId.Create(id));
                var soldier=campaign.Soldiers.Soldiers.OrderedSoldiers.First();var troop=campaign.Soldiers.Definitions.GetRequired(soldier.TroopDefinitionId);var equipment=campaign.Soldiers.Equipment.OrderedEquipment.ToDictionary(x=>x.Id,x=>x);var plan=new VisualSoldierPlanner(catalog).Plan(soldier,troop,equipment);if(plan.Modules.Count<3)throw new InvalidOperationException("Historical Soldier visual plan is incomplete.");
                Debug.Log("FOC_HISTORICAL_CONTENT_PIPELINE_PASS anchor="+VerticalSliceCampaignFactory.HistoricalAnchorDate+" characters="+campaign.Characters.OrderedCharacters.Count+" cities="+campaign.Cities.Count+" goods="+campaign.Economy.Goods.OrderedGoods.Count+" soldiers="+campaign.Soldiers.Soldiers.OrderedSoldiers.Count+" visualModules="+plan.Modules.Count+" saveVersion="+CampaignSaveData.CurrentSaveVersion);EditorApplication.Exit(0);
            }
            catch(Exception exception){Debug.LogException(exception);EditorApplication.Exit(1);}
        }
        private static TextAsset Load(string path)=>Resources.Load<TextAsset>(path)??throw new InvalidOperationException("TextAsset import failed: "+path);
    }
}
