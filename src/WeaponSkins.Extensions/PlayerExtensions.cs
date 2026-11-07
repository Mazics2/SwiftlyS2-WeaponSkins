using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using WeaponSkins.Services;

namespace WeaponSkins.Extensions;

public static class PlayerExtensions
{
    [SwiftlyInject] private static ISwiftlyCore Core { get; set; } = null!;

    public static bool IsAlive(this IPlayer player)
    {
        return player.IsAlive;
    }

    public static void RegiveWeapon(this IPlayer player,
        CBasePlayerWeapon? weapon,
        ushort newIndex,
        bool forceSelect = false)
    {
        if (weapon is not { IsValid: true } || !player.IsAlive()) return;

        var pawn = player.PlayerPawn;
        if (pawn is not { IsValid: true }) return;
        if (pawn.WeaponServices is not { IsValid: true } weaponServices ||
            pawn.ItemServices is not { IsValid: true } itemServices) return;

        bool wasActive = forceSelect || (weaponServices.ActiveWeapon.Value != null &&
            (weaponServices.ActiveWeapon.Value.Address == weapon.Address ||
             weaponServices.ActiveWeapon.Value.AttributeManager.Item.ItemDefinitionIndex == newIndex));

        if (newIndex == Core.Helpers.GetDefinitionIndexByClassname("weapon_taser"))
        {
            player.RegiveTaser(weapon, wasActive);
            return;
        }

        var name = Core.Helpers.GetClassnameByDefinitionIndex(newIndex);
        if (string.IsNullOrWhiteSpace(name)) return;

        var clip1 = weapon.Clip1;
        var reservedAmmo = weapon.ReserveAmmo[0];

        // Eski silahi envanterden cikar ve dunyadan tamamen yok et
        weaponServices.RemoveWeapon(weapon);
        try
        {
            weapon.AcceptInput("Kill", "");
        }
        catch { }

        var newWeapon = itemServices.GiveItem<CBasePlayerWeapon>(name);
        if (newWeapon is not { IsValid: true }) return;

        newWeapon.Clip1 = clip1;
        newWeapon.ReserveAmmo[0] = reservedAmmo;

        try
        {
            newWeapon.AcceptInput("ChangeSubclass", newIndex.ToString());
        }
        catch { }

        // Skini aninda uygula
        try
        {
            HookInventoryUpdateService.Instance?.ApplyWeaponSkin(player.SteamID, player.Controller.Team, newWeapon);
        }
        catch { }

        if (wasActive)
        {
            // CS2 istemcisinin viewmodel kaplamasını/materyalini deploy döngüsüyle sıfırdan derlemesi için
            // anlık olarak bıçak yuvasına geçilir, sonraki tick'te yeni silah eline verilir.
            weaponServices.SelectWeaponBySlot(gear_slot_t.GEAR_SLOT_KNIFE);
            Core.Scheduler.NextTick(() =>
            {
                if (player.IsAlive() && newWeapon.IsValid)
                {
                    try
                    {
                        newWeapon.AcceptInput("ChangeSubclass", newIndex.ToString());
                        HookInventoryUpdateService.Instance?.ApplyWeaponSkin(player.SteamID, player.Controller.Team, newWeapon);
                    }
                    catch { }
                    player.PlayerPawn?.WeaponServices?.SelectWeapon(newWeapon);
                }
            });
        }
        else
        {
            Core.Scheduler.NextTick(() =>
            {
                if (player.IsAlive() && newWeapon.IsValid)
                {
                    try
                    {
                        newWeapon.AcceptInput("ChangeSubclass", newIndex.ToString());
                        HookInventoryUpdateService.Instance?.ApplyWeaponSkin(player.SteamID, player.Controller.Team, newWeapon);
                    }
                    catch { }
                }
            });
        }
    }

    public static void RegiveTaser(this IPlayer player,
        CBasePlayerWeapon? weapon,
        bool wasActive = false)
    {
        if (weapon is not { IsValid: true } || !player.IsAlive()) return;

        var pawn = player.PlayerPawn;
        if (pawn is not { IsValid: true }) return;
        if (pawn.WeaponServices is not { IsValid: true } weaponServices ||
            pawn.ItemServices is not { IsValid: true } itemServices) return;

        var oldTaser = weapon.As<CWeaponTaser>();
        var clip1 = oldTaser.Clip1;
        var reservedAmmo = oldTaser.ReserveAmmo[0];
        var fireTime = oldTaser.FireTime.Value;
        var lastAttackTick = oldTaser.LastAttackTick;

        weaponServices.RemoveWeapon(weapon);
        try
        {
            weapon.AcceptInput("Kill", "");
        }
        catch { }

        var newWeapon = itemServices.GiveItem<CWeaponTaser>("weapon_taser");
        if (newWeapon is not { IsValid: true }) return;

        newWeapon.Clip1 = clip1;
        newWeapon.ReserveAmmo[0] = reservedAmmo;
        newWeapon.FireTime.Value = fireTime;
        newWeapon.LastAttackTick = lastAttackTick;

        try
        {
            HookInventoryUpdateService.Instance?.ApplyWeaponSkin(player.SteamID, player.Controller.Team, newWeapon);
        }
        catch { }

        if (wasActive)
        {
            weaponServices.SelectWeapon(newWeapon);
            Core.Scheduler.NextTick(() =>
            {
                if (player.IsAlive() && newWeapon.IsValid)
                {
                    try
                    {
                        HookInventoryUpdateService.Instance?.ApplyWeaponSkin(player.SteamID, player.Controller.Team, newWeapon);
                    }
                    catch { }
                    player.PlayerPawn?.WeaponServices?.SelectWeapon(newWeapon);
                }
            });
        }
    }

