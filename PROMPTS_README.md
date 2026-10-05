# 美术生成工作流（Trae Work 专用）

用 Trae Work 为《Spotlight》咖啡店玩法生成全部 8 张 2D 美术，并自动替换当前占位图。

## 产物与位置

| 文件 | 作用 |
|---|---|
| `Assets/Art/Gameplay/prompts.json` | 提示词清单：8 张图的完整 prompt、尺寸、透明背景要求、后处理规则 |
| `Assets/ConfigTables/apply_ai_art.py` | 后处理脚本：把生成图裁边/抠图后覆盖到最终路径 |
| `Assets/Art/Gameplay/AI/` | 生成图的暂存目录（Trae Work 的输出位置） |

## 三步工作流

**第 1 步：生成（在 Trae Work 中执行）**
打开本项目，读取 `Assets/Art/Gameplay/prompts.json`，对 `assets` 数组中的每一项：
- 按 `prompt` 字段生成图片（注意遵守 `size`、`transparent_background` 要求）
- 保存到该项的 `output` 路径（即 `Assets/Art/Gameplay/AI/<id>.png`）
- 全部 8 张依次生成；已存在且有效的文件可跳过

**第 2 步：后处理（Trae Work 或手动执行）**

```powershell
python Assets/ConfigTables/apply_ai_art.py
```

脚本按 `postprocess` 规则处理并覆盖到 `Assets/Art/Gameplay/<id>.png`（正式路径）：
- `none`：背景图原样复制
- `trim-or-circle`：有透明通道 → 裁掉透明边；无透明 → 圆形遮罩（头像徽章）
- `trim-or-key`：有透明通道 → 裁掉透明边；无透明 → 边缘色键抠图 + 裁剪

**第 3 步：回到 Unity**
等 Unity 自动导入（同名覆盖，GUID 不变），CoffeeShop 场景立即显示新美术，无需任何拖拽。

## 约束与兜底

- 输出必须为 PNG；文件名必须与清单 `id` 完全一致
- 人物和道具优先要求透明背景；生成器不支持时改用纯品红 `#FF00FF` 纯色背景
- 所有图禁止出现文字、水印
- **后悔药**：`python Assets/ConfigTables/draw_art.py` 可重新生成程序化占位图，覆盖回原始状态
- 抠图失败保护：脚本检测到抠图后内容面积过小会自动跳过该文件并提示，不会写入损坏图片