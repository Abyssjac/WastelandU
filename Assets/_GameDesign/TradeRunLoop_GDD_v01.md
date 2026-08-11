# SkyCity Trade Run Loop GDD - v01

## 1. Purpose

This document defines the first playable version of SkyCity's small roguelite trade loop.

The goal of this system is not to build the full economy yet. The goal is to verify whether the player can enjoy a short route-planning trade run built around:

- Limited action points.
- Limited backpack slots.
- Limited money capacity.
- Resource gathering.
- Merchant trading.
- Temporary upgrades.
- Simple delivery quests.
- Bringing useful materials back to the base.

The small loop should connect into the larger base and NPC progression loop later.

Core design statement:

```text
In each trade run, the player uses limited action points and inventory space to decide which route to take, which resources to gather, which goods to trade, which quests to complete, and which materials are worth bringing home.
```

## 2. High-Level Structure

The game has two major sessions:

### Small Loop: Trade Run

The player enters a route map, travels between nodes, gathers materials, buys and sells goods, completes small quests, and returns with resources.

Primary output:

- Base materials.
- Quest rewards.
- Small amount of money.
- Possible meta progression hooks.

### Large Loop: Base And NPC Progression

The player uses resources from trade runs to build facilities, craft furniture, process goods, progress story content, and improve NPC affection.

NPC affection should eventually improve the player's future trade runs.

Example connection:

```text
Trade run resources
-> Base crafting and building
-> NPC affection growth
-> Run efficiency bonuses
-> Better trade run results
```

## 3. Core Loop

```text
Start trade run
-> Check current map and player status
-> Choose next node
-> Spend action points to travel
-> Resolve node interaction
-> Manage backpack, money, and quests
-> Continue route planning
-> Reach exit node or return early
-> Settle rewards
-> Return to base progression
```

The expected player questions during a run are:

- Should I gather base materials or save backpack slots for trade goods?
- Should I sell materials for money or bring them home?
- Should I spend money on more action points?
- Should I visit a merchant for resale profit?
- Should I spend extra action points to complete a quest?
- Should I return now or risk going deeper?

## 4. Player Run State

Each trade run tracks the following values.

| Field | Description | Design Purpose |
|---|---|---|
| Current Node | The player's current map node. | Determines available routes and interactions. |
| Action Points | Resource spent on travel, quests, and some interactions. | Limits run length. |
| Backpack Capacity | Number of item stacks or item slots the player can carry. | Limits material and cargo output. |
| Backpack Items | Items currently carried by the player. | Creates tradeoffs between resources, trade goods, and quest goods. |
| Money | Current run money. | Used for buying goods and services. |
| Money Cap | Maximum money that can be carried during the run. | Prevents pure money farming. |
| Active Quests | Quests accepted during this run. | Creates route objectives. |
| Completed Quests | Quests completed during this run. | Used for settlement and rewards. |

## 5. Resource Categories

The system separates item categories so that resource nodes and merchant nodes have different incentives.

### 5.1 Base Materials

Base materials are mainly gained from resource nodes. They are the most important items to bring back to the large loop.

Recommended first materials:

| Display Name | Item ID | Chinese Name | Primary Use |
|---|---|---|---|
| Cloudwood | `cloudwood` | 云木 | Basic structure, workbench, early furniture. |
| Brass Scrap | `brass_scrap` | 黄铜碎件 | Mechanical repair, tools, ship controls. |
| Sailcloth Fiber | `sailcloth_fiber` | 帆布纤维 | Sails, soft furniture, fabric repairs. |
| Aether Shard | `aether_shard` | 浮素碎晶 | Power core, stabilizer, tech unlocks. |

Design notes:

- These should usually come from resource nodes.
- Regular merchants may buy them, but should not sell large amounts of them in the early version.
- Selling base materials should be possible, but the player should often feel tension because these materials are useful for base progression.

### 5.2 Trade Goods

Trade goods are mainly bought from regular merchants and sold to other merchants or used in quests.

Trade goods are not usually used directly in base building.

Example trade goods:

