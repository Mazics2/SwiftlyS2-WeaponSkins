using System.Globalization;

using PanoramaHud.Contract;

using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

using WeaponSkins.Shared;

namespace WeaponSkins;

public partial class MenuService
{
    public void OpenGlovePropertiesMenu(IPlayer player)
    {
        if (_hud == null) return;
        var builder = _hud.Menu($"ws.gloveprops.{player.SteamID}", LocalizationService[player].MenuTitleGloveProperties);
        BuildGlovePropertiesSubmenu(builder, player);
        builder.Build().Open(player);
    }

    public void BuildGlovePropertiesSubmenu(IMenuBuilder builder, IPlayer player)
    {
        builder.Description("Özellik Seçiniz");

        if (!TryGetGloveDataInHand(player, out var gloveInHand))
        {
            builder.Info("Kayıtlı eldiven verisi bulunamadı!");
            return;
        }

        // 1. Aşınma (Wear)
        builder.Button(
            p => TryGetGloveDataInHand(p, out var d) ? $"Aşınma: [{d.PaintkitWear:F2}]" : $"Aşınma: [{gloveInHand.PaintkitWear:F2}]",
            ctx =>
            {
                var p = ctx.Player;
                PromptChatInput(p, "Yeni eldiven aşınmasını giriniz (0.00 - 1.00):",
                    input => float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v is >= 0.0f and <= 1.0f,
                    (target, input) =>
                    {
                        var val = float.Parse(input, CultureInfo.InvariantCulture);
                        Api.UpdateGloveSkin(gloveInHand.SteamID, gloveInHand.Team, skin =>
                        {
                            skin.PaintkitWear = val;
                        }, true);

                        _hud?.Toast(target, new ToastOptions
                        {
                            Title = "WeaponSkins",
                            Text = $"Eldiven aşınması {val:F2} yapıldı!",
                            Kind = ToastKind.Success,
                            Key = "ws.glove"
                        });
                        target.SendChat($"[gold][WeaponSkins][/] Eldiven aşınması [lime]{val:F2}[/] yapıldı!");
                        OpenGlovePropertiesMenu(target);
                    },
                    onCancel: target => OpenGlovePropertiesMenu(target));
            });

        // 2. Desen No (Seed)
        builder.Button(
            p => TryGetGloveDataInHand(p, out var d) ? $"Desen No (Seed): [{d.PaintkitSeed}]" : $"Desen No: [{gloveInHand.PaintkitSeed}]",
            ctx =>
            {
                var p = ctx.Player;
                PromptChatInput(p, "Yeni Seed değerini giriniz (Sayı):",
                    input => int.TryParse(input, out var v) && v >= 0,
                    (target, input) =>
                    {
                        var val = int.Parse(input);
                        Api.UpdateGloveSkin(gloveInHand.SteamID, gloveInHand.Team, skin =>
                        {
                            skin.PaintkitSeed = val;
                        }, true);

                        _hud?.Toast(target, new ToastOptions
                        {
                            Title = "WeaponSkins",
                            Text = $"Eldiven seed değeri {val} yapıldı!",
                            Kind = ToastKind.Success,
                            Key = "ws.glove"
                        });
                        target.SendChat($"[gold][WeaponSkins][/] Eldiven seed değeri [lime]{val}[/] yapıldı!");
                        OpenGlovePropertiesMenu(target);
                    },
                    onCancel: target => OpenGlovePropertiesMenu(target));
            });
    }
}
