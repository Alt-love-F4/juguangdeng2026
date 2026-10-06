# 项目结构与协作边界

## 当前正式入口

```text
Assets/Scenes/SampleScene.unity
    主菜单与 GameFlow 入口
        -> Assets/Scenes/CoffeeShop.unity
           FreeCafe 正式经营场景
```

Build Settings 中只把正式运行流程放在前两项：

1. `Assets/Scenes/SampleScene.unity`
2. `Assets/Scenes/CoffeeShop.unity`

## 代码模块

```text
Assets/Scripts/Config/
    Luban 生成的配置类型与运行时支持

Assets/Scripts/GameFlow/
    主菜单、场景加载、暂停、设置、存档外壳和 Yoki/UIKit 面板

Assets/Scripts/FreeCafe/Domain/
    纯 C# 经营领域模型，不依赖 Unity 场景或 UI

Assets/Scripts/FreeCafe/Unity/
    FreeCafe 场景运行时、实体视图、HUD、输入和 GameFlow 桥接

Assets/Scripts/Narrative/
    Luban 故事系统。当前由 GameFlow 配置关闭运行时入口，后续恢复时继续使用

Assets/Scripts/TextLocalization/
    公共文本本地化服务

Assets/Editor/FreeCafe/
    FreeCafe 场景构建和编辑器工具
```

旧版 `Gameplay` / `Interaction` 咖啡工作流已经归档删除，不再作为新的代码依赖。

## Unity 资源目录

Unity 资源路径当前保持稳定，避免 `.meta` GUID 和场景引用变化：

```text
Assets/Scenes/
    正式场景

Assets/Resources/Art/UIPrefab/
    Yoki/UIKit 运行时面板 Prefab

Assets/Resources/FreeCafe/
    FreeCafe 原型目录、运行时目录资产和美术合同

Assets/Resources/Fonts/
    运行时字体

Assets/Settings/Resources/Luban/
    Luban 生成的运行时 JSON

Assets/ConfigTables/Data/
    Luban Excel 源表
```

## 双人分工

### GameFlow 负责人

修改范围优先放在：

- `Assets/Scripts/GameFlow/`
- `Assets/Resources/Art/UIPrefab/`
- `Assets/Scenes/SampleScene.unity`
- `Assets/Settings/GameFlowConfig.asset`

负责主菜单、暂停、设置、通用弹窗、场景生命周期和 Yoki/UIKit 面板。

### FreeCafe 负责人

修改范围优先放在：

- `Assets/Scripts/FreeCafe/`
- `Assets/Editor/FreeCafe/`
- `Assets/Scenes/CoffeeShop.unity`
- `Assets/Resources/FreeCafe/`
- `Assets/Prefabs/FreeCafe/`

负责经营领域模型、咖啡店输入、实体视图、经营 HUD 和经营配表。

### 共同接口

双方只通过这些稳定边界连接：

- `GameplayPanelHooks`
- `FreeCafeGameFlowBridge`
- `GameFlowConfig`
- `GameSaveParticipantRegistry`
- Luban 生成程序集

不要在 `GameFlowBootstrap` 中直接访问 `CafeWorld.State`，也不要在 `CafeWorld` 中依赖 UIKit、场景对象或具体面板。

## 提交规则

- `Assets` 中资源移动必须连同对应 `.meta` 一起移动。
- 优先在 Unity Project 窗口移动资源，不要只在系统文件管理器中移动单个资源。
- 代码移动后确认 asmdef、场景组件和 Prefab 引用仍然有效。
- 不提交 `Library/`、`Temp/`、`Logs/`、`UserSettings/`、`.yokiframe/` 和 `.unity/`。
- 修改 Luban 源表后重新生成代码和 JSON，生成物与源表放在同一个提交中。
- 不要手工修改 `Assets/Scripts/Config/Generated/` 中的自动生成代码。
- 每个功能提交只修改一个主要模块，减少两人合并冲突。

## 合并前检查

```text
git status
git diff --check
确认所有新增/移动的 Assets 都有 .meta
确认 SampleScene -> CoffeeShop 入口可用
确认 CoffeeShop 中暂停菜单可打开
确认 Unity Console 没有编译错误
```
