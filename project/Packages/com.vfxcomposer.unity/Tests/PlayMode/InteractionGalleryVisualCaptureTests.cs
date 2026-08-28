using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace VFXComposer.Tests.PlayMode
{
    [Explicit("Graphics-backed evidence recorder; run only for a requested visual capture, never in the automatic PlayMode regression.")]
    public sealed class InteractionGalleryVisualCaptureTests
    {
        private const string ScenePath="Assets/VFX/Preview/VFXPREVIEW_InteractionGallery_3x3.unity";private const int Width=1280,Height=720;
        [UnityTest]
        public IEnumerator CaptureNineInteractionProfiles_FromSavedCameraAndNaturalUpdate()
        {
            var operation=SceneManager.LoadSceneAsync(ScenePath,LoadSceneMode.Single);Assert.That(operation,Is.Not.Null);yield return operation;var scene=SceneManager.GetSceneByPath(ScenePath);var roots=scene.GetRootGameObjects();var camera=roots.SelectMany(root=>root.GetComponentsInChildren<Camera>(true)).Single();var entries=roots.SelectMany(root=>root.GetComponentsInChildren<InteractionGalleryVfxController>(true)).ToArray();Assert.That(entries.Length,Is.EqualTo(9));var directory=EvidenceDirectory();Directory.CreateDirectory(directory);foreach(var old in Directory.GetFiles(directory,"interaction_*.png"))File.Delete(old);var priorRate=Time.captureFramerate;var priorDelta=Time.captureDeltaTime;var records=new List<string>();try{Time.captureFramerate=60;Time.captureDeltaTime=1f/60f;for(var frame=1;frame<=330;frame++){yield return null;if(frame==18||frame==90||frame==180||frame==240||frame==330){var file="interaction_"+frame.ToString("000",CultureInfo.InvariantCulture)+".png";var metrics=Capture(camera,directory,file);var alive=entries.Count(entry=>entry.IsAlive);records.Add("{ \"file\": \""+file+"\", \"time\": "+(frame/60f).ToString("0.###",CultureInfo.InvariantCulture)+", \"aliveEntries\": "+alive+", \"cellForegroundPixels\": ["+string.Join(",",metrics.Cells)+"], \"whiteClipRatio\": "+metrics.WhiteClip.ToString("0.######",CultureInfo.InvariantCulture)+" }");if(frame==18||frame==90||frame==330){Assert.That(alive,Is.EqualTo(9));Assert.That(metrics.Cells.All(value=>value>80),Is.True,"Every interaction cell must be visible: "+string.Join(",",metrics.Cells));}Assert.That(metrics.WhiteClip,Is.LessThan(.08f));}}File.WriteAllText(Path.Combine(directory,"metadata.json"),"{\n  \"scene\": \""+ScenePath+"\",\n  \"capture\": \"one serialized perspective Camera; natural Update; preview driver sends release/retarget/hit events\",\n  \"fps\": 60,\n  \"frames\": [\n    "+string.Join(",\n    ",records)+"\n  ]\n}\n");}finally{Time.captureFramerate=priorRate;Time.captureDeltaTime=priorDelta;foreach(var entry in entries)if(entry!=null)entry.ResetForPool();}
        }
        private static string EvidenceDirectory(){return Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","docs","vfx-reviews","interaction-gallery","evidence","current-run"));}
        private static Metrics Capture(Camera camera,string directory,string file){var render=RenderTexture.GetTemporary(Width,Height,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;try{camera.targetTexture=render;camera.Render();RenderTexture.active=render;var image=new Texture2D(Width,Height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,Width,Height),0,0);image.Apply(false);var pixels=image.GetPixels32();var background=pixels[0];var cells=new int[9];var foreground=0;var clip=0;for(var y=0;y<Height;y++)for(var x=0;x<Width;x++){var pixel=pixels[y*Width+x];if(Mathf.Max(Mathf.Abs(pixel.r-background.r),Mathf.Abs(pixel.g-background.g),Mathf.Abs(pixel.b-background.b))<=10)continue;foreground++;if(pixel.r>245&&pixel.g>245&&pixel.b>245)clip++;var row=Mathf.Clamp(y/(Height/3),0,2);var within=y-row*(Height/3);if(within>74&&within<235){var column=Mathf.Clamp(x/(Width/3),0,2);cells[(2-row)*3+column]++;}}File.WriteAllBytes(Path.Combine(directory,file),image.EncodeToPNG());Object.Destroy(image);return new Metrics{Cells=cells,WhiteClip=foreground==0?0:(float)clip/foreground};}finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(render);}}
        private sealed class Metrics{public int[] Cells;public float WhiteClip;}
    }
}
