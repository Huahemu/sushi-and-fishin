using System;
using System.Collections.Generic;

namespace PaperPixelGames.Core.Localization
{
	public static class NotificationLocalization
	{
		private static readonly IReadOnlyDictionary<string, string> KeyFallbacks = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			{ "notification.chef.debug_ingredient_incompatible", "A recipe ingredient cannot be placed in a gastropan" },
			{ "notification.chef.clear_center_to_change_recipe", "Move the sushi from the center before changing recipes" },
			{ "notification.chef.clear_center_to_make_sushi", "Move the sushi from the center before making another one" },
			{ "notification.chef.no_space_for_sushi", "No space to place the sushi. It will stay in the center" },
			{ "notification.chef.action_failed", "Oops! The action couldn’t be completed" },
			{ "notification.chef.debug_pans_filled", "Recipe gastropans filled" },
			{ "notification.chef.debug_select_recipe_first", "Select a recipe first" },
			{ "notification.chef.recipe_verification_failed", "The current recipe couldn’t be verified" },
			{ "notification.chef.gastropan_empty", "The gastropan is empty" },
			{ "notification.chef.cannot_pick_up_empty_pan", "The gastropan is empty. You can’t pick it up" },
			{ "notification.chef.gastropan_full", "The gastropan is full and can’t hold any more ingredients." },
			{ "notification.chef.held_gastropan_empty", "The gastropan you’re holding is empty. You can’t transfer anything" },
			{ "notification.chef.pack_empty", "The pack is empty. You can’t transfer anything" },
			{ "notification.chef.recipe_prefix_mismatch", "The placed ingredients don’t match the start of this recipe" },
			{ "notification.chef.recipe_selection_failed", "The recipe couldn’t be selected due to an error" },
			{ "notification.chef.recipe_completed", "The recipe is already completed" },
			{ "notification.chef.recipe_incomplete", "The recipe is not completed yet" },
			{ "notification.chef.recipe_continues", "The same recipe continues" },
			{ "notification.chef.no_active_user_to_complete", "The sushi couldn’t be completed because nobody is using the chef desk" },
			{ "notification.chef.sushi_completion_failed", "The sushi couldn’t be completed due to an error" },
			{ "notification.chef.sushi_no_longer_on_table", "The sushi was no longer on the table." },
			{ "notification.chef.table_reset_success", "The table was reset successfully" },
			{ "notification.chef.table_returned_to_empty_pan", "An ingredient was returned to an empty gastropan." },
			{ "notification.chef.table_return_blocked", "Not enough gastropan space to clear the table. Make room and try again." },
			{ "notification.chef.top_ingredient_mismatch", "The top ingredient doesn’t match the one in the gastropan" },
			{ "notification.chef.debug_not_enough_pans", "There are not enough gastropans for this recipe" },
			{ "notification.chef.nothing_to_pick_up", "There’s nothing on the table to pick up" },
			{ "notification.chef.ingredient_incompatible", "This ingredient cannot be placed in the gastropan" },
			{ "notification.chef.wrong_recipe_ingredient", "This isn’t the required ingredient" },
			{ "notification.chef.invalid_custom_ingredient", "This ingredient cannot be used for custom sushi" },
			{ "notification.chef.wrong_custom_base_ingredient", "Place the required base ingredient first" },
			{ "notification.chef.custom_base_cannot_repeat", "Rice and nori can only be used in the sushi base" },
			{ "notification.chef.custom_filling_limit_reached", "This sushi already has the maximum number of fillings" },
			{ "notification.chef.custom_composition_locked", "Finish preparation before changing this sushi" },
			{ "notification.chef.custom_type_required", "Select Maki, Uramaki, or Temaki first" },
			{ "notification.chef.custom_base_incomplete", "Complete the sushi base before finishing" },
			{ "notification.chef.custom_recipe_saved", "Custom sushi recipe saved" },
			{ "notification.chef.custom_recipe_already_exists", "This sushi already has a recipe" },
			{ "notification.chef.custom_recipe_save_failed", "The custom sushi recipe could not be saved" },
			{ "notification.chef.custom_sushi_spawned", "Sushi is ready. You can save this recipe." },
			{ "notification.chef.custom_sushi_spawned_existing", "Sushi is ready. This recipe already exists and cannot be saved again." },
			{ "notification.chef.custom_mode_ready", "Custom sushi is ready for another composition" },
			{ "notification.chef.custom_recipe_deleted", "Custom sushi recipe deleted" },
			{ "notification.chef.custom_recipe_delete_blocked_order", "This recipe is being used by a customer order" },
			{ "notification.chef.custom_recipe_delete_blocked_preparation", "This recipe is currently being prepared" },
			{ "notification.chef.custom_recipe_delete_blocked_stock", "This recipe cannot be deleted while prepared sushi is still in stock" },
			{ "notification.chef.custom_recipe_delete_blocked_last_sushi", "At least one sushi must remain on the menu" },
			{ "notification.chef.recipe_sushi_type_mismatch", "You can only switch to the same sushi type after starting a recipe" },
			{ "notification.chef.debug_active_user_required", "You must be the active chef desk user" },
			{ "notification.chef.select_recipe_first", "You need to select a recipe first" },
			{ "notification.prep.no_space_to_move_pan", "No space to move the gastropan aside." },
			{ "notification.prep.no_space_to_spawn_pan", "No space to spawn a gastropan." },
			{ "notification.prep.lost_ingredient_recovered", "Prepared ingredient was lost. Station recovered." },
			{ "notification.prep.gastropan_full", "The gastropan is full. It can’t hold any more ingredients." },
			{ "notification.prep.ingredients_different", "The ingredients are different. You can’t add them together." }
		};

