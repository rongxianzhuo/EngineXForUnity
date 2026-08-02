# EngineXForUnity 适配层系统设计

> 版本：v0.1（探索期设计稿）
> 状态：草稿，待评审

## 1. 背景与目标

### 1.1 背景

EngineX 是引擎无关的游戏框架。游戏本体（`IGame` 实现）由用户/AI 编写，只使用 ECS 通用组件与 System，**不包含任何渲染逻辑、不依赖任何具体引擎**。市面引擎过重、容器内跑不动，因此 EngineX 本体必须在无引擎环境下可运行、可测试。

EngineXForUnity 是 EngineX 在 Unity 上的**适配层**：负责加载并运行 `IGame` 游戏本体，把 ECS 数据翻译为 Unity 的真实渲染。

### 1.2 目标

- **通用性**：任意 `IGame` 游戏本体都能被加载、运行、渲染，不绑定特定游戏
- **数据驱动**：游戏侧"声明"（组件数据），适配层"实现"（渲染行为）
- **可扩展**：新的渲染能力（相机、光源、UI、输入）以"组件 + 适配系统"方式增量加入
- **性能**：以 GPU Instancing 合批为主路径，热路径零 GC、零每帧分配
- **健壮**：资源缺失、非法数据（零四元数、NaN）、游戏逻辑异常均不导致 Unity 崩溃

### 1.3 非目标

- 不参与游戏逻辑开发
- 不做美术/关卡编辑器工具（后续可考虑 Inspector 装配辅助）
- 不做多引擎适配（那是兄弟项目，但接口设计需为其留空间）

## 2. 总体架构

```
┌────────────────────── 游戏侧（用户/AI，不依赖引擎）──────────────────────┐
│  IGame（Create / Update / Destroy）                                      │
│  组件：TransformData / RenderData / …（通用数据，无引擎对象引用）          │
│  System：游戏自有逻辑（可并行 Job，由游戏自己调度与同步）                    │
└───────────────────────────────┬──────────────────────────────────────────┘
                                │ 契约：同 World 上的组件数据
┌───────────────────────────────┴──────────────────────────────────────────┐
│  Unity 适配层（EngineXForUnity，本设计的主体）                             │
│                                                                            │
│  ┌──────────────┐   ┌──────────────────┐   ┌──────────────────────────┐  │
│  │ GameInstance │   │ RenderPipeline   │   │ ResourceSystem           │  │
│  │ · 生命周期     │ → │ · 收集/合批/绘制  │ → │ · IResourceLoader 抽象    │  │
│  │ · 更新循环     │   │ · 实例缓冲复用    │   │ · 缓存/缺失兜底/热重载     │  │
│  └──────────────┘   └──────────────────┘   └──────────────────────────┘  │
│  ┌────────────────────────────────────────────────────────────────────┐  │
│  │ ComponentAdapters：CameraAdapter / InputAdapter / …（未来扩展）     │  │
│  └────────────────────────────────────────────────────────────────────┘  │
└───────────────────────────────┬──────────────────────────────────────────┘
                                │ Unity 渲染 API（Graphics / GameObject / …）
                        ┌───────┴───────┐
                        │  Unity 引擎    │
                        └───────────────┘
```

### 2.1 核心原则

1. **数据声明与引擎实现分离**：游戏侧组件只含数据（`RenderData` 是资源路径字符串，不是 Material 引用）；适配层负责把数据解析为引擎对象
2. **单向依赖**：游戏侧 ← 适配层（适配层读游戏数据，游戏不知道适配层存在）
3. **组件驱动渲染**：每一个"游戏侧组件 → 引擎表现"的映射由一个独立适配系统负责（TransformData+RenderData → 实例化绘制；未来 CameraData → Unity Camera 同步……）

## 3. 模块设计

### 3.1 GameInstance（游戏实例与生命周期）

`UnityAdapter`（MonoBehaviour，场景入口）与 `GameInstance`（纯 C# 核心）分离：

