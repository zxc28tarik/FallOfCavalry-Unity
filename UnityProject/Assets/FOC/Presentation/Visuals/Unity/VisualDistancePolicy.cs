#nullable enable
using System;
using UnityEngine;
namespace FOC.Presentation.Visuals{public sealed class VisualDistancePolicy:MonoBehaviour{[SerializeField]private Animator? animator;[SerializeField]private float mediumDistance=45f;[SerializeField]private float farDistance=90f;private int frameOffset;private void Awake(){frameOffset=Math.Abs(GetInstanceID())%4;if(animator!=null)animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;}public bool ShouldEvaluateThisFrame(float squaredDistance,int frame){if(squaredDistance<=mediumDistance*mediumDistance)return true;if(squaredDistance<=farDistance*farDistance)return(frame+frameOffset)%2==0;return(frame+frameOffset)%4==0;}}}
