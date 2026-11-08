using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

using PanoramaHud.Contract;
using MenuApi.API;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.SteamAPI;

using Tomlyn.Extensions.Configuration;

using WeaponSkins.Configuration;
using WeaponSkins.Database;
using WeaponSkins.Injections;
using WeaponSkins.Services;
using WeaponSkins.Shared;

namespace WeaponSkins;

[PluginMetadata(
    Id = "WeaponSkins",
#if WORKFLOW
    Version = WORKFLOW_VERSION,
#else
    Version = "Local",
#endif
    Name = "WeaponSkins",
    Author = "samyyc & ELDment",
    Description = "A swiftlys2 plugin to change player's skins."
)]
public partial class WeaponSkins : BasePlugin
{
    private ServiceProvider? _provider;
    private IPanoramaHudApi? _hudApi;
    private IMenuApi? _menuApi;

    public WeaponSkins(ISwiftlyCore core) : base(core)
    {
    }

    public override void Load(bool hotReload)
    {
        Core.Configuration.InitializeTomlWithModel<MainConfigModel>("config.toml", "Main")
            .Configure(builder =>
            {
                builder.AddTomlFile("config.toml", false, true);
            });

        var collection = new ServiceCollection()
            .AddSwiftly(Core)
            .AddDataService()
            .AddNativeService()
            .AddInventoryService()
            .AddPlayerService()
            .AddApi()
            .AddEconService()
            .AddMenuService()
            .AddStorageService()
            .AddStattrakService()
            .AddLocalizationService()
            .AddItemPermissionService()
            .AddCommandService();

        collection
            .AddOptions<MainConfigModel>()
            .BindConfiguration("Main");

        _provider = collection.BuildServiceProvider();

        _provider
            .UseDataService()
            .UseNativeService()
            .UseInventoryService()
            .UsePlayerService()
            .UseApi()
            .UseEconService()
            .UseMenuService()
            .UseStorageService()
            .UseStattrakService()
            .UseLocalizationService()
            .UseItemPermissionService()
            .UseCommandService();

        if (_hudApi != null)
        {
            _provider.GetRequiredService<MenuService>().SetPanoramaHud(_hudApi);
        }

        if (_menuApi != null)
        {
            _provider.GetRequiredService<MenuService>().SetMenuApi(_menuApi);
            RegisterMenuApiSubPages();
        }
    }

    public override void Unload()
    {
        WeaponSkinAPI.Reset();
        HookInventoryUpdateService.Reset();
        _provider?.GetService<MenuService>()?.Dispose();
        _provider?.Dispose();
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
        if (_provider != null)
        {
            interfaceManager.AddSharedInterface<IWeaponSkinAPI, WeaponSkinAPI>("WeaponSkins.API",
                _provider.GetRequiredService<WeaponSkinAPI>());
        }
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        if (interfaceManager.HasSharedInterface("MenuApi"))
        {
            try
            {
                _menuApi = interfaceManager.GetSharedInterface<IMenuApi>("MenuApi");
                if (_provider != null)
                {
                    _provider.GetService<MenuService>()?.SetMenuApi(_menuApi);
                }
                RegisterMenuApiSubPages();
                Core.Logger.LogInformation("[WeaponSkins] SwiftlyS2-MenuApi shared interface resolved.");
            }
            catch (Exception ex)
            {
                Core.Logger.LogError(ex, "[WeaponSkins] Error resolving MenuApi interface.");
            }
        }
        else
        {
            _menuApi = null;
            if (_provider != null)
            {
                _provider.GetService<MenuService>()?.SetMenuApi(null);
            }
            Core.Logger.LogWarning("[WeaponSkins] MenuApi shared interface not found on server.");
        }
    }

