#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Domain.Common;
using FOC.Domain.Soldiers;
using FOC.Visuals.Core;
using UnityEngine;

namespace FOC.Presentation.Visuals
{
    /// <summary>
    /// Isolated, whole-clothed Meshy integration experiment. A valid Unity Human
    /// Avatar is not certification of the FOC canonical modular rig. This helper
    /// neither edits that contract nor installs a production catalog/profile.
    /// The caller owns the returned transient ScriptableObject.
    /// </summary>
    public static class MeshyHasanPilotCatalog
    {
        public const string CharacterId = "CHR_HasanAga_MeshyPilot";
        public const string ProfileId = "hasan-aga-meshy-pilot";
        public const string UnsupportedCrowdId = "CRWD_MeshyPilot_NotSupported";
        public const string PrefabPath = "Assets/FOC/Presentation/Characters/Ottoman1648/MeshyPilot/CHR_HasanAga_MeshyPilot.prefab";

        public static VisualCatalogAsset Create(GameObject wholeClothedPrefab, VisualCatalogAsset? equipmentCatalog = null)
        {
            if (wholeClothedPrefab == null) throw new ArgumentNullException(nameof(wholeClothedPrefab));
            var animator = wholeClothedPrefab.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException("Meshy pilot requires its own valid Unity Human Avatar; no skeleton substitution is allowed.");
            var renderers = wholeClothedPrefab.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0 || !renderers.OfType<SkinnedMeshRenderer>().Any())
                throw new InvalidOperationException("Meshy pilot must contain actual skinned clothing/body geometry.");
            if (renderers.Any(r => r.sharedMaterials.Length == 0 || r.sharedMaterials.Any(m => m == null)))
                throw new InvalidOperationException("Meshy pilot has missing shared materials.");
            var names = new HashSet<string>(wholeClothedPrefab.GetComponentsInChildren<Transform>(true).Select(t => t.name), StringComparer.Ordinal);
            if (CanonicalRig.HumanSockets.Values.Any(name => !names.Contains(name)))
                throw new InvalidOperationException("Meshy pilot requires additive FOC socket markers on its retained foreign rig.");