| Display Name | Item ID | Role |
|---|---|---|
| Spice Crate | `spice_crate` | Basic resale cargo. |
| Sealed Rations | `sealed_rations` | Common supply cargo. |
| Mechanical Oil | `mechanical_oil` | Engineering-flavored trade item. |
| Glassware Case | `glassware_case` | Higher-value fragile cargo. |
| Map Bundle | `map_bundle` | Information-flavored cargo. |
| Fine Fabric | `fine_fabric` | Merchant-side fabric item. |

Design notes:

- Trade goods create money-making opportunities inside the small loop.
- Trade goods occupy backpack slots, so they compete with base materials.
- Trade goods help regular merchant nodes feel valuable even when the player primarily wants materials.

### 5.3 Quest Goods

Quest goods are tied to run quests.

They may be:

- Given by a quest node and delivered elsewhere.
- Purchased from a merchant and delivered to a quest target.
- Built from carried materials in a later version.

Example quest goods:

| Display Name | Item ID | Role |
|---|---|---|
| Repair Package | `repair_package` | Delivery quest cargo. |
| Urgent Parcel | `urgent_parcel` | Time-sensitive quest cargo. |
| Beacon Parts | `beacon_parts` | Navigation quest cargo. |
| Medical Bundle | `medical_bundle` | Rescue quest cargo. |

Design notes:

- Quest goods should occupy backpack space.
- Quest rewards should usually be better than a single resource node.
- Quest route cost should make the player evaluate whether the reward is worth the detour.

## 6. Node Types

The first prototype should support a small set of node types.

### 6.1 Start Node

The start node is where the player begins a trade run.

Possible actions:

- View current run status.
- Move to connected nodes.

### 6.2 Resource Node

Resource nodes provide base materials.

Example outputs:

| Node Theme | Possible Reward |
|---|---|
| Floating Timber Dock | `cloudwood` x2-4 |
| Abandoned Machine Crate | `brass_scrap` x2-3 |
| Torn Sail Camp | `sailcloth_fiber` x2-4 |
| Aether Rift | `aether_shard` x1-2 |

Design rules:

- Resource nodes should be stable and easy to understand.
- Rewards should usually be visible before entering the node.
- Resource nodes should be the main source of take-home materials.
- Some resource nodes may cost action points to harvest.

### 6.3 Regular Merchant Node

Regular merchants focus on buying and selling items.

They are responsible for the resale gameplay.

Regular merchant functions:

- Buy items from the player.
- Sell trade goods.
- Use different prices for different goods.
- Create route-based profit opportunities.

Regular merchants should not be the main source of action point recovery. That role belongs to upgrade merchants.

Design purpose:

```text
Regular merchants make money useful during a run and create a tradeoff between carrying materials home and carrying goods for resale profit.
```

Example merchant profiles:

| Merchant Type | Sells | Buys At High Price |
|---|---|---|
| Food Merchant | `sealed_rations` | `spice_crate`, food-related cargo |
| Mechanic Merchant | `mechanical_oil`, `glassware_case` | `brass_scrap`, machine-related cargo |
| Map Merchant | `map_bundle` | `aether_shard`, information-related cargo |
| Fabric Merchant | `fine_fabric` | `sailcloth_fiber`, fabric-related cargo |

### 6.4 Upgrade Merchant Node

Upgrade merchants focus on temporary run upgrades and recovery.

They are responsible for extending the run.

Upgrade merchant functions:

- Restore action points.
- Temporarily increase backpack capacity.
- Provide one-run bonuses.
- Compress goods.
- Repair negative run states.

Upgrade merchants should not focus on resale trading.

Design purpose:

```text
Upgrade merchants let the player spend money to continue exploring or improve run efficiency.
```

Example services:

| Service | Effect |
|---|---|
| Supply Pack | Restore action points. |
| Cargo Pouch | Increase backpack capacity for this run. |
| Light Sail Tuning | Reduce the next travel cost. |
| Cargo Compression | Convert several same-type materials into one packed item. |
| Temporary Porter | Next resource node gives +1 item. |
| Hull Check | Remove one negative event state. |

### 6.5 Quest Node

Quest nodes give or complete small run-limited tasks.

Quest functions:

- Offer a delivery request.
- Ask for specific items.
- Mark a target delivery node.
- Reward the player with materials, money, or temporary bonuses.

Quest design rule:

```text
Quest rewards should often be higher than one normal resource node, but the quest should cost extra route distance, backpack space, or action points.
```

Example quest:

```text
Quest: Lighthouse Repair
Requirement: brass_scrap x2
Delivery Node: Old Lighthouse
Reward: aether_shard x3 + 20 money
```

Example quest:

```text
Quest: Emergency Cloth Delivery
Requirement: sailcloth_fiber x3 OR fine_fabric x1
Delivery Node: Windbreak Dock
Reward: +1 backpack capacity this run + cloudwood x3
```

### 6.6 Exit Node

The exit node ends the current trade run and settles rewards.

Possible settlement output:

- Materials brought home.
- Money brought home, limited by money cap.
- Quest completion rewards.
- Optional score or summary.

## 7. Map Structure

The first prototype should use manually configured maps, not random generation.

The map should be a route graph with tree-like branching.

The player moves between connected nodes. Movement cost is calculated by the configured edge cost or by shortest path cost between two nodes.

Example structure:

```text
Start
-> Layer 1 nodes
-> Layer 2 nodes
-> Layer 3 nodes
-> Exit
```

Design goals:

- Let players compare route options.
- Let players plan around action point cost.
- Let merchant placement matter.
- Let quest delivery targets create detours.
- Keep the first implementation simple and configurable.

Recommended first test map size:

- 12-18 nodes.
- 3-4 route layers.
- 2-3 resource nodes.
- 2 regular merchants.
- 1 upgrade merchant.
- 2 quest nodes.
- 1 exit node.

## 8. Limits And Economy

### 8.1 Backpack Limit

Backpack capacity creates the main item tradeoff.

The player should frequently choose between:

- Carrying base materials home.
- Carrying trade goods for resale.
- Carrying quest goods for a high reward.
- Selling low-priority items to make space.

### 8.2 Money Limit

Money cap prevents the small loop from becoming pure money farming.

Recommended fiction:

```text
The ship has a limited safe cash lockbox during each voyage.
```

Design notes:

- The player may still use money during the run.
- Money should be most useful as a conversion resource.
- Upgrade merchants give money an active purpose.
- If money exceeds the cap, the overflow should either be lost or converted into a minor score or reputation value.

### 8.3 Money As Conversion Resource

Money should mainly help the player:

- Buy trade goods.
- Buy action point recovery.
- Buy temporary backpack capacity.
- Buy temporary route efficiency.
- Create resale opportunities.

Money should not be the primary long-term output of the small loop.

## 9. Quest Design

Quests in the small loop should be simple and run-limited.

Most quests should use this pattern:

```text
Accept quest
-> Obtain or already carry required item
-> Travel to target node
-> Deliver item
-> Receive reward
```

Possible quest variables:

| Variable | Description |
|---|---|
| Required Items | Items needed to complete the quest. |
| Delivery Node | Target node for completion. |
| Reward Items | Materials or goods gained. |
| Reward Money | Money gained. |
| Extra Action Cost | Optional action point cost. |
| Time Limit | Optional max moves before failure. |
| Large Cargo | Optional cargo that occupies extra backpack slots. |
| Fragile Cargo | Optional cargo that may be damaged by events. |

For the first MVP, only required items, delivery node, reward items, and reward money are necessary.

## 10. NPC And Large Loop Integration

NPC progression belongs to the large loop, but the small loop should reserve hooks for NPC bonuses.

Possible NPC bonus directions:

| NPC | Bonus Direction | Example Bonuses |
|---|---|---|
| Nara Vey | Navigation | More starting action points, lower travel cost, hidden route preview. |
| Ivo Renn | Engineering | More backpack slots, better resource yield, reduced repair cost. |
| Silas Marr | Trading | Better merchant prices, higher money cap, improved merchant inventory. |
| Eli | Information | Reveal quest rewards, reveal node contents, record island demand. |

Design principle:

```text
NPC affection should improve small-loop efficiency, not replace player route planning.
```

## 11. MVP EditorWindow Requirements

The first in-engine prototype can be a pure text-based EditorWindow.

No visual presentation is required.

### 11.1 Required Panels

#### Run Status Panel

Displays:

- Current node.
- Action points.
- Money.
- Money cap.
- Backpack capacity.
- Backpack contents.
- Active quests.

#### Map / Route Panel

Displays:

- Current node.
- Connected nodes.
- Travel cost to each connected node.
- Node type.
- Optional known rewards or merchant type.

