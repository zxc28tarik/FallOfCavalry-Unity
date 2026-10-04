#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FOC.Domain.Common;
using FOC.Domain.Soldiers;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Stopwatch = System.Diagnostics.Stopwatch;
using Object = UnityEngine.Object;

namespace FOC.Presentation.Visuals
{
    /// <summary>Isolated, real FOC assembly/reuse measurements; never production activation or a hardware-independent budget.</summary>
    public static class MeshyHasanPilotBenchmark
    {
        [Serializable] public sealed class Operation
        {
            public double elapsedMilliseconds;
            public bool managedAllocationCounterAvailable;
            public long managedAllocatedBytes = -1, managedHeapDeltaBytes;
            public int createdViews, reusedViews, createdRepresentations, reusedRepresentations, cacheHits, cacheMisses;
        }
        [Serializable] public sealed class LodInventory
        {
            public int level, rendererReferences;
            public long triangles;
        }
        [Serializable] public sealed class GeometryInventory
        {
            public int actorCount, rendererComponents, skinnedRendererComponents, uniqueMaterials, materialSlots, uniqueTextures, uniqueMeshes;
            public long unityReportedSharedTextureMemoryBytes, unityReportedSharedMeshMemoryBytes;
            public LodInventory[] lods = Array.Empty<LodInventory>();
            public string note = "All renderer components/assets are inventoried; LOD0 is forced during timing. LOD triangles are summed per visible actor, shared texture/mesh memory is counted once. Unity runtime-memory estimates are not dedicated VRAM readings.";
        }
        [Serializable] public sealed class FrameSamples
        {
            public int frames, animatedActors, cpuFrameSamples, gpuFrameSamples;
            public bool cpuFrameTimingAvailable, gpuFrameTimingAvailable;
            public bool unityFrameTimingFeatureEnabled;
            public double meanFrameIntervalMilliseconds, meanManualAnimationEvaluationMilliseconds;
            public double meanCpuFrameMilliseconds = -1, meanGpuFrameMilliseconds = -1;
            public string unavailableReason = "";
            public string note = "Warm-reused LOD0 actors, supplied Humanoid clip, manual graph evaluation. Frame interval includes player scheduling; manual animation CPU is not isolated skinning cost. CPU/GPU frame timing is scene-wide, includes review stage/camera and is reported only for distinct nonzero FrameTiming samples. No proof-asset comparison.";
        }
        [Serializable] public sealed class Case
        {
            public int actors, cachedVariants, pooledRepresentations, activeLeasesAfterReturn;
            public string effectiveAvatar = "", avatarOrigin = "";
            public Operation coldAssembly = new Operation(), warmAssembly = new Operation();
            public GeometryInventory geometry = new GeometryInventory();
            public FrameSamples timing = new FrameSamples();
            public bool warmReusedAllViewsAndRepresentations;
        }
        [Serializable] public sealed class Report
        {
            public string status = "TECHNICAL_BENCHMARK_NOT_PRODUCTION_ACCEPTANCE";
            public string unityVersion = "", graphicsDevice = "", graphicsApi = "", platform = "", clip = "";
            public string effectiveAvatar = "", sourcePrefabAvatar = "", avatarOrigin = "";
            public string contactProfile = "NONE";
            public string scope = "Empty-loadout single Hasan variant through real VisualSoldier3DAssembler/cache/VisualSoldierPool. Cold means fresh FOC caches, not cold disk/GPU assets. No gameplay, save, horse or production catalog mutation.";
            public Case[] cases = Array.Empty<Case>();
        }

        private sealed class Motion
        {
            public Animator animator = null!;
            public RuntimeAnimatorController? previousController;
            public Avatar? previousAvatar;
            public PlayableGraph graph;
            public AnimationClipPlayable playable;
            public MeshyTargetContactProfile? contact;
            public Vector3 presentationRestPosition;
        }
        private sealed class Counters
        {
            public int createdViews, reusedViews, createdRepresentations, reusedRepresentations, cacheHits, cacheMisses;
        }
        private static readonly Func<long>? AllocationCounter = CreateAllocationCounter();

