using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;
using KSP;

namespace KRnD
{
    [KSPModule("R&D_T")]
    public class KRnDModuleT : PartModule
    {
        // Version of this part (just for show):
        [KSPField(guiActive = true, guiName = "R&D", guiUnits = "", guiFormat = "", isPersistant = false)]
        public String moduleVersion;

        // Flag, which can be set by other mods to apply latest upgrades on load:
        [KSPField(isPersistant = true)]
        public int upgradeToLatest = 0;

		[KSPField(isPersistant = true)]
		public KRnDLocalUpgradesList localUpgrades = new KRnDLocalUpgradesList();

         public static String ToRoman(int number)
        {
            if (number == 0) return "";
            if (number >= 1000) return "M" + ToRoman(number - 1000);
            if (number >= 900) return "CM" + ToRoman(number - 900);
            if (number >= 500) return "D" + ToRoman(number - 500);
            if (number >= 400) return "CD" + ToRoman(number - 400);
            if (number >= 100) return "C" + ToRoman(number - 100);
            if (number >= 90) return "XC" + ToRoman(number - 90);
            if (number >= 50) return "L" + ToRoman(number - 50);
            if (number >= 40) return "XL" + ToRoman(number - 40);
            if (number >= 10) return "X" + ToRoman(number - 10);
            if (number >= 9) return "IX" + ToRoman(number - 9);
            if (number >= 5) return "V" + ToRoman(number - 5);
            if (number >= 4) return "IV" + ToRoman(number - 4);
            if (number >= 1) return "I" + ToRoman(number - 1);
            return number.ToString();
        }

        public String getVersion()
        {
            int upgrades = 0;
            if (upgrades == 0) return "";
            return "Mk " + ToRoman(upgrades + 1); // Mk I is the part without upgrades, Mk II the first upgraded version.
        }

        public override void OnStart(PartModule.StartState state)
        {
            this.moduleVersion = getVersion();
            if (this.moduleVersion == "")
            {
                this.Fields[0].guiActive = false;
            }
            else
            {
                this.Fields[0].guiActive = true;
            }
        }

        // Returns the upgrade-stats which this module represents.
        public KRnDUpgrade getCurrentUpgrades()
        {
            KRnDUpgrade upgrades = new KRnDUpgrade();
            return upgrades;
        }
    }

	public class KRnDLocalUpgradesList : List<KRnDLocalUpgradeT>, IConfigNode
	{

		public void Save(ConfigNode node)
		{
			try
			{
				foreach (KRnDLocalUpgradeT upgrade in this)
				{
					ConfigNode currentUpgradeNode = new ConfigNode(upgrade.upgradeName);
					currentUpgradeNode.AddValue("upgradeModuleName",upgrade.upgradeModuleName);
					currentUpgradeNode.AddValue("upgradePropertyName",upgrade.upgradePropertyName);
					currentUpgradeNode.AddValue("upgradeEquation",upgrade.upgradeEquation);
					currentUpgradeNode.AddValue("upgradeLevel",upgrade.upgradeLevel);
					node.AddNode(currentUpgradeNode);
					Debug.Log("[KRnD] saved: " + upgrade.upgradeName + " " + upgrade.ToString());
				}
			}
			catch (Exception e)
			{
				Debug.LogError("[KRnD] OnSave(): " + e.ToString());
			}
		}

		public void Load(ConfigNode node)
		{
			try
			{
				this.Clear();
				foreach (ConfigNode upgradeNode in node.GetNodes())
				{
					KRnDLocalUpgradeT localUpgrade = new KRnDLocalUpgradeT ();
					localUpgrade.upgradeEquation = upgradeNode.GetValue("upgradeEquation");
					localUpgrade.upgradeLevel =  Convert.ToInt32(upgradeNode.GetValue("upgradeLevel"));
					localUpgrade.upgradeModuleName = upgradeNode.GetValue("upgradeModuleName");
					localUpgrade.upgradeName = upgradeNode.name;
					localUpgrade.upgradePropertyName = upgradeNode.GetValue("upgradePropertyName");
					this.Add (localUpgrade);
					Debug.Log("[KRnD] loaded: " + localUpgrade.upgradeName + " " + localUpgrade.ToString());
					//KRnDUpgrade upgrade = KRnDUpgrade.createFromConfigNode(upgradeNode);
					//KRnD.upgrades.Add(upgradeNode.name, upgrade);
				}

				// Update global part-list with new upgrades from the savegame:
				//upgradesApplied = KRnD.updateGlobalParts();

				// If we started with an active vessel, update that vessel:
				/*Vessel vessel = FlightGlobals.ActiveVessel;
				if (vessel)
				{
					KRnD.updateVessel(vessel);
				}*/

			}
			catch (Exception e)
			{
				Debug.LogError("[KRnD] OnLoad(): " + e.ToString());
			}
		}
	}

	public class KRnDLocalUpgradeT
	{
		[KSPField(isPersistant = true)]
		public String upgradeName = "";

		[KSPField(isPersistant = true)]
		public String upgradeModuleName = "";

		[KSPField(isPersistant = true)]
		public String upgradePropertyName = "";

		[KSPField(isPersistant = true)]
		public String upgradeEquation = ""; //keyword "level" - is for current upgrade level

		[KSPField(isPersistant = true)]
		public int upgradeLevel = 0; //keyword "level" - is for current upgrade level
	}
}
