using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Spotlight.FreeCafe;
using Spotlight.Narrative;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spotlight.GameFlow.Tests
{
    public sealed class FinalPlayableCampaignTests
    {
        [UnityTest, Timeout(1200000)] public IEnumerator AllEnabledEndingsHaveAnActualDayOneCampaignWitness()
        {
            var config = LubanNarrativeLoader.Load();
            var catalog = JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text);
            var directory = Path.Combine(Application.dataPath,"../Library/Locus/tmp/final-playability/campaigns");
            Directory.CreateDirectory(directory);
            var failed = new List<string>();
            foreach (var ending in config.endings.Where(e => e.enabled))
            {
                using (var run = new NarrativeAutoSession(config,catalog,NarrativeAutoPresets.Make(ending.endingId,0,false)))
                {
                    for(int frame=0;frame<4000&&!run.Finished;frame++) { run.Tick(256); yield return null; }
                    File.WriteAllText(Path.Combine(directory,ending.endingId+".json"),JsonUtility.ToJson(run.Result,true));
                    if (!run.Finished || run.Controller.SelectedEndingId != ending.endingId)
                        failed.Add(ending.endingId+" -> "+run.Controller.SelectedEndingId+" / "+run.Result.reason+" D"+run.Day);
                }
            }
            Assert.That(failed,Is.Empty,string.Join("\n",failed));
        }
    }
}
