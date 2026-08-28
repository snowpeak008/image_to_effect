using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VFXComposer.Editor.NextCandidates;
using VFXComposer.W11W13NextCandidate;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W11W13NextCandidatePreviewTests
    {
        [Test]
        public void ThreePreviewScenes_AreIndependentBoundedAndUseOnlySceneDrivers()
        {
            W11W13NextCandidateAuthoring.BuildAll();
            var sandbox=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                foreach(var item in new[]{new{Group="W11",Count=7},new{Group="W12",Count=7},new{Group="W13",Count=6}})
                {
                    AssertPreviewScene(item.Group,item.Count);
                }
            }
            finally{if(sandbox.IsValid())EditorSceneManager.CloseScene(sandbox,true);}
        }

        [Test]
        public void W13PreviewScene_IsIndependentBoundedAndUsesOnlySceneDriver()
        {
            W11W13NextCandidateAuthoring.BuildW13ForBatch();
            var sandbox=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try{AssertPreviewScene("W13",6);}
            finally{if(sandbox.IsValid())EditorSceneManager.CloseScene(sandbox,true);}
        }

        private static void AssertPreviewScene(string group,int count)
        {
            var scene=EditorSceneManager.OpenScene(W11W13NextCandidateAuthoring.PreviewPath(group),OpenSceneMode.Additive);
            try
            {
                var roots=scene.GetRootGameObjects();
                Assert.That(roots.SelectMany(value=>value.GetComponentsInChildren<Camera>(true)).Count(),Is.EqualTo(1),group);
                var drivers=roots.SelectMany(value=>value.GetComponentsInChildren<W11W13NextCandidatePreviewDriver>(true)).ToArray();
                Assert.That(drivers.Length,Is.EqualTo(1),group);
                Assert.That(drivers[0].EntryCount,Is.EqualTo(count),group);
                Assert.That(roots.SelectMany(value=>value.GetComponentsInChildren<W11W13NextCandidateController>(true)).Count(),Is.EqualTo(count),group);
                if(group=="W12")Assert.That(roots.Count(value=>value.name.StartsWith("ExternalTarget_")),Is.EqualTo(7));
                Assert.That(roots.SelectMany(value=>value.GetComponentsInChildren<W11W13NextCandidateController>(true)).All(value=>value.GetComponent<W11W13NextCandidatePreviewDriver>()==null),Is.True);
            }
            finally{EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
