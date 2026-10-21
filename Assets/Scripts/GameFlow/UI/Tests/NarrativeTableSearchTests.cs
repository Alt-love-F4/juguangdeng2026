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
        [Test] public void UnexpectedEndingIsDiscoveredOnceAndKeepsFirstEvidence()
        {
            var found = new System.Collections.Generic.List<NarrativeTableSearch.Discovery>();
            var a = new NarrativeTableSearch.Attempt { target="target-a", actual="ending-b", status="ending", day=37, file="first.md", profile="legal" };
            Assert.That(NarrativeTableSearch.RegisterDiscovery(found,a,"结局B"),Is.True);
            a.target="target-c"; a.file="second.md";
            Assert.That(NarrativeTableSearch.RegisterDiscovery(found,a,"结局B"),Is.False);
            Assert.That(found.Count,Is.EqualTo(1));
            Assert.That(found[0].count,Is.EqualTo(2));
            Assert.That(found[0].firstTarget,Is.EqualTo("target-a"));
            Assert.That(found[0].firstTrace,Is.EqualTo("first.md"));
            a.actual="ending-c"; a.status="replay-mismatch";
            Assert.That(NarrativeTableSearch.RegisterDiscovery(found,a,"结局C"),Is.False);
            a.status="ending"; a.actual="";
            Assert.That(NarrativeTableSearch.RegisterDiscovery(found,a,""),Is.False);
            a.actual="ending-c";
            Assert.That(NarrativeTableSearch.RegisterDiscovery(found,a,"结局C"),Is.True);
            Assert.That(found.Count,Is.EqualTo(2));
        }

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
