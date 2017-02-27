﻿using System;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;
using KSP;
using System.IO;

using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace KRnD
{
    // This class stores all types of upgrades a part can have.
    public class KRnDUpgradeT
    {
        //public int ispVac = 0;
        //public const String ISP_VAC = "ispVac";

        public override string ToString()
        {
            return "KRnDUpgradeT(" +
              //  ISP_VAC+":" + this.ispVac.ToString() + "," +
				")";
        }

        public ConfigNode createConfigNode(string name)
        {
            ConfigNode node = new ConfigNode(name);
           // if (this.ispVac > 0) node.AddValue(ISP_VAC, this.ispVac.ToString());
            return node;
        }

        public static KRnDUpgrade createFromConfigNode(ConfigNode node)
        {
            KRnDUpgrade upgrade = new KRnDUpgrade();
          //  if (node.HasValue(ISP_VAC)) upgrade.ispVac = Int32.Parse(node.GetValue(ISP_VAC));
            return upgrade;
        }

        public KRnDUpgrade clone()
        {
            KRnDUpgrade copy = new KRnDUpgrade();
           // copy.ispVac = this.ispVac;
            return copy;
        }
    }

    // This class is used to store all relevant base-stats of a part used to calculate all other stats with
    // incrementel upgrades as well as a backup for resoting the original stats (eg after loading a savegame).
    public class PartStatsT
    {
     //   public float mass = 0;

        public PartStatsT(Part part)
        {
      //      this.mass = part.mass;

            // There might be different converter-modules in the same part with different names (eg for Fuel, Monopropellant, etc):

        }
    }

    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    public class KRnDT : UnityEngine.MonoBehaviour
    {
        private static bool initialized = false;
        public static Dictionary<string, PartStats> originalStats = null;
        public static Dictionary<string, KRnDUpgrade> upgrades = new Dictionary<string, KRnDUpgrade>();
        public static List<string> fuelResources = null;
        public static List<string> blacklistedParts = null;

        public static KRnDModule getKRnDModule(Part part)
        {
            // If this is a blacklisted part, don't touch it, even if it should have an RnD-Module. We do it like
            // this because using module-manager-magic to prevent RnD from getting installed with other, incompatible
            // modules from other mods depends on the order in which module-manager applies the patches; this way
            // we can avoid these problems. It means though that parts might have the RnD-Module, wich isn't used though.
            if (KRnD.blacklistedParts.Contains(KRnD.sanatizePartName(part.name))) return null;

            // Check if this part has the RnD-Module and return it:
            foreach (PartModule partModule in part.Modules)
            {
                if (partModule.moduleName == "KRnDModule") return (KRnDModule)partModule;
            }
            return null;
        }

        // Multi-Mode engines have multiple Engine-Modules which we return as a list.
       /* public static List<ModuleEngines> getEngineModules(Part part)
        {
            List<ModuleEngines> engines = new List<ModuleEngines>();
            foreach (PartModule partModule in part.Modules)
            {
                if (partModule.moduleName == "ModuleEngines" || partModule.moduleName == "ModuleEnginesFX")
                {
                    engines.Add((ModuleEngines)partModule);
                }
            }
            if (engines.Count > 0) return engines;
            return null;
        }*/
 
        // Since KSP 1.1 the info-text of solar panels is not updated correctly, so we have use this workaround-function
        // to create our own text.
        public static String getSolarPanelInfo(ModuleDeployableSolarPanel solarModule)
        {
            String info = solarModule.GetInfo();
            float chargeRate = solarModule.chargeRate * solarModule.efficiencyMult;
            String chargeString = chargeRate.ToString("0.####/s");
            String prefix = "<b>Electric Charge: </b>";
            return Regex.Replace(info, prefix + "[0-9.]+/[A-Za-z.]+", prefix + chargeString);
        }

        // Updates the global dictionary of available parts with the current set of upgrades (should be
        // executed for example when a new game starts or an existing game is loaded).
        public static int updateGlobalParts()
        {
            int upgradesApplied = 0;
            try
            {
                if (KRnD.upgrades == null) throw new Exception("upgrades-dictionary missing");
                foreach (AvailablePart part in PartLoader.LoadedPartsList)
                {
                    try
                    {
                        KRnDUpgrade upgrade;
                        if (!KRnD.upgrades.TryGetValue(part.name, out upgrade)) upgrade = new KRnDUpgrade(); // If there are no upgrades, reset the part.

                        // Udate the part to its latest model:
                        KRnD.updatePart(part.partPrefab, true);

                        // Rebuild the info-screen:
                        int converterModuleNumber = 0; // There might be multiple modules of this type
                        int engineModuleNumber = 0; // There might be multiple modules of this type
                        foreach (AvailablePart.ModuleInfo info in part.moduleInfos)
                        {
                            if (info.moduleName.ToLower() == "engine")
                            {
                                List<ModuleEngines> engines = KRnD.getEngineModules(part.partPrefab);
                                if (engines != null && engines.Count > 0)
                                {
                                    ModuleEngines engine = engines[engineModuleNumber];
                                    info.info = engine.GetInfo();
                                    info.primaryInfo = engine.GetPrimaryField();
                                    engineModuleNumber++;
                                }
                            }
                             else if (info.moduleName.ToLower() == "resource converter")
                            {
                                List<ModuleResourceConverter> converterList = KRnD.getConverterModules(part.partPrefab);
                                if (converterList != null && converterList.Count > 0)
                                {
                                    ModuleResourceConverter converter = converterList[converterModuleNumber];
                                    info.info = converter.GetInfo();
                                    converterModuleNumber++;
                                }
                            }
                        }

                        List<PartResource> fuelResources = KRnD.getFuelResources(part.partPrefab);
                        PartResource electricCharge = KRnD.getChargeResource(part.partPrefab);
                        foreach (AvailablePart.ResourceInfo info in part.resourceInfos)
                        {
                            // The Resource-Names are not always formated the same way, eg "Electric Charge" vs "ElectricCharge", so we do some reformating.
                            if (electricCharge != null && info.resourceName.Replace(" ", "").ToLower() == electricCharge.resourceName.Replace(" ", "").ToLower())
                            {
                                info.info = electricCharge.GetInfo();
                                info.primaryInfo = "<b>" + info.resourceName + ":</b> " + electricCharge.maxAmount.ToString();
                            }
                            else if (fuelResources != null)
                            {
                                foreach (PartResource fuelResource in fuelResources)
                                {
                                    if (info.resourceName.Replace(" ", "").ToLower() == fuelResource.resourceName.Replace(" ", "").ToLower())
                                    {
                                        info.info = fuelResource.GetInfo();
                                        info.primaryInfo = "<b>" + info.resourceName + ":</b> " + fuelResource.maxAmount.ToString();
                                        break;
                                    }
                                }
                            }
                        }

                        upgradesApplied++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("[KRnD] updateGlobalParts(" + part.title.ToString() + "): " + e.ToString());
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[KRnD] updateGlobalParts(): " + e.ToString());
            }
            return upgradesApplied;
        }

        // Updates all parts in the vessel that is currently active in the editor.
        public static void updateEditorVessel(Part rootPart=null)
        {
            if (rootPart == null) rootPart = EditorLogic.RootPart;
            if (!rootPart) return;
            KRnD.updatePart(rootPart, true); // Update to the latest model
            foreach (Part childPart in rootPart.children)
            {
                KRnD.updateEditorVessel(childPart);
            }
        }

        // Updates the given part either to the latest model (updateToLatestModel=TRUE) or to the model defined by its
        // KRnDModule.
        public static void updatePart(Part part, bool updateToLatestModel)
        {
            KRnDUpgrade upgradesToApply;
            if (updateToLatestModel)
            {
                if (KRnD.upgrades.TryGetValue(KRnD.sanatizePartName(part.name), out upgradesToApply))
                {
                    // Apply upgrades from global list:
                    KRnD.updatePart(part, upgradesToApply);
                }
                else
                {
                    // No Upgrades found, applay base-stats:
                    upgradesToApply = new KRnDUpgrade();
                    KRnD.updatePart(part, upgradesToApply);
                }
            }
            else
            {
                // Extract current upgrades of the part and set thoes stats:
                KRnDModule rndModule = KRnD.getKRnDModule(part);
                if (rndModule != null && (upgradesToApply = rndModule.getCurrentUpgrades()) != null)
                {
                    // Apply upgrades from the RnD-Module:
                    KRnD.updatePart(part, upgradesToApply);
                }
                else
                {
                    // No Upgrades found, applay base-stats:
                    upgradesToApply = new KRnDUpgrade();
                    KRnD.updatePart(part, upgradesToApply);
                }
            }
        }

        // Sometimes the name of the root-part of a vessel is extended by the vessel-name like "Mk1Pod (X-Bird)", this function can be used
        // as wrapper to always return the real name:
        public static string sanatizePartName(string partName)
        {
            return Regex.Replace(partName, @" \(.*\)$", "");
        }

        // Updates the given part with all upgrades provided in "upgradesToApply".
        public static void updatePart(Part part, KRnDUpgrade upgradesToApply)
        {
            try
            {
                // Find all relevant modules of this part:
                KRnDModule rndModule = KRnD.getKRnDModule(part);
                if (rndModule == null) return;
                if (KRnD.upgrades == null) throw new Exception("upgrades-dictionary missing");
                if (KRnD.originalStats == null) throw new Exception("original-stats-dictionary missing");

                // Get the part-name ("):
                String partName = KRnD.sanatizePartName(part.name);

                // Get the original part-stats:
                PartStats originalStats;
                if (!KRnD.originalStats.TryGetValue(partName, out originalStats)) throw new Exception("no original-stats for part '" + partName + "'");

                KRnDUpgrade latestModel;
                if (!KRnD.upgrades.TryGetValue(partName, out latestModel)) latestModel = null;

                
				// Charge Rate:
                ModuleDeployableSolarPanel solarPanel = KRnD.getSolarPanelModule(part);
                if (solarPanel)
                {
                    rndModule.chargeRate_upgrades = upgradesToApply.chargeRate;
                    float chargeEfficiency = (1 + KRnD.calculateImprovementFactor(rndModule.chargeRate_improvement, rndModule.chargeRate_improvementScale, upgradesToApply.chargeRate));
                    // Somehow changing the charge-rate stopped working in KSP 1.1, so we use the efficiency instead. This however does not
                    // show up in the module-info (probably a bug in KSP), which is why we have another workaround to update the info-texts.
                    // float chargeRate = originalStats.chargeRate * chargeEfficiency;
                    // solarPanel.chargeRate = chargeRate;
                    solarPanel.efficiencyMult = chargeEfficiency;
                }
                else
                {
                    rndModule.chargeRate_upgrades = 0;
                }

            }
            catch (Exception e)
            {
                Debug.LogError("[KRnD] updatePart("+part.name.ToString()+"): " + e.ToString());
            }
        }

        // Updates all parts of the given vessel according to their RnD-Moudle settings (should be executed
        // when the vessel is loaded to make sure, that the vessel uses its own, historic upgrades and not
        // the global part-upgrades).
        public static void updateVessel(Vessel vessel)
        {
            try
            {
                if (!vessel.isActiveVessel) return; // Only the currently active vessel matters, the others are not simulated anyway.
                if (KRnD.upgrades == null) throw new Exception("upgrades-dictionary missing");
                Debug.Log("[KRnD] updating vessel '" + vessel.vesselName.ToString() + "'");

                // Iterate through all parts:
                foreach (Part part in vessel.parts)
                {
                    // We only have to update parts which have the RnD-Module:
                    KRnDModule rndModule = KRnD.getKRnDModule(part);
                    if (rndModule == null) continue;

                    if (vessel.situation == Vessel.Situations.PRELAUNCH)
                    {
                        // Update the part with the latest model while on the launchpad:
                        KRnD.updatePart(part, true);
                    }
                    else if (rndModule.upgradeToLatest > 0)
                    {
                        // Flagged by another mod (eg KSTS) to get updated to the latest model (once):
                        Debug.Log("[KRnD] part '"+ KRnD.sanatizePartName(part.name) + "' of '"+ vessel.vesselName + "' was flagged to be updated to the latest model");
                        rndModule.upgradeToLatest = 0;
                        KRnD.updatePart(part, true);
                    }
                    else
                    {
                        // Update this part with its own stats:
                        KRnD.updatePart(part, false);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[KRnD] updateVesselActive(): " + e.ToString());
            }
        }

        // Is called every time the active vessel changes (on entering a scene, switching the vessel or on docking).
        private void OnVesselChange(Vessel vessel)
        {
            try
            {
                KRnD.updateVessel(vessel);
            }
            catch (Exception e)
            {
                Debug.LogError("[KRnD] OnVesselChange(): " + e.ToString());
            }
        }

        // Is called when we interact with a part in the editor.
        private void EditorPartEvent(ConstructionEventType ev, Part part)
        {
            try
            {
                if (ev != ConstructionEventType.PartCreated && ev != ConstructionEventType.PartDetached && ev != ConstructionEventType.PartAttached && ev != ConstructionEventType.PartDragging) return;
                KRnDGUI.selectedPart = part;
            }
            catch (Exception e)
            {
                Debug.LogError("[KRnD] EditorPartEvent(): " + e.ToString());
            }
        }

        public List<string> getBlacklistedModules()
        {
            List<string> blacklistedModules = new List<string>();
            try
            {
                ConfigNode node = ConfigNode.Load(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/blacklist.cfg");

                foreach (string blacklistedModule in node.GetValues("BLACKLISTED_MODULE"))
                {
                    if (!blacklistedModules.Contains(blacklistedModule)) blacklistedModules.Add(blacklistedModule);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[KRnD] getBlacklistedModules(): " + e.ToString());
            }
            return blacklistedModules;
        }

        public List<string> getBlacklistedParts()
        {
            List<string> blacklistedParts = new List<string>();
            try
            {
                ConfigNode node = ConfigNode.Load(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/blacklist.cfg");

                foreach (string blacklistedPart in node.GetValues("BLACKLISTED_PART"))
                {
                    if (!blacklistedParts.Contains(blacklistedPart)) blacklistedParts.Add(blacklistedPart);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[KRnD] getBlacklistedParts(): " + e.ToString());
            }
            return blacklistedParts;
        }

        // Is called when this Addon is first loaded to initializes all values (eg registration of event-handlers and creation
        // of original-stats library).
        public void Awake()
        {
            try
            {
                // Create a list of all valid fuel resources:
                if (KRnD.fuelResources == null)
                {
                    KRnD.fuelResources = new List<string>();
                    KRnD.fuelResources.Add("MonoPropellant"); // Always use MonoPropellant as fuel (RCS-Thrusters don't have engine modules and are not found with the code below)

                    foreach (AvailablePart aPart in PartLoader.LoadedPartsList)
                    {
                        Part part = aPart.partPrefab;
                        List<ModuleEngines> engineModules = KRnD.getEngineModules(part);
                        if (engineModules == null) continue;
                        foreach (ModuleEngines engineModule in engineModules)
                        {
                            if (engineModule.propellants == null) continue;
                            foreach (Propellant propellant in engineModule.propellants)
                            {
                                if (propellant.name == "ElectricCharge") continue; // Electric Charge is improved by batteries.
                                if (propellant.name == "IntakeAir") continue; // This is no real fuel-type.
                                if (!fuelResources.Contains(propellant.name)) fuelResources.Add(propellant.name);
                            }
                        }
                    }

                    String listString = "";
                    foreach (String fuelName in KRnD.fuelResources)
                    {
                        if (listString != "") listString += ", ";
                        listString += fuelName;
                    }
                    Debug.Log("[KRnD] found " + KRnD.fuelResources.Count.ToString() + " propellants: " + listString);
                }

                // Create a list of blacklisted parts (parts with known incompatible modules of other mods):
                if (KRnD.blacklistedParts == null)
                {
                    KRnD.blacklistedParts = getBlacklistedParts();
                    List<string> blacklistedModules = getBlacklistedModules();

                    foreach (AvailablePart aPart in PartLoader.LoadedPartsList)
                    {
                        Part part = aPart.partPrefab;
                        Boolean skip = false;
                        string blacklistedModule = "N/A";

                        foreach (PartModule partModule in part.Modules)
                        {
                            if (blacklistedModules.Contains(partModule.moduleName))
                            {
                                blacklistedModule = partModule.moduleName;
                                skip = true;
                                break;
                            }
                        }
                        if (skip)
                        {
                            Debug.Log("[KRnD] blacklisting part '" + part.name.ToString() + "' (has blacklisted module '" + blacklistedModule.ToString() + "')");
                            if (!KRnD.blacklistedParts.Contains(part.name)) KRnD.blacklistedParts.Add(part.name);
                            continue;
                        }
                    }

                    Debug.Log("[KRnD] blacklisted " + KRnD.blacklistedParts.Count.ToString() + " parts, which contained one of " + blacklistedModules.Count.ToString() + " blacklisted modules");
                }

                // Create a backup of all unmodified parts before we update them. We will later use these backup-parts
                // for all calculations of upgraded stats.
                if (KRnD.originalStats == null)
                {
                    KRnD.originalStats = new Dictionary<string, PartStats>();
                    foreach (AvailablePart aPart in PartLoader.LoadedPartsList)
                    {
                        Part part = aPart.partPrefab;

                        // Backup this part, if it has the RnD-Module:
                        if (KRnD.getKRnDModule(part) != null)
                        {
                            PartStats duplicate;
                            if (originalStats.TryGetValue(part.name, out duplicate))
                            {
                                Debug.LogError("[KRnD] Awake(): duplicate part-name: " + part.name.ToString());
                            }
                            else
                            {
                                originalStats.Add(part.name, new PartStats(part));
                            }
                        }
                    }
                }

                // Execute the following code only once:
                if (KRnDT.initialized) return;
                DontDestroyOnLoad(this);

                // Register event-handlers:
                GameEvents.onVesselChange.Add(this.OnVesselChange);
                GameEvents.onEditorPartEvent.Add(this.EditorPartEvent);

                KRnDT.initialized = true;
            }
            catch (Exception e)
            {
                Debug.LogError("[KRnD] Awake(): " + e.ToString());
            }
        }
    }

    // This class handels load- and save-operations.
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.TRACKSTATION, GameScenes.SPACECENTER)]
    class KRnDScenarioModuleT : ScenarioModule
    {
        public override void OnSave(ConfigNode node)
        {
            try
            {
                double time = DateTime.Now.Ticks;
                ConfigNode upgradeNodes = new ConfigNode("upgrades");
                foreach (string upgradeName in KRnD.upgrades.Keys)
                {
                    KRnDUpgrade upgrade;
                    if (!KRnD.upgrades.TryGetValue(upgradeName, out upgrade)) continue;
                    upgradeNodes.AddNode(upgrade.createConfigNode(upgradeName));
                    Debug.Log("[KRnD] saved: " + upgradeName + " " + upgrade.ToString());
                }
                node.AddNode(upgradeNodes);

                time = (DateTime.Now.Ticks - time) / TimeSpan.TicksPerSecond;
                Debug.Log("[KRnD] saved " + upgradeNodes.CountNodes.ToString() + " upgrades in " + time.ToString("0.000s"));

                ConfigNode guiSettings = new ConfigNode("gui");
                guiSettings.AddValue("left", KRnDGUI.windowPosition.xMin);
                guiSettings.AddValue("top", KRnDGUI.windowPosition.yMin);
                node.AddNode(guiSettings);
            }
            catch (Exception e)
            {
                Debug.LogError("[KRnD] OnSave(): " + e.ToString());
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            try
            {
                double time = DateTime.Now.Ticks;
                int upgradesApplied = 0;

                KRnD.upgrades.Clear();

                ConfigNode upgradeNodes = node.GetNode("upgrades");
                if (upgradeNodes != null)
                {
                    foreach (ConfigNode upgradeNode in upgradeNodes.GetNodes())
                    {
                        KRnDUpgrade upgrade = KRnDUpgrade.createFromConfigNode(upgradeNode);
                        KRnD.upgrades.Add(upgradeNode.name, upgrade);
                    }

                    // Update global part-list with new upgrades from the savegame:
                    upgradesApplied = KRnD.updateGlobalParts();

                    // If we started with an active vessel, update that vessel:
                    Vessel vessel = FlightGlobals.ActiveVessel;
                    if (vessel)
                    {
                        KRnD.updateVessel(vessel);
                    }

                    time = (DateTime.Now.Ticks - time) / TimeSpan.TicksPerSecond;
                    Debug.Log("[KRnD] retrieved and applied " + upgradesApplied.ToString() + " upgrades in " + time.ToString("0.000s"));
                }

                ConfigNode guiSettings = node.GetNode("gui");
                if (guiSettings != null)
                {
                    if (guiSettings.HasValue("left")) KRnDGUI.windowPosition.xMin = (float) Double.Parse(guiSettings.GetValue("left"));
                    if (guiSettings.HasValue("top")) KRnDGUI.windowPosition.yMin = (float) Double.Parse(guiSettings.GetValue("top"));
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[KRnD] OnLoad(): " + e.ToString());
            }
        }
    }

}
