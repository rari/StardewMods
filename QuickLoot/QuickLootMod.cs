using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using System;
using System.Collections.Generic;
using StardewValley;
using System.Reflection;
using StardewValley.Tools;
using StardewValley.Objects;
using Microsoft.Xna.Framework;
using StardewValley.Menus;
using System.Linq;

namespace QuickLootMod {
    public class QuickLootMod : Mod {
        private ItemGrabMenu menu;
        private ModConfig config;
        private GenericModConfigMenuIntegration configMenu;
        private bool isConvenientInventoryLoaded = false;
        private Type convenientInventoryType = null;
        private Type convenientInventoryModEntryType = null;

        public override void Entry(IModHelper helper) {
            // Load or create config
            config = Helper.ReadConfig<ModConfig>();
            
            // Register Generic Mod Config Menu integration if available
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            
            helper.Events.Display.MenuChanged += ReadMenuChanged;
            helper.Events.Input.ButtonPressed += ButtonPressed;
        }

        /// <summary>Raised after the game is launched, right before the first update tick. This happens once per game session (unlike Entry, which is only called once per mod).</summary>
        private void OnGameLaunched(object sender, StardewModdingAPI.Events.GameLaunchedEventArgs e) {
            // Check if ConvenientInventory is loaded for favorite item support
            isConvenientInventoryLoaded = Helper.ModRegistry.IsLoaded("gaussfire.ConvenientInventory");
            if (isConvenientInventoryLoaded) {
                try {
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                    foreach (var assembly in assemblies) {
                        if (convenientInventoryType == null) {
                            convenientInventoryType = assembly.GetType("ConvenientInventory.ConvenientInventory");
                        }
                        if (convenientInventoryModEntryType == null) {
                            convenientInventoryModEntryType = assembly.GetType("ConvenientInventory.ModEntry");
                        }
                        if (convenientInventoryType != null && convenientInventoryModEntryType != null) {
                            // Test that we can access the FavoriteItemSlots property
                            var testProperty = convenientInventoryType.GetProperty("FavoriteItemSlots", BindingFlags.Public | BindingFlags.Static);
                            if (testProperty != null) {
                                Monitor.Log("Successfully found ConvenientInventory.FavoriteItemSlots property", StardewModdingAPI.LogLevel.Trace);
                            } else {
                                Monitor.Log("Could not find ConvenientInventory.FavoriteItemSlots property", StardewModdingAPI.LogLevel.Warn);
                            }
                            break;
                        }
                    }
                } catch (Exception ex) {
                    // If reflection fails, we'll just skip favorite checking
                    Monitor.Log($"Failed to load ConvenientInventory types: {ex.Message}", StardewModdingAPI.LogLevel.Warn);
                    convenientInventoryType = null;
                    convenientInventoryModEntryType = null;
                }
            } else {
                Monitor.Log("ConvenientInventory is not loaded, favorite item support disabled", StardewModdingAPI.LogLevel.Trace);
            }
            
            // Register Generic Mod Config Menu integration if available
            configMenu = new GenericModConfigMenuIntegration(
                manifest: ModManifest,
                modRegistry: Helper.ModRegistry,
                config: config,
                save: () => Helper.WriteConfig(config),
                helper: Helper,
                monitor: Monitor
            );
            configMenu.Register();
        }

        private void ButtonPressed(object sender, StardewModdingAPI.Events.ButtonPressedEventArgs e) {
            // Check if the configured keybind was just pressed
            if (config.LootHotkey.JustPressed()) {
                PerformLoot();
            }
            // Check if Quick Stow hotkey was pressed
            else if (config.UseQuickStowHotkey && config.QuickStowHotkey.JustPressed()) {
                PerformStow();
            }
        }


        /// <summary>Perform the quick looting action.</summary>
        private void PerformLoot() {
            // Don't operate on CJB Item Spawner menus
            if (menu != null && !IsCJBItemSpawnerMenu(menu)) {
                int max = 32;
                System.Collections.Generic.IList<Item> actualInventory = menu.ItemsToGrabMenu.actualInventory;
                int itemsBefore = actualInventory.Count;
                
                // Fixed: Changed || to && and added proper bounds checking
                while (actualInventory.Count > 0 && max-- > 0) {
                    // Additional safety check to prevent index out of range
                    if (actualInventory.Count == 0) {
                        break;
                    }
                    
                    Item item = actualInventory[0];
                    if (Game1.player.addItemToInventoryBool(item, false)) {
                        actualInventory.RemoveAt(0);
                    } else {
                        break;
                    }
                }
                
                // Play sound if items were looted
                if (actualInventory.Count < itemsBefore) {
                    Game1.playSound("Ship");
                }
                
                // Close the menu if all items were looted and auto-close is enabled
                if (config.CloseMenuAfterLoot && actualInventory.Count == 0 && menu != null) {
                    if (Game1.activeClickableMenu == menu) {
                        menu.exitThisMenu();
                    }
                }
            }
        }

