using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>经营前准备页面；玩家确认后进入开店演出。</summary>
    public sealed class PreBusinessPanel : DayFlowPresentationPanel
    {
        private Button testSkipButton;
        private Func<bool> skipTutorial;

        protected override void OnOpen(IUIData data = null)
        {
            base.OnOpen(data);
            skipTutorial = null;
            if (testSkipButton != null) testSkipButton.gameObject.SetActive(false);
        }

        public void ShowTutorialHandoff(Func<bool> onSkip)
        {
            SetDetail("接下来将进入工作台与自由经营教学。开发测试可保留当前角色，直接前往 10 月 7 日。");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            skipTutorial = onSkip;
            if (testSkipButton == null)
            {
                var source = GetComponentInChildren<Button>(true);
                if (source == null) return;
                testSkipButton = Instantiate(source, source.transform.parent);
                testSkipButton.name = "TestSkipToOctober07";
                testSkipButton.onClick = new Button.ButtonClickedEvent();
                testSkipButton.onClick.AddListener(SkipTutorial);
                var rect = (RectTransform)testSkipButton.transform;
                rect.anchoredPosition += Vector2.up * (rect.rect.height + 18f);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(360f, rect.rect.width));
                var label = testSkipButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = "测试：跳到 10 月 7 日";
            }
            testSkipButton.interactable = true;
            testSkipButton.gameObject.SetActive(true);
#endif
        }

        private void SkipTutorial()
        {
            if (!IsPlaying || skipTutorial == null || testSkipButton == null || !testSkipButton.interactable) return;
            testSkipButton.interactable = false;
            if (!skipTutorial()) testSkipButton.interactable = true;
        }

        protected override void OnClose()
        {
            skipTutorial = null;
            base.OnClose();
        }

        public void ShowIntroductionUnavailable()
            => SetDetail("阿黛尔的引入剧本暂时无法读取。请检查 Luban 配表、生成数据和演出资源，修正后点击继续重试。");

        public void ShowFirstArrivalUnavailable()
            => SetDetail("初来乍到的演出已中断。点击继续重试当前片段；此前已确认的选择会保留。");

        public void ShowNarrativeEntryUnavailable()
            => SetDetail("剧情暂时无法继续。请检查内容或演出资源，点击继续重试；当前进度不会被标记完成。");

        public void ShowUnavailable()
            => SetDetail("未找到当前玩法模块。请确认当前场景已注册经营入口，然后重新尝试进入准备。");
    }
}
