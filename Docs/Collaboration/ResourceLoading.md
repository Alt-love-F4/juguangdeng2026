# 资源加载约定

## 结论

`Resources.Load` 是 Unity 自带的资源加载 API，不是 Yoki 专属规范。Yoki/UIKit 的部分运行时面板可以通过 Resources 加载，但项目不应把所有资源都无条件放入 Resources。

当前项目正处于两人合并和原型验证阶段，保留现有 Resources 路径，避免一次迁移同时改动场景、Prefab、程序集和运行时加载代码。

## 当前允许使用 Resources 的范围

```text
Assets/Resources/Art/UIPrefab/
    Yoki/UIKit 运行时动态面板

Assets/Resources/FreeCafe/
    FreeCafe 原型目录资产和运行时配置

Assets/Resources/Fonts/
    运行时需要按名称取得的字体

Assets/Settings/Resources/Luban/
    Luban 生成的运行时 JSON
```

## 路径规则

`Resources.Load` 的路径不包含 `Assets/Resources/`，不包含扩展名：

```csharp
Resources.Load<TextAsset>("FreeCafe/catalog")
Resources.Load<TMP_FontAsset>("Fonts/SimHei SDF")
Resources.Load<TextAsset>("Luban/tbtext")
```

对应文件分别是：

```text
Assets/Resources/FreeCafe/catalog.json
Assets/Resources/Fonts/SimHei SDF.asset
Assets/Settings/Resources/Luban/tbtext.json
```

移动文件时必须同步修改代码中的加载路径，并在 Unity 中重新导入验证。

## 不应放入 Resources 的内容

以下资源优先使用 Inspector 序列化引用：

- 场景专用贴图
- 场景专用材质
- 场景专用 Prefab
- 不需要动态查找的 UI 图片
- 只在一个场景中使用的装饰资源

这样可以减少 Resources 启动扫描和运行时内存常驻，也能让引用关系在 Inspector 中可见。

## Yoki/UIKit 面板

通用面板由 Yoki/UIKit 按项目现有配置加载。面板 Prefab 继续放在：

```text
Assets/Resources/Art/UIPrefab/
```

不要为了“统一目录”把这些 Prefab 移出 Resources，除非同时改造 Yoki 的面板加载配置并完成完整回归测试。

## Luban 配表

Luban 的源数据在：

```text
Assets/ConfigTables/Data/
```

生成结果在：

```text
Assets/Scripts/Config/Generated/
Assets/Settings/Resources/Luban/
```

Excel 是唯一编辑来源。不要手改生成 C# 或 JSON。修改表后运行：

```text
Assets/ConfigTables/GenerateAll.bat
```

并提交源表、生成代码和生成 JSON 的一致变更。

## 后续迁移

如果项目进入正式生产阶段，再评估 Addressables。迁移应单独进行，先建立统一的 `IAssetProvider`，再逐模块替换加载方式；不要在本轮合并中混合进行。
