using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using MenuApi.API;

using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

using WeaponSkins.Econ;
using WeaponSkins.Extensions;
using WeaponSkins.Shared;

namespace WeaponSkins;

public partial class MenuService
{
    public void OpenMenuApiMainMenu(IPlayer player)
    {
        if (_menuApi == null || !player.IsValid) return;

        ulong steamId = player.SteamID;
        bool canWp = ItemPermissionService.CanUseWeaponSkins(steamId);
        bool canKnife = ItemPermissionService.CanUseKnifeSkins(steamId);
        bool canGloves = ItemPermissionService.CanUseGloveSkins(steamId);
        bool canStickers = ItemPermissionService.CanUseStickers(steamId);
        bool canKeychains = ItemPermissionService.CanUseKeychains(steamId);
        bool canAgents = ItemPermissionService.CanUseAgents(steamId);
        bool canMusic = ItemPermissionService.CanUseMusicKits(steamId);

        if (!canWp && !canKnife && !canGloves && !canMusic && !canAgents)
        {
            player.SendChat("[red][WeaponSkins][/] Bu menüyü kullanmak için yetkiniz bulunmuyor.");
            return;
        }

        string teamModeText = GetTeamModeDisplayName(steamId);
        ushort? handDefIndex = null;
        string handWeaponName = "Yok";
        if (TryGetWeaponInHand(player, out var wepInHand))
        {
            ushort defIdx = wepInHand.AttributeManager.Item.ItemDefinitionIndex;
            if (Utilities.IsWeaponDefinitionIndex(defIdx))
            {
                handDefIndex = defIdx;
                var itemDef = EconService.Items.Values.FirstOrDefault(i => i.Index == defIdx);
                handWeaponName = itemDef != null
                    ? EconService.GetLocalizedName(itemDef.LocalizedNames, player.PlayerLanguage.Value)
                    : $"Silah #{defIdx}";
            }
        }

        var builder = _menuApi.CreateBuilder($"ws_main_{steamId}", "🎨 SİLAH & SKİN YÖNETİMİ (!ws)")
            .SetSubtitle($"Hedef Takım: {teamModeText} | Eldeki: {handWeaponName}")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .SetTimeout(TimeSpan.FromSeconds(60))
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        // 0. Görsel 3D Kart Izgarasını Aç (!vs / !skins)
        builder.AddItem("🖼️ 3D Görsel Skin Seçiciyi Aç (!vs)", ctx =>
        {
            _menuApi.OpenPage(ctx.Player, "skins");
        }, closeOnSelect: false);

        // 1. Takım Seçimi
        builder.AddItem($"👥 Takım Seçimi: [{teamModeText}]", ctx =>
        {
            OpenMenuApiTeamSelectMenu(ctx.Player);
        }, closeOnSelect: false);

        // 2. Elindeki Silahın Hızlı Özelleştirme Menüsü (Float / Anahtarlık / Sticker)
        if (canWp || canStickers || canKeychains)
        {
            string handLabel = handDefIndex.HasValue
                ? $"✨ Elimdeki Silah ({handWeaponName}): Float / Sticker / Anahtarlık"
                : "✨ Silah Özelleştir: Float / Sticker / Anahtarlık";
            builder.AddItem(handLabel, ctx =>
            {
                if (handDefIndex.HasValue)
                {
                    OpenMenuApiWeaponCustomizeMenu(ctx.Player, handDefIndex.Value);
                }
                else
                {
                    OpenMenuApiSelectWeaponForCustomizeMenu(ctx.Player);
                }
            }, closeOnSelect: false);
        }

        // 3. Silah Kaplamaları
        if (canWp)
        {
            builder.AddItem("🔫 Silah Kaplamaları (Weapon Skins)", ctx =>
            {
                OpenMenuApiWeaponsRootMenu(ctx.Player);
            }, closeOnSelect: false);
        }

        // 4. Bıçak Seçimi & Bıçak Float/Seed
        if (canKnife)
        {
            builder.AddItem("🔪 Bıçak Seçimi & Bıçak Ayarları", ctx =>
            {
                OpenMenuApiKnivesMenu(ctx.Player);
            }, closeOnSelect: false);
        }

        // 5. Eldiven Seçimi & Eldiven Float/Seed
        if (canGloves)
        {
            builder.AddItem("🧤 Eldiven Seçimi & Eldiven Ayarları", ctx =>
            {
                OpenMenuApiGlovesMenu(ctx.Player);
            }, closeOnSelect: false);
        }

        // 6. Hızlı Alt Menüler: Sadece Float, Sadece Anahtarlık, Sadece Sticker
        if (canWp)
        {
            builder.AddItem("🌡️ Hızlı Float (Aşınma) Seçimi", ctx =>
            {
                if (handDefIndex.HasValue)
                    OpenMenuApiFloatMenu(ctx.Player, handDefIndex.Value);
                else
                    OpenMenuApiSelectWeaponForCustomizeMenu(ctx.Player);
            }, closeOnSelect: false);
        }

        if (canKeychains)
        {
            builder.AddItem("🔑 Hızlı Anahtarlık (Keychain) Seçimi", ctx =>
            {
                if (handDefIndex.HasValue)
                    OpenMenuApiKeychainMenu(ctx.Player, handDefIndex.Value);
                else
                    OpenMenuApiSelectWeaponForCustomizeMenu(ctx.Player);
            }, closeOnSelect: false);
        }

        if (canStickers)
        {
            builder.AddItem("🏷️ Hızlı Çıkartma (Sticker Slot 1-5) Seçimi", ctx =>
            {
                if (handDefIndex.HasValue)
                    OpenMenuApiStickerSlotsMenu(ctx.Player, handDefIndex.Value);
                else
                    OpenMenuApiSelectWeaponForCustomizeMenu(ctx.Player);
            }, closeOnSelect: false);
        }

        // 7. Ajanlar (T / CT)
        if (canAgents)
        {
            builder.AddItem("🕵️ Ajan Seçimi (T & CT Ajanları)", ctx =>
            {
                OpenMenuApiAgentsMenu(ctx.Player);
            }, closeOnSelect: false);
        }

        // 8. Müzik Kitleri
        if (canMusic)
        {
            builder.AddItem("🎵 Müzik Kitleri (Music Kits)", ctx =>
            {
                OpenMenuApiMusicKitsMenu(ctx.Player);
            }, closeOnSelect: false);
        }

        // 9. 3D Görsel Grid Menüsü
        builder.AddItem("🖼️ 3D Görsel Skin Grid Menüsünü Aç (!skins)", ctx =>
        {
            ctx.Player.ExecuteCommand("sw_skins");
        }, closeOnSelect: true);

        builder.Build().Open(player);
    }

    // =========================================================================
    // TAKIM SEÇİMİ MENÜSÜ (T / CT / HER İKİSİ / MEVCUT TAKIM)
    // =========================================================================
    public void OpenMenuApiTeamSelectMenu(IPlayer player)
    {
        if (_menuApi == null || !player.IsValid) return;

        var currentMode = GetPlayerTeamMode(player.SteamID);
        var builder = _menuApi.CreateBuilder($"ws_team_{player.SteamID}", "👥 HEDEF TAKIM SEÇİMİ")
            .SetSubtitle($"Şu anki seçim: {GetTeamModeDisplayName(player.SteamID)}")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        void AddModeItem(TargetTeamMode mode, string label)
        {
            string prefix = currentMode == mode ? "✓ " : "";
            builder.AddItem($"{prefix}{label}", ctx =>
            {
                SetPlayerTeamMode(ctx.Player.SteamID, mode);
                ctx.Player.SendChat($"[gold][WeaponSkins][/] Hedef takım seçimi [lime]{GetTeamModeDisplayName(ctx.Player.SteamID)}[/] olarak ayarlandı!");
                OpenMenuApiMainMenu(ctx.Player);
            }, closeOnSelect: false);
        }

        AddModeItem(TargetTeamMode.BothTeams, "🔄 Her İki Takım İçin Uygula (T & CT) [Önerilen]");
        AddModeItem(TargetTeamMode.OnlyT, "🔥 Sadece Terörist (T) Takımı İçin Uygula");
        AddModeItem(TargetTeamMode.OnlyCT, "🛡️ Sadece Anti-Terörist (CT) Takımı İçin Uygula");
        AddModeItem(TargetTeamMode.CurrentTeam, "🎯 Sadece O An Bulunduğum Takım İçin Uygula");

        builder.AddItem("⬅️ Ana Menüye Dön", ctx => OpenMenuApiMainMenu(ctx.Player), closeOnSelect: false);
        builder.Build().Open(player);
    }

    // =========================================================================
    // SİLAH ÖZELLEŞTİRME (FLOAT / ANAHTARLIK / STICKER / SEED / STATTRAK)
    // =========================================================================
    public void OpenMenuApiSelectWeaponForCustomizeMenu(IPlayer player)
    {
        if (_menuApi == null || !player.IsValid) return;

        var builder = _menuApi.CreateBuilder($"ws_selwep_cust_{player.SteamID}", "✨ ÖZELLEŞTİRİLECEK SİLAHI SEÇİN")
            .SetSubtitle($"Takım: {GetTeamModeDisplayName(player.SteamID)}")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        if (TryGetWeaponInHand(player, out var wepInHand) &&
            Utilities.IsWeaponDefinitionIndex(wepInHand.AttributeManager.Item.ItemDefinitionIndex))
        {
            ushort handIdx = wepInHand.AttributeManager.Item.ItemDefinitionIndex;
            string handName = GetWeaponDisplayName(player, handIdx);
            builder.AddItem($"✋ Elimdeki Silah: {handName}", ctx =>
            {
                OpenMenuApiWeaponCustomizeMenu(ctx.Player, handIdx);
            }, closeOnSelect: false);
        }

        foreach (var (weaponKey, _) in EconService.WeaponToPaintkits)
        {
            if (!EconService.Items.TryGetValue(weaponKey, out var item)) continue;
            if (!Utilities.IsWeaponDefinitionIndex(item.Index)) continue;

            ushort defIdx = (ushort)item.Index;
            string weaponName = EconService.GetLocalizedName(item.LocalizedNames, player.PlayerLanguage.Value);
            var data = GetOrCreateWeaponSkinData(player, defIdx);
            string paintName = GetPaintkitDisplayName(player, data.Paintkit);

            builder.AddItem($"{weaponName} [{paintName}]", ctx =>
            {
                OpenMenuApiWeaponCustomizeMenu(ctx.Player, defIdx);
            }, closeOnSelect: false);
        }

        builder.AddItem("⬅️ Ana Menüye Dön", ctx => OpenMenuApiMainMenu(ctx.Player), closeOnSelect: false);
        builder.Build().Open(player);
    }

