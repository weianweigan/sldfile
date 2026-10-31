# SolidWorks 特征数据逆向工程任务跟踪看板

> 本文件维护 SolidWorks 各工作台特征数据在 `SldFile` 库中的纯 C# 逆向解码与实现闭环进度。  
> 逆向工程规范依据：[.agents/skills/solidworks-feature-reverse/SKILL.md](.agents/skills/solidworks-feature-reverse/SKILL.md)  
> 测试样本资产目录：`SldFileTests/TestFiles/Features2026/`

---

## 逆向工程三位一体验收准则 (Acceptance Criteria)

每个特征任务的最终闭环必须同时满足以下三项核心能力指标：

1. **全量 FeatureData 属性解析 (Full API Alignment)**：
   - 不仅涵盖主几何尺寸（如深度、厚度、角度），还必须 100% 映射对齐官方 API 属性（如双向拔模、起始偏移、薄壁类型、K-Factor 算法、释放槽、合并实体与作用范围开关等）。
2. **完整 Selection 选择项逆向 (Selection & Topology Graph)**：
   - 彻底逆向底层拓扑映射对象：父草图与所选轮廓 (`moFromSktEntSurfIdRep_c`, `moSketchChain_c`, `moSketchRegion_c`)、拉伸/成形方向向量 (`moEdgeRef_c`, `moCompRefPlane_c`)、终止目标实体 (`moFaceRef_c`, `moEndFaceSurfIdRep_c`, `moCompFace_c`)、多实体作用范围 (`moPerBodyChooserData_c`, `moCompSolidBody_c`)。
   - 将底层持久句柄转化为上层强类型 Selection 属性。
3. **纯 C# 代码创建与特征注入 (Feature Authoring & Injection)**：
   - 逆向成果能够支持**使用纯 C# 代码像调用原生 API 一样创建新特征并注入到文件**。
   - 自动组装 MFC FolderHeader、类标签原语与子对象字节流，更新 `Contents/Config-0-ResolvedFeatures` 流。
   - 生成的 `.SLDPRT` / `.SLDASM` 经原生 SolidWorks 打开并执行 `ForceRebuild3` 能够无错误、无警告正常重建，且能被 `SldFile` 自身完整读取。

---

## 总体进度仪表盘

| 阶段 (Phase) | 工作台 / 领域 | 任务数 | 基础属性闭环 | Selection 逆向 | 代码创建注入 | 状态 |
|:---|:---|:---:|:---:|:---:|:---:|:---:|
| **Phase 1** | 核心实体基元工作台 (Solid Primitives) | 5 | 1 | 1 | 1 | 🟡 进行中 |
| **Phase 2** | 钣金工作台 (Sheet Metal) | 4 | 1 | 0 | 0 | 🟡 进行中 |
| **Phase 3** | 高级实体与多实体操作 (Advanced Body) | 3 | 0 | 0 | 0 | ⚪ 待启动 |
| **Phase 4** | 曲面工作台 (Surface Modeling) | 3 | 0 | 0 | 0 | ⚪ 待启动 |
| **Phase 5** | 焊件与结构系统 (Weldments & Structure) | 3 | 0 | 0 | 0 | ⚪ 待启动 |
| **Phase 6** | 装配体配合与引用系统 (Assembly Mates) | 2 | 0 | 0 | 0 | ⚪ 待启动 |
| **总计** | **全工作台特征逆向闭环** | **20** | **2** | **1** | **1** | 🚀 **推进中** |

---

## 阶段任务跟踪清单

### Phase 1: 核心实体基元工作台 (Solid Primitives)

