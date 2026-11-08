using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using MenuApi.API;

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

public enum TargetTeamMode
{
    BothTeams = 0,
    OnlyT = 1,
    OnlyCT = 2,
    CurrentTeam = 3
}

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
    private IMenuApi? _menuApi;

    public IPanoramaHudApi? HudApi => _hudApi;
    public IHudScope? Hud => _hud;
    public IMenuApi? MenuApi => _menuApi;

    private readonly ConcurrentDictionary<ulong, TargetTeamMode> _playerTeamModes = new();

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

    public void SetMenuApi(IMenuApi? menuApi)
    {
        _menuApi = menuApi;
        if (menuApi != null)
        {
            Logger.LogInformation("MenuApi registered successfully in WeaponSkins MenuService.");
        }
    }

    public TargetTeamMode GetPlayerTeamMode(ulong steamId)
    {
        return _playerTeamModes.GetOrAdd(steamId, TargetTeamMode.BothTeams);
    }

    public void SetPlayerTeamMode(ulong steamId, TargetTeamMode mode)
    {
        _playerTeamModes[steamId] = mode;
    }

    public TargetTeamMode CyclePlayerTeamMode(ulong steamId)
    {
        var next = GetPlayerTeamMode(steamId) switch
        {
            TargetTeamMode.BothTeams => TargetTeamMode.OnlyT,
            TargetTeamMode.OnlyT => TargetTeamMode.OnlyCT,
            TargetTeamMode.OnlyCT => TargetTeamMode.CurrentTeam,
            _ => TargetTeamMode.BothTeams
        };
        _playerTeamModes[steamId] = next;
        return next;
    }

    public string GetTeamModeDisplayName(ulong steamId)
    {
        return GetPlayerTeamMode(steamId) switch
        {
            TargetTeamMode.BothTeams => "Her İki Takım (T & CT)",
            TargetTeamMode.OnlyT => "Sadece Terörist (T)",
            TargetTeamMode.OnlyCT => "Sadece Anti-Terörist (CT)",
            TargetTeamMode.CurrentTeam => "Mevcut Takımım",
            _ => "Her İki Takım (T & CT)"
        };
    }

    public Team GetPlayerEffectiveTeam(IPlayer player)
    {
        var team = player.Controller?.Team ?? Team.T;
        return team is Team.T or Team.CT ? team : Team.T;
    }

    public Team GetPrimaryPreviewTeam(IPlayer player)
    {
        return GetPlayerTeamMode(player.SteamID) switch
        {
            TargetTeamMode.OnlyT => Team.T,
            TargetTeamMode.OnlyCT => Team.CT,
            _ => GetPlayerEffectiveTeam(player)
        };
    }

    public Team[] GetTargetTeams(IPlayer player)
    {
        return GetPlayerTeamMode(player.SteamID) switch
        {
            TargetTeamMode.BothTeams => [Team.T, Team.CT],
            TargetTeamMode.OnlyT => [Team.T],
            TargetTeamMode.OnlyCT => [Team.CT],
            TargetTeamMode.CurrentTeam => [GetPlayerEffectiveTeam(player)],
            _ => [Team.T, Team.CT]
        };
    }

    public void UpdateWeaponSkinForSelectedTeams(IPlayer player, ushort defIndex, Action<WeaponSkinData> action, bool permanent = true)
    {
        foreach (var team in GetTargetTeams(player))
        {
            Api.UpdateWeaponSkin(player.SteamID, team, defIndex, action, permanent);
        }
    }

    public void ResetWeaponSkinForSelectedTeams(IPlayer player, ushort defIndex, bool permanent = true)
    {
        foreach (var team in GetTargetTeams(player))
        {
            Api.ResetWeaponSkin(player.SteamID, team, defIndex, permanent);
        }
    }

    public void UpdateKnifeSkinForSelectedTeams(IPlayer player, Action<KnifeSkinData> action, bool permanent = true)
    {
        foreach (var team in GetTargetTeams(player))
        {
            Api.UpdateKnifeSkin(player.SteamID, team, action, permanent);
        }
    }

    public void ResetKnifeSkinForSelectedTeams(IPlayer player, bool permanent = true)
    {
        foreach (var team in GetTargetTeams(player))
        {
            Api.ResetKnifeSkin(player.SteamID, team, permanent);
        }
    }

    public void UpdateGloveSkinForSelectedTeams(IPlayer player, Action<GloveData> action, bool permanent = true)
    {
        foreach (var team in GetTargetTeams(player))
        {
            Api.UpdateGloveSkin(player.SteamID, team, action, permanent);
        }
    }

    public void ResetGloveSkinForSelectedTeams(IPlayer player, bool permanent = true)
    {
        foreach (var team in GetTargetTeams(player))
        {
            Api.ResetGloveSkin(player.SteamID, team, permanent);
        }
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

        _menuApi?.CloseMenu(player);
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
        if (_menuApi != null)
        {
            OpenMenuApiMainMenu(player);
            return;
        }

        player.SendChat("[red][WeaponSkins][/] Menü sistemi yüklenemedi (MenuApi eksik).");
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
        var previewTeam = GetPrimaryPreviewTeam(player);
        if (Api.TryGetWeaponSkin(player.SteamID, previewTeam, defIndex, out dataInHand))
        {
            return true;
        }
        return Api.TryGetWeaponSkin(player.SteamID, player.Controller.Team, defIndex, out dataInHand);
    }

    public WeaponSkinData GetOrCreateWeaponSkinData(IPlayer player, ushort defIndex)
    {
        var team = GetPrimaryPreviewTeam(player);
        if (Api.TryGetWeaponSkin(player.SteamID, team, defIndex, out var existing) && existing != null)
        {
            return existing;
        }
        if (Api.TryGetWeaponSkin(player.SteamID, Team.T, defIndex, out existing) && existing != null)
        {
            return existing;
        }
        if (Api.TryGetWeaponSkin(player.SteamID, Team.CT, defIndex, out existing) && existing != null)
        {
            return existing;
        }
        return new WeaponSkinData
        {
            SteamID = player.SteamID,
            Team = team,
            DefinitionIndex = defIndex
        };
    }

    public bool TryGetKnifeDataInHand(IPlayer player,
        [MaybeNullWhen(false)] out KnifeSkinData dataInHand)
    {
        var previewTeam = GetPrimaryPreviewTeam(player);
        if (Api.TryGetKnifeSkin(player.SteamID, previewTeam, out dataInHand))
        {
            return true;
        }
        return Api.TryGetKnifeSkin(player.SteamID, player.Controller.Team, out dataInHand);
    }

    public bool TryGetGloveDataInHand(IPlayer player,
        [MaybeNullWhen(false)] out GloveData dataInHand)
    {
        var previewTeam = GetPrimaryPreviewTeam(player);
        if (Api.TryGetGloveSkin(player.SteamID, previewTeam, out dataInHand))
        {
            return true;
        }
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
        _menuApi = null;
    }
}