    public void OpenMenuApiWeaponCustomizeMenu(IPlayer player, ushort defIndex)
    {
        if (_menuApi == null || !player.IsValid) return;

        var data = GetOrCreateWeaponSkinData(player, defIndex);
        string weaponName = GetWeaponDisplayName(player, defIndex);
        string paintName = GetPaintkitDisplayName(player, data.Paintkit);
        var kc = data.GetKeychain(0);
        string kcName = GetKeychainName(kc, player.PlayerLanguage.Value) ?? "Yok";
        int stickerCount = Enumerable.Range(0, 5).Count(s => data.GetSticker(s) is { Id: > 0 });

        var builder = _menuApi.CreateBuilder($"ws_cust_{player.SteamID}_{defIndex}", $"⚙️ {weaponName.ToUpperInvariant()} ÖZELLEŞTİR")
            .SetSubtitle($"Skin: {paintName} | Float: {data.PaintkitWear:F4} | Takım: {GetTeamModeDisplayName(player.SteamID)}")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        // 1. Float (Aşınma) Seçimi
        builder.AddItem($"🌡️ Float (Aşınma) Seçimi: [{data.PaintkitWear:F4} - {GetWearCategoryName(data.PaintkitWear)}]", ctx =>
        {
            OpenMenuApiFloatMenu(ctx.Player, defIndex);
        }, closeOnSelect: false);

        // 2. Anahtarlık (Keychain) Seçimi & Ayarları
        if (ItemPermissionService.CanUseKeychains(player.SteamID))
        {
            builder.AddItem($"🔑 Anahtarlık (Keychain): [{kcName}]", ctx =>
            {
                OpenMenuApiKeychainMenu(ctx.Player, defIndex);
            }, closeOnSelect: false);
        }

        // 3. Çıkartma (Sticker) Seçimi & Ayarları (Slot 1-5)
        if (ItemPermissionService.CanUseStickers(player.SteamID))
        {
            builder.AddItem($"🏷️ Çıkartmalar (Stickers): [{stickerCount}/5 Dolu]", ctx =>
            {
                OpenMenuApiStickerSlotsMenu(ctx.Player, defIndex);
            }, closeOnSelect: false);
        }

        // 4. Desen No (Seed / Pattern)
        builder.AddItem($"🎲 Desen No (Pattern / Seed): [{data.PaintkitSeed}]", ctx =>
        {
            OpenMenuApiSeedMenu(ctx.Player, defIndex);
        }, closeOnSelect: false);

        // 5. StatTrak™ Aç/Kapat
        bool isStatTrak = data.Quality == EconItemQuality.StatTrak;
        builder.AddItem($"🔥 StatTrak™: [{(isStatTrak ? $"AÇIK ({data.StattrakCount} Leş)" : "KAPALI")}]", ctx =>
        {
            UpdateWeaponSkinForSelectedTeams(ctx.Player, defIndex, skin =>
            {
                skin.Quality = isStatTrak ? EconItemQuality.Normal : EconItemQuality.StatTrak;
            }, true);
            ctx.Player.SendChat($"[gold][WeaponSkins][/] {weaponName} StatTrak™: [lime]{(!isStatTrak ? "Açık" : "Kapalı")}[/]");
            OpenMenuApiWeaponCustomizeMenu(ctx.Player, defIndex);
        }, closeOnSelect: false);

        // 6. StatTrak Sayısı Değiştir
        if (isStatTrak)
        {
            builder.AddItem($"🔢 StatTrak™ Leş Sayısını Değiştir: [{data.StattrakCount}]", ctx =>
            {
                PromptChatInput(ctx.Player, "Yeni StatTrak™ leş sayısını yazın:",
                    input => int.TryParse(input, out var v) && v >= 0,
                    (target, input) =>
                    {
                        int val = int.Parse(input);
                        UpdateWeaponSkinForSelectedTeams(target, defIndex, skin => skin.StattrakCount = val, true);
                        target.SendChat($"[gold][WeaponSkins][/] StatTrak™ sayısı [lime]{val}[/] olarak ayarlandı!");
                        OpenMenuApiWeaponCustomizeMenu(target, defIndex);
                    },
                    target => OpenMenuApiWeaponCustomizeMenu(target, defIndex));
            }, closeOnSelect: false);
        }

        // 7. Hatıra (Souvenir) Aç/Kapat
        bool isSouvenir = data.Quality == EconItemQuality.Souvenir;
        builder.AddItem($"🏆 Hatıra (Souvenir): [{(isSouvenir ? "AÇIK" : "KAPALI")}]", ctx =>
        {
            UpdateWeaponSkinForSelectedTeams(ctx.Player, defIndex, skin =>
            {
                skin.Quality = isSouvenir ? EconItemQuality.Normal : EconItemQuality.Souvenir;
            }, true);
            ctx.Player.SendChat($"[gold][WeaponSkins][/] {weaponName} Hatıra (Souvenir): [lime]{(!isSouvenir ? "Açık" : "Kapalı")}[/]");
            OpenMenuApiWeaponCustomizeMenu(ctx.Player, defIndex);
        }, closeOnSelect: false);

        // 8. İsim Etiketi (NameTag)
        string nametagDisplay = string.IsNullOrWhiteSpace(data.Nametag) ? "Yok" : data.Nametag;
        builder.AddItem($"🏷️ İsim Etiketi (NameTag): [{nametagDisplay}]", ctx =>
        {
            PromptChatInput(ctx.Player, "Yeni isim etiketini yazın (Kaldırmak için '-' yazın):",
                input => !string.IsNullOrWhiteSpace(input) && input.Length <= 32,
                (target, input) =>
                {
                    string? tag = input.Trim() == "-" ? null : input.Trim();
                    UpdateWeaponSkinForSelectedTeams(target, defIndex, skin => skin.Nametag = tag, true);
                    target.SendChat($"[gold][WeaponSkins][/] İsim etiketi [lime]{(tag ?? "Kaldırıldı")}[/] olarak güncellendi!");
                    OpenMenuApiWeaponCustomizeMenu(target, defIndex);
                },
                target => OpenMenuApiWeaponCustomizeMenu(target, defIndex));
        }, closeOnSelect: false);

        // 9. Bu Silahın Kaplamasını (Paintkit) Değiştir
        var itemDef = EconService.Items.Values.FirstOrDefault(i => i.Index == defIndex);
        if (itemDef != null)
        {
            var weaponKey = EconService.Items.FirstOrDefault(kv => kv.Value.Index == defIndex).Key;
            if (!string.IsNullOrEmpty(weaponKey) && EconService.WeaponToPaintkits.TryGetValue(weaponKey, out var paintkits))
            {
                builder.AddItem($"🎨 {weaponName} Kaplamasını (Skin) Değiştir", ctx =>
                {
                    OpenMenuApiWeaponPaintkitsMenu(ctx.Player, itemDef, paintkits);
                }, closeOnSelect: false);
            }
        }

        // 10. Takım Seçimini Değiştir
        builder.AddItem($"👥 Hedef Takımı Değiştir: [{GetTeamModeDisplayName(player.SteamID)}]", ctx =>
        {
            CyclePlayerTeamMode(ctx.Player.SteamID);
            ctx.Player.SendChat($"[gold][WeaponSkins][/] Hedef takım: [lime]{GetTeamModeDisplayName(ctx.Player.SteamID)}[/]");
            OpenMenuApiWeaponCustomizeMenu(ctx.Player, defIndex);
        }, closeOnSelect: false);

        builder.AddItem("⬅️ Ana Menüye Dön", ctx => OpenMenuApiMainMenu(ctx.Player), closeOnSelect: false);
        builder.Build().Open(player);
    }

