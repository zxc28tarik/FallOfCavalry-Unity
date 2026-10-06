#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FOC.Presentation.Visuals
{
    public sealed partial class MeshyHasanPilotPlayer
    {
        private IEnumerator CaptureWorldStanceReview(Actor actor,LocomotionContactMeasurement measurement)
        {
            var result=MeshyLocomotionStanceAudit.Measure(measurement);
            if(result.windows.Length==0)yield break;
            var origin=actor.view.transform.position;
            var helpers=new List<GameObject>();
            var material=new Material(Shader.Find("Standard")){color=new Color(.15f,.55f,.55f)};
            material.SetFloat("_Glossiness",0f);
            try
            {
                foreach(var window in result.windows)
                {
                    var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);helpers.Add(marker);
                    marker.name="Diagnostic fixed ground support marker: "+window.marker;
                    marker.transform.position=new Vector3(window.worldAnchor.x,groundHeight+.012f,window.worldAnchor.z);
                    marker.transform.localScale=Vector3.one*.024f;marker.GetComponent<Renderer>().sharedMaterial=material;
                }
                foreach(var window in result.windows)
                {
                    foreach(var fraction in new[]{.2f,.8f})
                    {
                        var elapsedPhase=Mathf.Lerp(window.startPhase,window.endPhase,fraction);
                        var clipPhase=elapsedPhase%1f;
                        actor.view.transform.position=origin+Vector3.forward*result.fittedNativeSpeedMetersPerSecond*measurement.durationSeconds*elapsedPhase;
                        yield return null;SetPhase(actor,clipPhase);var sampled=SnapshotTrackedJoints(actor);
                        yield return new WaitForEndOfFrame();AssertStablePoseAcrossRenderBoundary(actor,sampled);
                        Frame("side");
                        Capture("world-support-"+measurement.clip.Replace('|','_')+"-"+window.marker+"-"+fraction.ToString("0.0",CultureInfo.InvariantCulture)+".png","side",clipPhase);
                    }
                }
            }
            finally
            {
                actor.view.transform.position=origin;
                foreach(var helper in helpers)Destroy(helper);
                Destroy(material);
            }
        }
    }
}
