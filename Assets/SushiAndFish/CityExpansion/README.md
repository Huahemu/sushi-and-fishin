# 城市扩建 2026-10-21

当前功能与验证见 `F:\虚拟现实\MyProject\Sushi & Fish\reports\城市扩建与电梯对话验证-20261021.md`。

Models/ 为 ArtSource/JapaneseTown_20261020 的导入副本；Prefabs/ 为三座建筑与原生弹窗模板；Materials/ 为 GLB PBR 定义转换；RoadMeshes/ 为东南避让护岸的道路改段；TownNavigation_20261021.asset 为已保存导航。

Runtime/ 包含独立自动门、影院入口、原生座位桥接、可中断 NPC 用餐状态、持久用餐调度以及地图地点。所有餐厅椅子为8餐椅+4吧凳。原归档装饰/结构预制体不在此复制改写；海岸设施仅实例化原库资源。

编辑 GameState.unity/CityBuildings_20261021。12个新增场景网络对象已经由持久GlobalObjectId生成唯一非零标识；若主动重建世界并重新创建座位，必须重新执行 AssignAndVerifyNetworkHashes 并运行完整核验。不要把诊断Probe的PASS当作交付验收。

模型、影院内部和门扇的原始源文件只读。当前本地工程为实际运行代码的权威版本；scripts/ 下是准备/核验工具和同步副本。
