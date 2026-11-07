using PanoramaHud.Contract;

using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

using WeaponSkins.Econ;
using WeaponSkins.Extensions;
using WeaponSkins.Shared;

namespace WeaponSkins;

public partial class MenuService
{
    public void OpenAgentMenu(IPlayer player)
    {
        if (_hud == null) return;
        var builder = _hud.Menu($"ws.agents.{player.SteamID}", LocalizationService[player].MenuTitleAgents);
        BuildAgentSubmenu(builder, player);
        builder.Build().Open(player);
    }

    public void BuildAgentSubmenu(IMenuBuilder builder, IPlayer player)
    {
        builder.Description("Ajan Seçiniz");

        // 1. Reset option
        builder.Button(LocalizationService[player].MenuReset, ctx =>
        {
            var p = ctx.Player;
            var currentTeam = p.Controller.Team;
            DataService.AgentDataService.TryRemoveAgent(p.SteamID, currentTeam);
            if (DataService.AgentDataService.TryGetDefaultModel(p.SteamID, currentTeam, out var defaultModel))
            {
                ApplyAgentModel(p, defaultModel);
            }

            Api.ResetAgentSkin(p.SteamID, currentTeam, true);
            _hud?.Toast(p, new ToastOptions
            {
                Title = "WeaponSkins",
                Text = "Ajan varsayılana sıfırlandı.",
                Kind = ToastKind.Warning,
                Key = "ws.agent"
            });
            p.SendChat("[gold][WeaponSkins][/] Ajan varsayılana sıfırlandı.");
        });

        var team = player.Controller.Team;
        var teamPrefix = (int)team == 3 ? "ctm_" : "tm_";
        var agents = EconService.Agents.Values
            .Where(a => a.ModelPath.Contains($"/{teamPrefix}", StringComparison.OrdinalIgnoreCase) ||
                        a.ModelPath.StartsWith(teamPrefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a => a.Rarity.Id)
            .ThenBy(a => EconService.GetLocalizedName(a.LocalizedNames, player.PlayerLanguage.Value))
            .ToList();

        foreach (var agent in agents)
        {
            var agentName = EconService.GetLocalizedName(agent.LocalizedNames, player.PlayerLanguage.Value);

            builder.Button(agentName, ctx =>
            {
                var p = ctx.Player;
                var currentTeam = p.Controller.Team;

                if (p.IsAlive())
                {
                    var pawn = p.PlayerPawn!;
                    var current = pawn.CBodyComponent!.SceneNode!.GetSkeletonInstance().ModelState.ModelName;
                    DataService.AgentDataService.CaptureDefaultModel(p.SteamID, currentTeam, current);
                }

                DataService.AgentDataService.SetAgent(p.SteamID, currentTeam, agent.Index);
                ApplyAgentModel(p, agent.ModelPath);

                Api.UpdateAgentSkin(p.SteamID, currentTeam, agent.Index, true);

                Core.Scheduler.DelayBySeconds(0.1f, () =>
                {
                    ApplyAgentModel(p, agent.ModelPath);
                });

                _hud?.Toast(p, new ToastOptions
                {
                    Title = "WeaponSkins",
                    Text = $"{agentName} seçildi!",
                    Kind = ToastKind.Success,
                    Key = "ws.agent"
                });
                p.SendChat($"[gold][WeaponSkins][/] [lime]{agentName}[/] seçildi!");
            });
        }
    }

    private string? GetRefreshModel(string currentModel, string targetModel)
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

    private void ApplyAgentModel(IPlayer player, string model)
    {
        if (!player.IsAlive()) return;

        var pawn = player.PlayerPawn!;
        var current = pawn.CBodyComponent!.SceneNode!.GetSkeletonInstance().ModelState.ModelName;
        var refreshModel = GetRefreshModel(current, model);
        if (!string.IsNullOrWhiteSpace(refreshModel))
        {
            pawn.SetModel(refreshModel);
            pawn.SetModel(current);
        }

        Core.Scheduler.NextTick(() =>
        {
            if (!player.IsAlive()) return;
            pawn.SetModel(model);
        });
    }
}
