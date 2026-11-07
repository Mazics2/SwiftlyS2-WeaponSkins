using PanoramaHud.Contract;

using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

using WeaponSkins.Econ;
using WeaponSkins.Shared;

namespace WeaponSkins;

public partial class MenuService
{
    public void OpenMusicKitMenu(IPlayer player)
    {
        if (_hud == null) return;
        var builder = _hud.Menu($"ws.musickits.{player.SteamID}", LocalizationService[player].MenuTitleMusicKits);
        BuildMusicKitSubmenu(builder, player);
        builder.Build().Open(player);
    }

    public void BuildMusicKitSubmenu(IMenuBuilder builder, IPlayer player)
    {
        builder.Description("Müzik Kiti Seçiniz");

        // 1. Reset option
        builder.Button(LocalizationService[player].MenuReset, ctx =>
        {
            var p = ctx.Player;
            Api.ResetMusicKit(p.SteamID);
            _hud?.Toast(p, new ToastOptions
            {
                Title = "WeaponSkins",
                Text = "Müzik Kiti varsayılana sıfırlandı.",
                Kind = ToastKind.Warning,
                Key = "ws.musickit"
            });
            p.SendChat("[gold][WeaponSkins][/] Müzik Kiti varsayılana sıfırlandı.");
        });

        foreach (var musicKit in EconService.MusicKits.Values.OrderBy(mk => mk.Index))
        {
            var musicKitName = EconService.GetLocalizedName(musicKit.LocalizedNames, player.PlayerLanguage.Value);
            var index = musicKit.Index;

            builder.Button(musicKitName, ctx =>
            {
                var p = ctx.Player;
                Api.SetMusicKit(p.SteamID, index);
                _hud?.Toast(p, new ToastOptions
                {
                    Title = "WeaponSkins",
                    Text = $"{musicKitName} seçildi!",
                    Kind = ToastKind.Success,
                    Key = "ws.musickit"
                });
                p.SendChat($"[gold][WeaponSkins][/] [lime]{musicKitName}[/] seçildi!");
            });
        }
    }
}
