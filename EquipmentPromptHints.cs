using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace ChatAi
{
	public static class OutfitSummaryBuilder
	{
		public static string BuildOutfitSummary(Hero hero)
		{
			try
			{
				if (hero == null || hero.CharacterObject == null || hero.CharacterObject.Equipment == null)
				{
					return "No outfit details available.";
				}

				Equipment equipment = hero.CharacterObject.Equipment;
				List<string> armorSnippets = new List<string>();
				List<string> weaponNames = new List<string>();

				AppendArmorPhrase(equipment, EquipmentIndex.Head, "headgear", armorSnippets);
				AppendArmorPhrase(equipment, EquipmentIndex.Cape, "cloak", armorSnippets);
				AppendArmorPhrase(equipment, EquipmentIndex.Body, "body armor", armorSnippets);
				AppendArmorPhrase(equipment, EquipmentIndex.Gloves, "gauntlets", armorSnippets);
				AppendArmorPhrase(equipment, EquipmentIndex.Leg, "legwear", armorSnippets);

				for (EquipmentIndex i = EquipmentIndex.Weapon0; i <= EquipmentIndex.Weapon3; i++)
				{
					var item = equipment[i].Item;
					if (item != null)
					{
						weaponNames.Add(item.Name.ToString());
					}
				}

				StringBuilder summary = new StringBuilder();
				if (armorSnippets.Any())
				{
					summary.Append("Attire: " + string.Join(", ", armorSnippets) + ". ");
				}
				else
				{
					summary.Append("Attire: plain garments, no notable armor. ");
				}

				if (weaponNames.Any())
				{
					var distinct = weaponNames.Distinct();
					summary.Append("Arms: " + string.Join(", ", distinct) + ".");
				}
				else
				{
					summary.Append("Arms: none visible.");
				}

				return summary.ToString();
			}
			catch
			{
				return "No outfit details available.";
			}
		}

		private static void AppendArmorPhrase(Equipment equipment, EquipmentIndex index, string slotLabel, List<string> parts)
		{
			try
			{
				var item = equipment[index].Item;
				if (item != null)
				{
					parts.Add($"{item.Name} as {slotLabel}");
				}
			}
			catch { }
		}
	}
}


