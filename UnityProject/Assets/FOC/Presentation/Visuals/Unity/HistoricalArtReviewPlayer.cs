#nullable enable
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Globalization;
using UnityEngine;

namespace FOC.Presentation.Visuals
{
    /// <summary>Actual standalone-player draft review. Never modifies campaign state.</summary>
    public sealed class HistoricalArtReviewPlayer:MonoBehaviour
    {
        public GameObject[] candidates=Array.Empty<GameObject>();
        private IEnumerator Start()
        {
            Application.targetFrameRate=60;
            var id=Arg("-focArtAsset")??"MNT_Horse_Anatolian_01";
            var source=candidates.SingleOrDefault(p=>p!=null&&p.name==id);
            if(source==null){Debug.LogError("FOC_ART_REVIEW_MISSING "+id);Application.Quit(1);yield break;}
            var instance=Instantiate(source);var lod=instance.GetComponent<LODGroup>();if(lod!=null)lod.ForceLOD(0);
            GameObject? mount=null;
            var pose=Arg("-focArtPose");
            if(pose=="standing")HistoricalArtPoseReview.Standing(instance);
            if(pose=="mounted")
            {
                mount=Instantiate(candidates.Single(p=>p.name=="MNT_Horse_Anatolian_01"));mount.GetComponent<LODGroup>().ForceLOD(0);
                var harness=Instantiate(candidates.Single(p=>p.name=="HAR_SipahiHarness_01"),mount.transform);harness.GetComponent<LODGroup>().ForceLOD(0);
                HistoricalArtPoseReview.Mounted(instance,HistoricalArtPoseReview.Bone(mount,"Socket_Rider"));
                Debug.Log("FOC_ART_DIAGNOSTIC_MOUNTED_POSE_ONLY: not shared-animation, loadout or production runtime acceptance");
            }
            var headgear=Arg("-focArtHeadgear");
            if(headgear!=null){var hat=Instantiate(candidates.Single(p=>p.name==headgear),HistoricalArtPoseReview.Bone(instance,"Socket_Head"));hat.GetComponent<LODGroup>().ForceLOD(0);}
            var weapon=Arg("-focArtWeapon");
            if(weapon!=null){var item=Instantiate(candidates.Single(p=>p.name==weapon),HistoricalArtPoseReview.Bone(instance,"Socket_RightHand"));item.GetComponent<LODGroup>().ForceLOD(0);item.transform.localRotation=Quaternion.Euler(0,0,-90);}
            var gait=Arg("-focArtAnimation");var motionAnimator=instance.GetComponent<Animator>();
            if(gait!=null)
            {
                if(motionAnimator==null||motionAnimator.runtimeAnimatorController==null)throw new InvalidOperationException("Requested motion has no controller: "+gait);
                // These are authored transform curves on the canonical skeleton,
                // not humanoid muscle clips. Do not silently discard them through
                // humanoid retargeting. The versioned prefab retains its human avatar.
                if(id=="CHR_HasanAga_DonorDraft")
                {
                    motionAnimator.avatar=AvatarBuilder.BuildGenericAvatar(instance,"Root");
                    motionAnimator.Rebind();motionAnimator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                }
                motionAnimator.Play(gait,0,0);motionAnimator.Update(0);
                if(!motionAnimator.GetCurrentAnimatorStateInfo(0).IsName(gait))throw new InvalidOperationException("Requested motion was not entered: "+gait);
                if(mount!=null)mount.GetComponent<Animator>().enabled=false;
            }
            var bounds=instance.GetComponentsInChildren<Renderer>().First().bounds;
            foreach(var r in instance.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);
            if(mount!=null)foreach(var r in mount.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);
            // Skinned renderer bounds may still reflect the previous culling
            // update immediately after Play(). Use a stable full-actor frame for
            // comparable human motion evidence; never crop a failed deformation.
            if(id=="CHR_HasanAga_DonorDraft"&&mount==null)bounds=new Bounds(new Vector3(0,1,0),new Vector3(1.5f,2.05f,1));
            var camera=new GameObject("Art review camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.055f,.07f);camera.fieldOfView=30;
            var side=Arg("-focArtAngle")=="side";var scale=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
            var angle=Arg("-focArtAngle");var view=angle=="back"?new Vector3(0,.15f,-2.8f):angle=="straight"?new Vector3(0,.15f,2.8f):side?new Vector3(2.8f,.15f,.02f):new Vector3(1.25f,.2f,2.5f);
            camera.transform.position=bounds.center+view*scale;camera.transform.LookAt(bounds.center);
            RenderSettings.ambientLight=new Color(.36f,.37f,.39f);
            foreach(var setup in new[]{(new Vector3(35,-25,0),1.0f,Color.white),(new Vector3(20,135,0),.55f,Color.white)}){var light=new GameObject("Review light").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(setup.Item1);light.intensity=setup.Item2;light.color=setup.Item3;}
            QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowDistance=25;
            foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None)){light.shadows=LightShadows.Soft;light.shadowBias=.01f;light.shadowNormalBias=.02f;light.shadowStrength=.8f;}
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.transform.localScale=Vector3.one*10;ground.transform.position=new Vector3(0,bounds.min.y-.01f,0);ground.GetComponent<Renderer>().material=new Material(Shader.Find("Standard")){color=new Color(.085f,.09f,.095f)};
            ground.GetComponent<Renderer>().material.SetFloat("_Glossiness",0);
            if(id=="CHR_HasanAga_DonorDraft"&&mount==null)ground.transform.position=Vector3.zero;
            var tracked=instance.GetComponentsInChildren<Transform>().Where(t=>t.name=="Hand_L"||t.name=="Hand_R"||t.name=="Foot_L"||t.name=="Foot_R"||t.name=="Head").ToArray();
            var first=tracked.Select(t=>t.position).ToArray();var maxMotion=0f;var frames=0;var until=Time.realtimeSinceStartup+2;
            while(Time.realtimeSinceStartup<until)
            {
                yield return null;frames++;
                for(var i=0;i<tracked.Length;i++)maxMotion=Mathf.Max(maxMotion,Vector3.Distance(first[i],tracked[i].position));
            }
            if(gait!=null)
            {
                if(frames<2||maxMotion<.001f)throw new InvalidOperationException("Requested motion did not move joints: "+gait+" displacement="+maxMotion);
                var phase=float.Parse(Arg("-focArtPhase")??"0.25",CultureInfo.InvariantCulture);
                motionAnimator!.Play(gait,0,phase);motionAnimator.Update(0);motionAnimator.enabled=false;
                Debug.Log("FOC_HASAN_REAL_MOTION motion="+gait+" elapsedFrames="+frames+" maxJointTravel="+maxMotion.ToString("R",CultureInfo.InvariantCulture)+" capturePhase="+phase.ToString("R",CultureInfo.InvariantCulture)+" diagnosticClipNotBattleAcceptance=true");
            }
            yield return new WaitForEndOfFrame();
            var output=Arg("-focScreenshotPath");
            if(!string.IsNullOrWhiteSpace(output))
            {
                // Explicit player-camera render also works while the helper window is
                // hidden. Reading a hidden swapchain can return an all-black frame.
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);var target=new RenderTexture(1280,900,24);camera.targetTexture=target;camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
                var texture=new Texture2D(1280,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,900),0,0);texture.Apply();
                var pixels=texture.GetPixels32();if(pixels.All(p=>p.r==0&&p.g==0&&p.b==0))throw new InvalidOperationException("Blank render is not valid art evidence.");
                WriteBmp(output,pixels,texture.width,texture.height);RenderTexture.active=previous;camera.targetTexture=null;target.Release();Destroy(target);Destroy(texture);Debug.Log("FOC_ART_DRAFT_PLAYER_CAPTURE "+id+" path="+output+" graphics="+SystemInfo.graphicsDeviceName);
            }
            Application.Quit(0);
        }
        private static string? Arg(string name){var args=Environment.GetCommandLineArgs();var index=Array.FindIndex(args,a=>a==name);return index>=0&&index+1<args.Length?args[index+1]:null;}
        private static void WriteBmp(string path,Color32[] pixels,int width,int height)
        {
            var row=(width*3+3)&~3;using var writer=new BinaryWriter(File.Create(path));writer.Write((byte)'B');writer.Write((byte)'M');writer.Write(54+row*height);writer.Write(0);writer.Write(54);writer.Write(40);writer.Write(width);writer.Write(height);writer.Write((short)1);writer.Write((short)24);writer.Write(0);writer.Write(row*height);writer.Write(2835);writer.Write(2835);writer.Write(0);writer.Write(0);
            for(var y=0;y<height;y++){for(var x=0;x<width;x++){var c=pixels[y*width+x];writer.Write(c.b);writer.Write(c.g);writer.Write(c.r);}for(var p=width*3;p<row;p++)writer.Write((byte)0);}
        }
    }
}
