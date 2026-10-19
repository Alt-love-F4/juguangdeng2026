# 非结局需求 API 美术补充（2026-10-19）

用户本轮明确要求直接调用 API，补充结局之外尚未填充的部分需求，并遵循当前美术风格。使用用户已有 PackyAPI image skill，模型 `gpt-image-2.5-sunburst`；密钥预检成功，实际成功调用 **4次 images/edits**，没有自动重试、切换模型或批量扩张到整张清单。

参考图片只包含必要的项目角色原画和既有CG／咖啡馆完整对话构图。保留原人物辨识度、棕色细线、平涂色块、低饱和复古色彩与纸感。原始需求清单、人物原图、现有CG03–CG06、结局图片与目录均保留。

## 结果与游戏状态

| 需求 | 生成结果 | 绑定与限制 |
|---|---|---|
| CG07 月光下墙角的玛丽 | 成功；沿用短发、棕色背心、白衬衫与蓝领巾，月光咖啡馆场景 | 16:9交付图绑定既有 `cg.marie_moonlit`。正式 `revolution.1029.s1.page.p013.1` 至P020已有挂点，P021仍恢复scene.cafe。生成图已接入，不代表事件自然通关或人工美术验收。 |
| CG09 雨中三人 | 成功保存候选稿；玛丽、勒苏、破顶落雨与低视角可读 | **未替换运行时需求卡**。主角枪口画成下垂，与P225“举枪瞄准玛丽”不符；保留实际结果及原因，没有自动付费重试。它属于正文特殊CG，用户排除结局图的约束仍满足。 |
| CH35 教程老太太 | 成功；普通老年女顾客，朴素长裙与披肩；本地去除边界连通白背景 | 彩色透明立绘绑定需求预览用 `character.tutorial_elderly_woman`。从同张立绘本地派生剪影，接入已有 `Dialogue/CafeTutorialVisuals.centerPortrait`，替换旧通用坐姿剪影。保留既有教程黑影表现；未新增“恢复彩色”时机。人物不是经营最终坐姿，也没有新建角色ID。 |
| P04 国王币 | 成功生成单币；本地复用为14枚一组的透明素材 | 登记 `prop.king_coins`，需求窗口可预览。**尚无启用的正式剧情图片挂点**，不能报告为游戏已出现。14是图中数量，不改报酬数值；币面是生成的设计提案，不当作作者已确认的货币设定。 |

CG09只保留候选，不因已付费就错误接入。P02骑士画像本轮优先保留现有CG03的复用可能，P03名片宜采用精确法语／中文排版；CG02主角外观、暂定特效、服装差分和未确认项目未批量扩画。结局相关请求数量为0。

## 路径与可复核记录

- 可导入图片：`Assets/Art/NarrativeGenerated/`，包含CG07、CH35彩色与剪影、P04单币与组图，共5张派生／交付PNG。
- API原始输出：`output/art-fill-2026-10-19/raw/`，四张全部保留；候选CG09未放入运行时素材目录。
- 提示词和有序参考路径：`output/art-fill-2026-10-19/prompts/`。不含密钥。
- 台账和SHA256：`Docs/NarrativeGeneratedArtDelivery-2026-10-19.json`。`humanArtApproval=false`保留。
- 只做本地确定性处理：`Tools/Narrative/prepare_generated_art_20261019.py`。它不调用API、不重画人物、不改源表／注册表；重新处理会使用已保存的四份原始输出。
- Unity绑定通过现有AssetImporter／NarrativeStageVisuals.SetEntry／SerializedObject，未手改Unity YAML、源Excel或生成JSON。
- `art.requirement.*` 原始卡仍保留placeholder；三项实际图片key标为非需求卡，只表示真实图片已取代说明卡，不等于人工美术批准。

## 验证

图片已实际解码与目视检查。CH35检查透明边界和内部浅色衣服，单币只去除边界连通白背景。导入设置使用Single Sprite（人物／道具）、无mipmap、2048上限与非压缩；CG保持1536×864。

`output/art-fill-2026-10-19/preservation-check.json` 校验本轮只改三条注册：`cg.marie_moonlit`、`character.tutorial_elderly_woman`、`prop.king_coins`。另修改CafeTutorialVisuals的中心槽位；源表、结局资源、原图、需求卡逐文件hash保持一致。教程已有测试对新角色剪影名称同步，仍检查共享CafeSceneVisuals没有中心人物，以及首次登场之前不出现顾客。

本轮未宣称全清单完成、未宣称CG09可用、未改作者闸门或推进剧情事实。所有Panel后续回归仍使用既有PresentationDebugScene，不新增Scene／Canvas／Input。

### 最终验证记录

- Unity最终编译：errorCount=0、warningCount=0。
- EditMode：NarrativeArtPlaceholderTests **4/4叶用例通过**，原始结果 `output/art-fill-2026-10-19/editmode-result.json`。
- PlayMode：NcsOpeningDataTests **13/13叶用例通过**，已核对并归档Unity实际NUnit文件 `output/art-fill-2026-10-19/opening-playmode-result.xml`（2026-10-19 18:27:01–18:28:10 Asia/Shanghai）。桥接runner跨重载未更新状态、最后报RUNNER_TIMEOUT；没有用追踪超时替代实际NUnit结果，也未凭超时宣称通过。
- 最终通过Unity实际资源API复核3条注册与教程Sprite路径，CG09仍placeholder；CH35窗口error=null，见 `output/art-fill-2026-10-19/final-binding-check.json`。
- 基准资源保护检查和交付PNG哈希通过。本轮只读／绑定验证未播放完整剧情，不宣称自然可达或视觉最终验收。
