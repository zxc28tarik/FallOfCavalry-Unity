#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FOC.Domain.Common;
using FOC.Domain.Soldiers;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace FOC.Presentation.Visuals
{
    /// <summary>Isolated real-player intake review. Never activates a production profile or changes campaign data.</summary>
    public sealed class MeshyHasanPilotPlayer : MonoBehaviour
    {
        public GameObject characterPrefab = null!;
        public AnimationClip[] clips = Array.Empty<AnimationClip>();
        private readonly List<Actor> actors = new List<Actor>();
        private readonly List<CaptureEvidence> captures = new List<CaptureEvidence>();
        private VisualSoldier3DAssembler assembler = null!;
        private VisualSoldierPool pool = null!;
        private VisualCatalogAsset pilotCatalog = null!;
        private Camera cameraView = null!;
        private bool interactive;
        private string? output;
        private int actorSequence;
        private const int Width = 1600, Height = 1100;

        private sealed class Actor
        {
            public VisualSoldier3D view = null!;
            public Animator animator = null!;
            public AnimationClip? clip;
            public RuntimeAnimatorController? originalController;
            public PlayableGraph graph;
            public AnimationClipPlayable playable;
            public Transform[] tracked = Array.Empty<Transform>();
            public Vector3[] initial = Array.Empty<Vector3>();
            public float maximumTravel;
            public int evaluatedSteps;
            public int restoredBindTransforms;
            public float normalizedPhase;
            public bool renderBoundaryPoseStable;
        }

        [Serializable]
        public sealed class JointEvidence
        {
            public string name = "";
            public Vector3 actorLocalPosition, worldPosition;
        }

        [Serializable]
        public sealed class PoseEvidence
        {
            public string actor = "", clip = "";
            public float normalizedPhase;
            public double sampledClipSeconds;
            public bool runtimeControllerDetached;
            public bool renderBoundaryPoseStable;
            public JointEvidence[] joints = Array.Empty<JointEvidence>();
        }

        [Serializable]
        public sealed class CaptureEvidence
        {
            public string file = "", view = "", clip = "", signature = "";
            public float normalizedPhase, maximumJointTravelMeters;
            public int actors, rendererCount, materialSlots, boneCount, animationEvaluationSteps;
            public bool humanoidAvatarValid, realWindowsPlayer = true;
            public bool sourceRestBindPoseRestored;
            public PoseEvidence[] poses = Array.Empty<PoseEvidence>();
        }

        [Serializable]
        public sealed class PlayerEvidence
        {
            public string status = "TECHNICAL_CAPTURE_ONLY_NOT_PRODUCTION_ACCEPTANCE";
            public string sourceSha = "", unityVersion = "", graphicsDevice = "", graphicsApi = "", platform = "";
            public string note = "Empty-loadout isolated Hasan clones through FOC assembler/cache/view pool; not gameplay, mounted, grip or historical acceptance.";
            public int width, height, captures, poolViewsCreated, poolViewsReused, cachedVariants, pooledRepresentations, activeLeases;
            public CaptureEvidence[] results = Array.Empty<CaptureEvidence>();
        }

        private IEnumerator Start()
        {
            // Enumerate explicitly so all capture exceptions produce a nonzero
            // player exit, instead of silently stopping a coroutine in a loop.
            var routine = Run();
            while (true)
            {
                Exception? failure = null;
                var moved = false;
                try { moved = routine.MoveNext(); }
                catch (Exception exception) { failure = exception; }
                if (failure != null)
                {
                    Debug.LogException(failure);
                    if (output != null) File.WriteAllText(Path.Combine(output, "failure.txt"), failure.ToString());
                    Application.Quit(1);
                    yield break;
                }
                if (!moved) break;
                yield return routine.Current;
            }
        }

        private IEnumerator Run()
        {
            output = Arg("--meshy-output");
            if (output != null) { output = Path.GetFullPath(output); Directory.CreateDirectory(output); }
            if (characterPrefab == null) throw new InvalidOperationException("Meshy review prefab missing.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Actual player captures require a graphics device; -nographics is invalid.");
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            CreateStage();
            pilotCatalog = MeshyHasanPilotCatalog.Create(characterPrefab);
            assembler = gameObject.AddComponent<VisualSoldier3DAssembler>();
            pool = gameObject.AddComponent<VisualSoldierPool>();
            var template = new GameObject("Isolated Meshy view template").AddComponent<VisualSoldier3D>();
            template.transform.SetParent(transform, false);
            template.gameObject.SetActive(false);
            pool.Configure(template, 16);

            if (output == null)
            {
                var actor = CreateActor(FindClip("Walking"), Vector3.zero);
                Evaluate(actor, 1f / 60f);
                Frame("quarter");
                interactive = true;
                Debug.Log("FOC_MESHY_PILOT_INTERACTIVE Walking loop; isolated preview, close the window to exit.");
                yield break;
            }

            foreach (var direction in new[] { "front", "side", "back" })
            {
                ClearActors();
                CreateActor(null, Vector3.zero);
                yield return null;
                Frame(direction);
                Capture((captures.Count + 1).ToString("00") + "-rest-" + direction + ".png", direction, 0);
            }
            foreach (var motion in new[] { "Walking", "Running" })
            {
                ClearActors();
                var actor = CreateActor(FindClip(motion), Vector3.zero);
                // Playables genuinely evaluate source humanoid animation in the
                // built player. Fixed steps make results reproducible; these are
                // simulated animation steps, not frame-rate/performance claims.
                for (var step = 0; step < 90; step++)
                {
                    Evaluate(actor, 1f / 60f);
                    if (step % 6 == 0) yield return null;
                }
                if (actor.maximumTravel < .005f)
                    throw new InvalidOperationException(motion + " did not move the measured hand/foot joints.");
                foreach (var phase in new[] { 0f, .25f, .5f, .75f })
                {
                    // The controller is detached and this graph is manual. Let
                    // Unity flush the newly sampled skin matrices through a real
                    // render boundary before reading the camera: an immediate
                    // Camera.Render can otherwise retain the prior GPU skin pose.
                    yield return null;
                    SetPhase(actor, phase);
                    var sampledJoints = SnapshotTrackedJoints(actor);
                    yield return new WaitForEndOfFrame();
                    AssertStablePoseAcrossRenderBoundary(actor, sampledJoints);
                    Frame("quarter");
                    Capture((captures.Count + 1).ToString("00") + "-" + motion.ToLowerInvariant() + "-" + phase.ToString("0.00", CultureInfo.InvariantCulture) + ".png", "quarter", phase);
                }
            }
            ClearActors();
            for (var i = 0; i < 12; i++)
            {
                CreateActor(FindClip("Walking"), new Vector3((i % 4 - 1.5f) * 1.05f, 0, -(i / 4) * 1.25f));
            }
            yield return null;
            for (var i = 0; i < actors.Count; i++) SetPhase(actors[i], i / 12f);
            var groupSamples = actors.Select(SnapshotTrackedJoints).ToArray();
            yield return new WaitForEndOfFrame();
            for (var i = 0; i < actors.Count; i++) AssertStablePoseAcrossRenderBoundary(actors[i], groupSamples[i]);
            Frame("group12");
            Capture("12-group12.png", "group12", 0);
            ClearActors();
            if (captures.Count != 12 || assembler.ActiveLeaseCount != 0 || pool.LeasedCount != 0)
                throw new InvalidOperationException("Incomplete capture suite or leaked presentation lease.");
            var evidence = new PlayerEvidence
            {
                sourceSha = Arg("--meshy-sha") ?? "NOT_SUPPLIED", unityVersion = Application.unityVersion,
                graphicsDevice = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                platform = Application.platform.ToString(), width = Width, height = Height, captures = captures.Count,
                poolViewsCreated = pool.CreatedViewCount, poolViewsReused = pool.ReusedViewCount,
                cachedVariants = assembler.CachedVariantCount, pooledRepresentations = assembler.PooledInstanceCount,
                activeLeases = assembler.ActiveLeaseCount, results = captures.ToArray()
            };
            File.WriteAllText(Path.Combine(output, "player-evidence.json"), JsonUtility.ToJson(evidence, true));
            Debug.Log("FOC_MESHY_PILOT_CAPTURE_PASS captures=12 leases=0 output=" + output);
            Application.Quit(0);
        }

        private Actor CreateActor(AnimationClip? clip, Vector3 location)
        {
            var index = actorSequence++;
            var troop = new TroopDefinition(TroopDefinitionId.Create("meshy-hasan-review"), "Meshy Hasan pilot",
                UnitClassId.Create("pilot-review-class"), CombatRoleId.Create("pilot-review-role"),
                MountContext.InfantryOnly, VisualProfileId.Create(MeshyHasanPilotCatalog.ProfileId));
            var soldier = new SoldierInstance(SoldierId.Create("meshy-hasan-review-" + index), UnitGroupId.Create("pilot-review-group"),
                troop.Id, new SoldierRecruitmentProvenance(RecruitmentSourceId.Create("pilot-review-source"),
                RecruitmentRecordId.Create("pilot-review-record-" + index)), new SoldierLoadout(Array.Empty<WeaponSlotAssignment>()), troop.DefaultCombatRoleId);
            var view = pool.Rent();
            view.transform.localPosition = location;
            MeshyHasanPilotCatalog.Assemble(assembler, pilotCatalog, view, soldier, troop,
                new Dictionary<EquipmentInstanceId, EquipmentInstance>());
            var animator = view.GetComponentsInChildren<Animator>(true).Single();
            if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException("Source humanoid avatar is missing or invalid.");
            foreach (var lod in view.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);
            foreach (var skin in view.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
            var actor = new Actor { view = view, animator = animator, clip = clip,
                originalController = animator.runtimeAnimatorController };
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // A manual graph exclusively owns this review Animator. Retaining
            // the default controller lets Unity overwrite a sampled clip pose.
            animator.runtimeAnimatorController = null;
            animator.enabled = clip != null;
            if (clip == null)
            {
                // The existing assembler legitimately Rebinds its controller,
                // whose default state may already be Walking. Disabling it does
                // not restore the imported rest pose. Copy the ORIGINAL source
                // bone local transforms, never a synthesized T/A pose.
                actor.restoredBindTransforms = RestoreSourceBindPose(characterPrefab, animator.gameObject);
            }
            else
            {
                if (!clip.isHumanMotion) throw new InvalidOperationException("Review clip is not imported as humanoid: " + clip.name);
                actor.graph = PlayableGraph.Create("Meshy source humanoid " + index);
                actor.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                actor.playable = AnimationClipPlayable.Create(actor.graph, clip);
                actor.playable.SetApplyFootIK(false);
                actor.playable.SetApplyPlayableIK(false);
                var animationOutput = AnimationPlayableOutput.Create(actor.graph, "Humanoid source clip", animator);
                animationOutput.SetSourcePlayable(actor.playable);
                actor.graph.Play();
                actor.graph.Evaluate(0);
            }
            actor.tracked = new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot }
                .Select(animator.GetBoneTransform).Where(bone => bone != null).ToArray();
            if (actor.tracked.Length != 4) throw new InvalidOperationException("Humanoid hand/foot mapping incomplete.");
            actor.initial = actor.tracked.Select(bone => bone.position).ToArray();
            actors.Add(actor);
            return actor;
        }

        private static void Evaluate(Actor actor, float delta)
        {
            if (actor.clip == null) return;
            if (actor.playable.GetTime() >= actor.clip.length) actor.playable.SetTime(actor.playable.GetTime() % actor.clip.length);
            actor.graph.Evaluate(delta);
            actor.evaluatedSteps++;
            for (var i = 0; i < actor.tracked.Length; i++)
                actor.maximumTravel = Mathf.Max(actor.maximumTravel, Vector3.Distance(actor.initial[i], actor.tracked[i].position));
        }

        private static void SetPhase(Actor actor, float phase)
        {
            actor.normalizedPhase = phase;
            actor.renderBoundaryPoseStable = false;
            actor.playable.SetTime(phase * actor.clip!.length);
            actor.graph.Evaluate(0);
        }

        private static Vector3[] SnapshotTrackedJoints(Actor actor)
            => actor.tracked.Select(joint => joint.position).ToArray();

        private static void AssertStablePoseAcrossRenderBoundary(Actor actor, Vector3[] sampled)
        {
            if (actor.animator.runtimeAnimatorController != null)
                throw new InvalidOperationException("An automatic controller regained ownership of the sampled pose.");
            for (var i = 0; i < sampled.Length; i++)
                if (Vector3.Distance(sampled[i], actor.tracked[i].position) > .00001f)
                    throw new InvalidOperationException("Manual sampled pose changed across the render boundary: " + actor.tracked[i].name);
            actor.renderBoundaryPoseStable = true;
        }

        private void LateUpdate()
        {
            if (interactive) foreach (var actor in actors) Evaluate(actor, Mathf.Min(Time.deltaTime, .1f));
        }

        private void ClearActors()
        {
            foreach (var actor in actors)
            {
                if (actor.graph.IsValid()) actor.graph.Destroy();
                actor.animator.enabled = false;
                actor.animator.runtimeAnimatorController = actor.originalController;
                pool.Return(actor.view);
            }
            actors.Clear();
        }

        private AnimationClip FindClip(string name)
        {
            var matches = clips.Where(clip => clip != null &&
                (clip.name.Equals(name, StringComparison.OrdinalIgnoreCase) || clip.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Expected exactly one " + name + " source clip; found " + matches.Length);
            return matches[0];
        }

        private void CreateStage()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.40f, .42f, .45f);
            RenderSettings.fog = false;
            foreach (var setup in new[] { (new Vector3(38, -28, 0), 1.05f), (new Vector3(22, 140, 0), .55f) })
            {
                var light = new GameObject("Neutral Meshy review light").AddComponent<Light>();
                light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(setup.Item1);
                light.intensity = setup.Item2; light.color = Color.white; light.shadows = LightShadows.Soft;
                light.shadowBias = .01f; light.shadowNormalBias = .025f; light.shadowStrength = .75f;
            }
            QualitySettings.shadows = ShadowQuality.All; QualitySettings.shadowDistance = 40;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Neutral review floor (not character art)"; ground.transform.localScale = Vector3.one * 10;
            ground.transform.position = new Vector3(0, -.015f, 0);
            var surface = new Material(Shader.Find("Standard")) { color = new Color(.17f, .18f, .20f) };
            surface.SetFloat("_Glossiness", .05f); ground.GetComponent<Renderer>().sharedMaterial = surface;
            cameraView = new GameObject("Actual player review camera").AddComponent<Camera>();
            cameraView.clearFlags = CameraClearFlags.SolidColor; cameraView.backgroundColor = new Color(.065f, .073f, .085f);
            cameraView.fieldOfView = 34; cameraView.nearClipPlane = .02f; cameraView.farClipPlane = 100;
        }

        private Bounds ActorBounds()
        {
            var bounds = new Bounds(); var first = true;
            foreach (var actor in actors) foreach (var renderer in actor.view.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    var mesh = new Mesh();
                    try
                    {
                        skin.BakeMesh(mesh);
                        foreach (var vertex in mesh.vertices)
                        {
                            var world = skin.transform.TransformPoint(vertex);
                            if (first) { bounds = new Bounds(world, Vector3.zero); first = false; } else bounds.Encapsulate(world);
                        }
                    }
                    finally { Destroy(mesh); }
                }
                else { if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds); }
            }
            if (first || bounds.size.magnitude < .1f || bounds.size.magnitude > 20)
                throw new InvalidOperationException("Invalid pilot render bounds; units/geometry require investigation.");
            return bounds;
        }

        private void Frame(string view)
        {
            var bounds = ActorBounds();
            var direction = view == "front" ? new Vector3(0, .03f, 1) : view == "back" ? new Vector3(0, .03f, -1)
                : view == "side" ? new Vector3(1, .03f, 0) : view == "group12" ? new Vector3(.32f, .23f, 1) : new Vector3(.45f, .08f, 1);
            var distance = bounds.extents.magnitude / Mathf.Sin(cameraView.fieldOfView * Mathf.Deg2Rad * .5f) * 1.09f;
            cameraView.transform.position = bounds.center + direction.normalized * distance;
            cameraView.transform.LookAt(bounds.center);
        }

        private void Capture(string filename, string view, float phase)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                cameraView.targetTexture = target; cameraView.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); texture.Apply();
                var pixels = texture.GetPixels32();
                var minimum = 765; var maximum = 0;
                foreach (var pixel in pixels) { minimum = Math.Min(minimum, pixel.r + pixel.g + pixel.b); maximum = Math.Max(maximum, pixel.r + pixel.g + pixel.b); }
                if (maximum - minimum < 20) throw new InvalidOperationException("Blank or uniform player capture.");
                File.WriteAllBytes(Path.Combine(output!, filename), texture.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; cameraView.targetTexture = null; target.Release(); Destroy(target); Destroy(texture); }
            var skins = actors.SelectMany(actor => actor.view.GetComponentsInChildren<SkinnedMeshRenderer>()).ToArray();
            var renderers = actors.SelectMany(actor => actor.view.GetComponentsInChildren<Renderer>()).Where(r => r.enabled).ToArray();
            captures.Add(new CaptureEvidence
            {
                file = filename, view = view, clip = actors[0].clip == null ? "SOURCE_REST" : actors[0].clip!.name,
                normalizedPhase = phase, actors = actors.Count, rendererCount = renderers.Length,
                materialSlots = renderers.Sum(r => r.sharedMaterials.Length), boneCount = skins.Sum(s => s.bones.Length),
                humanoidAvatarValid = actors.All(actor => actor.animator.avatar.isValid && actor.animator.avatar.isHuman),
                maximumJointTravelMeters = actors.Max(actor => actor.maximumTravel), animationEvaluationSteps = actors.Sum(actor => actor.evaluatedSteps),
                signature = actors[0].view.ActiveRepresentationRoot!.GetComponent<VisualRuntimeVariant>().Signature,
                sourceRestBindPoseRestored = actors.All(actor => actor.clip == null && actor.restoredBindTransforms > 0),
                poses = actors.Select(actor => new PoseEvidence
                {
                    actor = actor.view.name, clip = actor.clip == null ? "SOURCE_REST" : actor.clip.name,
                    normalizedPhase = actor.normalizedPhase,
                    sampledClipSeconds = actor.clip == null ? 0 : actor.playable.GetTime(),
                    runtimeControllerDetached = actor.animator.runtimeAnimatorController == null,
                    renderBoundaryPoseStable = actor.renderBoundaryPoseStable,
                    joints = actor.tracked.Select(joint => new JointEvidence
                    {
                        name = joint.name, worldPosition = joint.position,
                        actorLocalPosition = actor.view.transform.InverseTransformPoint(joint.position)
                    }).ToArray()
                }).ToArray()
            });
            Debug.Log("FOC_MESHY_PLAYER_CAPTURE " + filename + " actors=" + actors.Count + " clip=" + captures.Last().clip);
        }

        private void OnDestroy()
        {
            ClearActors();
            if (assembler != null) assembler.DisposeCache();
            if (pilotCatalog != null) Destroy(pilotCatalog);
        }

        /// <summary>Restore only exact source bone/ancestor TRS, not the assembled actor's placement.</summary>
        public static int RestoreSourceBindPose(GameObject sourcePrefab, GameObject sourceInstance)
        {
            if (sourcePrefab == null || sourceInstance == null) throw new ArgumentNullException(nameof(sourcePrefab));
            var animator = sourceInstance.GetComponent<Animator>();
            if (animator == null || animator.enabled)
                throw new InvalidOperationException("Disable the source instance Animator before restoring its original bind pose.");
            var bones = sourcePrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(skin => skin.bones).Distinct().ToArray();
            if (bones.Length == 0) throw new InvalidOperationException("Source prefab has no skin-bound bones.");
            var originals = new HashSet<Transform>();
            foreach (var bone in bones)
            {
                if (bone == null) throw new InvalidOperationException("Source bone reference is missing.");
                var current = bone;
                while (current != sourcePrefab.transform)
                {
                    if (current == null) throw new InvalidOperationException("Source skin bone is outside its prefab hierarchy.");
                    originals.Add(current);
                    current = current.parent;
                }
            }
            var instanceByPath = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var node in sourceInstance.GetComponentsInChildren<Transform>(true))
            {
                if (node == sourceInstance.transform) continue;
                var path = RelativeBonePath(node, sourceInstance.transform);
                if (instanceByPath.ContainsKey(path)) throw new InvalidOperationException("Ambiguous source instance hierarchy path: " + path);
                instanceByPath.Add(path, node);
            }
            foreach (var original in originals)
            {
                var path = RelativeBonePath(original, sourcePrefab.transform);
                if (!instanceByPath.TryGetValue(path, out var instance))
                    throw new InvalidOperationException("Source bind pose hierarchy mismatch: " + path);
                instance.localPosition = original.localPosition;
                instance.localRotation = original.localRotation;
                instance.localScale = original.localScale;
            }
            if (originals.Count == 0) throw new InvalidOperationException("No source bone transforms were restored.");
            return originals.Count;
        }

        private static string RelativeBonePath(Transform node, Transform root)
        {
            var names = new Stack<string>();
            var current = node;
            while (current != root)
            {
                if (current == null) throw new InvalidOperationException("Bone is outside expected source hierarchy.");
                names.Push(current.name);
                current = current.parent;
            }
            return string.Join("/", names);
        }

        private static string? Arg(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(arguments, name);
            if (index < 0) return null;
            if (index + 1 == arguments.Length || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException(name + " needs a value.");
            return arguments[index + 1];
        }
    }
}
