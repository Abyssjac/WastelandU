using JackyUtility;
using UnityEngine;

/// <summary>
/// 临时测试脚本 — 验证 Container 全套逻辑（含堆叠上限）。
/// 挂在场景中任意 GameObject 上，确保场景中有 PropertyDatabaseManager 且已注册 ContainerItemDatabase。
/// 用键盘操作，OnGUI 实时显示状态，Console 输出详细日志。验证完毕后删除此脚本。
/// </summary>
public class ContainerTester : MonoBehaviour
{
    private Container<Key_ContainerItemPP> container;
    private ContainerPropertyLookup<ContainerItemProperty, Key_ContainerItemPP> lookup;
    [SerializeField] private UI_Container uiContainer;  

    private void Start()
    {
        // 尝试绑定 database，用于获取 maxStackCount
        ContainerItemDatabase db = null;
        var dbManager = PropertyDatabaseManager.Instance;
        if (dbManager != null)
            db = dbManager.GetDatabase<ContainerItemDatabase>();

        if (db != null)
        {
            // 用 database 的 maxStackCount 作为堆叠上限回调
            container = new Container<Key_ContainerItemPP>(6, key =>
            {
                var prop = db.GetByEnum(key);
                return prop != null ? prop.maxStackCount : int.MaxValue;
            });
            lookup = new ContainerPropertyLookup<ContainerItemProperty, Key_ContainerItemPP>(container, db);
            Log("Container(6 slots) created with stack limits from ContainerItemDatabase.");
        }
        else
        {
            container = new Container<Key_ContainerItemPP>(6);
            Log("<color=yellow>No ContainerItemDatabase found — stack limits disabled.</color>");
        }

        PrintState();

        //uiContainer.InitSlots(container.MaxSlots);
        lookup?.BindUIContainer(uiContainer);
    }

    private void Update()
    {
        // ─── 数字键 1–5：TryAddItem 不同物品 ──────────────────
        if (Input.GetKeyDown(KeyCode.Alpha1))
            TestTryAdd(Key_ContainerItemPP.ContainerItem_Iron, 5);


        if (Input.GetKeyDown(KeyCode.Alpha2))
            TestTryAdd(Key_ContainerItemPP.ContainerItem_Copper, 3);

        if (Input.GetKeyDown(KeyCode.Alpha3))
            TestTryAdd(Key_ContainerItemPP.ContainerItem_Gold, 10);

        if (Input.GetKeyDown(KeyCode.Alpha4))
            TestTryAdd(Key_ContainerItemPP.ContainerItem_Wood, 1);

        if (Input.GetKeyDown(KeyCode.Alpha5))
            TestTryAdd(Key_ContainerItemPP.ContainerItem_Iron, 99);

        // ─── Q / W：TryRemoveItem ──────────────────────────────
        if (Input.GetKeyDown(KeyCode.Q))
            TestTryRemove(Key_ContainerItemPP.ContainerItem_Iron, 2);

        if (Input.GetKeyDown(KeyCode.W))
            TestTryRemove(Key_ContainerItemPP.ContainerItem_Copper, 99); // 故意超量

        // ─── E：AddItemReturnExcess（测试溢出）──────────────────
        if (Input.GetKeyDown(KeyCode.E))
            TestAddReturnExcess(Key_ContainerItemPP.ContainerItem_Stone, 50);

        // ─── R：RemoveItemReturnLack ─────────────────────────
        if (Input.GetKeyDown(KeyCode.R))
            TestRemoveReturnLack(Key_ContainerItemPP.ContainerItem_Iron, 999);

        // ─── T：EmptySlotAtIndex(0) ──────────────────────────
        if (Input.GetKeyDown(KeyCode.T))
        {
            bool ok = container.EmptySlotAtIndex(0);
            Log($"EmptySlotAtIndex(0) → {ok}");
            PrintState();
        }

        // ─── Y：EmptyItemByEnum ──────────────────────────────
        if (Input.GetKeyDown(KeyCode.Y))
        {
            bool ok = container.EmptyItemByEnum(Key_ContainerItemPP.ContainerItem_Copper);
            Log($"EmptyItemByEnum(Copper) → {ok}");
            PrintState();
        }

        // ─── U：PropertyLookup 测试 ──────────────────────────
        if (Input.GetKeyDown(KeyCode.U))
            TestPropertyLookup();

        // ─── P：打印当前状态 ─────────────────────────────────
        if (Input.GetKeyDown(KeyCode.P))
            PrintState();

        // ─── Backspace：清空所有 ─────────────────────────────
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            var oldGetMaxStack = lookup?.Database;
            if (oldGetMaxStack != null)
            {
                var db = oldGetMaxStack;
                container = new Container<Key_ContainerItemPP>(6, key =>
                {
                    var prop = db.GetByEnum(key);
                    return prop != null ? prop.maxStackCount : int.MaxValue;
                });
                lookup = new ContainerPropertyLookup<ContainerItemProperty, Key_ContainerItemPP>(container, db);
            }
            else
            {
                container = new Container<Key_ContainerItemPP>(6);
            }
            Log("<color=orange>Container reset to empty 6 slots.</color>");
            PrintState();
        }

