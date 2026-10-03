#nullable enable
using System;
using System.Linq;
using UnityEditor;
using FOC.Visuals.Core;

namespace FOC.Editor.Visuals
{
    public sealed class VisualModelImportPostprocessor:AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            // Isolated foreign-rig pilot owns its import settings; do not apply
            // canonical-rig optimization or lossy compression to its source.
            if(assetPath.StartsWith(MeshyHasanPilotPipeline.IntakeRoot+"/",StringComparison.Ordinal))return;
            if(!assetPath.StartsWith("Assets/FOC/ArtSource/",StringComparison.Ordinal))return;var importer=(ModelImporter)assetImporter;var mount=assetPath.IndexOf("/Mounts/",StringComparison.Ordinal)>=0;importer.globalScale=1f;importer.useFileScale=true;importer.importCameras=false;importer.importLights=false;importer.importVisibility=false;importer.importBlendShapes=false;importer.importAnimation=true;importer.animationType=mount?ModelImporterAnimationType.Generic:ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.meshCompression=ModelImporterMeshCompression.Medium;importer.isReadable=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.optimizeGameObjects=true;importer.extraExposedTransformPaths=(mount?new[]{CanonicalRig.RiderSocket}:CanonicalRig.HumanSockets.Values).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        }

        private void OnPreprocessTexture()
        {
            // In particular, the packed metallic/smoothness map is linear data.
            if(assetPath.StartsWith(MeshyHasanPilotPipeline.IntakeRoot+"/",StringComparison.Ordinal))return;
            if(!assetPath.StartsWith("Assets/FOC/ArtSource/",StringComparison.Ordinal))return;var importer=(TextureImporter)assetImporter;var normal=assetPath.IndexOf("_N.",StringComparison.OrdinalIgnoreCase)>=0||assetPath.IndexOf("_Normal.",StringComparison.OrdinalIgnoreCase)>=0;importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=!normal;importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.Compressed;importer.maxTextureSize=assetPath.IndexOf("/Narrative/",StringComparison.Ordinal)>=0?2048:1024;
        }
    }
}
