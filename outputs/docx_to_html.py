# -*- coding: utf-8 -*-
"""把填充后的 docx 转成单文件 HTML（章节层级 + 目录 + 表格）。"""
import html
import os
import zipfile
from xml.etree import ElementTree as ET

W = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"
SRC = r"E:/Unity project/聚光灯2026/outputs/隐私政策-沸点咖啡.docx"
OUT = r"E:/Unity project/聚光灯2026/outputs/privacy-policy/index.html"

TOC_NUM = "2"       # 文档自带目录
SECTION_NUM = "3"   # 正文章节标题

CN = "零一二三四五六七八九十"


def q(tag):
    return W + tag


def cn_num(n):
    if n <= 10:
        return CN[n]
    if n < 20:
        return "十" + CN[n - 10]
    if n < 100:
        return CN[n // 10] + "十" + (CN[n % 10] if n % 10 else "")
    return str(n)


def para_text(p):
    out = []
    for node in p.iter():
        if node.tag == q("t"):
            out.append(node.text or "")
        elif node.tag == q("tab"):
            out.append(" ")
    return "".join(out).strip()


def num_key(p):
    ppr = p.find(q("pPr"))
    if ppr is None:
        return None
    np = ppr.find(q("numPr"))
    if np is None:
        return None
    nid = np.find(q("numId"))
    return nid.get(q("val")) if nid is not None else None


def collect():
    z = zipfile.ZipFile(SRC)
    root = ET.fromstring(z.read("word/document.xml"))
    body = root.find(q("body"))
    blocks = []
    for el in body:
        if el.tag == q("p"):
            text = para_text(el)
            if not text:
                continue
            nid = num_key(el)
            if nid == TOC_NUM:
                continue
            if text.strip("… .·") == "":
                continue
            if nid == SECTION_NUM:
                blocks.append(["h2", text, None])
            elif text.startswith("（") and "）" in text[:6]:
                blocks.append(["h3", text, None])
            elif nid in {"4", "5", "10", "13", "17"} and len(text) < 40:
                blocks.append(["h4", text, None])
            else:
                blocks.append(["p", text, None])
        elif el.tag == q("tbl"):
            rows = []
            for tr in el.findall(q("tr")):
                cells = [" ".join(para_text(p) for p in tc.findall(q("p"))).strip()
                         for tc in tr.findall(q("tc"))]
                rows.append(cells)
            blocks.append(["table", rows, None])

    # 去掉紧跟标题的空章节（模板遗留的 “……” 占位段已被过滤，会产生连续标题）
    cleaned = []
    for i, b in enumerate(blocks):
        if b[0] in ("h3", "h4"):
            nxt = blocks[i + 1][0] if i + 1 < len(blocks) else "end"
            if nxt in ("h3", "h4", "h2", "end"):
                continue
        cleaned.append(b)
    return cleaned


def build(blocks):
    sec = 0
    toc = []
    buf = []
    for kind, val, _ in blocks:
        if kind == "h2":
            sec += 1
            anchor = f"sec{sec}"
            toc.append((anchor, f"{cn_num(sec)}、{val}"))
            buf.append(f'<h2 id="{anchor}"><span class="no">{cn_num(sec)}</span>{html.escape(val)}</h2>')
        elif kind == "h3":
            buf.append(f"<h3>{html.escape(val)}</h3>")
        elif kind == "h4":
            buf.append(f"<h4>{html.escape(val)}</h4>")
        elif kind == "table":
            buf.append('<div class="tw"><table>')
            for i, row in enumerate(val):
                tag = "th" if i == 0 else "td"
                tds = "".join(f"<{tag}>{html.escape(c)}</{tag}>" for c in row)
                buf.append(f"<tr>{tds}</tr>")
            buf.append("</table></div>")
        else:
            buf.append(f"<p>{html.escape(val)}</p>")
    return "\n".join(buf), toc


CSS = """
:root{--ink:#20201f;--sub:#5d5d68;--line:#e6e3dd;--bg:#f6f4f0;--card:#fff;
 --accent:#8a5a2b;--accent-soft:#f3ece3}
*{box-sizing:border-box}
html{scroll-behavior:smooth}
body{margin:0;background:var(--bg);color:var(--ink);
 font:16px/1.9 -apple-system,BlinkMacSystemFont,"PingFang SC","Microsoft YaHei","Segoe UI",sans-serif;
 -webkit-font-smoothing:antialiased}
.wrap{max-width:900px;margin:0 auto;padding:48px 24px 96px}
.card{background:var(--card);border:1px solid var(--line);border-radius:14px;
 padding:56px 60px 64px;box-shadow:0 2px 20px rgba(60,48,36,.05)}
.badge{display:inline-block;font-size:11px;letter-spacing:.2em;color:var(--accent);
 border:1px solid rgba(138,90,43,.3);border-radius:999px;padding:4px 12px;margin-bottom:18px}
h1{font-size:30px;line-height:1.4;margin:0 0 16px;font-weight:650;letter-spacing:.5px}
.meta{color:var(--sub);font-size:13.5px;border-top:1px solid var(--line);
 border-bottom:1px solid var(--line);padding:14px 0;margin-bottom:8px}
.meta span{display:inline-block;margin-right:22px}
nav.toc{background:var(--accent-soft);border-radius:10px;padding:22px 26px;margin:32px 0 8px}
nav.toc h5{margin:0 0 12px;font-size:13px;letter-spacing:.12em;color:var(--accent);font-weight:600}
nav.toc ol{margin:0;padding-left:0;list-style:none;columns:2;column-gap:32px;font-size:14px}
nav.toc li{margin:5px 0;break-inside:avoid}
nav.toc a{color:#3f3f47;text-decoration:none;border-bottom:1px solid transparent}
nav.toc a:hover{color:var(--accent);border-bottom-color:var(--accent)}
h2{font-size:19px;margin:52px 0 16px;padding-top:22px;border-top:1px solid var(--line);
 font-weight:650;line-height:1.6;display:flex;gap:10px;align-items:baseline}
h2 .no{color:var(--accent);font-size:15px;flex:none;letter-spacing:1px}
h3{font-size:16.5px;margin:30px 0 10px;font-weight:650;color:#33333b}
h4{font-size:15.5px;margin:22px 0 8px;font-weight:600;color:#4a4a55}
p{margin:11px 0;text-align:justify;color:#2c2c31}
.tw{overflow-x:auto;margin:18px 0 24px}
table{width:100%;border-collapse:collapse;font-size:14px;min-width:520px}
th,td{border:1px solid var(--line);padding:11px 13px;text-align:left;vertical-align:top;line-height:1.7}
th{background:var(--accent-soft);font-weight:600;white-space:nowrap}
footer{margin-top:44px;padding-top:22px;border-top:1px solid var(--line);
 color:var(--sub);font-size:12.5px;line-height:1.8}
@media (max-width:720px){
 .wrap{padding:20px 12px 60px}.card{padding:30px 20px 40px;border-radius:10px}
 h1{font-size:23px}nav.toc ol{columns:1}h2{font-size:17.5px}body{font-size:15px}
 .meta span{display:block;margin:3px 0}
}
"""


def main():
    blocks = collect()
    body, toc = build(blocks)
    nav = "\n".join(f'<li><a href="#{a}">{html.escape(t)}</a></li>' for a, t in toc)
    page = f"""<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="description" content="沸点咖啡 隐私政策 - 有机游戏工坊">
<title>沸点咖啡 隐私政策</title>
<style>{CSS}</style>
</head>
<body>
<div class="wrap"><div class="card">
<div class="badge">PRIVACY POLICY</div>
<h1>沸点咖啡 隐私政策</h1>
<div class="meta">
<span>运营主体：有机游戏工坊</span><span>更新日期：2026年10月19日</span><span>生效日期：2026年10月19日</span>
</div>
<nav class="toc"><h5>目录</h5><ol>{nav}</ol></nav>
{body}
<footer>本页面为《沸点咖啡 隐私政策》的公开版本，与提交审核的正式文档内容一致。如对本政策有任何疑问、意见或建议，可通过上文「如何联系我们」中的方式与我们联系。</footer>
</div></div>
</body>
</html>
"""
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(page)
    print("written", OUT, len(page), "bytes; sections:", len(toc))


if __name__ == "__main__":
    main()
