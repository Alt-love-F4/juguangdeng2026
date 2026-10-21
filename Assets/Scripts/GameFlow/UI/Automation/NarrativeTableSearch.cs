using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Spotlight.FreeCafe;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>Bounded prefix-replay search. Uses real transactions; never clones an in-flight coroutine or invents history.</summary>
    public sealed class NarrativeTableSearch : IDisposable
    {
        [Serializable] public sealed class Decision
        {
            public int day;
            public string group, chosen, text, node;
            public string[] alternatives;
        }
        [Serializable] public sealed class Attempt
        {
            public string target, actual, profile, status, reason, file, lastEvent;
            public int day;
            public bool newlyDiscovered;
            public List<Decision> decisions;
        }
        [Serializable] public sealed class Discovery
        {
            public string endingId, title, firstTarget, firstProfile, firstTrace;
            public int firstDay, count;
        }
        public static bool RegisterDiscovery(List<Discovery> found, Attempt attempt, string title)
        {
            if (attempt.status != "ending" || string.IsNullOrWhiteSpace(attempt.actual)) return false;
            var existing = found.Find(d => d.endingId == attempt.actual);
            if (existing != null) { existing.count++; return false; }
            found.Add(new Discovery { endingId = attempt.actual, title = title, firstTarget = attempt.target,
                firstProfile = attempt.profile, firstTrace = attempt.file, firstDay = attempt.day, count = 1 });
            return true;
        }
        [Serializable] private sealed class Summary
        {
            public string scope = "有界选择前缀重放；不是穷举证明；地图行动固定策略；咖啡始终理想交付/免费补料；无历史或资格注入", hash;
            public bool finished;
            public int droppedPrefixes;
            public List<Attempt> attempts;
            public List<Discovery> discoveries;
        }
        private sealed class Job { public string target; public int profile; public string[] prefix; public double rank; }
        private readonly NarrativeConfiguration config;
        private readonly CafeCatalog catalog;
        private readonly PlayerCreationCatalog creationCatalog;
        private readonly List<PlayerCreationPlan> creations = new List<PlayerCreationPlan>();
        private readonly List<string> targets;
        private readonly Dictionary<string,List<string>> dependencies = new Dictionary<string,List<string>>();
        private readonly List<Job> frontier = new List<Job>();
        private readonly HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string,int> counts = new Dictionary<string,int>();
        private readonly List<Discovery> discoveries = new List<Discovery>();
        public IReadOnlyList<Discovery> Discoveries => discoveries;
        public int EnabledEndingCount => config.endings.Count(e => e.enabled);
        public string EndingTitle(string id) => Title(id);
        public static string OutcomeLabel(Attempt a)
        {
            if (a.status == "ending" && !string.IsNullOrWhiteSpace(a.actual)) return a.actual == a.target ? "命中目标" : "发现其他结局";
            switch (a.status)
            {
                case "no-ending": return "到达日期上限，未触发结局";
                case "blocked": case "content-blocked": case "host-blocked": return "被条件或宿主阻塞";
                case "replay-mismatch": return "分支重放不一致";
                case "cancelled": return "已停止";
                case "budget-exhausted": return "达到演算预算";
                case "error": return "执行错误";
                default: return a.status ?? "未完成";
            }
        }
        private readonly List<Attempt> attempts = new List<Attempt>();
        private readonly int perTarget;
        private NarrativeAutoSession session;
        private Job current;
        private List<Decision> decisions;
        private bool prefixInvalid;
        private int dropped;
        private readonly string hash;
        public bool Finished { get; private set; }
        public string Status { get; private set; }
        public string DirectoryPath { get; }
        public IReadOnlyList<Attempt> Attempts => attempts;
        public int Pending => frontier.Count;
        public int CurrentDay => session?.Day ?? 0;

        public NarrativeTableSearch(NarrativeConfiguration config, CafeCatalog catalog, string directory, int runsPerEnding = 12, string onlyEnding = null)
        {
            this.config = config; this.catalog = catalog; DirectoryPath = directory; perTarget = Math.Max(2, runsPerEnding);
            Directory.CreateDirectory(directory);
            targets = config.endings.Where(e => e.enabled && e.endingId != "arrival.ending.if0" && (onlyEnding == null || e.endingId == onlyEnding)).Select(e => e.endingId).ToList();
            if (targets.Count == 0) throw new ArgumentException("没有目标结局");
            using (var sha = System.Security.Cryptography.SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", Resources.LoadAll<TextAsset>("Luban").OrderBy(a=>a.name).Select(a=>a.name+a.text)) + JsonUtility.ToJson(catalog)))).Replace("-", "");
            foreach (string target in targets)
            {
                counts[target] = 0;
                dependencies[target] = NarrativeEndingDependencies.Explain(config,target);
                Enqueue(new Job { target=target, profile=0, prefix=Array.Empty<string>(), rank=100000 });
                Enqueue(new Job { target=target, profile=1, prefix=Array.Empty<string>(), rank=99999 });
            }
            creationCatalog = PlayerCreationCatalog.Load();
            void Expand(int group, List<string> chosen)
            {
                if (group == creationCatalog.Groups.Count) { creations.Add(creationCatalog.Prepare(new FirstDayLetterDraft(creationCatalog, "演算角色", chosen.ToArray()))); return; }
                foreach (var option in creationCatalog.ForGroup(creationCatalog.Groups[group].GroupId)) { chosen.Add(option.OptionId); Expand(group+1,chosen); chosen.RemoveAt(chosen.Count-1); }
            }
            Expand(0,new List<string>());
            creations.Sort((a,b) => ScoreCreation(b).CompareTo(ScoreCreation(a)));
            Status = "配表加载完毕，开始分支搜索"; Save();
        }
        private void Enqueue(Job job)
        {
            string key = job.target + ":" + job.profile + ":" + string.Join("|",job.prefix);
            if (!visited.Add(key)) return;
            if (frontier.Count >= targets.Count * 256) { dropped++; return; }
            frontier.Add(job);
        }
        private void StartNext()
        {
            frontier.RemoveAll(j => counts[j.target] >= perTarget);
            if(frontier.Count==0) { Finished=true;Status="有界搜索完成；未发现路径不等于不可达";Save();return; }
            current=frontier.OrderBy(j=>counts[j.target]).ThenByDescending(j=>j.rank).First();frontier.Remove(current);counts[current.target]++;
            var preset=NarrativeAutoPresets.Make(current.target,0,false);
            preset.id="table-search:"+current.target+":"+counts[current.target];
            preset.useTableInitialValues=current.profile==0; preset.initialMoney=current.profile==0?catalog.startingMoney:10000;
            preset.dailyActionPoints=current.profile==0?5:6; preset.maxDay=60;
            // profile1 retains the explicitly disclosed optimistic threshold fixture; profile0 is raw table values.
            PlayerCreationPlan creation = null;
            if (current.profile == 0)
            {
                creation = current.target == "church.ending.if4" || current.target == "church.ending.if2"
                    ? creations.OrderBy(c => c.AttributeBonuses.TryGetValue("language",out int n)?n:0).First()
                    : creations[0];
                preset.dailyActionPoints = creationCatalog.ActionPointMax;
                preset.initialMoney = creation.StartingMoney;
            }
            session=new NarrativeAutoSession(config,catalog,preset,creation);
            if (creation != null) session.Result.log.Insert(0,"合法建角选项："+string.Join(",",creation.OptionIds));
            decisions=new List<Decision>();prefixInvalid=false;
            session.Presenter.ChoiceSelector=Choose;
            Status="目标 "+Title(current.target)+" / "+Profile(current.profile)+" / 尝试"+counts[current.target]+"，前缀"+current.prefix.Length;
        }
        private string Choose(NarrativeAutomationChoiceContext context)
        {
            int depth=decisions.Count;
            var ranked=context.Scores.OrderByDescending(s=>s.Score).ThenBy(s=>s.ChoiceId,StringComparer.Ordinal).Select(s=>s.ChoiceId).ToList();
            string preferred=session.Preset.preferredChoices.FirstOrDefault(id=>context.Choices.Any(c=>c.choiceId==id));
            string node = context.Request.Event.eventId + "~" + context.PageId + "~" + context.GroupKey + "~";
            string token = depth < current.prefix.Length ? current.prefix[depth] : null;
            if (token != null && !token.StartsWith(node,StringComparison.Ordinal)) { prefixInvalid=true;throw new InvalidOperationException("前缀决策节点漂移："+node); }
            string chosen=token!=null?token.Substring(node.Length):preferred??ranked.First();
            if(!context.Choices.Any(c=>c.choiceId==chosen)) { prefixInvalid=true;throw new InvalidOperationException("前缀重放不一致："+context.GroupKey+" / "+chosen); }
            var choice=context.Choices.First(c=>c.choiceId==chosen);
            decisions.Add(new Decision { day=session.Day,node=node,group=context.GroupKey,chosen=chosen,text=Text(choice.textKey),alternatives=context.Choices.Select(c=>c.choiceId).ToArray() });
            // Enumerate legal alternatives at each newly encountered node; their prefix is replayable from day1.
            if(depth>=current.prefix.Length)
            {
                foreach(string alternative in ranked.Where(id=>id!=chosen))
                {
                    var prefix=decisions.Take(depth).Select(d=>d.node+d.chosen).Concat(new[]{node+alternative}).ToArray();
                    double score=context.Scores.First(s=>s.ChoiceId==alternative).Score;
                    // Remove explicit override's million-point preference; retain semantic relevance and prefer earlier new branches.
                    double priority=Math.Min(score,9999)-depth*.1;
                    Enqueue(new Job { target=current.target,profile=current.profile,prefix=prefix,rank=priority });
                }
            }
            return chosen;
        }
        public void Tick(int units=256)
        {
            if(Finished)return;
            if(session==null) { StartNext();return; }
            session.Tick(units);
            if(!session.Finished)return;
            var result=session.Result;
            if (decisions.Count < current.prefix.Length) { prefixInvalid=true; result.reason="前缀尚未消费完即结束："+result.reason; }
            string filename="attempt-"+(attempts.Count+1).ToString("0000")+".md";
            var attempt=new Attempt {target=current.target,actual=result.actualEndingId,profile=Profile(current.profile),status=prefixInvalid?"replay-mismatch":result.status,
                reason=result.reason,day=result.day,lastEvent=result.events.LastOrDefault(),decisions=decisions,file=filename};
            attempt.newlyDiscovered = RegisterDiscovery(discoveries, attempt, Title(attempt.actual));
            attempts.Add(attempt);
            var body=new StringBuilder("# 分支演算轨迹\n\n目标："+Title(current.target)+"\n\n实际："+Title(result.actualEndingId)+"\n\n输入："+attempt.profile+"\n\n状态："+attempt.status+"；D"+attempt.day+"；"+attempt.reason+"\n\n## 实际选择\n");
            foreach(var d in decisions)body.AppendLine("- D"+d.day+" **"+d.text+"** (`"+d.chosen+"`)；可选："+string.Join(" / ",d.alternatives));
            body.AppendLine("\n## 最终数值\n");foreach(var v in result.finalValues)body.AppendLine("- "+v.key+"="+v.value);
            body.AppendLine("\n## 原控制器执行记录\n\n```text");foreach(var line in result.log)body.AppendLine(line);body.AppendLine("```");
            File.WriteAllText(Path.Combine(DirectoryPath,filename),body.ToString());
            session.Dispose();session=null; Save();
        }
        public void Stop() { if(Finished)return;session?.Stop("cancelled","用户停止演算");if(session!=null)Tick(1);Finished=true;Status="搜索已停止，保留已完成证据";Save(); }
        public void Dispose() { Stop(); session?.Dispose();session=null; }
        private string Text(string key)=>config.texts.Find(t=>t.key==key)?.content??key;
        private string Title(string id) => string.IsNullOrWhiteSpace(id)?"未到达结局":Text(config.endings.Find(e=>e.endingId==id)?.titleText??id);
        private static int ScoreCreation(PlayerCreationPlan p) => (p.AttributeBonuses.TryGetValue("language",out int l)?l:0)*100 + (p.AttributeBonuses.TryGetValue("courage",out int c)?c:0)*10 + p.StartingMoney/100;
        private static string Profile(int p)=>p==0?"配表枚举合法建角后选代表（长线优先语言、坏结局低语言）；正式初资/AP；理想制作免费补料；固定地图策略":"乐观阈值预设：高属性/王女好感20/10000钱/6AP（IF2/IF4保留失败属性），非合法建角证明";
        private void Save()
        {
            File.WriteAllText(Path.Combine(DirectoryPath,"results.json"),JsonUtility.ToJson(new Summary{hash=hash,finished=Finished,droppedPrefixes=dropped,attempts=attempts,discoveries=discoveries},true));
            var text=new StringBuilder("# 配表分支演算报告\n\n"+Status+"\n\n配置指纹："+hash+"\n\n使用真实控制器、选择检定、日期与交付结算；不注入历史/路线资格。\n\n这是有界前缀搜索：每目标最多"+perTarget+"次；两个属性假设；不穷举建角、地图行动、咖啡失败。没有找到路径不代表不存在路径。\n\n已完成 "+attempts.Count+" 次；队列 "+frontier.Count+"；队列预算丢弃 "+dropped+" 个前缀。\n");
            text.AppendLine("\n## 全局已发现结局（本批次去重，与当轮目标无关）\n");
            text.AppendLine("已实际触发 " + discoveries.Count + " / " + EnabledEndingCount + " 个启用结局。仅表示列明假设下触发，不表示正常玩法可达。\n");
            foreach (var discovery in discoveries)
                text.AppendLine("- **" + discovery.title + "**：" + discovery.count + "次；首次目标「" + Title(discovery.firstTarget)
                    + "」；第" + discovery.firstDay + "天；[首次路径](" + discovery.firstTrace + ")；输入：" + discovery.firstProfile);
            foreach(string target in targets)
            {
                var rows=attempts.Where(a=>a.target==target).ToArray();var reached=rows.Where(a=>a.status=="ending" && a.actual==target).ToArray();
                text.AppendLine("\n## "+Title(target)+" (`"+target+"`)\n\n"+(reached.Length>0?"找到假设条件下可执行路径；不等于正常经济/合法建角可达。":"尚未找到目标路径（未证明）。"));
                foreach(var group in rows.GroupBy(a=>string.IsNullOrWhiteSpace(a.actual)?OutcomeLabel(a):Title(a.actual)))text.AppendLine("- 目标 → **"+group.Key+"**："+group.Count()+"次");
                foreach(var row in rows)text.AppendLine("- [D"+row.day+" → "+Title(row.actual)+"]("+row.file+")；"+row.profile+"；最后剧情 `"+row.lastEvent+"`；"+row.reason);
                text.AppendLine("\n### 反向依赖证据（含备选路径，不作全局不可达断言）");
                foreach(string line in dependencies[target])text.AppendLine("- "+line);
            }
            File.WriteAllText(Path.Combine(DirectoryPath,"演算报告.md"),text.ToString());
        }
    }
}
