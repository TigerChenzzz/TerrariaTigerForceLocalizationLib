using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;
using TigerForceLocalizationLib.Filters;
using TypeFilter = TigerForceLocalizationLib.Filters.TypeFilter;

namespace TigerForceLocalizationLib;

/// <summary>
/// 用以辅助本地化的一些方法
/// </summary>
public static class TigerForceLocalizationHelper {
    #region LocalizeAll, LocalizeMethod
    /// <summary>
    /// 筛选需要本地化的内容
    /// </summary>
    public class LocalizeFilters {
        /// <summary>
        /// 筛选一个类型
        /// </summary>
        public TypeFilter? TypeFilter { get; init; }
        /// <summary>
        /// 筛选一个方法
        /// </summary>
        public MethodFilter? MethodFilter { get; init; }
        /// <summary>
        /// 筛选特定字符串处的<see cref="ILCursor"/>
        /// 此时 <see cref="ILCursor.Next"/> 一般会指向一处 ldstr
        /// </summary>
        public ILCursorFilter? CursorFilter { get; init; }
    }
    /// <summary>
    /// <br/>以 hjson 的方式本地化一个模组中的所有方法
    /// <br/>在 PostSetup 阶段使用
    /// <para/>
    /// <br/>一对本地化字符串会存在 <paramref name="localizationRoot"/>.&lt;type.FullName>.&lt;method name>.&lt;i>.OldString/NewString 中
    /// <br/>"i"必须从 1 开始逐一增加, 不能间断
    /// <br/>如果有 NewString_&lt;j>, 那么代表方法中第 j 个出现的字符串会被替换为此字符串
    /// <br/>j 同样需从 1 开始, 逐一增加, 不能间断
    /// <br/>默认会将 OldString 对应的字符串替换为 NewString 或 NewString_1 所对应的字符串
    /// </summary>
    /// <param name="selfModName">
    /// <br/>本地化模组自身的名字
    /// <br/>用以自动设置本地化键名, 如果设置了 <paramref name="localizationRoot"/> 则此参数无效
    /// </param>
    /// <param name="modName">需本地化的目标模组的名字</param>
    /// <param name="registerKey">
    /// <br/>是否自动注册键
    /// <br/>如果将其设置为 true 那么初次加载时如果目标模组较大可能会在加载时和"添加合成配方"时卡较长的时间
    /// <br/>此时分别在添加 IL 钩子 和 注册本地化键
    /// <br/>在"添加合成配方"后如果 <paramref name="localizationRoot"/> 以 "Mods.&lt;<paramref name="selfModName"/>>" 开头
    /// <br/>那么本地化模组自身的 hjson 文件将会自动更新
    /// </param>
    /// <param name="localizationRoot">
    /// <br/>本地化键名的根
    /// <br/>默认为 "Mods.&lt;<paramref name="selfModName"/>>.ForceLocalizations"
    /// <br/>建议以 "Mods.&lt;<paramref name="selfModName"/>>" 开头
    /// <br/>实际的键名会是 &lt;<paramref name="localizationRoot"/>>.&lt;type.FullName>.&lt;method name>.&lt;序号>.OldString/NewString
    /// </param>
    /// <param name="useLocalizedText">
    /// <br/>是否使用 <see cref="LocalizedText.Value"/> 代替代码中的字符串
    /// <br/>否则直接使用字符串替换
    /// <br/>如果设置为 true 可能会稍微影响性能, 但是支持 hjson 热重载和多语言切换
    /// </param>
    /// <param name="filters">筛选需要本地化的内容</param>
    public static void LocalizeAll(string selfModName, string modName, bool registerKey = false, string? localizationRoot = null, bool useLocalizedText = true, LocalizeFilters? filters = null) {
        if (!ModLoader.TryGetMod(modName, out var mod)) {
            throw new Exception($"未找到模组\"{modName}\"!");
        }
        var jitFilter = mod.PreJITFilter;
        localizationRoot ??= $"Mods.{selfModName}.ForceLocalizations";
        #region 处理 filters
        var typeFilter = filters?.TypeFilter;
        bool ShouldJITRecursive(Type type) => jitFilter.ShouldJIT(type) && (type.DeclaringType is null || ShouldJITRecursive(type.DeclaringType));

        var types = AssemblyManager.GetModAssemblies(modName)
            .SelectMany(AssemblyManager.GetLoadableTypes)
            .Where(ShouldJITRecursive)
            .Where(t => !t.ContainsGenericParameters);
        if (typeFilter != null)
            types = types.Where(typeFilter.Filter);
        var methodFilter = filters?.MethodFilter;
        Func<MethodInfo, bool> GetMethodPredicate(Type type) => methodFilter == null
            ? method => method.DeclaringType == type
                    && !method.IsAbstract
                    && !method.ContainsGenericParameters
                    && jitFilter.ShouldJIT(method)
                    && method.GetMethodBody() != null
            : method => method.DeclaringType == type
                    && !method.IsAbstract
                    && !method.ContainsGenericParameters
                    && jitFilter.ShouldJIT(method)
                    && method.GetMethodBody() != null
                    && methodFilter.Filter(method);
        var cursorFilter = filters?.CursorFilter;
        #endregion
        foreach (var type in types) {
            Dictionary<string, int> methodCount = [];
            HashSet<string> usedMethodKey = [];
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(GetMethodPredicate(type)).ToArray();
            foreach (var method in methods) {
                if (!methodCount.TryAdd(method.Name, 1)) {
                    methodCount[method.Name] += 1;
                }
            }
            foreach (var method in methods) {
                var methodKey = GetMethodKey(type, method, methodCount[method.Name] > 1, localizationRoot, usedMethodKey.Contains);
                LocalizeMethod(method, methodKey, registerKey, useLocalizedText, cursorFilter);
            }
        }
    }

