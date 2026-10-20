# 对话立绘尺寸配表

配表：`Assets/Resources/Dialogue/DialoguePortraitSizing.asset`。

- `heightRatio`：所有立绘的默认可见高度 / 立绘框高度，初始 0.88。
- `entries`：按 `characterId` 与 `sprite` 匹配。普通对话使用原角色 ID；巡览的室内对照使用 `角色ID:indoor`。同一图片未匹配 ID 时回退至该图片的首条配置。
- `heightScale`：单张立绘的身高倍率，初始全部为 1；1.05 表示增加 5%。
- `offset`：归一化后的 UI 本地像素偏移，X 向右、Y 向上。
- `visibleRect`：从 PNG 非透明像素烘焙的归一化边界，一般不要手改。

只改变对话框的绘制网格，不改原图、Sprite 导入设置或咖啡馆场景人物。图片保持比例，允许宽度超出原有窄框；宽体型角色在多人同屏时可能需要美术检查重叠。

更新图片或角色表后，退出运行模式，执行 `Tools > Game Flow > Refresh Dialogue Portrait Sizing`。该命令添加缺少的配置并刷新透明边界，保留已有倍率和偏移。修改配表后重新启动巡览查看效果。

在 `Assets/Scenes/PresentationDebugScene.unity` 运行后，点击“全人物立绘／直接巡览”或按 D。每页一个角色，左侧对话立绘，右侧独立室内形象；缺图角色仍保留页面并显示诊断。当前角色表包含 75 个非 world 对象，其中 player 尚无立绘，因此显示缺图诊断。

默认统一的是非透明轮廓总高度，不是眼睛线或头部大小；坐姿、帽子、武器和不同构图仍可通过独立倍率与偏移进行美术微调。说话聚焦与进退场仍保留短暂的演出缩放。
