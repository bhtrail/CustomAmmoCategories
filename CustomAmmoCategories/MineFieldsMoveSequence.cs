﻿using BattleTech;
using BattleTech.UI;
using CustAmmoCategories;
using CustomAmmoCategoriesLog;
using Harmony;
using Localize;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CustAmmoCategories {
  public class MapTerrainCellWaypoint {
    public MapTerrainDataCellEx cell;
    public WayPoint startPoint;
    public MapTerrainCellWaypoint(MapTerrainDataCellEx c, WayPoint sp) {
      this.cell = c;
      this.startPoint = sp;
    }
  }
  public class LandMineFXRecord {
    public MapTerrainDataCellEx cell;
    public Vector3 position;
    public bool hasPlayed;
    public bool shouldPlay;
    public MineFieldDef def;
    public GameObject goVFX;
    public CombatGameState Combat;
    public LandMineFXRecord(CombatGameState combat, MapTerrainDataCellEx c, MineFieldDef d, bool sp, Vector3 pos) {
      Combat = combat;
      cell = c;
      hasPlayed = false;
      shouldPlay = sp;
      def = d;
      goVFX = null;
      position = pos;
    }
    public void Play(CombatGameState combat,AbstractActor unit) {
      this.Combat = combat;
      if (this.def == null) { return; }
      if (string.IsNullOrEmpty(this.def.VFXprefab)) { return; };
      if (this.shouldPlay == false) { return; };
      Vector3 scale = Vector3.zero;
      scale.x = this.def.VFXScaleX;
      scale.y = this.def.VFXScaleY;
      scale.z = this.def.VFXScaleZ;
      Vector3 offset = Vector3.zero;
      offset.x = this.def.VFXOffsetX;
      offset.y = this.def.VFXOffsetY;
      offset.z = this.def.VFXOffsetZ;
      ParticleSystem component = ObjectSpawnDataSelf.playVFXAt(combat, this.def.VFXprefab, this.position + offset, scale, Vector3.zero);
      this.goVFX = component.gameObject;
      if (this.def.SFX != null) { this.def.SFX.play(unit.GameRep.audioObject); };
    }
  }
  public class AoEExplosionHitRecord {
    public Vector3 hitPosition;
    public float Damage;
    public AoEExplosionHitRecord(Vector3 pos, float dmg) {
      this.hitPosition = pos;
      this.Damage = dmg;
    }
  }
  public class AoEExplosionRecord {
    public ICombatant target;
    public float HeatDamage;
    public float StabDamage;
    public Dictionary<int, AoEExplosionHitRecord> hitRecords;
    public AoEExplosionRecord(ICombatant trg) {
      this.target = trg;
      this.HeatDamage = 0f;
      this.StabDamage = 0f;
      this.hitRecords = new Dictionary<int, AoEExplosionHitRecord>();
    }
  }
  public class LandMineTerrainRecord {
    public MineFieldDef def;
    public MapTerrainDataCellEx cell;
    public LandMineTerrainRecord(MineFieldDef d, MapTerrainDataCellEx c) { def = d; cell = c; }
    public void applyImpactTempMask(CombatGameState combat) {
      Log.F.TWL(0,"Applying minefield long effect:" + def.LongVFXOnImpact + "/" + def.tempDesignMaskOnImpact + "/" + this.cell.x + "," + this.cell.y);
      int turns = def.tempDesignMaskOnImpactTurns;
      string vfx = def.LongVFXOnImpact;
      Vector3 scale = Vector3.one; scale.x = def.LongVFXOnImpactScaleX; scale.y = def.LongVFXOnImpactScaleY; scale.z = def.LongVFXOnImpactScaleZ;
      int radius = def.tempDesignMaskCellRadius;
      DesignMaskDef mask = def.TempDesignMask();
      //if (mask == null) { return; };
      if (radius == 0) {
        cell.hexCell.addTempTerrainVFX(combat, vfx, turns, scale);
        DynamicMapHelper.addDesignMaskAsync(cell.hexCell, mask, turns);
        //AsyncDesignMaskUpdater admu = new AsyncDesignMaskUpdater(cell.hexCell, mask, turns);
        //Thread designMaskApplyer = new Thread(new ThreadStart(admu.asyncAddMask));
        //designMaskApplyer.Start();
      } else {
        List<MapTerrainHexCell> affectedHexCells = MapTerrainHexCell.listHexCellsByCellRadius(cell, radius);
        foreach (MapTerrainHexCell hexCell in affectedHexCells) {
          hexCell.addTempTerrainVFX(combat, vfx, turns, scale);
          DynamicMapHelper.addDesignMaskAsync(hexCell, mask, turns);
        }
        //AsyncDesignMaskUpdater admu = new AsyncDesignMaskUpdater(affectedHexCells, mask, turns);
        //Thread designMaskApplyer = new Thread(new ThreadStart(admu.asyncAddMask));
        //designMaskApplyer.Start();
      }
    }
    public void applyExplodeBurn(Weapon weapon) {
      Log.F.TWL(0, "Applying minefield burn effect:" + this.cell.x + "," + this.cell.y );
      if (def.FireTerrainCellRadius == 0) {
        if (cell.hexCell.TryBurnCell(weapon, def.FireTerrainChance, def.FireTerrainStrength, def.FireDurationWithoutForest)) {
          DynamicMapHelper.burningHexes.Add(cell.hexCell);
        };
      } else {
        List<MapTerrainHexCell> affectedHexCells = MapTerrainHexCell.listHexCellsByCellRadius(cell, def.FireTerrainCellRadius);
        foreach (MapTerrainHexCell hexCell in affectedHexCells) {
          if (hexCell.TryBurnCell(weapon, def.FireTerrainChance, def.FireTerrainStrength, def.FireDurationWithoutForest)) {
            DynamicMapHelper.burningHexes.Add(hexCell);
          };
        }
      }
    }
  }
  public class MineFieldDamage {
    public Vector3 lastVFXPos;
    public List<LandMineTerrainRecord> landminesTerrain;
    public Weapon weapon;
    public float BurnHeat;
    public Dictionary<int, AoEExplosionHitRecord> burnDamage;
    public bool sequenceAborted;
    public List<LandMineFXRecord> fxRecords;
    public Dictionary<MapTerrainDataCellEx, int> fxDictionary;
    public Dictionary<ICombatant, Dictionary<EffectData, List<ICombatant>>> Effects;
    public Dictionary<ICombatant, AoEExplosionRecord> AoEDamage;
    public void debugPrint() {
      Log.F.WL(0, "MineFieldDamage");
      Log.F.WL(1, "landminesCount:" + landminesTerrain.Count);
      Log.F.WL(1, "BurnHeat:" + BurnHeat);
      foreach (var dmg in burnDamage) {
        Log.F.WL(3, dmg.Key + ":" + dmg.Value.Damage + ":" + dmg.Value.hitPosition);
      }
      Log.F.WL(1, "sequenceAborted:" + sequenceAborted);
      foreach (var dmg in AoEDamage) {
        Log.F.WL(2, dmg.Key.DisplayName + ":" + dmg.Key.GUID);
        Log.F.WL(2, "HeatDamage:" + dmg.Value.HeatDamage);
        Log.F.WL(2, "StabDamage:" + dmg.Value.StabDamage);
        foreach (var loc in dmg.Value.hitRecords) {
          Log.F.WL(3, loc.Key + ":" + loc.Value.Damage + ":" + loc.Value.hitPosition);
        }
      }
    }
    public MineFieldDamage() {
      weapon = null;
      lastVFXPos = Vector3.zero;
      sequenceAborted = false;
      fxRecords = new List<LandMineFXRecord>();
      fxDictionary = new Dictionary<MapTerrainDataCellEx, int>();
      AoEDamage = new Dictionary<ICombatant, AoEExplosionRecord>();
      Effects = new Dictionary<ICombatant, Dictionary<EffectData, List<ICombatant>>>();
      burnDamage = new Dictionary<int, AoEExplosionHitRecord>();
      landminesTerrain = new List<LandMineTerrainRecord>();
    }
    public void AddEffect(ICombatant creator, ICombatant target, EffectData effect) {
      if (Effects.ContainsKey(target) == false) { Effects.Add(target, new Dictionary<EffectData, List<ICombatant>>()); };
      Dictionary<EffectData, List<ICombatant>> trgEffects = Effects[target];
      if (trgEffects.ContainsKey(effect) == false) { trgEffects.Add(effect, new List<ICombatant>()); };
      List<ICombatant> trgEffectCreators = trgEffects[effect];
      trgEffectCreators.Add(creator);
    }
    public void AddBurnHeat(AbstractActor target, float burnHeat, Vector3 pos) {
      Mech mech = target as Mech;
      Vehicle vehicle = target as Vehicle;
      if (mech != null) { this.BurnHeat = burnHeat; return; };
      this.BurnHeat = 0f;
      List<int> hitLocations = new List<int> { 1 };
      if (vehicle != null) {
        hitLocations = new List<int> { (int)VehicleChassisLocations.Front, (int)VehicleChassisLocations.Left, (int)VehicleChassisLocations.Right, (int)VehicleChassisLocations.Left };
      }
      foreach (int hitLocation in hitLocations) {
        float CurrentLocationDamage = burnHeat / (float)hitLocations.Count;
        if (this.burnDamage.ContainsKey(hitLocation)) {
          this.burnDamage[hitLocation].Damage += CurrentLocationDamage;
        } else {
          Vector3 hitPos = target.getImpactPositionSimple(target, pos, hitLocation);
          this.burnDamage[hitLocation] = new AoEExplosionHitRecord(hitPos, CurrentLocationDamage);
        }
      }
    }
    public bool AddLandMineDamage(AbstractActor target, MineFieldDef def, Vector3 pos) {
      Mech mech = target as Mech;
      Vehicle vehicle = target as Vehicle;
      foreach (var effect in def.statusEffects) { AddEffect(target, target, effect); };
      if (AoEDamage.ContainsKey(target) == false) { AoEDamage.Add(target, new AoEExplosionRecord(target)); };
      AoEExplosionRecord AoERecord = AoEDamage[target];
      AoERecord.HeatDamage += def.Heat;
      AoERecord.StabDamage += def.Instability;
      float Damage = def.Damage;
      List<int> hitLocations = new List<int> { 1 };
      if (mech != null) {
        hitLocations = new List<int> { (int)ArmorLocation.LeftLeg, (int)ArmorLocation.RightLeg };
      } else {
        Damage += def.Heat;
        if (vehicle != null) {
          hitLocations = new List<int> { (int)VehicleChassisLocations.Front, (int)VehicleChassisLocations.Left, (int)VehicleChassisLocations.Right, (int)VehicleChassisLocations.Left };
        }
      }
      bool result = false;
      foreach (int hitLocation in hitLocations) {
        float CurrentLocationDamage = Damage / (float)hitLocations.Count;
        if (AoERecord.hitRecords.ContainsKey(hitLocation)) {
          AoERecord.hitRecords[hitLocation].Damage += CurrentLocationDamage;
        } else {
          Vector3 hitPos = target.getImpactPositionSimple(target, pos, hitLocation);
          AoERecord.hitRecords[hitLocation] = new AoEExplosionHitRecord(hitPos, CurrentLocationDamage);
        }
        if (target.ArmorForLocation(hitLocation) < AoERecord.hitRecords[hitLocation].Damage) { result = true; };
      }
      return result;
    }
    public void AddLandMineExplosion(AbstractActor unit, MineFieldDef def, Vector3 pos) {
      Log.F.TWL(0, "AddLandMineExplosion " + def.AoERange + "/" + def.AoEDamage + "/" + def.AoEHeat);
      if (def.AoERange < CustomAmmoCategories.Epsilon) { return; };
      foreach (ICombatant target in unit.Combat.GetAllLivingCombatants()) {
        if (target.GUID == unit.GUID) { continue; };
        if (target.IsDead) { continue; };
        Vector3 CurrentPosition = target.CurrentPosition + Vector3.up * target.AoEHeightFix();
        float distance = Vector3.Distance(CurrentPosition, pos);
        Log.LogWrite(" " + target.DisplayName + ":" + target.GUID + " " + distance + "\n");
        if (distance > def.AoERange) { continue; };
        foreach (var effect in def.statusEffects) { AddEffect(unit, target, effect); };
        float HeatDamage = def.AoEHeat * (def.AoERange - distance) / def.AoERange;
        float Damage = def.AoEDamage * (def.AoERange - distance) / def.AoERange;
        float StabDamage = def.AoEInstability * (def.AoERange - distance) / def.AoERange;
        Mech mech = target as Mech;
        Vehicle vehicle = target as Vehicle;
        if (mech == null) {
          Damage += HeatDamage;
        };
        List<int> hitLocations = null;
        Dictionary<int, float> AOELocationDict = null;
        if (mech != null) {
          hitLocations = unit.Combat.HitLocation.GetPossibleHitLocations(pos, mech);
          if (CustomAmmoCategories.MechHitLocations == null) { CustomAmmoCategories.InitHitLocationsAOE(); };
          AOELocationDict = CustomAmmoCategories.MechHitLocations;
          int HeadIndex = hitLocations.IndexOf((int)ArmorLocation.Head);
          if ((HeadIndex >= 0) && (HeadIndex < hitLocations.Count)) { hitLocations.RemoveAt(HeadIndex); };
        } else
        if (target is Vehicle) {
          hitLocations = unit.Combat.HitLocation.GetPossibleHitLocations(pos, vehicle);
          if (CustomAmmoCategories.VehicleLocations == null) { CustomAmmoCategories.InitHitLocationsAOE(); };
          AOELocationDict = CustomAmmoCategories.VehicleLocations;
        } else {
          hitLocations = new List<int>() { 1 };
          if (CustomAmmoCategories.OtherLocations == null) { CustomAmmoCategories.InitHitLocationsAOE(); };
          AOELocationDict = CustomAmmoCategories.OtherLocations;
        }
        float fullLocationDamage = 0.0f;
        foreach (int hitLocation in hitLocations) {
          if (AOELocationDict.ContainsKey(hitLocation)) {
            fullLocationDamage += AOELocationDict[hitLocation];
          } else {
            fullLocationDamage += 100f;
          }
        }
        if (AoEDamage.ContainsKey(target) == false) { AoEDamage.Add(target, new AoEExplosionRecord(target)); };
        AoEExplosionRecord AoERecord = AoEDamage[target];
        AoERecord.HeatDamage += HeatDamage;
        AoERecord.StabDamage += StabDamage;
        foreach (int hitLocation in hitLocations) {
          float currentDamageCoeff = 100f;
          if (AOELocationDict.ContainsKey(hitLocation)) {
            currentDamageCoeff = AOELocationDict[hitLocation];
          }
          currentDamageCoeff /= fullLocationDamage;
          float CurrentLocationDamage = Damage * currentDamageCoeff;
          if (CurrentLocationDamage < CustomAmmoCategories.Epsilon) { continue; }
          if (AoERecord.hitRecords.ContainsKey(hitLocation)) {
            AoERecord.hitRecords[hitLocation].Damage += CurrentLocationDamage;
          } else {
            Vector3 hitPos = target.getImpactPositionSimple(unit, pos, hitLocation);
            AoERecord.hitRecords[hitLocation] = new AoEExplosionHitRecord(hitPos, CurrentLocationDamage);
          }
        }
      }
    }
    public void resolveMineFiledDamage(AbstractActor unit, int SequenceID) {
      Log.F.TWL(0, "resolveMineFiledDamage " + unit.DisplayName+":"+unit.GUID+" "+SequenceID);
      if (this.weapon == null) {
        Log.F.WL(1, "weapon seted. no minefield/burn damage?");
        return;
      }
      var fakeHit = new WeaponHitInfo(-1, -1, -1, -1, this.weapon.parent.GUID, unit.GUID, -1, null, null, null, null, null, null, new AttackImpactQuality[1] { AttackImpactQuality.Solid }, new AttackDirection[1] { AttackDirection.FromArtillery }, null, null, null);
      HashSet<Mech> heatSequence = new HashSet<Mech>();
      HashSet<Mech> instabilitySequence = new HashSet<Mech>();
      HashSet<ICombatant> deathSequence = new HashSet<ICombatant>();
      Log.F.WL(1, "Applying burn vehicle damage");
      foreach (var bdmg in this.burnDamage) {
        float LocArmor = unit.ArmorForLocation(bdmg.Key);
        if ((double)LocArmor < (double)bdmg.Value.Damage) {
          unit.Combat.MessageCenter.PublishMessage((MessageCenterMessage)new FloatieMessage(this.weapon.parent.GUID, unit.GUID, new Text("{0}", new object[1]
          {
                      (object) (int) Mathf.Max(1f, bdmg.Value.Damage)
          }), unit.Combat.Constants.CombatUIConstants.floatieSizeMedium, FloatieMessage.MessageNature.StructureDamage, bdmg.Value.hitPosition.x, bdmg.Value.hitPosition.y, bdmg.Value.hitPosition.z));
        } else {
          unit.Combat.MessageCenter.PublishMessage((MessageCenterMessage)new FloatieMessage(this.weapon.parent.GUID, unit.GUID, new Text("{0}", new object[1]
          {
                      (object) (int) Mathf.Max(1f, bdmg.Value.Damage)
          }), unit.Combat.Constants.CombatUIConstants.floatieSizeMedium, FloatieMessage.MessageNature.ArmorDamage, bdmg.Value.hitPosition.x, bdmg.Value.hitPosition.y, bdmg.Value.hitPosition.z));
        }
        unit.TakeWeaponDamage(fakeHit, bdmg.Key, this.weapon, bdmg.Value.Damage, 0, DamageType.AmmoExplosion);
      }
      Log.F.WL(1, "Applying burn floatie message");
      if (this.burnDamage.Count > 0) {
        unit.Combat.MessageCenter.PublishMessage((MessageCenterMessage)new FloatieMessage(this.weapon.parent.GUID, unit.GUID, "BURN DAMAGE", FloatieMessage.MessageNature.CriticalHit));
      }
      deathSequence.Add(unit);
      Mech mech = unit as Mech;
      if ((mech != null) && (this.BurnHeat > CustomAmmoCategories.Epsilon)) {
        unit.AddExternalHeat("BurningCell", Mathf.RoundToInt(this.BurnHeat));
        unit.Combat.MessageCenter.PublishMessage((MessageCenterMessage)new FloatieMessage(this.weapon.parent.GUID, unit.GUID, "+ " + Mathf.RoundToInt(this.BurnHeat) + " HEAT FROM FIRE", FloatieMessage.MessageNature.Debuff));
        heatSequence.Add(mech);
      }
      Log.F.WL(1, "Applying minefield damage");
      foreach (var mfdmgs in this.AoEDamage) {
        fakeHit.targetId = mfdmgs.Value.target.GUID;
        ICombatant target = mfdmgs.Key;
        foreach (var mfdmg in mfdmgs.Value.hitRecords) {
          float LocArmor = target.ArmorForLocation(mfdmg.Key);
          if ((double)LocArmor < (double)mfdmg.Value.Damage) {
            Log.F.WL(2, "floatie message structure");
            unit.Combat.MessageCenter.PublishMessage((MessageCenterMessage)new FloatieMessage(this.weapon.parent.GUID, target.GUID, new Text("{0}", new object[1]
            {
                      (object) (int) Mathf.Max(1f, mfdmg.Value.Damage)
            }), unit.Combat.Constants.CombatUIConstants.floatieSizeMedium, FloatieMessage.MessageNature.StructureDamage, mfdmg.Value.hitPosition.x, mfdmg.Value.hitPosition.y, mfdmg.Value.hitPosition.z));
          } else {
            Log.F.WL(2, "floatie message armor");
            unit.Combat.MessageCenter.PublishMessage((MessageCenterMessage)new FloatieMessage(this.weapon.parent.GUID, target.GUID, new Text("{0}", new object[1]
            {
                      (object) (int) Mathf.Max(1f, mfdmg.Value.Damage)
            }), unit.Combat.Constants.CombatUIConstants.floatieSizeMedium, FloatieMessage.MessageNature.ArmorDamage, mfdmg.Value.hitPosition.x, mfdmg.Value.hitPosition.y, mfdmg.Value.hitPosition.z));
          }
          Log.F.WL(2, "take weapon damage");
          target.TakeWeaponDamage(fakeHit, mfdmg.Key, this.weapon, mfdmg.Value.Damage, 0, DamageType.AmmoExplosion);
        }
        if (mfdmgs.Value.hitRecords.Count > 0) {
          Log.F.WL(2, "floatie message explosion");
          unit.Combat.MessageCenter.PublishMessage((MessageCenterMessage)new FloatieMessage(this.weapon.parent.GUID, unit.GUID, "LANDMINES EXPLODES:" + this.landminesTerrain.Count, FloatieMessage.MessageNature.CriticalHit));
          deathSequence.Add(mfdmgs.Value.target);
        }
        Mech trgmech = mfdmgs.Value.target as Mech;
        if (trgmech != null) {
          if (mfdmgs.Value.HeatDamage > CustomAmmoCategories.Epsilon) {
            trgmech.AddExternalHeat("LandMinesHeat", Mathf.RoundToInt(mfdmgs.Value.HeatDamage));
            trgmech.Combat.MessageCenter.PublishMessage((MessageCenterMessage)new FloatieMessage(this.weapon.parent.GUID, unit.GUID, "+ " + Mathf.RoundToInt(mfdmgs.Value.HeatDamage) + " HEAT FROM LANDMINES", FloatieMessage.MessageNature.Debuff));
            heatSequence.Add(trgmech);
          }
          if (mfdmgs.Value.StabDamage > CustomAmmoCategories.Epsilon) {
            trgmech.AddAbsoluteInstability(mfdmgs.Value.StabDamage, StabilityChangeSource.Moving, this.weapon.parent.GUID);
            trgmech.Combat.MessageCenter.PublishMessage((MessageCenterMessage)new FloatieMessage(this.weapon.parent.GUID, unit.GUID, "+ " + mfdmgs.Value.StabDamage + " INSTABILITY FROM LANDMINES", FloatieMessage.MessageNature.Debuff));
            instabilitySequence.Add(trgmech);
          }
        }
      }
      Log.F.WL(1, "Applying minefield effects");
      foreach (var trgseffs in this.Effects) {
        ICombatant target = trgseffs.Key;
        foreach (var trgeffs in trgseffs.Value) {
          EffectData effect = trgeffs.Key;
          foreach (ICombatant creator in trgeffs.Value) {
            string effectID = string.Format("OnLandMineHitEffect_{0}_{1}", (object)creator.GUID, (object)SequenceID);
            Log.F.WL(1, $"Applying effectID:{effect.Description.Id} with effectDescId:{effect?.Description.Id} effectDescName:{effect?.Description.Name}");
            unit.Combat.EffectManager.CreateEffect(effect, effectID, -1, creator, target, new WeaponHitInfo(), 0, false);
          }
          if (trgeffs.Value.Count > 1) {
            Log.F.WL(2, "floatie message effect");
            unit.Combat.MessageCenter.PublishMessage((MessageCenterMessage)new FloatieMessage(this.weapon.parent.GUID, target.GUID, effect.Description.Name + " x " + trgeffs.Value.Count.ToString(), FloatieMessage.MessageNature.Debuff));
          } else {
            unit.Combat.MessageCenter.PublishMessage((MessageCenterMessage)new FloatieMessage(this.weapon.parent.GUID, target.GUID, effect.Description.Name, FloatieMessage.MessageNature.Debuff));
          }
        }
      }
      Log.F.WL(1, "Applying handling procedures\n");
      foreach (Mech trgmech in heatSequence) {
        Log.F.WL(2, "GenerateAndPublishHeatSequence\n");
        trgmech.GenerateAndPublishHeatSequence(SequenceID, true, false, this.weapon.parent.GUID);
      }
      foreach (Mech trgmech in instabilitySequence) {
        Log.F.WL(2, "HandleKnockdown\n");
        trgmech.HandleKnockdown(-1, "LANDMINES", this.weapon.parent.CurrentPosition, null);
      }
      foreach (ICombatant trg in deathSequence) {
        Log.F.WL(2, "HandleDeath\n");
        trg.HandleDeath(this.weapon.parent.GUID);
      }
      Log.F.WL(1, "Applying vfx effects\n");
      foreach (var lmVFX in this.fxRecords) {
        if (lmVFX.hasPlayed == false) { lmVFX.Play(unit.Combat, unit); };
        if (lmVFX.goVFX != null) { DynamicMapHelper.delayedPoolList.Add(new DelayedGameObjectPoolRecord(lmVFX.def.VFXprefab,lmVFX.goVFX)); }
      }
      Log.F.WL(1, "Applying terrain effects\n");
      foreach (LandMineTerrainRecord lmtr in this.landminesTerrain) {
        lmtr.applyExplodeBurn(this.weapon);
        lmtr.applyImpactTempMask(unit.Combat);
      }
      DynamicMapHelper.Combat = unit.Combat;
      //System.Threading.Timer timer = null;
      //timer = new System.Threading.Timer((obj) => {
        
        //timer.Dispose();
      //}, null, 3000, System.Threading.Timeout.Infinite);
    }
    /*public bool AddMineField(MineField field,AbstractActor unit) {
      Mech mech = unit as Mech;
      Vehicle vehicle = unit as Vehicle;

      if(mech != null) {
      }
    }*/
  }
  public class DelayedGameObjectPoolRecord {
    public string name;
    public GameObject obj;
    public DelayedGameObjectPoolRecord(string n, GameObject go) { name = n; obj = go; }
  }
  public class AsyncMineDamageDelegate {
    public AbstractActor unit;
    public int sequenceId;
    public AsyncMineDamageDelegate(AbstractActor u, int sid) {
      this.unit = u;
      this.sequenceId = sid;
    }
    public void procMineFieldDamage() {
      if (DynamicMapHelper.registredMineFieldDamage.ContainsKey(this.unit) == false) { return; }
      MineFieldDamage mf = DynamicMapHelper.registredMineFieldDamage[this.unit];
      mf.resolveMineFiledDamage(this.unit, this.sequenceId);
      DynamicMapHelper.registredMineFieldDamage.Remove(this.unit);
    }
  }
  public class AsyncDesignMaskApplyRecord {
    public DesignMaskDef designMask;
    public MapTerrainHexCell hexCell;
    public int counter;
    public AsyncDesignMaskApplyRecord(DesignMaskDef dm, MapTerrainHexCell hc,int c) {
      designMask = dm;
      hexCell = hc;
      counter = c;
    }
  }
  public static partial class DynamicMapHelper {
    public static Dictionary<ICombatant, MineFieldDamage> registredMineFieldDamage = new Dictionary<ICombatant, MineFieldDamage>();
    public static List<DelayedGameObjectPoolRecord> delayedPoolList = new List<DelayedGameObjectPoolRecord>();
    public static Queue<AsyncDesignMaskApplyRecord> asyncTerrainDesignMaskQueue = new Queue<AsyncDesignMaskApplyRecord>();
    public static CombatGameState Combat = null;
    public static Thread asyncTerrainDesignMask = new Thread(DynamicMapHelper.asyncTerrainDesignMaskProc);
    public static void addDesignMaskAsync(MapTerrainHexCell hex,DesignMaskDef dm, int counter) {
      if (dm == null) { return; }
      asyncTerrainDesignMaskQueue.Enqueue(new AsyncDesignMaskApplyRecord(dm,hex,counter));
    }
    public static void asyncTerrainDesignMaskProc() {
      try {
        Log.F.TWL(0, "asyncTerrainDesignMaskProc started\n");
        while (true) {
          if (asyncTerrainDesignMaskQueue.Count > 0) {
            AsyncDesignMaskApplyRecord arec = DynamicMapHelper.asyncTerrainDesignMaskQueue.Dequeue();
            Log.F.TWL(0, "async add design mask:" + arec.designMask.Id+ " to "+arec.hexCell.x+","+arec.hexCell.y);
            arec.hexCell.addTempTerrainMask(arec.designMask, arec.counter);
          } else {
            Thread.Sleep(100);
          }
        }
      } catch (ThreadAbortException) {
        Log.F.TWL(0, "asyncTerrainDesignMaskProc aborted");
        asyncTerrainDesignMaskQueue.Clear();
      }
    }
    public static void PoolDelayedGameObject() {
      if (DynamicMapHelper.Combat == null) { return; }
      Log.F.TWL(0, "DynamicMapHelper.PoolDelayedGameObject:" + delayedPoolList.Count);
      List<DelayedGameObjectPoolRecord> tmp = new List<DelayedGameObjectPoolRecord>();
      tmp.AddRange(delayedPoolList);
      delayedPoolList.Clear();
      foreach (var dgo in tmp) {
        Log.F.WL(1, dgo.name + ":" + dgo.obj.GetInstanceID());
        Combat.DataManager.PoolGameObject(dgo.name, dgo.obj);
      }
    }
    /*public static bool isNewMoveCell(this ICombatant combatant, MapTerrainDataCellEx cell) {
      if (registredMineFieldDamage.ContainsKey(combatant) == false) {
        registredMineFieldDamage.Add(combatant, new MineFieldDamage());
      };
      bool result = true;
      MineFieldDamage fieldDamageRec = registredMineFieldDamage[combatant];
      if(fieldDamageRec != null) { result = fieldDamageRec.lastCell == cell; };
      fieldDamageRec.lastCell = cell;
      return result;
    }*/
    public static bool testMineFiledDamage(Mech mech, float damage, float heat, float stability) {
      if (mech.LeftLegArmor < (damage / 2f)) { return true; }
      if (mech.RightLegArmor < (damage / 2f)) { return true; }
      if ((mech.CurrentHeat + heat) > mech.MaxHeat) { return true; }
      if ((mech.CurrentStability + stability) > mech.MaxStability) { return true; }
      return false;
    }
    public static bool testMineFiledDamage(Vehicle vehicle, float damage, float heat, float stability) {
      float dmg = (damage + heat) / 4f;
      if (vehicle.RearArmor < dmg) { return true; }
      if (vehicle.FrontArmor < dmg) { return true; }
      if (vehicle.LeftSideArmor < dmg) { return true; }
      if (vehicle.RightSideArmor < dmg) { return true; }
      return false;
    }
    public static bool calculateAbortPos(AbstractActor unit, List<WayPoint> waypoints, Vector3 finalHeading, out List<WayPoint> result, out Vector3 heading) {
      Log.F.TWL(0, "calculateAbortPos:" + unit.DisplayName+" waypoints:"+waypoints.Count);
      DynamicMapHelper.PoolDelayedGameObject();
      List<MapTerrainCellWaypoint> terrainWaypoints = DynamicMapHelper.getVisitedWaypoints(unit.Combat, waypoints);
      result = new List<WayPoint>();
      WayPoint lastWaypoint = null;
      Mech mech = unit as Mech;
      Vehicle vehicle = unit as Vehicle;
      //float Damage = 0f;
      //float Heat = 0f;
      float BurnHeat = 0f;
      float BurnAllHeat = 0f;
      float BurnAllCount = 0f;
      //float Stability = 0f;
      float rollMod = 1f;
      Weapon minefieldWeapon = null;
      Weapon burnWeapon = null;
      heading = finalHeading;
      if (waypoints == null) { Log.F.WL(1, "waypoints is null"); return false; }
      if (waypoints.Count == 0) { Log.F.WL(1, "waypoints is empty");return false; }
      if (unit.UnaffectedFire() && unit.UnaffectedLandmines()) { Log.F.WL(1, "unaffected fire and landmines");  return false; };
      bool UnaffectedFire = unit.UnaffectedFire();
      bool UnaffectedLandmines = unit.UnaffectedLandmines();
      PathingCapabilitiesDef PathingCaps = (PathingCapabilitiesDef)typeof(Pathing).GetProperty("PathingCaps", BindingFlags.Instance | BindingFlags.NonPublic).GetGetMethod(true).Invoke(unit.Pathing, null);
      if (CustomAmmoCategories.Settings.MineFieldPathingMods.ContainsKey(PathingCaps.Description.Id)) {
        rollMod = CustomAmmoCategories.Settings.MineFieldPathingMods[PathingCaps.Description.Id];
      }
      Log.F.WL(1, "current pathing:" + PathingCaps.Description.Id +" roll mod:"+rollMod);
      MineFieldDamage mfDamage = new MineFieldDamage();
      bool abortSequce = false;
      for (int index = terrainWaypoints.Count - 1; index >= 0; --index) {
        MapTerrainCellWaypoint cell = terrainWaypoints[index];
        if (lastWaypoint == null) { result.Add(cell.startPoint); lastWaypoint = cell.startPoint; };
        if (lastWaypoint.Position != cell.startPoint.Position) { result.Add(cell.startPoint); lastWaypoint = cell.startPoint; };
        Log.F.WL(2, "cell:" + cell.cell.x + "," + cell.cell.y + " startPos:" + cell.startPoint.Position);
        if ((cell.cell.BurningStrength > 0)&&(UnaffectedFire == false)) {
          BurnAllHeat += (float)cell.cell.BurningStrength;
          BurnAllCount += 1f;
          BurnHeat = BurnAllHeat / BurnAllCount;
          burnWeapon = cell.cell.BurningWeapon;
        }
        Log.F.WL(2, "burning damage added");
        if (unit.Combat == null) {
          Log.F.WL(2, "actor without combat???!!!!", true);
        }
        Vector3 cellPosition = unit.Combat.MapMetaData.getWorldPos(new Point(cell.cell.y, cell.cell.x));
        MineField strongestMine = null;
        if (cell.cell.hexCell == null) {
          Log.F.WL(2, "cell without HEX????!!", true);
          continue;
        }
        if (cell.cell.hexCell.MineField == null) {
          Log.F.WL(2, "HEX withlout landmines list????!!", true);
          continue;
        }
        Log.F.WL(2, "Hex cell " + cell.cell.hexCell.x + ":" + cell.cell.hexCell.y + " minefields in hex:" + cell.cell.hexCell.MineField.Count);
        if (UnaffectedLandmines) {
          Log.F.WL(2, "Unaffected by landmines\n");
          continue;
        };
        //if (cell.cell.hexCell.MineField.Count == 0) { continue; }
        foreach (MineField mineField in cell.cell.hexCell.MineField) {
          if (mineField == null) { Log.F.WL(3, "null minefield???!!!", true); continue; }
          if (mineField.Def == null) { Log.F.WL(3, "mine field without def???!!!\n", true); continue; };
          Log.F.WL(3, "mines in minefield:" + mineField.count + " chance:" + mineField.Def.Chance + " damage:" + mineField.Def.Damage);
          if (mineField.count == 0) { continue; };
          float roll = Random.Range(0f, 1f);
          float chance = mineField.Def.Chance * rollMod;
          Log.F.WL(3, "roll:" + roll + " effective chance:" + chance);
          if (roll < chance) {
            Log.F.WL(3, "damage");
            mineField.count -= 1;
            minefieldWeapon = mineField.weapon;
            mfDamage.landminesTerrain.Add(new LandMineTerrainRecord(mineField.Def, cell.cell));
            if (mfDamage.AddLandMineDamage(unit, mineField.Def, cellPosition)) { abortSequce = true; };
            mfDamage.AddLandMineExplosion(unit, mineField.Def, cellPosition);
            //mfDamage.debugPrint();
            if (string.IsNullOrEmpty(mineField.Def.VFXprefab) == false)
              if ((strongestMine == null)) { strongestMine = mineField; } else
                if (strongestMine.Def.Damage < mineField.Def.Damage) { strongestMine = mineField; }
            break;
          }
        }
        if (strongestMine != null) {
          if ((mfDamage.lastVFXPos == Vector3.zero) || (Vector3.Distance(mfDamage.lastVFXPos, cellPosition) >= strongestMine.Def.VFXMinDistance)) {
            LandMineFXRecord fxRec = new LandMineFXRecord(unit.Combat, cell.cell, strongestMine.Def, true, cellPosition);
            mfDamage.fxRecords.Add(fxRec);
            mfDamage.fxDictionary.Add(cell.cell, mfDamage.fxRecords.Count - 1);
            mfDamage.lastVFXPos = cellPosition;
          } else {
            LandMineFXRecord fxRec = new LandMineFXRecord(unit.Combat, cell.cell, null, false, cellPosition);
            mfDamage.fxRecords.Add(fxRec);
            mfDamage.fxDictionary.Add(cell.cell, mfDamage.fxRecords.Count - 1);
          }
        } else {
          LandMineFXRecord fxRec = new LandMineFXRecord(unit.Combat, cell.cell, null, false, cellPosition);
          mfDamage.fxRecords.Add(fxRec);
          mfDamage.fxDictionary.Add(cell.cell, mfDamage.fxRecords.Count - 1);
        }
        if (abortSequce) {
          cellPosition = unit.Combat.HexGrid.GetClosestPointOnGrid(cellPosition);
          result.Add(new WayPoint(cellPosition, lastWaypoint.Backwards));
          heading = cellPosition - lastWaypoint.Position;
          break;
        }
      }
      //mfDamage.BurnHeat = BurnHeat;
      mfDamage.AddBurnHeat(unit, BurnHeat, waypoints[waypoints.Count - 1].Position);
      mfDamage.weapon = (minefieldWeapon != null) ? minefieldWeapon : burnWeapon;
      mfDamage.sequenceAborted = abortSequce;
      if (DynamicMapHelper.registredMineFieldDamage.ContainsKey(unit) == false) {
        DynamicMapHelper.registredMineFieldDamage.Add(unit, mfDamage);
      };
      mfDamage.debugPrint();
      return abortSequce;
    }
    public static void calculateJumpDamage(AbstractActor unit, Vector3 position) {
      Log.F.TWL(0, "calculateJumpDamage " + unit.DisplayName + ":"+unit.GUID+" "+position+"\n");
      DynamicMapHelper.PoolDelayedGameObject();
      Mech mech = unit as Mech;
      Vehicle vehicle = unit as Vehicle;
      float BurnHeat = 0f;
      float BurnAllHeat = 0f;
      float BurnAllCount = 0f;
      //float Stability = 0f;
      float rollMod = 1f;
      Weapon minefieldWeapon = null;
      Weapon burnWeapon = null;
      PathingCapabilitiesDef PathingCaps = (PathingCapabilitiesDef)typeof(Pathing).GetProperty("PathingCaps", BindingFlags.Instance | BindingFlags.NonPublic).GetGetMethod(true).Invoke(unit.Pathing, null);
      Log.F.WL(1, "current pathing:" + PathingCaps.Description.Id);
      if (CustomAmmoCategories.Settings.MineFieldPathingMods.ContainsKey(PathingCaps.Description.Id)) {
        rollMod = CustomAmmoCategories.Settings.MineFieldPathingMods[PathingCaps.Description.Id];
      }
      MineFieldDamage mfDamage = new MineFieldDamage();
      MapTerrainDataCellEx cell = unit.Combat.MapMetaData.GetCellAt(position) as MapTerrainDataCellEx;
      if (cell == null) { return; }
      Log.F.WL(1, "cell:" + cell.x + "," + cell.y + "\n");
      if (cell.BurningStrength > 0) {
        BurnAllHeat += (float)cell.BurningStrength;
        BurnAllCount += 1f;
        BurnHeat = BurnAllHeat / BurnAllCount;
        burnWeapon = cell.BurningWeapon;
      }
      Log.F.WL(1, "burning damage added\n");
      MineField strongestMine = null;
      if (cell.hexCell == null) {
        Log.F.WL(2, "cell without HEX????!!\n", true);
        return;
      }
      if (cell.hexCell.MineField == null) {
        Log.F.WL(2, "HEX withlout landmines list????!!\n", true);
        return;
      }
      Log.F.WL(2, "Hex cell " + cell.hexCell.x + ":" + cell.hexCell.y + " minefields in hex:" + cell.hexCell.MineField.Count);
      //if (cell.cell.hexCell.MineField.Count == 0) { continue; }
      foreach (MineField mineField in cell.hexCell.MineField) {
        if (mineField == null) { Log.F.WL(3, "null minefield???!!!", true); continue; }
        if (mineField.Def == null) { Log.F.WL(3, "mine field without def???!!!", true); continue; };
        Log.F.WL(3, "mines in minefield:" + mineField.count + " chance:" + mineField.Def.Chance + " damage:" + mineField.Def.Damage);
        if (mineField.count == 0) { continue; };
        float roll = Random.Range(0f, 1f);
        float chance = mineField.Def.Chance * rollMod;
        Log.F.WL(3, "roll:" + roll + " effective chance:" + chance);
        if (roll < chance) {
          mineField.count -= 1;
          minefieldWeapon = mineField.weapon;
          mfDamage.landminesTerrain.Add(new LandMineTerrainRecord(mineField.Def, cell));
          mfDamage.AddLandMineDamage(unit, mineField.Def, position);
          mfDamage.AddLandMineExplosion(unit, mineField.Def, position);
          if (string.IsNullOrEmpty(mineField.Def.VFXprefab) == false)
            if ((strongestMine == null)) { strongestMine = mineField; } else
              if (strongestMine.Def.Damage < mineField.Def.Damage) { strongestMine = mineField; }
        }
      }
      if (strongestMine != null) {
        if ((mfDamage.lastVFXPos == Vector3.zero) || (Vector3.Distance(mfDamage.lastVFXPos, position) >= strongestMine.Def.VFXMinDistance)) {
          LandMineFXRecord fxRec = new LandMineFXRecord(unit.Combat, cell, strongestMine.Def, true, position);
          mfDamage.fxRecords.Add(fxRec);
          mfDamage.fxDictionary.Add(cell, mfDamage.fxRecords.Count - 1);
          mfDamage.lastVFXPos = position;
        } else {
          LandMineFXRecord fxRec = new LandMineFXRecord(unit.Combat, cell, null, false, position);
          mfDamage.fxRecords.Add(fxRec);
          mfDamage.fxDictionary.Add(cell, mfDamage.fxRecords.Count - 1);
        }
      } else {
        LandMineFXRecord fxRec = new LandMineFXRecord(unit.Combat, cell, null, false, position);
        mfDamage.fxRecords.Add(fxRec);
        mfDamage.fxDictionary.Add(cell, mfDamage.fxRecords.Count - 1);
      }
      
      //mfDamage.BurnHeat = BurnHeat;
      mfDamage.AddBurnHeat(unit, BurnHeat, position);
      mfDamage.weapon = (minefieldWeapon != null) ? minefieldWeapon : burnWeapon;
      mfDamage.sequenceAborted = false;
      if (DynamicMapHelper.registredMineFieldDamage.ContainsKey(unit) == false) {
        DynamicMapHelper.registredMineFieldDamage.Add(unit, mfDamage);
      };
      mfDamage.debugPrint();
    }
  }
}

