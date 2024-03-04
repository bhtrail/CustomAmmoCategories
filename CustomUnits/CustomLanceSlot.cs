﻿/*  
 *  This file is part of CustomUnits.
 *  CustomUnits is free software: you can redistribute it and/or modify it under the terms of the 
 *  GNU Lesser General Public License as published by the Free Software Foundation, either version 3 of the License, 
 *  or (at your option) any later version. CustomAmmoCategories is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. 
 *  See the GNU Lesser General Public License for more details.
 *  You should have received a copy of the GNU Lesser General Public License along with CustomAmmoCategories. 
 *  If not, see <https://www.gnu.org/licenses/>. 
*/
using BattleTech;
using BattleTech.Data;
using BattleTech.Framework;
using BattleTech.UI;
using BattleTech.UI.Tooltips;
using CustAmmoCategories;
using HarmonyLib;
using HBS;
using IRBTModUtils;
using SVGImporter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CustomUnits {
  public static class CustomLanceSlotHelper {
    public static string GetSlotGUID(this LanceLoadoutSlot lanceLoadoutSlot) {
      return lanceLoadoutSlot.SelectedMech.MechDef.GUID + "_" + lanceLoadoutSlot.SelectedMech.MechDef.Description.Id + "_" + lanceLoadoutSlot.SelectedPilot.Pilot.Description.Id;
    }
    public static string GetSlotGUID(this SpawnableUnit slot) {
      string result = string.Empty;
      if (slot.Unit != null) { result = slot.Unit.GUID; } else
      if (slot.VUnit != null) { result = slot.VUnit.GUID; } else
      if (slot.TUnit != null) { result = slot.TUnit.GUID; }
      return result + "_" + slot.UnitId + "_" + slot.PilotId;
    }
    public static string GetSlotGUID(this UnitSpawnPointGameLogic slot) {
      string result = string.Empty;
      if (slot.mechDefOverride != null) { result = slot.mechDefOverride.GUID; } else
      if (slot.vehicleDefOverride != null) { result = slot.vehicleDefOverride.GUID; } else
      if (slot.turretDefOverride != null) { result = slot.turretDefOverride.GUID; }
      return result + "_" + slot.mechDefId + "_" + slot.pilotDefId;
    }
  }
  [HarmonyPatch(typeof(CombatHUDActionButton))]
  [HarmonyPatch("ExecuteClick")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { })]
  public static class CombatHUDActionButton_ExecuteClick {
    public static void Prefix(ref bool __runOriginal, CombatHUDActionButton __instance) {
      try {
        if (!__runOriginal) { return; }
        Log.Combat?.WL(0,"CombatHUDActionButton.ExecuteClick '" + __instance.GUID + "'/'" + CombatHUD.ButtonID_Move + "' " + (__instance.GUID == CombatHUD.ButtonID_Move));
        CombatHUD HUD = __instance.HUD;
        bool modifyers = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
        if (modifyers) {
          Log.Combat?.WL(1, "button GUID:" + __instance.GUID);
          if (__instance.Ability != null) {
            Log.Combat?.WL(1, "button ability:" + __instance.Ability.Def.Description.Id);
          } else {
            Log.Combat?.WL(1, "button ability:null");
          }
          SelectionType selectionType = __instance.SelectionType;
          Log.Combat?.WL(1, "selection type:" + selectionType);
          if (__instance.GUID == "ID_ATTACKGROUND") {
            List<Vector3> pointWithinRadius = HUD.Combat.HexGrid.GetGridPointsAroundPointWithinRadius(HUD.SelectedActor.CurrentPosition, 1);
            Log.Combat?.TWL(0, "SpawnHOTDROP:" + HUD.SelectedActor.CurrentPosition);
            for (int t = 0; t < pointWithinRadius.Count; ++t) { pointWithinRadius[t] = new Vector3(pointWithinRadius[t].x, HUD.Combat.MapMetaData.GetLerpedHeightAt(pointWithinRadius[t]), pointWithinRadius[t].z); }
            foreach (Vector3 pos in pointWithinRadius) { Log.Combat?.WL(1, pos.ToString()); }
            GenericPopupBuilder.Create("DEBUG HOTDROP SPAWN", "DEBUG HOTDROP SPAWN")
            .AddButton("OK", (Action)(() => {
              CustomLanceHelper.HotDrop(pointWithinRadius, HUD.SelectedActor.GUID);
            }), true)
            .AddButton("CANCEL", (Action)(() => {
            }), true)
            .AddFader(new UIColorRef?(LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.PopupBackfill), 0.0f, true).Render();

          }
          return;
        }
        return;
      } catch (Exception e) {
        Log.Combat?.TWL(0, e.ToString());
        UIManager.logger.LogException(e);
        return;
      }
    }
  }

  [HarmonyPatch(typeof(LanceLoadoutSlot))]
  [HarmonyPatch("SetData")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { typeof(LanceConfiguratorPanel), typeof(SimGameState), typeof(DataManager), typeof(bool), typeof(bool), typeof(float), typeof(float) })]
  public static class LanceLoadoutSlot_SetData {
    public static void Prefix(LanceLoadoutSlot __instance, LanceConfiguratorPanel LC, SimGameState sim, DataManager dataManager, bool useDragAndDrop, ref bool locked, ref float minTonnage, ref float maxTonnage) {
      Log.M?.TWL(0, "LanceLoadoutSlot.SetData " + __instance.GetInstanceID());
      try {
        if (maxTonnage == 0f) { return; }
        if (locked == true) { return; }
        CustomLanceSlot custSlot = __instance.gameObject.GetComponent<CustomLanceSlot>();
        if (custSlot == null) { return; }
        if (custSlot.lanceIndex < 0) { locked = true; return; }
        if (custSlot.lanceDef == null) { locked = true; return; }
        if (custSlot.slotDef == null) { locked = true; return; }
        if (custSlot.slotDef.Disabled) { locked = true; return; }
      } catch (Exception e) {
        Log.M?.TWL(0, e.ToString(), true);
        UIManager.logger.LogException(e);
      }
    }
  }

  [HarmonyPatch(typeof(LanceConfiguratorPanel), "SetData")]

  [HarmonyPatch(typeof(LanceLoadoutSlot))]
  [HarmonyPatch("SetLockedData")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { typeof(IMechLabDraggableItem), typeof(IMechLabDraggableItem), typeof(bool) })]
  public static class LanceLoadoutSlot_SetLockedData {
    public static void Prefix(LanceLoadoutSlot __instance, IMechLabDraggableItem forcedMech, IMechLabDraggableItem forcedPilot, bool shouldBeLocked) {
      if (shouldBeLocked) { Thread.CurrentThread.SetFlag("LanceLoadoutSlot.LOCKED"); }
    }
    public static void Postfix(LanceLoadoutSlot __instance, IMechLabDraggableItem forcedMech, IMechLabDraggableItem forcedPilot, bool shouldBeLocked) {
      if (shouldBeLocked) { Thread.CurrentThread.ClearFlag("LanceLoadoutSlot.LOCKED"); }
    }
  }
  [HarmonyPatch(typeof(SimGameState))]
  [HarmonyPatch("SaveLastLance")]
  [HarmonyPatch(MethodType.Normal)]
  public static class SimGameState_SaveLastLance {
    public static void Prefix(ref bool __runOriginal, SimGameState __instance, LanceConfiguration config) {
      try {
        if (!__runOriginal) { return; }
        __instance.LastUsedMechs = new List<string>();
        __instance.LastUsedPilots = new List<string>();
        Log.M?.TWL(0, "SimGameState.SaveLastLance");
        if (config == null) {
          Log.M?.WL(0, "no config - no to save");
          __runOriginal = false;
          return;
        }
        for (int t = 0; t < __instance.currentLayout().slotsCount; ++t) {
          __instance.LastUsedMechs.Add(string.Empty);
          __instance.LastUsedPilots.Add(string.Empty);
        }
        foreach (SpawnableUnit lanceUnit in config.GetLanceUnits("bf40fd39-ccf9-47c4-94a6-061809681140")) {
          SpawnableUnit spawnableUnit = lanceUnit;
          if (spawnableUnit != null) {
            if (spawnableUnit.Unit == null) { continue; }
            if (spawnableUnit.Pilot == null) { continue; }
            string GUID = spawnableUnit.Unit.GUID + "_" + spawnableUnit.Unit.Description.Id + "_" + spawnableUnit.Pilot.Description.Id;
            if (CustomLanceHelper.playerLanceLoadout.loadout.TryGetValue(GUID, out int index)) {
              __instance.LastUsedPilots[index] = spawnableUnit.Pilot.Description.Id;
              __instance.LastUsedMechs[index] = spawnableUnit.Unit.GUID;
            }
          }
        }
        foreach (SpawnableUnit lanceUnit in config.GetLanceUnits("HOTDROP_bf40fd39-ccf9-47c4-94a6-061809681140")) {
          SpawnableUnit spawnableUnit = lanceUnit;
          if (spawnableUnit != null) {
            if (spawnableUnit.Unit == null) { continue; }
            if (spawnableUnit.Pilot == null) { continue; }
            string GUID = spawnableUnit.Unit.GUID + "_" + spawnableUnit.Unit.Description.Id + "_" + spawnableUnit.Pilot.Description.Id;
            if (CustomLanceHelper.playerLanceLoadout.loadout.TryGetValue(GUID, out int index)) {
              __instance.LastUsedPilots[index] = spawnableUnit.Pilot.Description.Id;
              __instance.LastUsedMechs[index] = spawnableUnit.Unit.GUID;
            }
          }
        }
        foreach (SpawnableUnit lanceUnit in config.GetLanceUnits(Core.Settings.EMPLOYER_LANCE_GUID)) {
          SpawnableUnit spawnableUnit = lanceUnit;
          if (spawnableUnit != null) {
            if (spawnableUnit.Unit == null) { continue; }
            if (spawnableUnit.Pilot == null) { continue; }
            string GUID = spawnableUnit.Unit.GUID + "_" + spawnableUnit.Unit.Description.Id + "_" + spawnableUnit.Pilot.Description.Id;
            if (CustomLanceHelper.playerLanceLoadout.loadout.TryGetValue(GUID, out int index)) {
              __instance.LastUsedPilots[index] = spawnableUnit.Pilot.Description.Id;
              __instance.LastUsedMechs[index] = spawnableUnit.Unit.GUID;
            }
          }
        }
        foreach (SpawnableUnit lanceUnit in config.GetLanceUnits("HOTDROP_" + Core.Settings.EMPLOYER_LANCE_GUID)) {
          SpawnableUnit spawnableUnit = lanceUnit;
          if (spawnableUnit != null) {
            if (spawnableUnit.Unit == null) { continue; }
            if (spawnableUnit.Pilot == null) { continue; }
            string GUID = spawnableUnit.Unit.GUID + "_" + spawnableUnit.Unit.Description.Id + "_" + spawnableUnit.Pilot.Description.Id;
            if (CustomLanceHelper.playerLanceLoadout.loadout.TryGetValue(GUID, out int index)) {
              __instance.LastUsedPilots[index] = spawnableUnit.Pilot.Description.Id;
              __instance.LastUsedMechs[index] = spawnableUnit.Unit.GUID;
            }
          }
        }
        Log.M?.WL(1, "pilots:");
        for (int i = 0; i < __instance.LastUsedPilots.Count; ++i) {
          Log.M?.WL(2, "[" + i + "] " + __instance.LastUsedPilots[i]);
        }
        Log.M?.WL(1, "units:");
        for (int i = 0; i < __instance.LastUsedMechs.Count; ++i) {
          Log.M?.WL(2, "[" + i + "] " + __instance.LastUsedMechs[i]);
        }
      } catch (Exception e) {
        Log.E?.TWL(0, e.ToString(), true);
        SimGameState.logger.LogException(e);
      }
      __runOriginal = false;
      return;
    }
  }
  [HarmonyPatch(typeof(LanceConfiguratorPanel))]
  [HarmonyPatch("LoadOverrideLance")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { typeof(LanceConfiguration) })]
  public static class LanceConfiguratorPanel_LoadOverrideLance {
    private static void Sanitize(this LanceConfiguratorPanel __instance, SpawnableUnit[] includeEmptySlots) {
      Log.M?.TWL(0, "LanceConfiguratorPanel.LoadOverrideLance.Sanitize");
      Log.M?.WL(1, $"inventory:{__instance.mechListWidget.inventory.Count}");
      foreach(var unit in __instance.mechListWidget.inventory) {
        if (unit.MechDef == null) { continue; }
        Log.M?.WL(2,$"mech:{unit.MechDef.Description.Id} chassis:{unit.MechDef.Chassis.Description.Id} guid:{unit.MechDef.GUID}");
      }
      //for (int i = 0; i < includeEmptySlots.Length; ++i) {
      //  Log.M?.WL(1, $"slot[{i}] Unit.GUID:{(includeEmptySlots[i].Unit == null ? "null" : (includeEmptySlots[i].Unit.GUID))} UnitId:{includeEmptySlots[i].UnitId}");
      //  if(includeEmptySlots[i].Unit)
      //}
    }
    public static void Prefix(ref bool __runOriginal, LanceConfiguratorPanel __instance, LanceConfiguration lance) {
      if (!__runOriginal) { return; }
      try {
        __instance.mechwarriorLoadNotification.SetActive(true);
        __instance.mechwarriorLoadNotification.SetActive(false);
        Log.M?.TWL(0, "LanceConfiguratorPanel.LoadOverrideLance");
        SpawnableUnit[] includeEmptySlots = lance.GetLanceUnitsIncludeEmptySlots();
        __instance.Sanitize(includeEmptySlots);
        for (int index = 0; index < includeEmptySlots.Length; ++index) {
          Log.M?.WL(1, $"slot[{index}] Unit.GUID:{(includeEmptySlots[index].Unit==null?"null": (includeEmptySlots[index].Unit.GUID))} UnitId:{includeEmptySlots[index].UnitId}");
          SpawnableUnit unit = includeEmptySlots[index];
          if (unit.unitType != UnitType.UNDEFINED && index < __instance.loadoutSlots.Length) {
            LanceLoadoutSlot loadoutSlot = __instance.loadoutSlots[index];
            IMechLabDraggableItem labDraggableItem = null;
            if (includeEmptySlots[index].Unit != null) 
              labDraggableItem = __instance.mechListWidget.GetMechDefByGUID(includeEmptySlots[index].Unit.GUID);
            if (labDraggableItem == null) {
              Log.M?.WL(2, "unit is not found by GUID. Searching by UnitId");
              labDraggableItem = __instance.mechListWidget.GetInventoryItem(includeEmptySlots[index].UnitId);
            } else {
              Log.M?.WL(2, "unit is found by GUID.");
            }
            if (labDraggableItem == null && unit.Unit != null) {
              Log.M?.WL(2, "new lance item");
              labDraggableItem = __instance.mechListWidget.CreateLanceItem(new MechDef(unit.Unit, __instance.Sim.GenerateSimGameUID()));
            }
            IMechLabDraggableItem pilot = __instance.pilotListWidget.GetPilot(unit.PilotId);
            if (pilot == null && unit.Pilot != null) {
              __instance.pilotListWidget.AddPilot(new Pilot((PilotDef)null, string.Format("Override_{0}", (object)unit.PilotId), false), null, false);
              pilot = __instance.pilotListWidget.GetPilot(unit.PilotId);
              __instance.pilotListWidget.RemovePilot(__instance.availablePilots.Find((Predicate<Pilot>)(x => x.Description.Id == unit.PilotId)));
            }
            IMechLabDraggableItem forcedMech = labDraggableItem;
            IMechLabDraggableItem forcedPilot = pilot;
            loadoutSlot.SetLockedData(forcedMech, forcedPilot, true);
          }
        }
        __runOriginal = false;
      } catch (Exception e) {
        Log.E?.TWL(0, e.ToString(), true);
      }
    }
  }
  [HarmonyPatch(typeof(MechBayMechStorageWidget))]
  [HarmonyPatch("InitInventory")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { typeof(List<MechDef>), typeof(bool) })]
  public static class MechBayMechStorageWidget_InitInventory {
    public static void Prefix(ref bool __runOriginal, MechBayMechStorageWidget __instance, List<MechDef> mechDefs, bool resetFilters) {
      if (!__runOriginal) { return; }
      try {
        Log.M?.TWL(0,$"MechBayMechStorageWidget.InitInventory:{mechDefs.Count}");
        foreach(var mech in mechDefs) {
          Log.M?.WL(1, $"{mech.Description.Id}:{mech.Chassis.Description.Id} guid:{mech.GUID}");
        }
        Log.M?.WL(0, Environment.StackTrace);
      } catch (Exception e) {
        Log.E?.TWL(0, e.ToString(), true);
      }
    }
  }
  //[HarmonyPatch(typeof(SimGameState))]
  //[HarmonyPatch("CompleteLanceConfigurationPrep")]
  //[HarmonyPatch(MethodType.Normal)]
  //[HarmonyPatch(new Type[] { typeof(LoadRequest) })]
  //[HarmonyAfter("co.uk.cwolf.MissionControl")]
  //public static class SimGameState_CompleteLanceConfigurationPrep {
  //  public static void Prefix(ref bool __runOriginal, SimGameState __instance, LoadRequest request) {
  //    if (!__runOriginal) { return; }
  //    __runOriginal = false;
  //    try {
  //      Log.M?.TWL(0, $"SimGameState.CompleteLanceConfigurationPrep");
  //    } catch (Exception e) {
  //      Log.E?.TWL(0, e.ToString(), true);
  //    }
  //  }
  //}
  [HarmonyPatch(typeof(LanceLoadoutSlot))]
  [HarmonyPatch("OnAddItem")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { typeof(IMechLabDraggableItem), typeof(bool) })]
  public static class LanceLoadoutSlot_OnAddItem {
    public static void Prefix(ref bool __runOriginal, LanceLoadoutSlot __instance, IMechLabDraggableItem item, bool validate, bool __result) {
      try {
        int slotIndex = -1;
        LanceLoadoutSlot[] loadoutSlots = null;
        if (__instance.LC != null) {
          loadoutSlots = __instance.LC.loadoutSlots;
          for (int i = 0; i < loadoutSlots.Length; ++i) { if (loadoutSlots[i] == __instance) { slotIndex = i; break; } }
        }
        Log.M?.TW(0, $"LanceLoadoutSlot.OnAddItem slot:{slotIndex} item:{item.ItemType}");
        if (item.ItemType == MechLabDraggableItemType.Mech) {
          LanceLoadoutMechItem lanceLoadoutMechItem = item as LanceLoadoutMechItem;
          if ((loadoutSlots != null)&&(string.IsNullOrEmpty(lanceLoadoutMechItem.MechDef.GUID) == false)) {
            Log.M?.WL(1, $"{lanceLoadoutMechItem.MechDef.ChassisID}({lanceLoadoutMechItem.MechDef.GUID})");
            for (int i = 0; i < loadoutSlots.Length; ++i) {
              if (slotIndex == i) { continue; }
              if (loadoutSlots[i].SelectedMech == null) { continue; }
              if (loadoutSlots[i].SelectedMech.MechDef.GUID == lanceLoadoutMechItem.MechDef.GUID) {
                Log.M?.WL(1, $"Duplicate detected in slot {i}");
                Log.M?.WL(1, Environment.StackTrace);
                __result = true;
                __runOriginal = false;
                return;
              }
            }
          }
        } else if (item.ItemType == MechLabDraggableItemType.Pilot) {
          SGBarracksRosterSlot barracksRosterSlot = item as SGBarracksRosterSlot;
          Log.M?.WL(1, $"{barracksRosterSlot.Pilot.Description.Id}({barracksRosterSlot.Pilot.Callsign})");
        }
        try {
          if (Thread.CurrentThread.isFlagSet("LanceLoadoutSlot.LOCKED")) { return; };
          if ((item.ItemType != MechLabDraggableItemType.Mech) && (item.ItemType != MechLabDraggableItemType.Pilot)) { return; }
          if (item.ItemType == MechLabDraggableItemType.Mech) {
            LanceLoadoutMechItem lanceLoadoutMechItem = item as LanceLoadoutMechItem;
            if (lanceLoadoutMechItem.MechDef.Chassis == null) {
              throw new Exception("lanceLoadoutMechItem.MechDef.Chassis is null");
            }
            CustomLanceSlot customSlot = __instance.gameObject.GetComponent<CustomLanceSlot>();
            if (customSlot != null) {
              if (lanceLoadoutMechItem.MechDef.Chassis.CanBeDropedInto(customSlot.slotDef, out string title, out string message) == false) {
                if (__instance.LC != null) { __instance.LC.ReturnItem(item); }
                __result = false;
                GenericPopupBuilder.Create(title, message).AddFader(new UIColorRef?(LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.PopupBackfill), 0.0f, true).Render();
                __runOriginal = false;
                return;
              }
            }
            if (__instance.SelectedPilot != null) {
              if (lanceLoadoutMechItem.MechDef.Chassis.CanBePilotedBy(__instance.SelectedPilot.Pilot.pilotDef, out string title, out string message) == false) {
                if (__instance.LC != null) { __instance.LC.ReturnItem(item); }
                __result = false;
                GenericPopupBuilder.Create(title, message).AddFader(new UIColorRef?(LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.PopupBackfill), 0.0f, true).Render();
                __runOriginal = false;
                return;
              }
            }
          } else if (item.ItemType == MechLabDraggableItemType.Pilot) {
            SGBarracksRosterSlot barracksRosterSlot = item as SGBarracksRosterSlot;
            if (__instance.SelectedMech != null) {
              if (__instance.SelectedMech.MechDef.Chassis == null) {
                throw new Exception("__instance.SelectedMech.MechDef.Chassis is null");
              }
              if (__instance.SelectedMech.MechDef.Chassis.CanBePilotedBy(barracksRosterSlot.Pilot.pilotDef, out string title, out string message) == false) {
                if (__instance.LC != null) { __instance.LC.ReturnItem(item); }
                __result = false;
                GenericPopupBuilder.Create(title, message).AddFader(new UIColorRef?(LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.PopupBackfill), 0.0f, true).Render();
                __runOriginal = false;
                return;
              }
            }
          }
          return;
        } catch (Exception e) {
          Log.E?.TWL(0, e.ToString(), true);
          UIManager.logger.LogException(e);
          return;
        }
      } catch (Exception e) {
        Log.E?.TWL(0, e.ToString(), true);
        UIManager.logger.LogException(e);
        return;
      }
    }
  }
  [HarmonyPatch(typeof(LanceConfiguratorPanel))]
  [HarmonyPatch("CreateLanceConfiguration")]
  public static class LanceConfiguratorPanel_CreateLanceConfiguration {
    static void Prefix(ref bool __runOriginal,LanceConfiguratorPanel __instance, ref LanceConfiguration __result) {
      __runOriginal = false;
    }

    static void Postfix(LanceConfiguratorPanel __instance, ref LanceConfiguration __result) {
      try {
        Log.M?.TWL(0, "LanceConfiguratorPanel.CreateLanceConfiguration");
        for (int i = 0; i < __instance.loadoutSlots.Length; ++i) {
          Pilot pilot = null;
          MechDef mech = null;
          if (__instance.loadoutSlots[i].SelectedPilot != null) { pilot = __instance.loadoutSlots[i].SelectedPilot.Pilot; }
          if (__instance.loadoutSlots[i].SelectedMech != null) { mech = __instance.loadoutSlots[i].SelectedMech.MechDef; }
          Log.M?.WL(1, $"[{i}] state:{__instance.loadoutSlots[i].curLockState} pilot:{((pilot != null) ? (pilot.Description.Id + "(" + pilot.Callsign + ")") : "null")}  unit:{((mech != null) ? mech.ChassisID + "(" + mech.GUID + ")" : "null")}");
        }
        LanceConfiguration lanceConfiguration = new LanceConfiguration();
        CustomLanceHelper.playerLanceLoadout.loadout.Clear();
        CustomLanceHelper.hotdropLayout.Clear();
        for (int i = 0; i < __instance.loadoutSlots.Length; i++) {
          LanceLoadoutSlot lanceLoadoutSlot = __instance.loadoutSlots[i];
          CustomLanceSlot customSlot = lanceLoadoutSlot.gameObject.GetComponent<CustomLanceSlot>();
          if ((lanceLoadoutSlot.SelectedMech == null) && (lanceLoadoutSlot.SelectedPilot != null)) { continue; }
          if ((lanceLoadoutSlot.SelectedMech != null) && (lanceLoadoutSlot.SelectedPilot == null)) { continue; }
          if ((lanceLoadoutSlot.SelectedMech == null) && (lanceLoadoutSlot.SelectedPilot == null)) {
            lanceConfiguration.AddUnit(__instance.playerGUID, string.Empty, string.Empty, UnitType.UNDEFINED);
            continue;
          }
          bool isPlayer = true;
          bool hotdrop = false;
          if (customSlot != null) {
            if (customSlot.slotDef != null) {
              if (customSlot.slotDef.PlayerControl == false) { isPlayer = false; }
              if (customSlot.slotDef.HotDrop) { hotdrop = true; }
            }
          }
          string teamGUID = __instance.playerGUID;
          if (isPlayer == false) { teamGUID = Core.Settings.EMPLOYER_LANCE_GUID; };
          string GUID = lanceLoadoutSlot.GetSlotGUID();
          int index = 0;
          while (CustomLanceHelper.playerLanceLoadout.loadout.ContainsKey(GUID)) {
            ++index;
            lanceLoadoutSlot.SelectedMech.MechDef.SetGuid(lanceLoadoutSlot.SelectedMech.MechDef.GUID + "_" + index.ToString());
            GUID = lanceLoadoutSlot.GetSlotGUID();
          }
          CustomLanceHelper.playerLanceLoadout.loadout.Add(GUID, i);
          if (hotdrop) {
            CustomLanceHelper.hotdropLayout.Add(new HotDropDefinition(lanceLoadoutSlot.SelectedMech.MechDef, lanceLoadoutSlot.SelectedPilot.Pilot, teamGUID));
          }
          if (hotdrop) { teamGUID = "HOTDROP_" + teamGUID; }
          lanceConfiguration.AddUnit(teamGUID, lanceLoadoutSlot.SelectedMech.MechDef, lanceLoadoutSlot.SelectedPilot.Pilot.pilotDef);
          Log.M?.WL(1, teamGUID + " " + GUID + ":" + i);
        }
        __result = lanceConfiguration;
      } catch (Exception e) {
        Log.E?.TWL(0, e.ToString(), true);
        UIManager.logger.LogException(e);
      }
    }
  }
  [HarmonyPatch(typeof(LanceConfiguratorPanel))]
  [HarmonyPatch("SetData")]
  [HarmonyPatch(MethodType.Normal)]
  public static class LanceConfiguratorPanel_SetData {
    //private static FieldInfo f_loadoutSlots = typeof(LanceConfiguratorPanel).GetField("loadoutSlots", BindingFlags.Instance | BindingFlags.NonPublic);
    //public static LanceLoadoutSlot[] loadoutSlots(this LanceConfiguratorPanel panel) { return (LanceLoadoutSlot[])f_loadoutSlots.GetValue(panel); }
    public static void InitLanceSaveButtons(LanceConfiguratorPanel __instance, ShuffleLanceSlotsLayout customLanceSlotsLayout) {
      try {
        Transform buttons_tr = __instance.transform.FindRecursive("lanceSwitchButtons-layout");
        if (buttons_tr == null) {
          Log.M?.WL(1, "lanceSwitchButtons-layout not found");
          buttons_tr = __instance.transform.FindRecursive("lanceSaveButtons-layout");
          if (buttons_tr == null) {
            Log.M?.WL(1, "lanceSaveButtons-layout not found");
            return;
          };
          Transform DeployBttn_layout = __instance.transform.FindRecursive("DeployBttn-layout");
          Transform nbtn = GameObject.Instantiate(buttons_tr.gameObject).transform;
          nbtn.SetParent(buttons_tr.parent);
          nbtn.localPosition = buttons_tr.localPosition;
          nbtn.localScale = Vector3.one;
          if (DeployBttn_layout != null) {
            Vector3 pos = nbtn.localPosition;
            pos.y = DeployBttn_layout.localPosition.y;
            nbtn.localPosition = pos;
          }
          buttons_tr = nbtn;
          buttons_tr.gameObject.name = "lanceSwitchButtons-layout";
          buttons_tr.Find("uixPrfBttn_BASE_button2-MANAGED-delete").gameObject.AddComponent<LanceConfigutationNextLance>().Init(customLanceSlotsLayout);
          buttons_tr.Find("uixPrfBttn_BASE_button2-MANAGED-save").gameObject.AddComponent<LanceConfigSaver>().Init(__instance);
          buttons_tr.Find("uixPrfBttn_BASE_button2-MANAGED-save").gameObject.SetActive(true);
          buttons_tr.Find("uixPrfBttn_BASE_button2-MANAGED-copy (1)").gameObject.AddComponent<LanceConfigLoader>().Init(__instance);
          buttons_tr.Find("uixPrfBttn_BASE_button2-MANAGED-copy (1)").gameObject.SetActive(true);
        } else {
          buttons_tr.Find("uixPrfBttn_BASE_button2-MANAGED-delete").gameObject.GetComponent<LanceConfigutationNextLance>().Init(customLanceSlotsLayout);
          buttons_tr.Find("uixPrfBttn_BASE_button2-MANAGED-save").gameObject.GetComponent<LanceConfigSaver>().Init(__instance);
          buttons_tr.Find("uixPrfBttn_BASE_button2-MANAGED-copy (1)").gameObject.GetComponent<LanceConfigLoader>().Init(__instance);
        }
        buttons_tr.gameObject.SetActive(true);
      } catch (Exception e) {
        Log.M?.TWL(0, e.ToString(), true);
        UIManager.logger.LogException(e);
      }
    }
    public static void InitDeploySelectButtons(LanceConfiguratorPanel __instance, Contract contract, SimGameState sim) {
      try {
        DeployManualHelper.IsInManualSpawnSequence = false;
        Transform buttons_tr = __instance.transform.FindRecursive("deploySelectButtons-layout");
        if (buttons_tr == null) {
          Log.M?.WL(1, "deploySelectButtons-layout not found");
          buttons_tr = __instance.transform.FindRecursive("lanceSaveButtons-layout");
          if (buttons_tr == null) {
            Log.M?.WL(1, "lanceSaveButtons-layout not found");
            return;
          };
          Transform DeployBttn_layout = __instance.transform.FindRecursive("DeployBttn-layout");
          Transform nbtn = GameObject.Instantiate(buttons_tr.gameObject).transform;
          nbtn.SetParent(buttons_tr.parent);
          nbtn.localPosition = buttons_tr.localPosition;
          nbtn.localScale = Vector3.one;
          if (DeployBttn_layout != null) {
            Vector3 pos = nbtn.localPosition;
            pos.y = DeployBttn_layout.localPosition.y;
            pos.x = 0f - nbtn.localPosition.x;
            nbtn.localPosition = pos;
          }
          buttons_tr = nbtn;
          buttons_tr.gameObject.name = "deploySelectButtons-layout";
          buttons_tr.Find("uixPrfBttn_BASE_button2-MANAGED-delete").gameObject.SetActive(false);
          buttons_tr.Find("uixPrfBttn_BASE_button2-MANAGED-save").gameObject.SetActive(true);
          SetDeploySwitchButton switchBtn = buttons_tr.Find("uixPrfBttn_BASE_button2-MANAGED-save").gameObject.AddComponent<SetDeploySwitchButton>();
          buttons_tr.Find("uixPrfBttn_BASE_button2-MANAGED-copy (1)").gameObject.SetActive(false);
          //SetDeployManualButton manualBtn = buttons_tr.Find("uixPrfBttn_BASE_button2-MANAGED-copy (1)").gameObject.AddComponent<SetDeployManualButton>();
          buttons_tr.Find("uixPrfBttn_BASE_button2-MANAGED-new").gameObject.SetActive(false);
          if (contract != null) {
            switchBtn.Init(contract);
            //manualBtn.Init(contract, autoBtn);
          }
        } else {
          SetDeploySwitchButton switchBtn = buttons_tr.Find("uixPrfBttn_BASE_button2-MANAGED-save").gameObject.GetComponent<SetDeploySwitchButton>();
          //SetDeployManualButton manualBtn = buttons_tr.Find("uixPrfBttn_BASE_button2-MANAGED-copy (1)").gameObject.GetComponent<SetDeployManualButton>();
          if (contract != null) {
            switchBtn.Init(contract);
            //manualBtn.Init(contract, autoBtn);
          }
          //buttons_tr.Find("uixPrfBttn_BASE_button2-MANAGED-delete").gameObject.GetComponent<LanceConfigutationNextLance>().Init(customLanceSlotsLayout);
          //buttons_tr.Find("uixPrfBttn_BASE_button2-MANAGED-save").gameObject.GetComponent<LanceConfigSaver>().Init(__instance);
          //buttons_tr.Find("uixPrfBttn_BASE_button2-MANAGED-copy (1)").gameObject.GetComponent<LanceConfigLoader>().Init(__instance);
        }
        buttons_tr.gameObject.SetActive((sim != null) && (contract != null));
      } catch (Exception e) {
        Log.M?.TWL(0, e.ToString(), true);
        UIManager.logger.LogException(e);
      }
    }
    internal class SanitizeUnitData {
      public List<SanitizeLanceConfigData> lanceData = new List<SanitizeLanceConfigData>();
      public List<int> indexes = new List<int>();
    }
    internal class SanitizeLanceConfigData {
      public string lanceid;
      public int index;
      public SanitizeLanceConfigData(string l, int i) { this.lanceid = l; this.index = i; }
    }
    public static void SanitizeInventoryAndConfig(this LanceConfiguratorPanel __instance, SimGameState sim, List<MechDef> mechs, LanceConfiguration overrideLance) {
      Dictionary<MechDef, SanitizeUnitData> mechdefData = new Dictionary<MechDef, SanitizeUnitData>();
      Log.M?.TWL(0, "LanceConfiguratorPanel.SanitizeInventoryAndConfig");
      try {
        for (int t = 0; t < mechs.Count; ++t) {
          if (mechdefData.TryGetValue(mechs[t], out var data) == false) {
            data = new SanitizeUnitData();
            mechdefData.Add(mechs[t], data);
          }
          data.indexes.Add(t);
        }
        if ((overrideLance != null) && (overrideLance.lanceConfigurations != null)) {
          foreach (var lance in overrideLance.lanceConfigurations) {
            for (int t = 0; t < lance.Value.Count; ++t) {
              if (lance.Value[t].Unit == null) { continue; }
              if (mechdefData.TryGetValue(lance.Value[t].Unit, out var data) == false) {
                data = new SanitizeUnitData();
                mechdefData.Add(mechs[t], data);
              }
              data.lanceData.Add(new SanitizeLanceConfigData(lance.Key, t));
            }
          }
        }
        Log.M?.WL(1, $"data:{mechdefData.Count}");
        foreach (var data in mechdefData) {
          if ((data.Value.indexes.Count > 1) || (data.Value.lanceData.Count > 1)) {
            Log.M?.WL(2, $"duplicate found:{data.Key.Description.Id}:{data.Key.Chassis.Description.Id} guid:{data.Key.GUID}");
            int count = Mathf.Max(data.Value.indexes.Count, data.Value.lanceData.Count);
            for (int t = 1; t < count; ++t) {
              MechDef mech = new MechDef(data.Key, sim.GenerateSimGameUID());
              Log.M?.WL(3, $"creating new:{mech.Description.Id}:{mech.Chassis.Description.Id} guid:{mech.GUID}");
              if (t < data.Value.indexes.Count) {
                mechs[data.Value.indexes[t]] = mech;
                Log.M?.WL(4, $"inventory index:{data.Value.indexes[t]}");
              }
              if (t < data.Value.lanceData.Count) {
                overrideLance.lanceConfigurations[data.Value.lanceData[t].lanceid][data.Value.lanceData[t].index].Unit = mech;
                Log.M?.WL(4, $"lance:{data.Value.lanceData[t].lanceid} index:{data.Value.lanceData[t].index}");
              }
            }
            //Log.M?.WL(2, $"mech:{data.Key.Description.Id}:{data.Key.Chassis.Description.Id} guid:{data.Key.GUID}");
            //Log.M?.WL(3, $"inventory:{data.Value.indexes.Count}");
            //foreach (int index in data.Value.indexes) {
            //  Log.M?.WL(4, $"{index}");
            //}
            //Log.M?.WL(3, $"lance:{data.Value.lanceData.Count}");
            //foreach (var index in data.Value.lanceData) {
            //  Log.M?.WL(4, $"lance:{index.lanceid} index:{index.index}");
            //}
          }
        }
      }catch(Exception e) {
        Log.E?.TWL(0, e.ToString(), true);
        UIManager.logger.LogException(e);
      }
    }
    public static void Prefix(LanceConfiguratorPanel __instance, SimGameState sim, ref int maxUnits, Contract contract, List<MechDef> mechs, LanceConfiguration overrideLance) {
      try {
        Log.M?.TWL(0, "LanceConfiguratorPanel.SetData prefix");
        if(sim != null)__instance.SanitizeInventoryAndConfig(sim, mechs, overrideLance);
        ShuffleLanceSlotsLayout customLanceSlotsLayout = __instance.loadoutSlots[0].transform.parent.gameObject.GetComponent<ShuffleLanceSlotsLayout>();
        if (customLanceSlotsLayout == null) { customLanceSlotsLayout = __instance.loadoutSlots[0].transform.parent.gameObject.AddComponent<ShuffleLanceSlotsLayout>(); }
        customLanceSlotsLayout.currentLanceIndex = 0;
        customLanceSlotsLayout.LayoutDef = sim.currentLayout();
        InitLanceSaveButtons(__instance, customLanceSlotsLayout);
        InitDeploySelectButtons(__instance, contract, sim);
        Log.M?.WL(1, "current layout:" + customLanceSlotsLayout.LayoutDef.Description.Id);
        List<float> listMaxTonnages = __instance.slotMaxTonnages.ToList();
        List<float> listMinTonnages = __instance.slotMinTonnages.ToList();
        Log.M?.WL(0, "loadoutSlots:" + __instance.loadoutSlots.Length + "/" + UnityGameInstance.BattleTechGame.Simulation.currentLayout().slotsCount);
        List<LanceLoadoutSlot> slots = new List<LanceLoadoutSlot>();
        slots.AddRange(__instance.loadoutSlots);
        GameObject lanceSlotSrc = __instance.loadoutSlots[0].gameObject;
        for (int t = __instance.loadoutSlots.Length; t < UnityGameInstance.BattleTechGame.Simulation.currentLayout().slotsCount; ++t) {
          GameObject lanceSlotNew = GameObject.Instantiate(lanceSlotSrc);
          lanceSlotNew.transform.SetParent(lanceSlotSrc.transform.parent);
          lanceSlotNew.transform.localPosition = lanceSlotSrc.transform.localPosition;
          lanceSlotNew.transform.localScale = lanceSlotSrc.transform.localScale;
          lanceSlotNew.transform.localRotation = lanceSlotSrc.transform.localRotation;
          lanceSlotNew.name = "lanceSlot" + (t + 1).ToString();
          slots.Add(lanceSlotNew.GetComponent<LanceLoadoutSlot>());
          Log.M?.WL(0, lanceSlotNew.name + " parent:" + lanceSlotNew.transform.parent.name);
          listMaxTonnages.Add(-1f);
          listMinTonnages.Add(-1f);
        }
        for (int t = 0; t < slots.Count; ++t) {
          slots[t].gameObject.SetActive(false);
          GameObject slot = slots[t].gameObject;
          CustomLanceSlot custSlot = slot.GetComponent<CustomLanceSlot>();
          if (custSlot == null) { custSlot = slot.AddComponent<CustomLanceSlot>(); }
          slot.transform.SetSiblingIndex(t);
          custSlot.weight = t;
          custSlot.lanceIndex = -1;
          custSlot.lanceDef = null;
          custSlot.slotDef = null;
        }
        {
          int slot_index = 0;
          for (int lance_index = 0; lance_index < customLanceSlotsLayout.LayoutDef.dropLances.Count; ++lance_index) {
            DropLanceDef lance = customLanceSlotsLayout.LayoutDef.dropLances[lance_index];
            foreach (DropSlotDef dropslot in lance.dropSlots) {
              if (slot_index >= slots.Count) { break; }
              GameObject slot = slots[slot_index].gameObject;
              CustomLanceSlot custSlot = slot.GetComponent<CustomLanceSlot>();
              custSlot.lanceIndex = lance_index;
              custSlot.lanceDef = lance;
              custSlot.slotDef = dropslot;
              custSlot.Awake();
              custSlot.ApplyDecoration(true);
              ++slot_index;
            }
          }
        }
        maxUnits = sim.currentLayout().slotsCount;
        Log.M?.WL(1, "loadoutSlots:" + slots.Count + "/" + maxUnits);
        customLanceSlotsLayout.Refresh();
        OrderLanceSlotLayoutGroup horizontalLayout = slots[0].transform.parent.gameObject.GetComponent<OrderLanceSlotLayoutGroup>();
        if (horizontalLayout == null) {
          HorizontalLayoutGroup old_horizontalLayout = slots[0].transform.parent.gameObject.GetComponent<HorizontalLayoutGroup>();
          RectOffset padding = old_horizontalLayout.padding;
          float spacing = old_horizontalLayout.spacing;
          GameObject.DestroyImmediate(old_horizontalLayout);
          horizontalLayout = slots[0].transform.parent.gameObject.AddComponent<OrderLanceSlotLayoutGroup>();
          if (horizontalLayout == null) {
            throw new Exception("fail to add OrderedHorizontalLayoutGroup");
          } else {
            horizontalLayout.childAlignment = TextAnchor.MiddleLeft;
            horizontalLayout.childControlHeight = false;
            horizontalLayout.childControlWidth = false;
            horizontalLayout.childForceExpandHeight = false;
            horizontalLayout.childForceExpandWidth = true;
            horizontalLayout.padding = padding;
            horizontalLayout.spacing = spacing;
            horizontalLayout.enabled = true;
          }
        }
        __instance.loadoutSlots = slots.ToArray();
        for (int t = 0; t < listMaxTonnages.Count; ++t) { listMaxTonnages[t] = -1f; }
        for (int t = 0; t < listMinTonnages.Count; ++t) { listMinTonnages[t] = -1f; }
        __instance.slotMaxTonnages = listMaxTonnages.ToArray();
        __instance.slotMinTonnages = listMinTonnages.ToArray();
      } catch (Exception e) {
        Log.E?.TWL(0, e.ToString(), true);
        UIManager.logger.LogException(e);
      }
    }
    public static void Postfix(LanceConfiguratorPanel __instance, Contract contract) {
      Log.M?.TWL(0, "LanceConfiguratorPanel.SetData postfix:" + __instance.maxUnits + "/" + __instance.loadoutSlots.Length);
      try {
        ShuffleLanceSlotsLayout customLanceSlotsLayout = __instance.loadoutSlots[0].transform.parent.gameObject.GetComponent<ShuffleLanceSlotsLayout>();
        float lastValidMaxTonnage = -1f;
        for (var index = 3; index >= 0; index--) {
          if (__instance.slotMaxTonnages[index] >= 0f) {
            lastValidMaxTonnage = __instance.slotMaxTonnages[index];
            break;
          }
        }
        for (int i = 4; i < __instance.slotMaxTonnages.Length; i++) {
          __instance.slotMaxTonnages[i] = lastValidMaxTonnage;
        }
        for (int i = 4; i < __instance.loadoutSlots.Length; i++) {
          __instance.loadoutSlots[i].SetData(__instance, __instance.Sim, __instance.dataManager, true, i >= __instance.maxUnits || __instance.slotMaxTonnages[i] == 0f, __instance.slotMinTonnages[i], lastValidMaxTonnage);
        }
        customLanceSlotsLayout.Refresh();
        customLanceSlotsLayout.UpdateSlots();
      } catch (Exception e) {
        Log.E?.TWL(0, e.ToString(), true);
        UIManager.logger.LogException(e);
      }
      //updateSlots(customLanceSlotsLayout);
    }
  }
  public class OrderLanceSlotLayoutGroup : HorizontalLayoutGroup {
    protected virtual List<KeyValuePair<RectTransform, CustomLanceSlot>> orderChildren { get; set; } = new List<KeyValuePair<RectTransform, CustomLanceSlot>>();
    public override void CalculateLayoutInputHorizontal() {
      this.rectChildren.Clear();
      this.orderChildren.Clear();
      CustomLanceSlot[] elements = this.gameObject.GetComponentsInChildren<CustomLanceSlot>();
      foreach (CustomLanceSlot el in elements) {
        if (el.transform.parent != this.transform) { continue; }
        RectTransform child = el.transform as RectTransform;
        if (child != null) { this.orderChildren.Add(new KeyValuePair<RectTransform, CustomLanceSlot>(child, el)); }
      }
      orderChildren.Sort((x, y) => { return x.Value.index - y.Value.index; });
      foreach (var el in orderChildren) {
        rectChildren.Add(el.Key);
      }
      this.m_Tracker.Clear();
    }
  }
  public class ShuffleLanceSlotsLayout : MonoBehaviour {
    public OrderLanceSlotLayoutGroup slotsLayout { get; set; }
    public RectTransform rectTransform { get; set; }
    public List<CustomLanceSlot> slots { get; set; } = new List<CustomLanceSlot>();
    public CustomLanceSlot currentSlot { get; set; }
    public DropSlotsDef LayoutDef { get; set; }
    public int currentLanceIndex { get; set; } = 0;
    public void UpdateSlots() {
      Log.M?.TWL(0, "ShuffleLanceSlotsLayout.UpdateSlots:" + slots.Count);
      foreach (CustomLanceSlot slot in slots) {
        Log.M?.WL(1, "slot:" + slot.lanceIndex + "/" + this.currentLanceIndex);
        slot.gameObject.SetActive(slot.lanceIndex == this.currentLanceIndex);
      }
    }
    public void Awake() {
      rectTransform = this.gameObject.GetComponent<RectTransform>();
      this.Refresh();
    }
    public void Refresh() {
      this.slotsLayout = this.gameObject.GetComponent<OrderLanceSlotLayoutGroup>();
      CustomLanceSlot[] trs = this.gameObject.GetComponentsInChildren<CustomLanceSlot>(true);
      slots.Clear();
      foreach (CustomLanceSlot tr in trs) {
        if (tr.transform.parent != this.transform) { continue; }
        slots.Add(tr);
      }
      slots.Sort((x, y) => { return x.weight - y.weight; });
      for (int t = 0; t < slots.Count; ++t) {
        slots[t].index = t;
      }
    }
    public void Update() {
      if (slotsLayout == null) { slotsLayout = this.gameObject.GetComponent<OrderLanceSlotLayoutGroup>(); }
      foreach (CustomLanceSlot slot in slots) {
        slot.transform.localScale = slot == this.currentSlot ? Vector3.one : (new Vector3(0.8f, 0.8f, 1.0f));
      }
      if (slotsLayout == null) { return; }
      float view_width = this.rectTransform.sizeDelta.x - this.slotsLayout.padding.left - this.slotsLayout.padding.right;
      float components_width = 0f;
      int count = 0;
      foreach (CustomLanceSlot slot in slots) {
        if (slot.gameObject.activeInHierarchy == false) { continue; }
        count += 1;
        components_width += slot.rectTransform.sizeDelta.x;
      }
      if (count > 1) {
        slotsLayout.spacing = (view_width - components_width) / ((float)(count - 1));
      }
    }
  }
  public class CustomLanceSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    public int index { get; set; }
    public int weight { get; set; }
    public ShuffleLanceSlotsLayout shuffleLayout { get; set; }
    public RectTransform rectTransform { get; set; }
    public int lanceIndex { get; set; } = -1;
    public DropLanceDef lanceDef { get; set; } = null;
    private DropSlotDef f_slotDef = null;
    public DropSlotDef slotDef { get { return f_slotDef; } set { f_slotDef = value; } }
    public RectTransform f_decorationLayout { get; set; } = null;
    public RectTransform decorationLayout { get { return f_decorationLayout; } set { f_decorationLayout = value; } }
    public RectTransform firstDecoration { get; set; } = null;
    private bool decorationApplied = false;
    public void Update() {
      try {
        if (decorationApplied == false) {
          this.ApplyDecoration(true, false);
        }
        if (firstDecoration == null) { return; }
        Vector3[] decorationCorners = new Vector3[4]; firstDecoration.GetWorldCorners(decorationCorners);
        Vector3[] layoutCorners = new Vector3[4]; rectTransform.GetWorldCorners(layoutCorners);
        if (Mathf.Abs(decorationCorners[1].x - layoutCorners[1].x) > (this.rectTransform.sizeDelta.x / 2f)) {
          HorizontalLayoutGroup group = decorationLayout.gameObject.GetComponent<HorizontalLayoutGroup>();
          Log.M?.TWL(0, "CustomLanceSlot.Update " + this.gameObject.name + " delta:" + Mathf.Abs(decorationCorners[1].x - layoutCorners[1].x) + " border:" + (this.rectTransform.sizeDelta.x / 2f) + " aligin:" + (group == null ? "null" : group.childAlignment.ToString()));
          if (group != null) {
            group.childAlignment = group.childAlignment == TextAnchor.MiddleLeft ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
          }
        }
      } catch (Exception e) {
        Log.M?.TWL(0, e.ToString(), true);
        UIManager.logger.LogException(e);
      }
      //if (Mathf.Abs(firstDecoration.localPosition.x) > firstDecoration.sizeDelta.x * 2f) {
      //  HorizontalLayoutGroup group = decorationLayout.gameObject.GetComponent<HorizontalLayoutGroup>();
      //  if (group != null) {
      //    group.childAlignment = group.childAlignment == TextAnchor.MiddleLeft? TextAnchor.MiddleRight: TextAnchor.MiddleLeft;
      //  }
      //}
    }
    public void ApplyDecoration(bool loadDeps, bool async = true) {
      try {
        if (async) { decorationApplied = false; return; };
        if (decorationLayout == null) { return; }
        if (slotDef == null) { return; }
        Log.M?.TWL(0, "CustomLanceSlot.ApplyDecoration " + (decorationLayout == null ? "null" : decorationLayout.parent.name + "." + decorationLayout.name + ":" + decorationLayout.GetInstanceID()) + " def:" + (slotDef == null ? "null" : slotDef.Description.Id));
        int childCount = decorationLayout.childCount;
        int decorCount = slotDef.decorations.Count;
        //if (decorCount == 1) { decorCount = 2; }
        Log.M?.WL(1, "slotDef.decorations.Count:" + childCount + "->" + decorCount);
        for (int t = childCount; t < decorCount; ++t) {
          GameObject decorationGO = new GameObject("decoration" + t.ToString(), typeof(RectTransform));
          RectTransform tr = decorationGO.GetComponent<RectTransform>();
          tr.SetParent(decorationLayout);
          tr.localScale = Vector3.one;
          tr.localPosition = Vector3.zero;
          tr.localRotation = Quaternion.identity;
          tr.pivot = new Vector2(0.5f, 0.5f);
          tr.sizeDelta = new Vector2(decorationLayout.sizeDelta.y, decorationLayout.sizeDelta.y);
          SVGImage svg = decorationGO.AddComponent<SVGImage>();
          HBSTooltip tooltip = decorationGO.AddComponent<HBSTooltip>();
        }
        DataManager.InjectedDependencyLoadRequest dependencyLoad = null;
        if (loadDeps) {
          dependencyLoad = new DataManager.InjectedDependencyLoadRequest(UnityGameInstance.BattleTechGame.DataManager);
        }
        for (int t = 0; t < slotDef.decorations.Count; ++t) {
          GameObject decorationGO = decorationLayout.GetChild(t).gameObject;
          DropSlotDecorationDef decorationDef = slotDef.decorations[t];
          if (decorationDef.DataManager == null) { decorationDef.DataManager = UnityGameInstance.BattleTechGame.DataManager; }
          if (loadDeps) {
            if (decorationDef.DependenciesLoaded(10u)) {
              decorationGO.GetComponent<SVGImage>().vectorGraphics = decorationDef.Icon;
            } else {
              decorationDef.GatherDependencies(UnityGameInstance.BattleTechGame.DataManager, dependencyLoad, 10u);
            }
          } else {
            decorationGO.GetComponent<SVGImage>().vectorGraphics = decorationDef.Icon;
          }
          HBSTooltip tooltip = decorationGO.GetComponent<HBSTooltip>();
          if (tooltip == null) { tooltip = decorationGO.AddComponent<HBSTooltip>(); }
          tooltip.SetDefaultStateData(TooltipUtilities.GetStateDataFromObject(decorationDef.description));
          tooltip.enabled = true;
        }
        for (int t = 0; t < decorationLayout.childCount; ++t) {
          decorationLayout.GetChild(t).gameObject.SetActive(t < decorCount);
        }
        if (decorationLayout.childCount > 0) { firstDecoration = decorationLayout.GetChild(0) as RectTransform; }
        //if (slotDef.decorations.Count == 1) {
        //  decorationLayout.GetChild(slotDef.decorations.Count).gameObject.GetComponent<SVGImage>().vectorGraphics = null;
        //  HBSTooltip tooltip = decorationLayout.GetChild(slotDef.decorations.Count).gameObject.GetComponent<HBSTooltip>();
        //  if (tooltip == null) { tooltip = decorationLayout.GetChild(slotDef.decorations.Count).gameObject.AddComponent<HBSTooltip>(); }
        //  tooltip.enabled = false;
        //}
        HorizontalLayoutGroup group = decorationLayout.gameObject.GetComponent<HorizontalLayoutGroup>();
        if (group == null) {
          group = decorationLayout.gameObject.AddComponent<HorizontalLayoutGroup>();
          group.spacing = 8f;
          group.padding = new RectOffset(10, 10, 0, 0);
          group.childAlignment = TextAnchor.MiddleRight;
          group.childControlHeight = false;
          group.childControlWidth = false;
          group.childForceExpandHeight = false;
          group.childForceExpandWidth = false;
        }
        decorationApplied = true;
        if (loadDeps) {
          if (dependencyLoad.DependencyCount() > 0) {
            dependencyLoad.RegisterLoadCompleteCallback(new Action(this.ApplyDecoration));
            UnityGameInstance.BattleTechGame.DataManager.InjectDependencyLoader(dependencyLoad, 10U);
          }
        }
      } catch (Exception e) {
        Log.E?.TWL(0, e.ToString(), true);
        UIManager.logger.LogException(e);
      }
    }
    public void ApplyDecoration() {
      ApplyDecoration(false, false);
    }
    public void Awake() {
      Log.M?.TWL(0, "CustomLanceSlot.Awake " + this.gameObject.name);
      try {
        if (this.transform.parent == null) { return; }
        rectTransform = this.gameObject.GetComponent<RectTransform>();
        Transform mainBackground = this.transform.FindRecursive("mainBackground");
        Transform mainBorder = this.transform.FindRecursive("mainBorder");
        this.decorationLayout = this.transform.FindRecursive("decoration") as RectTransform;
        if ((mainBorder != null) && (mainBackground == null)) {
          mainBackground = GameObject.Instantiate(mainBorder.gameObject).transform;
          mainBackground.gameObject.name = "mainBackground";
          mainBackground.SetParent(mainBorder.parent);
          mainBackground.localPosition = mainBorder.localPosition;
          mainBackground.localRotation = mainBorder.localRotation;
          mainBackground.localScale = mainBorder.localScale;
          mainBackground.SetAsFirstSibling();
          Image img = mainBackground.GetComponent<Image>();
          img.enabled = true;
          img.color = Color.black;
        }
        if ((this.decorationLayout == null) && (mainBorder != null)) {
          RectTransform decorationLocal = GameObject.Instantiate(mainBorder.gameObject).transform as RectTransform;
          decorationLocal.name = "decoration";
          HashSet<Transform> trs = new HashSet<Transform>();
          for (int t = 0; t < decorationLocal.childCount; ++t) { trs.Add(decorationLocal.GetChild(t)); }
          foreach (Transform tr in trs) {
            if (tr.gameObject.GetComponent<HBSTooltip>() == null) {
              GameObject.Destroy(tr.gameObject);
            }
          }
          decorationLocal.SetParent(mainBorder.parent);
          decorationLocal.localPosition = mainBorder.localPosition;
          decorationLocal.localRotation = mainBorder.localRotation;
          decorationLocal.localScale = mainBorder.localScale;
          decorationLocal.pivot = new Vector2(0.5f, -1.0f);
          decorationLocal.sizeDelta = new Vector2(rectTransform.sizeDelta.x, rectTransform.sizeDelta.y - (mainBorder as RectTransform).sizeDelta.y);
          decorationLocal.anchoredPosition = new Vector2(0f, 0f - ((mainBorder as RectTransform).sizeDelta.y / 2.0f));
          Image img = decorationLocal.gameObject.GetComponent<Image>();
          img.enabled = true;
          img.color = Color.black;
          HorizontalLayoutGroup group = decorationLocal.gameObject.GetComponent<HorizontalLayoutGroup>();
          if (group == null) {
            group = decorationLocal.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 8f;
            group.padding = new RectOffset(10, 10, 0, 0);
            group.childAlignment = UnityEngine.TextAnchor.MiddleRight;
            group.childControlHeight = false;
            group.childControlWidth = false;
            group.childForceExpandHeight = false;
            group.childForceExpandWidth = false;
          }
          this.decorationLayout = decorationLocal;
        }
        this.shuffleLayout = this.transform.parent.gameObject.GetComponent<ShuffleLanceSlotsLayout>();
        this.transform.localScale = new Vector3(0.8f, 0.8f, 1.0f);
      } catch (Exception e) {
        Log.M?.TWL(0, e.ToString(), true);
        UIManager.logger.LogException(e);
      }
      //Log.WL(1, "decorationLayout.GetInstanceID:" + (decorationLayout==null?"null":decorationLayout.GetInstanceID().ToString()));
    }
    public void OnPointerEnter(PointerEventData eventData) {
      //Log.TWL(0, "CustomLanceSlot.OnPointerEnter");
      this.transform.SetAsLastSibling();
      this.transform.localScale = Vector3.one;
      this.shuffleLayout.currentSlot = this;
      int siblingIndex = this.transform.GetSiblingIndex();
      for (int t = this.index + 1; t < shuffleLayout.slots.Count; ++t) {
        siblingIndex -= 1;
        shuffleLayout.slots[t].transform.SetSiblingIndex(siblingIndex);
      }
      for (int t = this.index - 1; t >= 0; --t) {
        siblingIndex -= 1;
        shuffleLayout.slots[t].transform.SetSiblingIndex(siblingIndex);
      }
    }
    public void OnPointerExit(PointerEventData eventData) {
      if (this.shuffleLayout.currentSlot == this) { this.shuffleLayout.currentSlot = null; };
      this.transform.localScale = new Vector3(0.8f, 0.8f, 1.0f);
    }
  }
}