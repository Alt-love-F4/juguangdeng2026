# Luban 文本配表

策划维护 `Assets/ConfigTables/Data/Text.xlsx`。在 Unity Project 窗口展开 `Assets/ConfigTables` 即可找到 Excel 和 `GenerateText.bat`。双击 Excel 会交给系统默认程序打开；运行 bat 可使用右键 Show in Explorer 后双击（若 Unity 直接双击打开了文本编辑器，使用此方式）。

表头遵循 Luban 约定：

- `##var`：字段名
- `##type`：字段类型
- `##group`：客户端分组 `c`
- 第一行 A1 必须以 `##` 开头

生成方式：

```bat
GenerateText.bat
```

生成结果写入 Unity：

```text
Assets/Settings/Resources/Luban/tbtext.json
```

## 策划工作流

1. 使用 Excel 打开 `Assets/ConfigTables/Data/Text.xlsx`，从第 5 行起编辑 B/C/D 列的 `key / text / comment`，A 列留空。
2. key 必须唯一且非空，例如 `mainmenu.title`；text 和 comment 可留空，空 text 会显示为空字符串。
3. 保存并关闭 Excel，双击 `Assets/ConfigTables/GenerateText.bat`（或在终端运行）。只有退出码为 0 才代表生成成功。
4. 返回 Unity 等待导入，再进入 Play Mode。游戏不会直接读取 Excel，编辑表后必须重新生成。

不要修改前 4 行的表头、类型和分组。允许文本换行、引号和中文，Luban 会处理 JSON 转义。key 区分大小写。

运行时 `LocalizedTMPText` 读取 TMP 的初始 text 作为 key，查询 `tbtext.json` 并注入文本。给目标 TMP 对象挂载此组件即可；脚本创建的对象应在激活前设置 text。非配表的动态数值文本无需挂载。

`Assets/Archive/TextLocalization/TextTable_Archived.asset` 是已归档的旧示例，运行时不再读取它，请勿在两处分别维护文案。Luban 工具本体位于 `D:/Luban`，不会复制到 Unity 包体。更换机器时可设置 `LUBAN_DIR` 环境变量，指向包含 Luban.exe 的目录。
