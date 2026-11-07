using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Microsoft.Extensions.Logging;

using PanoramaHud.Contract;

using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Translation;

using WeaponSkins.Econ;
using WeaponSkins.Services;
using WeaponSkins.Shared;

namespace WeaponSkins;

public partial class MenuService : IDisposable
{
    private ISwiftlyCore Core { get; init; }
    private ILogger<MenuService> Logger { get; init; }
    private WeaponSkinAPI Api { get; init; }
    private EconService EconService { get; init; }
    private LocalizationService LocalizationService { get; init; }
    private ItemPermissionService ItemPermissionService { get; init; }
    private DataService DataService { get; init; }

    private IPanoramaHudApi? _hudApi;
    private IHudScope? _hud;

    public IPanoramaHudApi? HudApi => _hudApi;
    public IHudScope? Hud => _hud;

    public sealed class ChatInputSession
    {
        public required string Prompt { get; init; }
        public required Func<string, bool> Validator { get; init; }
        public required Action<IPlayer, string> OnSuccess { get; init; }
        public Action<IPlayer>? OnCancel { get; init; }
    }

    private readonly ConcurrentDictionary<int, ChatInputSession> _activeChatInputs = new();
    private Guid _chatHookGuid;

    public MenuService(ISwiftlyCore core,
        ILogger<MenuService> logger,
        WeaponSkinAPI api,
        EconService econService,
        LocalizationService localizationService,
        ItemPermissionService itemPermissionService,
        DataService dataService)
    {
        Core = core;
        Logger = logger;
        Api = api;
        EconService = econService;
        LocalizationService = localizationService;
        ItemPermissionService = itemPermissionService;
        DataService = dataService;

        _chatHookGuid = Core.Command.HookClientChat(OnClientChat);
    }

    public void SetPanoramaHud(IPanoramaHudApi hudApi)
    {
        _hudApi = hudApi;
        _hud?.Dispose();
        _hud = hudApi.CreateScope("WeaponSkins");
        Logger.LogInformation("PanoramaHud registered successfully in WeaponSkins.");
    }

    public void PromptChatInput(IPlayer player,
        string prompt,
        Func<string, bool> validator,
        Action<IPlayer, string> onSuccess,
        Action<IPlayer>? onCancel = null)
    {
        _activeChatInputs[player.PlayerID] = new ChatInputSession
        {
            Prompt = prompt,
            Validator = validator,
            OnSuccess = onSuccess,
            OnCancel = onCancel
        };

        _hudApi?.CloseModal(player);
        _hud?.Toast(player, new ToastOptions
        {
            Title = "WeaponSkins",
            Text = prompt,
            Kind = ToastKind.Info,
            Key = "ws.prompt"
        });
        player.SendChat($"[gold][WeaponSkins][/] {prompt} [silver](İptal için 'iptal' yazın)[/]");
    }

