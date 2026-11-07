using PanoramaHud.Contract;

using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

using WeaponSkins.Econ;
using WeaponSkins.Shared;

namespace WeaponSkins;

public partial class MenuService
{
    private readonly Dictionary<int, KeychainDefinition> _cachedKeychainDefinitions = new();

    private string? GetKeychainName(KeychainData? data, string language)
    {
        if (data == null || data.Id == 0) return null;
        if (!_cachedKeychainDefinitions.TryGetValue(data.Id, out var definition))
        {
            definition = EconService.Keychains.Values.FirstOrDefault(k => k.Index == data.Id);
            if (definition != null)
            {
                _cachedKeychainDefinitions[data.Id] = definition;
            }
        }

        if (definition == null) return null;
        return EconService.GetLocalizedName(definition.LocalizedNames, language);
    }

    public void OpenKeychainMenu(IPlayer player)
    {
        if (_hud == null) return;
        var builder = _hud.Menu($"ws.keychains.{player.SteamID}", LocalizationService[player].MenuTitleKeychains);
        BuildKeychainSubmenu(builder, player);
        builder.Build().Open(player);
    }

    public void BuildKeychainSubmenu(IMenuBuilder builder, IPlayer player)
    {
        builder.Description("Anahtarlık Seçiniz");

        if (!TryGetWeaponDataInHand(player, out var dataInHand))
        {
            builder.Info("Lütfen önce elinize bir silah alınız!");
            return;
        }

        var currentKeychain = dataInHand.GetKeychain(0);
        var keychainName = GetKeychainName(currentKeychain, player.PlayerLanguage.Value);
        var slotTitle = $"Slot 1: [ {(keychainName ?? "Boş")} ]";

        builder.Submenu(slotTitle, sub => BuildKeychainListSubmenu(sub, player, dataInHand, 0));
    }

    public void BuildKeychainListSubmenu(IMenuBuilder builder, IPlayer player, WeaponSkinData weaponData, int slot)
    {
        builder.Description("Anahtarlık Seçiniz");

        // 1. Reset option
        builder.Button(LocalizationService[player].MenuReset, ctx =>
        {
            var p = ctx.Player;
            Api.UpdateWeaponSkin(p.SteamID, p.Controller.Team, weaponData.DefinitionIndex, skin =>
            {
                skin.SetKeychain(slot, null);
            }, true);

            _hud?.Toast(p, new ToastOptions
            {
                Title = "WeaponSkins",
                Text = "Anahtarlık kaldırıldı.",
                Kind = ToastKind.Warning,
                Key = "ws.keychain"
            });
            p.SendChat("[gold][WeaponSkins][/] Anahtarlık kaldırıldı.");
        });

        var sorted = EconService.Keychains.Values.OrderByDescending(k => k.Rarity.Id).ToList();
        foreach (var keychain in sorted)
        {
            var keychainName = EconService.GetLocalizedName(keychain.LocalizedNames, player.PlayerLanguage.Value);

            builder.Button(keychainName, ctx =>
            {
                var p = ctx.Player;
                Api.UpdateWeaponSkin(p.SteamID, p.Controller.Team, weaponData.DefinitionIndex, skin =>
                {
                    skin.SetKeychain(slot, new KeychainData { Id = keychain.Index });
                }, true);

                _hud?.Toast(p, new ToastOptions
                {
                    Title = "WeaponSkins",
                    Text = $"{keychainName} ayarlandı!",
                    Kind = ToastKind.Success,
                    Key = "ws.keychain"
                });
                p.SendChat($"[gold][WeaponSkins][/] Anahtarlık [lime]{keychainName}[/] olarak ayarlandı!");
            });
        }
    }
}