- [x] **Task 1.1: 基础拉伸族 (`Extrusion`, `Boss`, `Cut`, `BaseBody`)**
  - **API 接口**: `IExtrudeFeatureData2` | **MFC 类**: `moExtrusion_c` (`0x4B`), `moExtrusion_ct`, `moICE_c` (切除)
  - **样本资产**: ✅ 已加入 `SldFileTests/TestFiles/Features2026/` (`extrude_10mm`, `20mm`, `30mm`, `midplane`, `cut_5mm`)
  - **全量 API 属性与数据模型**:
    - [x] 核心深度与终止条件 (`Depth`, `DepthDirection2`, `EndCondition`, `BothDirections`, `ReverseDirection`)
    - [x] 拔模选项 (`DraftAngle`, `DraftAngleDirection2`, `DraftOutward`, `DraftOutwardDirection2`)
    - [x] 起始条件 (`FromType`, `FromOffsetDistance`, `FromOffsetReverse`)
    - [x] 实体合并与范围 (`Merge`, `FeatureScope`, `AutoSelect`, `AssemblyFeatureScope`)
    - [x] 切除与薄壁特有 (`IsCut`, `FlipSideToCut`, `NormalCut`, `IsThinFeature`, `ThinWallType`, `WallThickness`, `CapEnds`)
    - [x] 通用特征管理层绑定 (`IsSuppressed`, `IsVisible`, `Definition` 强类型映射)
  - **Selection 选择项逆向**:
    - [x] 父草图引用句柄解码（`moFromSktEntSurfIdRep_c` 解码父级 `moProfileFeature_c` 的 TagID 与关联索引）
    - [x] 所选轮廓与区域解码（`moSketchChain_c` 闭环链与 `moSketchRegion_c` 区域索引映射，$139 + 40N$ 动态变长公式）
    - [x] 终止条件实体引用解码（`moFaceRef_c` 拓扑路径、`moEndFaceSurfIdRep_c`、`moCompFace_c` 到顶点/面的关联）
    - [x] 拉伸方向向量引用解码（`moEdgeRef_c` 边线参考、`moCompRefPlane_c` 基准面参考）
    - [x] 多实体范围选择解码（`moPerBodyChooserData_c` 策略器与 `moCompSolidBody_c` 实体句柄列表）
  - **代码创建与特征注入 (Feature Authoring)**:
    - [x] 封装纯 C# 创建拉伸特征入口（`part.AddExtrude(ExtrudeFeatureDefinitionData)` 与快捷重载）
    - [x] 纯程序化分段发射器（`MfcExtrudeSerializer.cs` 与 `ExtrudeSegments.cs`，彻底铲除静态 byte 数组与 Base64 模板）
    - [x] 注入 `ResolvedFeatures` 流并由原生 SolidWorks 打开调用 `ForceRebuild3` 重建验证（18/18 原生测试 100% 成功，物理体积精准匹配）

- [ ] **Task 1.2: 旋转族 (`Revolution`, `RevCut`)**
  - **API 接口**: `IRevolveFeatureData2` | **已知 MFC 类**: `moRevolution_c` (`0x4E`)
  - **样本资产**: ⚠️ 已有 `revolve_360.SLDPRT`，待制备 90°、180° 与旋转切除单变量样本
  - **全量 API 属性与数据模型**:
    - [ ] 旋转角度解码（`Angle`, `AngleDirection2`，IEEE 754 弧度/角度）
    - [ ] 终止条件与双向旋转（`Type`, `ReverseDirection`, `BothDirections`）
    - [ ] 实体合并与薄壁（`Merge`, `IsThinFeature`, `Thickness1`, `Thickness2`）
  - **Selection 选择项逆向**:
    - [ ] 旋转轴实体引用解码（中心线、基准轴、模型边线 `moEdgeRef_c`）
    - [ ] 所选草图与轮廓解码（`moFromSktEntSurfIdRep_c`, `moSketchChain_c`）
    - [ ] 终止目标实体引用解码（`moFaceRef_c` 成形到面/顶点）
  - **代码创建与特征注入 (Feature Authoring)**:
    - [ ] 构造 `moRevolution_c` 序列化模板并注入特征流
    - [ ] 原生 SolidWorks 重建验证

- [ ] **Task 1.3: 圆角与倒角 (`Fillet`, `Chamfer`)**
  - **API 接口**: `ISimpleFilletFeatureData2`, `IChamferFeatureData2` | **已知 MFC 类**: `moFillet_c` (`0x4C`), `moChamfer_c` (`0x4D`)
  - **样本资产**: ⚠️ 已有 `fillet_2mm.SLDPRT`，待制备 `fillet_5mm` 与倒角（距离-角度、距离-距离）样本
  - **全量 API 属性与数据模型**:
    - [ ] 圆角参数（`Radius`, `ConicRho`, `FilletType` 等厚/变半径/面圆角/完整圆角）
    - [ ] 倒角参数（`Distance`, `Angle`, `ChamferType`）
  - **Selection 选择项逆向**:
    - [ ] 被施加圆角的边线拓扑句柄解码（`moEdgeRef_c` 集合列表）
    - [ ] 被施加圆角/倒角的目标面拓扑句柄解码（`moFaceRef_c`）
    - [ ] 保留特征与拐角选取
  - **代码创建与特征注入 (Feature Authoring)**:
    - [ ] 构造 `moFillet_c` / `moChamfer_c` 节点并注入流
    - [ ] 原生 SolidWorks 重建验证

