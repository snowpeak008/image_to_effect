using System;
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
    public sealed class ValidationGalleryVisualCaptureTests
    {
        private const string ScenePath="Assets/VFX/Preview/VFXPREVIEW_ValidationGallery_3x3.unity";
        private const int Width=1280,Height=720;

        [UnityTest]
        public IEnumerator CaptureNineArchetypes_FromOneSerializedCameraAndNaturalUpdate()
        {
            var operation=SceneManager.LoadSceneAsync(ScenePath,LoadSceneMode.Single); Assert.That(operation,Is.Not.Null); yield return operation;
            var scene=SceneManager.GetSceneByPath(ScenePath); var roots=scene.GetRootGameObjects(); var camera=roots.SelectMany(root=>root.GetComponentsInChildren<Camera>(true)).Single(); var driver=roots.SelectMany(root=>root.GetComponentsInChildren<ValidationGalleryPlaybackDriver>(true)).Single(); var entries=roots.SelectMany(root=>root.GetComponentsInChildren<MonoBehaviour>(true)).Where(value=>value is IVfxRuntimeEntry).Cast<IVfxRuntimeEntry>().ToArray(); Assert.That(entries.Length,Is.EqualTo(9)); Assert.That(driver,Is.Not.Null);
            var directory=EvidenceDirectory(); Directory.CreateDirectory(directory); foreach(var old in Directory.GetFiles(directory,"gallery_*.png"))File.Delete(old);
            var priorRate=Time.captureFramerate; var priorDelta=Time.captureDeltaTime; var records=new List<string>();
            try
            {
                Time.captureFramerate=60; Time.captureDeltaTime=1f/60f;
                for(var frame=1;frame<=228;frame++)
                {
                    yield return null;
                    if(frame==18||frame==48||frame==86||frame==132||frame==228)
                    {
                        var file="gallery_"+frame.ToString("000",CultureInfo.InvariantCulture)+".png"; var metrics=Capture(camera,directory,file); var alive=entries.Count(entry=>entry.IsAlive); records.Add("{ \"file\": \""+file+"\", \"time\": "+(frame/60f).ToString("0.###",CultureInfo.InvariantCulture)+", \"aliveEntries\": "+alive+", \"foregroundPixels\": "+metrics.Foreground+", \"cellForegroundPixels\": ["+string.Join(",",metrics.CellForeground)+"], \"whiteClipRatio\": "+metrics.WhiteClip.ToString("0.######",CultureInfo.InvariantCulture)+" }");
                        if(frame==18)
                        {
                            Assert.That(alive,Is.EqualTo(9),"The first synchronized review frame must contain all nine active Runtime Entries.");
                            Assert.That(metrics.CellForeground.All(value=>value>100),Is.True,"Every cell must contain a visible effect body above its label. Counts: "+string.Join(",",metrics.CellForeground));
                        }
                        if(frame==18||frame==86)Assert.That(metrics.Foreground,Is.GreaterThan(25000));
                        Assert.That(metrics.WhiteClip,Is.LessThan(.08f));
                    }
                }
                Assert.That(entries.Count(entry=>entry.IsAlive),Is.EqualTo(5),"Aura, Area, Beam, Trail and Shield must remain alive while the four one-shot entries finish.");
                File.WriteAllText(Path.Combine(directory,"metadata.json"),"{\n  \"scene\": \""+ScenePath+"\",\n  \"capture\": \"one serialized Camera; natural Update; synchronized preview-only scheduler\",\n  \"fps\": 60,\n  \"frames\": [\n    "+string.Join(",\n    ",records)+"\n  ]\n}\n");
            }
            finally { Time.captureFramerate=priorRate; Time.captureDeltaTime=priorDelta; foreach(var entry in entries)entry.ResetForPool(); }
        }

        private static string EvidenceDirectory(){return Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","docs","vfx-reviews","validation-gallery-3x3","evidence","current-run"));}
        private static Metrics Capture(Camera camera,string directory,string file)
        {
            var render=RenderTexture.GetTemporary(Width,Height,24,RenderTextureFormat.ARGB32); var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=render;camera.Render();RenderTexture.active=render;var image=new Texture2D(Width,Height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,Width,Height),0,0);image.Apply(false);var pixels=image.GetPixels32();var background=pixels[0];int foreground=0,clip=0;
                var cells=new int[9];
                for(var y=0;y<Height;y++)for(var x=0;x<Width;x++)
                {
                    var pixel=pixels[y*Width+x]; if(Mathf.Max(Mathf.Abs(pixel.r-background.r),Mathf.Abs(pixel.g-background.g),Mathf.Abs(pixel.b-background.b))<=10)continue;
                    foreground++;if(pixel.r>245&&pixel.g>245&&pixel.b>245)clip++;
                    var row=Mathf.Clamp(y/(Height/3),0,2); var withinRow=y-row*(Height/3);
                    if(withinRow>80&&withinRow<235){var column=Mathf.Clamp(x/(Width/3),0,2);cells[(2-row)*3+column]++;}
                }
                File.WriteAllBytes(Path.Combine(directory,file),image.EncodeToPNG());UnityEngine.Object.Destroy(image);return new Metrics{Foreground=foreground,CellForeground=cells,WhiteClip=foreground==0?0f:(float)clip/foreground};
            }
            finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(render);}
        }
        private sealed class Metrics{public int Foreground;public int[] CellForeground;public float WhiteClip;}
    }
}
