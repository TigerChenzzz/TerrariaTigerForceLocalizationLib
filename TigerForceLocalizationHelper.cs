using MonoMod.Cil;
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

namespace TigerForceLocalizationLib;

/// <summary>
/// 用以辅助本地化的一些方法
/// </summary>
public static class TigerForceLocalizationHelper {
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
    public static void LocalizeAll(string selfModName, string modName, bool registerKey = false, string? localizationRoot = null, bool useLocalizedText = true) {
        if (!ModLoader.TryGetMod(modName, out var mod)) {
            throw new Exception($"未找到模组\"{modName}\"!");
        }
        localizationRoot ??= $"Mods.{selfModName}.ForceLocalizations";
        foreach (var type in AssemblyManager.GetLoadableTypes(mod.Code)) {
            if (type.ContainsGenericParameters) {
                continue;
            }
            Dictionary<string, int> methodCount = [];
            HashSet<string> usedMethodKey = [];
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.DeclaringType == type && !method.IsAbstract && !method.ContainsGenericParameters && method.GetMethodBody() != null).ToArray();
            foreach (var method in methods) {
                if (!methodCount.TryAdd(method.Name, 1)) {
                    methodCount[method.Name] += 1;
                }
            }
            foreach (var method in methods) {
                #region 设置 methodKey
                string methodKey;
                if (methodCount[method.Name] == 1) {
                    methodKey = method.Name;
                }
                else {
                    StringBuilder methodKeyBuilder = new();
                    methodKeyBuilder.Append(method.Name).Append('_').AppendJoin('_', method.GetParameters().Select(p => p.ParameterType.Name));
                    methodKey = methodKeyBuilder.ToString();
                }
                string realMethodKey = methodKey;
                for (int i = 2; usedMethodKey.Contains(realMethodKey); ++i) {
                    realMethodKey = $"{methodKey}_{i}";
                }
                methodKey = $"{localizationRoot}.{type.FullName}.{realMethodKey}";
                #endregion
                methodKey = GetMethodKey(type, method, methodCount[method.Name] > 1, localizationRoot, usedMethodKey.Contains);
                LocalizeMethod(method, methodKey, registerKey, useLocalizedText);
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
    /// 本地化特定方法, 具体规则见 <see cref="LocalizeAll(string, string, bool, string?, bool)"/>
    /// </summary>
    /// <param name="method">本地化的目标方法</param>
    /// <param name="methodKey">
    /// <br/>此方法的键名
    /// <br/>实际的键名会是 &lt;<paramref name="methodKey"/>>.&lt;序号>.OldString/NewString
    /// </param>
    /// <inheritdoc cref="LocalizeAll(string, string, bool, string?, bool)"/>
    /// <param name="registerKey"></param>
    /// <param name="useLocalizedText"></param>
    public static void LocalizeMethod(MethodInfo method, string methodKey, bool registerKey = false, bool useLocalizedText = true) {
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
                if (!localizations.TryGetValue(oldString, out var newStringClass)) {
                    if (!registerKey) {
                        continue;
                    }
                    string stringPairKey = $"{methodKey}.{localizations.Count + 1}.";
                    string oldStringKey = stringPairKey + "OldString";
                    string newStringKey = stringPairKey + "NewString";
                    Language.GetOrRegister(oldStringKey, () => oldString);
                    Language.GetOrRegister(newStringKey, () => oldString);
                    newStringClass = new SingleNewString(newStringKey, useLocalizedText);
                    localizations.Add(oldString, newStringClass);
                }
                string newString = newStringClass.GetValue();
                if (!useLocalizedText) {
                    cursor.Next.Operand = newString;
                }
                else {
                    cursor.MoveAfterLabels();
                    cursor.EmitLdstr(newString);
                    cursor.EmitCall(LanguageGetTextValueMethod);
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
    /// <inheritdoc cref="LocalizeMethod(MethodInfo, string, bool, bool)"/>
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

    private static MethodInfo? languageGetTextValueMethod;
    private static MethodInfo LanguageGetTextValueMethod {
        get {
            if (languageGetTextValueMethod != null) {
                return languageGetTextValueMethod;
            }
            languageGetTextValueMethod = typeof(Language).GetMethod(nameof(Language.GetTextValue), BindingFlags.Static | BindingFlags.Public, [typeof(string)])!;
            return languageGetTextValueMethod;
        }
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
}