        private void ReadMenuChanged(object sender, StardewModdingAPI.Events.MenuChangedEventArgs e) {
            if (e.NewMenu is ItemGrabMenu) {
                menu = (ItemGrabMenu)e.NewMenu;
            }
            if (e.OldMenu == menu) {
                menu = null;
            }
        }

        /// <summary>Perform the quick stow action - transfers items from player inventory to the opened chest.</summary>
        private void PerformStow() {
            Monitor.Log("PerformStow called", StardewModdingAPI.LogLevel.Info);
            
            // Only work when a chest menu is open
            if (menu == null || IsCJBItemSpawnerMenu(menu)) {
                Monitor.Log("Menu is null or CJB menu, returning", StardewModdingAPI.LogLevel.Info);
                return;
            }

            // Get the chest from the menu context
            Chest chest = menu.context as Chest;
            if (chest == null) {
                Monitor.Log("Chest is null, returning", StardewModdingAPI.LogLevel.Info);
                return;
            }

            var playerInventory = Game1.player.Items;
            bool movedAtLeastOne = false;
            
            Monitor.Log($"Processing {playerInventory.Count} inventory items. ConvenientInventory loaded: {isConvenientInventoryLoaded}", StardewModdingAPI.LogLevel.Info);
            
            // Create a copy of the inventory list to iterate over (since we'll be modifying it)
            var itemsToProcess = new List<Item>();
            var itemIndices = new List<int>();
            for (int i = 0; i < playerInventory.Count; i++) {
                if (playerInventory[i] != null) {
                    itemsToProcess.Add(playerInventory[i]);
                    itemIndices.Add(i);
                }
            }
            
            Monitor.Log($"Found {itemsToProcess.Count} non-null items to process", StardewModdingAPI.LogLevel.Info);
            
            // Iterate through player inventory and transfer items to chest
            for (int idx = 0; idx < itemsToProcess.Count; idx++) {
                Item item = itemsToProcess[idx];
                if (item == null) {
                    continue;
                }

                // Get the current index of the item in the inventory (may have changed)
                int currentInventoryIndex = playerInventory.IndexOf(item);
                if (currentInventoryIndex == -1) {
                    Monitor.Log($"Item {item.Name} no longer in inventory, skipping", StardewModdingAPI.LogLevel.Info);
                    continue; // Item no longer in inventory
                }
                
                // Check if item is favorited (skip if favorited)
                bool isFavorited = IsItemFavorited(currentInventoryIndex);
                Monitor.Log($"Item {item.Name} at slot {currentInventoryIndex}: isFavorited={isFavorited}", StardewModdingAPI.LogLevel.Info);
                if (isFavorited) {
                    Monitor.Log($"Skipping favorited item {item.Name} at slot {currentInventoryIndex}", StardewModdingAPI.LogLevel.Info);
                    continue;
                }

                // Use the chest's addItem method which handles stacking properly
                int stackBefore = item.Stack;
                Item leftoverItem = chest.addItem(item);
                bool itemMoved = leftoverItem == null || item.Stack != stackBefore;
                
                if (itemMoved) {
                    movedAtLeastOne = true;
                    
                    // If the item was completely transferred, remove it from inventory
                    if (leftoverItem == null) {
                        Game1.player.removeItemFromInventory(item);
                    }
                    // If only part was transferred, the item's stack was already updated by addItem
                }
            }

            // Play sound if items were moved
            if (movedAtLeastOne) {
                Game1.playSound("Ship");
            } else {
                Game1.playSound("cancel");
            }
        }

