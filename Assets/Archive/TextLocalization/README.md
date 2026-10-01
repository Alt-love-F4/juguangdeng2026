# 已归档：旧文本注入配置

本目录仅保留早期 ScriptableObject 文本表示例，不再作为正式文案来源。

- `TextTable_Archived.asset`：旧文本表，保留原 GUID 和内容，已添加 Archived / Legacy 标签。
- `TextTable.cs`：旧表类型，为保持旧资源可读取而保留。创建菜单位于 Archive/Localization/Legacy Text Table。

当前唯一配表入口：`Assets/ConfigTables/Data/Text.xlsx`。
生成入口：`Assets/ConfigTables/GenerateText.bat`。
运行时数据：`Assets/Settings/Resources/Luban/tbtext.json`。

旧表已移出 Resources，不会仅因位于 Resources 而随构建打包。不要同时维护旧表和 Excel。