            var lods = wholeClothedPrefab.GetComponent<LODGroup>()?.GetLODs();
            var nearRenderers = lods != null && lods.Length > 0 ? lods[0].renderers : renderers;
            if (nearRenderers.Length == 0 || nearRenderers.Any(r => r == null))
                throw new InvalidOperationException("Meshy pilot near representation is empty.");
            var assets = new List<VisualCatalogAsset.AssetEntry>
            {
                new VisualCatalogAsset.AssetEntry
                {
                    id = CharacterId, category = VisualAssetCategory.ConsolidatedCharacter,
                    prefab = wholeClothedPrefab, rigKind = RigKind.Humanoid,
                    lodCount = lods == null || lods.Length == 0 ? 1 : lods.Length,
                    rendererCount = nearRenderers.Length,
                    materialCount = nearRenderers.SelectMany(r => r.sharedMaterials).Distinct().Count(),
                    sockets = CanonicalRig.HumanSockets.Keys.OrderBy(s => s).ToArray()
                }
            };
            var equipment = new List<VisualCatalogAsset.EquipmentEntry>();
            if (equipmentCatalog != null)
            {
                // Only external hand/back/hip accessories are copied. Their
                // attachment is testable, but grip/fit is not art-certified.
                // Body armor cannot replace this fixed suit. Mounted fitting
                // and animation have not been supplied, so mounts fail closed.
                // Omitted mappings fail closed in the existing planner if used.
                var external = equipmentCatalog.Assets.Where(a => IsExternalCategory(a.category)).ToDictionary(a => a.id, StringComparer.Ordinal);
                var wanted = new HashSet<string>(StringComparer.Ordinal);
                foreach (var mapping in equipmentCatalog.Equipment)
                {
                    if (!external.ContainsKey(mapping.visualAssetId)) continue;
                    if (mapping.bodyFamily != "any" && mapping.bodyFamily != "standard-human") continue;
                    equipment.Add(new VisualCatalogAsset.EquipmentEntry
                    {
                        definitionKind = mapping.definitionKind, definitionId = mapping.definitionId,
                        visualAssetId = mapping.visualAssetId, socket = mapping.socket, bodyFamily = mapping.bodyFamily
                    });
                    wanted.Add(mapping.visualAssetId);
                }
                foreach (var id in wanted.OrderBy(x => x, StringComparer.Ordinal))
                {
                    if (id == CharacterId) throw new InvalidOperationException("External equipment collides with the isolated character ID.");
                    var source = external[id];
                    assets.Add(new VisualCatalogAsset.AssetEntry
                    {
                        id = source.id, category = source.category, prefab = source.prefab, rigKind = source.rigKind,
                        lodCount = source.lodCount, rendererCount = source.rendererCount, materialCount = source.materialCount,
                        sockets = source.sockets.ToArray()
                    });
                }
            }
            var catalog = ScriptableObject.CreateInstance<VisualCatalogAsset>();
            catalog.name = "MeshyHasanPilot_Transient_NotProduction";
            try
            {
                // These are three semantic aliases of ONE indivisible asset,
                // not fake empty meshes or independently usable modular pieces.
                // Standard consolidation removes those planner slots and emits
                // CharacterId once. Narrative/Crowd are deliberately forbidden.
                catalog.ReplaceContent(assets, new[]
                {
                    new VisualCatalogAsset.ProfileEntry
                    {
                        visualProfileId = ProfileId, bodyFamily = "standard-human",
                        bodyAssetId = CharacterId, headAssetId = CharacterId, clothingAssetId = CharacterId,
                        headgearAssetId = string.Empty, qualityTier = VisualQualityTier.Standard,
                        cultureFamily = "meshy-pilot-unapproved"
                    }
                }, equipment, CharacterId, UnsupportedCrowdId);
                Validate(catalog);
                return catalog;
            }
            catch
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(catalog); else UnityEngine.Object.DestroyImmediate(catalog);
                throw;
            }
        }

        public static void Validate(VisualCatalogAsset catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (catalog.Profiles.Count != 1 || catalog.ConsolidatedCharacterAssetId.Value != CharacterId || catalog.CrowdAssetId.Value != UnsupportedCrowdId)
                throw new InvalidOperationException("Meshy pilot is one isolated Standard-only profile, not a production or crowd catalog.");
            var profile = catalog.Profiles[0];
            if (profile.visualProfileId != ProfileId || profile.qualityTier != VisualQualityTier.Standard ||
                profile.bodyAssetId != CharacterId || profile.headAssetId != CharacterId || profile.clothingAssetId != CharacterId ||
                !string.IsNullOrEmpty(profile.headgearAssetId))
                throw new InvalidOperationException("Meshy whole-clothed aliases must not execute as separate Narrative modules.");
            if (catalog.Assets.Count(a => a.id == CharacterId && a.category == VisualAssetCategory.ConsolidatedCharacter && a.prefab != null) != 1 ||
                catalog.Assets.Any(a => a.id != CharacterId && !IsExternalCategory(a.category)))
                throw new InvalidOperationException("Meshy pilot must contain exactly one whole character and only external accessory assets.");
            var validation = catalog.BuildCoreCatalog().Validate();
            if (!validation.IsValid) throw new InvalidOperationException(string.Join("\n", validation.Errors));
        }

        public static TroopDefinition CreateTroop(TroopDefinition source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new TroopDefinition(source.Id, source.Name, source.UnitClassId, source.DefaultCombatRoleId,
                source.MountContext, VisualProfileId.Create(ProfileId));
        }

        public static VisualAssemblyPlan Assemble(VisualSoldier3DAssembler assembler, VisualCatalogAsset pilotCatalog,
            VisualSoldier3D view, SoldierInstance soldier, TroopDefinition sourceTroop,
            IReadOnlyDictionary<EquipmentInstanceId, EquipmentInstance> equipment)
        {
            if (assembler == null) throw new ArgumentNullException(nameof(assembler));
            Validate(pilotCatalog);
            assembler.Configure(pilotCatalog);
            return assembler.Assemble(view, soldier, CreateTroop(sourceTroop), equipment);
        }

        private static bool IsExternalCategory(VisualAssetCategory category) =>
            category == VisualAssetCategory.Weapon || category == VisualAssetCategory.Shield ||
            category == VisualAssetCategory.Auxiliary;
    }
}