    private static string GetMethodKey(Type type, MethodInfo method, bool duplicated, string root, Func<string, bool>? checkMethodKeyUsed = null) {
        checkMethodKeyUsed ??= methodKey => Language.Exists($"{root}.{type.FullName}.{methodKey}");
        string methodKey;
        if (!duplicated) {
            methodKey = method.Name;
        }
        else {
            StringBuilder methodKeyBuilder = new();
            methodKeyBuilder.Append(method.Name).Append('_').AppendJoin('_', method.GetParameters().Select(p => p.ParameterType.Name));
            methodKey = methodKeyBuilder.ToString();
        }
        string realMethodKey = methodKey;
        for (int i = 2; checkMethodKeyUsed(realMethodKey); ++i) {
            realMethodKey = $"{methodKey}_{i}";
        }
        methodKey = $"{root}.{type.FullName}.{realMethodKey}";
        return methodKey;
    }

    /// <summary>
    /// 本地化特定方法, 具体规则见 <see cref="LocalizeAll(string, string, bool, string?, bool, LocalizeFilters?)"/>
    /// </summary>
    /// <param name="method">本地化的目标方法</param>
    /// <param name="methodKey">
    /// <br/>此方法的键名
    /// <br/>实际的键名会是 &lt;<paramref name="methodKey"/>>.&lt;序号>.OldString/NewString
    /// </param>
    /// <param name="cursorFilter">
    /// 筛选特定字符串处的<see cref="ILCursor"/>
    /// 此时 <see cref="ILCursor.Next"/> 一般会指向一处 ldstr
    /// </param>
    /// <inheritdoc cref="LocalizeAll(string, string, bool, string?, bool, LocalizeFilters?)"/>
    /// <param name="registerKey"></param>
    /// <param name="useLocalizedText"></param>
    public static void LocalizeMethod(MethodInfo method, string methodKey, bool registerKey = false, bool useLocalizedText = true, ILCursorFilter? cursorFilter = null) {
        #region 跳过不需要本地化的方法
        if (!registerKey) {
            if (!Language.Exists(methodKey + ".1.OldString")) {
                return;
            }
        }
        else {
            ILContext il = new(new DynamicMethodDefinition(method).Definition);
            if (!il.Instrs.Any(i => i.MatchLdstr(out _))) {
                return;
            }
        }
        #endregion
        #region 生成本地化字典
        Dictionary<string, NewString> localizations = [];
        for (int i = 1; ; ++i) {
            string stringPairKey = $"{methodKey}.{i}.";
            string oldStringKey = stringPairKey + "OldString";
            if (!Language.Exists(oldStringKey)) {
                break;
            }
            string oldString = Language.GetTextValue(oldStringKey);
            string newStringKey = stringPairKey + "NewString";
            if (Language.Exists(newStringKey + "_1")) {
                List<string> newStrings = [];
                for (int j = 1; ; ++j) {
                    string newStringsKey = $"{newStringKey}_{j}";
                    if (!Language.Exists(newStringsKey)) {
                        break;
                    }
                    newStrings.Add(useLocalizedText ? newStringsKey : Language.GetTextValue(newStringsKey));
                }
                if (newStrings.Count == 0) {
                    Debug.Assert(false, "应该存在 NewString_1 但却没有");
                    continue;
                }
                string? defaultNewString = !Language.Exists(newStringKey) ? null : useLocalizedText ? newStringKey : Language.GetTextValue(newStringKey);
                localizations.Add(oldString, new MultipleNewString(newStrings, defaultNewString));
            }
            else {
                if (Language.Exists(newStringKey)) {
                    localizations.Add(oldString, new SingleNewString(newStringKey, useLocalizedText));
                }
            }
        }
        #endregion
        #region IL
        MonoModHooks.Modify(method, il => {
            ILCursor cursor = new(il);
            string? oldString = null;
            while (cursor.TryGotoNext(i => i.MatchLdstr(out oldString))) {
                if (oldString == null || cursor.Next == null) {
                    continue;
                }
                var inLocalizations = localizations.TryGetValue(oldString, out var newStringClass);
                if (!inLocalizations && !registerKey)
                    continue;
                if (cursorFilter != null && !cursorFilter.Filter(cursor))
                    continue;
                if (!inLocalizations) {
                    string stringPairKey = $"{methodKey}.{localizations.Count + 1}.";
                    string oldStringKey = stringPairKey + "OldString";
                    string newStringKey = stringPairKey + "NewString";
                    Language.GetOrRegister(oldStringKey, () => oldString);
                    Language.GetOrRegister(newStringKey, () => oldString);
                    newStringClass = new SingleNewString(newStringKey, useLocalizedText);
                    localizations.Add(oldString, newStringClass);
                }
                string newString = newStringClass!.GetValue();
                if (!useLocalizedText) {
                    cursor.Next.Operand = newString;
                }
                else {
                    cursor.MoveAfterLabels();
                    cursor.EmitLdstr(newString);
                    cursor.EmitCall(TMLReflections.Language.GetTextValue_string);
                    cursor.Remove();
                }
            }
        });
        #endregion
    }
    /// <param name="localizationRoot">
    /// <br/>本地化键名的根
    /// <br/>建议以 "Mods.&lt;selfModName>" 开头
    /// <br/>实际的键名会是 &lt;<paramref name="localizationRoot"/>>.&lt;type.FullName>.&lt;method name>.&lt;序号>.OldString/NewString
    /// </param>
    /// <inheritdoc cref="LocalizeMethod(MethodInfo, string, bool, bool, ILCursorFilter)"/>
    /// <param name="method"></param>
    /// <param name="registerKey"></param>
    /// <param name="useLocalizedText"></param>
    /// <exception cref="NullReferenceException"></exception>
    public static void LocalizeMethodByRoot(MethodInfo method, string localizationRoot, bool registerKey = false, bool useLocalizedText = true) {
        var type = method.DeclaringType ?? throw new NullReferenceException("method should have declaring type");
        bool duplicated = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.Name == method.Name).Count() > 1;
        var methodKey = GetMethodKey(method.DeclaringType!, method, duplicated, localizationRoot);
        LocalizeMethod(method, methodKey, registerKey, useLocalizedText);
    }

    #region class NewString
    private abstract class NewString {
        public abstract string GetValue();
    }

    private class SingleNewString(string value) : NewString {
        public SingleNewString(string key, bool useLocalizedText) : this(useLocalizedText ? key : Language.GetTextValue(key)) { }
        public override string GetValue() => value;
    }

    private class MultipleNewString : NewString {
        private List<string> Values { get; }
        private string DefaultValue { get; }
        public MultipleNewString(List<string> values, string? defaultValue) {
            Values = values;
            DefaultValue = defaultValue ?? Values[0];
        }

        private int count;
        public override string GetValue() => count < Values.Count ? Values[count++] : DefaultValue;
    }
    #endregion
    #endregion
    #region ShowLocalizationRegisterProgress
    /// <summary>
    /// <br/>在加载模组的最后阶段显示添加键的进度
    /// <br/>当 <see cref="LocalizeAll(string, string, bool, string?, bool, LocalizeFilters?)"/> 的 registerKey 为 <see langword="true"/> 时,
    /// <br/>第一次注册键往往是一个漫长的过程
    /// <br/>此方法可以在此过程中显示其进度
    /// </summary>
    /// <param name="progressTextFormat">进度文字的格式, 显示在进度条上方的大字, 其中 {0} 表示模组名</param>
    /// <param name="subProgressTextFormat">副进度文字的格式, 显示在进度条下方的小字, 其中 {0} 表示本地化键名</param>
    public static void ShowLocalizationRegisterProgress(string progressTextFormat = "更新本地化文件: {0}", string subProgressTextFormat = "注册键: {0}") {
        if (finishedLocalizeRegister)
            return;
        if (showLocalizationRegisterProgressHooks == null) {
            showLocalizationRegisterProgressHooks = new(progressTextFormat, subProgressTextFormat);
        }
        else {
            showLocalizationRegisterProgressHooks.ProgressTextFormat = progressTextFormat;
            showLocalizationRegisterProgressHooks.SubProgressTextFormat = subProgressTextFormat;
        }
    }

    private static bool finishedLocalizeRegister;
    private static ShowLocalizationRegisterProgressHooksClass? showLocalizationRegisterProgressHooks;

    private class ShowLocalizationRegisterProgressHooksClass : IDisposable {
        private readonly Hook updateLocalizationFilesForModHook;
        private readonly ILHook updateLocalizationFilesForModILHook;
        private readonly Hook addEntryToHJSONHook;
        private readonly Hook updateLocalizationFilesHook;

        public string ProgressTextFormat { get; set; }
        public string SubProgressTextFormat { get; set; }

        public ShowLocalizationRegisterProgressHooksClass(string progressTextFormat, string subProgressTextFormat) {
            ProgressTextFormat = progressTextFormat;
            SubProgressTextFormat = subProgressTextFormat;
            updateLocalizationFilesForModHook = new(TMLReflections.LocalizationLoader.UpdateLocalizationFilesForModMethod, On_LocalizationLoader_UpdateLocalizationFilesForMod);
            updateLocalizationFilesForModILHook = new(TMLReflections.LocalizationLoader.UpdateLocalizationFilesForModMethod, IL_LocalizationLoader_UpdateLocalizationFilesForMod);
            addEntryToHJSONHook = new(TMLReflections.LocalizationLoader.AddEntryToHJSONMethod, On_LocalizationLoader_AddEntryToHJSON);
            updateLocalizationFilesHook = new(TMLReflections.LocalizationLoader.UpdateLocalizationFilesMethod, On_LocalizationLoader_UpdateLocalizationFiles);
        }

        private Mod? localizingMod;
        private int totalLocalizations;
        private int currentLocalzationCount;

        private delegate void UpdateLocalizationFilesForModDelegate(Mod mod, string? outputPath, GameCulture? specificCulture);
        private void On_LocalizationLoader_UpdateLocalizationFilesForMod(UpdateLocalizationFilesForModDelegate orig, Mod mod, string? outputPath, GameCulture? specificCulture) {
            localizingMod = mod;
            TMLReflections.UILoadMods.SetProgressText(string.Format(ProgressTextFormat, mod.DisplayNameClean));
            orig(mod, outputPath, specificCulture);
            localizingMod = null;
            TMLReflections.UILoadMods.SetSubProgressText("");
            TMLReflections.UILoadMods.SetProgress(0);
        }
        private void IL_LocalizationLoader_UpdateLocalizationFilesForMod(ILContext il) {
            ILCursor cursor = new(il);
            cursor.GotoNext(MoveType.After, i => i.MatchStloc(9)); // baseLocalizationKeys
            cursor.EmitLdloc(9);
            cursor.EmitDelegate((HashSet<string> baseLocalizationKeys) => {
                if (localizingMod == null)
                    return;
                totalLocalizations = TMLReflections.LanguageManager.LocalizedText.Values.Count(t => t.Key.StartsWith($"Mods.{localizingMod.Name}.") && !baseLocalizationKeys.Contains(t.Key));
                if (totalLocalizations <= 0)
                    totalLocalizations = 1;
                currentLocalzationCount = 0;
            });
        }

        private delegate void AddEntryToHJSONDelegate(LocalizationLoader.LocalizationFile file, string key, string value, string comment);
        private void On_LocalizationLoader_AddEntryToHJSON(AddEntryToHJSONDelegate orig, LocalizationLoader.LocalizationFile file, string key, string value, string comment) {
            if (localizingMod != null) {
                TMLReflections.UILoadMods.SetProgress((float)currentLocalzationCount++ / totalLocalizations);
                TMLReflections.UILoadMods.SetSubProgressText(string.Format(SubProgressTextFormat, key));
            }
            orig(file, key, value, comment);
        }

        private void On_LocalizationLoader_UpdateLocalizationFiles(Action orig) {
            orig();
            finishedLocalizeRegister = true;
            Dispose();
            showLocalizationRegisterProgressHooks = null;
        }

        public void Dispose() {
            updateLocalizationFilesForModHook.Undo();
            updateLocalizationFilesForModHook.Dispose();
            updateLocalizationFilesForModILHook.Undo();
            updateLocalizationFilesForModILHook.Dispose();
            addEntryToHJSONHook.Undo();
            addEntryToHJSONHook.Dispose();
            updateLocalizationFilesHook.Undo();
            updateLocalizationFilesHook.Dispose();
            localizingMod = null;
        }
    }
    #endregion
}