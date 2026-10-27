# sushi and fishin

完整可编辑 Unity 工程快照，包含已经合并并通过实际主工程验收的住宅与地图更新 **ResidentialIsland 1.0.2**，以及沿岸墙体、台阶和平台实体基础修复。现已加入海钓系统、两座可驾船前往的小岛、鱼仓与图鉴、53道鱼种专属料理及运行时中文名称修复。

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

## 海钓与料理更新（2026-10-27）

- 60种新增钓获物、111种原色/异色外观，接入原有图鉴与物品系统。
- 抛竿、咬钩、收线上岸、岸上跳动与攻击、射击捕获、无限鱼仓和原商店老板收购。
- 两座可驾船到达的小岛及不同鱼池，岛屿平地、植被与航行/游泳通路修复。
- 钓鱼期间随机天气、海鸥与海马事件；每个存档记录角色位置和视角。
- 正常外形原色钓获物各一道专属菜，新增53道，保留原42道寿司，共95道；沿用原切鱼、备料、厨台、菜单及传送带流程。
- 异色和收藏钓获物可手持面对商店老板Gabe，按原左键卖鱼提示出售。
- 修复实际旧语言包缺少新增条目时的名称显示，图鉴、菜单、配方书及食材使用已有中文名。

完整操作与修改记录见 [FISHING_MIGRATION.md](FISHING_MIGRATION.md)，菜谱及售价见 [海钓菜谱](Assets/SushiAndFish/FishingIntegration/Cuisine/CuisineGuide.md)。进入游戏使用原有主场景 `Assets/Scenes/Main.unity`；`Assets/HowToFish` 与 `Assets/SushiAndFish/FishingIntegration` 已包含在工程中。

近期海钓/料理更新经过Unity编译、静态资产/配方/语言包查询检查；没有为这些更新进入Play或模拟游戏，交互效果由项目使用者运行验证。下方文档同时保留较早住宅模块的历史验收记录。

验证范围和结果见 [Documentation/VALIDATION.md](Documentation/VALIDATION.md)。

## 资源与权利说明

工程包含从用户提供的 Deep Blue Sushi v0.3.6 游戏文件恢复的代码和资源，以及集成资源。现有第三方版权与许可文件原样保留。本仓库未添加覆盖整个项目的开源许可证；公开可见不表示对第三方内容授予额外的再分发或商业使用许可。

GitHub 普通文件存在大小限制，因此较大资源通过 Git LFS 保存；请确保已下载 LFS 内容再打开工程。