namespace CustAmmoCategoriesPatches {
  [HarmonyPatch(typeof(ActorMovementSequence))]
  [HarmonyPatch("SetWaypoints")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { typeof(List<WayPoint>), typeof(Vector3), typeof(bool), typeof(ICombatant) })]
  public static class ActorMovementSequence_CompleteOrders {
    private static bool Prefix(ActorMovementSequence __instance, ref List<WayPoint> waypoints, ref Vector3 finalHeading, bool sprinting, ref ICombatant meleeTarget) {
      Log.F.TWL(0, "ActorMovementSequence.SetWaypoints " + __instance.owningActor.DisplayName + ":" + __instance.owningActor.GUID);
      try {
        List<WayPoint> newWaypoints;
        Vector3 newHeading;
        if (DynamicMapHelper.calculateAbortPos(__instance.owningActor, waypoints, finalHeading, out newWaypoints, out newHeading)) {
          if (meleeTarget != null) {
            Log.F.WL(1, "melee sequence interrupted. Floatie");
            __instance.owningActor.Combat.MessageCenter.PublishMessage((MessageCenterMessage)new FloatieMessage(__instance.owningActor.GUID, __instance.owningActor.GUID, "MELEE ATTACK INTERUPTED", FloatieMessage.MessageNature.CriticalHit));
            meleeTarget = null;
          }
          Log.F.W(1, "old:"); foreach (var waypoint in waypoints) { Log.F.W(waypoint.Position.ToString() + "->"); }; Log.F.W("\n");
          waypoints.Clear();
          waypoints.AddRange(newWaypoints);
          //waypoints = newWaypoints;
          Log.F.W(1, "new:"); foreach (var waypoint in waypoints) { Log.F.W(waypoint.Position.ToString() + "->"); }; Log.F.W("\n");
          //finalHeading = newHeading;
          Log.F.WL(1,"waypoints updated\n");
        }
      } catch (Exception e) {
        Log.F.TWL(1, e.ToString(), true);
      }
      return true;
    }
  }
  [HarmonyPatch(typeof(ActorMovementSequence))]
  [HarmonyPatch("Update")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { })]
  public static class ActorMovementSequence_Update {
    private static void Postfix(ActorMovementSequence __instance) {
      try {
        AbstractActor unit = __instance.owningActor;
        if (unit == null) { return; }
        List<MapEncounterLayerDataCell> encounterLayerCells = unit.occupiedEncounterLayerCells;
        if ((encounterLayerCells == null) || (encounterLayerCells.Count <= 0)) { return; }
        MapTerrainDataCellEx cell = encounterLayerCells[0].relatedTerrainCell as MapTerrainDataCellEx;
        if (cell == null) { return; };
        if (DynamicMapHelper.registredMineFieldDamage.ContainsKey(unit) == false) { return; };
        MineFieldDamage mfDamage = DynamicMapHelper.registredMineFieldDamage[unit];
        if (mfDamage.fxDictionary.ContainsKey(cell)) {
          int fxIndex = mfDamage.fxDictionary[cell];
          int unPlayed = fxIndex;
          for (int i = fxIndex; i >= 0; --i) {
            LandMineFXRecord lmVX = mfDamage.fxRecords[i];
            if (lmVX.hasPlayed) { unPlayed = i; break; }
          }
          for (int i = unPlayed; i <= fxIndex; ++i) {
            LandMineFXRecord lmVX = mfDamage.fxRecords[i];
            if (lmVX.hasPlayed) { continue; }
            lmVX.hasPlayed = true;
            lmVX.Play(unit.Combat, unit);
          }
        }
      } catch (Exception e) {
        Log.F.TWL(1, e.ToString(), true);
      }
    }
  }
  [HarmonyPatch(typeof(ActorMovementSequence))]
  [HarmonyPatch("CompleteOrders")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { })]
  public static class ActorMovementSequence_CompleteMove {
    private static void Postfix(ActorMovementSequence __instance) {
      Log.F.TWL(0, "ActorMovementSequence.CompleteOrders " + __instance.owningActor.DisplayName + ":" + __instance.owningActor.GUID);
      try {
        if (DynamicMapHelper.registredMineFieldDamage.ContainsKey(__instance.owningActor) == false) { return; };
        MineFieldDamage mfDamage = DynamicMapHelper.registredMineFieldDamage[__instance.owningActor];
        if (__instance.meleeType == MeleeAttackType.NotSet) {
          try {
            mfDamage.resolveMineFiledDamage(__instance.owningActor, __instance.SequenceGUID);
          }catch(Exception e) {
            Log.F.TWL(0, "resolving minefield damage exception:"+e.ToString());
          }
          DynamicMapHelper.registredMineFieldDamage.Remove(__instance.owningActor);
        }
      } catch (Exception e) {
        Log.F.TWL(0, e.ToString(), true);
      }
    }
  }
  [HarmonyPatch(typeof(CombatHUD))]
  [HarmonyPatch("Init")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { typeof(CombatGameState) })]
  public static class CombatHUD_InitDynamicMap {
    public static bool Prefix(CombatHUD __instance, CombatGameState Combat) {
      if ((DynamicMapHelper.asyncTerrainDesignMask.ThreadState == ThreadState.Unstarted)
        || (DynamicMapHelper.asyncTerrainDesignMask.ThreadState == ThreadState.Aborted)) {
        DynamicMapHelper.asyncTerrainDesignMask.Start();
      }
      return true;
    }
  }
  [HarmonyPatch(typeof(MechMeleeSequence))]
  [HarmonyPatch("ExecuteMelee")]
  [HarmonyPatch(MethodType.Normal)]
  public static class MechMeleeSequence_ExecuteMelee {
    private static void Postfix(MechMeleeSequence __instance) {
      Log.F.TWL(0, "MechMeleeSequence.ExecuteMelee " + __instance.owningActor.DisplayName + ":" + __instance.owningActor.GUID);
      try {
        if (DynamicMapHelper.registredMineFieldDamage.ContainsKey(__instance.owningActor) == false) { return; };
        MineFieldDamage mfDamage = DynamicMapHelper.registredMineFieldDamage[__instance.owningActor];
        if (mfDamage.sequenceAborted) {
          Log.F.WL(1, "sequence aborted. executing OnMeleeReady()");
          __instance.OnMeleeReady(null);
        };
      } catch (Exception e) {
        Log.LogWrite(e.ToString() + "\n", true);
      }
    }
  }

  
  [HarmonyPatch(typeof(MechMeleeSequence))]
  [HarmonyPatch("BuildMeleeDirectorSequence")]
  [HarmonyPatch(MethodType.Normal)]
  public static class MechMeleeSequence_BuildMeleeDirectorSequence {
    private static bool Prefix(MechMeleeSequence __instance) {
      Log.F.TWL(0, "MechMeleeSequence.BuildMeleeDirectorSequence " + __instance.owningActor.DisplayName + ":" + __instance.owningActor.GUID);
      if (DynamicMapHelper.registredMineFieldDamage.ContainsKey(__instance.owningActor) == false) {
        return true;
      };
      MineFieldDamage mfDamage = DynamicMapHelper.registredMineFieldDamage[__instance.owningActor];
      if (mfDamage.sequenceAborted) {
        Log.F.WL(1, "damaged on path. Executing empty melee sequnce");
        List<Weapon> requestedWeapons = (List<Weapon>)typeof(MechMeleeSequence).GetField("requestedWeapons", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance);
        requestedWeapons.Clear();
        ActorMovementSequence moveSequence = (ActorMovementSequence)typeof(MechMeleeSequence).GetField("moveSequence", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance);
        AttackStackSequence meleeSequence = new AttackStackSequence((AbstractActor)__instance.OwningMech, __instance.MeleeTarget, moveSequence.FinalPos, Quaternion.LookRotation(moveSequence.FinalHeading), new List<Weapon>(), MeleeAttackType.NotSet, 0, -1);
        typeof(MechMeleeSequence).GetField("meleeSequence", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(__instance, meleeSequence);
        meleeSequence.willConsumeFiring = false;
        meleeSequence.hasOwningSequence = true;
        meleeSequence.RootSequenceGUID = __instance.RootSequenceGUID;
        if (__instance.cameraSequence == null) {
          return false;
        }
        meleeSequence.SetCamera(__instance.cameraSequence, __instance.MessageIndex);
        return false;
      }
      return true;
    }
  }
  [HarmonyPatch(typeof(MechMeleeSequence))]
  [HarmonyPatch("CompleteOrders")]
  [HarmonyPatch(MethodType.Normal)]
  public static class MechMeleeSequence_CompleteOrders {
    private static void Postfix(MechMeleeSequence __instance) {
      Log.F.TWL(0, "MechMeleeSequence.CompleteOrders " + __instance.owningActor.DisplayName + ":" + __instance.owningActor.GUID);
      try {
        if (DynamicMapHelper.registredMineFieldDamage.ContainsKey(__instance.owningActor) == false) { return; };
        MineFieldDamage mfDamage = DynamicMapHelper.registredMineFieldDamage[__instance.owningActor];
        mfDamage.resolveMineFiledDamage(__instance.owningActor, __instance.SequenceGUID);
        DynamicMapHelper.registredMineFieldDamage.Remove(__instance.owningActor);
      } catch (Exception e) {
        Log.F.TWL(0, e.ToString(), true);
      }
    }
  }
  [HarmonyPatch(typeof(MechJumpSequence))]
  [HarmonyPatch("CompleteOrders")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { })]
  public static class MechJumpSequence_CompleteOrders {
    private static void Postfix(MechJumpSequence __instance) {
      Log.F.TWL(0, "MechJumpSequence.CompleteOrders " + __instance.owningActor.DisplayName + ":" + __instance.owningActor.GUID);
      try {
        ICombatant DFATarget = (ICombatant)typeof(MechJumpSequence).GetField("DFATarget", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance);
        DynamicMapHelper.calculateJumpDamage(__instance.owningActor, __instance.FinalPos);
        if (DFATarget == null) {
          Log.F.WL(1, "not DFA");
          if (DynamicMapHelper.registredMineFieldDamage.ContainsKey(__instance.owningActor) == false) { return; };
          MineFieldDamage mfDamage = DynamicMapHelper.registredMineFieldDamage[__instance.owningActor];
          mfDamage.resolveMineFiledDamage(__instance.owningActor, __instance.SequenceGUID);
          DynamicMapHelper.registredMineFieldDamage.Remove(__instance.owningActor);
        } else {
          Log.F.WL(1, "DFA");
        }
      } catch (Exception e) {
        Log.F.TWL(0, e.ToString(), true);
      }
    }
  }
  [HarmonyPatch(typeof(MechDFASequence))]
  [HarmonyPatch("CompleteOrders")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { })]
  public static class MechDFASequence_CompleteOrders {
    private static void Postfix(MechDFASequence __instance) {
      Log.F.TWL(0, "MechDFASequence.CompleteOrders" + __instance.owningActor.DisplayName + ":" + __instance.owningActor.GUID);
      try {
        if (DynamicMapHelper.registredMineFieldDamage.ContainsKey(__instance.owningActor) == false) { return; };
        MineFieldDamage mfDamage = DynamicMapHelper.registredMineFieldDamage[__instance.owningActor];
        mfDamage.resolveMineFiledDamage(__instance.owningActor, __instance.SequenceGUID);
        DynamicMapHelper.registredMineFieldDamage.Remove(__instance.owningActor);
      } catch (Exception e) {
        Log.F.TWL(0, e.ToString(), true);
      }
    }
  }
  /*
  [HarmonyPatch(typeof(MechMeleeSequence))]
  [HarmonyPatch("GenerateMeleePath")]
  [HarmonyPatch(MethodType.Normal)]
  public static class MechMeleeSequence_GenerateMeleePath {
    private static void Postfix(MechMeleeSequence __instance) {
      CustomAmmoCategoriesLog.Log.LogWrite("MechMeleeSequence.GenerateMeleePath.\n");
      ActorMovementSequence moveSequence = (ActorMovementSequence)typeof(MechMeleeSequence).GetField("moveSequence", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance);
      List<WayPoint> Waypoints = (List<WayPoint>)typeof(ActorMovementSequence).GetProperty("Waypoints", BindingFlags.Instance | BindingFlags.NonPublic).GetGetMethod(true).Invoke(moveSequence, new object[0] { });
      MineFieldHelper.registerMovingDamageFromPath(__instance.OwningMech, Waypoints);
      if (__instance.OwningMech.hasRegistredMovingDamage()) {
        CustomAmmoCategoriesLog.Log.LogWrite(" damaged in movement. Melee animation interrupt\n");
        __instance.owningActor.Combat.MessageCenter.PublishMessage((MessageCenterMessage)new FloatieMessage(__instance.owningActor.GUID, __instance.owningActor.GUID, "MELEE ATTACK INTERUPTED", FloatieMessage.MessageNature.CriticalHit));
        typeof(ActorMovementSequence).GetField("meleeTarget", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(moveSequence, null);
      }
    }
  }
  [HarmonyPatch(typeof(ActorMovementSequence))]
  [HarmonyPatch("CompleteOrders")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { })]
  public static class ActorMovementSequence_CompleteOrders {
    private static void Postfix(ActorMovementSequence __instance) {
      CustomAmmoCategoriesLog.Log.LogWrite("ActorMovementSequence.CompleteOrders " + __instance.meleeType + "\n");
      if (__instance.meleeType == MeleeAttackType.NotSet) {
        CustomAmmoCategoriesLog.Log.LogWrite(" not meele\n");
        List<WayPoint> Waypoints = (List<WayPoint>)typeof(ActorMovementSequence).GetProperty("Waypoints", BindingFlags.Instance | BindingFlags.NonPublic).GetGetMethod(true).Invoke(__instance, new object[0] { });
        MineFieldHelper.registerMovingDamageFromPath(__instance.OwningActor, Waypoints);
        if (__instance.OwningMech != null) { MineFieldHelper.inflictRegistredMovingDamageMech(__instance.OwningMech); };
        if (__instance.OwningVehicle != null) { MineFieldHelper.inflictRegistredMovingDamageVehicle(__instance.OwningVehicle); }
      } else {
        CustomAmmoCategoriesLog.Log.LogWrite(" meele\n");
      }
    }
  }
  [HarmonyPatch(typeof(MechJumpSequence))]
  [HarmonyPatch("CompleteOrders")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { })]
  public static class MechJumpSequence_CompleteOrders {
    private static void Postfix(MechJumpSequence __instance) {
      CustomAmmoCategoriesLog.Log.LogWrite("MechJumpSequence.CompleteOrders\n");
      ICombatant DFATarget = (ICombatant)typeof(MechJumpSequence).GetField("DFATarget", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance);
      MineFieldHelper.registerJumpingDamageFrom(__instance.OwningMech, __instance.FinalPos);
      if (DFATarget == null) {
        CustomAmmoCategoriesLog.Log.LogWrite(" not DFA\n");
        if (__instance.OwningMech != null) { MineFieldHelper.inflictRegistredMovingDamageMech(__instance.OwningMech); };
      } else {
        CustomAmmoCategoriesLog.Log.LogWrite(" DFA\n");
      }
    }
  }
  [HarmonyPatch(typeof(MechDFASequence))]
  [HarmonyPatch("CompleteOrders")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { })]
  public static class MechDFASequence_CompleteOrders {
    private static void Postfix(MechDFASequence __instance) {
      CustomAmmoCategoriesLog.Log.LogWrite("MechDFASequence.CompleteOrders\n");
      MineFieldHelper.inflictRegistredMovingDamageMech(__instance.OwningMech);
    }
  }*/
}