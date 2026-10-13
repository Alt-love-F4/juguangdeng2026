# 字体资源与 Git 协作

## 目的与边界

保留源字体、TMP 字体资产和 `.meta`，避免其他成员重新克隆后缺少字体或 GUID 引用失效。不要在 `.gitignore` 中忽略字体目录，也不要使用 `merge=ours` 自动吞掉另一方的修改。

项目基准 Unity 版本为 `6000.3.5f2`，以 `ProjectSettings/ProjectVersion.txt` 为准。建议团队统一版本。

动态 TMP 字体会按显示的文字生成字形、字符表和 Atlas，这些数据可能写回受 Git 追踪的 `.asset`。它们与字体配置、材质引用存在于同一个文件里，不能通过 `.gitignore` 只忽略其中几行。

`.gitattributes` 统一 Unity 文本资源的 LF 换行，并将源字体按二进制处理。`.asset` 使用 `text=auto`，避免把二进制资产强行当成文本。规则能减少换行差异，但不能消除真正的字体配置或缓存内容冲突。

本次不做全仓库 `git add --renormalize .`，也不重建字体或改动已有 GUID；已有文件在后续检出时使用新的换行规则。

## 每个成员启用一次

在仓库根目录的 PowerShell 执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Git\Install-GitHooks.ps1
```

安装器只修改当前仓库的本地配置：

- `core.hooksPath=.githooks`：启用随仓库提交的 `pre-commit` 检查。
- `merge.renormalize=true`：帮助合并旧换行规则与新规则下的文本文件。

安装器不会替换已有自定义 hooks。若提示已有 hooks，请在原来的 `pre-commit` 中调用 `sh Tools/Git/check-font-assets.sh`，保留原检查。

Git 不会因克隆仓库自动启用 hooks，所以每个成员、每个新 checkout 都需要执行安装步骤。Git for Windows 提供 hook 使用的 `sh`，不需要另装 Python 或 Node。

## 普通功能提交

检查只读取暂存区，检测以下目录的 `.asset` 新增、修改、删除和重命名：

- `Assets/Fonts/`
- `Assets/Resources/Fonts/`（包括 `Map/` 等子目录）
- `Assets/TextMesh Pro/Resources/Fonts & Materials/`

检测到字体资产变化会拒绝提交，显示受影响文件；不会恢复、删除文件或自动取消暂存。它不尝试猜测修改是否只是缓存，也不会拦截未暂存的字体变化。

若你没有主动修改字体，先查看具体文件的暂存差异。例如：

```powershell
git diff --cached -- 'Assets/Resources/Fonts/SimHei SDF.asset'
```

确认该文件只是 Unity 自动生成缓存后，可以只取消它的暂存，其他功能照常提交：

```powershell
git restore --staged -- 'Assets/Resources/Fonts/SimHei SDF.asset'
```

此操作保留本地字体修改。若确实要丢弃缓存，先关闭 Unity 并确认没有需要保留的字体配置修改，再单独恢复这个文件：

```powershell
git restore -- 'Assets/Resources/Fonts/SimHei SDF.asset'
```

不要恢复整个字体目录，那会同时丢弃源字体、新资源或 `.meta` 的真实修改。

## 有意修改字体或已审核的合并

新增、更换字体、修改 fallback、材质或字体参数时，单独提交字体变更，保留对应源字体和 `.meta`。不要为了减少冲突随意清空静态字体的字形，也不要把动态字体改成没有项目源字体引用的配置。

审核暂存区后，在 PowerShell 显式允许本次提交：

```powershell
$previousFontOverride = $env:UNITY_ALLOW_FONT_ASSET_CHANGES
try {
    $env:UNITY_ALLOW_FONT_ASSET_CHANGES = '1'
    git commit -m '更新字体资源'
    if ($LASTEXITCODE -ne 0) {
        throw '字体提交失败，请检查 Git 输出。'
    }
}
finally {
    $env:UNITY_ALLOW_FONT_ASSET_CHANGES = $previousFontOverride
}
```

解决字体冲突后的合并提交也需要审核并按此方式放行。不要长期设置放行变量，也不要用 `--no-verify` 跳过所有检查。此检查用于防误操作，不是不可绕过的服务器权限策略。

仍有字体内容冲突时，不能逐行随意拼接 Atlas 与字形表；应确认双方配置和引用，选取一致的完整资源或在统一 Unity 版本里重新生成，然后验证中文显示。

## 回归验证

变更字体后，在已有 `Assets/Scenes/PresentationDebugScene.unity` 中检查信件、对话和通知的中文显示，不另建测试场景。动态缓存再生成是正常行为，普通功能提交不要把这些缓存一并上传。

本方案防止意外缓存进入提交并减少换行噪音，不承诺消除真实的多人字体配置冲突。
