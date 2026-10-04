namespace PcMonitor.Core.GameMode;

/// <summary>The starting list written to gamemode.json the first time. Edit the JSON to change it.</summary>
public static class GameModeDefaults
{
    public static GameModeConfig Create() => new()
    {
        Items =
        {
            new GameModeItem
            {
                Id = "grammarly", DisplayName = "Grammarly (Superhuman Go)",
                ProcessNames = ["Grammarly.Desktop"],
            },
            new GameModeItem
            {
                Id = "claude", DisplayName = "Claude desktop",
                ProcessNames = ["claude"], PathContains = @"\WindowsApps\Claude_",
                AppId = "Claude_pzs8sxrjxfjjc!Claude",
            },
            new GameModeItem
            {
                Id = "chatgpt", DisplayName = "ChatGPT / Codex",
                ProcessNames = ["ChatGPT"], AppId = "OpenAI.Codex_2p2nqsd0c76g0!App",
            },
            new GameModeItem
            {
                Id = "granola", DisplayName = "Granola",
                ProcessNames = ["Granola"], AppId = "com.granola.app",
            },
            new GameModeItem
            {
                Id = "google-drive", DisplayName = "Google Drive",
                ProcessNames = ["GoogleDriveFS"],
            },
            new GameModeItem
            {
                Id = "onedrive", DisplayName = "OneDrive",
                ProcessNames = ["OneDrive"], CloseArgs = "/shutdown",
            },
            // Optional: shown in their own section, unchecked, because they're often used while gaming.
            new GameModeItem
            {
                Id = "chrome", DisplayName = "Chrome",
                ProcessNames = ["chrome"], AppId = "Chrome", SelectedByDefault = false,
                Note = "reopens with your normal startup tabs",
            },
            new GameModeItem
            {
                Id = "discord", DisplayName = "Discord",
                ProcessNames = ["Discord"], AppId = "com.squirrel.Discord.Discord", SelectedByDefault = false,
            },
            new GameModeItem
            {
                Id = "spotify", DisplayName = "Spotify",
                ProcessNames = ["Spotify"], SelectedByDefault = false,
            },
            new GameModeItem
            {
                Id = "m365-copilot", DisplayName = "Microsoft 365 Copilot",
                ProcessNames = ["M365Copilot"], Relaunch = false,
                Note = "not brought back",
            },
            new GameModeItem
            {
                Id = "geocomply", DisplayName = "Player Location Check (GeoComply)",
                Kind = GameModeItemKind.Service, ServiceName = "Player Location Check",
                MemoryProcessNames = ["PlayerLocationCheckService", "PlayerLocationCheck"],
                Relaunch = false, Note = "starts itself when a betting app needs it",
            },
            new GameModeItem
            {
                Id = "wsl", DisplayName = "WSL (Ubuntu)",
                Kind = GameModeItemKind.Command, DetectProcessNames = ["vmmemWSL"],
                StopCommand = new CommandSpec("wsl.exe", "--shutdown"),
                StartCommand = new CommandSpec("schtasks.exe", "/run /tn \"WSL AutoStart\""),
                Note = "ends every Claude Code / WSL session",
            },
            new GameModeItem
            {
                Id = "cowork-vm", DisplayName = "Claude Cowork VM",
                Kind = GameModeItemKind.Service, ServiceName = "CoworkVMService",
                MemoryProcessNames = ["vmmem"],
            },
        },
    };
}
