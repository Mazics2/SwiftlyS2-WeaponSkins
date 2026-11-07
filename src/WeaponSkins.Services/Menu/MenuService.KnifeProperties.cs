using System.Globalization;

using PanoramaHud.Contract;

using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

using WeaponSkins.Shared;

namespace WeaponSkins;

public partial class MenuService
{
    public void OpenKnifePropertiesMenu(IPlayer player)
    {
        if (_hud == null) return;
        var builder = _hud.Menu($"ws.knifeprops.{player.SteamID}", LocalizationService[player].MenuTitleKnifeProperties);
        BuildKnifePropertiesSubmenu(builder, player);
        builder.Build().Open(player);
    }

    public void BuildKnifePropertiesSubmenu(IMenuBuilder builder, IPlayer player)
    {
        builder.Description("Özellik Seçiniz");

        if (!TryGetKnifeDataInHand(player, out var knifeInHand))
        {
            builder.Info("Kayıtlı bıçak verisi bulunamadı!");
            return;
        }

        // 1. Aşınma (Wear)
        builder.Button(
            p => TryGetKnifeDataInHand(p, out var d) ? $"Aşınma: [{d.PaintkitWear:F2}]" : $"Aşınma: [{knifeInHand.PaintkitWear:F2}]",
            ctx =>
            {
                var p = ctx.Player;
                PromptChatInput(p, "Yeni bıçak aşınmasını giriniz (0.00 - 1.00):",
                    input => float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v is >= 0.0f and <= 1.0f,
                    (target, input) =>
                    {
                        var val = float.Parse(input, CultureInfo.InvariantCulture);
                        Api.UpdateKnifeSkin(knifeInHand.SteamID, knifeInHand.Team, skin =>
                        {
                            skin.PaintkitWear = val;
                        }, true);

                        _hud?.Toast(target, new ToastOptions
                        {
                            Title = "WeaponSkins",
                            Text = $"Bıçak aşınması {val:F2} yapıldı!",
                            Kind = ToastKind.Success,
                            Key = "ws.knife"
                        });
                        target.SendChat($"[gold][WeaponSkins][/] Bıçak aşınması [lime]{val:F2}[/] yapıldı!");
                        OpenKnifePropertiesMenu(target);
                    },
                    onCancel: target => OpenKnifePropertiesMenu(target));
            });

        // 2. Desen No (Seed)
        builder.Button(
            p => TryGetKnifeDataInHand(p, out var d) ? $"Desen No (Seed): [{d.PaintkitSeed}]" : $"Desen No: [{knifeInHand.PaintkitSeed}]",
            ctx =>
            {
                var p = ctx.Player;
                PromptChatInput(p, "Yeni Seed değerini giriniz (Sayı):",
                    input => int.TryParse(input, out var v) && v >= 0,
                    (target, input) =>
                    {
                        var val = int.Parse(input);
                        Api.UpdateKnifeSkin(knifeInHand.SteamID, knifeInHand.Team, skin =>
                        {
                            skin.PaintkitSeed = val;
                        }, true);

                        _hud?.Toast(target, new ToastOptions
                        {
                            Title = "WeaponSkins",
                            Text = $"Bıçak seed değeri {val} yapıldı!",
                            Kind = ToastKind.Success,
                            Key = "ws.knife"
                        });
                        target.SendChat($"[gold][WeaponSkins][/] Bıçak seed değeri [lime]{val}[/] yapıldı!");
                        OpenKnifePropertiesMenu(target);
                    },
                    onCancel: target => OpenKnifePropertiesMenu(target));
            });

        // 3. İsim Etiketi (Nametag)
        builder.Button(
            p =>
            {
                var current = TryGetKnifeDataInHand(p, out var d) ? d.Nametag : knifeInHand.Nametag;
                var display = string.IsNullOrWhiteSpace(current) ? LocalizationService[p].MenuSkinPropertiesNametagNone : current;
                return $"İsim Etiketi: [{display}]";
            },
            ctx =>
            {
                var p = ctx.Player;
                PromptChatInput(p, "Yeni isim etiketini yazınız:",
                    input => !string.IsNullOrWhiteSpace(input) && input.Length <= 32,
                    (target, input) =>
                    {
                        Api.UpdateKnifeSkin(knifeInHand.SteamID, knifeInHand.Team, skin =>
                        {
                            skin.Nametag = input;
                        }, true);

                        _hud?.Toast(target, new ToastOptions
                        {
                            Title = "WeaponSkins",
                            Text = $"Bıçak isim etiketi {input} yapıldı!",
                            Kind = ToastKind.Success,
                            Key = "ws.knife"
                        });
                        target.SendChat($"[gold][WeaponSkins][/] Bıçak isim etiketi [lime]{input}[/] yapıldı!");
                        OpenKnifePropertiesMenu(target);
                    },
                    onCancel: target => OpenKnifePropertiesMenu(target));
            });

        // 4. İsim Etiketini Kaldır
        builder.Button(LocalizationService[player].MenuSkinPropertiesNametagUnset, ctx =>
        {
            var p = ctx.Player;
            Api.UpdateKnifeSkin(knifeInHand.SteamID, knifeInHand.Team, skin =>
            {
                skin.Nametag = null;
            }, true);

            _hud?.Toast(p, new ToastOptions
            {
                Title = "WeaponSkins",
                Text = "Bıçak isim etiketi kaldırıldı.",
                Kind = ToastKind.Warning,
                Key = "ws.knife"
            });
            p.SendChat("[gold][WeaponSkins][/] Bıçak isim etiketi kaldırıldı.");
            ctx.Refresh();
        });

        // 5. StatTrak Toggle
        builder.Toggle("StatTrak™",
            get: p => (TryGetKnifeDataInHand(p, out var d) ? d.Quality : knifeInHand.Quality) == EconItemQuality.StatTrak,
            set: (ctx, val) =>
            {
                var p = ctx.Player;
                Api.UpdateKnifeSkin(knifeInHand.SteamID, knifeInHand.Team, skin =>
                {
                    skin.Quality = val ? EconItemQuality.StatTrak : EconItemQuality.Normal;
                }, true);

                _hud?.Toast(p, new ToastOptions
                {
                    Title = "StatTrak™",
                    Text = val ? "Açık" : "Kapalı",
                    Kind = val ? ToastKind.Success : ToastKind.Info,
                    Key = "ws.knife"
                });
                p.SendChat($"[gold][WeaponSkins][/] Bıçak StatTrak™: {(val ? "[lime]Açık[/]" : "[red]Kapalı[/]")}");
            });

        // 6. StatTrak Sayısı
        builder.Button(
            p => TryGetKnifeDataInHand(p, out var d) ? $"StatTrak™ Sayısı: [{d.StattrakCount}]" : $"StatTrak™: [{knifeInHand.StattrakCount}]",
            ctx =>
            {
                var p = ctx.Player;
                PromptChatInput(p, "Yeni StatTrak™ leş sayısını giriniz:",
                    input => int.TryParse(input, out var v) && v >= 0,
                    (target, input) =>
                    {
                        var val = int.Parse(input);
                        Api.UpdateKnifeSkin(knifeInHand.SteamID, knifeInHand.Team, skin =>
                        {
                            skin.StattrakCount = val;
                        }, true);

                        _hud?.Toast(target, new ToastOptions
                        {
                            Title = "StatTrak™",
                            Text = $"StatTrak™ sayısı {val} yapıldı!",
                            Kind = ToastKind.Success,
                            Key = "ws.knife"
                        });
                        target.SendChat($"[gold][WeaponSkins][/] Bıçak StatTrak™ sayısı [lime]{val}[/] yapıldı!");
                        OpenKnifePropertiesMenu(target);
                    },
                    onCancel: target => OpenKnifePropertiesMenu(target));
            });
    }
}