- `UnityAdapter`：只做 Unity 生命周期桥接（Awake/OnDestroy/域重载），不含逻辑
- `GameInstance`：
  - 持有 `IGame`、共享 `World`、模拟 Group（游戏自建）、渲染 Group（适配层自建）
  - `Initialize(IGame)`：`world = game.Create()`；构建渲染 Group 并 `Create(world)`
  - `Simulate()`：`game.Update()`（游戏调度自己的 System/Job）
  - `Render()`：`renderGroup.Update(world)`（适配层只读渲染）
  - `Shutdown()`：先渲染组销毁，再 `game.Destroy()`，最后释放 World

**域重载**：play 模式进入/退出时的资源清理；Editor 下退出 Play 必须完整 Dispose（NativeArray 泄漏检测）。

### 3.2 更新循环与同步纪律

```
Unity FixedUpdate ──→ GameInstance.Simulate()  （游戏 Job 调度 + 当场 Complete）
Unity Update      ──→ GameInstance.Render()    （适配层渲染系统，只读组件）
```

同步纪律（硬性规则）：
1. `IGame.Update()` 返回时，**游戏侧所有 Job 必须已完成**（或游戏明确声明"本帧数据已稳定"）—— 适配层渲染读取时不允许存在并发写入
2. 适配层系统只读游戏组件（`GetComponentRef` 读，不写）；需要写缓冲时只写自己持有的 NativeArray
3. 渲染 API（Graphics 调用）只能在主线程、渲染系统 OnUpdate 内执行

> 演进：若未来需要模拟插值（固定步长 → 显示帧率），在组件层加 prev/current 快照，由适配层插值，游戏侧无感知。

### 3.3 渲染管线（RenderPipeline）

目标形态（M2 落地）：

```
[ECS 侧] RenderCollectSystem（可并行 Job）
   查询 TransformData + RenderData
   → 写入复用式 NativeArray<RenderInstance>（Pos/Rot/Scale/MeshId/MaterialId）
                    ↓ 帧末同步（Complete）
[Unity 侧] RenderDrawStage（主线程）
   按 (Mesh, Material) 分组合批
   → Graphics.RenderMeshInstanced 分批绘制（≤1023/批）
```

- **收集与绘制分离**：收集可进 Job（并行、低开销），绘制必须主线程。两者通过**复用式实例缓冲**解耦
- **实例缓冲**：`NativeArray<RenderInstance>` 按需扩容、跨帧复用（零每帧分配）；实例数量变化时仅扩容
- **合批**：以 (Mesh, Material) 为键分组；组内超 1023 自动切批
- **材质要求**：加载时自动 `enableInstancing = true`
- 当前 demo 的单系统实现（收集+绘制同在主线程）作为 M1 保留，M2 升级为分离式

### 3.4 资源系统（ResourceSystem）

- `IResourceLoader`：资源加载抽象，适配层渲染系统只依赖此接口
  ```csharp
  public interface IResourceLoader
  {
      Mesh LoadMesh(string resourcePath);
      Material LoadMaterial(string resourcePath);
  }
  ```
- 实现：
  - `DummyResourceLoader`：开发期占位（Resources 直读，**需补 null 防御**）
  - `UnityResourceLoader`：正式 Resources 实现（M2：支持 prefab 取网格、缺失返回 null）
  - 未来：Addressables / AssetBundle 实现（异步接口另行设计）
- **缓存策略**：`path → 资源` 字典；**加载失败不缓存**（允许运行中补资源后重载）
- **缺失兜底**：缺失资源 → Debug.LogWarning（每路径只告警一次）+ 跳过该实例；可选默认材质兜底
- 渲染系统持有资源缓存的唯一所有权；资源卸载在 GameInstance.Shutdown 时统一处理

### 3.5 组件契约与扩展

当前契约组件（来自 EngineX 核心）：

| 组件 | 内容 | 适配层消费方 |
|---|---|---|
| `TransformData` | Position/Rotation/Scale（FP） | 渲染系统（转 Unity Matrix4x4） |
| `RenderData` | MeshPath / MaterialPath | 资源系统 + 渲染系统 |

扩展路径（每项 = 一个组件 + 一个适配系统，可独立开发）：

| 未来组件 | 数据示例 | 适配系统职责 |
|---|---|---|
| `CameraData` | FOV / 位置 / 朝向 | 同步到 Unity Camera（或创建相机） |
| `LightData` | 类型 / 颜色 / 强度 | 创建/同步 Unity Light |
| `InputData` | 轴值 / 按键状态 | 适配层把 Unity 输入写入 ECS，游戏侧读取 |
| `UiData` | 文本 / 布局 / 图集路径 | 生成/更新 UGUI 元素 |

