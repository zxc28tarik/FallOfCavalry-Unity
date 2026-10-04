#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object=UnityEngine.Object;

namespace FOC.Editor.Visuals
{
    /// <summary>
    /// Isolated, measured reference-pose experiment. All skeletal edits occur
    /// on disposable clones; only NEW Avatar/clip candidates are authored.
    /// Nothing here declares visual acceptance or activates production art.
    /// </summary>
    public static class MeshyHumanoidCalibration
    {
        public const string OutputRoot=MeshyHasanPilotPipeline.OutputRoot+"/Calibration";
        public const string TargetAvatarPath=OutputRoot+"/AVT_MeshyHasan_Calibrated.asset";
        public const string SourceAvatarPath=OutputRoot+"/AVT_FOC_Donor_Calibrated.asset";
        public const string ManifestPath=OutputRoot+"/CalibrationProvenance.json";
        public static string ReportPath=>Path.GetFullPath("../TestResults/MeshyCalibration/calibration.json");
        public static readonly float[] Phases={0f,.125f,.25f,.375f,.5f,.625f,.75f,.875f};
        public static readonly HumanBodyBones[] JointOrder={HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.Neck,HumanBodyBones.Head,HumanBodyBones.LeftShoulder,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightShoulder,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.LeftToes,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,HumanBodyBones.RightToes};
        private static readonly HumanBodyBones[] RequiredJoints=JointOrder.Where(b=>b!=HumanBodyBones.LeftShoulder&&b!=HumanBodyBones.RightShoulder&&b!=HumanBodyBones.LeftToes&&b!=HumanBodyBones.RightToes).ToArray();
        private static readonly HumanBodyBones[] LimbStarts={HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg};
        private static readonly HumanBodyBones[] LimbEnds={HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot};

        [Serializable] public sealed class InputHash { public string path="";public string sha256=""; }
        [Serializable] public sealed class BoneAudit
        {
            public string humanBone="";public string bone="";public string path="";public string parent="";
            public Vector3 localPosition;public Quaternion localRotation;public Vector3 localScale;public Vector3 worldPosition;public Quaternion worldRotation;
            public Vector3 localAxisX;public Vector3 localAxisY;public Vector3 localAxisZ;public Vector3 worldAxisX;public Vector3 worldAxisY;public Vector3 worldAxisZ;
            public HumanLimit limit;
        }
        [Serializable] public sealed class ReferenceDelta { public string path="";public Quaternion originalLocalRotation;public Quaternion candidateLocalRotation;public float degrees; }
        [Serializable] public sealed class MappingChange { public string bone="";public string originalHuman="";public string candidateHuman=""; }
        [Serializable] public sealed class ReferenceAlignment
        {
            public Vector3 gravityUp;public Vector3 physicalForward;public Vector3 anatomicalLeft;
            public float[] segmentDirectionErrors=Array.Empty<float>();public float maximumSegmentDirectionError;
            public bool measuredToes;public float[] toeForwardErrors=Array.Empty<float>();public float[] originalAnkleToToePitchDegrees=Array.Empty<float>();public float[] referenceAnkleToToePitchDegrees=Array.Empty<float>();public float[] ankleToToePitchDriftDegrees=Array.Empty<float>();
            public string toePolicy="Forward is projected horizontal heading, NOT flattening the ankle-to-toe bone. Original world foot pitch/roll is restored, then measured heading is yaw-aligned only; no new sole offset or body-height change.";
            public bool measuredHeadMarkers;public float headUpError;public float headForwardError;public float headMarkersNonorthogonalityDegrees;public float orthogonalHeadUpError;
            public string unsupportedOrientation="No synthetic finger or missing toe/head-marker bones. Hand axes are recorded, but palm roll without finger markers is NOT independently validated. A no-toe/no-head-marker donor retains its measured original world foot/head rotation on the reference clone.";
        }
        [Serializable] public sealed class RigAudit
        {
            public string role="";public string prefab="";public string avatar="";public bool isValid;public bool isHuman;public float humanScale;
            public Vector3 measuredLeft;public Vector3 measuredUp;public Vector3 measuredForward;
            public string frameDefinition="MappedLeft = projected mapped right-to-left upper-arm origins; Up = hips-to-neck; MappedForward = cross(Up,MappedLeft). This NAME-derived comparison frame is checked against independent toe/head/weighted-sole evidence; it is NOT automatically anatomical truth or a root rotation.";
            public ForwardAudit physicalForward=new ForwardAudit();
            public MappingChange[] mappingChanges=Array.Empty<MappingChange>();public string attackOwnership="Original semantics unchanged.";
            public ReferenceAlignment alignment=new ReferenceAlignment();public Vector3 originalNeutralBodyPosition;public Quaternion originalNeutralBodyRotation;
            public string zeroBodyPolicy="Identity root; preserve measured normalized COM height, center X/Z at zero, use identity bodyRotation. Original extracted body values recorded separately. Zero muscles are not assumed to be a T-pose.";
            public BoneAudit[] mappedBones=Array.Empty<BoneAudit>();public BoneAudit[] referenceBones=Array.Empty<BoneAudit>();public ReferenceDelta[] referenceDeltas=Array.Empty<ReferenceDelta>();
            public PoseSample rest=new PoseSample();public PoseSample zeroMuscle=new PoseSample();
        }
        [Serializable] public sealed class ForwardAudit
        {
            public bool hasToeBones;public Vector3 toeDirection;public bool hasHeadFront;public Vector3 headFrontDirection;
            public bool hasWeightedSoleEvidence;public Vector3 weightedSoleDirection;public int weightedSoleVertices;
            public Vector3 weightedSoleMinimum;public Vector3 weightedSoleMaximum;public Vector3 physicalForward;
            public float mappedForwardDotPhysical;public bool sideConventionContradictsPhysicalForward;public string finding="";
            public FootHeadingEvidence[] feet=Array.Empty<FootHeadingEvidence>();
        }
        [Serializable] public sealed class FootHeadingEvidence
        {
            public string humanBone="";public string method="";public Vector3 bindHorizontalHeading;public int weightedSoleVertices;public Vector3 weightedSoleCenterFromAnkle;
            public float longitudinalVariance;public float lateralVariance;public bool available;
        }
        [Serializable] public sealed class PoseSample
        {
            public Vector3[] joints=Array.Empty<Vector3>();public Quaternion[] jointRotations=Array.Empty<Quaternion>();public string[] jointPaths=Array.Empty<string>();public bool[] jointAvailable=Array.Empty<bool>();
            public string missingJointPolicy="Unavailable source shoulder/toe joints are explicitly flagged false with empty path and zero placeholder; they are NOT measured joints or added bones.";
            public Vector3 skinMinimum;public Vector3 skinMaximum;public string skin="";public int triangles;public Vector3 bodyPosition;public Quaternion bodyRotation;
            public Vector3 rootWorldPosition;public Quaternion rootWorldRotation;
        }
        [Serializable] public sealed class Sample
        {
            public string scenario="";public string motion="";public float phase;public PoseSample source=new PoseSample();public PoseSample target=new PoseSample();
            public float[] limbDirectionErrorDegrees=Array.Empty<float>();public float sameSourceAvatarRoundtripMaximumPosition;public float maximumAbsoluteMuscle;
            public string[] outsideNominalMuscles=Array.Empty<string>();public float sourceLeftAnkleHeight;public float targetLeftAnkleHeight;public float sourceRightAnkleHeight;public float targetRightAnkleHeight;
        }
        [Serializable] public sealed class ScenarioAudit
        {
            public string id="";public string targetAvatar="";public string extractionAvatar="";public string clipOrigin="";
            public float meanLimbDirectionErrorDegrees;public float maximumLimbDirectionErrorDegrees;public float minimumSkinY;public Sample[] samples=Array.Empty<Sample>();
        }
        [Serializable] public sealed class QualityDiagnostic
        {
            public string scenario="";public string status="NOT_EVALUATED";public string[] failedChecks=Array.Empty<string>();
            public string interpretation="Declared numerical review flags, NOT an art PASS. Original diagnostic intent and Windows visual QA remain binding. No threshold clamps pose, raises floor, or changes motion.";
            public bool physicalSourceSidesConsistent;public bool physicalTargetSidesConsistent;public bool neutralHandSidesConsistent;
            public float neutralArmDirectionMaximumDegrees;public float neutralLegDirectionMaximumDegrees;public float neutralPhysicalFootDirectionMaximumDegrees;public float neutralWristPositionMaximumNormalized;
            public float legacyAveragedFootAxisDifferenceDegrees;public float[] perFootNeutralDirectionDifferencesDegrees=Array.Empty<float>();
            public string neutralFootAxisPolicy="Use EACH foot's own horizontal bind heading: target own ankle-to-toe projection; no-toe donor own weighted-sole principal longitudinal axis, sign checked against sole center from ankle. Preserve legacy averaged-heading result as a known-invalid comparison control; it discards bind foot splay. No threshold changes or Avatar tuning. This is not full contact/palm QA.";
            public float neutralDirectionReviewToleranceDegrees=10f;public float actionDirectionReviewToleranceDegrees=20f;public float actionDirectionMaximumDegrees;
            public float sourceAttackLeftWristTravel;public float sourceAttackRightWristTravel;public float targetAttackLeftWristTravel;public float targetAttackRightWristTravel;public bool intendedRightHandAttackDominates;
            public float sourceAttackTorsoMaximumRotationDegrees;public float sourceAttackTorsoMaximumTravel;public bool hasAuthoredAttackTorsoIntent;
            public float reviewFloorY=0f;public float penetrationToleranceMeters=.002f;public float minimumOnFootSkinY;public bool sampledOnFootPenetrationWithinTolerance;
            public float maximumLoopMuscleEndpointDelta;public float maximumLoopBodyEndpointDelta;public float maximumLoopBodyRotationEndpointDegrees;public bool loopCurveEndpointsContinuous;
            public string loopLimit="Endpoint curve continuity only. Rendered temporal seam, sliding and support-phase continuity are NOT accepted by this metric.";
            public float maximumActorRootTranslation;public float maximumActorRootRotationDegrees;public bool actorRootStayedInPlace;
            public string palmRoll="UNVERIFIED: no articulated fingers/palm markers; recorded joint axes do not prove anatomical palm roll.";
            public string contactCleanup="NOT_APPLIED; boot pitch/sole contact are target presentation obligations, independent of source attack ownership.";
            public string suppliedLocomotion="This action audit does not correct supplied Walking/Running; actual Windows raw locomotion comparison is separate evidence.";
            public string mirrorFeasibility="Unity6000.3 AnimationClipSettings.mirror / AnimatorState.mirror can form a separate documented Humanoid candidate; not executed here. Mirroring changes side, not missing torso intent or contact.";
        }
        [Serializable] public sealed class Manifest
        {
            public int version=1;public bool includesScenarioC;public string method="Measured same-hierarchy gravity-upright T-reference on disposable clones. Target mapping unchanged. Source C candidate swaps HumanDescription side assignments ONLY after independent physical-forward contradiction is measured; original names/hierarchy/assets remain unchanged. No root rotation or choreography mirror. Runtime bind restored after Avatar assignment.";
            public string limitations="Existing FOC actions are converted diagnostic Transform clips, not production Humanoid/mocap. No choreography edits, foot offsets, muscle clamps, contact IK, grip corrections or garment edits.";
            public InputHash[] inputs=Array.Empty<InputHash>();public InputHash[] outputs=Array.Empty<InputHash>();public RigAudit[] rigs=Array.Empty<RigAudit>();
        }
        [Serializable] public sealed class Report
        {
            public string status="NOT_COMPLETED";public string operation="";public string utc="";public string unityVersion="";public string error="";
            public string acceptance="MEASUREMENT ONLY. Same-frame Windows A/B/C visual acceptance is separate. No automatically selected winning Avatar.";
            public string[] jointOrder=JointOrder.Select(b=>b.ToString()).ToArray();public string[] limbOrder=LimbStarts.Select((b,i)=>b+" -> "+LimbEnds[i]).ToArray();
            public bool inputAssetsUnchanged;public RigAudit[] rigs=Array.Empty<RigAudit>();public ScenarioAudit[] scenarios=Array.Empty<ScenarioAudit>();public QualityDiagnostic[] qualityDiagnostics=Array.Empty<QualityDiagnostic>();
        }