        /// <summary>Check if an item at the given inventory index is favorited by ConvenientInventory.</summary>
        /// <param name="inventoryIndex">The index in the player's inventory.</param>
        /// <returns>True if the item is favorited, false otherwise.</returns>
        private bool IsItemFavorited(int inventoryIndex) {
            Monitor.Log($"IsItemFavorited called for slot {inventoryIndex}", StardewModdingAPI.LogLevel.Info);
            
            if (!isConvenientInventoryLoaded || convenientInventoryType == null || convenientInventoryModEntryType == null) {
                Monitor.Log("ConvenientInventory not loaded or types not found", StardewModdingAPI.LogLevel.Info);
                return false;
            }

            try {
                // First check if favorites are enabled in ConvenientInventory's config
                var configProperty = convenientInventoryModEntryType.GetProperty("Config", BindingFlags.Public | BindingFlags.Static);
                if (configProperty == null) {
                    Monitor.Log("Could not find ModEntry.Config property", StardewModdingAPI.LogLevel.Warn);
                    return false;
                }
                
                var config = configProperty.GetValue(null);
                if (config == null) {
                    Monitor.Log("ModEntry.Config is null", StardewModdingAPI.LogLevel.Warn);
                    return false;
                }
                
                var isEnableFavoriteItemsProperty = config.GetType().GetProperty("IsEnableFavoriteItems");
                if (isEnableFavoriteItemsProperty == null) {
                    Monitor.Log("Could not find IsEnableFavoriteItems property", StardewModdingAPI.LogLevel.Warn);
                    return false;
                }
                
                bool favoritesEnabled = (bool)isEnableFavoriteItemsProperty.GetValue(config);
                Monitor.Log($"Favorites enabled in ConvenientInventory: {favoritesEnabled}", StardewModdingAPI.LogLevel.Info);
                if (!favoritesEnabled) {
                    Monitor.Log("Favorites are disabled in ConvenientInventory config", StardewModdingAPI.LogLevel.Info);
                    return false; // Favorites are disabled in ConvenientInventory
                }

                // Get the FavoriteItemSlots property from ConvenientInventory
                // This is a PerScreen<bool[]> property, so we need to call the getter
                var favoriteItemSlotsProperty = convenientInventoryType.GetProperty("FavoriteItemSlots", BindingFlags.Public | BindingFlags.Static);
                if (favoriteItemSlotsProperty == null) {
                    Monitor.Log("Could not find FavoriteItemSlots property", StardewModdingAPI.LogLevel.Warn);
                    return false;
                }
                
                // Call the property getter to get the actual bool[] array
                // The getter will initialize the array if needed
                object propertyValue = favoriteItemSlotsProperty.GetValue(null);
                if (propertyValue == null) {
                    Monitor.Log("FavoriteItemSlots property getter returned null", StardewModdingAPI.LogLevel.Warn);
                    return false;
                }
                
                bool[] favoriteItemSlots = propertyValue as bool[];
                if (favoriteItemSlots == null) {
                    Monitor.Log($"FavoriteItemSlots property returned non-array type: {propertyValue.GetType().FullName}", StardewModdingAPI.LogLevel.Warn);
                    return false;
                }
                
                Monitor.Log($"FavoriteItemSlots array length: {favoriteItemSlots.Length}, checking index {inventoryIndex}", StardewModdingAPI.LogLevel.Info);
                
                // Ensure the array is large enough and the index is valid
                if (inventoryIndex < 0) {
                    Monitor.Log($"Invalid inventory index: {inventoryIndex}", StardewModdingAPI.LogLevel.Warn);
                    return false;
                }
                
                if (inventoryIndex >= favoriteItemSlots.Length) {
                    Monitor.Log($"Inventory index {inventoryIndex} out of range for FavoriteItemSlots array (length: {favoriteItemSlots.Length})", StardewModdingAPI.LogLevel.Warn);
                    return false;
                }
                
                bool isFavorited = favoriteItemSlots[inventoryIndex];
                Monitor.Log($"Favorite status for slot {inventoryIndex}: {isFavorited} (array length: {favoriteItemSlots.Length})", StardewModdingAPI.LogLevel.Info);
                return isFavorited;
                
            } catch (Exception ex) {
                // If reflection fails, log and assume not favorited
                Monitor.Log($"Failed to check favorite status for slot {inventoryIndex}: {ex.Message}\n{ex.StackTrace}", StardewModdingAPI.LogLevel.Error);
            }

            return false;
        }

        /// <summary>Check if the menu is from CJB Item Spawner and should be excluded from quick looting.</summary>
        /// <param name="menu">The ItemGrabMenu to check.</param>
        /// <returns>True if this is a CJB Item Spawner menu.</returns>
        private bool IsCJBItemSpawnerMenu(ItemGrabMenu menu) {
            if (menu == null) {
                return false;
            }

            // CJB Item Spawner creates a menu class called "ItemMenu" in the namespace "CJBItemSpawner.Framework"
            // Check the menu's type name to detect it
            string menuTypeName = menu.GetType().FullName ?? "";
            
            // Check if it's the CJB Item Spawner ItemMenu class
            // The class is: CJBItemSpawner.Framework.ItemMenu
            return menuTypeName == "CJBItemSpawner.Framework.ItemMenu" ||
                   menuTypeName.Contains("CJBItemSpawner.Framework.ItemMenu");
        }
    }
    
    /// <summary>The raw mod configuration.</summary>
    public class ModConfig {
        /*********
        ** Accessors
        *********/
        /// <summary>The keybind(s) to press for quick looting items from chests and containers. Default is Tab and LeftStick. Supports key combinations (e.g., Ctrl+Tab).</summary>
        public KeybindList LootHotkey { get; set; } = KeybindList.Parse("Tab, LeftStick");

        /// <summary>Whether to automatically close the inventory menu after looting all items.</summary>
        public bool CloseMenuAfterLoot { get; set; } = true;

        /// <summary>Whether to use a hotkey for quick stow (transfer items from inventory to chest).</summary>
        public bool UseQuickStowHotkey { get; set; } = false;

        /// <summary>The keybind(s) to press for quick stowing items from inventory to chest. Default is L. Supports key combinations (e.g., Ctrl+L).</summary>
        public KeybindList QuickStowHotkey { get; set; } = KeybindList.ForSingle(new[] { SButton.L });
    }
}

