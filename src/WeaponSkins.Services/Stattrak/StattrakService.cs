using Microsoft.Extensions.Logging;

using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;

using WeaponSkins.Database;
using WeaponSkins.Extensions;
using WeaponSkins.Services;
using WeaponSkins.Shared;

namespace WeaponSkins;

public class StattrakService
{
    private ISwiftlyCore Core { get; init; }
    private DataService DataService { get; init; }
    private StorageService StorageService { get; init; }
    private ILogger<StattrakService> Logger { get; init; }

    public StattrakService(ISwiftlyCore core,
        DataService dataService,
        StorageService storageService,
        ILogger<StattrakService> logger)
    {
        Core = core;
        DataService = dataService;
        StorageService = storageService;
        Logger = logger;

        Core.GameEvent.HookPost<EventPlayerDeath>(OnPlayerDeath);
    }

    public HookResult OnPlayerDeath(EventPlayerDeath @event)
    {
        var attacker = Core.PlayerManager.GetPlayer(@event.Attacker);
        if (attacker is not { IsValid: true, IsFakeClient: false } || !attacker.IsAlive())
        {
            return HookResult.Continue;
        }

        if (!@event.UserIdController.IsValid) return HookResult.Continue;
        if (@event.UserId == @event.Attacker) return HookResult.Continue;

        var weaponServices = attacker.PlayerPawn?.WeaponServices;
        if (weaponServices is not { IsValid: true }) return HookResult.Continue;

        var weaponHandle = weaponServices.ActiveWeapon;
        if (!weaponHandle.IsValid || weaponHandle.Value is not { IsValid: true } weapon)
        {
            return HookResult.Continue;
        }

        var item = weapon.AttributeManager.Item;
        var definitionIndex = item.ItemDefinitionIndex;
        var team = attacker.Controller.Team;

        if (Utilities.IsWeaponDefinitionIndex(definitionIndex))
        {
            if (DataService.WeaponDataService.TryGetSkin(attacker.SteamID, team, definitionIndex, out var skin))
            {
                if (skin.Quality != EconItemQuality.StatTrak) return HookResult.Continue;

                skin.StattrakCount++;
                var countBits = BitConverter.Int32BitsToSingle(skin.StattrakCount);
                item.AttributeList.SetOrAddAttribute("kill eater", countBits);
                item.AttributeList.SetOrAddAttribute("kill eater score type", 0);
                item.NetworkedDynamicAttributes.SetOrAddAttribute("kill eater", countBits);
                item.NetworkedDynamicAttributes.SetOrAddAttribute("kill eater score type", 0);
                weapon.FallbackStatTrak = skin.StattrakCount;

                item.AttributeListUpdated();
                item.NetworkedDynamicAttributesUpdated();
                weapon.FallbackStatTrakUpdated();

                DataService.WeaponDataService.StoreSkin(skin);
                PersistWeaponStatTrak(skin.DeepClone());
            }
        }
        else if (Utilities.IsKnifeDefinitionIndex(definitionIndex))
        {
            if (DataService.KnifeDataService.TryGetKnife(attacker.SteamID, team, out var knife))
            {
                if (knife.Quality != EconItemQuality.StatTrak) return HookResult.Continue;

                knife.StattrakCount++;
                var countBits = BitConverter.Int32BitsToSingle(knife.StattrakCount);
                item.AttributeList.SetOrAddAttribute("kill eater", countBits);
                item.AttributeList.SetOrAddAttribute("kill eater score type", 0);
                item.NetworkedDynamicAttributes.SetOrAddAttribute("kill eater", countBits);
                item.NetworkedDynamicAttributes.SetOrAddAttribute("kill eater score type", 0);
                weapon.FallbackStatTrak = knife.StattrakCount;

                item.AttributeListUpdated();
                item.NetworkedDynamicAttributesUpdated();
                weapon.FallbackStatTrakUpdated();

                DataService.KnifeDataService.StoreKnife(knife);
                PersistKnifeStatTrak(knife.DeepClone());
            }
        }

        return HookResult.Continue;
    }

    private void PersistWeaponStatTrak(WeaponSkinData skin)
    {
        var storage = StorageService.Get();
        if (storage is EmptyStorageProvider) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await storage.StoreSkinsAsync([skin]);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving StatTrak weapon kill count");
            }
        });
    }

    private void PersistKnifeStatTrak(KnifeSkinData knife)
    {
        var storage = StorageService.Get();
        if (storage is EmptyStorageProvider) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await storage.StoreKnifesAsync([knife]);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving StatTrak knife kill count");
            }
        });
    }
}