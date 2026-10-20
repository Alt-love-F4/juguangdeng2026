# Android 手机适配实施计划

日期：2026-10-21
状态：待用户审核

## 执行方式与边界

- 规格依据：`Docs/superpowers/specs/2026-10-21-android-mobile-adaptation-design.md`。
- 在当前共享工作区直接实施；先逐项检查将要修改文件的已有差异，保留所有已暂存、未暂存和未跟踪的用户改动。
- Android 输入、方向、帧率和教程显示走独立平台分支。PC 鼠标/键盘语义、PC 教程文案、Standalone 设置和现有存档/业务流程保持不变。
- 不改 NCS/Excel/Luban 内容，不重建咖啡馆玩法或调试场景，不覆盖纹理 `.meta`。APK 只作为本地产物。

## 实施步骤

1. **工具链与基线**：在 Unity Preferences > External Tools 核验 Android SDK、NDK、JDK 路径；必要时指向 `D:\Android\Sdk`、`D:\Android\Sdk\ndk\27.2.12479018`、`D:\Android\OpenJDK17` 并重启 Editor。记录当前活动构建平台和 Android PlayerSettings；检查 `ProjectSettings/ProjectSettings.asset` 现有差异，不覆盖用户改动。运行现有咖啡工作台、地图教程和拉花相关测试作为基线。
2. **咖啡触屏倒液**：在 `FreeCafeRuntime` 现有拖拽事务内增加可测试的触屏倒液状态；仅 Android 触屏在有效接收目标上停留后进入倾倒，纵向移动双向调节流量，抬指、取消、失焦或暂停时停止流体并清理所有权。保留奶缸专用拉花流程。更新 `FreeCafeWorkstationTutorial` 与手势提示，只替换 Android 提示；原 PC 左键拖拽加右键倾倒和原文案不变。
3. **地图手势**：在 `MapSceneController` 增加 Android 单指地图平移和双指捏合缩放，沿用现有地图边界、缩放和 UI 命中规则；触点落在 HUD/可选控件时不启动地图手势。PC 鼠标拖动、滚轮缩放、右键收起焦点和键盘操作保持原分支。按平台更新 `MapUsageTutorial` 的操作说明。
4. **拉花触屏与显示**：让 `LatteArtPrototype` 从当前活动指针读取位置/按压并用同一指针做 UI 命中检测；Android 单指绘制、抬指断笔，PC 鼠标绘制与滚轮调笔刷保持原行为。在 `FreeCafeLatteArtBench` 的实际玩家界面增加仅 Android 显示的笔刷尺寸控件，不复用只供 GM 调试的 `LatteArtRuntimeControls`。Android 启动方向、交互内容安全区、系统返回和切后台输入清理只接入 Android 运行路径。
5. **回归测试**：添加触屏倒液状态和取消清理测试；覆盖 Android 手势策略、地图单指/双指输入、拉花触屏/UI 阻挡/笔刷控件、横屏安全区及平台返回行为。运行原有 PC 右键倒液、左键拖放、地图滚轮、鼠标拉花和 PC 教程回归，验证 Standalone 配置及场景列表未改变。
6. **APK 验收**：使用现有正式启动、咖啡馆、地图场景构建 ARM64/IL2CPP Android 测试 APK 到 `Builds/Android/Spotlight-Android-Test.apk`。用 `D:\Android\Sdk\platform-tools\adb.exe` 检查设备；如有连接设备则安装并验证启动、返回、地图、教程、倒液和拉花。记录 APK 构建与真机验收为两个独立结果，并在构建后恢复原活动构建平台。

## 完成条件

- Android 测试 APK 成功构建，Unity 实际使用的 SDK/NDK/JDK 路径和构建参数明确。
- Android 触屏交互、教程和横屏安全区通过对应测试；可用真机时完成安装试玩。
- PC 操作与教程回归通过；Standalone PlayerSettings、PC 构建场景和项目中原有改动得到保留。
- 测试失败、工具链未被 Unity 识别或设备未连接时，明确记录阻塞项，不把未验证项报告为通过。
