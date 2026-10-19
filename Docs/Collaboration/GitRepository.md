# Git 仓库与团队同步

自 2026-10-19 起，主仓库为 `https://github.com/Alt-love-F4/juguangdeng2026.git`，协作主分支为 `main`。旧 Gitee 远端保留历史，后续默认上传 GitHub。

## 新克隆

```powershell
git clone https://github.com/Alt-love-F4/juguangdeng2026.git
Set-Location juguangdeng2026
git switch main
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Git\Install-GitHooks.ps1
```

## 已有工作副本

先检查当前工作区和远端，保存本地工作后再切换、合并分支。不要覆盖本地未提交改动。

```powershell
git status --short
git remote -v
git branch -vv
```

如果 `origin` 仍是旧 Gitee 地址，在没有同名远端时先保留为 `gitee-legacy`，再将 GitHub 配置为 `origin`；如果已经配置了 `github` 远端且 URL 正确，可将其重命名为 `origin`。不要重复添加已有远端。

配置完成后确认 `git remote get-url origin` 为上述 GitHub 地址，再执行：

```powershell
git fetch origin
git branch --set-upstream-to=origin/main main
git config --local remote.pushDefault origin
git config --local branch.main.pushRemote origin
git config --local push.default simple
```

在 `main` 上、工作区状态允许时使用 `git pull --ff-only origin main` 获取进度。若发生分叉，先检查双方提交并合并；不要强制推送或用 hard reset 覆盖工作。

上传当前进度前审核暂存区，排除所有 `.psd`、`.psd.meta`，保留本地文件。字体缓存处理见 `FontAssetGitWorkflow.md`。GitHub 迁移保留已有提交历史，不借迁移重写历史或清理旧素材。

## 给 agent 的同步指令

> 先核对仓库 URL 为 https://github.com/Alt-love-F4/juguangdeng2026.git，目标分支为 main。检查工作区、远端和分支跟踪，再 fetch origin；保留本地修改及远端已有提交，必要时合并。排除 PSD、PSD.meta 和确认仅为自动缓存的字体资产，正常运行提交 hooks。推送到 origin/main 后，用远端 main 的实际哈希核对结果。
