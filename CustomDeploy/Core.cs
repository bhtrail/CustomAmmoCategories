﻿using BattleTech;
using BattleTech.Assetbundles;
using BattleTech.Data;
using BattleTech.ModSupport.Utils;
using BattleTech.Rendering.UI;
using BattleTech.Rendering.UrbanWarfare;
using BattleTech.StringInterpolation;
using BattleTech.UI;
using Harmony;
using HBS;
using HBS.Collections;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityHeapCrawler;

namespace CustomDeploy{
  public static class Log {
    //private static string m_assemblyFile;
    private static string m_logfile;
    private static readonly Mutex mutex = new Mutex();
    public static string BaseDirectory;
    private static StringBuilder m_cache = new StringBuilder();
    private static StreamWriter m_fs = null;
    private static readonly int flushBufferLength = 16 * 1024;
    public static bool flushThreadActive = true;
    public static Thread flushThread = new Thread(flushThreadProc);
    public static void flushThreadProc() {
      while (Log.flushThreadActive == true) {
        Thread.Sleep(10 * 1000);
        Log.LogWrite("flush\n");
        Log.flush();
      }
    }
    public static void InitLog() {
      Log.m_logfile = Path.Combine(BaseDirectory, "CustomDeploy.log");
      File.Delete(Log.m_logfile);
      Log.m_fs = new StreamWriter(Log.m_logfile);
      Log.m_fs.AutoFlush = true;
      Log.flushThread.Start();
    }
    public static void flush() {
      if (Log.mutex.WaitOne(1000)) {
        Log.m_fs.Write(Log.m_cache.ToString());
        Log.m_fs.Flush();
        Log.m_cache.Length = 0;
        Log.mutex.ReleaseMutex();
      }
    }
    public static void LogWrite(int initiation, string line, bool eol = false, bool timestamp = false, bool isCritical = false) {
      string init = new string(' ', initiation);
      string prefix = String.Empty;
      if (timestamp) { prefix = DateTime.Now.ToString("[HH:mm:ss.fff]"); }
      if (initiation > 0) { prefix += init; };
      if (eol) {
        LogWrite(prefix + line + "\n", isCritical);
      } else {
        LogWrite(prefix + line, isCritical);
      }
    }
    public static void LogWrite(string line, bool isCritical = false) {
      //try {
      if ((Core.debugLog) || (isCritical)) {
        if (Log.mutex.WaitOne(1000)) {
          m_cache.Append(line);
          //File.AppendAllText(Log.m_logfile, line);
          Log.mutex.ReleaseMutex();
        }
        if (isCritical) { Log.flush(); };
        if (m_logfile.Length > Log.flushBufferLength) { Log.flush(); };
      }
      //} catch (Exception) {
      //i'm sertanly don't know what to do
      //}
    }
    public static void W(string line, bool isCritical = false) {
      LogWrite(line, isCritical);
    }
    public static void WL(string line, bool isCritical = false) {
      line += "\n"; W(line, isCritical);
    }
    public static void W(int initiation, string line, bool isCritical = false) {
      string init = new string(' ', initiation);
      line = init + line; W(line, isCritical);
    }
    public static void WL(int initiation, string line, bool isCritical = false) {
      string init = new string(' ', initiation);
      line = init + line; WL(line, isCritical);
    }
    public static void TW(int initiation, string line, bool isCritical = false) {
      string init = new string(' ', initiation);
      line = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "]" + init + line;
      W(line, isCritical);
    }
    public static void TWL(int initiation, string line, bool isCritical = false) {
      string init = new string(' ', initiation);
      line = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "]" + init + line;
      WL(line, isCritical);
    }
  }
  [HarmonyPatch(typeof(Briefing))]
  [HarmonyPatch("BeginPlaying")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { })]
  public static class Briefing_BeginPlaying {
    public static bool Prefix(Briefing __instance) {
      try {
        return true;
      } catch (Exception e) {
        Log.TWL(0, e.ToString());
        return true;
      }
    }
  }
  [HarmonyPatch(typeof(Interpolator))]
  [HarmonyPatch("GetStringFromObjectDispatch")]
  [HarmonyPatch(MethodType.Normal)]
  public static class Interpolator_GetStringFromObjectDispatch {
    private static string GetStringFromObjectDispatch_Local(object obj, string expr) {
      int num = expr.IndexOf('?');
      string text = expr.Substring(0, num);
      string text2 = expr.Substring(num + 1).Trim();
      List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
      string[] array = text2.Split(new char[]
      {
        '|'
      });
      for (int i = 0; i < array.Length; i++) {
        int num2 = array[i].IndexOf(':');
        string key = array[i].Substring(0, num2).Trim();
        string value = array[i].Substring(num2 + 1);
        list.Add(new KeyValuePair<string, string>(key, value));
      }
      object obj2 = null;
      try {
        obj2 = Interpolator.GetObjectByStringFromObject(text, obj);
      } catch (Exception arg) {
        string result = string.Format("ERROR: resolving '{0}' on {1}\n{2}", expr, obj, arg);
        Log.TWL(0, result + "\n" + arg.ToString(), true);
        return result;
      }
      if (obj2 == null) {
        return string.Format("ERROR: can't resolve '{0}' on {1}. Null value.", text, obj);
      }
      string a = obj2.ToString();
      for (int j = 0; j < list.Count; j++) {
        if (a == list[j].Key || list[j].Key == "Default") {
          return list[j].Value;
        }
      }
      return string.Format("ERROR: Could not resolve '{0}'", expr);
    }
    public static bool Prefix(ref string __result, object obj, string expr) {
      try {
        __result = GetStringFromObjectDispatch_Local(obj, expr);
        return false;
      } catch (Exception e) {
        Log.TWL(0, e.ToString());
        return true;
      }
    }
  }
  [HarmonyPatch(typeof(Interpolator))]
  [HarmonyPatch("LookupStringFromObjectAndMaybeDispatch")]
  [HarmonyPatch(MethodType.Normal)]
  public static class Interpolator_LookupStringFromObjectAndMaybeDispatch {
    private static string LookupStringFromObjectAndMaybeDispatch_Local(object obj, string expr, out bool dispatchLocalize) {
      if (expr.IndexOf('?') != -1) {
        dispatchLocalize = false;
        return Interpolator.GetStringFromObjectDispatch(obj, expr);
      }
      dispatchLocalize = true;
      string result;
      try {
        object objectByStringFromObject = Interpolator.GetObjectByStringFromObject(expr, obj);
        if (objectByStringFromObject != null) {
          result = objectByStringFromObject.ToString();
        } else {
          result = string.Format("ERROR: can not resolve '{0}' on {1}. Null value.", expr, obj);
        }
      } catch (Exception ex) {
        result = string.Format("ERROR: resolving '{0}' on {1}. {2}", expr, obj, ex.Message);
        Log.TWL(0, result + "\n"+ ex.ToString(), true);
      }
      return result;
    }

    public static bool Prefix(ref string __result,object obj, string expr, ref bool dispatchLocalize) {
      try {
        __result = LookupStringFromObjectAndMaybeDispatch_Local(obj, expr, out dispatchLocalize);
        return false;
      } catch (Exception e) {
        Log.TWL(0, e.ToString());
        return true;
      }
    }
  }
  [HarmonyPatch(typeof(HeapSnapshotCollector))]
  [HarmonyPatch("CollectStaticFields")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { })]
  public static class HeapSnapshotCollector_CollectStaticFields {
    private static void CollectStaticFields_Local(this HeapSnapshotCollector __instance) {
      IEnumerable<Type> second = AppDomain.CurrentDomain.GetAssemblies().Where(new Func<Assembly, bool>(HeapSnapshotCollector.IsValidAssembly)).SelectMany((Assembly a) => a.GetTypesSafe());
      IEnumerable<Type> enumerable = __instance.staticTypes.Concat(second);
      HashSet<string> hashSet = new HashSet<string>();
      foreach (Type type in enumerable) {
        try {
          __instance.AddStaticFields(type, hashSet);
        } catch (Exception exception) {
          Debug.LogException(exception);
        }
      }
      if (hashSet.Count > 0) {
        List<string> list = hashSet.ToList<string>();
        list.Sort();
        using (StreamWriter streamWriter = new StreamWriter(__instance.outputDir + "generic-static-fields.txt")) {
          foreach (string value in list) {
            streamWriter.WriteLine(value);
          }
        }
      }
    }
    public static bool Prefix(HeapSnapshotCollector __instance) {
      try {
        __instance.CollectStaticFields_Local();
        return false;
      } catch (Exception e) {
        Log.TWL(0, e.ToString());
        return true;
      }
    }
  }
  [HarmonyPatch(typeof(AssemblyUtil))]
  [HarmonyPatch("FindMethods")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { typeof(Assembly), typeof(string), typeof(string) })]
  public static class AssemblyUtil_FindMethods {
    public static MethodInfo[] FindMethods_Local(Assembly assembly, string methodName, string typeName = null) {
      List<Type> list = new List<Type>();
      if (typeName == null) {
        list.AddRange(from x in assembly.GetTypesSafe()
                      where x.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public) != null
                      select x);
      } else {
        list.Add(assembly.GetType(typeName));
      }
      if (list.Count == 0) {
        return null;
      }
      List<MethodInfo> list2 = new List<MethodInfo>();
      foreach (Type type in list) {
        MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
        list2.Add(method);
      }
      return list2.ToArray();
    }
    public static bool Prefix(ref MethodInfo[] __result, Assembly assembly, string methodName, string typeName) {
      try {
        __result = FindMethods_Local(assembly, methodName, typeName);
        return false;
      } catch (Exception e) {
        Log.TWL(0, e.ToString());
        return true;
      }
    }
  }
  [HarmonyPatch(typeof(AkTriggerBase))]
  [HarmonyPatch("GetAllDerivedTypes")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { })]
  public static class AkTriggerBase_GetAllDerivedTypes {
    public static Dictionary<uint, string> GetAllDerivedTypes_Local() {
      Type typeFromHandle = typeof(AkTriggerBase);
      Type[] types = typeFromHandle.Assembly.GetTypesSafe();
      Dictionary<uint, string> dictionary = new Dictionary<uint, string>();
      for (int i = 0; i < types.Length; i++) {
        if (types[i].IsClass && (types[i].IsSubclassOf(typeFromHandle) || (typeFromHandle.IsAssignableFrom(types[i]) && typeFromHandle != types[i]))) {
          string name = types[i].Name;
          dictionary.Add(AkUtilities.ShortIDGenerator.Compute(name), name);
        }
      }
      dictionary.Add(AkUtilities.ShortIDGenerator.Compute("Awake"), "Awake");
      dictionary.Add(AkUtilities.ShortIDGenerator.Compute("Start"), "Start");
      dictionary.Add(AkUtilities.ShortIDGenerator.Compute("Destroy"), "Destroy");
      return dictionary;
    }
    public static bool Prefix(ref Dictionary<uint, string> __result) {
      try {
        __result = GetAllDerivedTypes_Local();
        return false;
      } catch (Exception e) {
        Log.TWL(0, e.ToString());
        return true;
      }
    }
  }
  [HarmonyPatch(typeof(Utilities))]
  [HarmonyPatch("BuildExtensionMethodCacheForType")]
  [HarmonyPatch(MethodType.Normal)]
  public static class Utilities_BuildExtensionMethodCacheForType {
    public static bool Prefix(System.Type type) {
      try {
        if (Utilities.extensionMethodsCache.ContainsKey(type)) { return false; }
        List<MethodInfo> methodInfoList = new List<MethodInfo>();
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
          try {
            foreach (System.Type type1 in ((IEnumerable<System.Type>)assembly.GetTypes()).Where<System.Type>((Func<System.Type, bool>)(t => t.IsSealed && !t.IsGenericType && !t.IsNested))) {
              foreach (MethodInfo method in type1.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)) {
                if (method.IsDefined(typeof(ExtensionAttribute), false) && method.GetParameters()[0].ParameterType == type)
                  methodInfoList.Add(method);
              }
            }
          }catch(Exception e) {
            Log.TWL(0, "Harmless exception. Just for log:" + assembly.FullName + "\n" + e.ToString(), true);
          }
        }
        Utilities.extensionMethodsCache[type] = methodInfoList;
        return false;
      } catch (Exception e) {
        Log.TWL(0, e.ToString());
        return true;
      }
    }
  }
  [HarmonyPatch(typeof(SimGameState))]
  [HarmonyPatch("BuildSimGameResults")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { typeof(SimGameEventResult[]), typeof(GameContext), typeof(SimGameStatDescDef.DescriptionTense?), typeof(Pilot) })]
  public static class Briefing_BuildSimGameResults {
    public static List<ResultDescriptionEntry> BuildSimGameStatsResultsLocal(this SimGameState __instance, SimGameStat[] stats,GameContext context,SimGameStatDescDef.DescriptionTense tense,string prefix = "•") {
      List<ResultDescriptionEntry> descriptionEntryList = new List<ResultDescriptionEntry>();
      foreach (SimGameStat stat in stats) {
        if (!string.IsNullOrEmpty(stat.name) && stat.value != null) {
          SimGameStatDescDef simGameStatDescDef = (SimGameStatDescDef)null;
          GameContext context1 = new GameContext(context);
          if (__instance.DataManager.SimGameStatDescDefs.Exists("SimGameStatDesc_" + stat.name)) {
            simGameStatDescDef = __instance.DataManager.GetStatDescDef(stat);
          } else {
            int length = stat.name.IndexOf('.');
            if (length >= 0) {
              string str1 = stat.name.Substring(0, length);
              if (__instance.DataManager.SimGameStatDescDefs.Exists("SimGameStatDesc_" + str1)) {
                simGameStatDescDef = __instance.DataManager.SimGameStatDescDefs.Get("SimGameStatDesc_" + str1);
                string[] strArray = stat.name.Split('.');
                BattleTechResourceType? nullable = new BattleTechResourceType?();
                object obj = (object)null;
                string id;
                if (strArray.Length < 3) {
                  if (str1 == "Reputation") {
                    id = "faction_" + strArray[1];
                    nullable = new BattleTechResourceType?(BattleTechResourceType.FactionDef);
                  } else {
                    id = (string)null;
                    nullable = new BattleTechResourceType?();
                  }
                } else {
                  string str2 = strArray[1];
                  id = strArray[2];
                  try {
                    nullable = new BattleTechResourceType?((BattleTechResourceType)Enum.Parse(typeof(BattleTechResourceType), str2));
                  } catch {
                    nullable = new BattleTechResourceType?();
                  }
                }
                if (nullable.HasValue)
                  obj = __instance.DataManager.Get(nullable.Value, id);
                if (obj != null)
                  context1.SetObject(GameContextObjectTagEnum.ResultObject, obj);
                else
                  simGameStatDescDef = (SimGameStatDescDef)null;
              }
            }
          }
          if (simGameStatDescDef != null) {
            if (!simGameStatDescDef.hidden) {
              context1.SetObject(GameContextObjectTagEnum.ResultValue, (object)Mathf.Abs(stat.ToSingle()));
              if (stat.set) {
                string str = !(stat.Type == typeof(bool)) ? Interpolator.Interpolate(simGameStatDescDef.GetResultString(SimGameStatDescDef.DescriptionType.Set, tense), context1) : (!stat.ToBool() ? Interpolator.Interpolate(simGameStatDescDef.GetResultString(SimGameStatDescDef.DescriptionType.Negative, tense), context1) : Interpolator.Interpolate(simGameStatDescDef.GetResultString(SimGameStatDescDef.DescriptionType.Positive, tense), context1));
                if (!string.IsNullOrEmpty(str)) {
                  descriptionEntryList.Add(new ResultDescriptionEntry(new Localize.Text("{0} {1}\n", new object[2]
                  {
                    (object) prefix,
                    (object) str
                  }), context1, stat.name));
                  Log.WL(1, "A - descriptionEntryList:"+str);
                }
              } else if (stat.Type == typeof(int) || stat.Type == typeof(float)) {
                string str = (string)null;
                if ((double)stat.ToSingle() > 0.0)
                  str = Interpolator.Interpolate(simGameStatDescDef.GetResultString(SimGameStatDescDef.DescriptionType.Positive, tense), context1);
                else if ((double)stat.ToSingle() < 0.0)
                  str = Interpolator.Interpolate(simGameStatDescDef.GetResultString(SimGameStatDescDef.DescriptionType.Negative, tense), context1);
                if (!string.IsNullOrEmpty(str)) {
                  descriptionEntryList.Add(new ResultDescriptionEntry(new Localize.Text("{0} {1}\n", new object[2]
                  {
                    (object) prefix,
                    (object) str
                  }), context1, stat.name));
                  Log.WL(1, "B - descriptionEntryList:" + str);
                }
              }
            }
          } else {
            string tooltipString = __instance.DataManager.GetTooltipString(stat);
            descriptionEntryList.Add(new ResultDescriptionEntry(new Localize.Text("{0} {1} {2}\n", new object[3]
            {
              (object) prefix,
              (object) tooltipString,
              (object) stat.value
            }), context1, stat.name));
            Log.WL(1, "C - descriptionEntryList:" + tooltipString);
          }
        }
      }
      return descriptionEntryList;
    }
    public static List<ResultDescriptionEntry> BuildSimGameActionStringLocal(this SimGameState __instance, SimGameResultAction[] actions,GameContext context,SimGameStatDescDef.DescriptionTense tense,string prefix = "•") {
      List<ResultDescriptionEntry> descriptionEntryList = new List<ResultDescriptionEntry>();
      foreach (SimGameResultAction action in actions) {
        string id1 = "SimGameStatDesc_SimGameResultAction_" + (object)action.Type;
        if (__instance.DataManager.SimGameStatDescDefs.Exists(id1)) {
          SimGameStatDescDef simGameStatDescDef = __instance.DataManager.SimGameStatDescDefs.Get(id1);
          if (simGameStatDescDef != null && !simGameStatDescDef.hidden) {
            GameContext context1 = new GameContext(context);
            context1.SetObject(GameContextObjectTagEnum.ResultValue, (object)action.value);
            if (action.additionalValues != null) {
              for (int index = 0; index < action.additionalValues.Length; ++index) {
                string additionalValue = action.additionalValues[index];
                string id2 = string.Format("faction_{0}", (object)additionalValue);
                string key = additionalValue.StartsWith("starsystemdef_") ? additionalValue : "starsystemdef_" + additionalValue;
                if (__instance.DataManager.Factions.Exists(id2)) {
                  FactionDef factionDef = __instance.DataManager.Factions.Get(id2);
                  context1.SetObject(GameContextObjectTagEnum.ResultFaction, (object)factionDef);
                } else if (__instance.starDict.ContainsKey(key))
                  context1.SetObject(GameContextObjectTagEnum.ResultSystem, (object)__instance.starDict[key]);
              }
            }
            bool result = false;
            string str = !bool.TryParse(action.value, out result) ? Interpolator.Interpolate(simGameStatDescDef.GetResultString(SimGameStatDescDef.DescriptionType.Set, tense), context1) : (!result ? Interpolator.Interpolate(simGameStatDescDef.GetResultString(SimGameStatDescDef.DescriptionType.Negative, tense), context1) : Interpolator.Interpolate(simGameStatDescDef.GetResultString(SimGameStatDescDef.DescriptionType.Positive, tense), context1));
            descriptionEntryList.Add(new ResultDescriptionEntry(string.Format("{0} {1}{2}", (object)prefix, (object)str, (object)Environment.NewLine), context1));
          }
        }
      }
      return descriptionEntryList;
    }

    public static List<ResultDescriptionEntry> BuildSimGameResultsLocal(this SimGameState __instance, SimGameEventResult[] resultsList,GameContext context,SimGameStatDescDef.DescriptionTense? tenseOverride,Pilot pilotOverride) {
      Log.TWL(0, "SimGameState.BuildSimGameResults",true);
      List<ResultDescriptionEntry> descriptionEntryList1 = new List<ResultDescriptionEntry>();
      if (resultsList != null) {
        TagDataStructFetcher dataStructFetcher = __instance.Context.GetObject(GameContextObjectTagEnum.TagDataStructFetcher) as TagDataStructFetcher;
        foreach (SimGameEventResult results in resultsList) {
          GameContext context1 = new GameContext(context);
          TagSet tagSet = (TagSet)null;
          Pilot pilot = (Pilot)null;
          MechDef mechDef = (MechDef)null;
          StarSystem starSystem = (StarSystem)null;
          if (pilotOverride != null) {
            pilot = pilotOverride;
            context1.SetObject(GameContextObjectTagEnum.ResultMechWarrior, (object)pilot);
            tagSet = pilot.pilotDef.PilotTags;
          } else {
            switch (results.Scope) {
              case EventScope.Company:
              tagSet = __instance.companyTags;
              break;
              case EventScope.MechWarrior:
              pilot = context1.GetObject(GameContextObjectTagEnum.TargetMechWarrior) as Pilot;
              context1.SetObject(GameContextObjectTagEnum.ResultMechWarrior, (object)pilot);
              tagSet = pilot.pilotDef.PilotTags;
              break;
              case EventScope.Mech:
              mechDef = context1.GetObject(GameContextObjectTagEnum.TargetUnit) as MechDef;
              context1.SetObject(GameContextObjectTagEnum.ResultMech, (object)mechDef);
              tagSet = mechDef.MechTags;
              break;
              case EventScope.Commander:
              pilot = __instance.Commander;
              context1.SetObject(GameContextObjectTagEnum.ResultMechWarrior, (object)__instance.Commander);
              tagSet = __instance.commander.pilotDef.PilotTags;
              break;
              case EventScope.StarSystem:
              starSystem = context1.GetObject(GameContextObjectTagEnum.TargetStarSystem) as StarSystem;
              context1.SetObject(GameContextObjectTagEnum.ResultSystem, (object)starSystem);
              tagSet = starSystem.Tags;
              break;
              case EventScope.SecondaryMechWarrior:
              pilot = context1.GetObject(GameContextObjectTagEnum.SecondaryMechWarrior) as Pilot;
              context1.SetObject(GameContextObjectTagEnum.ResultMechWarrior, (object)pilot);
              tagSet = pilot.pilotDef.PilotTags;
              break;
              case EventScope.SecondaryMech:
              mechDef = context1.GetObject(GameContextObjectTagEnum.SecondaryUnit) as MechDef;
              context1.SetObject(GameContextObjectTagEnum.ResultMech, (object)mechDef);
              tagSet = mechDef.MechTags;
              break;
              case EventScope.TertiaryMechWarrior:
              pilot = context1.GetObject(GameContextObjectTagEnum.TertiaryMechWarrior) as Pilot;
              context1.SetObject(GameContextObjectTagEnum.ResultMechWarrior, (object)pilot);
              tagSet = pilot.pilotDef.PilotTags;
              break;
              case EventScope.RandomMech:
              mechDef = context1.GetObject(GameContextObjectTagEnum.RandomUnit) as MechDef;
              context1.SetObject(GameContextObjectTagEnum.ResultMech, (object)mechDef);
              tagSet = mechDef.MechTags;
              break;
            }
          }
          if (results.TemporaryResult)
            context1.SetObject(GameContextObjectTagEnum.ResultDuration, (object)results.ResultDuration);
          if (results.AddedTags != null && tagSet != null) {
            List<string> stringList = new List<string>();
            foreach (string addedTag in results.AddedTags) {
              TagDataStruct tagDataStruct = dataStructFetcher.GetItem(addedTag, false);
              if (tagDataStruct != null && tagDataStruct.IsVisible && !string.IsNullOrEmpty(tagDataStruct.FriendlyName))
                stringList.Add(tagDataStruct.ToToolTipString().ToString(true));
            }
            if (stringList.Count > 0) {
              string str1 = pilot == null ? (mechDef == null ? (starSystem == null ? (!tenseOverride.HasValue || tenseOverride.Value != SimGameStatDescDef.DescriptionTense.TemporalEnd ? (!results.TemporaryResult ? Localize.Strings.T(__instance.Constants.Story.TagAddedCompany) : Interpolator.Interpolate(__instance.Constants.Story.TagAddedCompanyTemp, context1)) : Interpolator.Interpolate(__instance.Constants.Story.TagAddedCompanyTempEnd, context1)) : (!tenseOverride.HasValue || tenseOverride.Value != SimGameStatDescDef.DescriptionTense.TemporalEnd ? (!results.TemporaryResult ? Interpolator.Interpolate(__instance.Constants.Story.TagAddedSystem, context1) : Interpolator.Interpolate(__instance.Constants.Story.TagAddedSystemTemp, context1)) : Interpolator.Interpolate(__instance.Constants.Story.TagAddedSystemTempEnd, context1))) : (!tenseOverride.HasValue || tenseOverride.Value != SimGameStatDescDef.DescriptionTense.TemporalEnd ? (!results.TemporaryResult ? Interpolator.Interpolate(__instance.Constants.Story.TagAddedMech, context1) : Interpolator.Interpolate(__instance.Constants.Story.TagAddedMechTemp, context1)) : Interpolator.Interpolate(__instance.Constants.Story.TagAddedMechTempEnd, context1))) : (!tenseOverride.HasValue || tenseOverride.Value != SimGameStatDescDef.DescriptionTense.TemporalEnd ? (!results.TemporaryResult ? Interpolator.Interpolate(__instance.Constants.Story.TagAddedMW, context1) : Interpolator.Interpolate(__instance.Constants.Story.TagAddedMWTemp, context1)) : Interpolator.Interpolate(__instance.Constants.Story.TagAddedMWTempEnd, context1));
              foreach (string str2 in stringList) {
                descriptionEntryList1.Add(new ResultDescriptionEntry(new Localize.Text("{1} {2} {3}{0}", new object[4]
                {
                  (object) Environment.NewLine,
                  (object) "•",
                  (object) str1,
                  (object) str2
                }), context1));
                Log.WL(1, "descriptionEntryList1 '" + str1 + "' '" + str2 + "'");
              }
            }
          }
          if (results.RemovedTags != null && tagSet != null) {
            List<string> stringList = new List<string>();
            foreach (string removedTag in results.RemovedTags) {
              TagDataStruct tagDataStruct = dataStructFetcher.GetItem(removedTag, false);
              if (tagDataStruct != null && tagDataStruct.IsVisible && !string.IsNullOrEmpty(tagDataStruct.FriendlyName))
                stringList.Add(tagDataStruct.ToToolTipString().ToString(true));
            }
            if (stringList.Count > 0) {
              string str1 = pilot == null ? (mechDef == null ? (starSystem == null ? (!results.TemporaryResult ? Localize.Strings.T(__instance.Constants.Story.TagRemovedCompany) : Interpolator.Interpolate(__instance.Constants.Story.TagRemovedCompanyTemp, context1)) : (!results.TemporaryResult ? Interpolator.Interpolate(__instance.Constants.Story.TagRemovedSystem, context1) : Interpolator.Interpolate(__instance.Constants.Story.TagRemovedSystemTemp, context1))) : (!results.TemporaryResult ? Interpolator.Interpolate(__instance.Constants.Story.TagRemovedMech, context1) : Interpolator.Interpolate(__instance.Constants.Story.TagRemovedMechTemp, context1))) : (!results.TemporaryResult ? Interpolator.Interpolate(__instance.Constants.Story.TagRemovedMW, context1) : Interpolator.Interpolate(__instance.Constants.Story.TagRemovedMWTemp, context1));
              foreach (string str2 in stringList) {
                descriptionEntryList1.Add(new ResultDescriptionEntry(string.Format("{1} {2} {3}{0}", (object)Environment.NewLine, (object)"•", (object)str1, (object)str2), context1));
              }
            }
          }
          if (results.Stats != null) {
            SimGameStatDescDef.DescriptionTense tense = SimGameStatDescDef.DescriptionTense.Default;
            if (tenseOverride.HasValue)
              tense = tenseOverride.Value;
            else if (results.TemporaryResult)
              tense = SimGameStatDescDef.DescriptionTense.Temporal;
            List<ResultDescriptionEntry> descriptionEntryList2 = __instance.BuildSimGameStatsResultsLocal(results.Stats, context1, tense);
            if (descriptionEntryList2 != null && descriptionEntryList2.Count > 0)
              descriptionEntryList1.AddRange((IEnumerable<ResultDescriptionEntry>)descriptionEntryList2);
          }
          if (results.Actions != null) {
            List<ResultDescriptionEntry> descriptionEntryList2 = __instance.BuildSimGameActionString(results.Actions, context1, SimGameStatDescDef.DescriptionTense.Default);
            if (descriptionEntryList2 != null && descriptionEntryList2.Count > 0)
              descriptionEntryList1.AddRange((IEnumerable<ResultDescriptionEntry>)descriptionEntryList2);
          }
        }
      }
      return descriptionEntryList1;
    }
    public static bool Prefix(SimGameState __instance, SimGameEventResult[] resultsList, GameContext context,SimGameStatDescDef.DescriptionTense? tenseOverride,Pilot pilotOverride, ref List<ResultDescriptionEntry> __result) {
      try {
        __result = __instance.BuildSimGameResultsLocal(resultsList, context, tenseOverride, pilotOverride);
        return false;
      } catch (Exception e) {
        Log.TWL(0, e.ToString());
        return true;
      }
    }
  }
  public static class MechInitGameRepHelper {
    public static void InitGameRepLocal(this Mech __instance,Transform parentTransform) {
      try {
        if (__instance == null) { Log.TWL(0, "Mech.InitGameRepLocal mech is null");  return;  }
        Log.TWL(0, "Mech.InitGameRepLocal "+__instance.PilotableActorDef.Description.Id);
        string prefabIdentifier = __instance.MechDef.Chassis.PrefabIdentifier;
        GameObject gameObject = __instance.Combat.DataManager.PooledInstantiate(prefabIdentifier, BattleTechResourceType.Prefab);
        __instance._gameRep = (GameRepresentation)gameObject.GetComponent<MechRepresentation>();
        gameObject.GetComponent<Animator>().enabled = true;
        __instance.GameRep.Init(__instance, parentTransform, false);
        if ((UnityEngine.Object)parentTransform == (UnityEngine.Object)null) {
          gameObject.transform.position = __instance.currentPosition;
          gameObject.transform.rotation = __instance.currentRotation;
        }
        List<string> usedPrefabNames = new List<string>();
        foreach (MechComponent allComponent in __instance.allComponents) {
          if (allComponent.componentType != ComponentType.Weapon) {
            allComponent.baseComponentRef.prefabName = MechHardpointRules.GetComponentPrefabName(__instance.MechDef.Chassis.HardpointDataDef, allComponent.baseComponentRef, __instance.MechDef.Chassis.PrefabBase, allComponent.mechComponentRef.MountedLocation.ToString().ToLower(), ref usedPrefabNames);
            allComponent.baseComponentRef.hasPrefabName = true;
            if (!string.IsNullOrEmpty(allComponent.baseComponentRef.prefabName)) {
              Transform attachTransform = __instance.GetAttachTransform(allComponent.mechComponentRef.MountedLocation);
              allComponent.InitGameRep(allComponent.baseComponentRef.prefabName, attachTransform, __instance.LogDisplayName);
              __instance.GameRep.miscComponentReps.Add(allComponent.componentRep);
            }
          }
        }
        foreach (Weapon weapon in __instance.Weapons) {
          weapon.baseComponentRef.prefabName = MechHardpointRules.GetComponentPrefabName(__instance.MechDef.Chassis.HardpointDataDef, weapon.baseComponentRef, __instance.MechDef.Chassis.PrefabBase, weapon.mechComponentRef.MountedLocation.ToString().ToLower(), ref usedPrefabNames);
          weapon.baseComponentRef.hasPrefabName = true;
          if (!string.IsNullOrEmpty(weapon.baseComponentRef.prefabName)) {
            Transform attachTransform = __instance.GetAttachTransform(weapon.mechComponentRef.MountedLocation);
            weapon.InitGameRep(weapon.baseComponentRef.prefabName, attachTransform, __instance.LogDisplayName);
            __instance.GameRep.weaponReps.Add(weapon.weaponRep);
            string mountingPointPrefabName = MechHardpointRules.GetComponentMountingPointPrefabName(__instance.MechDef, weapon.mechComponentRef);
            if (!string.IsNullOrEmpty(mountingPointPrefabName)) {
              WeaponRepresentation component = __instance.Combat.DataManager.PooledInstantiate(mountingPointPrefabName, BattleTechResourceType.Prefab).GetComponent<WeaponRepresentation>();
              component.Init((ICombatant)__instance, attachTransform, true, __instance.LogDisplayName, weapon.Location);
              __instance.GameRep.weaponReps.Add(component);
            }
          }
        }
        foreach (MechComponent supportComponent in __instance.supportComponents) {
          if (supportComponent is Weapon weapon) {
            weapon.baseComponentRef.prefabName = MechHardpointRules.GetComponentPrefabName(__instance.MechDef.Chassis.HardpointDataDef, weapon.baseComponentRef, __instance.MechDef.Chassis.PrefabBase, weapon.mechComponentRef.MountedLocation.ToString().ToLower(), ref usedPrefabNames);
            weapon.baseComponentRef.hasPrefabName = true;
            if (!string.IsNullOrEmpty(weapon.baseComponentRef.prefabName)) {
              Transform attachTransform = __instance.GetAttachTransform(weapon.mechComponentRef.MountedLocation);
              weapon.InitGameRep(weapon.baseComponentRef.prefabName, attachTransform, __instance.LogDisplayName);
              __instance.GameRep.miscComponentReps.Add((ComponentRepresentation)weapon.weaponRep);
            }
          }
        }
        __instance.CreateBlankPrefabs(usedPrefabNames, ChassisLocations.CenterTorso);
        __instance.CreateBlankPrefabs(usedPrefabNames, ChassisLocations.LeftTorso);
        __instance.CreateBlankPrefabs(usedPrefabNames, ChassisLocations.RightTorso);
        __instance.CreateBlankPrefabs(usedPrefabNames, ChassisLocations.LeftArm);
        __instance.CreateBlankPrefabs(usedPrefabNames, ChassisLocations.RightArm);
        __instance.CreateBlankPrefabs(usedPrefabNames, ChassisLocations.Head);
        if (!__instance.MeleeWeapon.baseComponentRef.hasPrefabName) {
          __instance.MeleeWeapon.baseComponentRef.prefabName = "chrPrfWeap_generic_melee";
          __instance.MeleeWeapon.baseComponentRef.hasPrefabName = true;
        }
        __instance.MeleeWeapon.InitGameRep(__instance.MeleeWeapon.baseComponentRef.prefabName, __instance.GetAttachTransform(__instance.MeleeWeapon.mechComponentRef.MountedLocation), __instance.LogDisplayName);
        if (!__instance.DFAWeapon.mechComponentRef.hasPrefabName) {
          __instance.DFAWeapon.mechComponentRef.prefabName = "chrPrfWeap_generic_melee";
          __instance.DFAWeapon.mechComponentRef.hasPrefabName = true;
        }
        __instance.DFAWeapon.InitGameRep(__instance.DFAWeapon.mechComponentRef.prefabName, __instance.GetAttachTransform(__instance.DFAWeapon.mechComponentRef.MountedLocation), __instance.LogDisplayName);
        bool flag1 = __instance.MechDef.MechTags.Contains("PlaceholderUnfinishedMaterial");
        bool flag2 = __instance.MechDef.MechTags.Contains("PlaceholderImpostorMaterial");
        if (flag1 | flag2) {
          SkinnedMeshRenderer[] componentsInChildren = __instance.GameRep.GetComponentsInChildren<SkinnedMeshRenderer>(true);
          for (int index = 0; index < componentsInChildren.Length; ++index) {
            if (flag1)
              componentsInChildren[index].sharedMaterial = __instance.Combat.DataManager.TextureManager.PlaceholderUnfinishedMaterial;
            if (flag2)
              componentsInChildren[index].sharedMaterial = __instance.Combat.DataManager.TextureManager.PlaceholderImpostorMaterial;
          }
        }
        __instance.GameRep.RefreshEdgeCache();
        __instance.GameRep.FadeIn(1f);
        Log.WL(1, "GameRep inited successfully");
        if (__instance.IsDead || !__instance.Combat.IsLoadingFromSave)
          return;
        if (__instance.AuraComponents != null) {
          foreach (MechComponent auraComponent in __instance.AuraComponents) {
            for (int index = 0; index < auraComponent.componentDef.statusEffects.Length; ++index) {
              if (auraComponent.componentDef.statusEffects[index].targetingData.auraEffectType == AuraEffectType.ECM_GHOST) {
                __instance.GameRep.PlayVFXAt(__instance.GameRep.thisTransform, Vector3.zero, "vfxPrfPrtl_ECM_loop", true, Vector3.zero, false, -1f);
                __instance.GameRep.PlayVFXAt(__instance.GameRep.thisTransform, Vector3.zero, "vfxPrfPrtl_ECMcarrierAura_loop", true, Vector3.zero, false, -1f);
                return;
              }
            }
          }
        }
        if (__instance.VFXDataFromLoad != null) {
          foreach (VFXEffect.StoredVFXEffectData storedVfxEffectData in __instance.VFXDataFromLoad)
            __instance.GameRep.PlayVFXAt(__instance.GameRep.GetVFXTransform(storedVfxEffectData.hitLocation), storedVfxEffectData.hitPos, storedVfxEffectData.vfxName, storedVfxEffectData.isAttached, storedVfxEffectData.lookatPos, storedVfxEffectData.isOneShot, storedVfxEffectData.duration);
        }
      }catch(Exception e) {
        Log.TWL(0, e.ToString(), true);
      }

    }
  }
  public static class Core{
    public static System.Type[] GetTypesSafe(this Assembly assembly) {
      try {
        return assembly.GetTypes();
      } catch (ReflectionTypeLoadException e) {
        return e.Types.Where(x => x != null).ToArray();
      }
    }
    private static HashSet<string> PooledInstantiate_Fallback_tracked = new HashSet<string>();
    public static void PooledInstantiate_Fallback(this DataManager __instance,ref GameObject __result,string id,BattleTechResourceType resourceType,Vector3? position,Quaternion? rotation,Transform parent) {
      if (__result != null) { return; }
      if (resourceType != BattleTechResourceType.Prefab) { return; }
      if (PooledInstantiate_Fallback_tracked.Contains(id)) { return; }
      Log.TWL(0, "PooledInstantiate_Fallback: " + id + " result:" + (__result == null ? "null" : "not null"));
      PooledInstantiate_Fallback_tracked.Add(id);
      try {
        VersionManifestEntry entry = __instance.ResourceLocator.EntryByID(id, BattleTechResourceType.Prefab);
        if (entry == null) { Log.WL(1, "entry not found in manifest"); return; }
        if (string.IsNullOrEmpty(entry.AssetBundleName)) { Log.WL(1, "entry not asset bundled"); return; }
        VersionManifestEntry bundleEntry = __instance.ResourceLocator.EntryByID(entry.AssetBundleName, BattleTechResourceType.AssetBundle);
        if (entry == null) { Log.WL(1, "AssetBundle " + entry.AssetBundleName + " not found in manifest"); return; }
        if (__instance.AssetBundleManager.IsBundleLoaded(entry.AssetBundleName)) {
          Log.WL(1, "AssetBundle " + entry.AssetBundleName + " is already loaded");
          return;
        }
        AssetBundle bundle = AssetBundle.LoadFromFile(bundleEntry.FilePath);
        if (bundle == null) {
          Log.WL(1, "AssetBundle " + bundleEntry.FilePath + " fail to load");
          return;
        }
        AssetBundleTracker tracker = new AssetBundleTracker(bundle, false);
        tracker.ClearObjectMap();
        foreach (UnityEngine.Object allAsset in tracker.assetBundle.LoadAllAssets()) {
          System.Type type = allAsset.GetType();
          Dictionary<string, UnityEngine.Object> dictionary;
          if (!tracker.loadedObjects.TryGetValue(type, out dictionary)) {
            dictionary = new Dictionary<string, UnityEngine.Object>();
            tracker.loadedObjects[type] = dictionary;
          }
          if (!dictionary.ContainsKey(allAsset.name))
            dictionary.Add(allAsset.name, allAsset);
        }
        tracker.CurrentState = AssetBundleTracker.State.Ready;
        __instance.AssetBundleManager.loadedBundles.Add(entry.AssetBundleName, tracker);
        Log.WL(1, "AssetBundle " + bundleEntry.Name + " loaded. Request " + id + " again");
        __result = __instance.PooledInstantiate(id, BattleTechResourceType.Prefab, position, rotation, parent);
        Log.WL(1, "result:" + (__result == null ? "null" : "not null"));
      }catch(Exception e) {
        Log.TWL(0, e.ToString(), true);
      }
    }
    public static void FinishLoading() {
      Core.HarmonyInstance.Patch(typeof(DataManager).GetMethod("PooledInstantiate", BindingFlags.Public | BindingFlags.Instance),null, new HarmonyMethod(typeof(CustomDeploy.Core).GetMethod(nameof(CustomDeploy.Core.PooledInstantiate_Fallback), BindingFlags.Static | BindingFlags.Public)));
    }
    public static string BaseDir { get; set; }
    public static bool debugLog { get; set; }
    public static HarmonyInstance HarmonyInstance = null;
    public static void Init(string directory,bool debugLog) {
      Log.BaseDirectory = directory;
      Core.debugLog = debugLog;
      Log.InitLog();
      Core.BaseDir = directory;
      Log.TWL(0,"Initing... " + directory + " version: " + Assembly.GetExecutingAssembly().GetName().Version, true);
      try {
        HarmonyInstance = HarmonyInstance.Create("io.mission.customdeploy");
        HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
      } catch (Exception e) {
        Log.TWL(0,e.ToString(),true);
      }
    }
  }
}
 