- [ ] **Task 1.4: 异型孔向导与简单孔 (`HoleWzd`, `AdvHoleWzd`)**
  - **API 接口**: `IWizardHoleFeatureData2` (`moHoleWizard_c`, `0x51`), `IAdvancedHoleFeatureData` (`0x52`)
  - **样本资产**: 待制备（螺纹孔、沉头孔、锥孔单变量样本）
  - **全量 API 属性与数据模型**:
    - [ ] 孔类型标准（ANSI / ISO / GB / DIN）
    - [ ] 钻孔直径、孔深、沉头直径与深度、螺纹规格与公差
  - **Selection 选择项逆向**:
    - [ ] 孔定位基准面与草图点集合解码（`moFaceRef_c`, `sgPointHandle`）
    - [ ] 终止条件实体引用（成形到下一面/面）
  - **代码创建与特征注入**:
    - [ ] 构造孔特征并注入流，原生重建验证

- [ ] **Task 1.5: 基础阵列与镜像 (`LPattern`, `CirPattern`, `MirrorPattern`)**
  - **API 接口**: `ILinearPatternFeatureData` (`0x54`), `ICircularPatternFeatureData` (`0x53`), `IMirrorPatternFeatureData` (`0x55`)
  - **样本资产**: 待制备（线性阵列间距/数量、圆周阵列角度/数量、镜像基准面）
  - **全量 API 属性与数据模型**:
    - [ ] 阵列实例数量、间距、角度、几何阵列标志
  - **Selection 选择项逆向**:
    - [ ] 方向 1 / 2 参考实体（边线、基准轴、线性草图尺寸）
    - [ ] 镜像基准面选择项（`moRefPlane_c` / `moFaceRef_c`）
    - [ ] 源特征/源实体引用列表（`moCompFeature_c`, `moCompSolidBody_c`）
  - **代码创建与特征注入**:
    - [ ] 构造阵列/镜像节点并注入流，原生重建验证

---

### Phase 2: 钣金工作台 (Sheet Metal)

- [ ] **Task 2.1: 基体法兰与全局钣金参数 (`SMBaseFlange`, `SheetMetal`)**
  - **API 接口**: `IBaseFlangeFeatureData` | **MFC 类**: `moSMBaseFlange_c` (`0x5E`), `moTemplateSheetMetal_c`
  - **样本资产**: ✅ 已加入 `SldFileTests/TestFiles/Features2026/` (`sm_base_flange_10mm`, `20mm`, `thick2`, `rad3`)
  - **全量 API 属性与数据模型**:
    - [x] 核心板厚、折弯半径、拉伸深度 (`Thickness`, `BendRadius`, `Depth`, `DepthDirection2`, `BothDirections`)
    - [x] 折弯余量与计算 (`KFactor`, `BendAllowanceType`)
    - [x] 自动释放槽参数 (`AutoReliefType`, `ReliefRatio`, `ReliefWidth`, `ReliefDepth`, `UseDefaultBendRelief`)
    - [x] 钣金规格表选项 (`UseGaugeTable`)
    - [x] 通用特征管理层绑定 (`IsSuppressed`, `IsVisible`, `Definition` 强类型映射)
  - **Selection 选择项逆向**:
    - [ ] 基体法兰父草图与所选闭合回路解码（`moFromSktEntSurfIdRep_c`, `moSketchChain_c`）
    - [ ] 厚度方向基准面与翻转参考解码
  - **代码创建与特征注入 (Feature Authoring)**:
    - [ ] 封装纯 C# 基体法兰创建入口（`file.Features.AddSheetMetalBaseFlange(...)`）
    - [ ] 动态组装 `moSMBaseFlange_c`、`moTemplateSheetMetal_c` 及全局钣金参数
    - [ ] 原生 SolidWorks 重建验证

