# MoneyTime -- Bug Fixes

This repository contains a fixed version of the **MoneyTime** plugin
originally created by **Wulf** for Oxide/uMod.

The original plugin includes a **New Player Welcome Bonus** feature,
however due to a logic issue in the code the bonus was **never actually
awarded to new players**.\
While resolving that issue, a couple of additional stability
improvements were also implemented.

These fixes preserve the original behavior and configuration of the
plugin while correcting the problems.

------------------------------------------------------------------------

# Fixes Implemented

## 1. Welcome Bonus Logic Fix (Major Issue)

### Problem

In the original plugin, new players were initialized as if they had
**already received the welcome bonus**:

``` csharp
WelcomeBonus = true;
```

However, the payout logic checks for the opposite condition:

``` csharp
if (_config.WelcomeBonus > 0f && !_storedData.Players[player.Id].WelcomeBonus)
```

Because the value was already `true`, the condition always evaluated to
**false**, preventing the welcome bonus from ever being awarded.

### Fix

New players now start with:

``` csharp
WelcomeBonus = false;
```

This correctly represents that the player **has not yet received the
welcome bonus**.

When the player connects for the first time, the plugin now:

1.  Detects that the player has not received the bonus
2.  Pays the configured welcome bonus
3.  Marks the player as having received the bonus
4.  Saves the updated player data

Example logic after the fix:

``` csharp
if (_config.WelcomeBonus > 0f && !_storedData.Players[player.Id].WelcomeBonus)
{
    Payout(player, _config.WelcomeBonus, GetLang("ReceivedWelcomeBonus", player.Id));
    _storedData.Players[player.Id].WelcomeBonus = true;
    SaveData();
}
```

------------------------------------------------------------------------

## 2. Immediate Data Save After Welcome Bonus

### Problem

In the original implementation, the plugin did **not immediately save
the data file** after awarding the welcome bonus.

This could potentially result in:

-   Duplicate welcome bonuses after a server crash
-   Bonus state not being recorded after a plugin reload
-   Player data inconsistencies

### Fix

The plugin now immediately saves the data file after the bonus is
granted:

``` csharp
SaveData();
```

This ensures that the player's welcome bonus status is stored right
away.

------------------------------------------------------------------------

## 3. Permission Update Null Safety

### Problem

The original plugin recalculated payouts when permissions changed, but
it did not verify that the player object actually existed.

Original code:

``` csharp
IPlayer player = players.FindPlayerById(user);
InitializePlayer(player, current);
```

If the player object was `null` (which can occur if the player is
offline), this could produce console errors.

### Fix

A null safety check was added before recalculating payouts:

``` csharp
IPlayer player = players.FindPlayerById(user);
if (player != null)
    InitializePlayer(player, current);
```

This prevents unnecessary plugin errors when permissions change for
offline players.

------------------------------------------------------------------------

# Result

After these fixes:

-   New players now receive the welcome bonus **exactly once**
-   Player data is saved immediately after receiving the bonus
-   Permission updates no longer risk null reference errors

All original configuration options and functionality remain unchanged.

------------------------------------------------------------------------

# Compatibility

These fixes are fully compatible with existing installations.

No changes are required to:

-   `MoneyTime.json` (configuration)
-   `oxide/data/MoneyTime.json` (player data)

Servers can simply replace the plugin file and reload the plugin.

------------------------------------------------------------------------

# Credits

**Original Plugin**\
Wulf -- MoneyTime

**Fixes Implemented By**\
SeesAll
