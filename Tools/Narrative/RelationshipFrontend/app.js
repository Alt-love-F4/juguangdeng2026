(() => {
  "use strict";
  const TABLE_BASE = "../../../Assets/Settings/Resources/Luban/";
  const TABLES = {
    events: "tbncsevent", eventDates: "tbncseventdate", conditions: "tbncscondition",
    conditionKeys: "tbncsconditionkey", endings: "tbncsending", results: "tbncsresult",
    actions: "tbncsaction", choices: "tbncschoice", setups: "tbncssetupparams",
    abilityChecks: "tbncsabilitycheck", pages: "tbncsconversationpage", lines: "tbncsconversationline",
    notices: "tbncsnotice", readingPages: "tbncsreadingpage", readingLines: "tbncsreadingline",
    objects: "tbncsobject", presentationIndex: "tbncspresentationindex",
    openingPresentations: "tbopeningpresentation", openingLines: "tbopeningline",
    tutorialSteps: "tbcafetutorialstep", texts: "tbtext", triggers: "tbncstrigger", bars: "tbncsbar"
  };
  const GROUP_ORDER = ["neutral", "rose", "church", "revolution", "daily", "newspaper", "news", "opening", "tutorial", "ambient", "ending", "fact", "sample"];
  const GROUP_LABELS = { neutral: "中立 / 经营", rose: "新玫瑰", church: "王庭教会", revolution: "革命联合", daily: "地图日常", opening: "开场", tutorial: "教程", ambient: "咖啡馆片段", newspaper: "报纸", news: "公开消息", ending: "结局", fact: "表外条件", sample: "演示 / 测试" };
  const EDGE_LABELS = { Prerequisite: "前置事实", Exclusion: "排斥条件", Continuation: "续接", CheckSuccess: "检定成功", CheckFailure: "检定失败", Ending: "结局条件", Unresolved: "未找到表内生产者" };
  const EDGE_CLASSES = { Prerequisite: "prereq", Exclusion: "exclusion", Continuation: "continuation", CheckSuccess: "check", CheckFailure: "check", Ending: "continuation", Unresolved: "unresolved" };
  const GROUP_COLORS = { neutral: "#9a753b", rose: "#a65e62", church: "#75658b", revolution: "#5f805e", daily: "#467e83", newspaper: "#7b7b69", news: "#577a9a", opening: "#55765d", tutorial: "#3f7b73", ambient: "#7b8263", ending: "#ab762f", fact: "#8c7958", sample: "#8a8f8b" };
  const state = { graph: null, scope: "main", faction: "", groupFocus: "", query: "", selected: "", detailTab: "overview", onlyNeighbors: false, viewBox: null, fitViewBox: null, geometry: null, dragging: null, zoomScale: 1, lastModified: "", fitPending: false, fitRequested: false, error: "" };
  const el = id => document.getElementById(id);
  const escapeHtml = value => String(value ?? "").replace(/[&<>"']/g, char => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#39;" }[char]));
  const stripPrefix = (value, prefix) => String(value ?? "").toLowerCase().startsWith(prefix.toLowerCase()) ? String(value).slice(prefix.length) : String(value ?? "");
  const asBool = value => String(value).toLowerCase() === "true";
  const conditionText = condition => `${condition.condType}(${condition.target}, ${condition.value})`;
  const textOr = (key, textMap) => key ? (textMap.get(key) || key) : "";

  async function readTables() {
    const responses = await Promise.all(Object.entries(TABLES).map(async ([key, file]) => {
      const response = await fetch(`${TABLE_BASE}${file}.json?ts=${Date.now()}`, { cache: "no-store" });
      if (!response.ok) throw new Error(`读取 ${file}.json 失败：HTTP ${response.status}`);
      return [key, await response.json(), response.headers.get("last-modified") || ""];
    }));
    const tables = Object.fromEntries(responses.map(([key, value]) => [key, value]));
    const newest = responses.map(([, , date]) => Date.parse(date)).filter(Number.isFinite).sort((a, b) => b - a)[0];
    return { tables, modified: newest ? new Date(newest).toLocaleString("zh-CN", { hour12: false }) : "" };
  }

  const eventGroup = id => {
    if (id.startsWith("neutral.") || id === "oct07.s1") return "neutral";
    if (id.startsWith("rose.")) return "rose";
    if (id.startsWith("church.")) return "church";
    if (id.startsWith("revolution.") || id === "oct07.s2") return "revolution";
    if (id.startsWith("daily.")) return "daily";
    if (id.startsWith("newspaper.")) return "newspaper";
    if (id.startsWith("news.")) return "news";
    if (id.startsWith("arrival.")) return "opening";
    return "sample";
  };
  const isStory = id => ["oct07.", "neutral.", "rose.", "church.", "revolution."].some(prefix => String(id || "").startsWith(prefix));
  const dayInName = key => {
    const match = String(key || "").match(/(?:^|\.)(10|11)(\d{2})(?:\.|$)/);
    if (!match) return 0;
    const date = Date.UTC(2000, Number(match[1]) - 1, Number(match[2]));
    return Math.floor((date - Date.UTC(2000, 9, 6)) / 86400000) + 1;
  };
  const dateLabel = day => {
    if (!day) return "续接 / 未指定";
    const date = new Date(Date.UTC(2000, 9, 6 + day));
    return `${String(date.getUTCMonth() + 1).padStart(2, "0")}/${String(date.getUTCDate()).padStart(2, "0")} · D${day}`;
  };
  const parseProperties = value => {
    const properties = new Map();
    for (const part of String(value || "").split(/[|;\n]/)) {
      const split = part.indexOf("=");
      if (split > 0) properties.set(part.slice(0, split).trim(), part.slice(split + 1).trim());
    }
    return properties;
  };
  const groupLabel = group => GROUP_LABELS[group] || "演示 / 测试";

  function buildGraph(tables) {
    const textMap = new Map(tables.texts.map(row => [row.key, row.text || ""]));
    const indexBy = (rows, key) => {
      const map = new Map();
      for (const row of rows) { const value = key(row); if (!map.has(value)) map.set(value, []); map.get(value).push(row); }
      return map;
    };
    const maps = {
      events: new Map(tables.events.map(row => [row.eventId, row])),
      endings: new Map(tables.endings.map(row => [row.endingId, row])),
      openings: new Map(tables.openingPresentations.map(row => [row.presentationId, row])),
      dates: indexBy(tables.eventDates, row => row.eventId), pages: indexBy(tables.pages, row => row.presentationKey),
      lines: indexBy(tables.lines, row => row.pageId), choices: indexBy(tables.choices, row => row.groupKey),
      conditions: indexBy(tables.conditions, row => `${row.ownerType || "event"}:${row.ownerId}`),
      results: indexBy(tables.results, row => row.eventId), actions: new Map(tables.actions.map(row => [row.actionKey, row])),
      setups: new Map(tables.setups.map(row => [row.setupKey, row])), checks: new Map(tables.abilityChecks.map(row => [row.checkId, row])),
      conditionKeys: new Map(tables.conditionKeys.map(row => [row.key, row])), objects: new Map(tables.objects.map(row => [row.objId, row])),
      notices: indexBy(tables.notices, row => row.presentationKey), readingPages: indexBy(tables.readingPages, row => row.presentationKey),
      readingLines: indexBy(tables.readingLines, row => row.pageId), openingLines: indexBy(tables.openingLines, row => row.presentationId),
      tutorialSteps: indexBy(tables.tutorialSteps, row => row.tutorialId), triggers: indexBy(tables.triggers, row => row.eventId),
      bars: new Map(tables.bars.map(row => [row.barKey, row]))
    };
    const rows = (map, key) => map.get(key) || [];
    const getText = key => textOr(key, textMap);
    const nodes = new Map(), edges = [], edgeKeys = new Set(), owners = new Map(), producers = new Map();
    const ownerKey = (type, key) => `${type}:${key}`;
    const addNode = node => { nodes.set(node.id, node); return node; };
    const addOwner = (key, node, context) => {
      if (!key) return;
      if (!owners.has(key)) owners.set(key, []);
      const list = owners.get(key);
      if (!list.some(item => item.node === node && item.context === context)) list.push({ node, context });
    };

    for (const event of tables.events) {
      const id = `event:${event.eventId}`, group = eventGroup(event.eventId);
      const dates = rows(maps.dates, event.eventId).slice().sort((a, b) => a.day - b.day);
      let day = Number(dates[0]?.day || 0), dateSource = day ? "EventDate 源表" : "";
      if (!day) {
        const calendar = rows(maps.conditions, `event:${event.eventId}`).find(c => stripPrefix(c.target, "bar:") === "world.calendar" && ["BAR_BETWEEN", "BAR_GTE"].includes(c.condType));
        const value = calendar?.value?.match(/\d+/)?.[0];
        if (value) { day = Number(value); dateSource = "日历条件（非日期入口）"; }
      }
      if (!day) { day = dayInName(event.eventId); if (day) dateSource = "仅按 ID 排布"; }
      if (!day && group === "opening") { day = 1; dateSource = "开场分组"; }
      const properties = parseProperties(event.eventProperty);
      let lane = groupLabel(group);
      if (group === "daily" && properties.has("dailyPool")) lane = stripPrefix(properties.get("dailyPool"), "daily.action.");
      if (lane === "market") lane = "大堂区"; if (lane === "library") lane = "灰墙学院";
      const title = getText(event.titleText);
      const node = addNode({ id, key: event.eventId, kind: "Event", group, lane, day, dateSource,
        title: !title || title.endsWith(".title") ? event.eventId : title, entry: properties.get("entry") || "Immediate",
        onceOnly: asBool(event.onceOnly), blocksSave: asBool(properties.get("blocksSave")), blocker: properties.get("blockedBy") || "",
        disabled: false, sample: group === "sample", raw: event, content: [], searchText: "", dates });
      addOwner(event.eventId, id, "根事件结果");
      for (const page of rows(maps.pages, event.presentationKey)) {
        for (const choice of rows(maps.choices, page.optionGroupKey)) addOwner(choice.resultOwnerId || choice.choiceId, id, `选项 ${getText(choice.textKey)}`);
      }
      const setup = maps.setups.get(event.setupKey);
      const slots = new Map(String(setup?.slotValues || "").split(",").map(part => {
        const split = part.indexOf("="); return split > 0 ? [part.slice(0, split).trim().toLowerCase(), part.slice(split + 1).trim()] : ["", ""];
      }).filter(([key]) => key));
      if (slots.has("opts")) for (const choice of rows(maps.choices, slots.get("opts"))) addOwner(choice.resultOwnerId || choice.choiceId, id, `选项 ${getText(choice.textKey)}`);
      const contentKeys = new Set(String(event.contentKey || "").split(/[|,;]/).map(key => key.trim()).filter(Boolean));
      const pageIds = [];
      for (const page of rows(maps.pages, event.presentationKey).slice().sort((a, b) => a.seq - b.seq)) {
        pageIds.push(page.pageId);
        for (const line of rows(maps.lines, page.pageId)) contentKeys.add(line.textKey);
        for (const choice of rows(maps.choices, page.optionGroupKey)) contentKeys.add(choice.textKey);
        for (const reading of rows(maps.readingPages, page.readingKey)) { pageIds.push(reading.pageId); for (const line of rows(maps.readingLines, reading.pageId)) contentKeys.add(line.textKey); }
      }
      for (const reading of rows(maps.readingPages, event.presentationKey)) { pageIds.push(reading.pageId); for (const line of rows(maps.readingLines, reading.pageId)) contentKeys.add(line.textKey); }
      for (const notice of rows(maps.notices, event.presentationKey)) { contentKeys.add(notice.titleKey); contentKeys.add(notice.textKey); }
      const firstLine = rows(maps.pages, event.presentationKey).slice().sort((a, b) => a.seq - b.seq).flatMap(page => rows(maps.lines, page.pageId).slice().sort((a, b) => a.seq - b.seq))[0];
      node.snippet = firstLine ? getText(firstLine.textKey) : "";
      node.searchText = `${node.key}\n${node.title}\n${node.snippet}\n${Array.from(contentKeys).filter(Boolean).map(getText).join("\n")}\n${pageIds.join("\n")}`;
      node.content = buildEventContent(event, tables, maps, getText);
      node.triggers = rows(maps.triggers, event.eventId);
    }
    for (const opening of tables.openingPresentations) {
      const group = opening.presentationId.startsWith("cafe.ambient.") ? "ambient" : opening.presentationId.startsWith("cafe_tutorial.") ? "tutorial" : "opening";
      const body = rows(maps.openingLines, opening.presentationId).slice().sort((a, b) => a.seq - b.seq);
      const content = body.map(line => getText(line.textKey));
      addNode({ id: `opening:${opening.presentationId}`, key: opening.presentationId, kind: "Opening", group, lane: groupLabel(group),
        title: getText(opening.titleKey) || opening.presentationId, day: group === "ambient" ? 0 : 1, dateSource: "分组排布",
        entry: "代码 / 教程入口", snippet: content[0] || "", content: [{ id: opening.presentationId, caption: "开场演出快照",
          bodyText: body.map(line => `${line.speaker || "旁白"}：${getText(line.textKey)}`).join("\n\n"), diagnostics: "" }],
        searchText: `${opening.presentationId}\n${getText(opening.titleKey)}\n${content.join("\n")}`, disabled: false, blocker: "", raw: opening });
    }
    for (const ending of tables.endings) {
      const enabled = asBool(ending.enabled);
      addNode({ id: `ending:${ending.endingId}`, key: ending.endingId, kind: "Ending", group: "ending", lane: "结局",
        title: getText(ending.titleText) || ending.endingId, snippet: getText(ending.messageText), day: 0, entry: "安全边界检查",
        dateSource: "", disabled: !enabled, sample: !enabled, blocker: "", raw: ending,
        content: [{ id: ending.endingId, caption: "结局说明", bodyText: getText(ending.messageText), diagnostics: `enabled=${ending.enabled}; priority=${ending.priority}` }],
        searchText: `${ending.endingId}\n${getText(ending.titleText)}\n${getText(ending.messageText)}` });
    }

    for (const event of tables.events) {
      const nodeId = `event:${event.eventId}`, checkIds = new Set();
      const setup = maps.setups.get(event.setupKey);
      if (setup?.checkId) checkIds.add(setup.checkId);
      for (const page of rows(maps.pages, event.presentationKey)) {
        if (page.checkId) checkIds.add(page.checkId);
        for (const choice of rows(maps.choices, page.optionGroupKey)) if (choice.checkId) checkIds.add(choice.checkId);
      }
      for (const checkId of checkIds) {
        const check = maps.checks.get(checkId);
        if (!check) continue;
        if (check.successResultOwnerId && check.successResultOwnerId !== check.successEventId) addOwner(check.successResultOwnerId, nodeId, `检定成功 ${check.checkId}`);
        if (check.failureResultOwnerId && check.failureResultOwnerId !== check.failureEventId) addOwner(check.failureResultOwnerId, nodeId, `检定失败 ${check.checkId}`);
      }
    }
    const conditionsFor = (type, owner) => rows(maps.conditions, ownerKey(type, owner));
    const guards = resultId => {
      const conditions = conditionsFor("result", resultId);
      if (!conditions.length) return "";
      const groups = Array.from(new Set(conditions.map(condition => condition.groupIndex))).sort((a, b) => a - b);
      return groups.map(group => `(${conditions.filter(condition => condition.groupIndex === group).map(conditionText).join(" AND ")})`).join(" OR ");
    };
    const addEdge = (from, to, kind, label, evidence, group = 0, conditional = false) => {
      if (!nodes.has(from) || !nodes.has(to) || from === to) return;
      const key = [from, to, kind, label, group, evidence].join("\n");
      if (edgeKeys.has(key)) return;
      edgeKeys.add(key);
      edges.push({ from, to, kind, label, evidence, conditionGroup: group, conditional });
    };
    const factEdges = (fact, to, positive, label, evidence, group, kind) => {
      const candidates = producers.get(fact) || [];
      if (candidates.length) {
        for (const producer of candidates) addEdge(producer.node, to, positive ? kind : "Exclusion",
          `${label}${producer.context === "根事件结果" ? "" : " · 分支"}`,
          `${evidence}\n生产条件: ${producer.context}`, group, producer.context !== "根事件结果");
        return;
      }
      const conditionKey = maps.conditionKeys.get(fact), id = `fact:${fact}`;
      if (!nodes.has(id)) addNode({ id, key: fact, kind: "Fact", group: "fact", lane: "表外条件", title: fact,
        entry: conditionKey?.initialValue ? "初始配置为真" : "未找到表内置真来源", snippet: "可能由代码宿主提供；不推测运行状态。",
        blocker: "", disabled: false, day: 0, dateSource: "", content: [], searchText: fact, raw: conditionKey || {} });
      addEdge(id, to, positive ? "Unresolved" : "Exclusion", label,
        `${evidence}\n未找到表内生产者（不等于确认缺实现）。`, group, false);
    };
    const addConditions = (type, owner, to, defaultKind = "Prerequisite") => {
      for (const condition of conditionsFor(type, owner)) {
        const target = stripPrefix(stripPrefix(condition.target, "event:"), "cond:");
        const proof = `Condition ${condition.conditionId}\nowner=${type}:${owner}\n组 ${condition.groupIndex}（组内 AND，组间 OR）\n${conditionText(condition)}`;
        if (["EVENT_HAPPENED", "EVENT_NOT_HAPPENED"].includes(condition.condType)) {
          addEdge(`event:${target}`, to, condition.condType === "EVENT_NOT_HAPPENED" ? "Exclusion" : defaultKind,
            `${condition.condType} ${target}`, proof, condition.groupIndex);
        } else if (condition.condType === "BOOL_EQ" && ["true", "false"].includes(String(condition.value).toLowerCase()) &&
          !["choice:", "bar:", "not:", "check:", "gameplay:", "anyChoice:"].some(prefix => target.startsWith(prefix))) {
          const expected = asBool(condition.value);
          if (target === `story.${owner}.completed` && !expected) continue;
          factEdges(target, to, expected, `${target}=${expected}`, proof, condition.groupIndex, defaultKind);
        }
      }
    };

    for (const result of tables.results) {
      for (const owner of owners.get(result.eventId) || []) {
        const guard = guards(result.resultId);
        const context = owner.context + (guard ? `；${guard}` : "");
        if (["SET_FLAG", "SET_SWITCH"].includes(result.resultType) && asBool(result.value)) {
          const fact = stripPrefix(result.target, "cond:");
          if (!producers.has(fact)) producers.set(fact, []);
          const list = producers.get(fact);
          if (!list.some(item => item.node === owner.node && item.context === context)) list.push({ node: owner.node, context });
        }
        if (result.resultType === "QUEUE_EVENT") addEdge(owner.node, `event:${stripPrefix(result.target, "event:")}`, "Continuation",
          `续接${context === "根事件结果" ? "" : " · 分支"}`, `Result ${result.resultId}\n${context}`, 0, context !== "根事件结果");
      }
    }
    for (const event of tables.events) {
      const target = `event:${event.eventId}`;
      addConditions("event", event.conditionOwnerId || event.eventId, target);
      for (const action of tables.actions) {
        const resultEvents = String(action.resultEvents || "").split(/[|,;]/).map(value => value.trim());
        if (resultEvents.includes(event.eventId)) addConditions("action", action.actionKey, target);
      }
      const checkIds = new Set(), setup = maps.setups.get(event.setupKey);
      if (setup?.checkId) checkIds.add(setup.checkId);
      for (const page of rows(maps.pages, event.presentationKey)) {
        if (page.checkId) checkIds.add(page.checkId);
        for (const choice of rows(maps.choices, page.optionGroupKey)) if (choice.checkId) checkIds.add(choice.checkId);
      }
      for (const checkId of checkIds) {
        const check = maps.checks.get(checkId);
        if (!check) continue;
        if (check.successEventId) addEdge(target, `event:${check.successEventId}`, "CheckSuccess", "检定成功", `AbilityCheck ${check.checkId}`, 0, true);
        if (check.failureEventId) addEdge(target, `event:${check.failureEventId}`, "CheckFailure", "检定失败", `AbilityCheck ${check.checkId}`, 0, true);
      }
    }
    for (const ending of tables.endings) addConditions("ending", ending.endingId, `ending:${ending.endingId}`, "Ending");
    for (const step of tables.tutorialSteps) {
      if (!step.nextStepId) continue;
      const next = rows(maps.tutorialSteps, step.tutorialId).find(item => item.stepId === step.nextStepId);
      if (next) addEdge(`opening:${step.presentationId}`, `opening:${next.presentationId}`, "Continuation", "教程步骤",
        `CafeTutorialStep ${step.stepId} -> ${next.stepId}`, 0, true);
    }
    for (let pass = 0; pass < 8; pass++) for (const edge of edges) {
      if (!["Continuation", "Ending"].includes(edge.kind)) continue;
      const from = nodes.get(edge.from), to = nodes.get(edge.to);
      if (from?.day > 0 && to && !to.day) { to.day = from.day; to.dateSource = "随上游排布（非日期入口）"; }
    }
    const nodeList = Array.from(nodes.values()).sort((a, b) => (a.day || Number.MAX_SAFE_INTEGER) - (b.day || Number.MAX_SAFE_INTEGER) || a.group.localeCompare(b.group) || a.key.localeCompare(b.key));
    const adjacency = new Map(nodeList.map(node => [node.id, []]));
    for (const edge of edges) { adjacency.get(edge.from)?.push(edge); adjacency.get(edge.to)?.push(edge); }
    return { nodes: nodeList, nodeMap: nodes, edges, adjacency, tables, maps, textMap, owners, producers,
      storyCount: nodeList.filter(node => node.kind === "Event" && isStory(node.key)).length,
      blockedCount: nodeList.filter(node => node.blocker && node.kind === "Event" && isStory(node.key)).length };
  }

  function buildEventContent(event, tables, maps, getText) {
    const blocks = [], rows = (map, key) => map.get(key) || [];
    for (const page of rows(maps.pages, event.presentationKey).slice().sort((a, b) => a.seq - b.seq)) {
      const lines = rows(maps.lines, page.pageId).slice().sort((a, b) => a.seq - b.seq).map(line => {
        const object = maps.objects.get(line.speakerKey);
        const speaker = line.speakerNameKey ? getText(line.speakerNameKey) : object ? getText(object.nameText) : "旁白";
        return `${speaker}${line.emote ? ` [${line.emote}]` : ""}：${getText(line.textKey)}`;
      });
      const choices = rows(maps.choices, page.optionGroupKey).slice().sort((a, b) => a.seq - b.seq).map(choice => {
        const conditions = rows(maps.conditions, `choice:${choice.choiceId}`);
        return [`选项 ${choice.seq}：${getText(choice.textKey)}  [${choice.choiceId}]`,
          `检定: ${choice.checkId || "无"} · 结果所有者: ${choice.resultOwnerId || choice.choiceId}`,
          conditions.length ? `可选条件（组内 AND）: ${conditions.map(conditionText).join(" / ")}` : ""].filter(Boolean).join("\n  ");
      });
      const readings = rows(maps.readingPages, page.readingKey).slice().sort((a, b) => a.seq - b.seq).map(reading => {
        const body = rows(maps.readingLines, reading.pageId).slice().sort((a, b) => a.seq - b.seq).map(line => getText(line.textKey));
        return `阅读：${getText(reading.titleKey)}\n${body.join("\n")}`;
      });
      const bodyText = [...lines, ...choices, ...readings].filter(Boolean).join("\n\n");
      const diagnostics = [`${page.seq}. ${page.pageId} · ${page.stepType || "Dialogue"}`,
        page.nextPageId ? `显式 nextPageId: ${page.nextPageId}` : "未配置显式 nextPageId", page.gameplayKey ? `玩法: ${page.gameplayKey}` : "",
        page.checkId ? `检定: ${page.checkId}` : "", page.pageCondKeys ? `显示条件: ${page.pageCondKeys}` : "",
        page.pageBarConds ? `属性条件: ${page.pageBarConds}` : "", page.bgOverride ? `背景: ${page.bgOverride}` : ""].filter(Boolean).join("\n");
      blocks.push({ id: page.pageId, caption: `${page.seq}. ${page.pageId}`, bodyText, diagnostics });
    }
    for (const reading of rows(maps.readingPages, event.presentationKey).slice().sort((a, b) => a.seq - b.seq)) {
      const body = rows(maps.readingLines, reading.pageId).slice().sort((a, b) => a.seq - b.seq).map(line => getText(line.textKey));
      blocks.push({ id: reading.pageId, caption: `阅读 ${reading.seq} · ${getText(reading.titleKey)}`, bodyText: [getText(reading.titleKey), ...body].join("\n"), diagnostics: `ReadingPage ${reading.pageId}` });
    }
    for (const notice of rows(maps.notices, event.presentationKey)) blocks.push({ id: notice.presentationKey, caption: "通知",
      bodyText: [getText(notice.titleKey), getText(notice.timeKey), getText(notice.textKey)].filter(Boolean).join("\n\n"),
      diagnostics: `imageKey=${notice.imageKey || ""}\nbarConds=${notice.barConds || ""}` });
    if (!blocks.length) {
      const keys = String(event.contentKey || "").split(/[|,;]/).map(key => key.trim()).filter(Boolean);
      blocks.push({ id: event.eventId, caption: "事件文本 / 调度节点", bodyText: keys.map(getText).join("\n\n"), diagnostics: event.setupKey ? `SetupParams: ${event.setupKey}` : "" });
    }
    return blocks;
  }

  function visibleNodes() {
    const graph = state.graph, query = state.query.trim().toLocaleLowerCase();
    return graph.nodes.filter(node => {
      const main = (node.kind === "Event" && isStory(node.key)) || (node.kind === "Ending" && !node.disabled);
      if (state.scope === "main" && !main) return false;
      if (state.scope === "daily" && !["daily", "ambient"].includes(node.group)) return false;
      if (state.scope === "opening" && !["opening", "tutorial", "ambient"].includes(node.group)) return false;
      if (state.scope === "blocked" && !node.blocker) return false;
      if (state.faction && node.group !== state.faction && !(state.faction === "opening" && ["opening", "tutorial", "ambient"].includes(node.group))) return false;
      if (state.groupFocus && node.lane !== state.groupFocus) return false;
      if (query && !`${node.key}\n${node.title}\n${node.searchText}\n${node.snippet}\n${node.blocker}`.toLocaleLowerCase().includes(query)) return false;
      return true;
    });
  }

  function render() {
    if (!state.graph) return;
    const visible = visibleNodes();
    if (state.selected && !visible.some(node => node.id === state.selected) && !state.onlyNeighbors) state.selected = "";
    renderStats(); renderList(visible); renderGraph(visible);
    if (state.selected && !state.fitRequested) centerNode(state.selected);
    state.fitRequested = false;
    renderDetails(state.selected ? state.graph.nodeMap.get(state.selected) : null);
  }

  function renderStats() {
    const graph = state.graph;
    el("stat-nodes").textContent = graph.nodes.length.toLocaleString("zh-CN");
    el("stat-edges").textContent = graph.edges.length.toLocaleString("zh-CN");
    el("stat-stories").textContent = graph.storyCount.toLocaleString("zh-CN");
    el("stat-blocked").textContent = graph.blockedCount.toLocaleString("zh-CN");
    el("snapshot-label").textContent = state.lastModified ? `生成文件 ${state.lastModified}` : "读取当前生成文件";
  }

  function renderList(nodes) {
    el("visible-count").textContent = `${nodes.length.toLocaleString("zh-CN")} 个节点`;
    const counts = new Map();
    for (const node of nodes) counts.set(node.lane, (counts.get(node.lane) || 0) + 1);
    el("group-summary").innerHTML = Array.from(counts.entries()).sort((a, b) => a[0].localeCompare(b[0], "zh-CN")).map(([lane, count]) =>
      `<button class="group-chip${state.groupFocus === lane ? " active" : ""}" type="button" data-lane="${escapeHtml(lane)}"><span>${escapeHtml(lane)}</span><b>${count}</b></button>`).join("");
    if (!nodes.length) {
      el("node-list").innerHTML = `<div class="empty-list">当前筛选没有匹配节点</div>`;
      el("graph-caption").textContent = "0 个节点";
      return;
    }
    const scopeLabel = { all: "全量配置", main: "主线与启用结局", daily: "日常与咖啡馆片段", opening: "开场与教程", blocked: "明确阻塞项" }[state.scope];
    el("graph-caption").textContent = `${nodes.length.toLocaleString("zh-CN")} 个节点 · ${scopeLabel}`;
    el("node-list").innerHTML = nodes.map(node => {
      const metadata = node.blocker ? `<span class="node-meta node-blocker">阻塞</span>` : `<span class="node-meta">${escapeHtml(dateLabel(node.day).split(" · ")[0])}</span>`;
      return `<button class="node-row${state.selected === node.id ? " selected" : ""}" type="button" role="option" aria-selected="${state.selected === node.id}" data-node="${escapeHtml(node.id)}" style="--group-color:${GROUP_COLORS[node.group] || "#87918a"}">
        <span class="node-dot"></span><span class="node-main"><span class="node-title">${escapeHtml(node.title || node.key)}</span><span class="node-key">${escapeHtml(node.key)}</span></span>${metadata}</button>`;
    }).join("");
  }

  function renderGraph(nodes) {
    let viewNodes = nodes;
    if (state.onlyNeighbors && state.selected) {
      const neighborIds = new Set([state.selected]);
      for (const edge of state.graph.adjacency.get(state.selected) || []) neighborIds.add(edge.from === state.selected ? edge.to : edge.from);
      viewNodes = nodes.filter(node => neighborIds.has(node.id));
    }
    el("graph-empty").hidden = viewNodes.length > 0;
    if (!viewNodes.length) { el("lane-layer").innerHTML = ""; el("edge-layer").innerHTML = ""; el("node-layer").innerHTML = ""; return; }
    const laneMap = new Map();
    for (const node of viewNodes) { if (!laneMap.has(node.lane)) laneMap.set(node.lane, []); laneMap.get(node.lane).push(node); }
    const laneEntries = Array.from(laneMap.entries()).sort((a, b) => {
      const ga = GROUP_ORDER.indexOf(a[1][0].group), gb = GROUP_ORDER.indexOf(b[1][0].group);
      return (ga < 0 ? 99 : ga) - (gb < 0 ? 99 : gb) || a[0].localeCompare(b[0], "zh-CN");
    });
    const maxDay = Math.max(1, ...viewNodes.map(node => node.day || 0));
    const nodeWidth = 174, nodeHeight = 56, laneGap = 18, dayStep = 96;
    const positions = new Map(), laneMarkup = [];
    let yCursor = 52;
    for (const [lane, laneNodes] of laneEntries) {
      const buckets = new Map();
      for (const node of laneNodes) { const bucket = node.day || maxDay + 1; if (!buckets.has(bucket)) buckets.set(bucket, []); buckets.get(bucket).push(node); }
      const bucketKeys = Array.from(buckets.keys()).sort((a, b) => a - b);
      let stackHeight = 0;
      for (const bucket of bucketKeys) stackHeight = Math.max(stackHeight, buckets.get(bucket).length * 70);
      const laneHeight = Math.max(186, stackHeight + 48);
      laneMarkup.push(`<rect class="lane-band" x="18" y="${yCursor}" width="${Math.max(1000, (maxDay + 2) * dayStep + 220)}" height="${laneHeight - laneGap}" rx="4"></rect>`);
      laneMarkup.push(`<text class="lane-label" x="30" y="${yCursor + 22}">${escapeHtml(lane)}</text>`);
      for (const bucket of bucketKeys) {
        const bucketNodes = buckets.get(bucket).slice().sort((a, b) => a.key.localeCompare(b.key));
        const day = bucket > maxDay ? 0 : bucket, x = 112 + (bucket - 1) * dayStep;
        laneMarkup.push(`<text class="day-label" x="${x}" y="${yCursor + 22}">${escapeHtml(day ? dateLabel(day) : "续接 / 未指定")}</text>`);
        bucketNodes.forEach((node, index) => positions.set(node.id, { x, y: yCursor + 32 + index * 70, w: nodeWidth, h: nodeHeight }));
      }
      yCursor += laneHeight;
    }
    const width = Math.max(1100, (maxDay + 2) * dayStep + nodeWidth + 180), height = Math.max(500, yCursor + 30);
    state.geometry = { width, height, positions };
    el("lane-layer").innerHTML = laneMarkup.join("");
    const visibleIds = new Set(viewNodes.map(node => node.id)), edgeGroups = new Map();
    for (const edge of state.graph.edges) {
      if (!visibleIds.has(edge.from) || !visibleIds.has(edge.to)) continue;
      const key = `${edge.from}\n${edge.to}\n${edge.kind}`;
      if (!edgeGroups.has(key)) edgeGroups.set(key, { ...edge, count: 0 });
      edgeGroups.get(key).count++;
    }
    const edgeMarkup = [];
    for (const edge of edgeGroups.values()) {
      const from = positions.get(edge.from), to = positions.get(edge.to);
      if (!from || !to) continue;
      const forward = to.x >= from.x, x1 = forward ? from.x + from.w : from.x, x2 = forward ? to.x : to.x + to.w;
      const y1 = from.y + from.h / 2, y2 = to.y + to.h / 2, dx = Math.max(30, Math.abs(x2 - x1) * .44);
      const kindClass = EDGE_CLASSES[edge.kind] || "prereq", isSelected = edge.from === state.selected || edge.to === state.selected;
      const classes = ["edge-path", kindClass, edge.conditional ? "conditional" : "", edge.kind === "Exclusion" ? "exclusion" : "", edge.kind === "Unresolved" ? "unresolved" : "", state.selected ? (isSelected ? "selected-edge" : "faint-edge") : ""].filter(Boolean).join(" ");
      const marker = edge.kind.startsWith("Check") ? "check" : kindClass;
      const d = `M ${x1} ${y1} C ${x1 + (forward ? dx : -dx)} ${y1}, ${x2 - (forward ? dx : -dx)} ${y2}, ${x2} ${y2}`;
      edgeMarkup.push(`<path class="${classes}" d="${d}" stroke="${edgeColor(edge.kind)}" marker-end="url(#arrow-${marker})"><title>${escapeHtml(edge.label)}${edge.count > 1 ? ` ×${edge.count}` : ""}\n${escapeHtml(edge.evidence)}</title></path>`);
    }
    el("edge-layer").innerHTML = edgeMarkup.join("");
    const nodeMarkup = [], selectedEdges = state.selected ? state.graph.adjacency.get(state.selected) || [] : [];
    for (const node of viewNodes) {
      const p = positions.get(node.id);
      const adjacent = selectedEdges.some(edge => edge.from === node.id || edge.to === node.id);
      const classes = ["story-node", state.selected === node.id ? "selected" : "", adjacent ? "adjacent" : "", node.blocker ? "blocked" : "", node.disabled ? "disabled" : ""].filter(Boolean).join(" ");
      const title = truncate(node.title || node.key, 22), key = truncate(node.key, 28), mark = node.blocker ? "阻塞" : node.disabled ? "禁用" : "";
      nodeMarkup.push(`<g class="${classes}" transform="translate(${p.x},${p.y})" data-node="${escapeHtml(node.id)}" tabindex="0" role="button" aria-label="${escapeHtml(node.title || node.key)}">
        <rect width="${nodeWidth}" height="${nodeHeight}" rx="4" style="stroke:${node.blocker ? "#c27468" : GROUP_COLORS[node.group] || "#d3d9d2"}"></rect>
        <rect x="0" y="0" width="4" height="${nodeHeight}" rx="2" fill="${GROUP_COLORS[node.group] || "#87918a"}" stroke="none"></rect>
        <text class="node-title-svg" x="12" y="22">${escapeHtml(title)}</text><text class="node-key-svg" x="12" y="42">${escapeHtml(key)}</text>
        ${mark ? `<text class="node-mark-svg" x="${nodeWidth - 36}" y="42">${mark}</text>` : ""}</g>`);
    }
    el("node-layer").innerHTML = nodeMarkup.join("");
    if (!state.viewBox || state.fitPending) fitView(); else applyViewBox();
    state.fitPending = false;
  }
  const edgeColor = kind => ({ Prerequisite: "#557b66", Exclusion: "#ad6257", Continuation: "#a56c28", Ending: "#a56c28", CheckSuccess: "#597a9b", CheckFailure: "#597a9b", Unresolved: "#8b7b60" }[kind] || "#557b66");
  const truncate = (value, max) => { const text = String(value || ""); return text.length > max ? `${text.slice(0, max - 1)}…` : text; };

  function fitView() {
    const wrap = el("canvas-wrap"), w = Math.max(400, wrap.clientWidth - 32), h = Math.max(260, wrap.clientHeight - 32);
    const geometry = state.geometry || { width: 1200, height: 600 }, scale = Math.min(w / geometry.width, h / geometry.height);
    const width = w / Math.max(.03, scale), height = h / Math.max(.03, scale);
    state.viewBox = { x: (geometry.width - width) / 2, y: (geometry.height - height) / 2, width, height };
    state.fitViewBox = { ...state.viewBox }; state.zoomScale = 1; applyViewBox();
  }
  function applyViewBox() {
    if (!state.viewBox) return;
    const { x, y, width, height } = state.viewBox;
    el("graph-svg").setAttribute("viewBox", `${x} ${y} ${width} ${height}`);
    el("zoom-reset").textContent = `${Math.round(state.zoomScale * 100)}%`;
  }
  function zoomBy(factor, anchorX, anchorY) {
    if (!state.viewBox) fitView();
    const box = { ...state.viewBox }, rect = el("graph-svg").getBoundingClientRect();
    const px = anchorX == null ? .5 : Math.max(0, Math.min(1, (anchorX - rect.left) / rect.width));
    const py = anchorY == null ? .5 : Math.max(0, Math.min(1, (anchorY - rect.top) / rect.height));
    const width = Math.max(180, Math.min(50000, box.width / factor)), ratio = width / box.width, height = box.height * ratio;
    state.viewBox = { x: box.x + (box.width - width) * px, y: box.y + (box.height - height) * py, width, height };
    state.zoomScale *= factor; applyViewBox();
  }
  function centerNode(id) {
    const positions = state.geometry?.positions, selected = positions?.get(id);
    if (!selected || !state.viewBox) return;
    let minX = selected.x, maxX = selected.x + selected.w, minY = selected.y, maxY = selected.y + selected.h;
    for (const edge of state.graph.adjacency.get(id) || []) {
      const otherId = edge.from === id ? edge.to : edge.from, point = positions.get(otherId);
      if (!point) continue;
      minX = Math.min(minX, point.x); maxX = Math.max(maxX, point.x + point.w);
      minY = Math.min(minY, point.y); maxY = Math.max(maxY, point.y + point.h);
    }
    const wrap = el("canvas-wrap"), screenWidth = Math.max(400, wrap.clientWidth - 24), screenHeight = Math.max(260, wrap.clientHeight - 24);
    const ratio = screenWidth / screenHeight;
    let width = Math.max(760, maxX - minX + 300), height = Math.max(460, maxY - minY + 220);
    if (width / height > ratio) height = width / ratio; else width = height * ratio;
    state.viewBox = { x: (minX + maxX - width) / 2, y: (minY + maxY - height) / 2, width, height };
    state.zoomScale = state.fitViewBox ? state.fitViewBox.width / width : 1;
    applyViewBox();
  }

  function initialize() {
    el("reload-button").addEventListener("click", loadConfig);
    el("fit-button").addEventListener("click", () => { state.fitPending = true; state.fitRequested = true; render(); });
    el("zoom-in").addEventListener("click", () => zoomBy(1.25));
    el("zoom-out").addEventListener("click", () => zoomBy(.8));
    el("zoom-reset").addEventListener("click", fitView);
    el("search-input").addEventListener("input", event => { state.query = event.target.value; render(); });
    el("faction-filter").addEventListener("change", event => { state.faction = event.target.value; state.groupFocus = ""; render(); });
    el("neighbors-only").addEventListener("change", event => { state.onlyNeighbors = event.target.checked; render(); });
    el("clear-selection").addEventListener("click", () => { state.selected = ""; state.groupFocus = ""; render(); });
    document.querySelectorAll(".scope-button").forEach(button => button.addEventListener("click", () => {
      state.scope = button.dataset.scope; state.groupFocus = ""; state.selected = ""; state.fitPending = true;
      document.querySelectorAll(".scope-button").forEach(item => item.classList.toggle("active", item === button)); render();
    }));
    el("group-summary").addEventListener("click", event => {
      const button = event.target.closest("[data-lane]"); if (!button) return;
      state.groupFocus = state.groupFocus === button.dataset.lane ? "" : button.dataset.lane; render();
    });
    el("node-list").addEventListener("click", event => {
      const button = event.target.closest("[data-node]"); if (button) selectNode(button.dataset.node, true);
    });
    const svg = el("graph-svg");
    svg.addEventListener("click", event => { const node = event.target.closest("[data-node]"); if (node) selectNode(node.dataset.node, true); });
    svg.addEventListener("wheel", event => { event.preventDefault(); zoomBy(event.deltaY < 0 ? 1.12 : .89, event.clientX, event.clientY); }, { passive: false });
    svg.addEventListener("pointerdown", event => {
      if (event.target.closest("[data-node]")) return;
      state.dragging = { x: event.clientX, y: event.clientY, view: { ...state.viewBox } };
      svg.setPointerCapture(event.pointerId); svg.classList.add("dragging");
    });
    svg.addEventListener("pointermove", event => {
      if (!state.dragging) return;
      const rect = svg.getBoundingClientRect();
      state.viewBox.x = state.dragging.view.x - (event.clientX - state.dragging.x) * state.dragging.view.width / rect.width;
      state.viewBox.y = state.dragging.view.y - (event.clientY - state.dragging.y) * state.dragging.view.height / rect.height;
      applyViewBox();
    });
    const endDrag = () => { state.dragging = null; svg.classList.remove("dragging"); };
    svg.addEventListener("pointerup", endDrag); svg.addEventListener("pointercancel", endDrag);
    document.addEventListener("keydown", event => {
      if (event.key === "/" && !["INPUT", "TEXTAREA"].includes(document.activeElement.tagName)) { event.preventDefault(); el("search-input").focus(); }
      if (event.key === "Escape" && document.activeElement === el("search-input")) { el("search-input").value = ""; state.query = ""; render(); }
    });
    window.addEventListener("resize", () => { if (state.fitViewBox) { state.fitPending = true; render(); } });
    loadConfig();
  }

  async function loadConfig() {
    el("status-message").textContent = "正在读取官方 Luban 生成表…";
    el("reload-button").disabled = true;
    try {
      const loaded = await readTables();
      const previousSelection = state.selected;
      state.graph = buildGraph(loaded.tables); state.lastModified = loaded.modified; state.error = ""; state.fitPending = true;
      state.selected = state.graph.nodeMap.has(previousSelection) ? previousSelection : state.graph.nodeMap.has("event:oct07.s1") ? "event:oct07.s1" : "";
      el("status-message").textContent = "已读取生成配置；本页面不会推演运行时状态。";
      render();
    } catch (error) {
      state.error = error.message;
      el("node-list").innerHTML = `<div class="empty-list">${escapeHtml(error.message)}<br><br>请从项目根目录启动本地静态服务器，再打开此页面。</div>`;
      el("status-message").textContent = "配置读取失败"; el("snapshot-label").textContent = "无法连接生成表";
    } finally { el("reload-button").disabled = false; }
  }

  function selectNode(id, focus) {
    if (!state.graph?.nodeMap.has(id)) return;
    state.selected = id; state.detailTab = "overview"; render();
    if (focus) centerNode(id);
    const row = el("node-list").querySelector(`[data-node="${CSS.escape(id)}"]`);
    if (row) row.scrollIntoView({ block: "nearest" });
  }

  function renderDetails(node) {
    const root = el("details-content");
    if (!node) {
      root.innerHTML = `<div class="empty-detail"><div class="empty-mark" aria-hidden="true">◇</div><h2>选择一个节点</h2><p>查看入口、正文、选项、结果和关系证据。</p></div>`;
      return;
    }
    const typeName = node.kind === "Event" ? "事件根" : node.kind === "Ending" ? "结局" : node.kind === "Opening" ? "开场演出" : "事实条件";
    const badges = [`<span class="badge">${escapeHtml(groupLabel(node.group))}</span>`, node.day ? `<span class="badge">${escapeHtml(dateLabel(node.day))}</span>` : "",
      node.blocker ? `<span class="badge warning">明确阻塞</span>` : "", node.disabled ? `<span class="badge disabled">禁用</span>` : ""].filter(Boolean).join("");
    const tabs = [["overview", "入口 / 结果"], ["content", "正文 / 选项"], ["relations", "关系证据"]];
    root.innerHTML = `<div class="detail-topline"><span class="detail-kind">${typeName} · ${escapeHtml(node.lane)}</span>
      <div class="detail-actions"><button type="button" data-copy="${escapeHtml(node.key)}">复制 ID</button><button type="button" data-focus="${escapeHtml(node.id)}">定位</button></div></div>
      <h2 class="detail-title">${escapeHtml(node.title || node.key)}</h2><div class="detail-id">${escapeHtml(node.key)}</div>
      <div class="detail-badges">${badges}</div>
      <div class="detail-tabs" role="tablist">${tabs.map(([key, label]) => `<button class="detail-tab${state.detailTab === key ? " active" : ""}" type="button" role="tab" aria-selected="${state.detailTab === key}" data-detail-tab="${key}">${label}</button>`).join("")}</div>
      <div class="detail-tab-body">${state.detailTab === "overview" ? renderOverview(node) : state.detailTab === "content" ? renderContent(node) : renderRelations(node)}</div>`;
    root.querySelectorAll("[data-detail-tab]").forEach(button => button.addEventListener("click", () => { state.detailTab = button.dataset.detailTab; renderDetails(node); }));
    root.querySelectorAll("[data-target]").forEach(button => button.addEventListener("click", () => selectNode(button.dataset.target, true)));
    root.querySelector("[data-copy]")?.addEventListener("click", async event => {
      try { await navigator.clipboard.writeText(node.key); el("status-message").textContent = `已复制 ${node.key}`; }
      catch { el("status-message").textContent = "浏览器未允许访问剪贴板"; }
    });
    root.querySelector("[data-focus]")?.addEventListener("click", () => centerNode(node.id));
  }

  function section(title, body) {
    return `<section class="detail-section"><h3>${escapeHtml(title)}</h3>${body}</section>`;
  }
  function detailRow(label, value) {
    return `<div class="detail-row"><span>${escapeHtml(label)}</span><span>${escapeHtml(value || "未配置")}</span></div>`;
  }
  function renderOverview(node) {
    const graph = state.graph, blocks = [];
    blocks.push(section("基本信息", detailRow("类型", node.kind) + detailRow("入口", node.entry) +
      detailRow("日期排布", node.day ? `${dateLabel(node.day)}（${node.dateSource || "仅用于排布"}）` : "未指定 / 续接") +
      detailRow("一次性", node.kind === "Event" ? String(node.onceOnly) : "不适用") +
      detailRow("阻塞原因", node.blocker || "无已标记阻塞") +
      detailRow("配置摘要", node.snippet || "没有正文摘要")));
    if (node.kind === "Event") {
      if (node.dates?.length) blocks.push(section("日期入口", node.dates.map(date => detailRow("EventDate", `${dateLabel(Number(date.day) || 0)} · ${date.timeSlot || ""} · ${date.repeatRule || ""} · priority ${date.priority}`)).join("")));
      const conds = graph.maps.conditions.get(`event:${node.raw.conditionOwnerId || node.raw.eventId}`) || [];
      blocks.push(section("根事件前置条件（组内 AND / 组间 OR）", renderConditionGroups(conds)));
      const actions = graph.tables.actions.filter(action => String(action.resultEvents || "").split(/[|,;]/).map(value => value.trim()).includes(node.key));
      if (actions.length) blocks.push(section("地图行动入口", actions.map(action => {
        const actionConditions = graph.maps.conditions.get(`action:${action.actionKey}`) || [];
        return detailRow(graph.textMap.get(action.nameText) || action.actionKey, `${action.actionKey} · ${action.requirements || "无额外消耗信息"} · 必做=${action.mustExecute}`) +
          `<div class="detail-copy">行动前置条件（组内 AND / 组间 OR）：</div>${renderConditionGroups(actionConditions)}`;
      }).join("")));
      const resultRows = [];
      for (const [ownerKey, entries] of graph.owners) for (const owner of entries.filter(item => item.node === node.id)) {
        for (const result of graph.maps.results.get(ownerKey) || []) {
          const guard = result.resultId ? (graph.maps.conditions.get(`result:${result.resultId}`) || []) : [];
          const guardText = guard.length ? ` · 条件 ${guard.map(conditionText).join(" / ")}` : "";
          resultRows.push(detailRow(`${result.seq}. ${result.resultType}`, `${result.target} = ${result.value}${result.note ? ` · ${result.note}` : ""}${guardText} · ${owner.context}`));
        }
      }
      blocks.push(section("结果来源（配置记录，不代表每条路径都会执行）", resultRows.join("") || `<p class="detail-copy">没有登记在此事件根或其选项 / 检定结果所有者下的结果。</p>`));
      if (node.triggers?.length) {
        const triggerRows = node.triggers.map(trigger => {
          const bar = graph.maps.bars.get(trigger.barKey), barName = bar ? graph.textMap.get(bar.nameText) || bar.barKey : trigger.barKey;
          return detailRow(trigger.triggerId, `${barName} · ${trigger.direction} ${trigger.threshold} · ${trigger.crossType} · ${trigger.onceType} · ${trigger.enabled ? "启用" : "禁用"}`);
        }).join("");
        blocks.push(section("数值阈值触发器", triggerRows));
      }
      blocks.push(section("存档 / 调度", detailRow("阻塞存档", String(node.blocksSave)) + detailRow("演出", node.raw.presentationKey || node.raw.setupKey || node.raw.setupType || "未配置") + detailRow("事件标记", node.raw.logTag || "")));
    } else if (node.kind === "Ending") {
      blocks.push(section("结局判定", detailRow("启用", String(!node.disabled)) + detailRow("优先级", String(node.raw.priority)) +
        renderConditionGroups(graph.maps.conditions.get(`ending:${node.key}`) || [])));
    } else if (node.kind === "Fact") {
      blocks.push(section("条件状态配置", detailRow("初始值", String(Boolean(node.raw.initialValue))) + detailRow("仅允许置真", String(Boolean(node.raw.onlySetTrue))) +
        `<p class="detail-copy">未找到表内生产者只表示当前关系扫描未发现生成表来源；宿主代码、初始状态或未接入内容仍可能提供该事实。</p>`));
    } else {
      blocks.push(section("入口说明", `<p class="detail-copy">OpeningPresentation 是独立演出快照，不等同 Event 结果事务。</p>`));
    }
    return blocks.join("");
  }

  function renderConditionGroups(conditions) {
    if (!conditions.length) return `<p class="detail-copy">无表内条件；运行时入口、忙碌状态及宿主门禁仍可能限制触发。</p>`;
    const groups = new Map();
    for (const condition of conditions) { if (!groups.has(condition.groupIndex)) groups.set(condition.groupIndex, []); groups.get(condition.groupIndex).push(condition); }
    return Array.from(groups.entries()).sort((a, b) => a[0] - b[0]).map(([group, items]) => `<div class="condition-group"><div class="condition-title">条件组 ${group} · 组内 AND</div>${items.map(condition => `<div class="condition-line">${escapeHtml(conditionText(condition))}<br><small>${escapeHtml(condition.conditionId)}</small></div>`).join("")}</div>`).join("");
  }

  function renderContent(node) {
    const content = node.content || [];
    if (!content.length) return `<p class="detail-copy">没有可读正文块。该节点仍可能只有触发条件或结果定义。</p>`;
    return `<p class="detail-copy">共 ${content.length} 个配置内容块。显示配置正文，不推断互斥页的自然播放顺序。</p>` + content.map((block, index) =>
      `<details class="content-block"${index === 0 ? " open" : ""}><summary class="content-summary">${escapeHtml(block.caption || block.id)}</summary>
        <div class="content-body">${escapeHtml(block.bodyText || "（该页无正文文本）")}${block.diagnostics ? `<div class="relation-proof">${escapeHtml(block.diagnostics)}</div>` : ""}</div></details>`).join("");
  }

  function renderRelations(node) {
    const graph = state.graph;
    const edges = (graph.adjacency.get(node.id) || []).slice().sort((a, b) => {
      const otherA = a.from === node.id ? a.to : a.from, otherB = b.from === node.id ? b.to : b.from;
      return (graph.nodeMap.get(otherA)?.key || otherA).localeCompare(graph.nodeMap.get(otherB)?.key || otherB) || a.kind.localeCompare(b.kind);
    });
    if (!edges.length) return `<p class="detail-copy">没有扫描到跨节点连线。独立开场、代码入口或没有表内因果引用的节点可能没有关系证据。</p>`;
    return `<p class="detail-copy">${edges.length} 条原始关系证据。点击另一端可跳转。线条不代表运行时条件已满足。</p>` + edges.map(edge => {
      const outgoing = edge.from === node.id, otherId = outgoing ? edge.to : edge.from, other = graph.nodeMap.get(otherId);
      const direction = outgoing ? "下游 →" : "上游 ←";
      return `<button class="relation-item" type="button" data-target="${escapeHtml(otherId)}">
        <span class="relation-head"><span>${direction} ${escapeHtml(other?.title || other?.key || otherId)}</span><span class="relation-type">${escapeHtml(EDGE_LABELS[edge.kind] || edge.kind)}${edge.conditionGroup ? ` · 组 ${edge.conditionGroup}` : ""}</span></span>
        <span class="relation-proof">${escapeHtml(edge.label)}${edge.conditional ? " · 条件分支" : ""}\n${escapeHtml(edge.evidence)}</span>
      </button>`;
    }).join("");
  }

  initialize();
})();
