using System.Globalization;

using PanoramaHud.Contract;

using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

using WeaponSkins.Shared;

namespace WeaponSkins;

public partial class MenuService
{
    public void OpenStickerPropertiesMenu(IPlayer player, int slot)
    {
        if (_hud == null || !TryGetWeaponDataInHand(player, out var dataInHand)) return;
        var builder = _hud.Menu($"ws.stickerprop.{player.SteamID}.{slot}", $"Çıkartma {slot + 1} Özellikleri");
        BuildStickerPropertiesSubmenu(builder, player, dataInHand, slot);
        builder.Build().Open(player);
    }

    public void BuildStickerPropertiesSubmenu(IMenuBuilder builder, IPlayer player, WeaponSkinData data, int slot)
    {
        builder.Description($"Çıkartma {slot + 1} Özellikleri");

        var sticker = data.GetSticker(slot);
        if (sticker == null)
        {
            builder.Info("Bu slotta çıkartma bulunmuyor.");
            return;
        }

        // Aşınma (Wear)
        builder.Button(
            p =>
            {
                var s = TryGetWeaponDataInHand(p, out var d) ? d.GetSticker(slot) : sticker;
                return $"Aşınma: [{(s?.Wear ?? sticker.Wear):F2}]";
            },
            ctx =>
            {
                var p = ctx.Player;
                PromptChatInput(p, "Yeni çıkartma aşınmasını giriniz (0.00 - 1.00):",
                    input => float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v is >= 0.0f and <= 1.0f,
                    (target, input) =>
                    {
                        var val = float.Parse(input, CultureInfo.InvariantCulture);
                        Api.UpdateWeaponSkin(data.SteamID, data.Team, data.DefinitionIndex, skin =>
                        {
                            var s = skin.GetSticker(slot);
                            if (s != null) s.Wear = val;
                        }, true);

                        _hud?.Toast(target, new ToastOptions
                        {
                            Title = "WeaponSkins",
                            Text = $"Çıkartma {slot + 1} aşınması {val:F2} yapıldı!",
                            Kind = ToastKind.Success,
                            Key = "ws.sticker"
                        });
                        target.SendChat($"[gold][WeaponSkins][/] Çıkartma {slot + 1} aşınması [lime]{val:F2}[/] yapıldı!");
                        OpenStickerPropertiesMenu(target, slot);
                    },
                    onCancel: target => OpenStickerPropertiesMenu(target, slot));
            });

        // Offset X
        builder.Button(
            p =>
            {
                var s = TryGetWeaponDataInHand(p, out var d) ? d.GetSticker(slot) : sticker;
                return $"Offset X: [{(s?.OffsetX ?? sticker.OffsetX):F2}]";
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
                            var s = skin.GetSticker(slot);
                            if (s != null)
                            {
                                s.OffsetX = val;
                                s.Schema = 0;
                            }
                        }, true);

                        _hud?.Toast(target, new ToastOptions
                        {
                            Title = "WeaponSkins",
                            Text = $"Offset X: {val:F2} yapıldı!",
                            Kind = ToastKind.Success,
                            Key = "ws.sticker"
                        });
                        target.SendChat($"[gold][WeaponSkins][/] Offset X [lime]{val:F2}[/] yapıldı!");
                        OpenStickerPropertiesMenu(target, slot);
                    },
                    onCancel: target => OpenStickerPropertiesMenu(target, slot));
            });

        // Offset Y
        builder.Button(
            p =>
            {
                var s = TryGetWeaponDataInHand(p, out var d) ? d.GetSticker(slot) : sticker;
                return $"Offset Y: [{(s?.OffsetY ?? sticker.OffsetY):F2}]";
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
                            var s = skin.GetSticker(slot);
                            if (s != null)
                            {
                                s.OffsetY = val;
                                s.Schema = 0;
                            }
                        }, true);

                        _hud?.Toast(target, new ToastOptions
                        {
                            Title = "WeaponSkins",
                            Text = $"Offset Y: {val:F2} yapıldı!",
                            Kind = ToastKind.Success,
                            Key = "ws.sticker"
                        });
                        target.SendChat($"[gold][WeaponSkins][/] Offset Y [lime]{val:F2}[/] yapıldı!");
                        OpenStickerPropertiesMenu(target, slot);
                    },
                    onCancel: target => OpenStickerPropertiesMenu(target, slot));
            });

        // Reset Offset
        builder.Button(LocalizationService[player].MenuStickerPropertiesResetOffset, ctx =>
        {
            var p = ctx.Player;
            Api.UpdateWeaponSkin(data.SteamID, data.Team, data.DefinitionIndex, skin =>
            {
                var s = skin.GetSticker(slot);
                if (s != null)
                {
                    s.OffsetX = 0f;
                    s.OffsetY = 0f;
                    s.Schema = 0;
                }
            }, true);

            _hud?.Toast(p, new ToastOptions
            {
                Title = "WeaponSkins",
                Text = $"Çıkartma {slot + 1} ofsetleri sıfırlandı.",
                Kind = ToastKind.Warning,
                Key = "ws.sticker"
            });
            p.SendChat($"[gold][WeaponSkins][/] Çıkartma {slot + 1} ofsetleri sıfırlandı.");
            ctx.Refresh();
        });
    }
}
