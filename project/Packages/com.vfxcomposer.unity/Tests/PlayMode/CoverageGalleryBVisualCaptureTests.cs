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
    public sealed class CoverageGalleryBVisualCaptureTests
    {
        private const string ScenePath="Assets/VFX/Preview/VFXPREVIEW_CoverageGalleryB_3x3.unity";private const int Width=1280,Height=720;

        [UnityTest]
        public IEnumerator CaptureScreenUi_FromDedicatedFullscreenPreview()
        {
            const string screenScene="Assets/VFX/Preview/VFXPREVIEW_DamageWarningUI_Fullscreen.unity";var operation=SceneManager.LoadSceneAsync(screenScene,LoadSceneMode.Single);Assert.That(operation,Is.Not.Null);yield return operation;var scene=SceneManager.GetSceneByPath(screenScene);var roots=scene.GetRootGameObjects();var camera=roots.SelectMany(root=>root.GetComponentsInChildren<Camera>(true)).Single();var entry=roots.SelectMany(root=>root.GetComponentsInChildren<CoverageGalleryVfxController>(true)).Single();Assert.That(entry.Profile,Is.EqualTo(CoverageGalleryProfile.ScreenUi));var directory=EvidenceDirectory();Directory.CreateDirectory(directory);foreach(var old in Directory.GetFiles(directory,"screen_ui_full_*.png"))File.Delete(old);var priorRate=Time.captureFramerate;var priorDelta=Time.captureDeltaTime;
            try{Time.captureFramerate=60;Time.captureDeltaTime=1f/60f;for(var frame=1;frame<=72;frame++){yield return null;if(frame==18||frame==72){var file="screen_ui_full_"+frame.ToString("000",CultureInfo.InvariantCulture)+".png";var metrics=Capture(camera,directory,file);Assert.That(metrics.Foreground,Is.GreaterThan(1200),"Fullscreen Screen/UI feedback must be visible without opaque blocks.");Assert.That(metrics.WhiteClip,Is.LessThan(.02f));}}File.WriteAllText(Path.Combine(directory,"screen-ui-metadata.json"),"{\n  \"scene\": \""+screenScene+"\",\n  \"capture\": \"full-screen Canvas; serialized Camera; natural Update and runtime hit event\",\n  \"fps\": 60,\n  \"frames\": [18,72]\n}\n");}
            finally{Time.captureFramerate=priorRate;Time.captureDeltaTime=priorDelta;if(entry!=null)entry.ResetForPool();}
        }

        [UnityTest]
        public IEnumerator CaptureSpatialAndPresentationCoverage_FromSavedCameraAndNaturalUpdate()
        {
            var operation=SceneManager.LoadSceneAsync(ScenePath,LoadSceneMode.Single);Assert.That(operation,Is.Not.Null);yield return operation;var scene=SceneManager.GetSceneByPath(ScenePath);var roots=scene.GetRootGameObjects();var camera=roots.SelectMany(root=>root.GetComponentsInChildren<Camera>(true)).Single();var entries=roots.SelectMany(root=>root.GetComponentsInChildren<CoverageGalleryVfxController>(true)).ToArray();Assert.That(entries.Length,Is.EqualTo(9));var directory=EvidenceDirectory();Directory.CreateDirectory(directory);foreach(var old in Directory.GetFiles(directory,"coverage_b_*.png"))File.Delete(old);var priorRate=Time.captureFramerate;var priorDelta=Time.captureDeltaTime;var records=new List<string>();
            try
            {
                Time.captureFramerate=60;Time.captureDeltaTime=1f/60f;
                for(var frame=1;frame<=240;frame++)
                {
                    yield return null;if(frame==18||frame==72||frame==180||frame==240){var file="coverage_b_"+frame.ToString("000",CultureInfo.InvariantCulture)+".png";var metrics=Capture(camera,directory,file);var alive=entries.Count(entry=>entry.IsAlive);records.Add("{ \"file\": \""+file+"\", \"time\": "+(frame/60f).ToString("0.###",CultureInfo.InvariantCulture)+", \"aliveEntries\": "+alive+", \"foregroundPixels\": "+metrics.Foreground+", \"cellForegroundPixels\": ["+string.Join(",",metrics.Cells)+"], \"whiteClipRatio\": "+metrics.WhiteClip.ToString("0.######",CultureInfo.InvariantCulture)+" }");if(frame==18){Assert.That(alive,Is.EqualTo(9));Assert.That(metrics.Cells.All(value=>value>80),Is.True,"Every coverage cell must be visible: "+string.Join(",",metrics.Cells));}if(frame==180)Assert.That(alive,Is.EqualTo(8),"Impact receives one preview-only readability replay while Spawn remains a completed one-shot.");if(frame==240)Assert.That(alive,Is.EqualTo(9),"One-shots must replay without restarting sustained entries.");Assert.That(metrics.WhiteClip,Is.LessThan(.08f));}
                }
                File.WriteAllText(Path.Combine(directory,"metadata.json"),"{\n  \"scene\": \""+ScenePath+"\",\n  \"capture\": \"one serialized perspective Camera; natural Update; seven sustained entries; preview-only impact readability replay; two one-shot Runtime Entries\",\n  \"fps\": 60,\n  \"frames\": [\n    "+string.Join(",\n    ",records)+"\n  ]\n}\n");
            }
            finally{Time.captureFramerate=priorRate;Time.captureDeltaTime=priorDelta;foreach(var entry in entries)entry.ResetForPool();}
        }

        private static string EvidenceDirectory(){return Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","docs","vfx-reviews","coverage-gallery-b","evidence","current-run"));}
        private static Metrics Capture(Camera camera,string directory,string file)
        {
            var render=RenderTexture.GetTemporary(Width,Height,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
            try{camera.targetTexture=render;camera.Render();RenderTexture.active=render;var image=new Texture2D(Width,Height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,Width,Height),0,0);image.Apply(false);var pixels=image.GetPixels32();var background=pixels[0];var cells=new int[9];var foreground=0;var clip=0;for(var y=0;y<Height;y++)for(var x=0;x<Width;x++){var pixel=pixels[y*Width+x];if(Mathf.Max(Mathf.Abs(pixel.r-background.r),Mathf.Abs(pixel.g-background.g),Mathf.Abs(pixel.b-background.b))<=10)continue;foreground++;if(pixel.r>245&&pixel.g>245&&pixel.b>245)clip++;var row=Mathf.Clamp(y/(Height/3),0,2);var within=y-row*(Height/3);if(within>74&&within<235){var column=Mathf.Clamp(x/(Width/3),0,2);cells[(2-row)*3+column]++;}}File.WriteAllBytes(Path.Combine(directory,file),image.EncodeToPNG());Object.Destroy(image);return new Metrics{Foreground=foreground,Cells=cells,WhiteClip=foreground==0?0f:(float)clip/foreground};}finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(render);}
        }
        private sealed class Metrics{public int Foreground;public int[] Cells;public float WhiteClip;}
    }
}