        public static string CandidateClipPath(string motion)=>OutputRoot+"/ScenarioC/ANM_HumanoidCandidate_"+motion+".anim";
        public static Avatar LoadTargetAvatar()=>Load<Avatar>(TargetAvatarPath);
        public static Avatar LoadSourceAvatar()=>Load<Avatar>(SourceAvatarPath);
        public static Avatar TargetAvatarFor(string scenario)=>scenario=="A"?Load<GameObject>(MeshyHasanPilotPipeline.PrefabPath).GetComponent<Animator>().avatar:scenario=="B"||scenario=="C"?LoadTargetAvatar():throw new ArgumentException("Unknown scenario "+scenario);
        public static AnimationClip[] LoadClips(string scenario)
        {
            Require(scenario=="A"||scenario=="B"||scenario=="C","Unknown calibration scenario.");
            return scenario=="C"?MeshyHasanPilotMotionAdaptation.MotionNames.Select(m=>Load<AnimationClip>(CandidateClipPath(m))).ToArray():MeshyHasanPilotMotionAdaptation.LoadClips();
        }
        public static void Run()=>Batch(()=>Generate());
        public static void RunBoth()=>Batch(()=>GenerateBoth());
        public static void RunVerify()=>Batch(()=>Verify());
        private static void Batch(Func<Report> action){try{action();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static Report Generate()=>GenerateInternal(false);
        /// <summary>Explicit second experiment; call only after reviewing A/B.</summary>
        public static Report GenerateBoth()=>GenerateInternal(true);

        private static Report GenerateInternal(bool both)
        {
            var report=NewReport(both?"GenerateABC":"GenerateAB");
            var inputs=RecordInputs();
            try
            {
                Directory.CreateDirectory(OutputRoot);AssetDatabase.Refresh();
                var source=Load<GameObject>(MeshyHasanPilotMotionAdaptation.DonorPrefabPath);var target=Load<GameObject>(MeshyHasanPilotPipeline.PrefabPath);
                var rigs=new List<RigAudit>{AuditRig(source,"OriginalSource",MeshyHasanPilotMotionAdaptation.DonorPrefabPath),AuditRig(target,"OriginalTarget",MeshyHasanPilotPipeline.PrefabPath)};
                rigs.Add(BuildCandidate(target,"CalibratedTarget",MeshyHasanPilotPipeline.PrefabPath,TargetAvatarPath));
                if(both)
                {
                    rigs.Add(BuildCandidate(source,"CalibratedSource",MeshyHasanPilotMotionAdaptation.DonorPrefabPath,SourceAvatarPath));
                    Directory.CreateDirectory(OutputRoot+"/ScenarioC");AssetDatabase.Refresh();
                    foreach(var motion in MeshyHasanPilotMotionAdaptation.MotionNames)
                    {
                        var clip=ConvertWithAvatar(source,LoadSourceAvatar(),Load<AnimationClip>(MeshyHasanPilotMotionAdaptation.SourceClipPath(motion)),motion);
                        try{Store(clip,CandidateClipPath(motion));}finally{if(!AssetDatabase.Contains(clip))Object.DestroyImmediate(clip);}
                    }
                }
                AssetDatabase.SaveAssets();
                var outputs=new[]{TargetAvatarPath}.Concat(both?new[]{SourceAvatarPath}.Concat(MeshyHasanPilotMotionAdaptation.MotionNames.Select(CandidateClipPath)):Array.Empty<string>()).Select(Hash).ToArray();
                var manifest=new Manifest{includesScenarioC=both,inputs=inputs,outputs=outputs,rigs=rigs.ToArray()};
                CheckHashes(inputs);File.WriteAllText(ManifestPath,JsonUtility.ToJson(manifest,true));AssetDatabase.ImportAsset(ManifestPath);AssetDatabase.SaveAssets();
                AuditScenarios(report,manifest);report.inputAssetsUnchanged=true;report.status="CALIBRATION_EXPERIMENT_COMPLETE_VISUAL_ACCEPTANCE_PENDING";return report;
            }
            catch(Exception e){report.error=e.ToString();throw;}
            finally{WriteReport(report);}
        }
        /// <summary>No asset generation or mutation. Writes external JSON evidence only.</summary>
        public static Report Verify()
        {
            var report=NewReport("Verify");
            try
            {
                var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath))??throw new InvalidOperationException("Missing calibration provenance.");Require(manifest.version==1,"Unknown calibration provenance.");
                CheckHashes(manifest.inputs);CheckHashes(manifest.outputs);AuditScenarios(report,manifest);CheckHashes(manifest.inputs);
                report.inputAssetsUnchanged=true;report.status="CALIBRATION_EXPERIMENT_COMPLETE_VISUAL_ACCEPTANCE_PENDING";return report;
            }
            catch(Exception e){report.error=e.ToString();throw;}
            finally{WriteReport(report);}
        }