        /// <summary>Caller should drain this enumerator directly so exceptions reach its exit-code handler.</summary>
        public static IEnumerator Run(GameObject sourcePrefab, AnimationClip motionClip, Transform parent,
            Action<Report> completed, Action<Bounds>? frameActors = null, int frameSamples = 30, Avatar? avatarOverride = null, MeshyTargetContactProfile? contactProfile = null)
        {
            if (sourcePrefab == null || motionClip == null || parent == null || completed == null) throw new ArgumentNullException("Benchmark requires prefab, clip, parent and completion callback.");
            if (!motionClip.isHumanMotion || motionClip.length <= 0) throw new InvalidOperationException("Benchmark requires a real imported Humanoid motion clip.");
            if (avatarOverride != null && (!avatarOverride.isHuman || !avatarOverride.isValid))
                throw new InvalidOperationException("Benchmark candidate Avatar must be valid and Humanoid.");
            if (frameSamples < 2 || frameSamples > 600) throw new ArgumentOutOfRangeException(nameof(frameSamples));
            if (!Application.isPlaying || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Runtime benchmark must execute in the graphics-enabled player, not EditMode or -nographics.");
            var catalog = MeshyHasanPilotCatalog.Create(sourcePrefab);
            var sourceAvatar = sourcePrefab.GetComponent<Animator>().avatar;
            var effectiveAvatar = avatarOverride != null ? avatarOverride : sourceAvatar;
            var avatarOrigin = avatarOverride != null ? "EXPLICIT_CANDIDATE_OVERRIDE" : "ORIGINAL_SOURCE_PREFAB_AVATAR";
            var results = new List<Case>();
            try
            {
                foreach (var count in new[] { 1, 12, 100 })
                {
                    var host = new GameObject("Isolated Meshy benchmark " + count);
                    host.transform.SetParent(parent, false);
                    var assembler = host.AddComponent<VisualSoldier3DAssembler>();
                    var pool = host.AddComponent<VisualSoldierPool>();
                    var template = new GameObject("Benchmark view template").AddComponent<VisualSoldier3D>();
                    template.transform.SetParent(host.transform, false); template.gameObject.SetActive(false);
                    pool.Configure(template, count);
                    var views = new List<VisualSoldier3D>(count);
                    var motions = new List<Motion>(count);
                    var troop = new TroopDefinition(TroopDefinitionId.Create("meshy-hasan-benchmark"), "Isolated Meshy benchmark",
                        UnitClassId.Create("pilot-benchmark-class"), CombatRoleId.Create("pilot-benchmark-role"),
                        MountContext.InfantryOnly, VisualProfileId.Create(MeshyHasanPilotCatalog.ProfileId));
                    var soldiers = Enumerable.Range(0, count).Select(i => new SoldierInstance(
                        SoldierId.Create("meshy-benchmark-" + i), UnitGroupId.Create("meshy-benchmark-group"), troop.Id,
                        new SoldierRecruitmentProvenance(RecruitmentSourceId.Create("meshy-benchmark-source"), RecruitmentRecordId.Create("meshy-benchmark-record-" + i)),
                        new SoldierLoadout(Array.Empty<WeaponSlotAssignment>()), troop.DefaultCombatRoleId)).ToArray();
                    var equipment = new Dictionary<EquipmentInstanceId, EquipmentInstance>();
                    try
                    {
                        var result = new Case { actors = count, effectiveAvatar = effectiveAvatar.name, avatarOrigin = avatarOrigin };
                        result.coldAssembly = Measure(pool, assembler, () => Assemble(views, pool, assembler, catalog, soldiers, troop, equipment));
                        foreach (var view in views) pool.Return(view);
                        views.Clear();
                        result.warmAssembly = Measure(pool, assembler, () => Assemble(views, pool, assembler, catalog, soldiers, troop, equipment));
                        result.warmReusedAllViewsAndRepresentations = result.warmAssembly.createdViews == 0 && result.warmAssembly.createdRepresentations == 0
                            && result.warmAssembly.reusedViews == count && result.warmAssembly.reusedRepresentations == count;
                        if (!result.warmReusedAllViewsAndRepresentations) throw new InvalidOperationException("Warm benchmark failed actual FOC pool reuse for " + count + " actors.");
                        result.geometry = InspectGeometry(views);
                        for (var i = 0; i < views.Count; i++)
                        {
                            motions.Add(contactProfile == null ? Animate(views[i], sourcePrefab, motionClip, i / (float)count, avatarOverride)
                                : AnimateWithContact(views[i], sourcePrefab, motionClip, i / (float)count, avatarOverride, contactProfile));
                            if (motions[i].animator.avatar != effectiveAvatar)
                                throw new InvalidOperationException("Benchmark actor did not retain the selected effective Avatar.");
                        }
                        frameActors?.Invoke(VisibleBounds(views));
                        for (var warmup = 0; warmup < 5; warmup++)
                        {
                            Advance(motions, motionClip.length, 1f / 60f);
                            yield return new WaitForEndOfFrame();
                            yield return null;
                        }
                        var timing = result.timing;
                        timing.animatedActors = count;
                        timing.unityFrameTimingFeatureEnabled = FrameTimingFeatureEnabled();
                        var frame = new FrameTiming[1];
                        var seenCpu = new HashSet<ulong>(); var seenGpu = new HashSet<ulong>();
                        var sumInterval = 0d; var sumAnimation = 0d; var sumCpu = 0d; var sumGpu = 0d;
                        var interval = Stopwatch.StartNew();
                        for (var sample = 0; sample < frameSamples; sample++)
                        {
                            var animationWatch = Stopwatch.StartNew();
                            Advance(motions, motionClip.length, Mathf.Min(Time.unscaledDeltaTime, .1f));
                            animationWatch.Stop(); sumAnimation += animationWatch.Elapsed.TotalMilliseconds;
                            if (timing.unityFrameTimingFeatureEnabled) FrameTimingManager.CaptureFrameTimings();
                            yield return new WaitForEndOfFrame();
                            if (timing.unityFrameTimingFeatureEnabled && FrameTimingManager.GetLatestTimings(1, frame) > 0)
                            {
                                var value = frame[0];
                                if (value.cpuFrameTime > 0 && IsFinite(value.cpuFrameTime) && seenCpu.Add(value.frameStartTimestamp)) { sumCpu += value.cpuFrameTime; timing.cpuFrameSamples++; }
                                if (value.gpuFrameTime > 0 && IsFinite(value.gpuFrameTime) && seenGpu.Add(value.frameStartTimestamp)) { sumGpu += value.gpuFrameTime; timing.gpuFrameSamples++; }
                            }
                            yield return null;
                            sumInterval += interval.Elapsed.TotalMilliseconds; interval.Restart(); timing.frames++;
                        }
                        timing.meanFrameIntervalMilliseconds = sumInterval / timing.frames;
                        timing.meanManualAnimationEvaluationMilliseconds = sumAnimation / timing.frames;
                        timing.cpuFrameTimingAvailable = timing.cpuFrameSamples > 0;
                        timing.gpuFrameTimingAvailable = timing.gpuFrameSamples > 0;
                        if (timing.cpuFrameTimingAvailable) timing.meanCpuFrameMilliseconds = sumCpu / timing.cpuFrameSamples;
                        if (timing.gpuFrameTimingAvailable) timing.meanGpuFrameMilliseconds = sumGpu / timing.gpuFrameSamples;
                        if (!timing.cpuFrameTimingAvailable || !timing.gpuFrameTimingAvailable)
                            timing.unavailableReason = "Unity FrameTiming returned no usable distinct nonzero " + (!timing.cpuFrameTimingAvailable ? "CPU " : "")
                                + (!timing.gpuFrameTimingAvailable ? "GPU " : "") + "samples; capability/settings/driver availability is not assumed.";
                        StopMotion(motions);
                        foreach (var view in views) pool.Return(view);
                        views.Clear();
                        result.cachedVariants = assembler.CachedVariantCount; result.pooledRepresentations = assembler.PooledInstanceCount;
                        result.activeLeasesAfterReturn = assembler.ActiveLeaseCount + pool.LeasedCount;
                        if (result.activeLeasesAfterReturn != 0) throw new InvalidOperationException("Benchmark leaked FOC presentation leases.");
                        results.Add(result);
                        Debug.Log("FOC_MESHY_BENCHMARK actors=" + count + " coldMs=" + result.coldAssembly.elapsedMilliseconds
                            + " warmMs=" + result.warmAssembly.elapsedMilliseconds + " warmReuse=" + result.warmReusedAllViewsAndRepresentations);
                    }
                    finally
                    {
                        StopMotion(motions);
                        foreach (var view in views) pool.Return(view);
                        assembler.DisposeCache();
                        Object.Destroy(host);
                    }
                    // Let Unity process deferred Destroy before the next case.
                    yield return null;
                }
                completed(new Report { unityVersion = Application.unityVersion, graphicsDevice = SystemInfo.graphicsDeviceName,
                    graphicsApi = SystemInfo.graphicsDeviceType.ToString(), platform = Application.platform.ToString(), clip = motionClip.name,
                    effectiveAvatar = effectiveAvatar.name, sourcePrefabAvatar = sourceAvatar.name, avatarOrigin = avatarOrigin, contactProfile = contactProfile == null ? "NONE" : contactProfile.name, cases = results.ToArray() });
            }
            finally { Object.Destroy(catalog); }
        }

        private static void Assemble(List<VisualSoldier3D> views, VisualSoldierPool pool, VisualSoldier3DAssembler assembler,
            VisualCatalogAsset catalog, SoldierInstance[] soldiers, TroopDefinition troop, IReadOnlyDictionary<EquipmentInstanceId, EquipmentInstance> equipment)
        {
            var columns = Mathf.CeilToInt(Mathf.Sqrt(soldiers.Length));
            for (var i = 0; i < soldiers.Length; i++)
            {
                var view = pool.Rent(); views.Add(view);
                view.transform.localPosition = new Vector3((i % columns - (columns - 1) * .5f) * 1.05f, 0, -(i / columns) * 1.25f);
                MeshyHasanPilotCatalog.Assemble(assembler, catalog, view, soldiers[i], troop, equipment);
                foreach (var lod in view.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);
                foreach (var skin in view.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
            }
        }
        private static Operation Measure(VisualSoldierPool pool, VisualSoldier3DAssembler assembler, Action operation)
        {
            var before = ReadCounters(pool, assembler);
            var watch = new Stopwatch();
            var heap = GC.GetTotalMemory(false); var allocated = AllocationCounter?.Invoke() ?? -1;
            watch.Start(); operation(); watch.Stop();
            var allocationDelta = AllocationCounter == null ? -1 : AllocationCounter() - allocated;
            var heapDelta = GC.GetTotalMemory(false) - heap;
            var after = ReadCounters(pool, assembler);
            return new Operation { elapsedMilliseconds = watch.Elapsed.TotalMilliseconds, managedAllocationCounterAvailable = AllocationCounter != null,
                managedAllocatedBytes = allocationDelta, managedHeapDeltaBytes = heapDelta,
                createdViews = after.createdViews - before.createdViews, reusedViews = after.reusedViews - before.reusedViews,
                createdRepresentations = after.createdRepresentations - before.createdRepresentations, reusedRepresentations = after.reusedRepresentations - before.reusedRepresentations,
                cacheHits = after.cacheHits - before.cacheHits, cacheMisses = after.cacheMisses - before.cacheMisses };
        }
        private static Counters ReadCounters(VisualSoldierPool pool, VisualSoldier3DAssembler assembler) => new Counters
        {
            createdViews = pool.CreatedViewCount, reusedViews = pool.ReusedViewCount, createdRepresentations = assembler.Metrics.CreatedRepresentations,
            reusedRepresentations = assembler.Metrics.ReusedRepresentations, cacheHits = assembler.Metrics.CacheHits, cacheMisses = assembler.Metrics.CacheMisses
        };
        private static Func<long>? CreateAllocationCounter()
        {
            var method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
            return method == null ? null : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
        }
        private static Motion Animate(VisualSoldier3D view, GameObject sourcePrefab, AnimationClip clip, float phase, Avatar? avatarOverride)
        {
            var animator = view.GetComponentsInChildren<Animator>(true).Single();
            if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman) throw new InvalidOperationException("Benchmark actor lost its Humanoid avatar.");
            var motion = new Motion { animator = animator, previousController = animator.runtimeAnimatorController, previousAvatar = animator.avatar, presentationRestPosition = animator.transform.localPosition };
            try
            {
                animator.runtimeAnimatorController = null;
                if (avatarOverride != null)
                {
                    if (!avatarOverride.isHuman || !avatarOverride.isValid)
                        throw new InvalidOperationException("Benchmark candidate Avatar must be valid and Humanoid.");
                    animator.enabled = false;
                    MeshyHasanPilotPlayer.RestoreSourceBindPose(sourcePrefab, animator.gameObject);
                    animator.avatar = avatarOverride;
                    animator.Rebind();
                }
                animator.enabled = true; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (clip == null || !clip.isHumanMotion || clip.length <= 0)
                    throw new InvalidOperationException("Benchmark requires a real imported Humanoid motion clip.");
                motion.graph = PlayableGraph.Create("Meshy benchmark real Humanoid"); motion.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                motion.playable = AnimationClipPlayable.Create(motion.graph, clip); motion.playable.SetApplyFootIK(false); motion.playable.SetApplyPlayableIK(false);
                AnimationPlayableOutput.Create(motion.graph, "Benchmark supplied motion", animator).SetSourcePlayable(motion.playable);
                motion.graph.Play(); motion.playable.SetTime(phase * clip.length); motion.graph.Evaluate(0);
                return motion;
            }
            catch
            {
                // A failed Animate call is not yet in Run's motions list.
                // Restore its cached source state here before Run returns views.
                StopMotion(motion);
                throw;
            }
        }
        private static void Advance(List<Motion> motions, float length, float delta)
        {
            foreach (var motion in motions)
            {
                if (motion.playable.GetTime() >= length) motion.playable.SetTime(motion.playable.GetTime() % length);
                motion.animator.transform.localPosition = motion.presentationRestPosition;
                motion.graph.Evaluate(delta);
                if (motion.contact != null) motion.animator.transform.localPosition = motion.presentationRestPosition
                    + Vector3.up * motion.contact.Evaluate((float)(motion.playable.GetTime() % length / length));
            }
        }
        private static Motion AnimateWithContact(VisualSoldier3D view, GameObject prefab, AnimationClip clip, float phase, Avatar? avatar, MeshyTargetContactProfile contact)
        {
            var motion = Animate(view, prefab, clip, phase, avatar);
            try
            {
                contact.ValidateBinding(motion.animator, clip);
                motion.contact = contact;
                motion.playable.SetApplyFootIK(contact.useFootIK);
                motion.animator.transform.localPosition = motion.presentationRestPosition;
                motion.graph.Evaluate(0);
                motion.animator.transform.localPosition = motion.presentationRestPosition + Vector3.up * contact.Evaluate(phase);
                return motion;
            }
            catch { StopMotion(motion); throw; }
        }
        private static void StopMotion(List<Motion> motions)
        {
            foreach (var motion in motions) StopMotion(motion);
            motions.Clear();
        }
        private static void StopMotion(Motion motion)
        {
            if (motion.graph.IsValid()) motion.graph.Destroy();
            if (motion.animator != null)
            {
                motion.animator.enabled = false;
                motion.animator.avatar = motion.previousAvatar;
                motion.animator.runtimeAnimatorController = motion.previousController;
                motion.animator.transform.localPosition = motion.presentationRestPosition;
            }
        }

        public static GeometryInventory InspectGeometry(IReadOnlyList<VisualSoldier3D> views)
        {
            if (views == null) throw new ArgumentNullException(nameof(views));
            var renderers = views.SelectMany(v => v.GetComponentsInChildren<Renderer>(true)).Distinct().ToArray();
            var materials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
            var textures = materials.SelectMany(m => m.GetTexturePropertyNames().Select(m.GetTexture)).Where(t => t != null).Distinct().ToArray();
            var meshes = renderers.Select(RenderMesh).Where(m => m != null).Distinct().ToArray();
            var result = new GeometryInventory { actorCount = views.Count, rendererComponents = renderers.Length,
                skinnedRendererComponents = renderers.OfType<SkinnedMeshRenderer>().Count(), materialSlots = renderers.Sum(r => r.sharedMaterials.Length),
                uniqueMaterials = materials.Length, uniqueTextures = textures.Length, uniqueMeshes = meshes.Length,
                unityReportedSharedTextureMemoryBytes = textures.Sum(t => Profiler.GetRuntimeMemorySizeLong(t)),
                unityReportedSharedMeshMemoryBytes = meshes.Sum(m => Profiler.GetRuntimeMemorySizeLong(m)) };
            var levels = new Dictionary<int, HashSet<Renderer>>();
            foreach (var view in views)
            {
                var groups = view.GetComponentsInChildren<LODGroup>(true);
                if (groups.Length == 0) AddLod(levels, 0, view.GetComponentsInChildren<Renderer>(true));
                else foreach (var group in groups)
                {
                    var lods = group.GetLODs();
                    for (var level = 0; level < lods.Length; level++) AddLod(levels, level, lods[level].renderers);
                }
            }
            result.lods = levels.OrderBy(pair => pair.Key).Select(pair => new LodInventory
            {
                level = pair.Key, rendererReferences = pair.Value.Count, triangles = pair.Value.Sum(r => TriangleCount(RenderMesh(r)))
            }).ToArray();
            return result;
        }
        private static void AddLod(Dictionary<int, HashSet<Renderer>> levels, int level, IEnumerable<Renderer> renderers)
        {
            if (!levels.TryGetValue(level, out var set)) levels.Add(level, set = new HashSet<Renderer>());
            foreach (var renderer in renderers) if (renderer != null) set.Add(renderer);
        }
        private static Mesh? RenderMesh(Renderer renderer) => renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
        public static long TriangleCount(Mesh? mesh)
        {
            if (mesh == null) return 0;
            long triangles = 0;
            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
                if (mesh.GetTopology(submesh) == MeshTopology.Triangles) triangles += (long)mesh.GetIndexCount(submesh) / 3;
            return triangles;
        }
        private static Bounds VisibleBounds(IEnumerable<VisualSoldier3D> views)
        {
            var first = true; var result = new Bounds();
            foreach (var renderer in views.SelectMany(view => view.GetComponentsInChildren<Renderer>()))
            {
                if (first) { result = renderer.bounds; first = false; } else result.Encapsulate(renderer.bounds);
            }
            if (first) throw new InvalidOperationException("Benchmark has no visible renderer bounds.");
            return result;
        }
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool FrameTimingFeatureEnabled()
        {
            var method = typeof(FrameTimingManager).GetMethod("IsFeatureEnabled", Type.EmptyTypes);
            return method != null && method.Invoke(null, null) is bool enabled && enabled;
        }
    }
}