- [ ] **Task 2.2: 边线法兰与斜接法兰 (`EdgeFlange`, `SMMiteredFlange`)**
  - **API 接口**: `IEdgeFlangeFeatureData` (`0x63`), `IMiterFlangeFeatureData` (`0x64`)
  - **样本资产**: 待制备（不同法兰角度 90°/120°、法兰长度、折弯位置内侧/外侧）
  - **全量 API 属性与数据模型**:
    - [ ] 法兰长度、角度、折弯位置枚举（材料在内、材料在外、折弯在外）
    - [ ] 自定义释放槽与偏移距离
  - **Selection 选择项逆向**:
    - [ ] 附着边线持久句柄（`moEdgeRef_c`）
    - [ ] 法兰方向参考实体与展开边线关联
  - **代码创建与特征注入**:
    - [ ] 构造边线法兰并注入特征流，原生重建验证

- [ ] **Task 2.3: 钣金折弯成形 (`Hem`, `Jog`, `LoftedBend`)**
  - **API 接口**: `IHemFeatureData` (`0x65`), `IJogFeatureData` (`0x66`), `ILoftedBendsFeatureData` (`0x69`)
  - **样本资产**: 待制备（闭合褶边、开放褶边、转折偏移量、放样折弯）
  - **全量 API 属性与数据模型**:
    - [ ] 褶边长度与间隙、转折高度与固定面偏移、放样成形线数
  - **Selection 选择项逆向**:
    - [ ] 褶边边线（`moEdgeRef_c`）、转折固定面（`moFaceRef_c`）与转折草图线
  - **代码创建与特征注入**:
    - [ ] 构造成形节点并注入流，原生重建验证

- [ ] **Task 2.4: 钣金正交切除与展开系统 (`NormalCut`, `FlatPattern`, `Fold`/`UnFold`)**
  - **API 接口**: `ISMNormalCutFeatureData2` (`0x67`), `IFlatPatternFeatureData` (`0x62`), `IFoldsFeatureData` (`0x6D`/`0x6E`)
  - **样本资产**: 待制备（跨折弯正交切除、展平特征）
  - **全量 API 属性与数据模型**:
    - [ ] 正交切除优化几何体选项、展平特征角部处理与折弯线注释
  - **Selection 选择项逆向**:
    - [ ] 展开固定面实体引用（`moFaceRef_c`）、展开折弯边线
  - **代码创建与特征注入**:
    - [ ] 构造展平/切除节点并注入流，原生重建验证

---

### Phase 3: 高级实体与多实体操作 (Advanced Body)

- [ ] **Task 3.1: 扫描与放样 (`Sweep`, `Loft`)**
  - **API 接口**: `ISweepFeatureData` (`0x4F`), `ILoftFeatureData` (`0x50`)
  - **全量 API 属性与 Selection 逆向**:
    - [ ] 扫描截面与路径草图引用、引导线集合、截面相切控制
    - [ ] 放样截面顺序列表、起始/结束约束表面
  - **代码创建与特征注入**:
    - [ ] 构造扫描/放样流对象并注入验证

- [ ] **Task 3.2: 抽壳、拔模与筋 (`Shell`, `Draft`, `Rib`)**
  - **API 接口**: `IShellFeatureData` (`0x56`), `IDraftFeatureData` (`0x57`), `IRibFeatureData` (`0x58`)
  - **全量 API 属性与 Selection 逆向**:
    - [ ] 抽壳壁厚、开孔移除面集合（`moFaceRef_c` 列表）、多厚度面
    - [ ] 拔模角度、中性面/拔模方向基准面
    - [ ] 筋厚度、挤出方向、草图轮廓
  - **代码创建与特征注入**:
    - [ ] 构造抽壳/拔模/筋节点并注入验证

- [ ] **Task 3.3: 多实体组合与分割 (`Combine`, `Split`, `DeleteBody`)**
  - **API 接口**: `ICombineBodiesFeatureData` (`0x59`), `ISplitBodyFeatureData` (`0x5A`)
  - **全量 API 属性与 Selection 逆向**:
    - [ ] 布尔运算类型（Add, Subtract, Common）
    - [ ] 主体实体引用与操作体实体列表引用（`moCompSolidBody_c`）
  - **代码创建与特征注入**:
    - [ ] 构造实体组合节点并注入验证

