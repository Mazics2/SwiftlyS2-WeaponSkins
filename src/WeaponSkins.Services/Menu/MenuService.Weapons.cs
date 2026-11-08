using PanoramaHud.Contract;

using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

using WeaponSkins.Econ;
using WeaponSkins.Shared;

namespace WeaponSkins;

public partial class MenuService
{
    public void OpenWeaponSkinMenu(IPlayer player)
    {
        if (_menuApi != null)
        {
            OpenMenuApiWeaponsRootMenu(player);
            return;
        }
        if (_hud == null) return;
        var builder = _hud.Menu($"ws.weapons.{player.SteamID}", LocalizationService[player].MenuTitleSkins);
        BuildWeaponSkinSubmenu(builder, player);
        builder.Build().Open(player);
    }

    public void BuildWeaponSkinSubmenu(IMenuBuilder builder, IPlayer player)
    {
        builder.Description("Silah Seçiniz");

        foreach (var (weaponKey, paintkits) in EconService.WeaponToPaintkits)
        {
            if (!EconService.Items.TryGetValue(weaponKey, out var item))
            {
                continue;
            }

            if (!Utilities.IsWeaponDefinitionIndex(item.Index))
            {
                continue;
            }

            var weaponName = EconService.GetLocalizedName(item.LocalizedNames, player.PlayerLanguage.Value);
            builder.Submenu(weaponName, sub => BuildWeaponPaintkitsSubmenu(sub, player, item, paintkits));
        }
    }

    public void BuildWeaponPaintkitsSubmenu(IMenuBuilder builder, IPlayer player, ItemDefinition item, List<PaintkitDefinition> paintkits)
    {
        var weaponName = EconService.GetLocalizedName(item.LocalizedNames, player.PlayerLanguage.Value);
        builder.Description($"{weaponName} Kaplaması Seçiniz");

        // 1. Reset option
        builder.Button(LocalizationService[player].MenuReset, ctx =>
        {
            var p = ctx.Player;
            Api.ResetWeaponSkin(p.SteamID, p.Controller.Team, (ushort)item.Index, true);
            _hud?.Toast(p, new ToastOptions
            {
                Title = "WeaponSkins",
                Text = $"{weaponName} kaplaması sıfırlandı.",
                Kind = ToastKind.Warning,
                Key = "ws.skin"
            });
            p.SendChat($"[gold][WeaponSkins][/] {weaponName} kaplaması sıfırlandı.");
        });

        var sorted = paintkits.OrderByDescending(p => p.Rarity.Id).ToList();
        foreach (var paintkit in sorted)
        {
            var skinName = EconService.GetLocalizedName(paintkit.LocalizedNames, player.PlayerLanguage.Value);

            builder.Button(skinName, ctx =>
            {
                var p = ctx.Player;
                Api.UpdateWeaponSkin(p.SteamID, p.Controller.Team, (ushort)item.Index, skin =>
                {
                    skin.Paintkit = paintkit.Index;
                }, true);

                _hud?.Toast(p, new ToastOptions
                {
                    Title = "WeaponSkins",
                    Text = $"{skinName} seçildi!",
                    Kind = ToastKind.Success,
                    Key = "ws.skin"
                });

                if (p.IsAlive)
                {
                    var activeWep = p.PlayerPawn?.WeaponServices?.ActiveWeapon.Value;
                    if (activeWep is { IsValid: true } &&
                        activeWep.AttributeManager.Item.ItemDefinitionIndex == (ushort)item.Index)
                    {
                        p.SendChat($"[gold][WeaponSkins][/] [lime]{skinName}[/] seçildi ve elinize verildi!");
                    }
                    else
                    {
                        p.SendChat($"[gold][WeaponSkins][/] [lime]{skinName}[/] seçildi!");
                    }
                }
            });
        }
    }
}
