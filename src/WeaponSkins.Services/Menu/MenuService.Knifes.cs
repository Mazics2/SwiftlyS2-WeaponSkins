using PanoramaHud.Contract;

using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

using WeaponSkins.Econ;
using WeaponSkins.Shared;

namespace WeaponSkins;

public partial class MenuService
{
    public void OpenKnifeSkinMenu(IPlayer player)
    {
        if (_menuApi != null)
        {
            OpenMenuApiKnivesMenu(player);
            return;
        }
        if (_hud == null) return;
        var builder = _hud.Menu($"ws.knives.{player.SteamID}", LocalizationService[player].MenuTitleKnifes);
        BuildKnifeSkinSubmenu(builder, player);
        builder.Build().Open(player);
    }

    public void BuildKnifeSkinSubmenu(IMenuBuilder builder, IPlayer player)
    {
        builder.Description("Bıçak Türü Seçiniz");

        foreach (var (knifeKey, paintkits) in EconService.WeaponToPaintkits)
        {
            if (!EconService.Items.TryGetValue(knifeKey, out var item))
            {
                continue;
            }

            if (!Utilities.IsKnifeDefinitionIndex(item.Index))
            {
                continue;
            }

            var knifeName = EconService.GetLocalizedName(item.LocalizedNames, player.PlayerLanguage.Value);
            builder.Submenu(knifeName, sub => BuildKnifePaintkitsSubmenu(sub, player, item, paintkits));
        }
    }

    public void BuildKnifePaintkitsSubmenu(IMenuBuilder builder, IPlayer player, ItemDefinition item, List<PaintkitDefinition> paintkits)
    {
        var knifeName = EconService.GetLocalizedName(item.LocalizedNames, player.PlayerLanguage.Value);
        builder.Description($"{knifeName} Kaplaması Seçiniz");

        // 1. Reset option
        builder.Button(LocalizationService[player].MenuReset, ctx =>
        {
            var p = ctx.Player;
            Api.ResetKnifeSkin(p.SteamID, p.Controller.Team, true);
            _hud?.Toast(p, new ToastOptions
            {
                Title = "WeaponSkins",
                Text = "Bıçak varsayılana sıfırlandı.",
                Kind = ToastKind.Warning,
                Key = "ws.knife"
            });
            p.SendChat("[gold][WeaponSkins][/] Bıçak varsayılana sıfırlandı.");
        });

        var sorted = paintkits.OrderByDescending(p => p.Rarity.Id).ToList();
        foreach (var paintkit in sorted)
        {
            var skinName = EconService.GetLocalizedName(paintkit.LocalizedNames, player.PlayerLanguage.Value);

            builder.Button(skinName, ctx =>
            {
                var p = ctx.Player;
                Api.UpdateKnifeSkin(p.SteamID, p.Controller.Team, knife =>
                {
                    knife.DefinitionIndex = (ushort)item.Index;
                    knife.Paintkit = paintkit.Index;
                }, true);

                _hud?.Toast(p, new ToastOptions
                {
                    Title = "WeaponSkins",
                    Text = $"{skinName} seçildi!",
                    Kind = ToastKind.Success,
                    Key = "ws.knife"
                });

                if (p.IsAlive)
                {
                    var activeWep = p.PlayerPawn?.WeaponServices?.ActiveWeapon.Value;
                    bool isHoldingKnife = activeWep is { IsValid: true } &&
                        (Utilities.IsKnifeDefinitionIndex(activeWep.AttributeManager.Item.ItemDefinitionIndex) ||
                         activeWep.DesignerName?.Contains("knife") == true);

                    if (isHoldingKnife)
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
