using System.Globalization;

using PanoramaHud.Contract;

using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

using WeaponSkins.Shared;

namespace WeaponSkins;

public partial class MenuService
{
    public void OpenSkinPropertiesMenu(IPlayer player)
    {
        if (_hud == null) return;
        var builder = _hud.Menu($"ws.skinprops.{player.SteamID}", LocalizationService[player].MenuTitleSkinProperties);
        BuildSkinPropertiesSubmenu(builder, player);
        builder.Build().Open(player);
    }

    public void BuildSkinPropertiesSubmenu(IMenuBuilder builder, IPlayer player)
    {
        builder.Description("Özellik Seçiniz");

        if (!TryGetWeaponDataInHand(player, out var weaponInHand))
        {
            builder.Info("Lütfen önce elinize bir silah alınız!");
            return;
        }

        // 1. Aşınma (Wear)
        builder.Button(
            p => TryGetWeaponDataInHand(p, out var d) ? $"Aşınma: [{d.PaintkitWear:F2}]" : $"Aşınma: [{weaponInHand.PaintkitWear:F2}]",
            ctx =>
            {
                var p = ctx.Player;
                PromptChatInput(p, "Yeni aşınma değerini giriniz (0.00 - 1.00):",
                    input => float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v is >= 0.0f and <= 1.0f,
                    (target, input) =>
                    {
                        var val = float.Parse(input, CultureInfo.InvariantCulture);
                        Api.UpdateWeaponSkin(weaponInHand.SteamID, weaponInHand.Team, weaponInHand.DefinitionIndex, skin =>
                        {
                            skin.PaintkitWear = val;
                        }, true);

                        _hud?.Toast(target, new ToastOptions
                        {
                            Title = "WeaponSkins",
                            Text = $"Aşınma değeri {val:F2} olarak ayarlandı!",
                            Kind = ToastKind.Success,
                            Key = "ws.prop"
                        });
                        target.SendChat($"[gold][WeaponSkins][/] Aşınma değeri [lime]{val:F2}[/] olarak ayarlandı!");
                        OpenSkinPropertiesMenu(target);
                    },
                    onCancel: target => OpenSkinPropertiesMenu(target));
            });

        // 2. Desen No (Seed)
        builder.Button(
            p => TryGetWeaponDataInHand(p, out var d) ? $"Desen No (Seed): [{d.PaintkitSeed}]" : $"Desen No: [{weaponInHand.PaintkitSeed}]",
            ctx =>
            {
                var p = ctx.Player;
                PromptChatInput(p, "Yeni Seed değerini giriniz (Sayı):",
                    input => int.TryParse(input, out var v) && v >= 0,
                    (target, input) =>
                    {
                        var val = int.Parse(input);
                        Api.UpdateWeaponSkin(weaponInHand.SteamID, weaponInHand.Team, weaponInHand.DefinitionIndex, skin =>
                        {
                            skin.PaintkitSeed = val;
                        }, true);

                        _hud?.Toast(target, new ToastOptions
                        {
                            Title = "WeaponSkins",
                            Text = $"Seed değeri {val} olarak ayarlandı!",
                            Kind = ToastKind.Success,
                            Key = "ws.prop"
                        });
                        target.SendChat($"[gold][WeaponSkins][/] Seed değeri [lime]{val}[/] olarak ayarlandı!");
                        OpenSkinPropertiesMenu(target);
                    },
                    onCancel: target => OpenSkinPropertiesMenu(target));
            });

        // 3. İsim Etiketi (Nametag)
        builder.Button(
            p =>
            {
                var current = TryGetWeaponDataInHand(p, out var d) ? d.Nametag : weaponInHand.Nametag;
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
                        Api.UpdateWeaponSkin(weaponInHand.SteamID, weaponInHand.Team, weaponInHand.DefinitionIndex, skin =>
                        {
                            skin.Nametag = input;
                        }, true);

                        _hud?.Toast(target, new ToastOptions
                        {
                            Title = "WeaponSkins",
                            Text = $"İsim etiketi {input} yapıldı!",
                            Kind = ToastKind.Success,
                            Key = "ws.prop"
                        });
                        target.SendChat($"[gold][WeaponSkins][/] İsim etiketi [lime]{input}[/] yapıldı!");
                        OpenSkinPropertiesMenu(target);
                    },
                    onCancel: target => OpenSkinPropertiesMenu(target));
            });

        // 4. İsim Etiketini Kaldır
        builder.Button(LocalizationService[player].MenuSkinPropertiesNametagUnset, ctx =>
        {
            var p = ctx.Player;
            Api.UpdateWeaponSkin(weaponInHand.SteamID, weaponInHand.Team, weaponInHand.DefinitionIndex, skin =>
            {
                skin.Nametag = null;
            }, true);

            _hud?.Toast(p, new ToastOptions
            {
                Title = "WeaponSkins",
                Text = "İsim etiketi kaldırıldı.",
                Kind = ToastKind.Warning,
                Key = "ws.prop"
            });
            p.SendChat("[gold][WeaponSkins][/] İsim etiketi kaldırıldı.");
            ctx.Refresh();
        });

        // 5. StatTrak Toggle
        builder.Toggle("StatTrak™",
            get: p => (TryGetWeaponDataInHand(p, out var d) ? d.Quality : weaponInHand.Quality) == EconItemQuality.StatTrak,
            set: (ctx, val) =>
            {
                var p = ctx.Player;
                Api.UpdateWeaponSkin(weaponInHand.SteamID, weaponInHand.Team, weaponInHand.DefinitionIndex, skin =>
                {
                    skin.Quality = val ? EconItemQuality.StatTrak : EconItemQuality.Normal;
                }, true);

                _hud?.Toast(p, new ToastOptions
                {
                    Title = "StatTrak™",
                    Text = val ? "Açık" : "Kapalı",
                    Kind = val ? ToastKind.Success : ToastKind.Info,
                    Key = "ws.prop"
                });
                p.SendChat($"[gold][WeaponSkins][/] StatTrak™: {(val ? "[lime]Açık[/]" : "[red]Kapalı[/]")}");
            });

        // 6. StatTrak Sayısı
        builder.Button(
            p => TryGetWeaponDataInHand(p, out var d) ? $"StatTrak™ Sayısı: [{d.StattrakCount}]" : $"StatTrak™: [{weaponInHand.StattrakCount}]",
            ctx =>
            {
                var p = ctx.Player;
                PromptChatInput(p, "Yeni StatTrak™ leş sayısını giriniz:",
                    input => int.TryParse(input, out var v) && v >= 0,
                    (target, input) =>
                    {
                        var val = int.Parse(input);
                        Api.UpdateWeaponSkin(weaponInHand.SteamID, weaponInHand.Team, weaponInHand.DefinitionIndex, skin =>
                        {
                            skin.StattrakCount = val;
                        }, true);

                        _hud?.Toast(target, new ToastOptions
                        {
                            Title = "StatTrak™",
                            Text = $"StatTrak™ sayısı {val} yapıldı!",
                            Kind = ToastKind.Success,
                            Key = "ws.prop"
                        });
                        target.SendChat($"[gold][WeaponSkins][/] StatTrak™ sayısı [lime]{val}[/] yapıldı!");
                        OpenSkinPropertiesMenu(target);
                    },
                    onCancel: target => OpenSkinPropertiesMenu(target));
            });

        // 7. Hatıra (Souvenir) Toggle
        builder.Toggle("Hatıra (Souvenir)",
            get: p => (TryGetWeaponDataInHand(p, out var d) ? d.Quality : weaponInHand.Quality) == EconItemQuality.Souvenir,
            set: (ctx, val) =>
            {
                var p = ctx.Player;
                Api.UpdateWeaponSkin(weaponInHand.SteamID, weaponInHand.Team, weaponInHand.DefinitionIndex, skin =>
                {
                    skin.Quality = val ? EconItemQuality.Souvenir : EconItemQuality.Normal;
                }, true);

                _hud?.Toast(p, new ToastOptions
                {
                    Title = "Hatıra (Souvenir)",
                    Text = val ? "Açık" : "Kapalı",
                    Kind = val ? ToastKind.Success : ToastKind.Info,
                    Key = "ws.prop"
                });
                p.SendChat($"[gold][WeaponSkins][/] Hatıra (Souvenir): {(val ? "[lime]Açık[/]" : "[red]Kapalı[/]")}");
            });

        // 8. Stickers Properties Submenus (slots 0-4)
        for (int i = 0; i < 5; i++)
        {
            int slot = i;
            var sticker = weaponInHand.GetSticker(slot);
            if (sticker != null && sticker.Id != 0)
            {
                var sName = GetStickerName(sticker, player.PlayerLanguage.Value) ?? $"Slot {slot + 1}";
                builder.Submenu($"Çıkartma {slot + 1} ({sName}) Ayarları", sub => BuildStickerPropertiesSubmenu(sub, player, weaponInHand, slot));
            }
        }

        // 9. Keychain Properties Submenu
        var keychain = weaponInHand.GetKeychain(0);
        if (keychain != null && keychain.Id != 0)
        {
            var kName = GetKeychainName(keychain, player.PlayerLanguage.Value) ?? "Anahtarlık";
            builder.Submenu($"Anahtarlık ({kName}) Ayarları", sub => BuildKeychainPropertiesSubmenu(sub, player, weaponInHand, 0));
        }
    }
}
