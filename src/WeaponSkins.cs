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
                RegisterMenuApiSubPages();
                Core.Logger.LogInformation("[WeaponSkins] MenuApi shared interface resolved.");
            }
            catch (Exception ex)
            {
                Core.Logger.LogError(ex, "[WeaponSkins] Error resolving MenuApi interface.");
            }
        }
        else
        {
            _menuApi = null;
        }

        if (interfaceManager.HasSharedInterface("PanoramaHud"))
        {
            _hudApi = interfaceManager.GetSharedInterface<IPanoramaHudApi>("PanoramaHud");
            if (_provider != null)
            {
                var menuService = _provider.GetService<MenuService>();
                if (menuService != null && _hudApi != null)
                {
                    menuService.SetPanoramaHud(_hudApi);
                }
            }
            Core.Logger.LogInformation("[WeaponSkins] PanoramaHud shared interface resolved.");
        }
        else
        {
            Core.Logger.LogWarning("[WeaponSkins] PanoramaHud shared interface not found on server.");
        }
    }

    private void RegisterMenuApiSubPages()
    {
        if (_menuApi == null || _provider == null) return;

        var permService = _provider.GetService<ItemPermissionService>();

        _menuApi.RegisterSubPage(new MenuSubPageRegistration
        {
            ParentPageId = "settings",
            SubPageId = "ws_customizer",
            Title = "SKIN AYARLARI (FLOAT/STICKER)",
            Order = 15,
            CanAccess = p => permService == null ||
                             permService.CanUseWeaponSkins(p.SteamID) ||
                             permService.CanUseKnifeSkins(p.SteamID) ||
                             permService.CanUseMusicKits(p.SteamID),
            Commands = ["wsmenu", "skincfg"],
            PopulateItems = (player, builder) => PopulateSkinSettingsSubPage(player, builder, permService)
        });
    }

    private void PopulateSkinSettingsSubPage(IPlayer player, MenuApi.API.IMenuBuilder builder, ItemPermissionService? perms)
    {
        builder.AddHeader("SKIN VE ENVANTER YÖNETİMİ", "ws_main");

        bool canWp = perms?.CanUseWeaponSkins(player.SteamID) ?? true;
        bool canKnife = perms?.CanUseKnifeSkins(player.SteamID) ?? true;
        bool canGloves = perms?.CanUseGloveSkins(player.SteamID) ?? true;
        bool canMusic = perms?.CanUseMusicKits(player.SteamID) ?? true;
        bool canStickers = perms?.CanUseStickers(player.SteamID) ?? true;
        bool canKeychains = perms?.CanUseKeychains(player.SteamID) ?? true;

        builder.AddInfo("Silah Skin Yetkisi", canWp ? "✓ AKTİF" : "✗ YETKİ GEREKİR");
        builder.AddInfo("Bıçak Skin Yetkisi", canKnife ? "✓ AKTİF" : "✗ YETKİ GEREKİR");
        builder.AddInfo("Eldiven Yetkisi", canGloves ? "✓ AKTİF" : "✗ YETKİ GEREKİR");
        builder.AddInfo("Müzik Kiti Yetkisi", canMusic ? "✓ AKTİF" : "✗ YETKİ GEREKİR");
        builder.AddInfo("Çıkartma (Sticker) Yetkisi", canStickers ? "✓ AKTİF" : "✗ YETKİ GEREKİR");
        builder.AddInfo("Anahtarlık (Keychain) Yetkisi", canKeychains ? "✓ AKTİF" : "✗ YETKİ GEREKİR");

        builder.AddHeader("ENVANTER VE VERİTABANI İŞLEMLERİ", "ws_actions");

        builder.AddActionButton(
            "Envanterimi Veritabanından Yeniden Yükle (!wp)",
            "YENİDEN YÜKLE",
            ctx =>
            {
                if (_provider != null)
                {
                    var storageService = _provider.GetService<StorageService>();
                    if (storageService != null)
                    {
                        storageService.ReloadPlayer(ctx.Player, msg => ctx.Player.SendMessage(MessageType.Chat, $" \x04[WeaponSkins]\x01 {msg}"));
                    }
                }
            },
            description: "Skinlerinizde veya çıkartmalarınızda gecikme olursa anında veritabanıyla senkronize eder",
            closeOnSelect: false);

        builder.AddActionButton(
            "Skin Seçiciyi Aç (3D Weapon Grid)",
            "SKIN SEÇİCİ",
            ctx =>
            {
                _menuApi?.OpenPage(ctx.Player, "skins");
            },
            description: "Görsel kart ızgarasında silah modellerini ve kaplamalarını seçin",
            closeOnSelect: false);
    }
}