        private static RigAudit BuildCandidate(GameObject prefab,string role,string prefabPath,string output)
        {
            var clone=Clone(prefab);Avatar? generated=null;
            try
            {
                var animator=clone.GetComponent<Animator>();var original=animator.avatar;var description=original.humanDescription;var map=Map(animator);
                var before=new PoseBackup(clone);var originalFrame=AnatomicalFrame(map);var physical=AuditForward(clone,map,originalFrame);var mappingChanges=new List<MappingChange>();
                if(role=="CalibratedSource")
                {
                    // This donor's NAME-labelled left is physically right.
                    // Correct ONLY the separate candidate's semantic mapping;
                    // do not rename bones, mirror poses, or disguise the fact
                    // that Hand_R action now belongs to HumanLeftHand.
                    Require(physical.sideConventionContradictsPhysicalForward,"C source mapping correction requires measured physical-front reflection, not a name heuristic.");
                    description.human=description.human.Select(h=>
                    {
                        var corrected=h;var opposite=h.humanName.StartsWith("Left",StringComparison.Ordinal)?"Right"+h.humanName.Substring(4):h.humanName.StartsWith("Right",StringComparison.Ordinal)?"Left"+h.humanName.Substring(5):h.humanName;
                        if(opposite!=h.humanName){Require(description.human.Any(p=>p.humanName==opposite),"Cannot correct an unpaired Human side mapping.");corrected.humanName=opposite;mappingChanges.Add(new MappingChange{bone=h.boneName,originalHuman=h.humanName,candidateHuman=opposite});}return corrected;
                    }).ToArray();
                    map=MapDescription(clone,description);
                }
                var initialRotations=map.ToDictionary(p=>p.Key,p=>p.Value.localRotation);var footRotations=new[]{map[HumanBodyBones.LeftFoot].rotation,map[HumanBodyBones.RightFoot].rotation};var headRotation=map[HumanBodyBones.Head].rotation;
                var originalToePitches=map.ContainsKey(HumanBodyBones.LeftToes)&&map.ContainsKey(HumanBodyBones.RightToes)?new[]{Pitch(map[HumanBodyBones.LeftToes].position-map[HumanBodyBones.LeftFoot].position,clone.transform.up),Pitch(map[HumanBodyBones.RightToes].position-map[HumanBodyBones.RightFoot].position,clone.transform.up)}:Array.Empty<float>();
                // Gravity is root/world up (root identity is checked), NOT the
                // leaning saved hip->neck vector. Physical forward is measured
                // independently, projected horizontally, never a blind yaw.
                var up=clone.transform.up;var forward=Vector3.ProjectOnPlane(physical.physicalForward,up).normalized;var canonicalLeft=Vector3.Cross(forward,up).normalized;
                Require(forward.sqrMagnitude>.99f&&canonicalLeft.sqrMagnitude>.99f,"Cannot establish physical canonical horizontal basis.");
                Require(Vector3.Dot(map[HumanBodyBones.LeftUpperArm].position-map[HumanBodyBones.RightUpperArm].position,canonicalLeft)>0,"Human sides still contradict physical front; stop instead of flipping root.");
                var frame=(left:canonicalLeft,up:up,forward:forward);
                // No root/chest world flip: only the named existing segment's
                // swing changes, using the measured rig frame and minimum arc.
                // Authored axial twist is retained by FromToRotation, not reset.
                Aim(map,HumanBodyBones.Hips,HumanBodyBones.Spine,frame.up);
                Aim(map,HumanBodyBones.Spine,HumanBodyBones.Chest,frame.up);
                Aim(map,HumanBodyBones.Chest,HumanBodyBones.Neck,frame.up);
                Aim(map,HumanBodyBones.Neck,HumanBodyBones.Head,frame.up);
                foreach(var left in new[]{true,false})
                {
                    var upper=left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm;var lower=left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm;var hand=left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand;
                    var direction=frame.left*(left?1f:-1f);Aim(map,upper,lower,direction);Aim(map,lower,hand,direction);
                    var thigh=left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg;var knee=left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg;var foot=left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot;
                    Aim(map,thigh,knee,-frame.up);Aim(map,knee,foot,-frame.up);
                    var toes=left?HumanBodyBones.LeftToes:HumanBodyBones.RightToes;
                    // Ankle->toe is anatomically SLOPED (~41 degrees down on
                    // this source); flattening that bone produced artificial
                    // toe-up boots in the first candidate. Restore true bind
                    // world sole pitch/roll, then align heading by YAW only.
                    // Never treat a lower toe joint as a horizontal foot axis.
                    map[foot].rotation=footRotations[left?0:1];
                    if(map.ContainsKey(toes))
                    {
                        var heading=Vector3.ProjectOnPlane(map[toes].position-map[foot].position,frame.up).normalized;
                        Require(heading.sqrMagnitude>.99f,"Toe heading cannot be inferred from a vertical segment.");
                        map[foot].rotation=Quaternion.FromToRotation(heading,frame.forward)*map[foot].rotation;
                    }
                }
                var head=map[HumanBodyBones.Head];var headEnd=clone.GetComponentsInChildren<Transform>(true).SingleOrDefault(t=>t.name=="head_end");var headFront=clone.GetComponentsInChildren<Transform>(true).SingleOrDefault(t=>t.name=="headfront");
                if(headEnd!=null&&headFront!=null)
                {
                    var authoredForward=(headFront.position-head.position).normalized;var authoredUp=(headEnd.position-head.position).normalized;
                    head.rotation=Quaternion.LookRotation(frame.forward,frame.up)*Quaternion.Inverse(Quaternion.LookRotation(authoredForward,authoredUp))*head.rotation;
                }
                else head.rotation=headRotation;
                before.AssertTranslationsAndScales();
                var alignment=MeasureReferenceAlignment(clone,map,frame,originalToePitches);Require(alignment.maximumSegmentDirectionError<.1f,"Canonical reference is not horizontally armed / vertically legged.");
                Require(alignment.ankleToToePitchDriftDegrees.All(d=>d<.1f),"Reference unexpectedly changed measured bind foot pitch.");
                var candidateReference=AuditBones(clone,description);var deltas=map.Select(p=>new ReferenceDelta{path=PathOf(p.Value,clone.transform),originalLocalRotation=initialRotations[p.Key],candidateLocalRotation=p.Value.localRotation,degrees=Quaternion.Angle(initialRotations[p.Key],p.Value.localRotation)}).ToArray();
                description.skeleton=clone.GetComponentsInChildren<Transform>(true).Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray();
                // Copy all original limits/twist/stretch/translation settings.
                // No limit changes are used to hide extreme source poses.
                generated=AvatarBuilder.BuildHumanAvatar(clone,description);if(generated==null||!generated.isValid||!generated.isHuman)throw new InvalidOperationException("Candidate Avatar invalid: "+role);generated.name=Path.GetFileNameWithoutExtension(output);
                Store(generated,output);var saved=Load<Avatar>(output);animator.avatar=saved;before.Restore();animator.enabled=false;
                var audit=AuditInstance(clone,role,prefabPath);audit.referenceBones=candidateReference;audit.referenceDeltas=deltas;audit.alignment=alignment;audit.mappingChanges=mappingChanges.ToArray();
                if(mappingChanges.Count>0)audit.attackOwnership="Measured correction: original Hand_R now maps HumanLeftHand. The original Hand_R attack is therefore anatomically LEFT-handed. No choreography mirror or right-hand production claim; weapon-side acceptance remains separate.";
                return audit;
            }
            finally{Object.DestroyImmediate(clone);if(generated!=null&&!AssetDatabase.Contains(generated))Object.DestroyImmediate(generated);}
        }
        private static void Aim(Dictionary<HumanBodyBones,Transform> map,HumanBodyBones start,HumanBodyBones end,Vector3 direction)
        {
            if(!map.TryGetValue(start,out var from)||!map.TryGetValue(end,out var to))return;
            var segment=to.position-from.position;Require(segment.magnitude>.00001f,"Zero length mapped segment "+start);
            from.rotation=Quaternion.FromToRotation(segment.normalized,direction.normalized)*from.rotation;
        }
        private static ReferenceAlignment MeasureReferenceAlignment(GameObject clone,Dictionary<HumanBodyBones,Transform> map,(Vector3 left,Vector3 up,Vector3 forward) frame,float[] originalToePitches)
        {
            var errors=new float[LimbStarts.Length];
            for(var i=0;i<errors.Length;i++)
            {
                var direction=(map[LimbEnds[i]].position-map[LimbStarts[i]].position).normalized;var expected=i<2?frame.left:i<4?-frame.left:-frame.up;errors[i]=Vector3.Angle(direction,expected);
            }
            var alignment=new ReferenceAlignment{gravityUp=frame.up,physicalForward=frame.forward,anatomicalLeft=frame.left,segmentDirectionErrors=errors,maximumSegmentDirectionError=errors.Max()};
            if(map.TryGetValue(HumanBodyBones.LeftToes,out var leftToes)&&map.TryGetValue(HumanBodyBones.RightToes,out var rightToes))
            {
                var leftDirection=leftToes.position-map[HumanBodyBones.LeftFoot].position;var rightDirection=rightToes.position-map[HumanBodyBones.RightFoot].position;
                alignment.measuredToes=true;alignment.toeForwardErrors=new[]{Vector3.Angle(Vector3.ProjectOnPlane(leftDirection,frame.up),frame.forward),Vector3.Angle(Vector3.ProjectOnPlane(rightDirection,frame.up),frame.forward)};
                alignment.originalAnkleToToePitchDegrees=originalToePitches;alignment.referenceAnkleToToePitchDegrees=new[]{Pitch(leftDirection,frame.up),Pitch(rightDirection,frame.up)};alignment.ankleToToePitchDriftDegrees=originalToePitches.Select((p,i)=>Mathf.Abs(p-alignment.referenceAnkleToToePitchDegrees[i])).ToArray();
            }
            var all=clone.GetComponentsInChildren<Transform>(true);var headEnd=all.SingleOrDefault(t=>t.name=="head_end");var headFront=all.SingleOrDefault(t=>t.name=="headfront");
            if(headEnd!=null&&headFront!=null)
            {
                var headUp=headEnd.position-map[HumanBodyBones.Head].position;var headForward=headFront.position-map[HumanBodyBones.Head].position;
                alignment.measuredHeadMarkers=true;alignment.headUpError=Vector3.Angle(headUp,frame.up);alignment.headForwardError=Vector3.Angle(headForward,frame.forward);alignment.headMarkersNonorthogonalityDegrees=Mathf.Abs(90f-Vector3.Angle(headUp,headForward));alignment.orthogonalHeadUpError=Vector3.Angle(Vector3.ProjectOnPlane(headUp,headForward.normalized),frame.up);
            }
            return alignment;
        }

