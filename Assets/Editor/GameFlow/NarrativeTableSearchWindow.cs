#if UNITY_EDITOR
using System;
using System.IO;
using Spotlight.GameFlow;
using Spotlight.FreeCafe;
using Spotlight.Narrative;
using UnityEditor;
using UnityEngine;

namespace Spotlight.Narrative.Editor
{
    public sealed class NarrativeTableSearchWindow : EditorWindow
    {
        private static NarrativeTableSearch search;
        private static string error, output;
        private int perEnding = 12;
        private Vector2 scroll;
        [MenuItem("Tools/Narrative/配表分支演算")]
        public static void Open() => GetWindow<NarrativeTableSearchWindow>("配表分支演算");
        public static string StartSearch(int budget = 12, string target = null)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先停止前台测试并退出Play Mode，再启动独立演算。");
            if (search != null && !search.Finished) throw new InvalidOperationException("演算正在运行。");
            search?.Dispose(); error=null;
            output=Path.Combine(Application.persistentDataPath,"NarrativeTableSearch",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            var catalog=JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text);
            search=new NarrativeTableSearch(LubanNarrativeLoader.Load(),catalog,output,budget,target);
            EditorApplication.update-=Pump;EditorApplication.update+=Pump;
            AssemblyReloadEvents.beforeAssemblyReload-=StopSearch;AssemblyReloadEvents.beforeAssemblyReload+=StopSearch;
            return output;
        }
        public static string StatusText => error??search?.Status??"未运行";
        public static bool IsRunning => search!=null&&!search.Finished;
        public static int CompletedAttempts => search?.Attempts.Count??0;
        public static void StopSearch() { search?.Stop();EditorApplication.update-=Pump; }
        private static void Pump()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){StopSearch();return;}
            try
            {
                var timer=System.Diagnostics.Stopwatch.StartNew();
                do { search.Tick(128); } while (!search.Finished && timer.ElapsedMilliseconds<12);
                if(search.Finished)EditorApplication.update-=Pump;
            }
            catch(Exception exception){error=exception.ToString();StopSearch();Debug.LogException(exception);}
        }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("不加载游戏场景 · 真实剧情控制器分支重放",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("所有发现的可用选项都会产生候选前缀；按目标优先级有界探索。两种数值假设，固定地图/成功咖啡策略；不宣称穷举不可达。",MessageType.Info);
            perEnding=EditorGUILayout.IntSlider("每结局最多尝试",perEnding,2,64);
            GUI.enabled=!IsRunning;
            if(GUILayout.Button("开始全部15结局演算")){try{StartSearch(perEnding);}catch(Exception e){error=e.Message;}}
            GUI.enabled=IsRunning;if(GUILayout.Button("停止并保留报告"))StopSearch();GUI.enabled=true;
            if(!string.IsNullOrEmpty(output)&&GUILayout.Button("打开演算报告目录"))EditorUtility.RevealInFinder(Path.Combine(output,"演算报告.md"));
            EditorGUILayout.LabelField("已完成尝试："+CompletedAttempts+"；候选队列："+(search?.Pending??0)+"；当前D"+(search?.CurrentDay??0));
            scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.SelectableLabel(StatusText,EditorStyles.wordWrappedLabel,GUILayout.Height(90));
            if(search!=null)foreach(var a in search.Attempts)EditorGUILayout.LabelField(a.target+" → "+(a.actual??a.status)+" · D"+a.day,EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();if(IsRunning)Repaint();
        }
    }
}
#endif
