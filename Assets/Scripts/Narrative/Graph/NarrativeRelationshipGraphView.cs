using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Spotlight.Narrative
{
    /// <summary>Shared archive-style IMGUI view for scenes, EditorWindow and embedded GM hosts.
    /// Owns only UI resources; providers are observational, preview execution belongs to the host.
    /// Construct, draw, load and dispose on the Unity main thread.</summary>
    public sealed partial class NarrativeRelationshipGraphView : IDisposable
    {
        private NarrativeRelationshipGraph graph;
        private readonly Dictionary<string, Rect> positions = new Dictionary<string, Rect>(StringComparer.Ordinal);
        private readonly List<StoryGraphNode> visible = new List<StoryGraphNode>();
        private readonly List<DisplayEdge> links = new List<DisplayEdge>();
        private readonly List<string> lanes = new List<string>();
        private readonly Dictionary<int, float> dayColumns = new Dictionary<int, float>();
        private readonly HashSet<string> neighbors = new HashSet<string>(StringComparer.Ordinal);
        private string selected, query = "", pageQuery = "", error, info = "";
        private int scope = 1, faction, tab, page;
        private bool onlyNeighbors, labels = true, showFacts, disposed, built;
        private float zoom = .8f;
        private Vector2 pan = new Vector2(32, 54), filterScroll, detailScroll, outerScroll;
        private Rect viewport;
        private GUIStyle title, small, muted, heading, button, field, body, nodeTitle, tiny, wrapped, chip, chipActive;
        private GUIStyle zoomId, zoomTitle;
        private Font font, headingFont;
        private bool ownsFont;
        private GUISkin skin;
        private Texture2D white;
        private readonly List<Texture2D> textures = new List<Texture2D>();
        private readonly Dictionary<string, string> statusLabels = new Dictionary<string, string>(StringComparer.Ordinal);
        private SynchronizationContext mainContext;
        private Action pendingRequest;
        private int requestTicket;
#if UNITY_EDITOR
        private UnityEditor.EditorApplication.CallbackFunction editorDispatch;
#endif
        private float detailContentHeight = 600;
        // Matches Details' brown ink (.35, .19, .07) and Map's parchment / warm gold accents.
        private static readonly Color Ink = new Color(.35f, .19f, .07f);
        private static readonly Color MutedInk = new Color(.46f, .35f, .23f);
        private static readonly Color Brass = new Color(.64f, .43f, .19f);
        private static readonly Color Rule = new Color(.72f, .60f, .42f);
        private static readonly Color Paper = new Color(.97f, .93f, .83f);
        private static readonly Color SelectedPaper = new Color(.94f, .83f, .61f);
        private static readonly Color Seal = new Color(.61f, .24f, .16f);
        private static readonly Color bg = new Color(.89f, .83f, .70f);
        private static readonly Color panel = new Color(.94f, .88f, .76f);
        private sealed class DisplayEdge { public StoryGraphEdge First; public int Count; public string Proof; }
        private static readonly string[] Scopes = { "主线 / 分支", "全部配置", "日常 / 首访", "开场 / 教程", "仅阻塞" };
        private static readonly string[] Factions = { "全部阵营", "中立 / 经营", "新玫瑰", "王庭教会", "革命联合" };
        private static readonly string[] FactionKeys = { "", "neutral", "rose", "church", "revolution" };
        private static readonly string[] Tabs = { "正文 / 选项", "入口 / 结果", "关系证据" };
        private Rect worldBounds;
        /// <summary>Optional live, read-only host annotations. Never evaluated by the graph model.</summary>
        public Func<StoryGraphNode, string> StatusLabel { get; set; }
        public Func<StoryGraphNode, string> StatusDetails { get; set; }
        /// <summary>Null/empty permits preview; any nonempty value is a visible blocking reason.</summary>
        public Func<StoryGraphNode, string> PreviewBlockReason { get; set; }
        /// <summary>Invoked after IMGUI on the main thread. Page is the exact block.Id, or null for root playback.</summary>
        public Action<StoryGraphNode, string> PreviewRequested { get; set; }
        /// <summary>Optional host refresh (e.g. enqueue EnsureData(force)). Prevents injected snapshots switching to disk.</summary>
        public Action RefreshRequested { get; set; }
        public string ContextLabel { get; set; }
        public string SelectedNodeId => selected;
        public StoryGraphNode SelectedNode => graph?.Find(selected);
        public string SelectedPageId
        {
            get
            {
                var node = SelectedNode;
                if (node == null) return null;
                var blocks = graph.Content(node.Id);
                return blocks.Count == 0 ? null : blocks[Mathf.Clamp(page, 0, blocks.Count - 1)].Id;
            }
        }
        public int VisibleNodeCount => visible.Count;

        /// <summary>Rebuild from the supplied snapshot; never reads disk or creates a runtime.</summary>
        public void Load(NarrativeConfiguration config)
        {
            if (disposed) return;
            CancelPendingRequest();
            built = true;
            try
            {
                // Build before replacing so no partially constructed graph can escape.
                string previousPage = SelectedPageId;
                var nextGraph = NarrativeRelationshipGraph.Build(config);
                graph = nextGraph;
                error = null;
                if (graph.Find(selected) == null) selected = null;
                page = selected == null ? 0 : Math.Max(0, NarrativeRelationshipGraph.FindPageIndex(graph.Content(selected), previousPage));
                pageQuery = ""; info = ""; detailScroll = Vector2.zero;
                ResetNavigation();
                RefreshNeighbors();
                Rebuild();
            }
            catch (Exception ex) { error = ex.Message; graph = null; selected = null; ResetNavigation(); Rebuild(); }
        }

        /// <summary>Original standalone behavior: reload the generated configuration from Luban.</summary>
        public void Reload()
        {
            if (disposed) return;
            try { Load(LubanNarrativeLoader.Load()); }
            catch (Exception ex)
            {
                CancelPendingRequest(); error = ex.Message; graph = null; selected = null; built = true; ResetNavigation(); Rebuild();
            }
        }

        private void RefreshNeighbors()
        {
            neighbors.Clear();
            if (graph == null || selected == null) return;
            neighbors.Add(selected);
            foreach (var edge in graph.Connections(selected)) neighbors.Add(edge.From == selected ? edge.To : edge.From);
        }

        public void Select(string nodeId)
        {
            if (graph?.Find(nodeId) == null) return;
            bool hidden = !positions.ContainsKey(nodeId);
            if (selected == nodeId) return;
            RememberLocation();
            selected = nodeId; page = 0; pageQuery = ""; detailScroll = Vector2.zero; info = "";
            pageListScroll = Vector2.zero; jumpText = ""; copyNotice = "";
            if (!string.IsNullOrWhiteSpace(query))
            {
                var matches = NarrativeRelationshipGraph.FindMatchingPages(graph.Content(nodeId), query);
                if (matches.Count > 0) { page = matches[0]; pageQuery = query; info = "已定位搜索命中页；当前显示配置正文，不代表实际播放路线。"; }
            }
            RefreshNeighbors();
            if (hidden)
            {
                var node = graph.Find(nodeId);
                scope = node.Group == "daily" ? 2 : NarrativeRelationshipGraph.IsStory(node.Key) || node.Kind == StoryNodeKind.Ending ? 0 : 1;
                faction = 0; query = ""; onlyNeighbors = false;
                if (node.Kind == StoryNodeKind.Fact) showFacts = true;
                Rebuild(); Focus(nodeId);
            }
            else if (onlyNeighbors) Rebuild();
            if (mapMode) focusAfterLayout = nodeId;
        }
        private void Styles()
        {
            if (skin != null) return;
            white = Texture(Color.white);
            // Resource fonts are borrowed project assets, never destroyed by this view.
            font = Resources.Load<Font>("Fonts/Map/SourceHanSansSC-Regular-2");
            headingFont = Resources.Load<Font>("Fonts/Map/SourceHanSerifCN-Heavy-4");
            if (font == null)
            {
                font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "Arial" }, 14);
                ownsFont = font != null;
                if (ownsFont) font.hideFlags = HideFlags.HideAndDontSave;
            }
            if (headingFont == null) headingFont = font;
            // Do not mutate GUI.skin (the editor/GM may draw other controls after us).
            skin = UnityEngine.Object.Instantiate(GUI.skin);
            skin.hideFlags = HideFlags.HideAndDontSave;
            skin.name = "Narrative Archive Skin (view owned)";
            skin.font = font;
            skin.settings.cursorColor = Ink;
            skin.settings.selectionColor = new Color(.68f, .47f, .20f, .32f);
            title = Style(24, Ink, true); title.font = headingFont;
            small = Style(12, Ink); muted = Style(12, MutedInk); tiny = Style(11, MutedInk);
            heading = Style(14, Ink, true); heading.font = headingFont;
            nodeTitle = Style(14, Ink, true); nodeTitle.wordWrap = true; nodeTitle.font = headingFont;
            body = Style(14, Ink); body.wordWrap = true; body.padding = new RectOffset(10, 10, 9, 9);
            wrapped = Style(12, MutedInk); wrapped.wordWrap = true;
            button = ControlStyle(new Color(.90f, .81f, .64f), SelectedPaper);
            field = ControlStyle(Paper, new Color(1f, .97f, .88f));
            field.alignment = TextAnchor.MiddleLeft;
            chip = ControlStyle(panel, Paper); chip.alignment = TextAnchor.MiddleLeft;
            chipActive = ControlStyle(SelectedPaper, SelectedPaper); chipActive.alignment = TextAnchor.MiddleLeft;
            chipActive.fontStyle = FontStyle.Bold;
            skin.button = button; skin.textField = field; skin.textArea = new GUIStyle(field) { wordWrap = true };
            skin.label = small; skin.box = ControlStyle(panel, panel);
            skin.scrollView = new GUIStyle(); // Keep the archive paper visible, not the host skin's dark well.
            var track = Texture(new Color(.83f, .75f, .60f));
            var thumb = Texture(new Color(.64f, .49f, .30f));
            var thumbHover = Texture(Brass);
            skin.horizontalScrollbar = TrackStyle(track, 0, 12);
            skin.verticalScrollbar = TrackStyle(track, 12, 0);
            skin.horizontalScrollbarThumb = TrackStyle(thumb, 0, 12, thumbHover);
            skin.verticalScrollbarThumb = TrackStyle(thumb, 12, 0, thumbHover);
            skin.horizontalScrollbarLeftButton = EmptyControl(); skin.horizontalScrollbarRightButton = EmptyControl();
            skin.verticalScrollbarUpButton = EmptyControl(); skin.verticalScrollbarDownButton = EmptyControl();
            skin.horizontalSlider = TrackStyle(track, 0, 6);
            skin.horizontalSlider.margin = new RectOffset(0, 0, 7, 7);
            skin.horizontalSliderThumb = TrackStyle(thumb, 13, 20, thumbHover);
            zoomId = new GUIStyle(tiny); zoomTitle = new GUIStyle(nodeTitle);
        }

        private GUIStyle ControlStyle(Color surface, Color hover)
        {
            var style = Style(12, Ink);
            style.alignment = TextAnchor.MiddleCenter;
            style.padding = new RectOffset(8, 8, 4, 4);
            style.border = new RectOffset(1, 1, 1, 1);
            var rest = Texture(surface, Rule);
            var over = Texture(hover, Brass);
            var pressed = Texture(SelectedPaper, Brass);
            State(style.normal, rest); State(style.hover, over); State(style.active, pressed); State(style.focused, over);
            State(style.onNormal, pressed); State(style.onHover, over); State(style.onActive, pressed); State(style.onFocused, over);
            return style;
        }
        private static void State(GUIStyleState state, Texture2D texture) { state.background = texture; state.textColor = Ink; }
        private static GUIStyle EmptyControl() => new GUIStyle { fixedWidth = 0, fixedHeight = 0, margin = new RectOffset(), padding = new RectOffset() };
        private static GUIStyle TrackStyle(Texture2D rest, float width, float height, Texture2D hover = null)
        {
            var style = EmptyControl(); style.fixedWidth = width; style.fixedHeight = height;
            State(style.normal, rest); State(style.hover, hover ?? rest); State(style.active, hover ?? rest); State(style.focused, hover ?? rest);
            State(style.onNormal, rest); State(style.onHover, hover ?? rest); State(style.onActive, hover ?? rest); State(style.onFocused, hover ?? rest);
            return style;
        }
        private Texture2D Texture(Color surface, Color? border = null)
        {
            var texture = new Texture2D(3, 3) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.name = "Narrative Archive Swatch (view owned)";
            for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++) texture.SetPixel(x, y, border.HasValue && (x != 1 || y != 1) ? border.Value : surface);
            texture.Apply(); textures.Add(texture); return texture;
        }
        private GUIStyle Style(int size, Color color, bool bold = false) => new GUIStyle { font = font, fontSize = size, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
            normal = { textColor = color }, clipping = TextClipping.Clip, alignment = TextAnchor.UpperLeft, richText = false };
        private void Fill(Rect r, Color c) { Color old = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = old; }
        private void Frame(Rect r, Color c)
        {
            Fill(new Rect(r.x, r.y, r.width, 1), c); Fill(new Rect(r.x, r.yMax - 1, r.width, 1), c);
            Fill(new Rect(r.x, r.y, 1, r.height), c); Fill(new Rect(r.xMax - 1, r.y, 1, r.height), c);
        }
        private string Annotation(Func<StoryGraphNode, string> provider, StoryGraphNode node)
        {
            if (provider == null) return null;
            try { return provider(node); }
            catch (Exception ex) { return "宿主状态读取失败：" + ex.Message; }
        }
        private string NodeStatus(StoryGraphNode node)
        {
            if (!statusLabels.TryGetValue(node.Id, out var value))
            {
                value = Annotation(StatusLabel, node);
                statusLabels[node.Id] = value;
            }
            return value;
        }
        private static Color LaneColor(string group)
        {
            switch (group)
            {
                case "neutral": return new Color(.62f, .43f, .20f);
                case "rose": return new Color(.65f, .33f, .35f);
                case "church": return new Color(.51f, .43f, .56f);
                case "revolution": return new Color(.39f, .48f, .32f);
                case "ending": return Brass;
                case "daily": return new Color(.56f, .46f, .30f);
                case "fact": return new Color(.52f, .48f, .41f);
                default: return new Color(.59f, .38f, .26f);
            }
        }
        private static Color EdgeColor(StoryLinkKind kind)
        {
            switch (kind)
            {
                case StoryLinkKind.Continuation: return new Color(.36f, .45f, .25f);
                case StoryLinkKind.Exclusion: return Seal;
                case StoryLinkKind.CheckSuccess: return new Color(.35f, .47f, .31f);
                case StoryLinkKind.CheckFailure: return new Color(.70f, .36f, .16f);
                case StoryLinkKind.Ending: return Brass;
                case StoryLinkKind.Unresolved: return new Color(.51f, .46f, .39f);
                default: return new Color(.52f, .39f, .49f);
            }
        }
        public void Draw(Rect bounds)
        {
            if (disposed || bounds.width < 100 || bounds.height < 100) return;
            Styles();
            if (!built) Reload();
            // A generic SynchronizationContext posts to the thread pool, not Unity's main thread.
            var context = SynchronizationContext.Current;
            if (context != null && context.GetType().FullName == "UnityEngine.UnitySynchronizationContext") mainContext = context;
            statusLabels.Clear(); // Live host state, but evaluate each label at most once per draw.
            var oldSkin = GUI.skin;
            var oldColor = GUI.color; var oldBackground = GUI.backgroundColor; var oldContent = GUI.contentColor;
            try
            {
                GUI.skin = skin; GUI.color = Color.white; GUI.backgroundColor = Color.white; GUI.contentColor = Color.white;
                GUI.BeginGroup(bounds);
                try
                {
                    // Embedded hosts can be narrow: retain all controls with an outer fallback scroll.
                    float width = Mathf.Max(800, bounds.width), height = Mathf.Max(620, bounds.height);
                    bool compact = width > bounds.width || height > bounds.height;
                    if (compact)
                    {
                        width = Mathf.Max(800, bounds.width - 14); height = Mathf.Max(620, bounds.height - 14);
                        outerScroll = GUI.BeginScrollView(new Rect(0, 0, bounds.width, bounds.height), outerScroll, new Rect(0, 0, width, height));
                    }
                    try { DrawArchive(new Rect(0, 0, width, height)); }
                    finally { if (compact) GUI.EndScrollView(); }
                }
                finally { GUI.EndGroup(); }
            }
            finally { GUI.skin = oldSkin; GUI.color = oldColor; GUI.backgroundColor = oldBackground; GUI.contentColor = oldContent; }
        }

        private static string SingleLine(string value) => (value ?? "").Replace('\r', ' ').Replace('\n', ' ');
        private bool Toggle(Rect r, bool value, string label) => GUI.Button(r, (value ? "[√]  " : "[  ]  ") + label, small) ? !value : value;
        private bool Chip(Rect r, string label, bool active) => GUI.Button(r, label, active ? chipActive : chip);
        private void Rebuild()
        {
            filterScroll = Vector2.zero;
            visible.Clear();positions.Clear();links.Clear();lanes.Clear();dayColumns.Clear();
            if(graph==null)return;
            IEnumerable<StoryGraphNode> candidates=graph.Nodes;
            if(scope==0)candidates=candidates.Where(n=>NarrativeRelationshipGraph.IsStory(n.Key)||n.Kind==StoryNodeKind.Ending&&!n.Sample||showFacts&&n.Kind==StoryNodeKind.Fact);
            else if(scope==1)candidates=candidates.Where(n=>showFacts||n.Kind!=StoryNodeKind.Fact);
            else if(scope==2)candidates=candidates.Where(n=>n.Group=="daily"||showFacts&&n.Kind==StoryNodeKind.Fact);
            else if(scope==3)candidates=candidates.Where(n=>new[]{"opening","tutorial","ambient"}.Contains(n.Group));
            else candidates=candidates.Where(n=>n.Blocked||showFacts&&n.Kind==StoryNodeKind.Fact);
            if(faction>0)candidates=candidates.Where(n=>n.Group==FactionKeys[faction]||n.Kind==StoryNodeKind.Ending||showFacts&&n.Kind==StoryNodeKind.Fact);
            if(!string.IsNullOrWhiteSpace(query)) candidates=candidates.Where(n=>(n.SearchText??n.Key).IndexOf(query.Trim(),StringComparison.OrdinalIgnoreCase)>=0||n.Title.IndexOf(query.Trim(),StringComparison.OrdinalIgnoreCase)>=0);
            if(onlyNeighbors&&selected!=null)candidates=candidates.Where(n=>neighbors.Contains(n.Id));
            visible.AddRange(candidates);
            var ids=new HashSet<string>(visible.Select(n=>n.Id),StringComparer.Ordinal);
            string[] order={"opening","tutorial","neutral","rose","church","revolution","daily","newspaper","news","ambient","ending","sample","fact"};
            var groups=visible.Select(n=>n.Lane).Distinct().OrderBy(l=>Array.IndexOf(order,visible.First(n=>n.Lane==l).Group)).ThenBy(l=>l,StringComparer.Ordinal).ToArray();
            lanes.AddRange(groups);
            var days=visible.Select(n=>n.Day).Where(d=>d>0).Distinct().OrderBy(d=>d).ToList();
            if(visible.Any(n=>n.Day==0))days.Add(0);
            float x=0;
            foreach(int day in days){dayColumns[day]=x;x+=318;}
            float y=0;
            foreach(string lane in lanes){
                var row=visible.Where(n=>n.Lane==lane).ToList();int stacks=1;
                foreach(var byday in row.GroupBy(n=>n.Day)){
                    int index=0; foreach(var n in byday.OrderBy(n=>n.Key,StringComparer.Ordinal)){positions[n.Id]=new Rect(dayColumns[n.Day],y+index*129,282,111);index++;} stacks=Math.Max(stacks,index);
                }
                y+=stacks*129+44;
            }
            worldBounds=new Rect(-30,-55,Mathf.Max(360,x),Mathf.Max(240,y));
            foreach(var group in graph.Edges.Where(e=>ids.Contains(e.From)&&ids.Contains(e.To)).GroupBy(e=>e.From+"\n"+e.To+"\n"+e.Kind)){
                var list=group.ToList(); links.Add(new DisplayEdge{First=list[0],Count=list.Count,Proof=string.Join("\n\n",list.Take(12).Select(e=>e.Label+"\n"+e.Evidence))+(list.Count>12?"\n……其余证据见节点关系页":"")});
            }
        }
        private void Home(){zoom=.78f;pan=new Vector2(26,64);}
        private void Fit()
        {
            if(viewport.width<=0)return;
            zoom=Mathf.Clamp(Mathf.Min((viewport.width-55)/worldBounds.width,(viewport.height-70)/worldBounds.height),.045f,1.3f);
            pan=new Vector2((viewport.width-worldBounds.width*zoom)*.5f-worldBounds.x*zoom,(viewport.height-worldBounds.height*zoom)*.5f-worldBounds.y*zoom);
        }
        private void Focus(string id){if(positions.TryGetValue(id,out var r)){zoom=Mathf.Max(.65f,zoom);pan=viewport.size*.5f-r.center*zoom;}}
        private Rect ScreenRect(Rect world)=>new Rect(pan+world.position*zoom,world.size*zoom);
        private Vector2 Point(Vector2 world)=>pan+world*zoom;
        private void Canvas(Rect rect)
        {
            GUI.BeginGroup(rect); Rect clip=new Rect(0,0,rect.width,rect.height);Fill(clip,bg);
            Event e=Event.current;Vector2 mouse=e.mousePosition;
            if(clip.Contains(mouse)){
                if(e.type==EventType.ScrollWheel){float old=zoom;zoom=Mathf.Clamp(zoom*Mathf.Pow(1.12f,-e.delta.y),.045f,1.6f);pan=mouse-(mouse-pan)*(zoom/old);e.Use();}
                if(e.type==EventType.MouseDrag&&(e.button==1||e.button==2||e.button==0&&e.alt)){pan+=e.delta;e.Use();}
                if(e.type==EventType.KeyDown&&GUIUtility.keyboardControl==0&&!e.control&&!e.command&&!e.alt){if(e.keyCode==KeyCode.F){Fit();e.Use();}else if(e.keyCode==KeyCode.Home){Home();e.Use();}}
            }
            if(e.type==EventType.Repaint){
                float grid=Mathf.Max(28,80*zoom);Color line=new Color(.70f,.58f,.40f,.15f);
                for(float x=pan.x%grid;x<rect.width;x+=grid)Fill(new Rect(x,0,1,rect.height),line);
                for(float y=pan.y%grid;y<rect.height;y+=grid)Fill(new Rect(0,y,rect.width,1),line);
            }
            string hovered=null, clickedNode=null;string tooltip=null;
            foreach(var link in links){
                var edge=link.First;Rect a=positions[edge.From],b=positions[edge.To];bool forward=b.center.x>=a.center.x;
                Vector2 p0=Point(new Vector2(forward?a.xMax:a.xMin,a.center.y)),p3=Point(new Vector2(forward?b.xMin:b.xMax,b.center.y));
                float reach=Mathf.Clamp(Mathf.Abs(p3.x-p0.x)*.45f,45,160);Vector2 p1=p0+Vector2.right*reach*(forward?1:-1),p2=p3-Vector2.right*reach*(forward?1:-1);
                if(a.x==b.x){p1=p0+Vector2.right*75;p2=p3+Vector2.right*75;}
                if(Mathf.Max(p0.x,p1.x,p2.x,p3.x)<0 || Mathf.Min(p0.x,p1.x,p2.x,p3.x)>rect.width
                    || Mathf.Max(p0.y,p1.y,p2.y,p3.y)<0 || Mathf.Min(p0.y,p1.y,p2.y,p3.y)>rect.height) continue;
                bool highlight=selected!=null&&(edge.From==selected||edge.To==selected);Color color=EdgeColor(edge.Kind);color.a=selected==null||!positions.ContainsKey(selected)?.78f:highlight?1f:.30f;
                float nearest=float.MaxValue;Vector2 prev=p0;
                bool dashed=edge.Conditional||edge.Kind==StoryLinkKind.Exclusion||edge.Kind==StoryLinkKind.Unresolved;
                for(int i=1;i<=28;i++){
                    float t=i/28f,u=1-t;Vector2 p=u*u*u*p0+3*u*u*t*p1+3*u*t*t*p2+t*t*t*p3;
                    if(e.type==EventType.Repaint&&(!dashed||i%3!=0))Segment(prev,p,color,highlight?2.4f:1.25f);
                    nearest=Mathf.Min(nearest,Distance(mouse,prev,p));prev=p;
                }
                if(e.type==EventType.Repaint){Vector2 dir=(p3-p2).normalized;Segment(p3,p3-dir*8+new Vector2(-dir.y,dir.x)*4,color,1.8f);Segment(p3,p3-dir*8-new Vector2(-dir.y,dir.x)*4,color,1.8f);}
                if(nearest<6&&clip.Contains(mouse)&&!MinimapRect(clip).Contains(mouse)){tooltip=link.Proof;hovered=edge.To;}
                if(labels&&zoom>.45f&&highlight){Vector2 mid=.125f*p0+.375f*p1+.375f*p2+.125f*p3;string label=LinkLabel(edge.Kind)+ (link.Count>1?" ×"+link.Count:"");Rect r=new Rect(mid.x-70,mid.y-9,140,19);Fill(r,panel);GUI.Label(r,label,tiny);}
            }
            zoomId.fontSize = Mathf.Max(8, Mathf.RoundToInt(11 * zoom));
            zoomTitle.fontSize = Mathf.Max(9, Mathf.RoundToInt(14 * zoom));
            foreach(var node in visible){
                Rect r=ScreenRect(positions[node.Id]);if(!r.Overlaps(clip))continue;
                Color color=LaneColor(node.Group);bool chosen=node.Id==selected;bool related=selected==null||neighbors.Contains(node.Id);
                Color surface=chosen?SelectedPaper:Paper;if(!related)surface=Color.Lerp(bg,Paper,.40f);
                Fill(new Rect(r.x+3,r.y+4,r.width,r.height),new Color(.30f,.19f,.09f,.13f));
                Fill(new Rect(r.x-1.5f,r.y-1.5f,r.width+3,r.height+3),chosen?Brass:node.Blocked?Seal:Rule);Fill(r,surface);Fill(new Rect(r.x,r.y,Mathf.Max(3,4*zoom),r.height),color);
                if(zoom>.28f){
                    float pad=10*zoom;var idStyle=zoomId;
                    GUI.Label(new Rect(r.x+pad,r.y+8*zoom,r.width-pad*2,20*zoom),NarrativeRelationshipGraph.DateLabel(node.Day)+"   "+(node.Blocked?"阻塞":node.Kind==StoryNodeKind.Ending?"结局":"配置"),idStyle);
                    var nameStyle=zoomTitle;
                    string name=node.Title==node.Key?ShortKey(node.Key):node.Title;
                    GUI.Label(new Rect(r.x+pad,r.y+31*zoom,r.width-pad*2,35*zoom),name,nameStyle);
                    GUI.Label(new Rect(r.x+pad,r.y+70*zoom,r.width-pad*2,18*zoom),node.Key,idStyle);
                    string status=NodeStatus(node);
                    if(!string.IsNullOrEmpty(status)) GUI.Label(new Rect(r.x+pad,r.y+90*zoom,r.width-pad*2,18*zoom),new GUIContent(SingleLine(status),status),idStyle);
                }
                if(!MinimapRect(clip).Contains(mouse)&&GUI.Button(r,GUIContent.none,GUIStyle.none)){clickedNode=node.Id;GUIUtility.keyboardControl=0;}
            }
            // Selection can rebuild collections in neighbor-only mode; do it after enumeration.
            if(clickedNode!=null) Select(clickedNode);
            // Lane/date titles float inside the clipped graph instead of covering the side panels.
            foreach(string lane in lanes){var first=visible.FirstOrDefault(n=>n.Lane==lane);if(first==null)continue;float y=Point(positions[first.Id].position).y-25*zoom;if(y>-25&&y<rect.height)GUI.Label(new Rect(8,y,210,22),lane,heading);}
            foreach(var d in dayColumns){float x=Point(new Vector2(d.Value,0)).x;if(x>-200&&x<rect.width)GUI.Label(new Rect(x,5,260,23),NarrativeRelationshipGraph.DateLabel(d.Key),muted);}
            GUI.Label(new Rect(12,rect.height-49,560,18),"梅紫 前置 · 橄榄 续接 · 朱红 排斥 · 黄铜 结局 · 灰褐 表外",tiny);
            GUI.Label(new Rect(12,rect.height-29,300,22),"缩放 "+Mathf.RoundToInt(zoom*100)+"%    "+visible.Count+" 节点 / "+links.Count+" 组连线",muted);
            Minimap(clip);
            if(tooltip!=null&&e.type==EventType.MouseDown&&e.button==0){info=tooltip;tab=2;Select(hovered);info=tooltip;e.Use();}
            if(tooltip!=null&&e.type==EventType.Repaint){Rect tip=new Rect(Mathf.Clamp(mouse.x+16,6,Mathf.Max(6,rect.width-360)),Mathf.Clamp(mouse.y+16,35,Mathf.Max(35,rect.height-165)),Mathf.Min(350,rect.width-12),130);Fill(tip,Paper);Frame(tip,Brass);GUI.Label(tip,tooltip.Length>380?tooltip.Substring(0,380)+"…":tooltip,body);}
            GUI.EndGroup();
        }
        private Rect MinimapRect(Rect clip) => clip.width < 350 || clip.height < 280 || visible.Count == 0 ? new Rect() : new Rect(clip.width - 177, clip.height - 134, 165, 117);
        private void Minimap(Rect clip)
        {
            if(clip.width<350 || clip.height<280 || visible.Count==0)return;
            Rect area=MinimapRect(clip);Fill(area,Paper);Frame(area,Rule);
            GUI.Label(new Rect(area.x+8,area.y+4,150,18),"总览 · 点击跳转",tiny);
            Rect inner=new Rect(area.x+8,area.y+26,149,81);float scale=Mathf.Min(inner.width/worldBounds.width,inner.height/worldBounds.height);
            foreach(var n in visible){Rect r=positions[n.Id];Fill(new Rect(inner.x+(r.x-worldBounds.x)*scale,inner.y+(r.y-worldBounds.y)*scale,Mathf.Max(2,r.width*scale),Mathf.Max(2,r.height*scale)),LaneColor(n.Group));}
            Vector2 start=(-pan/zoom-worldBounds.position)*scale+inner.position;Vector2 size=clip.size/zoom*scale;
            Rect view=new Rect(start,size);Color frame=Ink;
            Fill(new Rect(view.x,view.y,view.width,1),frame);Fill(new Rect(view.x,view.yMax,view.width,1),frame);Fill(new Rect(view.x,view.y,1,view.height),frame);Fill(new Rect(view.xMax,view.y,1,view.height),frame);
            if(Event.current.type==EventType.MouseDown&&Event.current.button==0&&inner.Contains(Event.current.mousePosition)){
                Vector2 world=(Event.current.mousePosition-inner.position)/scale+worldBounds.position;pan=clip.size*.5f-world*zoom;Event.current.Use();
            }
        }
        private static string LinkLabel(StoryLinkKind kind)
        {
            switch(kind){case StoryLinkKind.Continuation:return "排队续接";case StoryLinkKind.Prerequisite:return "前置事实";case StoryLinkKind.Exclusion:return "排斥条件";case StoryLinkKind.CheckSuccess:return "检定成功";case StoryLinkKind.CheckFailure:return "检定失败";case StoryLinkKind.Ending:return "结局条件";default:return "表外来源";}
        }
        private static string ShortKey(string key)=>key.Replace("neutral.","中立 · ").Replace("rose.","新玫瑰 · ").Replace("church.","教会 · ").Replace("revolution.","革命 · ").Replace(".route."," · 分线 · ");
        private void Segment(Vector2 a,Vector2 b,Color c,float width)
        {
            Vector2 delta = b - a;
            if (delta.sqrMagnitude < .01f) return;
            // Do not rotate GUI.matrix inside nested/scaled groups: Unity's transformed clip
            // can move diagonal strokes into the sidebar in the embedded GM view.
            // Short axis-aligned spans retain the host matrix and its exact rectangular clip.
            int spans = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) / 2.5f));
            Vector2 previous = a;
            for (int i = 1; i <= spans; i++)
            {
                Vector2 point = a + delta * (i / (float)spans);
                var stroke = new Rect(Mathf.Min(previous.x, point.x) - width * .5f,
                    Mathf.Min(previous.y, point.y) - width * .5f,
                    Mathf.Abs(point.x - previous.x) + width, Mathf.Abs(point.y - previous.y) + width);
                Fill(stroke, c);
                previous = point;
            }
        }
        private static float Distance(Vector2 p,Vector2 a,Vector2 b){Vector2 d=b-a;float t=d.sqrMagnitude<.001f?0:Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);return Vector2.Distance(p,a+d*t);}
        private bool CanScheduleRequest
        {
            get
            {
#if UNITY_EDITOR
                return true;
#else
                return mainContext != null;
#endif
            }
        }
        private void RequestRefresh()
        {
            var callback = RefreshRequested;
            if (callback == null) { Reload(); return; }
            // In GM, only the host may choose/update its Runtime.Configuration snapshot.
            ScheduleRequest(() => { if (RefreshRequested == callback) callback(); });
        }
        private void QueuePreview(StoryGraphNode node, string pageId)
        {
            if (disposed || pendingRequest != null || PreviewRequested == null) return;
            if (!string.IsNullOrEmpty(Annotation(PreviewBlockReason, node))) return;
            var callback = PreviewRequested;
            var snapshot = graph;
            ScheduleRequest(() =>
            {
                if (graph != snapshot || PreviewRequested != callback) return;
                // Host state may have changed between click and dispatch; fail closed on provider errors.
                string reason = Annotation(PreviewBlockReason, node);
                if (!string.IsNullOrEmpty(reason)) { info = "预览已取消：" + reason; return; }
                callback(node, pageId);
            });
        }
        private void ScheduleRequest(Action work)
        {
            if (disposed || pendingRequest != null || !CanScheduleRequest) return;
            pendingRequest = work;
            int ticket = ++requestTicket;
            // Never call the host inside OnGUI: it may close/dispose this view or rebuild its container.
#if UNITY_EDITOR
            editorDispatch = () => DispatchRequest(ticket);
            UnityEditor.EditorApplication.delayCall += editorDispatch;
#else
            mainContext.Post(_ => DispatchRequest(ticket), null);
#endif
        }
        private void DispatchRequest(int ticket)
        {
            // A stale posted callback must not drain a newer request after Load/Dispose/requeue.
            if (disposed || ticket != requestTicket) return;
#if UNITY_EDITOR
            if (editorDispatch != null) UnityEditor.EditorApplication.delayCall -= editorDispatch;
            editorDispatch = null;
#endif
            var work = pendingRequest;
            pendingRequest = null;
            try { work?.Invoke(); }
            catch (Exception ex) { info = "宿主请求执行失败：" + ex.Message; Debug.LogException(ex); }
        }
        public void CancelQueuedActions() => CancelPendingRequest();

        private void CancelPendingRequest()
        {
            requestTicket++;
#if UNITY_EDITOR
            if (editorDispatch != null) UnityEditor.EditorApplication.delayCall -= editorDispatch;
            editorDispatch = null;
#endif
            pendingRequest = null;
        }

        private void RelationshipDetails(StoryGraphNode node, float width, ref float y)
        {
            var connections = graph.Connections(node.Id);
            Section(ref y, width, "关系证据 / " + connections.Count + " 条");
            Paragraph(ref y, width, "点击关系定位另一端；完整证据保留条件组与分支方向。", wrapped);
            foreach (var edge in connections)
            {
                string other = edge.From == node.Id ? edge.To : edge.From;
                string key = graph.Find(other)?.Key ?? other;
                string label = (edge.From == node.Id ? "→ " : "← ") + LinkLabel(edge.Kind) + " · " + key;
                if (DetailButton(ref y, width, label))
                {
                    Select(other);
                    if (!positions.ContainsKey(other)) { scope = 1; showFacts = true; query = ""; onlyNeighbors = true; faction = 0; Rebuild(); }
                    Focus(other);
                }
            }
            string Direction(StoryGraphEdge edge) => edge.From == node.Id ? "下游 → " + (graph.Find(edge.To)?.Key ?? edge.To) : "上游 ← " + (graph.Find(edge.From)?.Key ?? edge.From);
            string evidence = (string.IsNullOrEmpty(info) ? "" : info + "\n\n────────────\n\n") + string.Join("\n\n────────────────\n\n", connections.Select(e => Direction(e) + "\n类型：" + LinkLabel(e.Kind) + "\n" + e.Label + "\n" + e.Evidence));
            if (connections.Count == 0) evidence = "没有扫描到跨节点连线。独立开场、代码入口或没有表内因果引用的节点可能没有连线；不自动画一条假前后关系。";
            if (DetailButton(ref y, width, "复制全部关系证据")) GUIUtility.systemCopyBuffer = evidence;
            Paragraph(ref y, width, evidence, body);
        }
        private void Section(ref float y, float width, string label)
        {
            y += 10; Fill(new Rect(0, y, width, 1), Rule); y += 11;
            GUI.Label(new Rect(3, y, width - 6, 24), label, heading); y += 31;
        }
        private void Paragraph(ref float y, float width, string value, GUIStyle style)
        {
            if (string.IsNullOrEmpty(value)) return;
            // All host text is plain text and measured with wrapping; long status/reasons stay scrollable.
            var content = new GUIContent(value);
            bool previous = style.wordWrap;
            style.wordWrap = true;
            float height = Mathf.Max(style.lineHeight + style.padding.vertical, style.CalcHeight(content, width));
            GUI.Label(new Rect(0, y, width, height), content, style);
            style.wordWrap = previous;
            y += height + 8;
        }
        private bool DetailButton(ref float y, float width, string label)
        {
            bool clicked = GUI.Button(new Rect(0, y, width, 30), new GUIContent(label, label), button);
            y += 38; return clicked;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            CancelPendingRequest();
            StatusLabel = null; StatusDetails = null; PreviewBlockReason = null; PreviewRequested = null; RefreshRequested = null; ContextLabel = null;
            // Only the fallback dynamic font is ours; resource fonts are shared with Map/Details.
            Destroy(skin); skin = null;
            foreach (var texture in textures) Destroy(texture);
            textures.Clear(); white = null;
            if (ownsFont) Destroy(font);
            font = null; headingFont = null; ownsFont = false;
            graph = null; selected = null; mainContext = null; ResetNavigation();
            visible.Clear(); positions.Clear(); links.Clear(); lanes.Clear(); dayColumns.Clear(); neighbors.Clear(); statusLabels.Clear();
        }
        private static void Destroy(UnityEngine.Object value){if(value==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
    }
}
