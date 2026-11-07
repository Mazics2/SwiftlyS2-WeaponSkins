using PanoramaHud.Contract;

using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

using WeaponSkins.Econ;
using WeaponSkins.Shared;

namespace WeaponSkins;

public partial class MenuService
{
    public void OpenGloveSkinMenu(IPlayer player)
    {
        if (_hud == null) return;
        var builder = _hud.Menu($"ws.gloves.{player.SteamID}", LocalizationService[player].MenuTitleGloves);
        BuildGloveSkinSubmenu(builder, player);
        builder.Build().Open(player);
    }

    public void BuildGloveSkinSubmenu(IMenuBuilder builder, IPlayer player)
    {
        builder.Description("Eldiven Türü Seçiniz");

        foreach (var (gloveKey, paintkits) in EconService.WeaponToPaintkits)
        {
            if (!EconService.Items.TryGetValue(gloveKey, out var item))
            {
                continue;
            }

            if (!Utilities.IsGloveDefinitionIndex(item.Index))
            {
                continue;
            }

            var gloveName = EconService.GetLocalizedName(item.LocalizedNames, player.PlayerLanguage.Value);
            builder.Submenu(gloveName, sub => BuildGlovePaintkitsSubmenu(sub, player, item, paintkits));
        }
    }

    public void BuildGlovePaintkitsSubmenu(IMenuBuilder builder, IPlayer player, ItemDefinition item, List<PaintkitDefinition> paintkits)
    {
        var gloveName = EconService.GetLocalizedName(item.LocalizedNames, player.PlayerLanguage.Value);
        builder.Description($"{gloveName} Kaplaması Seçiniz");

        // 1. Reset option
        builder.Button(LocalizationService[player].MenuReset, ctx =>
        {
            var p = ctx.Player;
            Api.ResetGloveSkin(p.SteamID, p.Controller.Team, true);
            _hud?.Toast(p, new ToastOptions
            {
                Title = "WeaponSkins",
                Text = "Eldiven varsayılana sıfırlandı.",
                Kind = ToastKind.Warning,
                Key = "ws.glove"
            });
            p.SendChat("[gold][WeaponSkins][/] Eldiven varsayılana sıfırlandı.");
        });

        var sorted = paintkits.OrderByDescending(p => p.Rarity.Id).ToList();
        foreach (var paintkit in sorted)
        {
            var skinName = EconService.GetLocalizedName(paintkit.LocalizedNames, player.PlayerLanguage.Value);

            builder.Button(skinName, ctx =>
            {
                var p = ctx.Player;
                Api.UpdateGloveSkin(p.SteamID, p.Controller.Team, skin =>
                {
                    skin.DefinitionIndex = (ushort)item.Index;
                    skin.Paintkit = paintkit.Index;
                }, true);

                _hud?.Toast(p, new ToastOptions
                {
                    Title = "WeaponSkins",
                    Text = $"{skinName} seçildi!",
                    Kind = ToastKind.Success,
                    Key = "ws.glove"
                });

                p.SendChat($"[gold][WeaponSkins][/] [lime]{skinName}[/] seçildi!");
            });
        }
    }
}