    private HookResult OnClientChat(int playerId, string text, bool teamonly)
    {
        var player = Core.PlayerManager.GetPlayer(playerId);
        if (player == null || !_activeChatInputs.TryGetValue(playerId, out var session))
        {
            return HookResult.Continue;
        }

        var trimmed = text.Trim();
        if (string.Equals(trimmed, "iptal", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "!iptal", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "cancel", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "exit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "q", StringComparison.OrdinalIgnoreCase))
        {
            _activeChatInputs.TryRemove(playerId, out _);
            player.SendChat("[gold][WeaponSkins][/] İşlem iptal edildi.");
            session.OnCancel?.Invoke(player);
            return HookResult.Handled;
        }

        if (!session.Validator(trimmed))
        {
            player.SendChat("[red][WeaponSkins][/] Geçersiz değer! Lütfen tekrar deneyin veya iptal için 'iptal' yazın.");
            return HookResult.Handled;
        }

        _activeChatInputs.TryRemove(playerId, out _);
        session.OnSuccess(player, trimmed);
        return HookResult.Handled;
    }

    public void OpenMainMenu(IPlayer player)
    {
        if (_hud == null)
        {
            player.SendChat("[red][WeaponSkins][/] Menü sistemi yüklenemedi (PanoramaHud eksik).");
            return;
        }

        var builder = _hud.Menu($"ws.main.{player.SteamID}", LocalizationService[player].MenuTitle)
            .Description("Silah, Bıçak ve Eldiven Menüsü");

        if (ItemPermissionService.CanUseWeaponSkins(player.SteamID))
        {
            builder.Submenu(LocalizationService[player].MenuTitleSkins, sub => BuildWeaponSkinSubmenu(sub, player));
        }

        if (ItemPermissionService.CanUseKnifeSkins(player.SteamID))
        {
            builder.Submenu(LocalizationService[player].MenuTitleKnifes, sub => BuildKnifeSkinSubmenu(sub, player));
        }

        if (ItemPermissionService.CanUseGloveSkins(player.SteamID))
        {
            builder.Submenu(LocalizationService[player].MenuTitleGloves, sub => BuildGloveSkinSubmenu(sub, player));
        }

        if (ItemPermissionService.CanUseStickers(player.SteamID))
        {
            builder.Submenu(LocalizationService[player].MenuTitleStickers, sub => BuildStickerSubmenu(sub, player));
        }

        if (ItemPermissionService.CanUseKeychains(player.SteamID))
        {
            builder.Submenu(LocalizationService[player].MenuTitleKeychains, sub => BuildKeychainSubmenu(sub, player));
        }

        if (ItemPermissionService.CanUseWeaponSkins(player.SteamID))
        {
            builder.Submenu(LocalizationService[player].MenuTitleSkinProperties, sub => BuildSkinPropertiesSubmenu(sub, player));
        }

        if (ItemPermissionService.CanUseKnifeSkins(player.SteamID))
        {
            builder.Submenu(LocalizationService[player].MenuTitleKnifeProperties, sub => BuildKnifePropertiesSubmenu(sub, player));
        }

        if (ItemPermissionService.CanUseGloveSkins(player.SteamID))
        {
            builder.Submenu(LocalizationService[player].MenuTitleGloveProperties, sub => BuildGlovePropertiesSubmenu(sub, player));
        }

        if (ItemPermissionService.CanUseAgents(player.SteamID))
        {
            builder.Submenu(LocalizationService[player].MenuTitleAgents, sub => BuildAgentSubmenu(sub, player));
        }

        if (ItemPermissionService.CanUseMusicKits(player.SteamID))
        {
            builder.Submenu(LocalizationService[player].MenuTitleMusicKits, sub => BuildMusicKitSubmenu(sub, player));
        }

        builder.Build().Open(player);
    }

    public bool TryGetWeaponInHand(IPlayer player,
        [MaybeNullWhen(false)] out CBasePlayerWeapon weaponInHand)
    {
        weaponInHand = null;
        if (!player.IsValid)
        {
            return false;
        }

        if (!(player.PlayerPawn is { IsValid: true, LifeState: (byte)LifeState_t.LIFE_ALIVE }))
        {
            return false;
        }

        weaponInHand = player.PlayerPawn.WeaponServices?.ActiveWeapon.Value;
        return weaponInHand != null;
    }

    public bool TryGetWeaponDataInHand(IPlayer player,
        [MaybeNullWhen(false)] out WeaponSkinData dataInHand)
    {
        dataInHand = null;
        if (!TryGetWeaponInHand(player, out var weaponInHand))
        {
            return false;
        }

        var defIndex = weaponInHand.AttributeManager.Item.ItemDefinitionIndex;
        return Api.TryGetWeaponSkin(player.SteamID, player.Controller.Team, defIndex, out dataInHand);
    }

    public bool TryGetKnifeDataInHand(IPlayer player,
        [MaybeNullWhen(false)] out KnifeSkinData dataInHand)
    {
        return Api.TryGetKnifeSkin(player.SteamID, player.Controller.Team, out dataInHand);
    }

    public bool TryGetGloveDataInHand(IPlayer player,
        [MaybeNullWhen(false)] out GloveData dataInHand)
    {
        return Api.TryGetGloveSkin(player.SteamID, player.Controller.Team, out dataInHand);
    }

    private string GetAgentName(AgentDefinition agent, string language)
    {
        return agent.LocalizedNames.TryGetValue(language, out var localized)
            ? localized
            : agent.LocalizedNames.GetValueOrDefault("english") ?? agent.Name;
    }

    public void Dispose()
    {
        if (_chatHookGuid != Guid.Empty)
        {
            Core.Command.UnhookClientChat(_chatHookGuid);
            _chatHookGuid = Guid.Empty;
        }
        _activeChatInputs.Clear();
        _hud?.Dispose();
        _hud = null;
        _hudApi = null;
    }
}