    public static void RegiveKnife(this IPlayer player, bool selectKnife = true)
    {
        var pawn = player.PlayerPawn;
        if (pawn is not { IsValid: true }) return;
        var weaponServices = pawn.WeaponServices;
        var itemServices = pawn.ItemServices;
        if (weaponServices is not { IsValid: true } || itemServices is not { IsValid: true }) return;

        var activeWeapon = weaponServices.ActiveWeapon.Value;
        bool wasHoldingKnife = activeWeapon is { IsValid: true } &&
            (Utilities.IsKnifeDefinitionIndex(activeWeapon.AttributeManager.Item.ItemDefinitionIndex) ||
             activeWeapon.DesignerName?.Contains("knife") == true);

        // Bıçak yuvasındaki mevcut bıçağı kaldır ve öldür
        weaponServices.RemoveWeaponBySlot(gear_slot_t.GEAR_SLOT_KNIFE);
        if (activeWeapon is { IsValid: true } && wasHoldingKnife)
        {
            try
            {
                activeWeapon.AcceptInput("Kill", "");
            }
            catch { }
        }

        // Eğer oyuncu elinde bıçak tutuyorduysa, viewmodel animasyon/mesh sisteminin yeni bıçağı (subclass)
        // deploy etmesi için varsa başka bir silaha anlık geçiş yapılır. Başka silah yoksa (örn: surf sunucuları),
        // geçici bir dummy eşya verilerek viewmodel'in eski bıçağı unholster etmesi sağlanır.
        bool switchedToTemp = false;
        CBasePlayerWeapon? tempWeapon = null;
        if (wasHoldingKnife)
        {
            foreach (var handle in weaponServices.MyWeapons)
            {
                var w = handle.Value;
                if (w is { IsValid: true } && !Utilities.IsKnifeDefinitionIndex(w.AttributeManager.Item.ItemDefinitionIndex))
                {
                    try
                    {
                        weaponServices.SelectWeapon(w);
                        switchedToTemp = true;
                        break;
                    }
                    catch { }
                }
            }

            if (!switchedToTemp)
            {
                try
                {
                    tempWeapon = itemServices.GiveItem<CBasePlayerWeapon>("weapon_decoy");
                    if (tempWeapon is { IsValid: true })
                    {
                        weaponServices.SelectWeapon(tempWeapon);
                        switchedToTemp = true;
                    }
                }
                catch { }
            }
        }

        var newKnife = itemServices.GiveItem<CBasePlayerWeapon>("weapon_knife");
        if (newKnife is not { IsValid: true })
        {
            if (tempWeapon is { IsValid: true })
            {
                try { tempWeapon.AcceptInput("Kill", ""); } catch { }
            }
            return;
        }

        try
        {
            HookInventoryUpdateService.Instance?.ApplyWeaponSkin(player.SteamID, player.Controller.Team, newKnife);
        }
        catch { }

        if (selectKnife || wasHoldingKnife)
        {
            if (!switchedToTemp)
            {
                weaponServices.SelectWeaponBySlot(gear_slot_t.GEAR_SLOT_KNIFE);
                weaponServices.SelectWeapon(newKnife);
            }

            Core.Scheduler.NextTick(() =>
            {
                if (tempWeapon is { IsValid: true })
                {
                    try { tempWeapon.AcceptInput("Kill", ""); } catch { }
                }

                if (player.IsAlive() && player.PlayerPawn?.WeaponServices is { IsValid: true } ws)
                {
                    if (newKnife is { IsValid: true })
                    {
                        try
                        {
                            HookInventoryUpdateService.Instance?.ApplyWeaponSkin(player.SteamID, player.Controller.Team, newKnife);
                        }
                        catch { }

                        ws.SelectWeaponBySlot(gear_slot_t.GEAR_SLOT_KNIFE);
                        ws.SelectWeapon(newKnife);
                    }
                }
            });
        }
        else if (tempWeapon is { IsValid: true })
        {
            Core.Scheduler.NextTick(() =>
            {
                try { tempWeapon.AcceptInput("Kill", ""); } catch { }
            });
        }
    }
}
