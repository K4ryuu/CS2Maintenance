using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Entities;

namespace K4ryuuMaintenance
{
	public sealed class PluginConfig : BasePluginConfig
	{
		[JsonPropertyName("steamid-whitelist")]
		public List<string> Whitelist { get; set; } = new List<string>
		{
			"01234",
			"56789"
		};

		[JsonPropertyName("connect-permissions")]
		public List<string> CanJoinPermissions { get; set; } = new List<string>
		{
			"@myplugin/can-join-permission",
			"#myplugin/can-join-group",
			"can-join-override"
		};
	}

	[MinimumApiVersion(153)]
	public sealed partial class MaintenancePlugin : BasePlugin, IPluginConfig<PluginConfig>
	{

		public override string ModuleName => "CS2 Maintenance";
		public override string ModuleVersion => "1.0.0";
		public override string ModuleAuthor => "K4ryuu";

		public required PluginConfig Config { get; set; } = new PluginConfig();

		private bool MaintenanceEnabled = false;

		public void OnConfigParsed(PluginConfig config)
		{
			this.Config = config;
		}

		[ConsoleCommand("css_maintenance", "Enable/Disable maintenance mode for the server")]
		[ConsoleCommand("css_maint", "Enable/Disable maintenance mode for the server")]
		[ConsoleCommand("css_devmode", "Enable/Disable maintenance mode for the server")]
		[CommandHelper(minArgs: 1, usage: "[0|1]", whoCanExecute: CommandUsage.CLIENT_ONLY)]
		[RequiresPermissions("@maintenance/command")]
		public void OnCommandSetWebhook(CCSPlayerController? player, CommandInfo command)
		{
			if (player == null || !player.IsValid || player.PlayerPawn.Value == null)
				return;

			if (!bool.TryParse(command.ArgByIndex(1), out bool parsedBool))
			{
				command.ReplyToCommand($" {ChatColors.Yellow}[MAINTENANCE] {ChatColors.LightRed}Invalid argument. Usage: !maintenance [0|1]");
				return;
			}

			List<CCSPlayerController> players = Utilities.GetPlayers();

			foreach (CCSPlayerController target in players)
			{
				if (target is null || !target.IsValid || !target.PlayerPawn.IsValid || target.IsBot || target.IsHLTV)
					continue;

				if (!PlayerCanBeOnline(player))
				{
					Server.ExecuteCommand($"kickid {player.UserId} \"Currently in maintenance mode. Please try again later.\"");
				}
			}

			MaintenanceEnabled = parsedBool;
			Server.PrintToChatAll($" {ChatColors.Yellow}[MAINTENANCE] {ChatColors.LightRed}Maintenance mode has been {(MaintenanceEnabled ? "ENABLED" : "DISABLED")}.");
		}

		[GameEventHandler]
		public HookResult OnClientConnect(EventPlayerConnectFull @event, GameEventInfo info)
		{
			if (!MaintenanceEnabled)
				return HookResult.Continue;

			CCSPlayerController player = @event.Userid;

			if (player is null || !player.IsValid || player.IsBot || player.IsHLTV)
				return HookResult.Continue;

			if (!PlayerCanBeOnline(player))
			{
				Server.ExecuteCommand($"kickid {player.UserId} \"Currently in maintenance mode. Please try again later.\"");
			}

			return HookResult.Continue;
		}

		public bool PlayerCanBeOnline(CCSPlayerController player)
		{
			bool canJoin = false;

			foreach (string checkPermission in Config.CanJoinPermissions)
			{
				switch (checkPermission[0])
				{
					case '@':
						if (AdminManager.PlayerHasPermissions(player, checkPermission))
							canJoin = true;
						break;
					case '#':
						if (AdminManager.PlayerInGroup(player, checkPermission))
							canJoin = true;
						break;
					default:
						if (AdminManager.PlayerHasCommandOverride(player, checkPermission))
							canJoin = true;
						break;
				}
			}

			if (!canJoin)
			{
				SteamID checkSteamID = new SteamID(player.SteamID);
				canJoin = Config.Whitelist.Contains(checkSteamID.SteamId64.ToString()) || Config.Whitelist.Contains(checkSteamID.SteamId3.ToString()) || Config.Whitelist.Contains(checkSteamID.SteamId2.ToString()) || Config.Whitelist.Contains(checkSteamID.SteamId32.ToString());
			}

			return canJoin;
		}
	}
}
