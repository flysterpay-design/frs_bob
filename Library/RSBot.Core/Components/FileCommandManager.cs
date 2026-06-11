using System;
using System.IO;
using System.Threading.Tasks;
using RSBot.Core;
using RSBot.Core.Event;

namespace RSBot.Core.Components
{
    public static class FileCommandManager
    {
        private static string _commandDir;
        private static string _statusDir;
        private static string _profileName;
        private static bool _isRunning;

        public static void Start(string profileName)
        {
            if (_isRunning) return;

            _profileName = SanitizeFileName(profileName);
            string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            _commandDir = Path.Combine(baseDir, "BotCommands");
            _statusDir = Path.Combine(baseDir, "BotStatus");

            Directory.CreateDirectory(_commandDir);
            Directory.CreateDirectory(_statusDir);

            _isRunning = true;
            Task.Run(CommandWatcherLoop);
            Log.Debug($"[FileCommand] Started for profile {profileName} (commands: {_commandDir})");
        }

        private static async Task CommandWatcherLoop()
        {
            string commandFile = Path.Combine(_commandDir, $"{_profileName}.cmd");
            while (_isRunning)
            {
                try
                {
                    if (File.Exists(commandFile))
                    {
                        string command = File.ReadAllText(commandFile).Trim();
                        File.Delete(commandFile);
                        Log.Debug($"[FileCommand] Received command: {command}");

                        // Приводим к нижнему регистру только для сравнения команд без параметров
                        string cmdLower = command.ToLowerInvariant();

                        if (cmdLower == "start")
                        {
                            if (!Kernel.Bot.Running)
                            {
                                Kernel.Bot.Start();
                                UpdateStatusFile(true);
                            }
                        }
                        else if (cmdLower == "stop")
                        {
                            if (Kernel.Bot.Running)
                            {
                                Kernel.Bot.Stop();
                                UpdateStatusFile(false);
                            }
                        }
                        else if (cmdLower == "recall")
                        {
                            bool success = Game.Player.UseReturnScroll();
                            Log.Status($"[FileCommand] Recall: {(success ? "used" : "failed (no scroll or can't use)")}");
                        }
                        else if (cmdLower == "save")
                        {
                            PlayerConfig.Save();
                            Log.Status("[FileCommand] Settings saved");
                        }
                        else if (cmdLower.StartsWith("setarea"))
                        {
                            // Формат: setarea X Y Region
                            var parts = command.Split(' ');
                            if (parts.Length >= 4)
                            {
                                if (float.TryParse(parts[1], out float x) &&
                                    float.TryParse(parts[2], out float y) &&
                                    ushort.TryParse(parts[3], out ushort region))
                                {
                                    PlayerConfig.Set("RSBot.Area.X", x);
                                    PlayerConfig.Set("RSBot.Area.Y", y);
                                    PlayerConfig.Set("RSBot.Area.Z", 0f);
                                    PlayerConfig.Set("RSBot.Area.Region", region);
                                    EventManager.FireEvent("OnSetTrainingArea");
                                    Log.Status($"[FileCommand] Training area set to X={x}, Y={y}, Region={region}");
                                }
                                else
                                {
                                    Log.Warn("[FileCommand] Invalid setarea format. Use: setarea X Y Region");
                                }
                            }
                            else
                            {
                                Log.Warn("[FileCommand] setarea requires X Y Region");
                            }
                        }
                        else if (cmdLower.StartsWith("setscript"))
                        {
                            // Формат: setscript "полный путь к файлу" (путь может быть в кавычках или без)
                            string scriptPath = command.Substring("setscript".Length).Trim();
                            // Удаляем возможные кавычки
                            if (scriptPath.StartsWith("\"") && scriptPath.EndsWith("\""))
                                scriptPath = scriptPath.Substring(1, scriptPath.Length - 2);
                            if (File.Exists(scriptPath))
                            {
                                PlayerConfig.Set("RSBot.Walkback.File", scriptPath);
                                PlayerConfig.Save();
                                Log.Status($"[FileCommand] Walkback script set to {scriptPath}");
                            }
                            else
                            {
                                Log.Warn($"[FileCommand] Script file not found: {scriptPath}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[FileCommand] Error: {ex.Message}");
                }
                await Task.Delay(1000);
            }
        }

        public static void UpdateStatusFile(bool running)
        {
            if (string.IsNullOrEmpty(_statusDir) || string.IsNullOrEmpty(_profileName)) return;
            string statusFile = Path.Combine(_statusDir, $"{_profileName}.txt");
            try
            {
                File.WriteAllText(statusFile, running ? "Running" : "Stopped");
            }
            catch (Exception ex)
            {
                Log.Error($"[FileCommand] Failed to write status: {ex.Message}");
            }
        }

        public static void Stop()
        {
            _isRunning = false;
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            // также заменим пробелы и другие опасные символы
            foreach (char c in new[] { ' ', '.', '/', '\\', ':', '*', '?', '"', '<', '>', '|' })
                name = name.Replace(c, '_');
            return name;
        }
    }
}
