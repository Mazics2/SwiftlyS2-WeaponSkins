using System.Globalization;

using PanoramaHud.Contract;

using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

using WeaponSkins.Shared;

namespace WeaponSkins;

public partial class MenuService
{
    public void OpenKeychainPropertiesMenu(IPlayer player, int slot)
    {
        if (_hud == null || !TryGetWeaponDataInHand(player, out var dataInHand)) return;
        var builder = _hud.Menu($"ws.keychainprop.{player.SteamID}.{slot}", "Anahtarlık Özellikleri");
        BuildKeychainPropertiesSubmenu(builder, player, dataInHand, slot);
        builder.Build().Open(player);
    }

    public void BuildKeychainPropertiesSubmenu(IMenuBuilder builder, IPlayer player, WeaponSkinData data, int slot)
    {
        builder.Description("Anahtarlık Özellikleri");

        var keychain = data.GetKeychain(slot);
        if (keychain == null)
        {
            builder.Info("Anahtarlık bulunmuyor.");
            return;
        }

        // Seed
        builder.Button(
            p =>
            {
                var k = TryGetWeaponDataInHand(p, out var d) ? d.GetKeychain(slot) : keychain;
                return $"Seed: [{k?.Seed ?? keychain.Seed}]";
            },
            ctx =>
            {
                var p = ctx.Player;
                PromptChatInput(p, "Yeni anahtarlık seed değerini giriniz (Sayı):",
                    input => int.TryParse(input, out var v) && v >= 0,
                    (target, input) =>
                    {
                        var val = int.Parse(input);
                        Api.UpdateWeaponSkin(data.SteamID, data.Team, data.DefinitionIndex, skin =>
                        {
                            var k = skin.GetKeychain(slot);
                            if (k != null) k.Seed = val;
                        }, true);

                        _hud?.Toast(target, new ToastOptions
                        {
                            Title = "WeaponSkins",
                            Text = $"Anahtarlık seed {val} yapıldı!",
                            Kind = ToastKind.Success,
                            Key = "ws.keychain"
                        });
                        target.SendChat($"[gold][WeaponSkins][/] Anahtarlık seed [lime]{val}[/] yapıldı!");
                        OpenKeychainPropertiesMenu(target, slot);
                    },
                    onCancel: target => OpenKeychainPropertiesMenu(target, slot));
            });

        // Offset X
        builder.Button(
            p =>
            {
                var k = TryGetWeaponDataInHand(p, out var d) ? d.GetKeychain(slot) : keychain;
                return $"Offset X: [{(k?.OffsetX ?? keychain.OffsetX):F2}]";
            },
            ctx =>
            {
                var p = ctx.Player;
                PromptChatInput(p, "Yeni Offset X değerini giriniz (Örn: 0.1):",
                    input => float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && float.IsFinite(v),
                    (target, input) =>
                    {
                        var val = float.Parse(input, CultureInfo.InvariantCulture);
                        Api.UpdateWeaponSkin(data.SteamID, data.Team, data.DefinitionIndex, skin =>
                        {
                            var k = skin.GetKeychain(slot);
                            if (k != null) k.OffsetX = val;
                        }, true);

                        _hud?.Toast(target, new ToastOptions
                        {
                            Title = "WeaponSkins",
                            Text = $"Offset X: {val:F2} yapıldı!",
                            Kind = ToastKind.Success,
                            Key = "ws.keychain"
                        });
                        target.SendChat($"[gold][WeaponSkins][/] Offset X [lime]{val:F2}[/] yapıldı!");
                        OpenKeychainPropertiesMenu(target, slot);
                    },
                    onCancel: target => OpenKeychainPropertiesMenu(target, slot));
            });

        // Offset Y
        builder.Button(
            p =>
            {
                var k = TryGetWeaponDataInHand(p, out var d) ? d.GetKeychain(slot) : keychain;
                return $"Offset Y: [{(k?.OffsetY ?? keychain.OffsetY):F2}]";
            },
            ctx =>
            {
                var p = ctx.Player;
                PromptChatInput(p, "Yeni Offset Y değerini giriniz (Örn: 0.1):",
                    input => float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && float.IsFinite(v),
                    (target, input) =>
                    {
                        var val = float.Parse(input, CultureInfo.InvariantCulture);
                        Api.UpdateWeaponSkin(data.SteamID, data.Team, data.DefinitionIndex, skin =>
                        {
                            var k = skin.GetKeychain(slot);
                            if (k != null) k.OffsetY = val;
                        }, true);

                        _hud?.Toast(target, new ToastOptions
                        {
                            Title = "WeaponSkins",
                            Text = $"Offset Y: {val:F2} yapıldı!",
                            Kind = ToastKind.Success,
                            Key = "ws.keychain"
                        });
                        target.SendChat($"[gold][WeaponSkins][/] Offset Y [lime]{val:F2}[/] yapıldı!");
                        OpenKeychainPropertiesMenu(target, slot);
                    },
                    onCancel: target => OpenKeychainPropertiesMenu(target, slot));
            });

        // Offset Z
        builder.Button(
            p =>
            {
                var k = TryGetWeaponDataInHand(p, out var d) ? d.GetKeychain(slot) : keychain;
                return $"Offset Z: [{(k?.OffsetZ ?? keychain.OffsetZ):F2}]";
            },
            ctx =>
            {
                var p = ctx.Player;
                PromptChatInput(p, "Yeni Offset Z değerini giriniz (Örn: 0.1):",
                    input => float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && float.IsFinite(v),
                    (target, input) =>
                    {
                        var val = float.Parse(input, CultureInfo.InvariantCulture);
                        Api.UpdateWeaponSkin(data.SteamID, data.Team, data.DefinitionIndex, skin =>
                        {
                            var k = skin.GetKeychain(slot);
                            if (k != null) k.OffsetZ = val;
                        }, true);

                        _hud?.Toast(target, new ToastOptions
                        {
                            Title = "WeaponSkins",
                            Text = $"Offset Z: {val:F2} yapıldı!",
                            Kind = ToastKind.Success,
                            Key = "ws.keychain"
                        });
                        target.SendChat($"[gold][WeaponSkins][/] Offset Z [lime]{val:F2}[/] yapıldı!");
                        OpenKeychainPropertiesMenu(target, slot);
                    },
                    onCancel: target => OpenKeychainPropertiesMenu(target, slot));
            });

        // Reset Offset
        builder.Button(LocalizationService[player].MenuKeychainPropertiesResetOffset, ctx =>
        {
            var p = ctx.Player;
            Api.UpdateWeaponSkin(data.SteamID, data.Team, data.DefinitionIndex, skin =>
            {
                var k = skin.GetKeychain(slot);
                if (k != null)
                {
                    k.OffsetX = 0f;
                    k.OffsetY = 0f;
                    k.OffsetZ = 0f;
                }
            }, true);

            _hud?.Toast(p, new ToastOptions
            {
                Title = "WeaponSkins",
                Text = "Anahtarlık ofsetleri sıfırlandı.",
                Kind = ToastKind.Warning,
                Key = "ws.keychain"
            });
            p.SendChat("[gold][WeaponSkins][/] Anahtarlık ofsetleri sıfırlandı.");
            ctx.Refresh();
        });
    }
}
