using FluentMigrator.Runner;

using Microsoft.Extensions.Logging;

using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

using WeaponSkins.Econ;
using WeaponSkins.Extensions;
using WeaponSkins.Shared;

namespace WeaponSkins.Services;

public class HookInventoryUpdateService : IInventoryUpdateService, IDisposable
{
    public static HookInventoryUpdateService? Instance { get; private set; }

    public static void Reset()
    {
        Instance = null;
    }

    private readonly Dictionary<ulong, (int MusicKitId, ushort? InventoryMusicId)> _originalMusicKits = new();
    private ISwiftlyCore Core { get; }
    private PlayerService PlayerService { get; }
    private ILogger<HookInventoryUpdateService> Logger { get; }
    private ItemPermissionService ItemPermissionService { get; }
    private WeaponSkinGetterAPI Api { get; }
    private DataService DataService { get; }
    private EconService EconService { get; }

    public HookInventoryUpdateService(ISwiftlyCore core,
        WeaponSkinGetterAPI api,
        PlayerService playerService,
        ILogger<HookInventoryUpdateService> logger,
        ItemPermissionService itemPermissionService,
        DataService dataService,
        EconService econService)
    {
        Instance = this;
        Core = core;
        Api = api;
        PlayerService = playerService;
        Logger = logger;
        ItemPermissionService = itemPermissionService;
        DataService = dataService;
        EconService = econService;

        Core.Event.OnEntityCreated += OnEntityCreated;
        Core.GameEvent.HookPost<EventPlayerSpawn>(OnPlayerSpawn);
        Core.GameEvent.HookPre<EventRoundStart>(OnRoundStart);
        Core.GameEvent.HookPre<EventRoundMvp>(OnRoundMvp);

        Core.Event.OnClientDisconnected += OnClientDisconnected;

        foreach (var player in Core.PlayerManager.GetAllPlayers())
        {
            if (player.Controller is { IsValid: true, InventoryServices.IsValid: true })
            {
                ApplyPlayerWeapons(player);
                ApplyPlayerGlove(player);
            }
        }
    }