        //if (Input.GetKeyDown(KeyCode.O)) {
        //    Debug.Log("Refreshing UI...");
        //    SlotDisplayData[] displayData = lookup.BuildDisplayData();
        //    if (displayData != null)
        //        uiContainer.Refresh(displayData);
        //}
    }

    // ─── Test Methods ────────────────────────────────────────────

    private void TestTryAdd(Key_ContainerItemPP key, int count)
    {
        bool ok = container.TryAddItem(key, count, out string reason);
        Log($"TryAddItem({key}, {count}) → {ok}" +
            (ok ? "" : $"  reason: {reason}"));
        PrintState();
    }

    private void TestTryRemove(Key_ContainerItemPP key, int count)
    {
        bool ok = container.TryRemoveItem(key, count, out string reason);
        Log($"TryRemoveItem({key}, {count}) → {ok}" +
            (ok ? "" : $"  reason: {reason}"));
        PrintState();
    }

    private void TestAddReturnExcess(Key_ContainerItemPP key, int count)
    {
        bool ok = container.AddItemReturnExcess(key, count, out int excess);
        Log($"AddItemReturnExcess({key}, {count}) → added={ok}, excess={excess}");
        PrintState();
    }

    private void TestRemoveReturnLack(Key_ContainerItemPP key, int count)
    {
        bool ok = container.RemoveItemReturnLack(key, count, out int lack);
        Log($"RemoveItemReturnLack({key}, {count}) → removed={ok}, lack={lack}");
        PrintState();
    }

    private void TestPropertyLookup()
    {
        if (lookup == null)
        {
            Log("<color=yellow>PropertyLookup not available (no database).</color>");
            return;
        }

        Log("── PropertyLookup ──");
        for (int i = 0; i < container.MaxSlots; i++)
        {
            var prop = lookup.GetPropertyByIndex(i);
            if (prop != null)
                Log($"  Slot[{i}] → {prop.EnumKey}, maxStack={prop.maxStackCount}, icon={prop.icon?.name ?? "null"}");
            else
                Log($"  Slot[{i}] → (empty)");
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────

    private void PrintState()
    {
        Log($"  MaxSlots={container.MaxSlots}  Used={container.UsedSlots}  " +
            $"Free={container.FreeSlots}  IsEmpty={container.IsEmpty}");

        for (int i = 0; i < container.MaxSlots; i++)
        {
            var slot = container.GetItemInfoByIndex(i);
            if (slot.IsEmpty)
                Log($"    [{i}] (empty)");
            else
                Log($"    [{i}] {slot.ItemEnum} ×{slot.ItemCount}");
        }
    }

    private void Log(string msg)
    {
        Debug.Log($"[ContainerTest] {msg}");
    }

    // ─── OnGUI 快捷键提示 ────────────────────────────────────────

    private void OnGUI()
    {
        // lineCount = title(1) + keys(9) + space(1) + state header(1) + slots(6) = 18
        var panel = DebugGUIPanel.Begin(new Vector2(10, 10), 480f, 18);
        panel.DrawLine("<b>═══ Container Tester ═══</b>");
        panel.DrawLine("1  Add Iron×5      2  Add Copper×3");
        panel.DrawLine("3  Add Gold×10     4  Add Wood×1");
        panel.DrawLine("5  Add Iron×99 (test stack overflow)");
        panel.DrawLine("Q  Remove Iron×2   W  Remove Copper×99 (fail)");
        panel.DrawLine("E  AddReturnExcess Stone×50");
        panel.DrawLine("R  RemoveReturnLack Iron×999");
        panel.DrawLine("T  EmptySlotAtIndex(0)   Y  EmptyByEnum(Copper)");
        panel.DrawLine("U  PropertyLookup test   P  Print state");
        panel.DrawLine("Backspace  Reset all");
        panel.Space();

        if (container != null)
        {
            panel.DrawLine($"<b>Slots: {container.UsedSlots}/{container.MaxSlots}  " +
                           $"Empty={container.IsEmpty}</b>");
            for (int i = 0; i < container.MaxSlots; i++)
            {
                var slot = container.GetItemInfoByIndex(i);
                panel.DrawLine(slot.IsEmpty
                    ? $"  [{i}] ---"
                    : $"  [{i}] {slot.ItemEnum} ×{slot.ItemCount}");
            }
        }
        panel.End();
    }
}