using System;
using System.IO;
using NUnit.Framework;
using Spotlight.FreeCafe;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow.Tests
{
    public sealed class NarrativeTableSearchTests
    {
        [Test] public void SearchProducesRealEndingEvidenceWithoutMutatingFormalRuntime()
        {
            string dir=Path.Combine(Application.dataPath,"../Library/Locus/tmp/search-test-"+Guid.NewGuid().ToString("N"));
            var config=LubanNarrativeLoader.Load();
            var catalog=JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text);
            var formal=NarrativeRuntime.Instance;
            using(var search=new NarrativeTableSearch(config,catalog,dir,2,"neutral.ending.a"))
            {
                for(int i=0;i<3000&&!search.Finished;i++)search.Tick(256);
                Assert.That(search.Finished,Is.True);
                Assert.That(search.Attempts.Count,Is.EqualTo(2));
                Assert.That(search.Attempts[0].actual,Is.EqualTo("neutral.ending.a"));
                Assert.That(search.Attempts[0].decisions,Is.Not.Empty);
                Assert.That(search.Attempts[0].decisions.TrueForAll(d=>Array.IndexOf(d.alternatives,d.chosen)>=0),Is.True);
                Assert.That(search.Pending,Is.Zero);
                Assert.That(File.Exists(Path.Combine(dir,"演算报告.md")),Is.True);
            }
            Assert.That(NarrativeRuntime.Instance,Is.SameAs(formal));
        }
    }
}