    private void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player != null) _originalMusicKits.Remove(player.SessionId);
    }

    // Only new entities fire this, so a weapon picked up off the ground keeps its previous owner's finish.
    private void OnEntityCreated(IOnEntityCreatedEvent @event)
    {
        try
        {
            var entity = @event.Entity;
            if (!entity.DesignerName.StartsWith("weapon_", StringComparison.Ordinal)) return;
            var handle = Core.EntitySystem.GetRefEHandle(entity.As<CBasePlayerWeapon>());
            Core.Scheduler.NextTick(() => ApplyGivenWeapon(handle));
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error in OnEntityCreated");
        }
    }

    private void ApplyGivenWeapon(CHandle<CBasePlayerWeapon> handle)
    {
        try
        {
            if (!handle.IsValid || handle.Value is not { IsValid: true } weapon) return;
            if (weapon.OwnerEntity.Value?.As<CCSPlayerPawn>() is not { IsValid: true } owner) return;
            if (owner.Controller.Value is not { IsValid: true } controller) return;
            if (controller.SteamID == 0 || controller.Team == Team.None) return;
            ApplyWeaponSkin(controller.SteamID, controller.Team, weapon);
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Error applying a newly given weapon");
        }
    }

    private bool _mvpPlayed;
    private HookResult OnRoundStart(EventRoundStart @event)
    {
        _mvpPlayed = false;
        return HookResult.Continue;
    }

    private HookResult OnRoundMvp(EventRoundMvp @event)
    {
        if (_mvpPlayed) return HookResult.Continue;

        if (@event.UserIdPlayer is { IsValid: true } player)
        {
            if (DataService.MusicKitDataService.TryGetMusicKit(player.SteamID, out var musicKit))
            {
                @event.DontBroadcast = true;

                Core.GameEvent.Fire<EventRoundMvp>(@event =>
                {
                    @event.UserId = player.PlayerID;
                    @event.MusickItID = musicKit;
                    @event.NoMusic = 0;
                });

                _mvpPlayed = true;
            }
        }
        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event)
    {
        IPlayer? player = @event.UserIdPlayer;

        if (player is not { IsValid: true, IsFakeClient: false }) return HookResult.Continue;

        Core.Scheduler.NextTick(() =>
        {
            ApplyPlayerGlove(player);
            ApplyPlayerAgent(player);
            ApplyPlayerMusicKit(player);
        });

        Core.Scheduler.DelayBySeconds(0.1f, () =>
        {
            ApplyPlayerGlove(player);
            ApplyPlayerAgent(player);
        });

        return HookResult.Continue;
    }

    private void ApplyPlayerMusicKit(IPlayer player)
    {
        if (player.IsValid && DataService.MusicKitDataService.TryGetMusicKit(player.SteamID, out var musicKit))
        {
            UpdateMusicKit(player.SteamID, musicKit);
        }
    }



    private string? GetRefreshModel(string currentModel,
        string targetModel)
    {
        foreach (var agent in EconService.Agents.Values)
        {
            var candidate = agent.ModelPath;
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            if (string.Equals(candidate, currentModel, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(candidate, targetModel, StringComparison.OrdinalIgnoreCase)) continue;
            return candidate;
        }

        return null;
    }

    private void ApplyPlayerAgent(IPlayer player)
    {
        if (!player.IsAlive()) return;

        Core.Scheduler.NextTick(() =>
        {
            if (!player.IsAlive()) return;

            var pawn = player.PlayerPawn;
            if (pawn is not { IsValid: true } || pawn.CBodyComponent?.SceneNode is not { } sceneNode) return;
            var current = sceneNode.GetSkeletonInstance()
                .ModelState
                .ModelName;
            DataService.AgentDataService.CaptureDefaultModel(player.SteamID, player.Controller.Team, current);

            if (DataService.AgentDataService.TryGetAgent(player.SteamID, player.Controller.Team, out var agentIndex))
            {
                var agent = EconService.Agents.Values.FirstOrDefault(a => a.Index == agentIndex);
                if (agent != null)
                {
                    var modelPath = agent.ModelPath;
                    var refreshModel = GetRefreshModel(current, modelPath);
                    if (!string.IsNullOrWhiteSpace(refreshModel))
                    {
                        pawn.SetModel(refreshModel);
                        pawn.SetModel(current);
                    }

                    Core.Scheduler.NextTick(() =>
                    {
                        if (!player.IsAlive() || !pawn.IsValid) return;
                        pawn.SetModel(modelPath);
                    });
                }
            }
            else if (DataService.AgentDataService.TryGetDefaultModel(player.SteamID, player.Controller.Team,
                         out var defaultModel))
            {
                var refreshModel = GetRefreshModel(current, defaultModel);
                if (!string.IsNullOrWhiteSpace(refreshModel))
                {
                    pawn.SetModel(refreshModel);
                    pawn.SetModel(current);
                }

                Core.Scheduler.NextTick(() =>
                {
                    if (!player.IsAlive() || !pawn.IsValid) return;
                    pawn.SetModel(defaultModel);
                });
            }
        });
    }

    public void UpdateWeaponSkins(IEnumerable<WeaponSkinData> skins)
    {
        Dictionary<ulong, List<WeaponSkinData>> updatedSkinMaps = new();

        foreach (var skin in skins)
        {
            if (ItemPermissionService.TryBuildWeaponSkinView(skin, out var runtimeSkin))
            {
                if (DataService.WeaponDataService.StoreSkin(skin) || !runtimeSkin.Equals(skin))
                {
                    updatedSkinMaps.GetOrAdd(skin.SteamID, () => new()).Add(runtimeSkin);
                }
            }
        }

        foreach (var (steamID, updatedSkins) in updatedSkinMaps)
        {
            if (PlayerService.TryGetPlayer(steamID, out var player))
            {
                if (player.IsAlive())
                {
                    ApplyWeaponSkins(player, updatedSkins);

                    foreach (var skin in updatedSkins)
                    {
                        var matchingWeapons = player.PlayerPawn?.WeaponServices?.MyWeapons
                            .Where(w => w.Value is { IsValid: true } currentWeapon &&
                                        currentWeapon.AttributeManager.Item.ItemDefinitionIndex == skin.DefinitionIndex &&
                                        player.Controller.Team == skin.Team)
                            .Select(w => w.Value)
                            .ToList();

                        if (matchingWeapons != null)
                        {
                            foreach (var wep in matchingWeapons)
                            {
                                if (wep != null)
                                {
                                    Core.Scheduler.NextTick(() =>
                                    {
                                        if (player.IsAlive() && player.Controller.Team == skin.Team && wep.IsValid)
                                        {
                                            player.RegiveWeapon(wep, skin.DefinitionIndex);
                                        }
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    public void UpdateKnifeSkins(IEnumerable<KnifeSkinData> knives)
    {
        Dictionary<ulong, List<KnifeSkinData>> updatedKnifeMaps = new();
        foreach (var knife in knives)
        {
            if (ItemPermissionService.TryBuildKnifeSkinView(knife, out var runtimeKnife))
            {
                if (DataService.KnifeDataService.StoreKnife(knife) || !runtimeKnife.Equals(knife))
                {
                    updatedKnifeMaps.GetOrAdd(knife.SteamID, () => new()).Add(runtimeKnife);
                }
            }
        }

        foreach (var (steamID, updatedKnives) in updatedKnifeMaps)
        {

            if (PlayerService.TryGetPlayer(steamID, out var player))
            {
                if (player.IsAlive())
                {
                    ApplyKnifeSkins(player, updatedKnives);

                    bool isHoldingKnife = false;
                    var activeWep = player.PlayerPawn?.WeaponServices?.ActiveWeapon.Value;
                    if (activeWep is { IsValid: true } &&
                        (Utilities.IsKnifeDefinitionIndex(activeWep.AttributeManager.Item.ItemDefinitionIndex) ||
                         activeWep.DesignerName?.Contains("knife") == true))
                    {
                        isHoldingKnife = true;
                    }

                    bool hasKnife = player.PlayerPawn?.WeaponServices?.MyWeapons.Any(w =>
                        w.Value is { IsValid: true } kw &&
                        (Utilities.IsKnifeDefinitionIndex(kw.AttributeManager.Item.ItemDefinitionIndex) ||
                         kw.DesignerName?.Contains("knife") == true)) == true;

                    if (hasKnife || isHoldingKnife)
                    {
                        Core.Scheduler.NextTick(() =>
                        {
                            if (player.IsAlive())
                            {
                                player.RegiveKnife(selectKnife: isHoldingKnife);
                            }
                        });
                    }
                }
            }
        }
    }

    public void UpdateGloveSkins(IEnumerable<GloveData> gloves)
    {
        Dictionary<ulong, List<GloveData>> updatedGloveMaps = new();
        foreach (var glove in gloves)
        {
            if (ItemPermissionService.TryBuildGloveView(glove, out var runtimeGlove))
            {
                if (DataService.GloveDataService.StoreGlove(glove) || !runtimeGlove.Equals(glove))
                {
                    updatedGloveMaps.GetOrAdd(glove.SteamID, () => new()).Add(runtimeGlove);
                }
            }
        }

        foreach (var (steamID, updatedGloves) in updatedGloveMaps)
        {
            if (PlayerService.TryGetPlayer(steamID, out var player))
            {
                if (player.IsAlive())
                {
                    ApplyGlove(player, updatedGloves.Last());
                }
            }
        }
    }

    public void ResetWeaponSkin(ulong steamid,
        Team team,
        ushort definitionIndex)
    {
        if (DataService.WeaponDataService.TryRemoveSkin(steamid, team, definitionIndex))
        {
            if (PlayerService.TryGetPlayer(steamid, out var player))
            {
                Core.Scheduler.NextTick(() =>
                {
                    if (player.IsAlive())
                    {
                        var wep = player.PlayerPawn?.WeaponServices?.MyWeapons.FirstOrDefault(w =>
                            w.Value is { IsValid: true } cw &&
                            cw.AttributeManager.Item.ItemDefinitionIndex == definitionIndex &&
                            player.Controller.Team == team).Value;

                        if (wep is { IsValid: true })
                        {
                            player.RegiveWeapon(wep, definitionIndex);
                        }
                    }
                });
            }
        }
    }

    public void ResetKnifeSkin(ulong steamid,
        Team team)
    {
        if (DataService.KnifeDataService.TryRemoveKnife(steamid, team))
        {
            if (PlayerService.TryGetPlayer(steamid, out var player))
            {
                bool isHoldingKnife = false;
                var activeWep = player.PlayerPawn?.WeaponServices?.ActiveWeapon.Value;
                if (activeWep is { IsValid: true } &&
                    (Utilities.IsKnifeDefinitionIndex(activeWep.AttributeManager.Item.ItemDefinitionIndex) ||
                     activeWep.DesignerName?.Contains("knife") == true))
                {
                    isHoldingKnife = true;
                }

                Core.Scheduler.NextTick(() =>
                {
                    if (player.IsAlive())
                    {
                        player.RegiveKnife(selectKnife: isHoldingKnife);
                    }
                });
            }
        }
    }

    public void ResetGloveSkin(ulong steamid,
        Team team)
    {
        if (DataService.GloveDataService.TryRemoveGlove(steamid, team))
        {
            if (PlayerService.TryGetPlayer(steamid, out var player))
            {
                Core.Scheduler.NextTick(() =>
                {
                    if (player.IsAlive())
                    {
                        ApplyPlayerGlove(player);
                    }
                });
            }
        }
    }

    private void ApplyWeaponSkins(IPlayer player,
        IEnumerable<WeaponSkinData> skins)
    {
        var weaponMap = skins.Where(s => s.Team == player.Controller.Team)
            .ToDictionary(s => s.DefinitionIndex, s => s);
        foreach (var handle in player.PlayerPawn!.WeaponServices!.MyWeapons)
        {
            var weapon = handle.Value;
            if (weapon == null || !weapon.IsValid) continue;
            var def = weapon.AttributeManager.Item.ItemDefinitionIndex;
            if (weaponMap.TryGetValue(def, out var skin))
            {
                ApplyWeaponAttributes(weapon, skin);
            }
        }
    }

    private void ApplyKnifeSkins(IPlayer player,
        IEnumerable<KnifeSkinData> knives)
    {
        foreach (var handle in player.PlayerPawn!.WeaponServices!.MyWeapons)
        {
            var weapon = handle.Value;
            if (weapon == null || !weapon.IsValid) continue;
            if (!Utilities.IsKnifeDefinitionIndex(weapon.AttributeManager.Item.ItemDefinitionIndex)) continue;
            var knife = knives.LastOrDefault(k => k.Team == player.Controller.Team);
            if (knife != null)
            {
                ApplyKnifeAttributes(weapon, knife);
            }
        }
    }

    private void ApplyPlayerWeapons(IPlayer player)
    {
        if (!player.IsAlive()) return;
        if (!Api.TryGetWeaponSkins(player.SteamID, out var weaponSkins) &&
            !Api.TryGetKnifeSkins(player.SteamID, out var knifeSkins))
        {
            return;
        }

        foreach (var handle in player.PlayerPawn!.WeaponServices!.MyWeapons)
        {
            var weapon = handle.Value;
            if (weapon == null || !weapon.IsValid) continue;
            ApplyWeaponSkin(player.SteamID, player.Controller.Team, weapon);
        }
    }

    private void ApplyPlayerGlove(IPlayer player)
    {
        if (!player.IsAlive()) return;
        var pawn = player.PlayerPawn!;
        ApplyGlove(player, pawn);
    }

    public void ApplyWeaponSkin(ulong steamId,
        Team team,
        CBasePlayerWeapon weapon)
    {
        var def = weapon.AttributeManager.Item.ItemDefinitionIndex;
        if (Utilities.IsKnifeDefinitionIndex(def))
        {
            if (Api.TryGetKnifeSkin(steamId, team, out var knife))
            {
                ApplyKnifeAttributes(weapon, knife);
            }

            return;
        }

        if (Utilities.IsWeaponDefinitionIndex(def))
        {
            if (Api.TryGetWeaponSkin(steamId, team, (ushort)def, out var skin))
            {
                ApplyWeaponAttributes(weapon, skin);
            }
        }
    }

    public void ApplyWeaponAttributes(CBasePlayerWeapon weapon,
        WeaponSkinData skin)
    {
        WeaponAppearance.Apply(weapon, skin);
        try
        {
            weapon.AcceptInput("ChangeSubclass", skin.DefinitionIndex.ToString());
        }
        catch { }

        var classname = Core.Helpers.GetClassnameByDefinitionIndex(skin.DefinitionIndex);
        if (string.IsNullOrWhiteSpace(classname) || !EconService.WeaponToPaintkits.TryGetValue(classname, out var paintkits))
        {
            classname = EconService.Items.FirstOrDefault(item => item.Value.Index == skin.DefinitionIndex).Key;
            if (classname == null || !EconService.WeaponToPaintkits.TryGetValue(classname, out paintkits)) return;
        }

        var useLegacy = paintkits.FirstOrDefault(p => p.Index == skin.Paintkit)?.UseLegacyModel;
        weapon.AcceptInput("SetBodygroup", value: $"body,{(useLegacy == true ? 1 : 0)}");
    }

    public void ApplyKnifeAttributes(CBasePlayerWeapon weapon,
        KnifeSkinData knife)
    {
        if (weapon.AttributeManager.Item.ItemDefinitionIndex != knife.DefinitionIndex)
        {
            try
            {
                weapon.AcceptInput("ChangeSubclass", knife.DefinitionIndex.ToString());
            }
            catch { }
        }
        WeaponAppearance.Apply(weapon, knife);
    }

    private void ApplyGlove(IPlayer player,
        GloveData glove)
    {
        if (!player.IsAlive() || player.PlayerPawn is not { IsValid: true } pawn) return;
        ApplyGlove(pawn, glove);
    }

    private void ApplyGlove(IPlayer player,
        CCSPlayerPawn pawn)
    {
        if (!Api.TryGetGloveSkin(player.SteamID, player.Controller.Team, out var glove)) return;
        ApplyGlove(pawn, glove);
    }

    private void ApplyGlove(CCSPlayerPawn pawn,
        GloveData glove)
    {
        Core.Scheduler.NextTick(() =>
        {
            if (!pawn.IsValid) return;
            var econGloves = pawn.EconGloves;
            var equipped = glove.DefinitionIndex != 0;
            var itemId = equipped ? (ulong)Random.Shared.NextInt64(1, long.MaxValue) : 0;
            econGloves.ItemDefinitionIndex = glove.DefinitionIndex;
            econGloves.ItemID = itemId;
            econGloves.ItemIDLow = (uint)itemId;
            econGloves.ItemIDHigh = (uint)(itemId >> 32);
            econGloves.AccountID = equipped ? (uint)glove.SteamID : 0;
            econGloves.NetworkedDynamicAttributes.SetOrAddAttribute("set item texture prefab", glove.Paintkit);
            econGloves.NetworkedDynamicAttributes.SetOrAddAttribute("set item texture seed", glove.PaintkitSeed);
            econGloves.NetworkedDynamicAttributes.SetOrAddAttribute("set item texture wear", glove.PaintkitWear);
            econGloves.AttributeList.SetOrAddAttribute("set item texture prefab", glove.Paintkit);
            econGloves.AttributeList.SetOrAddAttribute("set item texture seed", glove.PaintkitSeed);
            econGloves.AttributeList.SetOrAddAttribute("set item texture wear", glove.PaintkitWear);
            econGloves.Initialized = equipped;
            econGloves.ItemDefinitionIndexUpdated();
            econGloves.ItemIDLowUpdated();
            econGloves.ItemIDHighUpdated();
            econGloves.AccountIDUpdated();
            econGloves.InitializedUpdated();
            econGloves.AttributeListUpdated();
            econGloves.NetworkedDynamicAttributesUpdated();
            pawn.EconGlovesChanged++;
            pawn.EconGlovesChangedUpdated();
            pawn.AcceptInput("SetBodygroup", "first_or_third_person,0");
            Core.Scheduler.DelayBySeconds(0.2f, () =>
            {
                if (pawn.IsValid) pawn.AcceptInput("SetBodygroup", "first_or_third_person,1");
            });
        });
    }

    public void UpdateMusicKit(ulong steamid, int musicKitIndex)
    {
        if (PlayerService.TryGetPlayer(steamid, out var player))
        {
            if (!_originalMusicKits.TryGetValue(player.SessionId, out var original))
            {
                original = (player.Controller.MusicKitID, null);
            }
            _originalMusicKits[player.SessionId] = (original.MusicKitId,
                original.InventoryMusicId ?? player.Controller.InventoryServices?.MusicID);

            player.Controller.MusicKitID = musicKitIndex;
            player.Controller.MusicKitIDUpdated();
            if (player.Controller.InventoryServices != null)
            {
                player.Controller.InventoryServices.MusicID = (ushort)musicKitIndex;
                player.Controller.InventoryServices.MusicIDUpdated();
            }
        }
    }

    public void ResetMusicKit(ulong steamid)
    {
        if (PlayerService.TryGetPlayer(steamid, out var player) &&
            _originalMusicKits.Remove(player.SessionId, out var original))
        {
            player.Controller.MusicKitID = original.MusicKitId;
            player.Controller.MusicKitIDUpdated();
            if (player.Controller.InventoryServices != null && original.InventoryMusicId is { } inventoryMusicId)
            {
                player.Controller.InventoryServices.MusicID = inventoryMusicId;
                player.Controller.InventoryServices.MusicIDUpdated();
            }
        }
    }

    public void RefreshPlayer(ulong steamId,
        bool resetGlove)
    {
        if (!PlayerService.TryGetPlayer(steamId, out var player)) return;

        if (DataService.MusicKitDataService.TryGetMusicKit(steamId, out var musicKit))
        {
            UpdateMusicKit(steamId, musicKit);
        }
        else
        {
            ResetMusicKit(steamId);
        }

        if (!player.IsAlive()) return;
        var pawn = player.PlayerPawn;
        if (pawn?.WeaponServices is not { IsValid: true } weapons) return;

        // Recreate held weapons even when their stored skin was removed.
        foreach (var handle in weapons.MyWeapons.ToArray())
        {
            var weapon = handle.Value;
            if (weapon is not { IsValid: true }) continue;
            var definition = weapon.AttributeManager.Item.ItemDefinitionIndex;
            if (Utilities.IsKnifeDefinitionIndex(definition))
            {
                player.RegiveKnife();
            }
            else if (Utilities.IsWeaponDefinitionIndex(definition))
            {
                player.RegiveWeapon(weapon, definition);
            }
        }

        if (resetGlove && !Api.TryGetGloveSkin(steamId, player.Controller.Team, out _))
        {
            ApplyGlove(player, new GloveData { SteamID = steamId, Team = player.Controller.Team, DefinitionIndex = 0 });
        }
        else
        {
            ApplyPlayerGlove(player);
        }
        ApplyPlayerAgent(player);
    }

    public void Dispose()
    {
        Core.Event.OnEntityCreated -= OnEntityCreated;
        Core.Event.OnClientDisconnected -= OnClientDisconnected;
        foreach (var sessionId in _originalMusicKits.Keys.ToArray())
        {
            var player = Core.PlayerManager.GetPlayerFromSessionId(sessionId);
            if (player is { IsValid: true, Controller.IsValid: true }) ResetMusicKit(player.SteamID);
        }
        _originalMusicKits.Clear();
        Instance = null;
    }
}