    private void RegisterMenuApiSubPages()
    {
        if (_menuApi == null || _provider == null) return;

        try
        {
            _menuApi.RegisterSubPage(new MenuSubPageRegistration
            {
                ParentPageId = "settings",
                SubPageId = "weaponskins",
                Title = "SİLAH & SKİN AYARLARI (!ws)",
                Order = 10,
                Commands = ["wsmenu", "skincfg"],
                PopulateItems = (player, builder) =>
                {
                    builder.AddHeader("SİLAH & SKİN AYARLARI (FLOAT, STICKER, TAKIM)", "ws_sec");
                    builder.AddActionButton(
                        "Skin Yönetim Menüsünü Aç (!ws)",
                        "YÖNETİM",
                        ctx =>
                        {
                            var menuService = _provider?.GetService<MenuService>();
                            menuService?.OpenMainMenu(ctx.Player);
                        },
                        description: "Takım seçimi, float, aşınma, anahtarlık ve çıkartmaları özelleştirin",
                        closeOnSelect: false);

                    builder.AddActionButton(
                        "Görsel Skin Seçiciyi Aç (!vs)",
                        "SKIN SEÇİCİ",
                        ctx =>
                        {
                            _menuApi?.OpenPage(ctx.Player, "skins");
                        },
                        description: "Görsel kart ızgarasında tüm CS2 silah kaplamalarını seçin",
                        closeOnSelect: false);
                }
            });
            Core.Logger.LogInformation("[WeaponSkins] MenuApi sub-page 'weaponskins' başarıyla kaydedildi.");
        }
        catch (Exception ex)
        {
            Core.Logger.LogError(ex, "[WeaponSkins] MenuApi sub-page kaydedilirken hata!");
        }

        Core.Command.RegisterCommand("wsmenu", ctx =>
        {
            if (ctx.IsSentByPlayer && ctx.Sender != null)
            {
                OpenSkinSettingsMenu(ctx.Sender);
            }
        });

        Core.Command.RegisterCommand("skincfg", ctx =>
        {
            if (ctx.IsSentByPlayer && ctx.Sender != null)
            {
                OpenSkinSettingsMenu(ctx.Sender);
            }
        });
    }

    public void OpenSkinSettingsMenu(IPlayer player)
    {
        if (_provider != null)
        {
            var menuService = _provider.GetService<MenuService>();
            if (menuService != null && _menuApi != null)
            {
                menuService.SetMenuApi(_menuApi);
                menuService.OpenMainMenu(player);
                return;
            }
        }
        if (_menuApi == null || _provider == null) return;

        var permService = _provider.GetService<ItemPermissionService>();
        if (permService != null &&
            !permService.CanUseWeaponSkins(player.SteamID) &&
            !permService.CanUseKnifeSkins(player.SteamID) &&
            !permService.CanUseMusicKits(player.SteamID))
        {
            player.SendMessage(MessageType.Chat, " \x04[WeaponSkins]\x01 Bu menüye erişim yetkiniz yok.");
            return;
        }

        var builder = _menuApi.CreateBuilder("ws_settings", "SKIN AYARLARI (FLOAT/STICKER)")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true);

        builder.AddText("=== SKIN VE ENVANTER YONETIMI ===");

        bool canWp = permService?.CanUseWeaponSkins(player.SteamID) ?? true;
        bool canKnife = permService?.CanUseKnifeSkins(player.SteamID) ?? true;
        bool canGloves = permService?.CanUseGloveSkins(player.SteamID) ?? true;
        bool canMusic = permService?.CanUseMusicKits(player.SteamID) ?? true;
        bool canStickers = permService?.CanUseStickers(player.SteamID) ?? true;
        bool canKeychains = permService?.CanUseKeychains(player.SteamID) ?? true;

        builder.AddText($"Silah Skin: {(canWp ? "✓ AKTİF" : "✗ YETKİ GEREKİR")}");
        builder.AddText($"Bıçak Skin: {(canKnife ? "✓ AKTİF" : "✗ YETKİ GEREKİR")}");
        builder.AddText($"Eldiven: {(canGloves ? "✓ AKTİF" : "✗ YETKİ GEREKİR")}");
        builder.AddText($"Müzik Kiti: {(canMusic ? "✓ AKTİF" : "✗ YETKİ GEREKİR")}");
        builder.AddText($"Çıkartma: {(canStickers ? "✓ AKTİF" : "✗ YETKİ GEREKİR")}");
        builder.AddText($"Anahtarlık: {(canKeychains ? "✓ AKTİF" : "✗ YETKİ GEREKİR")}");

        builder.AddSpacer();
        builder.AddText("=== ISLEMLER ===");

        builder.AddItem("Envanterimi Veritabanından Yeniden Yükle (!wp)", ctx =>
        {
            if (_provider != null)
            {
                var storageService = _provider.GetService<StorageService>();
                storageService?.ReloadPlayer(ctx.Player, msg => ctx.Player.SendMessage(MessageType.Chat, $" \x04[WeaponSkins]\x01 {msg}"));
            }
        }, enabled: true, closeOnSelect: false);

        builder.AddItem("Skin Seçiciyi Aç (3D Weapon Grid)", ctx =>
        {
            ctx.Player.ExecuteCommand("sw_skins");
        }, enabled: true, closeOnSelect: true);

        builder.Build().Open(player);
    }
}
