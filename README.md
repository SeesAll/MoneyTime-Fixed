# MoneyTime -- Welcome Bonus Fix

This repository contains a fixed version of the **MoneyTime** plugin
originally created by **Wulf** for Oxide/uMod.

The original plugin includes a **New Player Welcome Bonus** feature,
however due to a logic issue in the code the bonus was **never actually
awarded to new players**.

This version corrects that behavior while keeping the plugin's original
functionality intact.

------------------------------------------------------------------------

# The Problem

In the original plugin, new players were initialized with the following
value:

``` csharp
WelcomeBonus = true;
```

However, the payout logic checks for the opposite condition:

``` csharp
if (_config.WelcomeBonus > 0f && !_storedData.Players[player.Id].WelcomeBonus)
```

Because the value was already set to `true`, the condition always
evaluated to **false**, preventing the welcome bonus from ever being
paid.

As a result:

-   New players **never received the welcome bonus**
-   The configuration option appeared functional but **did nothing**

------------------------------------------------------------------------

# The Fix

The issue was corrected by properly initializing the welcome bonus
state.

### Change implemented

New players now start with:

``` csharp
WelcomeBonus = false;
```

This correctly represents that the player **has not yet received the
bonus**.

When a player connects for the first time, the plugin now:

1.  Detects the player has not received the bonus
2.  Pays the configured welcome bonus
3.  Marks the player as having received it
4.  Saves the updated data immediately

Example logic after fix:

``` csharp
if (_config.WelcomeBonus > 0f && !_storedData.Players[player.Id].WelcomeBonus)
{
    Payout(player, _config.WelcomeBonus, GetLang("ReceivedWelcomeBonus", player.Id));
    _storedData.Players[player.Id].WelcomeBonus = true;
    SaveData();
}
```

------------------------------------------------------------------------

# Result

After this fix:

-   New players receive the welcome bonus **exactly once**
-   Returning players **do not receive the bonus again**
-   The configuration option now works as originally intended

No other gameplay mechanics or configuration options were modified.

------------------------------------------------------------------------

# Compatibility

This fix is fully compatible with existing configurations.

No changes are required to:

-   `MoneyTime.json` config file
-   `oxide/data/MoneyTime.json` player data file

Servers can simply replace the plugin file and reload the plugin.

------------------------------------------------------------------------

# Credits

**Original Plugin**\
Wulf -- MoneyTime

**Fix Implemented By**\
SeesAll