注册机制：`GameInstance` 维护 `List<IAdapterSystem>`，按组件契约自动装配（M2 起，先硬编码枚举即可）。

### 3.6 健壮性

- **数据防御**（渲染边界）：
  - 零四元数 `(0,0,0,0)` → 按 Identity 处理（默认构造陷阱）
  - 非单位四元数 → 归一化后再进 `Matrix4x4.TRS`
  - Scale/Position 异常值（NaN/Inf）→ 跳过实例 + 告警
- **资源防御**：任何 Loader 返回 null 不得抛异常；渲染系统跳过该实例
- **游戏异常隔离**：Editor 下对 `IGame.Update()` 包 try/catch，异常打日志并暂停模拟（不崩 Unity）；Release 下可配置容错策略

### 3.7 性能策略

| 项 | 策略 |
|---|---|
| 每帧分配 | 渲染管线全链路复用缓冲（chunk 数组、实例缓冲、合批缓冲）；禁止每帧 new List/Dictionary（M2 整改） |
| 合批 | (Mesh, Material) 分组 + 1023 上限切批 |
| 资源 | 路径缓存，加载一次 |
| 数据转换 | FP→float 仅发生在绘制边界；转换辅助内联 |
| 收集 | 并行 Job 收集（M2），主线程只做绘制 |

### 3.8 相机与场景（M1 简易策略）

- 适配层提供默认相机（场景中手动放置 Camera + UnityAdapter）
- 渲染边界盒：`RenderParams.worldBounds` 覆盖游戏世界范围（当前固定 1000；M2 改为按实例集合动态计算）

### 3.9 输入桥接（M2+）

- 适配层轮询 Unity 输入 → 写入 `InputData` 组件（挂到游戏实体）→ 游戏 System 读取
- 设计约束：输入采样必须在模拟之前（FixedUpdate 顺序），保证模拟读到一致输入

## 4. 代码目录结构（M2 目标）

```
Script/Runtime/
├── Adapter/
│   ├── UnityAdapter.cs        # MonoBehaviour 入口（生命周期桥接）
│   └── GameInstance.cs        # 游戏实例：World/双 Group/时序/清理
├── Rendering/
│   ├── RenderCollectSystem.cs # ECS 侧收集（可并行）
│   ├── RenderDrawStage.cs     # 主线程绘制（合批 + 实例化）
│   └── RenderInstance.cs      # 实例缓冲结构
├── Resources/
│   ├── IResourceLoader.cs     # 加载抽象
│   ├── UnityResourceLoader.cs # 正式实现
│   └── DummyResourceLoader.cs # 开发占位
├── Adapters/                  # 组件→引擎 适配系统（Camera/Light/Input…，逐个新增）
└── Core/
    └── AdapterSystem.cs       # 适配系统基类/接口
```

## 5. 里程碑

| 里程碑 | 内容 | 状态 |
|---|---|---|
| M1 | 基础跑通：IGame + TransformData/RenderData + 实例化渲染（当前 demo） | ✅ 已达成 |
| M2 | 正式化：目录重构、收集/绘制分离、热路径零 GC、资源系统正式化（null 防御、失败不缓存）、默认相机 | 待开发 |
| M3 | 扩展契约：CameraData / InputData；用第二个游戏本体验证适配层通用性 | 待开发 |
| M4 | 编辑器辅助：一键装配 UnityAdapter 的 Inspector 工具；资源校验 | 待开发 |

## 6. 开放问题（需决策）

1. **模拟步长**：固定步长（当前 1/50）由游戏决定还是适配层配置？是否支持插值渲染？
2. **资源标识**：路径字符串（当前）vs 资源 ID（Addressables 友好）？是否保留字符串作为"最低公共协议"？
3. **相机声明**：游戏侧 `CameraData` 组件 vs 适配层默认相机 + 游戏覆盖？
4. **多实例**：一个场景多个 `IGame`（多世界）是否需要？当前按单实例设计。
5. **异常策略**：游戏逻辑抛异常时，Editor 下"暂停并提示"vs"跳过一帧继续"？