---

### Phase 4: 曲面工作台 (Surface Modeling)

- [ ] **Task 4.1: 基元曲面 (`ExtrudeSurf`, `RevolveSurf`, `FlatSurface`)**
  - **API 接口**: `ISurfExtrudeFeatureData` (`0x5B`), `ISurfRevolveFeatureData` (`0x5C`)
  - **全量 API 属性与 Selection 逆向**:
    - [ ] 拉伸/旋转曲面范围、草图轮廓引用、曲面实体归属（`moSurfaceBodyFolder_c`）
  - **代码创建与特征注入**:
    - [ ] 构造曲面节点并注入验证

- [ ] **Task 4.2: 复杂成形曲面 (`SweepSurf`, `LoftSurf`, `FillSurf`)**
  - **API 接口**: `ISweepSurfFeatureData` (`0x5D`), `ILoftSurfFeatureData` (`0x5E`), `IFillSurfaceFeatureData` (`0x5F`)
  - **全量 API 属性与 Selection 逆向**:
    - [ ] 边界曲线集合、修补相切曲率连续性参数 (G0/G1/G2)
  - **代码创建与特征注入**:
    - [ ] 构造复杂曲面节点并注入验证

- [ ] **Task 4.3: 曲面操作 (`Knit`, `TrimSurf`, `ExtendSurf`, `OffsetSurf`)**
  - **API 接口**: `ISurfaceKnitFeatureData` (`0x60`), `ISurfTrimFeatureData` (`0x61`)
  - **全量 API 属性与 Selection 逆向**:
    - [ ] 缝合曲面列表、缝合生成实体开关、裁剪工具曲面与保留/移除区域
  - **代码创建与特征注入**:
    - [ ] 构造曲面操作节点并注入验证

---

### Phase 5: 焊件与结构系统 (Weldments & Structure)

- [ ] **Task 5.1: 焊件特征与结构构件 (`Weldment`, `StructuralMember`)**
  - **API 接口**: `IStructuralMemberFeatureData` (`0x6A`)
  - **全量 API 属性与 Selection 逆向**:
    - [ ] 型材标准、尺寸规格、路径段线集合、角部斜切/对接处理
  - **代码创建与特征注入**:
    - [ ] 构造结构构件并注入验证

- [ ] **Task 5.2: 顶端盖与角撑板 (`EndCap`, `Gusset`)**
  - **API 接口**: `IEndCapFeatureData` (`0x6B`), `IGussetFeatureData` (`0x6C`)
  - **全量 API 属性与 Selection 逆向**:
    - [ ] 端盖厚度/倒角、角撑板型式与定位面引用
  - **代码创建与特征注入**:
    - [ ] 构造端盖/角撑板并注入验证

- [ ] **Task 5.3: 焊缝与切割清单 (`WeldBead`, `CutList`)**
  - **API 接口**: `IWeldBeadFeatureData` (`0x6F`), `ICutListFolder`
  - **全量 API 属性与 Selection 逆向**:
    - [ ] 焊缝大小、工艺标记、接触面集合、切割清单汇总属性
  - **代码创建与特征注入**:
    - [ ] 构造焊缝/清单并注入验证

---

### Phase 6: 装配体配合与引用系统 (Assembly Mates)

- [ ] **Task 6.1: 装配体标准配合关系 (Standard Mates)**
  - **API 接口**: `IMate2` (`moMate_c`)
  - **全量 API 属性与 Selection 逆向**:
    - [ ] 重合、同轴心、平行、垂直、距离、角度配合
    - [ ] 配合实体 1/2 的持久拓扑引用（面、边、轴、顶点句柄）与对齐方向（Aligned / AntiAligned）
  - **代码创建与配合注入**:
    - [ ] 构造配合关系并注入装配体流，原生重建验证

- [ ] **Task 6.2: 高级配合与机械配合 (Advanced & Mechanical Mates)**
  - **API 接口**: `IMate2` / `IScrewNutMateFeatureData` / `IGearMateFeatureData`
  - **全量 API 属性与 Selection 逆向**:
    - [ ] 宽度、路径、齿轮、齿条、螺旋配合参数
    - [ ] 传动比、螺距数值与从动件轨迹面引用
  - **代码创建与配合注入**:
    - [ ] 构造高级配合并注入验证
