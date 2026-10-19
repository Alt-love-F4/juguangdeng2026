using System;
using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
    /// <summary>Resolve an accepted ending into a passive panel snapshot. Never selects an ending or mutates story facts.</summary>
    public static class NarrativeEndingPresentation
    {
        public static ResultPanelData Create(GameFlowBootstrap flow, NarrativeEndingDefinition ending,
            Func<string,string> resolve, Action returned, string returnLabel = "返回主菜单")
        {
            if (ending == null || string.IsNullOrWhiteSpace(ending.endingId)) throw new ArgumentException("A concrete ending definition is required.", nameof(ending));
            if (resolve == null) throw new ArgumentNullException(nameof(resolve));
            string title = Resolve(resolve, ending.titleText, "旅程结束");
            string message = Resolve(resolve, ending.messageText, "这段旅程已抵达终点。感谢你在沸点咖啡馆留下的每一个选择。");
            if (message == title) message = "这段旅程已抵达终点。\n感谢你在沸点咖啡馆留下的每一个选择。";
            return new ResultPanelData(flow,title,message,returned,returnLabel,ending.endingId);
        }
        private static string Resolve(Func<string,string> resolver,string key,string fallback)
        {
            if(string.IsNullOrWhiteSpace(key))return fallback;
            string value=resolver(key);
            return string.IsNullOrWhiteSpace(value)||value==key?fallback:value;
        }
    }
}