		private static readonly IReadOnlyDictionary<string, string> ExactKeys = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			{ "Already clean!", "notification.already_clean" },
			{ "Can't place!", "notification.cant_place" },
			{ "Container is empty.", "notification.container_empty" },
			{ "Container is full.", "notification.container_full" },
			{ "Different beverage already placed.", "notification.different_beverage" },
			{ "Dining table unlock slot unlocked", "notification.dining_slot_unlocked" },
			{ "First complete the tutorial!", "notification.complete_tutorial_first" },
			{ "Fridge is full.", "notification.fridge_full" },
			{ "Game saved successfully.", "notification.game_saved" },
			{ "I'm busy right now!", "notification.busy" },
			{ "It can’t be transferred to the gastropan you’re holding.", "notification.cannot_transfer_to_held_gastropan" },
			{ "Left the lobby.", "notification.left_lobby" },
			{ "No empty slot.", "notification.no_empty_slot" },
			{ "No free quick slot for the dirty tray.", "notification.no_slot_dirty_tray" },
			{ "No free quick slot for the tray.", "notification.no_slot_tray" },
			{ "No item on the table. You cannot use a knife.", "notification.no_item_for_knife" },
			{ "Not enough money to repair the Conveyor Belt!", "notification.not_enough_money_conveyor" },
			{ "Not enough money to repair the table!", "notification.not_enough_money_table" },
			{ "Only raw ingredients can be cut here.", "notification.only_raw_can_be_cut" },
			{ "Open the fridge door first.", "notification.open_fridge_first" },
			{ "Reach the required restaurant level first.", "notification.restaurant_level_required" },
			{ "Recipe has already started", "notification.recipe_already_started" },
			{ "Recipe is not selected", "notification.recipe_not_selected" },
			{ "Save failed.", "notification.save_failed" },
			{ "Save skipped.", "notification.save_skipped" },
			{ "Session ended. Returned to main menu.", "notification.session_ended" },
			{ "Shelf empty.", "notification.shelf_empty" },
			{ "Shelf full.", "notification.shelf_full" },
			{ "Slots are full!", "notification.slots_full" },
			{ "Station is full, clear some space", "notification.station_full" },
			{ "The ingredients are different. The transfer cannot be completed.", "notification.ingredients_different" },
			{ "The table has been reset", "notification.table_reset" },
			{ "This cannot be trashed.", "notification.cannot_trash" },
			{ "This cut type is not available for this item.", "notification.cut_type_unavailable" },
			{ "This item cannot be cut.", "notification.item_cannot_be_cut" },
			{ "This seasoning is empty", "notification.seasoning_empty" },
			{ "This sushi type cannot be seasoned.", "notification.sushi_cannot_be_seasoned" },
			{ "Use the fillet machine first", "notification.use_fillet_machine_first" },
			{ "Waiting for other customers to arrive.", "notification.waiting_for_customers" },
			{ "Wrong beverage.", "notification.wrong_beverage" },
			{ "Wrong item.", "notification.wrong_item" },
			{ "The tray slot is already occupied.", "notification.tray_slot_occupied" },
			{ "This item cannot be placed on the tray.", "notification.item_cannot_be_placed_on_tray" },
			{ "This item does not belong in this container.", "notification.item_does_not_belong_in_container" },
			{ "This action cannot be completed.", "notification.tray_cannot_interact_here" },
			{ "This fridge must be repaired first.", "notification.fridge_repair_required" },
			{ "You can sleep after 21:00", "notification.sleep_after_21" },
			{ "You can’t select a recipe right now", "notification.cannot_select_recipe" },
			{ "You can’t use that item here", "notification.cannot_use_item_here" },
			{ "You need better harpoon!", "notification.better_harpoon_required" },
			{ "RESTAURANT", "notification.restaurant_header" },
			{ "No more customers will arrive today.", "notification.no_more_customers_today" },
			{ "Customers won't arrive while the restaurant is closed.", "notification.restaurant_closed" },
			{ "MENU IS EMPTY", "notification.menu_empty_header" },
			{ "Add at least one sushi to the menu. Dining customers will not arrive.", "notification.menu_empty_description" },
			{ "Pressure", "notification.pressure_header" },
			{ "Depth too deep for your equipment!", "notification.depth_too_deep" },
			{ "OPEN WATERS AHEAD", "notification.open_waters_header" },
			{ "Return to safe waters.", "notification.return_to_safe_waters" }
		};

		public static string Localize(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return text ?? string.Empty;
			}
			if (KeyFallbacks.TryGetValue(text, out var value))
			{
				return LocalizeKey(text, value);
			}
			if (ExactKeys.TryGetValue(text, out var value2))
			{
				return LocalizationDatabase.GetString("UiTable", value2, text);
			}
			if (IsHabitatName(text))
			{
				string key = "tablet.map.habitat_name." + LocalizationDatabase.ToSnakeCase(text);
				return LocalizationDatabase.GetString("UiTable", key, text);
			}
			if (TryLocalizeAffixedValue(text, "Restaurant Level ", string.Empty, "notification.restaurant_level", "Restaurant Level {0}", out var localized))
			{
				return localized;
			}
			if (TryLocalizeAffixedValue(text, "Unlocked: ", string.Empty, "notification.unlocked", "Unlocked: {0}", out localized))
			{
				return localized;
			}
			if (TryLocalizeAffixedValue(text, "Market item unlocked: ", string.Empty, "notification.market_item_unlocked", "Market item unlocked: {0}", out localized))
			{
				return localized;
			}
			if (TryLocalizeAffixedValue(text, string.Empty, " unlocked", "notification.item_unlocked", "{0} unlocked", out localized))
			{
				return localized;
			}
			if (TryLocalizeAffixedValue(text, "Table ", ": Order received.", "notification.table_order_received", "Table {0}: Order received.", out localized))
			{
				return localized;
			}
			return text;
		}

		private static string LocalizeKey(string key, string fallback)
		{
			return LocalizationDatabase.GetStringOrFallback("UiTable", key, fallback);
		}

		private static bool IsHabitatName(string text)
		{
			if (!text.EndsWith("Shoals", StringComparison.Ordinal) && !text.EndsWith("Reef", StringComparison.Ordinal) && !text.EndsWith("Current", StringComparison.Ordinal) && !text.EndsWith("Run", StringComparison.Ordinal) && !text.EndsWith("Gate", StringComparison.Ordinal) && !text.EndsWith("Garden", StringComparison.Ordinal) && !text.EndsWith("Basin", StringComparison.Ordinal) && !text.EndsWith("Hollow", StringComparison.Ordinal) && !text.EndsWith("Shelf", StringComparison.Ordinal) && !text.EndsWith("Pass", StringComparison.Ordinal) && !text.EndsWith("Trench", StringComparison.Ordinal) && !text.EndsWith("Drift", StringComparison.Ordinal) && !text.EndsWith("Ledge", StringComparison.Ordinal))
			{
				return text.EndsWith("Cave", StringComparison.Ordinal);
			}
			return true;
		}

		private static bool TryLocalizeAffixedValue(string text, string prefix, string suffix, string key, string fallback, out string localized)
		{
			localized = null;
			if (!text.StartsWith(prefix, StringComparison.Ordinal) || !text.EndsWith(suffix, StringComparison.Ordinal) || text.Length <= prefix.Length + suffix.Length)
			{
				return false;
			}
			string text2 = text.Substring(prefix.Length, text.Length - prefix.Length - suffix.Length);
			localized = LocalizationDatabase.GetFormattedString("UiTable", key, fallback, text2);
			return true;
		}
	}
}
