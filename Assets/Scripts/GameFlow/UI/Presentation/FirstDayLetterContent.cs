using System;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// 第一封信的内容快照。内容可以来自 NCS 文本表，也可以由旧的面板草稿作为兼容回退。
    /// 面板只消费解析后的值，不直接依赖 NarrativeRuntime。
    /// </summary>
    [Serializable]
    public sealed class FirstDayLetterContent
    {
        public string Body { get; }
        public string Signature { get; }

        public FirstDayLetterContent(string body, string signature)
        {
            Body = body ?? string.Empty;
            Signature = signature ?? string.Empty;
        }
    }
}