    // =========================================================================
    // FLOAT (AŞINMA) SEÇİMİ MENÜSÜ
    // =========================================================================
    public void OpenMenuApiFloatMenu(IPlayer player, ushort defIndex)
    {
        if (_menuApi == null || !player.IsValid) return;

        var data = GetOrCreateWeaponSkinData(player, defIndex);
        string weaponName = GetWeaponDisplayName(player, defIndex);

        var builder = _menuApi.CreateBuilder($"ws_float_{player.SteamID}_{defIndex}", $"🌡️ {weaponName.ToUpperInvariant()} FLOAT SEÇİMİ")
            .SetSubtitle($"Mevcut Float: {data.PaintkitWear:F4} ({GetWearCategoryName(data.PaintkitWear)}) | Takım: {GetTeamModeDisplayName(player.SteamID)}")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        (float Value, string Label)[] presets =
        [
            (0.0001f, "✨ 0.0001 - Factory New (Fabrikadan Yeni Çıkmış - Kusursuz)"),
            (0.0100f, "✨ 0.0100 - Factory New (Fabrikadan Yeni Çıkmış)"),
            (0.0701f, "🔹 0.0701 - Minimal Wear (Az Aşınmış)"),
            (0.1501f, "🔸 0.1501 - Field-Tested (Görevde Kullanılmış)"),
            (0.3801f, "🟠 0.3801 - Well-Worn (Eskimiş)"),
            (0.4501f, "🔴 0.4501 - Battle-Scarred (Savaş Görmüş)"),
            (0.9900f, "💀 0.9900 - Maksimum Aşınmış (Rust / Blackiimov)")
        ];

        foreach (var preset in presets)
        {
            float val = preset.Value;
            bool isSelected = MathF.Abs(data.PaintkitWear - val) < 0.002f;
            builder.AddItem($"{(isSelected ? "✓ " : "")}{preset.Label}", ctx =>
            {
                UpdateWeaponSkinForSelectedTeams(ctx.Player, defIndex, skin => skin.PaintkitWear = val, true);
                ctx.Player.SendChat($"[gold][WeaponSkins][/] {weaponName} Float değeri [lime]{val:F4}[/] ({GetWearCategoryName(val)}) olarak ayarlandı!");
                OpenMenuApiFloatMenu(ctx.Player, defIndex);
            }, closeOnSelect: false);
        }

        builder.AddItem("✏️ Özel Float Değeri Yaz (Sohbetten: 0.0000 - 1.0000)", ctx =>
        {
            PromptChatInput(ctx.Player, "Yeni Float (Aşınma) değerini yazın (0.00 - 1.00, örn: 0.005):",
                input => float.TryParse(input.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v is >= 0.0f and <= 1.0f,
                (target, input) =>
                {
                    float val = float.Parse(input.Replace(',', '.'), CultureInfo.InvariantCulture);
                    UpdateWeaponSkinForSelectedTeams(target, defIndex, skin => skin.PaintkitWear = val, true);
                    target.SendChat($"[gold][WeaponSkins][/] {weaponName} Float değeri [lime]{val:F4}[/] olarak ayarlandı!");
                    OpenMenuApiFloatMenu(target, defIndex);
                },
                target => OpenMenuApiFloatMenu(target, defIndex));
        }, closeOnSelect: false);

        builder.AddItem("⬅️ Silah Özelleştirme Menüsüne Dön", ctx => OpenMenuApiWeaponCustomizeMenu(ctx.Player, defIndex), closeOnSelect: false);
        builder.Build().Open(player);
    }

    // =========================================================================
    // SEED (DESEN NO) SEÇİMİ MENÜSÜ
    // =========================================================================
    public void OpenMenuApiSeedMenu(IPlayer player, ushort defIndex)
    {
        if (_menuApi == null || !player.IsValid) return;

        var data = GetOrCreateWeaponSkinData(player, defIndex);
        string weaponName = GetWeaponDisplayName(player, defIndex);

        var builder = _menuApi.CreateBuilder($"ws_seed_{player.SteamID}_{defIndex}", $"🎲 {weaponName.ToUpperInvariant()} SEED SEÇİMİ")
            .SetSubtitle($"Mevcut Desen No (Seed): {data.PaintkitSeed} | Takım: {GetTeamModeDisplayName(player.SteamID)}")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        builder.AddItem("✏️ Özel Seed Numarası Yaz (0 - 1000)", ctx =>
        {
            PromptChatInput(ctx.Player, "Yeni Seed (Desen) numarasını yazın (0 - 1000):",
                input => int.TryParse(input, out var v) && v >= 0,
                (target, input) =>
                {
                    int val = int.Parse(input);
                    UpdateWeaponSkinForSelectedTeams(target, defIndex, skin => skin.PaintkitSeed = val, true);
                    target.SendChat($"[gold][WeaponSkins][/] {weaponName} Seed değeri [lime]{val}[/] olarak ayarlandı!");
                    OpenMenuApiSeedMenu(target, defIndex);
                },
                target => OpenMenuApiSeedMenu(target, defIndex));
        }, closeOnSelect: false);

        (int Seed, string Label)[] popularSeeds =
        [
            (0, "🔄 Seed 0 (Varsayılan)"),
            (661, "💎 Seed 661 (AK-47 Case Hardened #1 Blue Gem)"),
            (387, "💎 Seed 387 (Blue Gem Pattern)"),
            (670, "💎 Seed 670 (Blue Gem Pattern)"),
            (321, "💎 Seed 321 (Blue Gem Pattern)"),
            (412, "🔥 Seed 412 (Max Fire & Ice / Fade)"),
            (763, "🌟 Seed 763 (100% Full Fade)")
        ];

        foreach (var preset in popularSeeds)
        {
            int seedVal = preset.Seed;
            bool isSelected = data.PaintkitSeed == seedVal;
            builder.AddItem($"{(isSelected ? "✓ " : "")}{preset.Label}", ctx =>
            {
                UpdateWeaponSkinForSelectedTeams(ctx.Player, defIndex, skin => skin.PaintkitSeed = seedVal, true);
                ctx.Player.SendChat($"[gold][WeaponSkins][/] {weaponName} Seed değeri [lime]{seedVal}[/] olarak ayarlandı!");
                OpenMenuApiSeedMenu(ctx.Player, defIndex);
            }, closeOnSelect: false);
        }

        builder.AddItem("⬅️ Silah Özelleştirme Menüsüne Dön", ctx => OpenMenuApiWeaponCustomizeMenu(ctx.Player, defIndex), closeOnSelect: false);
        builder.Build().Open(player);
    }

    // =========================================================================
    // ANAHTARLIK (KEYCHAIN / CHARM) SEÇİMİ VE AYARLARI MENÜSÜ
    // =========================================================================
    public void OpenMenuApiKeychainMenu(IPlayer player, ushort defIndex)
    {
        if (_menuApi == null || !player.IsValid) return;

        var data = GetOrCreateWeaponSkinData(player, defIndex);
        string weaponName = GetWeaponDisplayName(player, defIndex);
        var kc = data.GetKeychain(0);
        string kcName = GetKeychainName(kc, player.PlayerLanguage.Value) ?? "Yok";

        var builder = _menuApi.CreateBuilder($"ws_kc_{player.SteamID}_{defIndex}", $"🔑 {weaponName.ToUpperInvariant()} ANAHTARLIK")
            .SetSubtitle($"Seçili: {kcName} | Takım: {GetTeamModeDisplayName(player.SteamID)}")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        builder.AddItem("📿 Anahtarlık Seç / Değiştir (Tüm Anahtarlıklar)", ctx =>
        {
            OpenMenuApiKeychainBrowserMenu(ctx.Player, defIndex, null);
        }, closeOnSelect: false);

        builder.AddItem("🔍 İsimle Anahtarlık Ara (Sohbetten)", ctx =>
        {
            PromptChatInput(ctx.Player, "Aramak istediğiniz anahtarlık adını yazın:",
                input => !string.IsNullOrWhiteSpace(input),
                (target, input) => OpenMenuApiKeychainBrowserMenu(target, defIndex, input.Trim()),
                target => OpenMenuApiKeychainMenu(target, defIndex));
        }, closeOnSelect: false);

        if (kc != null && kc.Id > 0)
        {
            builder.AddItem($"🎲 Anahtarlık Deseni (Seed): [{kc.Seed}]", ctx =>
            {
                PromptChatInput(ctx.Player, "Yeni Anahtarlık Seed değerini yazın (Örn: 1 - 100000):",
                    input => int.TryParse(input, out var v) && v >= 0,
                    (target, input) =>
                    {
                        int val = int.Parse(input);
                        UpdateWeaponSkinForSelectedTeams(target, defIndex, skin =>
                        {
                            var k = skin.GetKeychain(0) ?? new KeychainData { Id = kc.Id };
                            k.Seed = val;
                            skin.SetKeychain(0, k);
                        }, true);
                        target.SendChat($"[gold][WeaponSkins][/] Anahtarlık Seed değeri [lime]{val}[/] olarak ayarlandı!");
                        OpenMenuApiKeychainMenu(target, defIndex);
                    },
                    target => OpenMenuApiKeychainMenu(target, defIndex));
            }, closeOnSelect: false);

            builder.AddItem($"📐 Anahtarlık Konumu (Offset X:{kc.OffsetX:F1} Y:{kc.OffsetY:F1} Z:{kc.OffsetZ:F1})", ctx =>
            {
                OpenMenuApiKeychainOffsetMenu(ctx.Player, defIndex);
            }, closeOnSelect: false);

            builder.AddItem("🗑️ Anahtarlığı Kaldır", ctx =>
            {
                UpdateWeaponSkinForSelectedTeams(ctx.Player, defIndex, skin => skin.SetKeychain(0, null), true);
                ctx.Player.SendChat($"[gold][WeaponSkins][/] {weaponName} üzerindeki anahtarlık kaldırıldı.");
                OpenMenuApiKeychainMenu(ctx.Player, defIndex);
            }, closeOnSelect: false);
        }

        builder.AddItem("⬅️ Silah Özelleştirme Menüsüne Dön", ctx => OpenMenuApiWeaponCustomizeMenu(ctx.Player, defIndex), closeOnSelect: false);
        builder.Build().Open(player);
    }

    public void OpenMenuApiKeychainBrowserMenu(IPlayer player, ushort defIndex, string? searchFilter)
    {
        if (_menuApi == null || !player.IsValid) return;

        var data = GetOrCreateWeaponSkinData(player, defIndex);
        int currentKcId = data.GetKeychain(0)?.Id ?? 0;
        string weaponName = GetWeaponDisplayName(player, defIndex);

        var sorted = EconService.Keychains.Values
            .OrderByDescending(k => k.Rarity.Id)
            .ThenBy(k => EconService.GetLocalizedName(k.LocalizedNames, player.PlayerLanguage.Value))
            .ToList();

        if (!string.IsNullOrWhiteSpace(searchFilter))
        {
            sorted = sorted.Where(k =>
                EconService.GetLocalizedName(k.LocalizedNames, player.PlayerLanguage.Value)
                    .Contains(searchFilter, StringComparison.OrdinalIgnoreCase) ||
                k.Name.Contains(searchFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var builder = _menuApi.CreateBuilder($"ws_kc_list_{player.SteamID}_{defIndex}", $"📿 ANAHTARLIK SEÇ ({sorted.Count} ADET)")
            .SetSubtitle($"Silah: {weaponName} | Takım: {GetTeamModeDisplayName(player.SteamID)}")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        builder.AddItem("⬅️ Anahtarlık Menüsüne Dön", ctx => OpenMenuApiKeychainMenu(ctx.Player, defIndex), closeOnSelect: false);

        foreach (var keychain in sorted)
        {
            var kcDef = keychain;
            string kcName = EconService.GetLocalizedName(kcDef.LocalizedNames, player.PlayerLanguage.Value);
            string prefix = currentKcId == kcDef.Index ? "✓ " : "";

            builder.AddItem($"{prefix}{kcName}", ctx =>
            {
                UpdateWeaponSkinForSelectedTeams(ctx.Player, defIndex, skin =>
                {
                    var prev = skin.GetKeychain(0);
                    skin.SetKeychain(0, new KeychainData
                    {
                        Id = kcDef.Index,
                        Seed = prev?.Seed ?? 0,
                        OffsetX = prev?.OffsetX ?? 0f,
                        OffsetY = prev?.OffsetY ?? 0f,
                        OffsetZ = prev?.OffsetZ ?? 0f
                    });
                }, true);
                ctx.Player.SendChat($"[gold][WeaponSkins][/] Anahtarlık [lime]{kcName}[/] olarak seçildi ({GetTeamModeDisplayName(ctx.Player.SteamID)})!");
                OpenMenuApiKeychainMenu(ctx.Player, defIndex);
            }, closeOnSelect: false);
        }

        builder.Build().Open(player);
    }

    public void OpenMenuApiKeychainOffsetMenu(IPlayer player, ushort defIndex)
    {
        if (_menuApi == null || !player.IsValid) return;

        var data = GetOrCreateWeaponSkinData(player, defIndex);
        var kc = data.GetKeychain(0);
        if (kc == null || kc.Id <= 0)
        {
            OpenMenuApiKeychainMenu(player, defIndex);
            return;
        }

        var builder = _menuApi.CreateBuilder($"ws_kc_ofs_{player.SteamID}_{defIndex}", "📐 ANAHTARLIK KONUMU (OFFSET)")
            .SetSubtitle($"X: {kc.OffsetX:F2} | Y: {kc.OffsetY:F2} | Z: {kc.OffsetZ:F2}")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        void AdjustOffset(float dx, float dy, float dz)
        {
            UpdateWeaponSkinForSelectedTeams(player, defIndex, skin =>
            {
                var k = skin.GetKeychain(0);
                if (k != null)
                {
                    k.OffsetX = MathF.Round(k.OffsetX + dx, 2);
                    k.OffsetY = MathF.Round(k.OffsetY + dy, 2);
                    k.OffsetZ = MathF.Round(k.OffsetZ + dz, 2);
                }
            }, true);
            OpenMenuApiKeychainOffsetMenu(player, defIndex);
        }

        builder.AddItem($"↔️ X Ekseni İleri (+0.5) [Şu an: {kc.OffsetX:F2}]", _ => AdjustOffset(0.5f, 0f, 0f), closeOnSelect: false);
        builder.AddItem($"↔️ X Ekseni Geri (-0.5) [Şu an: {kc.OffsetX:F2}]", _ => AdjustOffset(-0.5f, 0f, 0f), closeOnSelect: false);
        builder.AddItem($"↕️ Y Ekseni (+0.5) [Şu an: {kc.OffsetY:F2}]", _ => AdjustOffset(0f, 0.5f, 0f), closeOnSelect: false);
        builder.AddItem($"↕️ Y Ekseni (-0.5) [Şu an: {kc.OffsetY:F2}]", _ => AdjustOffset(0f, -0.5f, 0f), closeOnSelect: false);
        builder.AddItem($"⬆️ Z Ekseni Yukarı (+0.5) [Şu an: {kc.OffsetZ:F2}]", _ => AdjustOffset(0f, 0f, 0.5f), closeOnSelect: false);
        builder.AddItem($"⬇️ Z Ekseni Aşağı (-0.5) [Şu an: {kc.OffsetZ:F2}]", _ => AdjustOffset(0f, 0f, -0.5f), closeOnSelect: false);

        builder.AddItem("🔄 Konumu Varsayılana Sıfırla (0, 0, 0)", ctx =>
        {
            UpdateWeaponSkinForSelectedTeams(ctx.Player, defIndex, skin =>
            {
                var k = skin.GetKeychain(0);
                if (k != null)
                {
                    k.OffsetX = 0f;
                    k.OffsetY = 0f;
                    k.OffsetZ = 0f;
                }
            }, true);
            ctx.Player.SendChat("[gold][WeaponSkins][/] Anahtarlık konumu sıfırlandı.");
            OpenMenuApiKeychainOffsetMenu(ctx.Player, defIndex);
        }, closeOnSelect: false);

        builder.AddItem("⬅️ Anahtarlık Menüsüne Dön", ctx => OpenMenuApiKeychainMenu(ctx.Player, defIndex), closeOnSelect: false);
        builder.Build().Open(player);
    }

    // =========================================================================
    // ÇIKARTMA (STICKER SLOT 1 - 5) SEÇİMİ VE AYARLARI MENÜSÜ
    // =========================================================================
    public void OpenMenuApiStickerSlotsMenu(IPlayer player, ushort defIndex)
    {
        if (_menuApi == null || !player.IsValid) return;

        var data = GetOrCreateWeaponSkinData(player, defIndex);
        string weaponName = GetWeaponDisplayName(player, defIndex);

        var builder = _menuApi.CreateBuilder($"ws_stk_slots_{player.SteamID}_{defIndex}", $"🏷️ {weaponName.ToUpperInvariant()} ÇIKARTMALAR")
            .SetSubtitle($"Takım: {GetTeamModeDisplayName(player.SteamID)} | Slot Seçin (1 - 5)")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        for (int i = 0; i < 5; i++)
        {
            int slot = i;
            var stk = data.GetSticker(slot);
            string stkName = GetStickerName(stk, player.PlayerLanguage.Value) ?? "Boş";
            string wearInfo = stk is { Id: > 0 } ? $" (Aşınma: {stk.Wear:F2})" : "";

            builder.AddItem($"🏷️ Slot {slot + 1}: [{stkName}{wearInfo}]", ctx =>
            {
                OpenMenuApiStickerSlotDetailMenu(ctx.Player, defIndex, slot);
            }, closeOnSelect: false);
        }

        builder.AddItem("🗑️ Tüm Slotlardaki Çıkartmaları Temizle (Slot 1-5)", ctx =>
        {
            UpdateWeaponSkinForSelectedTeams(ctx.Player, defIndex, skin =>
            {
                for (int s = 0; s < 5; s++)
                    skin.SetSticker(s, null);
            }, true);
            ctx.Player.SendChat($"[gold][WeaponSkins][/] {weaponName} üzerindeki tüm çıkartmalar temizlendi.");
            OpenMenuApiStickerSlotsMenu(ctx.Player, defIndex);
        }, closeOnSelect: false);

        builder.AddItem("⬅️ Silah Özelleştirme Menüsüne Dön", ctx => OpenMenuApiWeaponCustomizeMenu(ctx.Player, defIndex), closeOnSelect: false);
        builder.Build().Open(player);
    }

    public void OpenMenuApiStickerSlotDetailMenu(IPlayer player, ushort defIndex, int slot)
    {
        if (_menuApi == null || !player.IsValid) return;

        var data = GetOrCreateWeaponSkinData(player, defIndex);
        var stk = data.GetSticker(slot);
        string stkName = GetStickerName(stk, player.PlayerLanguage.Value) ?? "Boş";
        string weaponName = GetWeaponDisplayName(player, defIndex);

        var builder = _menuApi.CreateBuilder($"ws_stk_det_{player.SteamID}_{defIndex}_{slot}", $"🏷️ SLOT {slot + 1} ÇIKARTMA YÖNETİMİ")
            .SetSubtitle($"Silah: {weaponName} | Seçili: {stkName}")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        builder.AddItem("📂 Koleksiyondan Çıkartma Seç", ctx =>
        {
            OpenMenuApiStickerCollectionsMenu(ctx.Player, defIndex, slot);
        }, closeOnSelect: false);

        builder.AddItem("🔍 İsimle Çıkartma Ara (Sohbetten)", ctx =>
        {
            PromptChatInput(ctx.Player, $"Slot {slot + 1} için aramak istediğiniz çıkartma adını yazın (Örn: Titan, iBUYPOWER, Crown):",
                input => !string.IsNullOrWhiteSpace(input),
                (target, input) => OpenMenuApiStickerSearchResultsMenu(target, defIndex, slot, input.Trim()),
                target => OpenMenuApiStickerSlotDetailMenu(target, defIndex, slot));
        }, closeOnSelect: false);

        if (stk != null && stk.Id > 0)
        {
            builder.AddItem($"🌡️ Çıkartma Aşınması (Scrape / Wear): [{stk.Wear:F2}]", ctx =>
            {
                OpenMenuApiStickerWearMenu(ctx.Player, defIndex, slot);
            }, closeOnSelect: false);

            builder.AddItem($"📐 Çıkartma Konumu (Offset X:{stk.OffsetX:F2} Y:{stk.OffsetY:F2})", ctx =>
            {
                OpenMenuApiStickerOffsetMenu(ctx.Player, defIndex, slot);
            }, closeOnSelect: false);

            builder.AddItem($"📋 Bu Çıkartmayı Tüm Slotlara (1-5) Uygula (5x Craft)", ctx =>
            {
                UpdateWeaponSkinForSelectedTeams(ctx.Player, defIndex, skin =>
                {
                    for (int s = 0; s < 5; s++)
                    {
                        skin.SetSticker(s, new StickerData
                        {
                            Id = stk.Id,
                            Wear = stk.Wear
                        });
                    }
                }, true);
                ctx.Player.SendChat($"[gold][WeaponSkins][/] [lime]{stkName}[/] çıkartması 5 slota birden uygulandı!");
                OpenMenuApiStickerSlotsMenu(ctx.Player, defIndex);
            }, closeOnSelect: false);

            builder.AddItem($"🗑️ Slot {slot + 1} Çıkartmasını Kaldır", ctx =>
            {
                UpdateWeaponSkinForSelectedTeams(ctx.Player, defIndex, skin => skin.SetSticker(slot, null), true);
                ctx.Player.SendChat($"[gold][WeaponSkins][/] Slot {slot + 1} çıkartması kaldırıldı.");
                OpenMenuApiStickerSlotsMenu(ctx.Player, defIndex);
            }, closeOnSelect: false);
        }

        builder.AddItem("⬅️ Çıkartma Slotlarına Dön", ctx => OpenMenuApiStickerSlotsMenu(ctx.Player, defIndex), closeOnSelect: false);
        builder.Build().Open(player);
    }

    public void OpenMenuApiStickerCollectionsMenu(IPlayer player, ushort defIndex, int slot)
    {
        if (_menuApi == null || !player.IsValid) return;

        var builder = _menuApi.CreateBuilder($"ws_stk_cols_{player.SteamID}_{defIndex}_{slot}", $"📂 SLOT {slot + 1}: KOLEKSİYON SEÇİN")
            .SetSubtitle($"Toplam {EconService.StickerCollections.Count} Koleksiyon")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        builder.AddItem("⬅️ Slot Yönetimine Dön", ctx => OpenMenuApiStickerSlotDetailMenu(ctx.Player, defIndex, slot), closeOnSelect: false);

        foreach (var (_, collection) in EconService.StickerCollections)
        {
            var col = collection;
            string colName = EconService.GetLocalizedName(col.LocalizedNames, player.PlayerLanguage.Value);
            builder.AddItem($"📁 {colName} ({col.Stickers.Count})", ctx =>
            {
                OpenMenuApiStickerListMenu(ctx.Player, defIndex, slot, col);
            }, closeOnSelect: false);
        }

        builder.Build().Open(player);
    }

    public void OpenMenuApiStickerListMenu(IPlayer player, ushort defIndex, int slot, StickerCollectionDefinition collection)
    {
        if (_menuApi == null || !player.IsValid) return;

        var data = GetOrCreateWeaponSkinData(player, defIndex);
        int currentId = data.GetSticker(slot)?.Id ?? 0;
        string colName = EconService.GetLocalizedName(collection.LocalizedNames, player.PlayerLanguage.Value);

        var builder = _menuApi.CreateBuilder($"ws_stk_list_{player.SteamID}_{defIndex}_{slot}", $"🏷️ {colName}")
            .SetSubtitle($"Slot {slot + 1} İçin Çıkartma Seçin | Takım: {GetTeamModeDisplayName(player.SteamID)}")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        builder.AddItem("⬅️ Koleksiyonlara Dön", ctx => OpenMenuApiStickerCollectionsMenu(ctx.Player, defIndex, slot), closeOnSelect: false);

        foreach (var sticker in collection.Stickers)
        {
            if (sticker.Index == 0) continue;
            var stkDef = sticker;
            string stkName = EconService.GetLocalizedName(stkDef.LocalizedNames, player.PlayerLanguage.Value);
            string prefix = currentId == stkDef.Index ? "✓ " : "";

            builder.AddItem($"{prefix}{stkName}", ctx =>
            {
                UpdateWeaponSkinForSelectedTeams(ctx.Player, defIndex, skin =>
                {
                    skin.SetSticker(slot, new StickerData { Id = stkDef.Index });
                }, true);
                ctx.Player.SendChat($"[gold][WeaponSkins][/] Slot {slot + 1} çıkartması [lime]{stkName}[/] olarak ayarlandı!");
                OpenMenuApiStickerSlotDetailMenu(ctx.Player, defIndex, slot);
            }, closeOnSelect: false);
        }

        builder.Build().Open(player);
    }

    public void OpenMenuApiStickerSearchResultsMenu(IPlayer player, ushort defIndex, int slot, string query)
    {
        if (_menuApi == null || !player.IsValid) return;

        var matches = new List<StickerDefinition>();
        foreach (var (_, col) in EconService.StickerCollections)
        {
            foreach (var sticker in col.Stickers)
            {
                if (sticker.Index == 0) continue;
                string name = EconService.GetLocalizedName(sticker.LocalizedNames, player.PlayerLanguage.Value);
                if (name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    sticker.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(sticker);
                    if (matches.Count >= 120) break;
                }
            }
            if (matches.Count >= 120) break;
        }

        var builder = _menuApi.CreateBuilder($"ws_stk_search_{player.SteamID}_{defIndex}_{slot}", $"🔍 ARAMA: \"{query}\" ({matches.Count})")
            .SetSubtitle($"Slot {slot + 1} Çıkartma Arama Sonuçları")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        builder.AddItem("⬅️ Slot Yönetimine Dön", ctx => OpenMenuApiStickerSlotDetailMenu(ctx.Player, defIndex, slot), closeOnSelect: false);

        if (matches.Count == 0)
        {
            builder.AddText("Eşleşen çıkartma bulunamadı.");
        }
        else
        {
            foreach (var sticker in matches)
            {
                var stkDef = sticker;
                string stkName = EconService.GetLocalizedName(stkDef.LocalizedNames, player.PlayerLanguage.Value);
                builder.AddItem(stkName, ctx =>
                {
                    UpdateWeaponSkinForSelectedTeams(ctx.Player, defIndex, skin =>
                    {
                        skin.SetSticker(slot, new StickerData { Id = stkDef.Index });
                    }, true);
                    ctx.Player.SendChat($"[gold][WeaponSkins][/] Slot {slot + 1} çıkartması [lime]{stkName}[/] olarak ayarlandı!");
                    OpenMenuApiStickerSlotDetailMenu(ctx.Player, defIndex, slot);
                }, closeOnSelect: false);
            }
        }

        builder.Build().Open(player);
    }

    public void OpenMenuApiStickerWearMenu(IPlayer player, ushort defIndex, int slot)
    {
        if (_menuApi == null || !player.IsValid) return;

        var data = GetOrCreateWeaponSkinData(player, defIndex);
        var stk = data.GetSticker(slot);
        if (stk == null || stk.Id <= 0)
        {
            OpenMenuApiStickerSlotDetailMenu(player, defIndex, slot);
            return;
        }

        var builder = _menuApi.CreateBuilder($"ws_stk_wear_{player.SteamID}_{defIndex}_{slot}", $"🌡️ SLOT {slot + 1} ÇIKARTMA AŞINMASI")
            .SetSubtitle($"Mevcut Kazınma: {stk.Wear:F2}")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        (float Wear, string Label)[] presets =
        [
            (0.00f, "✨ 0.00 - %0 Kazınmamış (Yepyeni)"),
            (0.25f, "🔹 0.25 - %25 Hafif Kazınmış"),
            (0.50f, "🔸 0.50 - %50 Yarı Kazınmış"),
            (0.75f, "🟠 0.75 - %75 Çok Kazınmış"),
            (0.92f, "🔴 0.92 - %92 Sadece İmza / Logo Görünümü")
        ];

        foreach (var preset in presets)
        {
            float w = preset.Wear;
            builder.AddItem(preset.Label, ctx =>
            {
                UpdateWeaponSkinForSelectedTeams(ctx.Player, defIndex, skin =>
                {
                    var s = skin.GetSticker(slot);
                    if (s != null) s.Wear = w;
                }, true);
                ctx.Player.SendChat($"[gold][WeaponSkins][/] Slot {slot + 1} çıkartma aşınması [lime]{w:F2}[/] olarak ayarlandı!");
                OpenMenuApiStickerSlotDetailMenu(ctx.Player, defIndex, slot);
            }, closeOnSelect: false);
        }

        builder.AddItem("✏️ Özel Aşınma Değeri Yaz (0.00 - 1.00)", ctx =>
        {
            PromptChatInput(ctx.Player, "Yeni çıkartma aşınma değerini yazın (0.00 - 1.00):",
                input => float.TryParse(input.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v is >= 0.0f and <= 1.0f,
                (target, input) =>
                {
                    float val = float.Parse(input.Replace(',', '.'), CultureInfo.InvariantCulture);
                    UpdateWeaponSkinForSelectedTeams(target, defIndex, skin =>
                    {
                        var s = skin.GetSticker(slot);
                        if (s != null) s.Wear = val;
                    }, true);
                    target.SendChat($"[gold][WeaponSkins][/] Slot {slot + 1} çıkartma aşınması [lime]{val:F2}[/] yapıldı!");
                    OpenMenuApiStickerSlotDetailMenu(target, defIndex, slot);
                },
                target => OpenMenuApiStickerWearMenu(target, defIndex, slot));
        }, closeOnSelect: false);

        builder.AddItem("⬅️ Slot Yönetimine Dön", ctx => OpenMenuApiStickerSlotDetailMenu(ctx.Player, defIndex, slot), closeOnSelect: false);
        builder.Build().Open(player);
    }

    public void OpenMenuApiStickerOffsetMenu(IPlayer player, ushort defIndex, int slot)
    {
        if (_menuApi == null || !player.IsValid) return;

        var data = GetOrCreateWeaponSkinData(player, defIndex);
        var stk = data.GetSticker(slot);
        if (stk == null || stk.Id <= 0)
        {
            OpenMenuApiStickerSlotDetailMenu(player, defIndex, slot);
            return;
        }

        var builder = _menuApi.CreateBuilder($"ws_stk_ofs_{player.SteamID}_{defIndex}_{slot}", $"📐 SLOT {slot + 1} ÇIKARTMA KONUMU")
            .SetSubtitle($"Offset X: {stk.OffsetX:F2} | Offset Y: {stk.OffsetY:F2}")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        void AdjustStickerOffset(float dx, float dy)
        {
            UpdateWeaponSkinForSelectedTeams(player, defIndex, skin =>
            {
                var s = skin.GetSticker(slot);
                if (s != null)
                {
                    s.OffsetX = MathF.Round(s.OffsetX + dx, 3);
                    s.OffsetY = MathF.Round(s.OffsetY + dy, 3);
                    s.Schema = 0;
                }
            }, true);
            OpenMenuApiStickerOffsetMenu(player, defIndex, slot);
        }

        builder.AddItem($"➡️ X Sağa/İleri (+0.02) [X: {stk.OffsetX:F2}]", _ => AdjustStickerOffset(0.02f, 0f), closeOnSelect: false);
        builder.AddItem($"⬅️ X Sola/Geri (-0.02) [X: {stk.OffsetX:F2}]", _ => AdjustStickerOffset(-0.02f, 0f), closeOnSelect: false);
        builder.AddItem($"⬆️ Y Yukarı (+0.02) [Y: {stk.OffsetY:F2}]", _ => AdjustStickerOffset(0f, 0.02f), closeOnSelect: false);
        builder.AddItem($"⬇️ Y Aşağı (-0.02) [Y: {stk.OffsetY:F2}]", _ => AdjustStickerOffset(0f, -0.02f), closeOnSelect: false);

        builder.AddItem("🔄 Konumu Sıfırla (0.00, 0.00)", ctx =>
        {
            UpdateWeaponSkinForSelectedTeams(ctx.Player, defIndex, skin =>
            {
                var s = skin.GetSticker(slot);
                if (s != null)
                {
                    s.OffsetX = 0f;
                    s.OffsetY = 0f;
                    s.Schema = 0;
                }
            }, true);
            ctx.Player.SendChat($"[gold][WeaponSkins][/] Slot {slot + 1} çıkartma konumu sıfırlandı.");
            OpenMenuApiStickerOffsetMenu(ctx.Player, defIndex, slot);
        }, closeOnSelect: false);

        builder.AddItem("⬅️ Slot Yönetimine Dön", ctx => OpenMenuApiStickerSlotDetailMenu(ctx.Player, defIndex, slot), closeOnSelect: false);
        builder.Build().Open(player);
    }

    // =========================================================================
    // SİLAH KAPLAMALARI (WEAPON SKINS) MENÜLERİ
    // =========================================================================
    public void OpenMenuApiWeaponsRootMenu(IPlayer player)
    {
        if (_menuApi == null || !player.IsValid) return;

        var builder = _menuApi.CreateBuilder($"ws_weps_root_{player.SteamID}", "🔫 SİLAH KAPLAMALARI (SKINS)")
            .SetSubtitle($"Hedef Takım: {GetTeamModeDisplayName(player.SteamID)}")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        if (TryGetWeaponInHand(player, out var wepInHand) &&
            Utilities.IsWeaponDefinitionIndex(wepInHand.AttributeManager.Item.ItemDefinitionIndex))
        {
            ushort handIdx = wepInHand.AttributeManager.Item.ItemDefinitionIndex;
            var itemDef = EconService.Items.Values.FirstOrDefault(i => i.Index == handIdx);
            var weaponKey = EconService.Items.FirstOrDefault(kv => kv.Value.Index == handIdx).Key;
            if (itemDef != null && !string.IsNullOrEmpty(weaponKey) &&
                EconService.WeaponToPaintkits.TryGetValue(weaponKey, out var handPaintkits))
            {
                string handName = EconService.GetLocalizedName(itemDef.LocalizedNames, player.PlayerLanguage.Value);
                builder.AddItem($"✋ Elimdeki Silahın Kaplamasını Seç ({handName})", ctx =>
                {
                    OpenMenuApiWeaponPaintkitsMenu(ctx.Player, itemDef, handPaintkits);
                }, closeOnSelect: false);
            }
        }

        foreach (var (weaponKey, paintkits) in EconService.WeaponToPaintkits)
        {
            if (!EconService.Items.TryGetValue(weaponKey, out var item)) continue;
            if (!Utilities.IsWeaponDefinitionIndex(item.Index)) continue;

            var itemCapt = item;
            var pkCapt = paintkits;
            string weaponName = EconService.GetLocalizedName(itemCapt.LocalizedNames, player.PlayerLanguage.Value);
            var currentData = GetOrCreateWeaponSkinData(player, (ushort)itemCapt.Index);
            string currentSkin = GetPaintkitDisplayName(player, currentData.Paintkit);

            builder.AddItem($"🔫 {weaponName} [{currentSkin}]", ctx =>
            {
                OpenMenuApiWeaponPaintkitsMenu(ctx.Player, itemCapt, pkCapt);
            }, closeOnSelect: false);
        }

        builder.AddItem("⬅️ Ana Menüye Dön", ctx => OpenMenuApiMainMenu(ctx.Player), closeOnSelect: false);
        builder.Build().Open(player);
    }

    public void OpenMenuApiWeaponPaintkitsMenu(IPlayer player, ItemDefinition item, List<PaintkitDefinition> paintkits, string? searchFilter = null)
    {
        if (_menuApi == null || !player.IsValid) return;

        ushort defIdx = (ushort)item.Index;
        string weaponName = EconService.GetLocalizedName(item.LocalizedNames, player.PlayerLanguage.Value);
        var currentData = GetOrCreateWeaponSkinData(player, defIdx);

        var sorted = paintkits.OrderByDescending(p => p.Rarity.Id).ToList();
        if (!string.IsNullOrWhiteSpace(searchFilter))
        {
            sorted = sorted.Where(p =>
                EconService.GetLocalizedName(p.LocalizedNames, player.PlayerLanguage.Value)
                    .Contains(searchFilter, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains(searchFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var builder = _menuApi.CreateBuilder($"ws_wep_pk_{player.SteamID}_{defIdx}", $"🎨 {weaponName.ToUpperInvariant()} KAPLAMALARI")
            .SetSubtitle($"Takım: {GetTeamModeDisplayName(player.SteamID)} | Float: {currentData.PaintkitWear:F4}")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        builder.AddItem($"⚙️ {weaponName}: Float / Sticker / Anahtarlık Ayarları", ctx =>
        {
            OpenMenuApiWeaponCustomizeMenu(ctx.Player, defIdx);
        }, closeOnSelect: false);

        builder.AddItem("🔍 İsimle Skin Ara (Sohbetten)", ctx =>
        {
            PromptChatInput(ctx.Player, $"{weaponName} için aramak istediğiniz skin adını yazın:",
                input => !string.IsNullOrWhiteSpace(input),
                (target, input) => OpenMenuApiWeaponPaintkitsMenu(target, item, paintkits, input.Trim()),
                target => OpenMenuApiWeaponPaintkitsMenu(target, item, paintkits, null));
        }, closeOnSelect: false);

        builder.AddItem("🔄 Varsayılan Kaplamaya Sıfırla", ctx =>
        {
            ResetWeaponSkinForSelectedTeams(ctx.Player, defIdx, true);
            ctx.Player.SendChat($"[gold][WeaponSkins][/] {weaponName} kaplaması sıfırlandı ({GetTeamModeDisplayName(ctx.Player.SteamID)}).");
            OpenMenuApiWeaponPaintkitsMenu(ctx.Player, item, paintkits, searchFilter);
        }, closeOnSelect: false);

        builder.AddItem("⬅️ Silah Listesine Dön", ctx => OpenMenuApiWeaponsRootMenu(ctx.Player), closeOnSelect: false);

        foreach (var paintkit in sorted)
        {
            var pk = paintkit;
            string skinName = EconService.GetLocalizedName(pk.LocalizedNames, player.PlayerLanguage.Value);
            string prefix = currentData.Paintkit == pk.Index ? "✓ " : "";

            builder.AddItem($"{prefix}{skinName}", ctx =>
            {
                UpdateWeaponSkinForSelectedTeams(ctx.Player, defIdx, skin =>
                {
                    skin.Paintkit = pk.Index;
                }, true);
                ctx.Player.SendChat($"[gold][WeaponSkins][/] {weaponName}: [lime]{skinName}[/] seçildi ({GetTeamModeDisplayName(ctx.Player.SteamID)})!");
                OpenMenuApiWeaponPaintkitsMenu(ctx.Player, item, paintkits, searchFilter);
            }, closeOnSelect: false);
        }

        builder.Build().Open(player);
    }

    // =========================================================================
    // BIÇAK (KNIVES) VE BIÇAK FLOAT/SEED MENÜLERİ
    // =========================================================================
    public void OpenMenuApiKnivesMenu(IPlayer player)
    {
        if (_menuApi == null || !player.IsValid) return;

        TryGetKnifeDataInHand(player, out var currentKnife);
        string currentKnifeText = currentKnife != null && currentKnife.DefinitionIndex > 0
            ? $"{GetWeaponDisplayName(player, currentKnife.DefinitionIndex)} ({GetPaintkitDisplayName(player, currentKnife.Paintkit)})"
            : "Varsayılan";

        var builder = _menuApi.CreateBuilder($"ws_knives_{player.SteamID}", "🔪 BIÇAK SEÇİMİ & AYARLARI")
            .SetSubtitle($"Seçili: {currentKnifeText} | Takım: {GetTeamModeDisplayName(player.SteamID)}")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        builder.AddItem("⚙️ Bıçak Float / Seed / StatTrak / İsim Etiketi Ayarları", ctx =>
        {
            OpenMenuApiKnifePropertiesMenu(ctx.Player);
        }, closeOnSelect: false);

        builder.AddItem($"👥 Hedef Takımı Değiştir: [{GetTeamModeDisplayName(player.SteamID)}]", ctx =>
        {
            CyclePlayerTeamMode(ctx.Player.SteamID);
            ctx.Player.SendChat($"[gold][WeaponSkins][/] Hedef takım: [lime]{GetTeamModeDisplayName(ctx.Player.SteamID)}[/]");
            OpenMenuApiKnivesMenu(ctx.Player);
        }, closeOnSelect: false);

        builder.AddItem("🔄 Bıçağı Varsayılana Sıfırla", ctx =>
        {
            ResetKnifeSkinForSelectedTeams(ctx.Player, true);
            ctx.Player.SendChat($"[gold][WeaponSkins][/] Bıçak varsayılana sıfırlandı ({GetTeamModeDisplayName(ctx.Player.SteamID)}).");
            OpenMenuApiKnivesMenu(ctx.Player);
        }, closeOnSelect: false);

        foreach (var (knifeKey, paintkits) in EconService.WeaponToPaintkits)
        {
            if (!EconService.Items.TryGetValue(knifeKey, out var item)) continue;
            if (!Utilities.IsKnifeDefinitionIndex(item.Index)) continue;

            var itemCapt = item;
            var pkCapt = paintkits;
            string knifeName = EconService.GetLocalizedName(itemCapt.LocalizedNames, player.PlayerLanguage.Value);
            string prefix = currentKnife != null && currentKnife.DefinitionIndex == itemCapt.Index ? "✓ " : "";

            builder.AddItem($"{prefix}🔪 {knifeName}", ctx =>
            {
                OpenMenuApiKnifePaintkitsMenu(ctx.Player, itemCapt, pkCapt);
            }, closeOnSelect: false);
        }

        builder.AddItem("⬅️ Ana Menüye Dön", ctx => OpenMenuApiMainMenu(ctx.Player), closeOnSelect: false);
        builder.Build().Open(player);
    }

    public void OpenMenuApiKnifePaintkitsMenu(IPlayer player, ItemDefinition item, List<PaintkitDefinition> paintkits)
    {
        if (_menuApi == null || !player.IsValid) return;

        string knifeName = EconService.GetLocalizedName(item.LocalizedNames, player.PlayerLanguage.Value);
        TryGetKnifeDataInHand(player, out var currentKnife);

        var builder = _menuApi.CreateBuilder($"ws_knife_pk_{player.SteamID}_{item.Index}", $"🔪 {knifeName.ToUpperInvariant()}")
            .SetSubtitle($"Takım: {GetTeamModeDisplayName(player.SteamID)}")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        builder.AddItem("⚙️ Bıçak Float / Seed / StatTrak Ayarları", ctx =>
        {
            OpenMenuApiKnifePropertiesMenu(ctx.Player);
        }, closeOnSelect: false);

        builder.AddItem("⬅️ Bıçak Türlerine Dön", ctx => OpenMenuApiKnivesMenu(ctx.Player), closeOnSelect: false);

        // Vanilla (Boyasız) seçeneği
        builder.AddItem($"🔪 {knifeName} - Vanilla (Boyasız)", ctx =>
        {
            UpdateKnifeSkinForSelectedTeams(ctx.Player, knife =>
            {
                knife.DefinitionIndex = (ushort)item.Index;
                knife.Paintkit = 0;
            }, true);
            ctx.Player.SendChat($"[gold][WeaponSkins][/] [lime]{knifeName} (Vanilla)[/] seçildi ({GetTeamModeDisplayName(ctx.Player.SteamID)})!");
            OpenMenuApiKnifePaintkitsMenu(ctx.Player, item, paintkits);
        }, closeOnSelect: false);

        var sorted = paintkits.OrderByDescending(p => p.Rarity.Id).ToList();
        foreach (var paintkit in sorted)
        {
            var pk = paintkit;
            string skinName = EconService.GetLocalizedName(pk.LocalizedNames, player.PlayerLanguage.Value);
            bool isSelected = currentKnife != null && currentKnife.DefinitionIndex == item.Index && currentKnife.Paintkit == pk.Index;

            builder.AddItem($"{(isSelected ? "✓ " : "")}{skinName}", ctx =>
            {
                UpdateKnifeSkinForSelectedTeams(ctx.Player, knife =>
                {
                    knife.DefinitionIndex = (ushort)item.Index;
                    knife.Paintkit = pk.Index;
                }, true);
                ctx.Player.SendChat($"[gold][WeaponSkins][/] [lime]{knifeName} | {skinName}[/] seçildi ({GetTeamModeDisplayName(ctx.Player.SteamID)})!");
                OpenMenuApiKnifePaintkitsMenu(ctx.Player, item, paintkits);
            }, closeOnSelect: false);
        }

        builder.Build().Open(player);
    }

    public void OpenMenuApiKnifePropertiesMenu(IPlayer player)
    {
        if (_menuApi == null || !player.IsValid) return;

        if (!TryGetKnifeDataInHand(player, out var knifeData))
        {
            player.SendChat("[red][WeaponSkins][/] Önce bir bıçak seçmelisiniz!");
            OpenMenuApiKnivesMenu(player);
            return;
        }

        var builder = _menuApi.CreateBuilder($"ws_knife_props_{player.SteamID}", "⚙️ BIÇAK FLOAT & ÖZELLİKLERİ")
            .SetSubtitle($"Float: {knifeData.PaintkitWear:F4} | Seed: {knifeData.PaintkitSeed} | Takım: {GetTeamModeDisplayName(player.SteamID)}")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        (float Val, string Label)[] floatPresets =
        [
            (0.0001f, "✨ Float: 0.0001 (Factory New - Kusursuz)"),
            (0.0701f, "🔹 Float: 0.0701 (Minimal Wear)"),
            (0.1501f, "🔸 Float: 0.1501 (Field-Tested)"),
            (0.4501f, "🔴 Float: 0.4501 (Battle-Scarred)")
        ];

        foreach (var fp in floatPresets)
        {
            float v = fp.Val;
            builder.AddItem(fp.Label, ctx =>
            {
                UpdateKnifeSkinForSelectedTeams(ctx.Player, k => k.PaintkitWear = v, true);
                ctx.Player.SendChat($"[gold][WeaponSkins][/] Bıçak Float değeri [lime]{v:F4}[/] yapıldı!");
                OpenMenuApiKnifePropertiesMenu(ctx.Player);
            }, closeOnSelect: false);
        }

        builder.AddItem($"✏️ Özel Bıçak Float Yaz [Şu an: {knifeData.PaintkitWear:F4}]", ctx =>
        {
            PromptChatInput(ctx.Player, "Yeni bıçak Float değerini yazın (0.00 - 1.00):",
                input => float.TryParse(input.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v is >= 0.0f and <= 1.0f,
                (target, input) =>
                {
                    float val = float.Parse(input.Replace(',', '.'), CultureInfo.InvariantCulture);
                    UpdateKnifeSkinForSelectedTeams(target, k => k.PaintkitWear = val, true);
                    target.SendChat($"[gold][WeaponSkins][/] Bıçak Float değeri [lime]{val:F4}[/] yapıldı!");
                    OpenMenuApiKnifePropertiesMenu(target);
                },
                target => OpenMenuApiKnifePropertiesMenu(target));
        }, closeOnSelect: false);

        builder.AddItem($"🎲 Bıçak Desen No (Seed) Değiştir: [{knifeData.PaintkitSeed}]", ctx =>
        {
            PromptChatInput(ctx.Player, "Yeni bıçak Seed değerini yazın (Örn: 387 Blue Gem):",
                input => int.TryParse(input, out var v) && v >= 0,
                (target, input) =>
                {
                    int val = int.Parse(input);
                    UpdateKnifeSkinForSelectedTeams(target, k => k.PaintkitSeed = val, true);
                    target.SendChat($"[gold][WeaponSkins][/] Bıçak Seed değeri [lime]{val}[/] yapıldı!");
                    OpenMenuApiKnifePropertiesMenu(target);
                },
                target => OpenMenuApiKnifePropertiesMenu(target));
        }, closeOnSelect: false);

        bool isStattrak = knifeData.Quality == EconItemQuality.StatTrak;
        builder.AddItem($"🔥 StatTrak™: [{(isStattrak ? "AÇIK" : "KAPALI")}]", ctx =>
        {
            UpdateKnifeSkinForSelectedTeams(ctx.Player, k => k.Quality = isStattrak ? EconItemQuality.Normal : EconItemQuality.StatTrak, true);
            ctx.Player.SendChat($"[gold][WeaponSkins][/] Bıçak StatTrak™: [lime]{(!isStattrak ? "Açık" : "Kapalı")}[/]");
            OpenMenuApiKnifePropertiesMenu(ctx.Player);
        }, closeOnSelect: false);

        string nametag = string.IsNullOrWhiteSpace(knifeData.Nametag) ? "Yok" : knifeData.Nametag;
        builder.AddItem($"🏷️ Bıçak İsim Etiketi: [{nametag}]", ctx =>
        {
            PromptChatInput(ctx.Player, "Yeni bıçak isim etiketini yazın (Kaldırmak için '-' yazın):",
                input => !string.IsNullOrWhiteSpace(input) && input.Length <= 32,
                (target, input) =>
                {
                    string? tag = input.Trim() == "-" ? null : input.Trim();
                    UpdateKnifeSkinForSelectedTeams(target, k => k.Nametag = tag, true);
                    target.SendChat($"[gold][WeaponSkins][/] Bıçak isim etiketi [lime]{(tag ?? "Kaldırıldı")}[/] yapıldı!");
                    OpenMenuApiKnifePropertiesMenu(target);
                },
                target => OpenMenuApiKnifePropertiesMenu(target));
        }, closeOnSelect: false);

        builder.AddItem("⬅️ Bıçak Menüsüne Dön", ctx => OpenMenuApiKnivesMenu(ctx.Player), closeOnSelect: false);
        builder.Build().Open(player);
    }

    // =========================================================================
    // ELDİVEN (GLOVES) VE ELDİVEN FLOAT/SEED MENÜLERİ
    // =========================================================================
    public void OpenMenuApiGlovesMenu(IPlayer player)
    {
        if (_menuApi == null || !player.IsValid) return;

        TryGetGloveDataInHand(player, out var currentGlove);
        string currentGloveText = currentGlove != null && currentGlove.DefinitionIndex > 0
            ? $"{GetWeaponDisplayName(player, currentGlove.DefinitionIndex)} ({GetPaintkitDisplayName(player, currentGlove.Paintkit)})"
            : "Varsayılan";

        var builder = _menuApi.CreateBuilder($"ws_gloves_{player.SteamID}", "🧤 ELDİVEN SEÇİMİ & AYARLARI")
            .SetSubtitle($"Seçili: {currentGloveText} | Takım: {GetTeamModeDisplayName(player.SteamID)}")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        builder.AddItem("⚙️ Eldiven Float (Aşınma) & Seed Ayarları", ctx =>
        {
            OpenMenuApiGlovePropertiesMenu(ctx.Player);
        }, closeOnSelect: false);

        builder.AddItem($"👥 Hedef Takımı Değiştir: [{GetTeamModeDisplayName(player.SteamID)}]", ctx =>
        {
            CyclePlayerTeamMode(ctx.Player.SteamID);
            ctx.Player.SendChat($"[gold][WeaponSkins][/] Hedef takım: [lime]{GetTeamModeDisplayName(ctx.Player.SteamID)}[/]");
            OpenMenuApiGlovesMenu(ctx.Player);
        }, closeOnSelect: false);

        builder.AddItem("🔄 Eldiveni Varsayılana Sıfırla", ctx =>
        {
            ResetGloveSkinForSelectedTeams(ctx.Player, true);
            ctx.Player.SendChat($"[gold][WeaponSkins][/] Eldiven varsayılana sıfırlandı ({GetTeamModeDisplayName(ctx.Player.SteamID)}).");
            OpenMenuApiGlovesMenu(ctx.Player);
        }, closeOnSelect: false);

        foreach (var (gloveKey, paintkits) in EconService.WeaponToPaintkits)
        {
            if (!EconService.Items.TryGetValue(gloveKey, out var item)) continue;
            if (!Utilities.IsGloveDefinitionIndex(item.Index)) continue;

            var itemCapt = item;
            var pkCapt = paintkits;
            string gloveName = EconService.GetLocalizedName(itemCapt.LocalizedNames, player.PlayerLanguage.Value);
            string prefix = currentGlove != null && currentGlove.DefinitionIndex == itemCapt.Index ? "✓ " : "";

            builder.AddItem($"{prefix}🧤 {gloveName}", ctx =>
            {
                OpenMenuApiGlovePaintkitsMenu(ctx.Player, itemCapt, pkCapt);
            }, closeOnSelect: false);
        }

        builder.AddItem("⬅️ Ana Menüye Dön", ctx => OpenMenuApiMainMenu(ctx.Player), closeOnSelect: false);
        builder.Build().Open(player);
    }

    public void OpenMenuApiGlovePaintkitsMenu(IPlayer player, ItemDefinition item, List<PaintkitDefinition> paintkits)
    {
        if (_menuApi == null || !player.IsValid) return;

        string gloveName = EconService.GetLocalizedName(item.LocalizedNames, player.PlayerLanguage.Value);
        TryGetGloveDataInHand(player, out var currentGlove);

        var builder = _menuApi.CreateBuilder($"ws_glove_pk_{player.SteamID}_{item.Index}", $"🧤 {gloveName.ToUpperInvariant()}")
            .SetSubtitle($"Takım: {GetTeamModeDisplayName(player.SteamID)}")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        builder.AddItem("⚙️ Eldiven Float & Seed Ayarları", ctx =>
        {
            OpenMenuApiGlovePropertiesMenu(ctx.Player);
        }, closeOnSelect: false);

        builder.AddItem("⬅️ Eldiven Türlerine Dön", ctx => OpenMenuApiGlovesMenu(ctx.Player), closeOnSelect: false);

        var sorted = paintkits.OrderByDescending(p => p.Rarity.Id).ToList();
        foreach (var paintkit in sorted)
        {
            var pk = paintkit;
            string skinName = EconService.GetLocalizedName(pk.LocalizedNames, player.PlayerLanguage.Value);
            bool isSelected = currentGlove != null && currentGlove.DefinitionIndex == item.Index && currentGlove.Paintkit == pk.Index;

            builder.AddItem($"{(isSelected ? "✓ " : "")}{skinName}", ctx =>
            {
                UpdateGloveSkinForSelectedTeams(ctx.Player, glove =>
                {
                    glove.DefinitionIndex = (ushort)item.Index;
                    glove.Paintkit = pk.Index;
                }, true);
                ctx.Player.SendChat($"[gold][WeaponSkins][/] [lime]{gloveName} | {skinName}[/] seçildi ({GetTeamModeDisplayName(ctx.Player.SteamID)})!");
                OpenMenuApiGlovePaintkitsMenu(ctx.Player, item, paintkits);
            }, closeOnSelect: false);
        }

        builder.Build().Open(player);
    }

    public void OpenMenuApiGlovePropertiesMenu(IPlayer player)
    {
        if (_menuApi == null || !player.IsValid) return;

        if (!TryGetGloveDataInHand(player, out var gloveData))
        {
            player.SendChat("[red][WeaponSkins][/] Önce bir eldiven seçmelisiniz!");
            OpenMenuApiGlovesMenu(player);
            return;
        }

        var builder = _menuApi.CreateBuilder($"ws_glove_props_{player.SteamID}", "⚙️ ELDİVEN FLOAT & SEED AYARLARI")
            .SetSubtitle($"Float: {gloveData.PaintkitWear:F4} | Seed: {gloveData.PaintkitSeed} | Takım: {GetTeamModeDisplayName(player.SteamID)}")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        (float Val, string Label)[] floatPresets =
        [
            (0.0001f, "✨ Float: 0.0001 (Factory New - Kusursuz)"),
            (0.0701f, "🔹 Float: 0.0701 (Minimal Wear)"),
            (0.1501f, "🔸 Float: 0.1501 (Field-Tested)"),
            (0.4501f, "🔴 Float: 0.4501 (Battle-Scarred)")
        ];

        foreach (var fp in floatPresets)
        {
            float v = fp.Val;
            builder.AddItem(fp.Label, ctx =>
            {
                UpdateGloveSkinForSelectedTeams(ctx.Player, g => g.PaintkitWear = v, true);
                ctx.Player.SendChat($"[gold][WeaponSkins][/] Eldiven Float değeri [lime]{v:F4}[/] yapıldı!");
                OpenMenuApiGlovePropertiesMenu(ctx.Player);
            }, closeOnSelect: false);
        }

        builder.AddItem($"✏️ Özel Eldiven Float Yaz [Şu an: {gloveData.PaintkitWear:F4}]", ctx =>
        {
            PromptChatInput(ctx.Player, "Yeni eldiven Float değerini yazın (0.00 - 1.00):",
                input => float.TryParse(input.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v is >= 0.0f and <= 1.0f,
                (target, input) =>
                {
                    float val = float.Parse(input.Replace(',', '.'), CultureInfo.InvariantCulture);
                    UpdateGloveSkinForSelectedTeams(target, g => g.PaintkitWear = val, true);
                    target.SendChat($"[gold][WeaponSkins][/] Eldiven Float değeri [lime]{val:F4}[/] yapıldı!");
                    OpenMenuApiGlovePropertiesMenu(target);
                },
                target => OpenMenuApiGlovePropertiesMenu(target));
        }, closeOnSelect: false);

        builder.AddItem($"🎲 Eldiven Desen No (Seed) Değiştir: [{gloveData.PaintkitSeed}]", ctx =>
        {
            PromptChatInput(ctx.Player, "Yeni eldiven Seed değerini yazın:",
                input => int.TryParse(input, out var v) && v >= 0,
                (target, input) =>
                {
                    int val = int.Parse(input);
                    UpdateGloveSkinForSelectedTeams(target, g => g.PaintkitSeed = val, true);
                    target.SendChat($"[gold][WeaponSkins][/] Eldiven Seed değeri [lime]{val}[/] yapıldı!");
                    OpenMenuApiGlovePropertiesMenu(target);
                },
                target => OpenMenuApiGlovePropertiesMenu(target));
        }, closeOnSelect: false);

        builder.AddItem("⬅️ Eldiven Menüsüne Dön", ctx => OpenMenuApiGlovesMenu(ctx.Player), closeOnSelect: false);
        builder.Build().Open(player);
    }

    // =========================================================================
    // AJANLAR (T VE CT TAKIMI SEÇİMLİ) VE MÜZİK KİTLERİ
    // =========================================================================
    public void OpenMenuApiAgentsMenu(IPlayer player)
    {
        if (_menuApi == null || !player.IsValid) return;

        var builder = _menuApi.CreateBuilder($"ws_agents_root_{player.SteamID}", "🕵️ AJAN SEÇİMİ (T & CT)")
            .SetSubtitle("Hangi takım için ajan seçmek istiyorsunuz?")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        builder.AddItem("🔥 Terörist (T) Ajanları", ctx => OpenMenuApiAgentsForTeamMenu(ctx.Player, Team.T), closeOnSelect: false);
        builder.AddItem("🛡️ Anti-Terörist (CT) Ajanları", ctx => OpenMenuApiAgentsForTeamMenu(ctx.Player, Team.CT), closeOnSelect: false);
        builder.AddItem("⬅️ Ana Menüye Dön", ctx => OpenMenuApiMainMenu(ctx.Player), closeOnSelect: false);

        builder.Build().Open(player);
    }

    public void OpenMenuApiAgentsForTeamMenu(IPlayer player, Team targetTeam)
    {
        if (_menuApi == null || !player.IsValid) return;

        string teamLabel = targetTeam == Team.CT ? "Anti-Terörist (CT)" : "Terörist (T)";
        string teamPrefix = targetTeam == Team.CT ? "ctm_" : "tm_";
        DataService.AgentDataService.TryGetAgent(player.SteamID, targetTeam, out int currentAgentIdx);

        var agents = EconService.Agents.Values
            .Where(a => a.ModelPath.Contains($"/{teamPrefix}", StringComparison.OrdinalIgnoreCase) ||
                        a.ModelPath.StartsWith(teamPrefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a => a.Rarity.Id)
            .ThenBy(a => EconService.GetLocalizedName(a.LocalizedNames, player.PlayerLanguage.Value))
            .ToList();

        var builder = _menuApi.CreateBuilder($"ws_agents_team_{player.SteamID}_{(int)targetTeam}", $"🕵️ {teamLabel.ToUpperInvariant()} AJANLARI")
            .SetSubtitle($"Toplam {agents.Count} Ajan")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        builder.AddItem($"🔄 {teamLabel} Ajanını Varsayılana Sıfırla", ctx =>
        {
            var p = ctx.Player;
            DataService.AgentDataService.TryRemoveAgent(p.SteamID, targetTeam);
            if (p.Controller.Team == targetTeam && DataService.AgentDataService.TryGetDefaultModel(p.SteamID, targetTeam, out var defaultModel))
            {
                ApplyAgentModel(p, defaultModel);
            }
            Api.ResetAgentSkin(p.SteamID, targetTeam, true);
            p.SendChat($"[gold][WeaponSkins][/] {teamLabel} ajanı sıfırlandı.");
            OpenMenuApiAgentsForTeamMenu(p, targetTeam);
        }, closeOnSelect: false);

        builder.AddItem("⬅️ Ajan Takım Seçimine Dön", ctx => OpenMenuApiAgentsMenu(ctx.Player), closeOnSelect: false);

        foreach (var agent in agents)
        {
            var ag = agent;
            string agentName = EconService.GetLocalizedName(ag.LocalizedNames, player.PlayerLanguage.Value);
            string prefix = currentAgentIdx == ag.Index ? "✓ " : "";

            builder.AddItem($"{prefix}{agentName}", ctx =>
            {
                var p = ctx.Player;
                if (p.IsAlive() && p.Controller.Team == targetTeam)
                {
                    var pawn = p.PlayerPawn!;
                    var current = pawn.CBodyComponent!.SceneNode!.GetSkeletonInstance().ModelState.ModelName;
                    DataService.AgentDataService.CaptureDefaultModel(p.SteamID, targetTeam, current);
                    ApplyAgentModel(p, ag.ModelPath);
                    Core.Scheduler.DelayBySeconds(0.1f, () => ApplyAgentModel(p, ag.ModelPath));
                }

                DataService.AgentDataService.SetAgent(p.SteamID, targetTeam, ag.Index);
                Api.UpdateAgentSkin(p.SteamID, targetTeam, ag.Index, true);
                p.SendChat($"[gold][WeaponSkins][/] {teamLabel} ajanı [lime]{agentName}[/] olarak seçildi!");
                OpenMenuApiAgentsForTeamMenu(p, targetTeam);
            }, closeOnSelect: false);
        }

        builder.Build().Open(player);
    }

    public void OpenMenuApiMusicKitsMenu(IPlayer player)
    {
        if (_menuApi == null || !player.IsValid) return;

        var builder = _menuApi.CreateBuilder($"ws_musickits_{player.SteamID}", "🎵 MÜZİK KİTLERİ (MUSIC KITS)")
            .SetSubtitle("Müzik Kiti Seçiniz")
            .SetTheme(MenuTheme.Blue)
            .SetItemsPerPage(7)
            .EnableSounds(true)
            .EnableSlotSelection(true)
            .EnableChatSelection(true);

        builder.AddItem("🔄 Müzik Kitini Varsayılana Sıfırla", ctx =>
        {
            Api.ResetMusicKit(ctx.Player.SteamID);
            ctx.Player.SendChat("[gold][WeaponSkins][/] Müzik Kiti varsayılana sıfırlandı.");
            OpenMenuApiMusicKitsMenu(ctx.Player);
        }, closeOnSelect: false);

        builder.AddItem("⬅️ Ana Menüye Dön", ctx => OpenMenuApiMainMenu(ctx.Player), closeOnSelect: false);

        foreach (var musicKit in EconService.MusicKits.Values.OrderBy(mk => mk.Index))
        {
            var mk = musicKit;
            string mkName = EconService.GetLocalizedName(mk.LocalizedNames, player.PlayerLanguage.Value);
            builder.AddItem($"🎵 {mkName}", ctx =>
            {
                Api.SetMusicKit(ctx.Player.SteamID, mk.Index);
                ctx.Player.SendChat($"[gold][WeaponSkins][/] Müzik Kiti [lime]{mkName}[/] olarak seçildi!");
                OpenMenuApiMusicKitsMenu(ctx.Player);
            }, closeOnSelect: false);
        }

        builder.Build().Open(player);
    }

    // =========================================================================
    // YARDIMCI METOTLAR
    // =========================================================================
    private string GetWeaponDisplayName(IPlayer player, ushort defIndex)
    {
        var item = EconService.Items.Values.FirstOrDefault(i => i.Index == defIndex);
        if (item == null) return $"Silah #{defIndex}";
        return EconService.GetLocalizedName(item.LocalizedNames, player.PlayerLanguage.Value);
    }

    private string GetPaintkitDisplayName(IPlayer player, int paintkitIndex)
    {
        if (paintkitIndex <= 0) return "Varsayılan";
        foreach (var (_, pks) in EconService.WeaponToPaintkits)
        {
            var match = pks.FirstOrDefault(p => p.Index == paintkitIndex);
            if (match != null)
            {
                return EconService.GetLocalizedName(match.LocalizedNames, player.PlayerLanguage.Value);
            }
        }
        return $"Skin #{paintkitIndex}";
    }

    private static string GetWearCategoryName(float wear)
    {
        return wear switch
        {
            < 0.07f => "Factory New",
            < 0.15f => "Minimal Wear",
            < 0.38f => "Field-Tested",
            < 0.45f => "Well-Worn",
            _ => "Battle-Scarred"
        };
    }
}

