using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using RSBot.Core;
using RSBot.Core.Components;
using RSBot.General.Models;

namespace RSBot.General.Components;

internal class Accounts
{
    /// <summary>
    ///     Gets or sets the saved accounts.
    /// </summary>
    /// <value>
    ///     The saved accounts.
    /// </value>
    public static List<Account> SavedAccounts { get; set; }

    /// <summary>
    ///     Gets or sets the joined account.
    /// </summary>
    public static Account Joined { get; set; }

    /// <summary>
    ///     Get the data file path
    /// </summary>
    private static string _filePath =>
        Path.Combine(Kernel.BasePath, "User", ProfileManager.SelectedProfile, "autologin.data");

    /// <summary>
    ///     Check the saving directory
    /// </summary>
    /// <returns></returns>
    private static void EnsureDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_filePath);

        Directory.CreateDirectory(directory);
    }

    /// <summary>
    ///     Loads this instance.
    /// </summary>
    public static void Load()
    {
        try
        {
            EnsureDirectoryExists();

            SavedAccounts = new List<Account>();

            if (!File.Exists(_filePath))
                return;

            // Читаем файл как обычный текст (без Blowfish)
            var json = File.ReadAllText(_filePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
                return;

            SavedAccounts = JsonSerializer.Deserialize<List<Account>>(json) ?? new List<Account>(4);
        }
        catch (Exception ex)
        {
            Log.NotifyLang("FileNotFound", _filePath);
            Log.Fatal(ex);
        }
    }

    /// <summary>
    ///     Saves this instance.
    /// </summary>
    public static void Save()
    {
        EnsureDirectoryExists();

        if (SavedAccounts == null)
            return;

        try
        {
            // Сохраняем как обычный JSON (без Blowfish)
            var json = JsonSerializer.Serialize(SavedAccounts);
            File.WriteAllText(_filePath, json, Encoding.UTF8);
        }
        catch
        {
            Log.NotifyLang("FileNotFound", _filePath);
        }
    }
}
