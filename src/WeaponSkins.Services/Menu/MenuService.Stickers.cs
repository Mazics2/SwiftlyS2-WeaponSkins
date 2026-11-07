using PanoramaHud.Contract;

using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

using WeaponSkins.Econ;
using WeaponSkins.Shared;

namespace WeaponSkins;

public partial class MenuService
{
    private readonly Dictionary<int, StickerDefinition> _cachedStickerDefinitions = new();

    private string? GetStickerName(StickerData? data, string language)
    {
        if (data == null || data.Id == 0) return null;
        if (!_cachedStickerDefinitions.TryGetValue(data.Id, out var definition))
        {
            foreach (var (_, stickers) in EconService.StickerCollections)
            {
                foreach (var sticker in stickers.Stickers)
                {
                    if (sticker.Index == data.Id)
                    {
                        definition = sticker;
                        _cachedStickerDefinitions[data.Id] = definition;
                        break;
                    }
                }
                if (definition != null) break;
            }
        }

        if (definition == null) return null;
        return EconService.GetLocalizedName(definition.LocalizedNames, language);
    }

    public void OpenStickerMenu(IPlayer player)
    {
        if (_hud == null) return;
        var builder = _hud.Menu($"ws.stickers.{player.SteamID}", LocalizationService[player].MenuTitleStickers);
        BuildStickerSubmenu(builder, player);
        builder.Build().Open(player);
    }

    public void BuildStickerSubmenu(IMenuBuilder builder, IPlayer player)
    {
        builder.Description("Çıkartma Slotu Seçiniz");

        if (!TryGetWeaponDataInHand(player, out var dataInHand))
        {
            builder.Info("Lütfen önce elinize bir silah alınız!");
            return;
        }

        for (int i = 0; i < 5; i++)
        {
            int slot = i;
            var currentSticker = dataInHand.GetSticker(slot);
            var stickerName = GetStickerName(currentSticker, player.PlayerLanguage.Value);
            var slotTitle = $"Slot {slot + 1}: [ {(stickerName ?? "Boş")} ]";

            builder.Submenu(slotTitle, sub => BuildStickerCollectionsSubmenu(sub, player, dataInHand, slot));
        }
    }

    public void BuildStickerCollectionsSubmenu(IMenuBuilder builder, IPlayer player, WeaponSkinData weaponData, int slot)
    {
        builder.Description($"Slot {slot + 1} Koleksiyon Seçiniz");

        // 1. Reset option
        builder.Button(LocalizationService[player].MenuReset, ctx =>
        {
            var p = ctx.Player;
            Api.UpdateWeaponSkin(p.SteamID, p.Controller.Team, weaponData.DefinitionIndex, skin =>
            {
                skin.SetSticker(slot, null);
            }, true);

            _hud?.Toast(p, new ToastOptions
            {
                Title = "WeaponSkins",
                Text = $"Slot {slot + 1} çıkartması kaldırıldı.",
                Kind = ToastKind.Warning,
                Key = "ws.sticker"
            });
            p.SendChat($"[gold][WeaponSkins][/] Slot {slot + 1} çıkartması kaldırıldı.");
        });

        foreach (var (_, collection) in EconService.StickerCollections)
        {
            var colName = EconService.GetLocalizedName(collection.LocalizedNames, player.PlayerLanguage.Value);
            builder.Submenu(colName, sub => BuildStickerListSubmenu(sub, player, weaponData, slot, collection));
        }
    }

    public void BuildStickerListSubmenu(IMenuBuilder builder, IPlayer player, WeaponSkinData weaponData, int slot, StickerCollectionDefinition collection)
    {
        var colName = EconService.GetLocalizedName(collection.LocalizedNames, player.PlayerLanguage.Value);
        builder.Description($"{colName} Çıkartması Seçiniz");

        // 1. Reset option
        builder.Button(LocalizationService[player].MenuReset, ctx =>
        {
            var p = ctx.Player;
            Api.UpdateWeaponSkin(p.SteamID, p.Controller.Team, weaponData.DefinitionIndex, skin =>
            {
                skin.SetSticker(slot, null);
            }, true);

            _hud?.Toast(p, new ToastOptions
            {
                Title = "WeaponSkins",
                Text = $"Slot {slot + 1} çıkartması kaldırıldı.",
                Kind = ToastKind.Warning,
                Key = "ws.sticker"
            });
            p.SendChat($"[gold][WeaponSkins][/] Slot {slot + 1} çıkartması kaldırıldı.");
        });

        foreach (var sticker in collection.Stickers)
        {
            if (sticker.Index == 0) continue;
            var stickerName = EconService.GetLocalizedName(sticker.LocalizedNames, player.PlayerLanguage.Value);

            builder.Button(stickerName, ctx =>
            {
                var p = ctx.Player;
                Api.UpdateWeaponSkin(p.SteamID, p.Controller.Team, weaponData.DefinitionIndex, skin =>
                {
                    skin.SetSticker(slot, new StickerData { Id = sticker.Index });
                }, true);

                _hud?.Toast(p, new ToastOptions
                {
                    Title = "WeaponSkins",
                    Text = $"Slot {slot + 1}: {stickerName} ayarlandı!",
                    Kind = ToastKind.Success,
                    Key = "ws.sticker"
                });
                p.SendChat($"[gold][WeaponSkins][/] Slot {slot + 1} çıkartması [lime]{stickerName}[/] olarak ayarlandı!");
            });
        }
    }
}
