# JackyUtilityToolKit

通用基础工具包，为项目中的其他系统提供底层支撑。包含 ScriptableObject 数据库框架、全局 Database 管理器、开发者调试控制台、可视化材质控制器、Billboard 工具库以及全局游戏系统启动器。

---

## 目录

1. [核心架构](#核心架构)
2. [脚本详解](#脚本详解)
   - [GeneralDataBase（数据库框架）](#generaldatabase数据库框架)
   - [PropertyDatabaseManager](#propertydatabasemanager)
   - [DeveloperDebugger（调试控制台）](#developerdebugger调试控制台)
   - [BaseVisualController](#basevisualcontroller)
   - [UtilityLibrary](#utilitylibrary)
   - [MyGameSystem](#mygamesystem)
3. [快速开始](#快速开始)

---

## 核心架构

```
JackyUtilityToolKit
├── GeneralDataBase.cs          ← 泛型数据库框架（接口 + 基类）
├── PropertyDatabaseManager.cs  ← 全局 Database 注册 & 获取（Singleton）
├── DeveloperDebugger/
│   ├── DebugConsoleManager.cs  ← 调试命令注册/执行中枢（Singleton）
│   ├── DebugConsoleUI.cs       ← 运行时控制台 UI（按 ~ 打开）
│   └── DebugHelper.cs          ← DebugGUIPanel 即时 GUI 面板 + IDebuggable 接口
├── BaseVisualController.cs     ← Renderer 材质批量管理（替换/闪烁/还原）
├── UtilityLibrary.cs           ← 静态工具方法（FaceCamera / Billboard）
└── GameSystem/
    └── MyGameSystem.cs         ← 全局系统初始化器（Singleton，实例化子系统 Prefab）
```

---

## 脚本详解

### GeneralDataBase（数据库框架）

提供一套基于 **Enum + String 双键** 索引的 ScriptableObject 数据库模式。

#### IEnumStringKeyedEntry\<TEnum\>

接口，定义任何可被数据库索引的条目必须提供的键：

| 属性 | 类型 | 说明 |
|---|---|---|
| `EnumKey` | `TEnum` | 枚举键（主键，用于快速查找） |
| `StringKey` | `string` | 字符串键（备用键，用于序列化/配置） |

#### EnumStringKeyedProperty\<TEnum\>

抽象 ScriptableObject 基类，已实现 `IEnumStringKeyedEntry<TEnum>`。子类只需添加自身数据字段即可。

```csharp
// 示例：定义一个物品属性
[CreateAssetMenu]
public class MyItemProperty : EnumStringKeyedProperty<MyItemKey>
{
    public int damage;
    public Sprite icon;
}
```

> 也可以不继承此基类，直接让 ScriptableObject 实现 `IEnumStringKeyedEntry<TEnum>` 接口（如 `BuildableProperty` 就是这样做的）。

#### EnumStringKeyedDatabase\<TEntry, TEnum\>

抽象 ScriptableObject 数据库基类。内部维护两个 Dictionary（按 Enum / 按 String）实现 O(1) 查询。

| 方法 | 说明 |
|---|---|
| `GetByEnum(TEnum key)` | 按枚举键查找，未找到返回 null |
| `GetByString(string key)` | 按字符串键查找，未找到返回 null |
| `TryGetByEnum(TEnum key, out TEntry)` | 带 out 参数的查找 |
| `TryGetByString(string key, out TEntry)` | 带 out 参数的查找 |
| `InitializeDictionaries()` | 手动重建索引（通常自动调用） |

| 属性 | 说明 |
|---|---|
| `Entries` | `IReadOnlyList<TEntry>` — 原始条目列表 |
| `ByEnum` | `IReadOnlyDictionary<TEnum, TEntry>` |
| `ByString` | `IReadOnlyDictionary<string, TEntry>` |

**创建具体数据库：**

```csharp
[CreateAssetMenu]
public class MyItemDatabase : EnumStringKeyedDatabase<MyItemProperty, MyItemKey> { }
```

字典在首次查询时自动初始化（懒加载）。Editor 下 `OnValidate` 时也会重建。

---

### PropertyDatabaseManager

全局数据库管理器（Singleton，`DontDestroyOnLoad`）。在 Inspector 中拖入所有 Database ScriptableObject，运行时通过类型查询。

**Inspector 配置：**

| 字段 | 说明 |
|---|---|
| `allDatabases` | `ScriptableObject[]` — 拖入项目中所有的 Database 资源 |

**使用方式：**

```csharp
var db = PropertyDatabaseManager.Instance.GetDatabase<MyItemDatabase>();
var item = db.GetByEnum(MyItemKey.Sword);
```

---

### DeveloperDebugger（调试控制台）

运行时开发者调试系统，由三个类组成。

#### DebugConsoleManager

调试命令中枢（Singleton，`DontDestroyOnLoad`）。

**命令系统：**

| 方法 | 说明 |
|---|---|
| `RegisterCommand(DebugCommand)` | 注册一条自定义命令 |
| `ExecuteCommand(string input)` | 执行命令字符串（空格分隔参数） |

**DebugCommand 结构：**

```csharp
new DebugCommand(
    "mycommand",                     // 命令 ID
    "Description of what it does",   // 帮助说明
    args => { /* 执行逻辑 */ }       // 回调
);
```

**IDebuggable 注册系统：**

任何 MonoBehaviour 实现 `IDebuggable` 接口即可通过终端控制 debug 开关：

```csharp
public class MySystem : MonoBehaviour, IDebuggable
{
    public string DebugId => "mysys";        // 终端中使用的 ID
    public bool DebugEnabled { get; set; }   // 控制 Gizmos/GUI 等
}
```

注册 / 注销：
```csharp
DebugConsoleManager.Instance.RegisterDebugTarget(this);   // Start
DebugConsoleManager.Instance.UnregisterDebugTarget(this); // OnDestroy
```

**内置命令：**

| 命令 | 说明 |
|---|---|
| `ls` | 列出所有已注册的命令 |
| `debug ls` | 列出所有已注册的 IDebuggable 目标及其开关状态 |
| `debug <targetId> <true/false>` | 切换某个目标的 debug 开关 |

**事件：**

| 事件 | 说明 |
|---|---|
| `OnConsoleToggled(bool isOpen)` | 控制台打开/关闭时触发，可用于暂停游戏输入等 |

#### DebugConsoleUI

运行时控制台 UI 面板，使用 TextMeshPro 输入框。

| 按键 | 功能 |
|---|---|
| `` ` ``（反引号 / 波浪键） | 打开/关闭控制台 |
| `Enter` | 提交命令 |

**Inspector 配置：**

| 字段 | 说明 |
|---|---|
| `consolePanel` | 控制台面板 GameObject |
| `inputField` | TMP_InputField 命令输入框 |
| `toggleKey` | 切换键（默认 BackQuote） |

#### DebugGUIPanel

轻量即时模式 debug 面板，用于在 `OnGUI` 中快速绘制调试信息。

**使用方式：**

```csharp
private void OnGUI()
{
    var panel = DebugGUIPanel.Begin(new Vector2(10, 10), 420f, 14);
    panel.DrawLine("<b>═══ My Debug ═══</b>");
    panel.DrawLine($"Health: {hp}");
    panel.DrawLine($"State: <color=cyan>{state}</color>");
    panel.End();
}
```

| 方法 | 说明 |
|---|---|
| `Begin(position, width, lineCount, ...)` | 开始绘制，绘制半透明黑色背景 |
| `DrawLine(string)` | 绘制一行（支持 Rich Text） |
| `Space(pixels)` | 空行 |
| `End()` | 结束绘制 |

内部复用单一实例，不产生 GC。

#### IDebuggable 接口

```csharp
public interface IDebuggable
{
    string DebugId { get; }       // 唯一标识，如 "buildpos", "playermove"
    bool DebugEnabled { get; set; }
}
```

---

### BaseVisualController

挂载在 GameObject 上，统一管理其所有 Renderer 的材质操作。适用于受击闪烁、状态高亮、预览着色等场景。

**Inspector 配置：**

| 字段 | 说明 |
|---|---|
| `targetRenderers` | 手动指定 Renderer 列表（留空则自动获取所有子 Renderer） |
| `applyMode` | `InstanceMaterials`（独立材质实例）/ `SharedMaterials`（共享材质，影响所有引用者） |
| `cacheOnAwake` | 是否在 Awake 时缓存初始材质 |

**公开 API：**

| 方法 | 说明 |
|---|---|
| `SetMaterialAll(Material)` | 替换所有 Renderer 的所有 slot 为同一材质 |
| `SetMaterialSlot(int slot, Material)` | 替换指定 slot 索引的材质 |
| `FlashMaterial(Material, float duration)` | 闪烁——临时替换材质，duration 秒后自动恢复 |
| `StopFlash()` | 立即停止闪烁 |
| `ResetMaterials()` | 恢复到缓存的初始材质 |
| `CacheCurrentMaterials()` | 将当前材质保存为"初始状态" |
| `ResolveRenderers()` | 重新搜集 Renderer 引用 |

---

### UtilityLibrary

静态工具类，提供面向摄像机的 Billboard 功能。

| 方法 | 说明 |
|---|---|
| `FaceCameraOnce(GameObject, Camera, keepUp, invertForward)` | 让目标立刻朝向摄像机（调用一次） |
| `EnsureFaceCamera(GameObject, Camera, keepUp, invertForward)` | 自动挂载 `FaceCameraBillboard` 组件，之后每帧自动朝向摄像机 |

**FaceCameraBillboard**（内部 MonoBehaviour）：
- 在 `LateUpdate` 中自动调用 `FaceCameraOnce`
- 通过 `EnsureFaceCamera` 添加，无需手动挂载

---

### MyGameSystem

全局游戏系统启动器（Singleton，`DontDestroyOnLoad`）。在 `Awake` 时实例化所有注册的子系统 Prefab。

**Inspector 配置：**

| 字段 | 说明 |
|---|---|
| `allSystemPrefabSingletons` | `GameObject[]` — 拖入需要全局存在的子系统 Prefab（如 PropertyDatabaseManager、DebugConsoleManager 等） |

**用途：**
在场景中放置一个 MyGameSystem，将所有全局管理器 Prefab 拖入数组。游戏启动时自动 `Instantiate` 所有子系统，保证它们在整个生命周期中存在。

---

## 快速开始

### 1. 搭建全局系统

1. 创建一个空 GameObject，挂载 `MyGameSystem`
2. 将以下 Prefab 拖入 `allSystemPrefabSingletons`：
   - PropertyDatabaseManager Prefab
   - DebugConsoleManager Prefab（可选）
   - 其他全局管理器 Prefab

### 2. 配置 PropertyDatabaseManager

1. 创建 PropertyDatabaseManager Prefab，挂载 `PropertyDatabaseManager` 组件
2. 创建你的 Database ScriptableObject（继承 `EnumStringKeyedDatabase`）
3. 将所有 Database 拖入 `allDatabases` 数组

### 3. 定义自己的数据模式

```csharp
// 1. 定义枚举
public enum MyItemKey { None = 0, Sword = 1, Shield = 2 }

// 2. 定义属性 ScriptableObject
[CreateAssetMenu]
public class MyItemProperty : EnumStringKeyedProperty<MyItemKey>
{
    public int damage;
    public Sprite icon;
}

// 3. 定义数据库
[CreateAssetMenu]
public class MyItemDatabase : EnumStringKeyedDatabase<MyItemProperty, MyItemKey> { }

// 4. 运行时查询
var db = PropertyDatabaseManager.Instance.GetDatabase<MyItemDatabase>();
var sword = db.GetByEnum(MyItemKey.Sword);
```

### 4. 使用调试控制台

1. 场景中确保存在 `DebugConsoleManager`（通过 MyGameSystem 或手动放置）
2. 创建 DebugConsoleUI Prefab（Canvas + Panel + TMP_InputField），挂载 `DebugConsoleUI`
3. 运行时按 `` ` `` 打开控制台，输入 `ls` 查看所有命令

### 5. 注册自定义命令

```csharp
private void Start()
{
    DebugConsoleManager.Instance.RegisterCommand(new DebugCommand(
        "heal",
        "Heal player. Usage: heal <amount>",
        args =>
        {
            int amount = args.Length > 0 ? int.Parse(args[0]) : 100;
            player.Heal(amount);
            Debug.Log($"Healed {amount} HP");
        }
    ));
}
```
