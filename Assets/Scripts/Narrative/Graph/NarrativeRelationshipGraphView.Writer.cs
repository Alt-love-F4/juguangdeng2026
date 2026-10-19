using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spotlight.Narrative
{
    /// <summary>Writer-facing navigation. UI state only; never evaluates or executes story results.</summary>
    public sealed partial class NarrativeRelationshipGraphView
    {
        private bool mapMode, advancedFilters, showPageDiagnostics;
        private string focusAfterLayout;
        private Vector2 pageListScroll;
        private string jumpText = "", copyNotice = "";
        private int readingSize = 16;
        private float copyNoticeUntil;
        private GUIStyle readingBody;
        private IReadOnlyList<StoryContentBlock> matchedBlocks;
        private IReadOnlyList<int> matchedPages;
        private string matchedQuery;
        private float pageListHeight;
        private IReadOnlyList<int> MatchingPages(IReadOnlyList<StoryContentBlock> blocks)
        {
            if (!ReferenceEquals(matchedBlocks, blocks) || matchedQuery != pageQuery)
            {
                matchedBlocks = blocks; matchedQuery = pageQuery;
                matchedPages = NarrativeRelationshipGraph.FindMatchingPages(blocks, pageQuery);
            }
            return matchedPages;
        }
        private readonly List<BrowseLocation> backStack = new List<BrowseLocation>();
        private sealed class BrowseLocation
        {
            public string Node, Page, Query, PageQuery;
            public Vector2 DetailScroll, PageScroll, FilterScroll;
            public int Scope, Faction, Tab;
            public bool Neighbors, Facts;
        }
        public bool CanGoBack => backStack.Count > 0;
        public bool IsMapMode
        {
            get => mapMode;
            set { if (mapMode == value) return; mapMode = value; if (value) focusAfterLayout = selected; }
        }

        private void RememberLocation()
        {
            if (SelectedNode == null) return;
            if (backStack.Count == 64) backStack.RemoveAt(0);
            backStack.Add(new BrowseLocation { Node = selected, Page = SelectedPageId, Query = query,
                Scope = scope, Faction = faction, Tab = tab, Neighbors = onlyNeighbors, Facts = showFacts,
                PageQuery = pageQuery, DetailScroll = detailScroll, PageScroll = pageListScroll, FilterScroll = filterScroll });
        }
        private void ResetNavigation()
        {
            backStack.Clear(); matchedBlocks = null; matchedPages = null; matchedQuery = null; pageListScroll = Vector2.zero; jumpText = ""; copyNotice = "";
        }
        public bool GoBack()
        {
            if (!CanGoBack || disposed) return false;
            var previous = backStack[backStack.Count - 1]; backStack.RemoveAt(backStack.Count - 1);
            if (graph?.Find(previous.Node) == null) return false;
            selected = previous.Node; query = previous.Query; scope = previous.Scope; faction = previous.Faction;
            tab = previous.Tab; onlyNeighbors = previous.Neighbors; showFacts = previous.Facts;
            page = Math.Max(0, NarrativeRelationshipGraph.FindPageIndex(graph.Content(selected), previous.Page));
            pageQuery = previous.PageQuery; detailScroll = previous.DetailScroll; pageListScroll = previous.PageScroll; info = "";
            RefreshNeighbors(); Rebuild(); filterScroll = previous.FilterScroll; Focus(selected); return true;
        }
        /// <summary>Exact config page ID, not a simulated branch traversal.</summary>
        public bool TrySelectPage(string pageId)
        {
            if (disposed || SelectedNode == null) return false;
            int index = NarrativeRelationshipGraph.FindPageIndex(graph.Content(selected), pageId);
            if (index < 0) return false;
            MoveToPage(index); return true;
        }
        private void MoveToPage(int index)
        {
            var blocks = graph.Content(selected);
            if (index < 0 || index >= blocks.Count || page == index) return;
            RememberLocation(); page = index; detailScroll = Vector2.zero; info = ""; jumpText = "";
            var matches = MatchingPages(blocks);
            for (int row = 0; row < matches.Count; row++)
                if (matches[row] == page)
                {
                    float top = row * 73;
                    if (top < pageListScroll.y) pageListScroll.y = top;
                    else if (pageListHeight > 0 && top + 73 > pageListScroll.y + pageListHeight)
                        pageListScroll.y = top + 73 - pageListHeight;
                    break;
                }
        }
        private void CopyText(string text, string message)
        {
            GUIUtility.systemCopyBuffer = text; copyNotice = message; copyNoticeUntil = Time.realtimeSinceStartup + 6;
        }
        private string IssueLocation()
        {
            var node = SelectedNode;
            if (node == null) return "";
            var blocks = graph.Content(node.Id);
            string text = blocks.Count == 0 ? "" : blocks[Mathf.Clamp(page, 0, blocks.Count - 1)].Text;
            return "【剧情文案问题】\n事件：" + node.Key + "\n标题：" + node.Title + "\n页面：" + SelectedPageId
                + "\n配置序号：" + (page + 1) + " / " + blocks.Count + "\n环境：" + ContextLabel
                + "\n当前状态（非已读记录）：" + NodeStatus(node)
                + "\n复现方式：配置浏览 / 单页样式 / 从头试播（请保留实际使用项）\n问题：\n预期：\n\n【当前页配置】\n" + text;
        }
        private void DrawArchive(Rect bounds)
        {
            Fill(bounds, bg);
            Fill(new Rect(0, 0, bounds.width, 88), panel);
            GUI.Label(new Rect(18, 8, 260, 31), "剧情工作台", title);
            if (Chip(new Rect(278, 10, 116, 30), "文案阅览", !mapMode)) IsMapMode = false;
            if (Chip(new Rect(402, 10, 108, 30), "关系图", mapMode)) IsMapMode = true;
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && CanGoBack;
            if (GUI.Button(new Rect(520, 10, 140, 30), "返回上一处", button)) GoBack();
            GUI.enabled = enabled;
            if (mapMode && GUI.Button(new Rect(670, 10, 102, 30), "适应全图 F", button)) Fit();
            if (mapMode && bounds.width >= 920 && selected != null
                && Chip(new Rect(780, 10, 120, 30), "仅相邻关系", onlyNeighbors))
            { onlyNeighbors = !onlyNeighbors; Rebuild(); focusAfterLayout = selected; }
            GUI.Label(new Rect(18, 47, bounds.width - 36, 30),
                string.IsNullOrEmpty(ContextLabel) ? "只读配置浏览 · 不推进剧情 / 不写存档 · 日期排序不代表因果" : ContextLabel, tiny);
            Fill(new Rect(0, 86, bounds.width, 2), Brass);
            if (error != null || graph == null)
            {
                GUI.Label(new Rect(24, 110, bounds.width - 48, 160), "配置尚不可用：" + error, body);
                if (GUI.Button(new Rect(24, 280, 180, 32), "重新读取配置", button)) RequestRefresh();
                return;
            }
            float leftWidth = bounds.width < 1050 ? 224 : 260;
            float contentHeight = bounds.height - 120;
            Filters(new Rect(0, 89, leftWidth, contentHeight));
            float rightWidth = mapMode ? Mathf.Clamp(bounds.width * .37f, 380, 610) : bounds.width - leftWidth - 248;
            // Narrow editor windows retain a comfortable reader instead of three cramped columns.
            bool pageColumn = !mapMode && bounds.width >= 1080;
            if (!mapMode && !pageColumn) rightWidth = bounds.width - leftWidth - 2;
            var right = new Rect(bounds.width - rightWidth, 89, rightWidth, contentHeight);
            viewport = new Rect(leftWidth + 2, 89, Mathf.Max(1, right.x - leftWidth - 4), contentHeight);
            if (mapMode && focusAfterLayout != null) { Focus(focusAfterLayout); focusAfterLayout = null; }
            if (mapMode) Canvas(viewport);
            else if (pageColumn) PageDirectory(viewport);
            Details(right);
            Fill(new Rect(0, bounds.height - 30, bounds.width, 30), panel);
            GUI.Label(new Rect(12, bounds.height - 25, bounds.width - 24, 22), (string.IsNullOrEmpty(copyNotice) || Time.realtimeSinceStartup > copyNoticeUntil)
                ? "Ctrl+F 搜索剧情 · Alt+← 返回 · PageUp / PageDown 按配置翻页（非剧情路径）" + (mapMode ? " · 图内滚轮缩放 / 右键拖动" : " · 点击页目录可直接检查任意分支")
                : copyNotice, tiny);
            HandleWriterKeys();
        }
        private void HandleWriterKeys()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;
            if ((e.control || e.command) && e.keyCode == KeyCode.F) { GUI.FocusControl("StorySearch"); e.Use(); return; }
            string focus = GUI.GetNameOfFocusedControl();
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                if (focus == "StoryPageJump" && SelectedNode != null)
                {
                    int count = graph.Content(selected).Count;
                    if (int.TryParse(jumpText, out int number) && number > 0 && number <= count) MoveToPage(number - 1);
                    else info = "请输入 1～" + count + " 的配置页码。";
                    e.Use(); return;
                }
                if ((focus == "StoryPageSearch" || focus == "StoryPageDirectorySearch") && SelectedNode != null)
                { FindNextPage(graph.Content(selected)); e.Use(); return; }
                if (focus == "StorySearch" && visible.Count > 0)
                { Select(visible[0].Id); GUI.FocusControl(null); e.Use(); return; }
            }
            if (focus == "StorySearch" || focus == "StoryPageSearch" || focus == "StoryPageJump" || focus == "StoryPageDirectorySearch") return;
            if (e.alt && e.keyCode == KeyCode.LeftArrow) { GoBack(); e.Use(); }
            else if (SelectedNode != null && (e.keyCode == KeyCode.PageDown || e.keyCode == KeyCode.PageUp))
            { MoveToPage(page + (e.keyCode == KeyCode.PageDown ? 1 : -1)); e.Use(); }
        }
        private void Filters(Rect rect)
        {
            Fill(rect, panel); Frame(rect, Rule); GUI.BeginGroup(rect);
            try
            {
                float w = rect.width - 20;
                GUI.Label(new Rect(10, 8, w, 22), "1  选择剧情", heading);
                GUI.SetNextControlName("StorySearch");
                string next = GUI.TextField(new Rect(10, 36, w - 34, 29), query, field);
                if (next != query) { query = next; Rebuild(); }
                if (GUI.Button(new Rect(w - 19, 36, 29, 29), "×", button)) { query = ""; Rebuild(); GUI.FocusControl(null); }
                GUI.Label(new Rect(10, 68, w, 20), "ID / 标题 / 正文（当前筛选内）", tiny);
                float half = (w - 4) / 2;
                for (int i = 0; i < Scopes.Length; i++)
                    if (Chip(new Rect(10 + i % 2 * (half + 4), 94 + i / 2 * 28, half, 25), Scopes[i], scope == i))
                    { scope = i; onlyNeighbors = false; Rebuild(); Home(); }
                for (int i = 0; i < Factions.Length; i++)
                    if (Chip(new Rect(10 + i % 2 * (half + 4), 184 + i / 2 * 28, half, 25), Factions[i], faction == i))
                    { faction = i; Rebuild(); Home(); }
                if (GUI.Button(new Rect(10, 272, half, 25), advancedFilters ? "收起更多" : "更多筛选", button)) advancedFilters = !advancedFilters;
                if (GUI.Button(new Rect(14 + half, 272, half, 25), "重置筛选", button))
                { scope = 1; faction = 0; query = ""; onlyNeighbors = false; showFacts = false; Rebuild(); Home(); }
                float listY = 305;
                if (advancedFilters)
                {
                    bool f = Toggle(new Rect(10, listY, w, 23), showFacts, "显示表外条件");
                    if (f != showFacts) { showFacts = f; Rebuild(); }
                    bool n = Toggle(new Rect(10, listY + 24, w, 23), onlyNeighbors, "仅选中及相邻");
                    if (n != onlyNeighbors) { onlyNeighbors = n; Rebuild(); }
                    labels = Toggle(new Rect(10, listY + 48, w, 23), labels, "显示关系标签");
                    bool enabled = GUI.enabled; GUI.enabled = enabled && pendingRequest == null && (RefreshRequested == null || CanScheduleRequest);
                    if (GUI.Button(new Rect(10, listY + 75, w, 26), RefreshRequested == null ? "重读生成配置" : "刷新当前快照", button)) RequestRefresh();
                    GUI.enabled = enabled; listY += 109;
                }
                GUI.Label(new Rect(10, listY, w, 23), "匹配 " + visible.Count + " / " + graph.Nodes.Count + " 项", muted); listY += 26;
                if (visible.Count == 0) GUI.Label(new Rect(12, listY + 12, w - 4, 90), "没有匹配剧情。\n试试清空搜索或重置筛选；搜索不会自动解除筛选。", body);
                float listHeight = Mathf.Max(40, rect.height - listY - 6);
                filterScroll = GUI.BeginScrollView(new Rect(4, listY, rect.width - 8, listHeight), filterScroll, new Rect(0, 0, rect.width - 29, visible.Count * 70));
                string clicked = null;
                for (int i = 0; i < visible.Count; i++)
                {
                    float y = i * 70; if (y + 70 < filterScroll.y || y > filterScroll.y + listHeight) continue;
                    var n = visible[i]; float width = rect.width - 33;
                    if (GUI.Button(new Rect(2, y, width, 66), GUIContent.none, selected == n.Id ? chipActive : chip)) clicked = n.Id;
                    Fill(new Rect(2, y, 3, 66), LaneColor(n.Group));
                    GUI.Label(new Rect(10, y + 4, width - 16, 21), new GUIContent(SingleLine(n.Title), n.Title), small);
                    GUI.Label(new Rect(10, y + 26, width - 16, 18), new GUIContent(n.Key, n.Key), tiny);
                    string status = NodeStatus(n);
                    GUI.Label(new Rect(10, y + 46, width - 16, 18), new GUIContent(SingleLine(status ?? (n.Blocked ? "内容闸门 · 阻塞" : NarrativeRelationshipGraph.GroupLabel(n.Group))), status), tiny);
                }
                GUI.EndScrollView();
                if (clicked != null) { Select(clicked); pageListScroll = Vector2.zero; Focus(clicked); GUI.FocusControl(null); }
            }
            finally { GUI.EndGroup(); }
        }
        private static string ReaderKind(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return "配置";
            int start = kind.IndexOf('（'), end = kind.IndexOf('）');
            return start >= 0 && end > start ? kind.Remove(start, end - start + 1) : kind;
        }
        private void PageDirectory(Rect rect)
        {
            Fill(rect, Paper); Frame(rect, Rule); GUI.BeginGroup(rect);
            try
            {
                float w = rect.width - 20;
                GUI.Label(new Rect(10, 8, w, 22), "2  页目录 / 分支检查", heading);
                GUI.Label(new Rect(10, 37, w, 43), "配置序号不是实际播放路线\n互斥页也会列出，不表示已读", tiny);
                if (SelectedNode == null) return;
                var blocks = graph.Content(selected);
                GUI.SetNextControlName("StoryPageDirectorySearch");
                string next = GUI.TextField(new Rect(10, 85, w - 32, 28), pageQuery, field);
                if (next != pageQuery) { pageQuery = next; pageListScroll = Vector2.zero; }
                if (GUI.Button(new Rect(rect.width - 39, 85, 29, 28), "×", button)) { pageQuery = ""; pageListScroll = Vector2.zero; }
                var matches = MatchingPages(blocks);
                GUI.Label(new Rect(10, 119, w, 20), "搜索页 ID / 正文 / 选项 · " + matches.Count + " 页", tiny);
                if (matches.Count == 0) GUI.Label(new Rect(10, 160, w, 90), "本段没有匹配页。\n清空搜索查看全部配置页。", body);
                pageListHeight = rect.height - 153;
                pageListScroll = GUI.BeginScrollView(new Rect(4, 147, rect.width - 8, pageListHeight), pageListScroll, new Rect(0, 0, rect.width - 28, matches.Count * 73));
                for (int row = 0; row < matches.Count; row++)
                {
                    float y = row * 73; if (y + 73 < pageListScroll.y || y > pageListScroll.y + rect.height - 147) continue;
                    int index = matches[row]; var block = blocks[index]; float width = rect.width - 32;
                    if (GUI.Button(new Rect(2, y, width, 69), GUIContent.none, page == index ? chipActive : chip)) { MoveToPage(index); GUI.FocusControl(null); }
                    GUI.Label(new Rect(9, y + 4, width - 15, 19), (index + 1) + "  ·  " + ReaderKind(block.KindLabel), small);
                    GUI.Label(new Rect(9, y + 25, width - 15, 19), new GUIContent(SingleLine(block.Summary), block.Summary), tiny);
                    GUI.Label(new Rect(9, y + 47, width - 15, 18), new GUIContent(block.Id, block.Id), tiny);
                }
                GUI.EndScrollView();
            }
            finally { GUI.EndGroup(); }
        }
        private void Details(Rect rect)
        {
            Fill(rect, Paper); Frame(rect, Rule); GUI.BeginGroup(rect);
            try
            {
                float w = rect.width, inner = w - 42;
                var n = SelectedNode;
                if (n == null)
                {
                    float introY = 20;
                    GUI.BeginGroup(new Rect(20, 0, w - 40, rect.height));
                    Paragraph(ref introY, w - 40, "3  阅读、定位和试播", title);
                    Paragraph(ref introY, w - 40, "从左侧选择一段剧情，再点击页目录检查正文和分支。\n\n文案阅览：优先显示台词和选项，技术条件可展开。\n关系图：查看事件间的前置、排斥和续接证据。\n\n配置翻页不等于剧情推进，看到一页不代表该分支自然可达。\n本页预览只检查样式；从头试播才会按独立副本的条件与选择推进。\n\n遇到问题可复制定位信息发给程序，无需手抄长 ID。", body);
                    GUI.EndGroup(); return;
                }
                GUI.Label(new Rect(14, 8, w - 28, 26), new GUIContent(n.Title, n.Title), heading);
                GUI.Label(new Rect(14, 36, w - 28, 20), new GUIContent(n.Key, n.Key), tiny);
                float third = (w - 36) / 3;
                if (GUI.Button(new Rect(12, 62, third, 27), "复制问题定位", button)) CopyText(IssueLocation(), "已复制问题模板、事件 / 页 ID 与当前配置；请补充问题和预期。");
                if (GUI.Button(new Rect(18 + third, 62, third, 27), "复制事件 ID", button)) CopyText(n.Key, "已复制事件 ID：" + n.Key);
                if (GUI.Button(new Rect(24 + 2 * third, 62, third, 27), mapMode ? "居中节点" : "查看关系图", button))
                { IsMapMode = true; focusAfterLayout = selected; }
                for (int i = 0; i < Tabs.Length; i++)
                    if (Chip(new Rect(12 + i * (w - 24) / 3, 97, (w - 24) / 3 - 2, 28), Tabs[i], tab == i)) { tab = i; detailScroll = Vector2.zero; info = ""; }
                var blocks = graph.Content(n.Id);
                page = blocks.Count == 0 ? 0 : Mathf.Clamp(page, 0, blocks.Count - 1);
                float top = 135;
                if (tab == 0 && blocks.Count > 0)
                {
                    bool enabled = GUI.enabled;
                    GUI.enabled = enabled && page > 0;
                    if (GUI.Button(new Rect(12, top, 66, 28), "上一页", button)) MoveToPage(page - 1);
                    GUI.enabled = enabled && page < blocks.Count - 1;
                    if (GUI.Button(new Rect(84, top, 66, 28), "下一页", button)) MoveToPage(page + 1);
                    GUI.enabled = enabled;
                    GUI.Label(new Rect(158, top + 3, w - 286, 23), (page + 1) + " / " + blocks.Count, small);
                    GUI.SetNextControlName("StoryPageJump");
                    jumpText = GUI.TextField(new Rect(w - 123, top, 61, 28), jumpText, field);
                    if (GUI.Button(new Rect(w - 58, top, 46, 28), "跳页", button))
                    { if (int.TryParse(jumpText, out int number) && number > 0 && number <= blocks.Count) MoveToPage(number - 1); else info = "请输入 1～" + blocks.Count + " 的配置页码。"; }
                    top += 34;
                    GUI.Label(new Rect(14, top, w - 28, 21), "按配置顺序翻页，非分支路径 · " + ReaderKind(blocks[page].KindLabel), tiny); top += 26;
                    GUI.SetNextControlName("StoryPageSearch");
                    pageQuery = GUI.TextField(new Rect(12, top, w - 104, 27), pageQuery, field);
                    if (GUI.Button(new Rect(w - 86, top, 74, 27), "查下一处", button)) FindNextPage(blocks);
                    top += 33;
                }
                // Keep preview actions in a fixed area: long dialogue cannot scroll them out of reach.
                if (PreviewRequested != null)
                {
                    string reason = Annotation(PreviewBlockReason, n);
                    bool enabled = GUI.enabled; GUI.enabled = enabled && pendingRequest == null && CanScheduleRequest && string.IsNullOrEmpty(reason);
                    float half = (w - 30) / 2;
                    bool rootEnabled = GUI.enabled; GUI.enabled = rootEnabled && !string.IsNullOrEmpty(SelectedPageId);
                    if (GUI.Button(new Rect(12, top, half, 29), new GUIContent("本页样式预览", "忽略本页条件，只看样式；选项不结算。不写正式存档。"), button)) QueuePreview(n, SelectedPageId);
                    GUI.enabled = rootEnabled;
                    if (GUI.Button(new Rect(18 + half, top, half, 29), new GUIContent("从头分支试播", "按独立副本条件、选择和检定推进；真实制作会停止。不写正式存档。"), button)) QueuePreview(n, null);
                    GUI.enabled = enabled; top += 35;
                }
                detailScroll = GUI.BeginScrollView(new Rect(8, top, w - 16, Mathf.Max(40, rect.height - top - 8)), detailScroll, new Rect(0, 0, inner, detailContentHeight));
                try
                {
                    float y = 4;
                    string status = NodeStatus(n);
                    if (!string.IsNullOrEmpty(status)) Paragraph(ref y, inner, "会话状态：" + status, wrapped);
                    if (PreviewRequested != null)
                    {
                        string reason = Annotation(PreviewBlockReason, n);
                        Paragraph(ref y, inner, string.IsNullOrEmpty(reason)
                            ? "不写正式存档 · 单页忽略条件 / 选项不结算；从头按副本条件走分支，遇真实制作停止。"
                            : "暂不可试播：" + reason, wrapped);
                    }
                    if (!string.IsNullOrEmpty(info)) Paragraph(ref y, inner, info, wrapped);
                    if (tab == 0) ContentDetails(n, inner, ref y);
                    else if (tab == 1)
                    {
                        Paragraph(ref y, inner, Annotation(StatusDetails, n), body);
                        string description = graph.Describe(n.Id);
                        if (DetailButton(ref y, inner, "复制入口、条件与结果")) CopyText(description, "已复制配置诊断。");
                        Paragraph(ref y, inner, description, body);
                    }
                    else RelationshipDetails(n, inner, ref y);
                    detailContentHeight = y + 16;
                }
                finally { GUI.EndScrollView(); }
            }
            finally { GUI.EndGroup(); }
        }
        private void FindNextPage(IReadOnlyList<StoryContentBlock> blocks)
        {
            if (string.IsNullOrWhiteSpace(pageQuery)) { info = "输入页面 ID、正文或选项文字后查找。"; return; }
            var matches = MatchingPages(blocks);
            if (matches.Count == 0) { info = "当前剧情没有匹配页。"; return; }
            int next = matches[0]; foreach (int index in matches) if (index > page) { next = index; break; }
            MoveToPage(next); info = "匹配 " + matches.Count + " 页；已定位第 " + (next + 1) + " 页（再次点击查下一处）。";
        }
        private void ContentDetails(StoryGraphNode node, float width, ref float y)
        {
            var blocks = graph.Content(node.Id);
            if (blocks.Count == 0) { Paragraph(ref y, width, "此节点没有正文页，请查看入口 / 结果或关系证据。", body); return; }
            var block = blocks[page];
            Paragraph(ref y, width, "页面 ID：" + block.Id, wrapped);
            if (!string.IsNullOrEmpty(block.NextPageId))
            {
                if (DetailButton(ref y, width, "查看显式跳转页 → " + block.NextPageId))
                { if (!TrySelectPage(block.NextPageId)) info = "该跳转目标不在当前正文中，请核对配置；没有推测或补造目标。"; }
                Paragraph(ref y, width, "这里只定位 nextPageId，不执行条件或结果。", tiny);
            }
            if (GUI.Button(new Rect(0, y, 102, 27), "字号 " + readingSize + "  −", button)) readingSize = Mathf.Max(13, readingSize - 1);
            if (GUI.Button(new Rect(108, y, 52, 27), "+", button)) readingSize = Mathf.Min(22, readingSize + 1);
            if (GUI.Button(new Rect(166, y, width - 166, 27), "复制本页", button)) CopyText(node.Key + "\n" + block.Id + "\n\n" + block.Text, "已复制当前页正文、选项和定位。");
            y += 38;
            readingBody ??= new GUIStyle(body) { wordWrap = true };
            readingBody.fontSize = readingSize;
            if ((block.KindLabel ?? "").Contains("条件页"))
                Paragraph(ref y, width, "条件页：是否出现由配置条件决定，不是线性下一句；下方可展开条件证据。", wrapped);
            Paragraph(ref y, width, string.IsNullOrEmpty(block.BodyText) ? block.Diagnostics : block.BodyText, readingBody);
            Section(ref y, width, "页面配置 / 分支条件（只读）");
            if (DetailButton(ref y, width, showPageDiagnostics ? "收起技术详情与条件" : "展开技术详情与条件")) showPageDiagnostics = !showPageDiagnostics;
            if (showPageDiagnostics)
            {
                Paragraph(ref y, width, block.Diagnostics, body);
                Paragraph(ref y, width, Annotation(StatusDetails, node), body);
            }
        }
    }
}
