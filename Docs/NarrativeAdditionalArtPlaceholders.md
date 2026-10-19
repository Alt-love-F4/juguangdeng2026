# 当前原稿补充美术需求卡（6项，非原93项替代）

直接读取当前DOCX的word/document.xml，按非空段P编号提取原文；每项保留当前源文件SHA256、段号及精确标记原文。没有使用旧抽取稿作为生产来源。

## 数量与范围

- 补充6张1920×1080素色中文卡：5项CG与1项双密道复合示意图。
- 原93项／31卡完全不改；补充清单独立，不回写其数量。两份清单合计99项、37张需求卡，不代表37张正式画稿订单。
- CG08允许复用；CG10原文P293允许立绘下移出屏幕替代CG；CG11复杂时可省略，两处重复标记共用一项。
- P12明确建筑平面＋地下剖面、两条密道、红色阻隔及店内中转，不是新玩法地图。
- 本补充仅处理明确委派的6项，不声称重新验收全部61份路线原稿或37处标记。

## 给主集成的JSON契约

`requirements`包含与主清单相同的id/category/name/status/requirement/sourceEvidence/configuration/notes/disposition/assetPath/suggestedRegistryKeys。
`sourceFile`、`sourceParagraphs`提供当前文件hash、非空段编号及精确原文；sourceAnchorVerified=true仅表示本次确已读取对应段。
**suggestedRegistryKeys在这份补充中是主集成指定的建议名称，不代表已注册**；suggestedRegistryKeysVerified=false，同时保留proposedUnregisteredKeys。本工具不读取／修改注册表，后续Unity绑定不会影响--check。
只允许主集成将明确挂点接到演出，绑定仍为placeholder，不能覆盖已解析真实美术；特殊CG后按来源显式恢复背景，普通咖啡馆保持scene.cafe。

|ID|需求|建议key|当前锚点|
|---|---|---|---|
|CG07|月光下墙角的玛丽|cg.marie_moonlit|P012|
|CG08|合作握手特写|cg.handshake|P591|
|CG09|雨中的玛丽、主角与勒苏|cg.rainy_triad|P228|
|CG10|雨水中倒地的玛丽|cg.marie_fallen|P292、P293|
|CG11|暖光角落中的王女|cg.alienor_corner|P453、P476|
|P12|咖啡馆建筑与双密道复合示意图|cg.cafe_tunnel_diagram|P384|

## 本地生成与只读检查

```powershell
$python = 'C:/Users/AltF4/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
& $python Tools/Narrative/build_additional_art_placeholders.py
& $python Tools/Narrative/build_additional_art_placeholders.py --check
```

首次仅创建缺失PNG；存在且不同则立即报错，绝不覆盖原图。--check重新读取当前DOCX、重排本地文字，校验6张PNG的字节、像素hash、尺寸、布局及清单。
仅写本补充脚本、Additional/*.png、本JSON和MD；不编辑主93项清单、原31卡、Excel、生成表、Runtime、Prefab、注册表。不运行Unity、无任何网络／图片API。
