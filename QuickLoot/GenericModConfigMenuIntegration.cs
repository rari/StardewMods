#nullable enable
using System;
using System.Reflection;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using GenericModConfigMenu;

namespace QuickLootMod {
    /// <summary>Registers the mod configuration with Generic Mod Config Menu.</summary>
    internal class GenericModConfigMenuIntegration
    {
        /*********
        ** Fields
        *********/
        /// <summary>The mod manifest.</summary>
        private readonly IManifest Manifest;

        /// <summary>The Generic Mod Config Menu integration.</summary>
        private readonly IGenericModConfigMenuApi? ConfigMenu;

        /// <summary>The current mod settings.</summary>
        private readonly ModConfig Config;

        /// <summary>Save the mod's current config to the <c>config.json</c> file.</summary>
        private readonly Action Save;

        /// <summary>The mod helper for translations.</summary>
        private readonly IModHelper Helper;


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="manifest">The mod manifest.</param>
        /// <param name="modRegistry">An API for fetching metadata about loaded mods.</param>
        /// <param name="config">The current mod config.</param>
        /// <param name="save">Save the mod's current config to the <c>config.json</c> file.</param>
        /// <param name="helper">The mod helper for translations.</param>
        /// <param name="monitor">Optional monitor for logging.</param>
        public GenericModConfigMenuIntegration(IManifest manifest, IModRegistry modRegistry, ModConfig config, Action save, IModHelper helper, IMonitor? monitor = null)
        {
            this.Manifest = manifest;
            this.ConfigMenu = modRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            this.Config = config;
            this.Save = save;
            this.Helper = helper;
        }

        /// <summary>Register the config menu if available.</summary>
        public void Register()
        {
            var menu = this.ConfigMenu;
            if (menu is null)
                return;

            menu.Register(this.Manifest, this.Reset, this.Save);

            // Options section
            menu.AddSectionTitle(this.Manifest, () => this.Helper.Translation.Get("ModConfigMenu.Label.Options"));

            menu.AddBoolOption(
                mod: this.Manifest,
                getValue: () => this.Config.CloseMenuAfterLoot,
                setValue: value => this.Config.CloseMenuAfterLoot = value,
                name: () => this.Helper.Translation.Get("ModConfigMenu.CloseMenuAfterLoot.Name"),
                tooltip: () => this.Helper.Translation.Get("ModConfigMenu.CloseMenuAfterLoot.Desc")
            );

            // Controls section
            menu.AddSectionTitle(this.Manifest, () => this.Helper.Translation.Get("ModConfigMenu.Label.Controls"));

            menu.AddKeybindList(
                mod: this.Manifest,
                getValue: () => this.Config.LootHotkey,
                setValue: value => this.Config.LootHotkey = value,
                name: () => this.Helper.Translation.Get("ModConfigMenu.LootHotkey.Name"),
                tooltip: () => this.Helper.Translation.Get("ModConfigMenu.LootHotkey.Desc")
            );

            menu.AddBoolOption(
                mod: this.Manifest,
                getValue: () => this.Config.UseQuickStowHotkey,
                setValue: value => this.Config.UseQuickStowHotkey = value,
                name: () => this.Helper.Translation.Get("ModConfigMenu.UseQuickStowHotkey.Name"),
                tooltip: () => this.Helper.Translation.Get("ModConfigMenu.UseQuickStowHotkey.Desc")
            );

            menu.AddKeybindList(
                mod: this.Manifest,
                getValue: () => this.Config.QuickStowHotkey,
                setValue: value => this.Config.QuickStowHotkey = value,
                name: () => this.Helper.Translation.Get("ModConfigMenu.QuickStowHotkey.Name"),
                tooltip: () => this.Helper.Translation.Get("ModConfigMenu.QuickStowHotkey.Desc")
            );
        }

        /*********
        ** Private methods
        *********/
        /// <summary>Reset the mod's config to its default values.</summary>
        private void Reset()
        {
            ModConfig config = this.Config;
            ModConfig defaults = new();

            config.LootHotkey = defaults.LootHotkey;
            config.CloseMenuAfterLoot = defaults.CloseMenuAfterLoot;
            config.UseQuickStowHotkey = defaults.UseQuickStowHotkey;
            config.QuickStowHotkey = defaults.QuickStowHotkey;
        }
    }
}