Actions:

- Move to selected node.

#### Node Interaction Panel

Displays actions based on node type.

Resource node actions:

- Gather listed resources.

Regular merchant actions:

- Buy listed trade goods.
- Sell carried items.

Upgrade merchant actions:

- Buy action point recovery.
- Buy temporary backpack capacity.
- Buy other temporary services.

Quest node actions:

- Accept quest.
- Complete quest if requirements are met.

Exit node actions:

- End run and settle rewards.

#### Settlement Panel

Displays after run ends:

- Materials brought home.
- Money brought home.
- Completed quests.
- Items left behind or discarded.
- Optional run summary.

### 11.2 Suggested Data Model

The first implementation can be driven by ScriptableObjects, JSON, or plain serialized C# classes.

Core data objects:

```text
ItemDefinition
RunNodeDefinition
RouteEdgeDefinition
MerchantInventoryEntry
UpgradeServiceDefinition
QuestDefinition
TradeRunState
```

Suggested fields:

```text
ItemDefinition
- id
- displayName
- category
- baseValue
- stackLimit

RunNodeDefinition
- id
- displayName
- nodeType
- rewards
- merchantInventory
- upgradeServices
- questIds
- connectedNodeIds

RouteEdgeDefinition
- fromNodeId
- toNodeId
- actionPointCost

MerchantInventoryEntry
- itemId
- buyPrice
- sellPrice
- quantity

UpgradeServiceDefinition
- id
- displayName
- moneyCost
- effectType
- effectValue

QuestDefinition
- id
- displayName
- startNodeId
- targetNodeId
- requiredItems
- rewardItems
- rewardMoney
```

## 12. First Test Scenario

Recommended first run setup:

Player starts with:

```text
Action Points: 10
Backpack Capacity: 6
Money: 20
Money Cap: 80
```

Map contains:

- 1 start node.
- 4 resource nodes.
- 2 regular merchants.
- 1 upgrade merchant.
- 2 quest nodes.
- 1 exit node.

Expected run decisions:

- Gather base materials early.
- Visit a regular merchant to sell low-priority items or buy trade goods.
- Decide whether to spend money at the upgrade merchant for more action points.
- Decide whether a quest detour is worth the cost.
- Return with a mix of base materials and limited money.

## 13. Validation Questions

The MVP should answer these questions:

1. Does the player care about route planning?
2. Does backpack pressure create meaningful choices?
3. Does the money cap prevent pure money farming without feeling too punishing?
4. Do regular merchants feel valuable for resale gameplay?
5. Do upgrade merchants feel valuable for extending or improving a run?
6. Do quests create interesting detours?
7. Does the final settlement make the player want to start another run?
8. Do brought-home materials clearly support the large base/NPC loop?

## 14. Main Risks

### Risk: Money Cap Feels Punishing

If players play well but lose money because of the cap, the system may feel unfair.

Possible solution:

- Let overflow money convert into a minor score, reputation, or merchant favor.
- Make money primarily useful during the run through upgrade merchants.

### Risk: Merchants Become Mandatory

If merchants are too efficient, every route may revolve around them.

Possible solution:

- Split merchants into regular merchants and upgrade merchants.
- Give each merchant limited inventory.
- Make merchant placement vary across maps.

### Risk: Quests Become Simple Math

If all quests are basic delivery tasks, they may feel repetitive.

Possible solution:

- Add optional constraints later, such as fragile cargo, time limits, large cargo, or alternate requirements.
- Keep the first MVP simple, then expand only if the base loop works.

### Risk: Base Materials Lose Value

If selling materials is always optimal, the large loop will weaken.

Possible solution:

- Make base materials important for base progression.
- Keep merchant sale value useful but not dominant.
- Let some base upgrades require specific materials from runs.

## 15. Design Summary

This small loop is a route-planning trade run.

The player should feel that every run is about choosing what is worth spending limited travel power, backpack space, and money on.

Regular merchants create resale opportunities.

Upgrade merchants convert money into more run potential.

Resource nodes provide the materials needed for base progression.

Quest nodes create high-reward detours.

The large loop gives these runs long-term meaning through base construction and NPC affection.

Final target feeling:

```text
I brought back useful materials, learned the route better, and next time I can make a sharper plan.
```
