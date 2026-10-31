# sushi and fishin

完整可编辑 Unity 工程快照，包含已经合并并通过实际主工程验收的住宅与地图更新 **ResidentialIsland 1.0.2**，以及沿岸墙体、台阶和平台实体基础修复。

## 打开工程

需要 **Unity 6000.3.6f1**、Git 与 Git LFS。

```sh
git lfs install
git clone https://github.com/Huahemu/sushi-and-fishin.git
cd sushi-and-fishin
git lfs pull
```

用 Unity Hub 将此仓库根目录添加为工程，等待导入后打开 `Assets/Scenes/Main.unity`。`Assets`、`Packages`、`ProjectSettings` 与全部资源元数据均已包含。Unity 缓存、日志、本机设置和 AssetRipper 诊断导出由 Unity 或本机工具重新生成，不参与版本管理。

## 已合并功能

- 原有岛屿玩法与当前场景、赌场、餐厅和影院模块。
- 沿岸七类结构的实体基础与碰撞，保留原构件顶面和位置。
- Tab 平板地图中的住宅地块选择、透明预览、WASD 四向、左键确认和右键/Esc 取消。
- 统一尺寸并居中的九页建筑楼层俯视图、信息窗口翻页及拖动关闭。
- 住宅门、原生座椅、第三人称床和马桶动作，以及马桶翻盖。
- 建房时安置占位 NPC、住宅禁入、原生保存返回读取后的住宅恢复。

验证范围和结果见 [Documentation/VALIDATION.md](Documentation/VALIDATION.md)。

## 资源与权利说明

工程包含从用户提供的 Deep Blue Sushi v0.3.6 游戏文件恢复的代码和资源，以及集成资源。现有第三方版权与许可文件原样保留。本仓库未添加覆盖整个项目的开源许可证；公开可见不表示对第三方内容授予额外的再分发或商业使用许可。

GitHub 普通文件存在大小限制，因此较大资源通过 Git LFS 保存；请确保已下载 LFS 内容再打开工程。