        private static RigAudit AuditRig(GameObject prefab,string role,string path)
        {
            var clone=Clone(prefab);try{return AuditInstance(clone,role,path);}finally{Object.DestroyImmediate(clone);}
        }
        private static RigAudit AuditInstance(GameObject instance,string role,string path)
        {
            var animator=instance.GetComponent<Animator>();RequireHuman(animator.avatar);var frame=AnatomicalFrame(Map(animator));
            var audit=new RigAudit{role=role,prefab=path,avatar=AssetDatabase.GetAssetPath(animator.avatar),isValid=animator.avatar.isValid,isHuman=animator.avatar.isHuman,humanScale=animator.humanScale,measuredLeft=frame.left,measuredUp=frame.up,measuredForward=frame.forward,physicalForward=AuditForward(instance,Map(animator),frame),mappedBones=AuditBones(instance,animator.avatar),rest=Snapshot(instance)};
            var backup=new PoseBackup(instance);
            try
            {
                using(var handler=new HumanPoseHandler(animator.avatar,instance.transform))
                {
                    var pose=new HumanPose{muscles=new float[HumanTrait.MuscleCount]};handler.GetHumanPose(ref pose);ValidatePose(pose);audit.originalNeutralBodyPosition=pose.bodyPosition;audit.originalNeutralBodyRotation=pose.bodyRotation;Array.Clear(pose.muscles,0,pose.muscles.Length);pose.bodyPosition=new Vector3(0,pose.bodyPosition.y,0);pose.bodyRotation=Quaternion.identity;
                    handler.SetHumanPose(ref pose);audit.zeroMuscle=Snapshot(instance);audit.zeroMuscle.bodyPosition=pose.bodyPosition;audit.zeroMuscle.bodyRotation=pose.bodyRotation;
                }
            }
            finally{backup.Restore();}
            return audit;
        }
        private static BoneAudit[] AuditBones(GameObject instance,Avatar avatar)
            =>AuditBones(instance,avatar.humanDescription);
        private static BoneAudit[] AuditBones(GameObject instance,HumanDescription description)
        {
            var all=instance.GetComponentsInChildren<Transform>(true);
            return description.human.Select(h=>
            {
                var t=all.SingleOrDefault(x=>x.name==h.boneName)??throw new InvalidOperationException("Nonunique/missing mapped bone: "+h.boneName);
                return new BoneAudit{humanBone=h.humanName,bone=t.name,path=PathOf(t,instance.transform),parent=t.parent==null?"":PathOf(t.parent,instance.transform),localPosition=t.localPosition,localRotation=t.localRotation,localScale=t.localScale,worldPosition=t.position,worldRotation=t.rotation,localAxisX=t.localRotation*Vector3.right,localAxisY=t.localRotation*Vector3.up,localAxisZ=t.localRotation*Vector3.forward,worldAxisX=t.right,worldAxisY=t.up,worldAxisZ=t.forward,limit=h.limit};
            }).ToArray();
        }
        private static void AuditScenarios(Report report,Manifest manifest)
        {
            // Remeasure current assets even in clean-SHA Verify. Authored
            // reference deltas/alignment are immutable provenance (their Avatar
            // outputs are hashed), but rest/zero pose evidence is always fresh.
            var freshRigs=new List<RigAudit>();
            foreach(var recorded in manifest.rigs)
            {
                var clone=Clone(Load<GameObject>(recorded.prefab),Load<Avatar>(recorded.avatar));
                try
                {
                    var fresh=AuditInstance(clone,recorded.role,recorded.prefab);fresh.referenceBones=recorded.referenceBones;fresh.referenceDeltas=recorded.referenceDeltas;fresh.mappingChanges=recorded.mappingChanges;fresh.attackOwnership=recorded.attackOwnership;fresh.alignment=recorded.alignment;freshRigs.Add(fresh);
                }
                finally{Object.DestroyImmediate(clone);}
            }
            report.rigs=freshRigs.ToArray();var scenarios=new List<ScenarioAudit>();
            foreach(var scenario in manifest.includesScenarioC?new[]{"A","B","C"}:new[]{"A","B"})scenarios.Add(AuditScenario(scenario));
            report.scenarios=scenarios.ToArray();
            report.qualityDiagnostics=scenarios.Select(s=>EvaluateQuality(report.rigs,s)).ToArray();
            CheckHashes(manifest.inputs);CheckHashes(manifest.outputs);
        }
        private static ScenarioAudit AuditScenario(string scenario)
        {
            var donor=Load<GameObject>(MeshyHasanPilotMotionAdaptation.DonorPrefabPath);var targetPrefab=Load<GameObject>(MeshyHasanPilotPipeline.PrefabPath);
            var sourceAvatar=scenario=="C"?LoadSourceAvatar():donor.GetComponent<Animator>().avatar;var targetAvatar=TargetAvatarFor(scenario);
            var result=new ScenarioAudit{id=scenario,targetAvatar=AssetDatabase.GetAssetPath(targetAvatar),extractionAvatar=AssetDatabase.GetAssetPath(sourceAvatar),clipOrigin=scenario=="C"?"New diagnostic conversion using calibrated source Avatar":"UNCHANGED previously converted FOC diagnostic muscle clips"};
            var samples=new List<Sample>();var clips=LoadClips(scenario);
            for(var index=0;index<MeshyHasanPilotMotionAdaptation.MotionNames.Length;index++)
            {
                var motion=MeshyHasanPilotMotionAdaptation.MotionNames[index];var genericClip=Load<AnimationClip>(MeshyHasanPilotMotionAdaptation.SourceClipPath(motion));
                GameObject? source=null,target=null,same=null;Avatar? generic=null;var graph=default(PlayableGraph);
                try
                {
                    source=Clone(donor);target=Clone(targetPrefab,targetAvatar);same=Clone(donor,sourceAvatar);
                    var sourceAnimator=source.GetComponent<Animator>();var sourceMap=MapDescription(source,sourceAvatar.humanDescription);var targetMap=Map(target.GetComponent<Animator>());var sameMap=Map(same.GetComponent<Animator>());
                    var sourceFrame=AnatomicalFrame(sourceMap);var targetFrame=AnatomicalFrame(targetMap);
                    generic=AvatarBuilder.BuildGenericAvatar(source,"Root");Require(generic!=null&&generic.isValid,"Generic source context invalid.");sourceAnimator.avatar=generic;sourceAnimator.Rebind();var reset=new PoseBackup(source);
                    var playable=Playback(target,clips[index],out graph);
                    using(var reader=new HumanPoseHandler(sourceAvatar,source.transform))using(var writer=new HumanPoseHandler(sourceAvatar,same.transform))
                    foreach(var phase in Phases)
                    {
                        reset.Restore();genericClip.SampleAnimation(source,genericClip.length*phase);
                        var pose=new HumanPose{muscles=new float[HumanTrait.MuscleCount]};reader.GetHumanPose(ref pose);ValidatePose(pose);writer.SetHumanPose(ref pose);
                        playable.SetTime(clips[index].length*phase);graph.Evaluate(0);
                        var sourceSample=Snapshot(source,sourceMap);sourceSample.bodyPosition=pose.bodyPosition;sourceSample.bodyRotation=pose.bodyRotation;
                        var targetSample=Snapshot(target,targetMap);var directionErrors=new float[LimbStarts.Length];
                        for(var limb=0;limb<LimbStarts.Length;limb++)
                        {
                            var a=ToFrame((sourceMap[LimbEnds[limb]].position-sourceMap[LimbStarts[limb]].position).normalized,sourceFrame);
                            var b=ToFrame((targetMap[LimbEnds[limb]].position-targetMap[LimbStarts[limb]].position).normalized,targetFrame);
                            directionErrors[limb]=Vector3.Angle(a,b);
                        }
                        var sameError=JointOrder.Where(b=>sourceMap.ContainsKey(b)&&sameMap.ContainsKey(b)).Max(b=>Vector3.Distance(sourceMap[b].position,sameMap[b].position));
                        samples.Add(new Sample{scenario=scenario,motion=motion,phase=phase,source=sourceSample,target=targetSample,limbDirectionErrorDegrees=directionErrors,sameSourceAvatarRoundtripMaximumPosition=sameError,maximumAbsoluteMuscle=pose.muscles.Max(v=>Mathf.Abs(v)),outsideNominalMuscles=pose.muscles.Select((v,i)=>new{v,i}).Where(p=>Mathf.Abs(p.v)>1.0001f).Select(p=>HumanTrait.MuscleName[p.i]+"="+p.v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).ToArray(),sourceLeftAnkleHeight=sourceMap[HumanBodyBones.LeftFoot].position.y,targetLeftAnkleHeight=targetMap[HumanBodyBones.LeftFoot].position.y,sourceRightAnkleHeight=sourceMap[HumanBodyBones.RightFoot].position.y,targetRightAnkleHeight=targetMap[HumanBodyBones.RightFoot].position.y});
                    }
                }
                finally{if(graph.IsValid())graph.Destroy();if(source!=null)Object.DestroyImmediate(source);if(target!=null)Object.DestroyImmediate(target);if(same!=null)Object.DestroyImmediate(same);if(generic!=null)Object.DestroyImmediate(generic);}
            }
            result.samples=samples.ToArray();result.meanLimbDirectionErrorDegrees=samples.SelectMany(s=>s.limbDirectionErrorDegrees).Average();result.maximumLimbDirectionErrorDegrees=samples.SelectMany(s=>s.limbDirectionErrorDegrees).Max();result.minimumSkinY=samples.Min(s=>s.target.skinMinimum.y);return result;
        }
        private static QualityDiagnostic EvaluateQuality(RigAudit[] rigs,ScenarioAudit scenario)
        {
            var source=rigs.Single(r=>r.role==(scenario.id=="C"?"CalibratedSource":"OriginalSource"));var target=rigs.Single(r=>r.role==(scenario.id=="A"?"OriginalTarget":"CalibratedTarget"));
            var q=new QualityDiagnostic{scenario=scenario.id};var failures=new List<string>();
            q.physicalSourceSidesConsistent=source.physicalForward.mappedForwardDotPhysical>.8f;q.physicalTargetSidesConsistent=target.physicalForward.mappedForwardDotPhysical>.8f;
            q.neutralHandSidesConsistent=NeutralHandsOnPhysicalSides(source)&&NeutralHandsOnPhysicalSides(target);
            for(var i=0;i<LimbStarts.Length;i++)
            {
                var a=PoseJoint(source.zeroMuscle,LimbEnds[i])-PoseJoint(source.zeroMuscle,LimbStarts[i]);var b=PoseJoint(target.zeroMuscle,LimbEnds[i])-PoseJoint(target.zeroMuscle,LimbStarts[i]);var error=Vector3.Angle(a,b);
                if(i<4)q.neutralArmDirectionMaximumDegrees=Mathf.Max(q.neutralArmDirectionMaximumDegrees,error);else q.neutralLegDirectionMaximumDegrees=Mathf.Max(q.neutralLegDirectionMaximumDegrees,error);
            }
            var footErrors=new List<float>();
            foreach(var foot in new[]{HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot})
            {
                // Compare physical long-foot axes transformed from each TRUE
                // bind into zero pose; do not compare ankle->toe slopes against
                // a missing source toe or infer a sole from a bone name.
                var index=Array.IndexOf(JointOrder,foot);
                var sourceFoot=source.physicalForward.feet.Single(f=>f.humanBone==foot.ToString());var targetFoot=target.physicalForward.feet.Single(f=>f.humanBone==foot.ToString());Require(sourceFoot.available&&targetFoot.available,"Per-foot independent heading missing; do not substitute the averaged actor front.");
                var a=TransformFootHeading(sourceFoot.bindHorizontalHeading,source.rest.jointRotations[index],source.zeroMuscle.jointRotations[index]);var b=TransformFootHeading(targetFoot.bindHorizontalHeading,target.rest.jointRotations[index],target.zeroMuscle.jointRotations[index]);var difference=Vector3.Angle(a,b);footErrors.Add(difference);q.neutralPhysicalFootDirectionMaximumDegrees=Mathf.Max(q.neutralPhysicalFootDirectionMaximumDegrees,difference);
                var oldA=TransformFootHeading(source.physicalForward.physicalForward,source.rest.jointRotations[index],source.zeroMuscle.jointRotations[index]);var oldB=TransformFootHeading(target.physicalForward.physicalForward,target.rest.jointRotations[index],target.zeroMuscle.jointRotations[index]);q.legacyAveragedFootAxisDifferenceDegrees=Mathf.Max(q.legacyAveragedFootAxisDifferenceDegrees,Vector3.Angle(oldA,oldB));
            }
            q.perFootNeutralDirectionDifferencesDegrees=footErrors.ToArray();
            foreach(var hand in new[]{HumanBodyBones.LeftHand,HumanBodyBones.RightHand})
            {
                var a=(PoseJoint(source.zeroMuscle,hand)-PoseJoint(source.zeroMuscle,HumanBodyBones.Hips))/source.humanScale;var b=(PoseJoint(target.zeroMuscle,hand)-PoseJoint(target.zeroMuscle,HumanBodyBones.Hips))/target.humanScale;
                q.neutralWristPositionMaximumNormalized=Mathf.Max(q.neutralWristPositionMaximumNormalized,Vector3.Distance(a,b));
            }
            var attack=scenario.samples.Where(s=>s.motion=="OneHandedAttack").ToArray();
            q.sourceAttackLeftWristTravel=JointTravel(attack.Select(s=>s.source).ToArray(),HumanBodyBones.LeftHand);q.sourceAttackRightWristTravel=JointTravel(attack.Select(s=>s.source).ToArray(),HumanBodyBones.RightHand);
            q.targetAttackLeftWristTravel=JointTravel(attack.Select(s=>s.target).ToArray(),HumanBodyBones.LeftHand);q.targetAttackRightWristTravel=JointTravel(attack.Select(s=>s.target).ToArray(),HumanBodyBones.RightHand);
            q.intendedRightHandAttackDominates=q.physicalSourceSidesConsistent&&q.physicalTargetSidesConsistent&&q.sourceAttackRightWristTravel>2*q.sourceAttackLeftWristTravel&&q.targetAttackRightWristTravel>2*q.targetAttackLeftWristTravel;
            foreach(var bone in new[]{HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.Chest})
            {
                var index=Array.IndexOf(JointOrder,bone);q.sourceAttackTorsoMaximumTravel=Mathf.Max(q.sourceAttackTorsoMaximumTravel,JointTravel(attack.Select(s=>s.source).ToArray(),bone));
                foreach(var sample in attack)q.sourceAttackTorsoMaximumRotationDegrees=Mathf.Max(q.sourceAttackTorsoMaximumRotationDegrees,Quaternion.Angle(attack[0].source.jointRotations[index],sample.source.jointRotations[index]));
            }
            q.hasAuthoredAttackTorsoIntent=q.sourceAttackTorsoMaximumRotationDegrees>.1f||q.sourceAttackTorsoMaximumTravel>.001f;
            q.minimumOnFootSkinY=scenario.samples.Where(s=>s.motion!="MountedSeated").Min(s=>s.target.skinMinimum.y);q.sampledOnFootPenetrationWithinTolerance=q.minimumOnFootSkinY>=q.reviewFloorY-q.penetrationToleranceMeters;
            q.actionDirectionMaximumDegrees=scenario.maximumLimbDirectionErrorDegrees;
            foreach(var clip in LoadClips(scenario.id))
            {
                var bindings=AnimationUtility.GetCurveBindings(clip);var muscleNames=new HashSet<string>(HumanTrait.MuscleName,StringComparer.Ordinal);
                foreach(var binding in bindings.Where(b=>muscleNames.Contains(b.propertyName))){var c=AnimationUtility.GetEditorCurve(clip,binding);q.maximumLoopMuscleEndpointDelta=Mathf.Max(q.maximumLoopMuscleEndpointDelta,Mathf.Abs(c.Evaluate(0)-c.Evaluate(clip.length)));}
                float V(string name,float time)=>AnimationUtility.GetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),name)).Evaluate(time);
                Vector3 P(float time)=>new Vector3(V("RootT.x",time),V("RootT.y",time),V("RootT.z",time));
                Quaternion R(float time)=>new Quaternion(V("RootQ.x",time),V("RootQ.y",time),V("RootQ.z",time),V("RootQ.w",time));
                q.maximumLoopBodyEndpointDelta=Mathf.Max(q.maximumLoopBodyEndpointDelta,Vector3.Distance(P(0),P(clip.length))*target.humanScale);q.maximumLoopBodyRotationEndpointDegrees=Mathf.Max(q.maximumLoopBodyRotationEndpointDegrees,Quaternion.Angle(R(0),R(clip.length)));
            }
            q.loopCurveEndpointsContinuous=q.maximumLoopMuscleEndpointDelta<.001f&&q.maximumLoopBodyEndpointDelta<.001f&&q.maximumLoopBodyRotationEndpointDegrees<.1f;
            q.maximumActorRootTranslation=scenario.samples.Max(s=>s.target.rootWorldPosition.magnitude);q.maximumActorRootRotationDegrees=scenario.samples.Max(s=>Quaternion.Angle(s.target.rootWorldRotation,Quaternion.identity));q.actorRootStayedInPlace=q.maximumActorRootTranslation<.00001f&&q.maximumActorRootRotationDegrees<.01f;
            if(!q.physicalSourceSidesConsistent||!q.physicalTargetSidesConsistent)failures.Add("PHYSICAL_HUMAN_SIDE_CONVENTION");
            if(!q.neutralHandSidesConsistent||q.neutralArmDirectionMaximumDegrees>q.neutralDirectionReviewToleranceDegrees||q.neutralLegDirectionMaximumDegrees>q.neutralDirectionReviewToleranceDegrees||q.neutralPhysicalFootDirectionMaximumDegrees>q.neutralDirectionReviewToleranceDegrees)failures.Add("ZERO_MUSCLE_REFERENCE_ORIENTATION");
            if(q.actionDirectionMaximumDegrees>q.actionDirectionReviewToleranceDegrees)failures.Add("ACTION_LIMB_DIRECTION_DIVERGENCE");
            if(!q.intendedRightHandAttackDominates)failures.Add("REQUIRED_RIGHT_HAND_ATTACK_OWNERSHIP");if(!q.hasAuthoredAttackTorsoIntent)failures.Add("SOURCE_ATTACK_HAS_NO_AUTHORED_TORSO_INTENT");
            if(!q.sampledOnFootPenetrationWithinTolerance)failures.Add("SAMPLED_ON_FOOT_CONTACT_PENETRATION");if(!q.loopCurveEndpointsContinuous)failures.Add("LOOP_CURVE_ENDPOINT_CONTINUITY");if(!q.actorRootStayedInPlace)failures.Add("EXTERNAL_ACTOR_ROOT_MOVED");
            q.failedChecks=failures.ToArray();q.status=failures.Count>0?"QUALITY_DIAGNOSTICS_FAIL":"NUMERIC_REVIEW_PASSED_VISUAL_AND_PALM_QA_PENDING";return q;
        }
        private static bool NeutralHandsOnPhysicalSides(RigAudit rig)
        {
            var left=Vector3.Cross(rig.physicalForward.physicalForward,Vector3.up).normalized;var hips=PoseJoint(rig.zeroMuscle,HumanBodyBones.Hips);
            return Vector3.Dot(PoseJoint(rig.zeroMuscle,HumanBodyBones.LeftHand)-hips,left)>0&&Vector3.Dot(PoseJoint(rig.zeroMuscle,HumanBodyBones.RightHand)-hips,left)<0;
        }
        private static Vector3 PoseJoint(PoseSample sample,HumanBodyBones bone)=>sample.joints[Array.IndexOf(JointOrder,bone)];
        private static float JointTravel(PoseSample[] samples,HumanBodyBones bone)
        {
            var maximum=0f;for(var i=0;i<samples.Length;i++)for(var j=0;j<i;j++)maximum=Mathf.Max(maximum,Vector3.Distance(PoseJoint(samples[i],bone),PoseJoint(samples[j],bone)));return maximum;
        }
        private static float Pitch(Vector3 direction,Vector3 up)=>Mathf.Atan2(Vector3.Dot(direction,up),Vector3.ProjectOnPlane(direction,up).magnitude)*Mathf.Rad2Deg;
        /// <summary>Foot-specific axis; never replace both bind headings with their average.</summary>
        public static Vector3 TransformFootHeading(Vector3 individualBindHeading,Quaternion bindRotation,Quaternion sampledRotation)
        {
            var heading=Vector3.ProjectOnPlane(individualBindHeading,Vector3.up).normalized;Require(heading.sqrMagnitude>.99f,"Missing horizontal physical foot heading.");return(sampledRotation*Quaternion.Inverse(bindRotation)*heading).normalized;
        }

        private static AnimationClip ConvertWithAvatar(GameObject prefab,Avatar humanAvatar,AnimationClip sourceClip,string motion)
        {
            MeshyHasanPilotMotionAdaptation.ValidateGenericDiagnostic(sourceClip);var source=Clone(prefab);Avatar? generic=null;AnimationClip? result=null;
            try
            {
                var animator=source.GetComponent<Animator>();generic=AvatarBuilder.BuildGenericAvatar(source,"Root");Require(generic!=null&&generic.isValid,"Cannot initialize generic donor.");animator.avatar=generic;animator.Rebind();var reset=new PoseBackup(source);
                var names=HumanTrait.MuscleName.Concat(new[]{"RootT.x","RootT.y","RootT.z","RootQ.x","RootQ.y","RootQ.z","RootQ.w"}).ToArray();var keys=names.Select(_=>new List<Keyframe>()).ToArray();var count=Mathf.CeilToInt(sourceClip.length*60);var previous=Quaternion.identity;
                using(var handler=new HumanPoseHandler(humanAvatar,source.transform))for(var frame=0;frame<=count;frame++)
                {
                    reset.Restore();var time=sourceClip.length*frame/count;sourceClip.SampleAnimation(source,time);var pose=new HumanPose{muscles=new float[HumanTrait.MuscleCount]};handler.GetHumanPose(ref pose);ValidatePose(pose);
                    if(frame>0&&Quaternion.Dot(previous,pose.bodyRotation)<0f)pose.bodyRotation=new Quaternion(-pose.bodyRotation.x,-pose.bodyRotation.y,-pose.bodyRotation.z,-pose.bodyRotation.w);previous=pose.bodyRotation;
                    // GetHumanPose center of mass is ALREADY / humanScale.
                    // Identity clone root permits direct normalized RootT.
                    var values=pose.muscles.Concat(new[]{pose.bodyPosition.x,pose.bodyPosition.y,pose.bodyPosition.z,pose.bodyRotation.x,pose.bodyRotation.y,pose.bodyRotation.z,pose.bodyRotation.w}).ToArray();
                    for(var i=0;i<values.Length;i++)keys[i].Add(new Keyframe(time,values[i]));
                }
                result=new AnimationClip{name="ANM_HumanoidCandidate_"+motion,frameRate=60,legacy=false};
                for(var i=0;i<names.Length;i++)
                {
                    var curve=new AnimationCurve(keys[i].ToArray());for(var k=0;k<curve.length;k++){AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.Linear);}
                    AnimationUtility.SetEditorCurve(result,EditorCurveBinding.FloatCurve("",typeof(Animator),names[i]),curve);
                }
                var settings=AnimationUtility.GetAnimationClipSettings(result);settings.loopTime=true;settings.loopBlend=false;settings.loopBlendPositionY=true;settings.loopBlendPositionXZ=true;settings.loopBlendOrientation=true;settings.keepOriginalPositionY=true;settings.keepOriginalPositionXZ=true;settings.keepOriginalOrientation=true;AnimationUtility.SetAnimationClipSettings(result,settings);
                MeshyHasanPilotMotionAdaptation.AuditClip(result,motion);return result;
            }
            catch{if(result!=null)Object.DestroyImmediate(result);throw;}
            finally{Object.DestroyImmediate(source);if(generic!=null)Object.DestroyImmediate(generic);}
        }

        private static PoseSample Snapshot(GameObject actor,Dictionary<HumanBodyBones,Transform>? knownMap=null)
        {
            var map=knownMap??Map(actor.GetComponent<Animator>());var skin=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.sharedMesh!=null).OrderByDescending(r=>r.sharedMesh.triangles.Length).First();var mesh=new Mesh();
            try
            {
                skin.BakeMesh(mesh);Require(mesh.vertexCount>0,"Calibration skin empty.");var minimum=Vector3.one*float.PositiveInfinity;var maximum=Vector3.one*float.NegativeInfinity;
                foreach(var vertex in mesh.vertices){var p=actor.transform.InverseTransformPoint(skin.transform.TransformPoint(vertex));Require(Finite(p),"Nonfinite calibration skin.");minimum=Vector3.Min(minimum,p);maximum=Vector3.Max(maximum,p);}
                Require((maximum-minimum).magnitude<5f,"Exploded calibration skin.");
                return new PoseSample{joints=JointOrder.Select(b=>map.ContainsKey(b)?actor.transform.InverseTransformPoint(map[b].position):Vector3.zero).ToArray(),jointRotations=JointOrder.Select(b=>map.ContainsKey(b)?Quaternion.Inverse(actor.transform.rotation)*map[b].rotation:Quaternion.identity).ToArray(),jointPaths=JointOrder.Select(b=>map.ContainsKey(b)?PathOf(map[b],actor.transform):"").ToArray(),jointAvailable=JointOrder.Select(map.ContainsKey).ToArray(),skinMinimum=minimum,skinMaximum=maximum,skin=skin.sharedMesh.name,triangles=skin.sharedMesh.triangles.Length/3,rootWorldPosition=actor.transform.position,rootWorldRotation=actor.transform.rotation};
            }
            finally{Object.DestroyImmediate(mesh);}
        }
        private static (Vector3 left,Vector3 up,Vector3 forward) AnatomicalFrame(Dictionary<HumanBodyBones,Transform> map)
        {
            var up=(map[HumanBodyBones.Neck].position-map[HumanBodyBones.Hips].position).normalized;var lateral=map[HumanBodyBones.LeftUpperArm].position-map[HumanBodyBones.RightUpperArm].position;
            var left=Vector3.ProjectOnPlane(lateral,up).normalized;var forward=Vector3.Cross(up,left).normalized;Require(left.sqrMagnitude>.99f&&up.sqrMagnitude>.99f&&forward.sqrMagnitude>.99f,"Degenerate measured mapped frame.");return(left,up,forward);
        }
        private static ForwardAudit AuditForward(GameObject actor,Dictionary<HumanBodyBones,Transform> map,(Vector3 left,Vector3 up,Vector3 forward) frame)
        {
            var audit=new ForwardAudit();
            if(map.TryGetValue(HumanBodyBones.LeftToes,out var leftToes)&&map.TryGetValue(HumanBodyBones.RightToes,out var rightToes))
            {
                audit.hasToeBones=true;audit.toeDirection=Vector3.ProjectOnPlane((leftToes.position-map[HumanBodyBones.LeftFoot].position)+(rightToes.position-map[HumanBodyBones.RightFoot].position),frame.up).normalized;
            }
            var marker=actor.GetComponentsInChildren<Transform>(true).SingleOrDefault(t=>t.name=="headfront");
            if(marker!=null){audit.hasHeadFront=true;audit.headFrontDirection=Vector3.ProjectOnPlane(marker.position-map[HumanBodyBones.Head].position,frame.up).normalized;}
            // Independently measure the weighted sole's horizontal center
            // relative to its ankle. This does not use the misleading L/R
            // suffix, and it is reported separately from actual toe markers.
            var skin=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.sharedMesh!=null).OrderByDescending(r=>r.sharedMesh.triangles.Length).First();var mesh=skin.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;
            var bones=skin.bones;var vector=Vector3.zero;var minimum=Vector3.one*float.PositiveInfinity;var maximum=Vector3.one*float.NegativeInfinity;var footEvidence=new List<FootHeadingEvidence>();
            foreach(var foot in new[]{map[HumanBodyBones.LeftFoot],map[HumanBodyBones.RightFoot]})
            {
                var isLeft=foot==map[HumanBodyBones.LeftFoot];var boneIndex=Array.IndexOf(bones,foot);var points=new List<Vector3>();
                for(var i=0;i<vertices.Length&&i<weights.Length;i++)
                {
                    if(boneIndex<0)break;
                    var w=weights[i];var influence=(w.boneIndex0==boneIndex?w.weight0:0)+(w.boneIndex1==boneIndex?w.weight1:0)+(w.boneIndex2==boneIndex?w.weight2:0)+(w.boneIndex3==boneIndex?w.weight3:0);
                    if(influence<.5f)continue;var relative=skin.transform.TransformPoint(vertices[i])-foot.position;
                    if(Vector3.Dot(relative,frame.up)>.02f)continue;
                    vector+=Vector3.ProjectOnPlane(relative,frame.up);minimum=Vector3.Min(minimum,relative);maximum=Vector3.Max(maximum,relative);audit.weightedSoleVertices++;points.Add(relative);
                }
                var evidence=MeasureSoleHeading(points,isLeft?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                var toe=isLeft?HumanBodyBones.LeftToes:HumanBodyBones.RightToes;
                if(map.TryGetValue(toe,out var toeBone)){evidence.bindHorizontalHeading=Vector3.ProjectOnPlane(toeBone.position-foot.position,Vector3.up).normalized;evidence.available=evidence.bindHorizontalHeading.sqrMagnitude>.99f;evidence.method="Own ankle-to-toe horizontal projection; natural vertical bone slope excluded, own foot splay retained.";}
                footEvidence.Add(evidence);
            }
            audit.feet=footEvidence.ToArray();
            if(audit.weightedSoleVertices>8&&vector.magnitude>.001f){audit.hasWeightedSoleEvidence=true;audit.weightedSoleDirection=vector.normalized;audit.weightedSoleMinimum=minimum;audit.weightedSoleMaximum=maximum;}
            audit.physicalForward=audit.hasToeBones?audit.toeDirection:audit.hasHeadFront?audit.headFrontDirection:audit.hasWeightedSoleEvidence?audit.weightedSoleDirection:Vector3.zero;
            audit.mappedForwardDotPhysical=Vector3.Dot(frame.forward,audit.physicalForward);audit.sideConventionContradictsPhysicalForward=audit.physicalForward.sqrMagnitude>.9f&&audit.mappedForwardDotPhysical<-.8f;
            audit.finding=audit.sideConventionContradictsPhysicalForward?"MAPPED SIDE CONVENTION OPPOSES INDEPENDENT PHYSICAL FRONT. Reference calibration preserves source names; it does not silently rename or mirror attack ownership.":audit.physicalForward.sqrMagnitude>.9f?"Mapped handedness checked against independent physical-front evidence; inspect exact vectors.":"Independent physical forward unavailable; semantic side validity NOT established.";
            return audit;
        }
        private static FootHeadingEvidence MeasureSoleHeading(List<Vector3> points,HumanBodyBones foot)
        {
            var evidence=new FootHeadingEvidence{humanBone=foot.ToString(),weightedSoleVertices=points.Count,method="Own >=0.5-foot-weight sole vertices; horizontal principal longitudinal axis, sign from sole center relative to ankle. Geometry inference, not a synthetic toe bone."};
            if(points.Count<9)return evidence;
            var center=points.Aggregate(Vector3.zero,(sum,p)=>sum+p)/points.Count;evidence.weightedSoleCenterFromAnkle=center;
            var xx=0f;var xz=0f;var zz=0f;foreach(var p in points){var x=p.x-center.x;var z=p.z-center.z;xx+=x*x;xz+=x*z;zz+=z*z;}xx/=points.Count;xz/=points.Count;zz/=points.Count;
            var spread=Mathf.Sqrt((xx-zz)*(xx-zz)+4*xz*xz);evidence.longitudinalVariance=(xx+zz+spread)*.5f;evidence.lateralVariance=(xx+zz-spread)*.5f;
            var yaw=.5f*Mathf.Atan2(2*xz,xx-zz);var axis=new Vector3(Mathf.Cos(yaw),0,Mathf.Sin(yaw));if(Vector3.Dot(axis,center)<0)axis=-axis;
            evidence.bindHorizontalHeading=axis;evidence.available=evidence.longitudinalVariance>1.5f*evidence.lateralVariance&&Mathf.Abs(Vector3.Dot(axis,center))>.001f;return evidence;
        }
        private static Vector3 ToFrame(Vector3 direction,(Vector3 left,Vector3 up,Vector3 forward) frame)=>new Vector3(Vector3.Dot(direction,frame.left),Vector3.Dot(direction,frame.up),Vector3.Dot(direction,frame.forward));
        private static Dictionary<HumanBodyBones,Transform> Map(Animator animator)
        {
            RequireHuman(animator.avatar);var map=new Dictionary<HumanBodyBones,Transform>();
            foreach(HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))if(bone!=HumanBodyBones.LastBone){var transform=animator.GetBoneTransform(bone);if(transform!=null)map.Add(bone,transform);}
            Require(RequiredJoints.All(map.ContainsKey),"Required measured joint is not mapped.");return map;
        }
        private static Dictionary<HumanBodyBones,Transform> MapDescription(GameObject actor,HumanDescription description)
        {
            var all=actor.GetComponentsInChildren<Transform>(true);var map=new Dictionary<HumanBodyBones,Transform>();
            foreach(var human in description.human)
            {
                Require(Enum.TryParse<HumanBodyBones>(human.humanName,out var bone),"Unsupported HumanDescription name: "+human.humanName);
                var transform=all.SingleOrDefault(t=>t.name==human.boneName)??throw new InvalidOperationException("Missing/nonunique mapped bone: "+human.boneName);map.Add(bone,transform);
            }
            Require(RequiredJoints.All(map.ContainsKey),"Candidate description omits required semantic joint.");return map;
        }
        private static GameObject Clone(GameObject prefab,Avatar? avatar=null)
        {
            var clone=Object.Instantiate(prefab);
            try
            {
                clone.name=prefab.name;clone.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);Require((clone.transform.localScale-Vector3.one).sqrMagnitude<1e-8f,"Sampling root must be unit scale.");
                var animator=clone.GetComponent<Animator>();animator.enabled=false;animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                var bind=new PoseBackup(clone);if(avatar!=null){RequireHuman(avatar);animator.avatar=avatar;bind.Restore();}return clone;
            }
            catch{Object.DestroyImmediate(clone);throw;}
        }
        private static AnimationClipPlayable Playback(GameObject target,AnimationClip clip,out PlayableGraph graph)
        {
            target.GetComponent<Animator>().enabled=true;graph=PlayableGraph.Create("Isolated reference-pose calibration");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);playable.SetApplyPlayableIK(false);AnimationPlayableOutput.Create(graph,"Measured candidate",target.GetComponent<Animator>()).SetSourcePlayable(playable);graph.Play();return playable;
        }
        private sealed class PoseBackup
        {
            private readonly Transform[] transforms;private readonly Vector3[] positions,scales;private readonly Quaternion[] rotations;
            public PoseBackup(GameObject root){transforms=root.GetComponentsInChildren<Transform>(true);positions=transforms.Select(t=>t.localPosition).ToArray();rotations=transforms.Select(t=>t.localRotation).ToArray();scales=transforms.Select(t=>t.localScale).ToArray();}
            public void Restore(){for(var i=0;i<transforms.Length;i++){transforms[i].localPosition=positions[i];transforms[i].localRotation=rotations[i];transforms[i].localScale=scales[i];}}
            public void AssertTranslationsAndScales(){for(var i=0;i<transforms.Length;i++)Require((transforms[i].localPosition-positions[i]).sqrMagnitude<1e-12f&&(transforms[i].localScale-scales[i]).sqrMagnitude<1e-12f,"Reference calibration changed translations/scales: "+transforms[i].name);}
        }
        private static InputHash[] RecordInputs()
        {
            var paths=new[]{MeshyHasanPilotPipeline.PrefabPath,MeshyHasanPilotMotionAdaptation.DonorPrefabPath,MeshyHasanPilotPipeline.ModelPath}.Concat(MeshyHasanPilotMotionAdaptation.MotionNames.Select(MeshyHasanPilotMotionAdaptation.SourceClipPath)).Concat(MeshyHasanPilotMotionAdaptation.MotionNames.Select(MeshyHasanPilotMotionAdaptation.OutputClipPath)).ToArray();
            return paths.Concat(AssetDatabase.GetDependencies(paths,true)).Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal)&&File.Exists(p)&&!p.StartsWith(OutputRoot+"/",StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).SelectMany(p=>File.Exists(p+".meta")?new[]{p,p+".meta"}:new[]{p}).OrderBy(p=>p,StringComparer.Ordinal).Select(Hash).ToArray();
        }
        private static InputHash Hash(string path){using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return new InputHash{path=path,sha256=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant()};}
        private static void CheckHashes(IEnumerable<InputHash> inputs){foreach(var input in inputs)Require(Hash(input.path).sha256==input.sha256,"Calibration must not change original/input assets: "+input.path);}
        private static void Store<T>(T generated,string path) where T:Object
        {
            var existing=AssetDatabase.LoadAssetAtPath<T>(path);if(existing==null)AssetDatabase.CreateAsset(generated,path);else{EditorUtility.CopySerialized(generated,existing);EditorUtility.SetDirty(existing);}
        }
        private static T Load<T>(string path) where T:Object=>AssetDatabase.LoadAssetAtPath<T>(path)??throw new InvalidOperationException("Calibration input missing: "+path);
        private static string PathOf(Transform transform,Transform root)=>AnimationUtility.CalculateTransformPath(transform,root);
        private static void RequireHuman(Avatar avatar)=>Require(avatar!=null&&avatar.isValid&&avatar.isHuman,"Calibration requires a valid existing Humanoid mapping.");
        private static void ValidatePose(HumanPose pose)=>Require(pose.muscles!=null&&pose.muscles.Length==HumanTrait.MuscleCount&&pose.muscles.All(Finite)&&Finite(pose.bodyPosition)&&Finite(pose.bodyRotation.x)&&Finite(pose.bodyRotation.y)&&Finite(pose.bodyRotation.z)&&Finite(pose.bodyRotation.w),"Nonfinite HumanPose; do not clamp it.");
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        private static bool Finite(Vector3 value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z);
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private static Report NewReport(string operation)=>new Report{operation=operation,utc=DateTime.UtcNow.ToString("O"),unityVersion=Application.unityVersion};
        private static void WriteReport(Report report){Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));}
    }
}
