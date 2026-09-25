using System;
using FOC.Application.Geography;
using FOC.Application.Save;
using FOC.Domain.Geography;
using FOC.Domain.Validation;
using FOC.Infrastructure.Save;
using FOC.Presentation.Core;
using UnityEditor;
using UnityEngine;

namespace FOC.Editor.Integration
{
    public static class WorldMapPipelineBatch
    {
        public static void Run()
        {
            try
            {
                var locations=Resources.Load<TextAsset>("FOC/Geography/vertical-slice-locations")??throw new InvalidOperationException("Location TextAsset import failed.");var routes=Resources.Load<TextAsset>("FOC/Geography/vertical-slice-routes")??throw new InvalidOperationException("Route TextAsset import failed.");var sources=Resources.Load<TextAsset>("FOC/Geography/geography-sources")??throw new InvalidOperationException("Source-provenance TextAsset import failed.");var art=Resources.Load<Texture2D>("FOC/Geography/MarmaraStrategyMap")??throw new InvalidOperationException("Map art import failed.");
                var campaign=VerticalSliceCampaignFactory.Create(locations.text,routes.text);if(!new GeographyInvariantValidator().Validate(campaign).IsValid)throw new InvalidOperationException("Runtime geography invalid.");var save=CampaignSaveMapper.ToSaveData(campaign);if(!new CampaignSaveValidator().Validate(save).IsValid)throw new InvalidOperationException("Save geography invalid.");var serializer=new CampaignSaveTextSerializer();var read=serializer.Deserialize(serializer.Serialize(save));if(!read.Success||read.Data==null)throw new InvalidOperationException(read.Error);CampaignSaveMapper.ToRuntimeState(read.Data);
                var map=new WorldMapPresentationDataProvider(campaign);if(map.GetKnownMarkers(new PresentationViewerContext(FOC.Domain.Common.FactionId.Create("faction-proof"),new PresentationEntityRef(PresentationEntityKind.Character,"slice-player-sipahi"))).Count<12)throw new InvalidOperationException("Map marker projection failed.");
                Debug.Log("FOC_WORLD_MAP_PIPELINE_PASS locations="+campaign.Geography.World.LocationCount+" routes="+campaign.Geography.World.RouteCount+" sourcesBytes="+sources.bytes.Length+" art="+art.width+"x"+art.height);EditorApplication.Exit(0);
            }
            catch(Exception exception){Debug.LogException(exception);EditorApplication.Exit(1);}
        }
    }
}
