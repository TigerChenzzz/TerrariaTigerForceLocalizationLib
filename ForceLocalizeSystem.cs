using log4net;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace TigerForceLocalizationLib;

/// <summary>
/// <br/>继承此类然后使用此类的 Localize 等方法以本地化方法中的硬编码字符串
/// <br/>如果是弱依赖的话要加上 <see cref="JITWhenModsEnabledAttribute"/>
/// <br/>单例的初始化默认使用无参构造, 如果需要自定义构造则需要一个类型为 <see cref="CustomInitializeFunctionDelegate"/>, 名字为 CustomInitializeFunction 的静态字段, 以描述如何构造此实例
/// </summary>
/// <typeparam name="TSelf">继承了此类的类自己</typeparam>
public abstract class ForceLocalizeSystem<TSelf> where TSelf : ForceLocalizeSystem<TSelf> {
    #region Instance
    private static TSelf InitializeFunction() {
        var selfType = typeof(TSelf);
        var customInitializeFunctionField = selfType.GetField("CustomInitializeFunction", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (customInitializeFunctionField != null && customInitializeFunctionField.FieldType == typeof(CustomInitializeFunctionDelegate)) {
            var customInitializeFunction = (CustomInitializeFunctionDelegate?)customInitializeFunctionField.GetValue(null);
            if (customInitializeFunction != null) {
                return customInitializeFunction();
            }
        }
        return (TSelf)(Activator.CreateInstance(selfType) ?? throw new NullReferenceException("Activator.CreateInstance return null for type " + typeof(TSelf)));
    }
    /// <summary>
    /// 自定义初始化构造的委托
    /// </summary>
    /// <returns></returns>
    protected delegate TSelf CustomInitializeFunctionDelegate();
    private static TSelf? _instance;
    /// <summary>
    /// 单例
    /// </summary>
    protected static TSelf Instance { get => _instance ??= InitializeFunction(); }
    #endregion
    #region Localize
    /// <summary>
    /// <br/>替换一个方法中的字符串
    /// <br/>只能在加载阶段使用
    /// <br/>如果不想用反射, 可以使用<see cref="LocalizeByTypeFullName"/>
    /// <br/>或 <see cref="LocalizeByTypeName"/>
    /// <br/>或 <see cref="LocalizeByType"/>
    /// <br/>注意如果此方法有多个重载 (即多个重名的方法), 则上述三个无法使用 (只能用这个)
    /// </summary>
    /// <param name="methodBase">此方法, 由反射得到</param>
    /// <param name="localizations">需要替换的字符串, 键为替换前, 值为替换后</param>
    public static void Localize(MethodBase? methodBase, Dictionary<string, string> localizations) {
        if (methodBase == null) {
            LogError("Method Null!\n" + new StackTrace());
            return;
        }
        if (localizations.Count == 0) {
            return;
        }
        MonoModHooks.Modify(methodBase, il => {
            ILCursor cursor = new(il);
            string? str = null;
            while (cursor.TryGotoNext(i => i.MatchLdstr(out str))) {
                if (str != null && cursor.Next != null && localizations.TryGetValue(str, out var value)) {
                    Instance.ReplaceString_IL(cursor, str, value);
                }
            }
        });
    }
    /// <summary>
    /// <br/>替换一个方法中的字符串
    /// <br/>只能在加载阶段使用
    /// </summary>
    /// <param name="type">包含此方法的类型</param>
    /// <param name="methodName">此方法名</param>
    /// <inheritdoc cref="Localize"/>
    /// <param name="localizations"></param>
    public static void LocalizeByType(Type type, string methodName, Dictionary<string, string> localizations) {
        MethodInfo? methodInfo;
        try {
            methodInfo = type.GetMethod(methodName, BFALL);
        }
        catch (Exception e) {
            LogError("Localize error", e);
            return;
        }
        if (methodInfo == null) {
            LogErrorFormat("Can't find method {0} in type {1}", methodInfo, type);
            return;
        }
        Localize(methodInfo, localizations);
    }
    /// <param name="typeName">包含此方法的类型的名字, 注意此类型必须在需汉化的模组中</param>
    /// <param name="methodName">此方法名</param>
    /// <inheritdoc cref="LocalizeByType"/>
    /// <param name="localizations"></param>
    public static void LocalizeByTypeName(string typeName, string methodName, Dictionary<string, string> localizations) {
        if (!TypeHelper.TypeByName.TryGetValue(typeName, out Type? type)) {
            LogErrorFormat("Can't find type by name: {0}", typeName);
            return;
        }
        if (TypeHelper.DuplicatedNames.Contains(typeName)) {
            LogWarnFormat("Type duplicated: {0}", typeName);
        }
        LocalizeByType(type, methodName, localizations);
    }
    /// <param name="typeFullName">
    /// <br/>包含此方法的类型的全名, 注意此类型必须在需汉化的模组中
    /// <br/>包含命名空间, 由 '.' 分隔
    /// <br/>例: LunarVeilChinesePack.Systems.ForceLocalizeSystem
    /// <br/>如果是内嵌类型, 则用 '+' 连接
    /// <br/>例: LunarVeilChinesePack.Systems.ForceLocalizeSystem+TypeHelper
    /// </param>
    /// <inheritdoc cref="LocalizeByTypeName"/>
    /// <param name="methodName"></param>
    /// <param name="localizations"></param>
    public static void LocalizeByTypeFullName(string typeFullName, string methodName, Dictionary<string, string> localizations) {
        if (!TypeHelper.TypeByFullName.TryGetValue(typeFullName, out Type? type)) {
            LogErrorFormat("Can't find type by full name: {0}", typeFullName);
            return;
        }
        LocalizeByType(type, methodName, localizations);
    }

    /// <summary>
    /// <br/>替换一个方法中的字符串
    /// <br/>只能在加载阶段使用
    /// <br/>如果不想用反射, 可以使用<see cref="LocalizeInOrderByTypeFullName"/>
    /// <br/>或 <see cref="LocalizeInOrderByTypeName"/>
    /// <br/>或 <see cref="LocalizeInOrderByType"/>
    /// <br/>注意如果此方法有多个重载 (即多个重名的方法), 则上述三个无法使用 (只能用这个)
    /// </summary>
    /// <param name="localizationsInOrder">
    /// <br/>需要替换的字符串, 需按顺序装有 (替换前, 替换后) 的值
    /// <br/>即使不需要替换也要写上一项 (替换前和替换后相同的值)
    /// </param>
    /// <inheritdoc cref="Localize"/>
    /// <param name="methodBase"></param>
    public static void LocalizeInOrder(MethodBase? methodBase, List<(string Key, string Value)> localizationsInOrder) {
        if (methodBase == null) {
            LogError("Method Null!\n" + new StackTrace());
            return;
        }
        if (localizationsInOrder.Count == 0) {
            return;
        }
        MonoModHooks.Modify(methodBase, il => {
            ILCursor cursor = new(il);
            string? str = null;
            int i = 0;
            while (cursor.TryGotoNext(i => i.MatchLdstr(out str))) {
                if (str == null || cursor.Next == null || localizationsInOrder[i].Key != str) {
                    continue;
                }
                Instance.ReplaceString_IL(cursor, str, localizationsInOrder[i].Value);
                i += 1;
                if (localizationsInOrder.Count <= i) {
                    return;
                }
            }
        });
    }
    /// <inheritdoc cref="LocalizeByType"/>
    /// <inheritdoc cref="LocalizeInOrder"/>
    public static void LocalizeInOrderByType(Type type, string methodName, List<(string Key, string Value)> localizationsInOrder) {
        MethodInfo? methodInfo;
        try {
            methodInfo = type.GetMethod(methodName, BFALL);
        }
        catch (Exception e) {
            LogError("Localize error", e);
            return;
        }
        if (methodInfo == null) {
            LogErrorFormat("Can't find method {0} in type {1}", methodInfo, type);
            return;
        }
        LocalizeInOrder(methodInfo, localizationsInOrder);
    }
    /// <inheritdoc cref="LocalizeInOrderByType"/>
    /// <inheritdoc cref="LocalizeByTypeName"/>
    public static void LocalizeInOrderByTypeName(string typeName, string methodName, List<(string Key, string Value)> localizationsInOrder) {
        if (!TypeHelper.TypeByName.TryGetValue(typeName, out Type? type)) {
            LogErrorFormat("Can't find type by name: {0}", typeName);
            return;
        }
        if (TypeHelper.DuplicatedNames.Contains(typeName)) {
            LogWarnFormat("Type duplicated: {0}", typeName);
        }
        LocalizeInOrderByType(type, methodName, localizationsInOrder);
    }
    /// <inheritdoc cref="LocalizeInOrderByType"/>
    /// <inheritdoc cref="LocalizeByTypeFullName"/>
    public static void LocalizeInOrderByTypeFullName(string typeFullName, string methodName, List<(string Key, string Value)> localizationsInOrder) {
        if (!TypeHelper.TypeByFullName.TryGetValue(typeFullName, out Type? type)) {
            LogErrorFormat("Can't find type by full name: {0}", typeFullName);
            return;
        }
        LocalizeInOrderByType(type, methodName, localizationsInOrder);
    }

    /// <summary>
    /// <br/>替换所有子类中此方法的重写中的字符串
    /// <br/>只能在加载阶段使用
    /// <br/>如果不想用反射, 可以使用<see cref="LocalizeDerivedByType"/>
    /// <br/>注意如果此方法有多个重载 (即多个重名的方法), 则上面那个无法使用 (只能用这个)
    /// </summary>
    /// <param name="includeSelf">是否同时替换此方法下的字符串</param>
    /// <inheritdoc cref="Localize"/>
    /// <param name="methodBase"></param>
    /// <param name="localizations"></param>
    public static void LocalizeDerived(MethodBase? methodBase, Dictionary<string, string> localizations, bool includeSelf = false) {
        if (methodBase == null) {
            LogError("Method Null!\n" + new StackTrace());
            return;
        }
        if (includeSelf) {
            Localize(methodBase, localizations);
        }
        if (methodBase.ReflectedType == null) {
            LogError("Method Don't have a reflectedType!\n" + new StackTrace());
            return;
        }
        if (!methodBase.IsVirtual) {
            return;
        }
        string methodName = methodBase.Name;
        Type[] types = methodBase.GetParameters().Select(p => p.ParameterType).ToArray();
        foreach (var type in TypeHelper.TypeByFullName.Values) {
            if (!type.IsSubclassOf(methodBase.ReflectedType)) {
                continue;
            }
            var method = type.GetMethod(methodName, BFALL, types);
            if (method == null || method.DeclaringType != type) {
                continue;
            }
            Localize(method, localizations);
        }
    }
    /// <summary>
    /// <br/>替换所有子类中此方法的重写中的字符串
    /// <br/>只能在加载阶段使用
    /// </summary>
    /// <inheritdoc cref="LocalizeDerived"/>
    /// <inheritdoc cref="LocalizeByType"/>
    public static void LocalizeDerivedByType(Type type, string methodName, Dictionary<string, string> localizations, bool includeSelf = false) {
        MethodInfo? methodInfo;
        try {
            methodInfo = type.GetMethod(methodName, BFALL);
        }
        catch (Exception e) {
            LogError("Localize error", e);
            return;
        }
        if (methodInfo == null) {
            LogErrorFormat("Can't find method {0} in type {1}", methodInfo, type);
            return;
        }
        LocalizeDerived(methodInfo, localizations, includeSelf);
    }
    /// <inheritdoc cref="LocalizeDerivedByType"/>
    /// <inheritdoc cref="LocalizeByTypeName"/>
    public static void LocalizeDerivedByTypeName(string typeName, string methodName, Dictionary<string, string> localizations, bool includeSelf = false) {
        if (!TypeHelper.TypeByName.TryGetValue(typeName, out Type? type)) {
            LogErrorFormat("Can't find type by name: {0}", typeName);
            return;
        }
        if (TypeHelper.DuplicatedNames.Contains(typeName)) {
            LogWarnFormat("Type duplicated: {0}", typeName);

        }
        LocalizeDerivedByType(type, methodName, localizations, includeSelf);
    }
    /// <inheritdoc cref="LocalizeDerivedByTypeName"/>
    /// <inheritdoc cref="LocalizeByTypeFullName"/>
    public static void LocalizeDerivedByTypeFullName(string typeFullName, string methodName, Dictionary<string, string> localizations, bool includeSelf = false) {
        if (!TypeHelper.TypeByFullName.TryGetValue(typeFullName, out Type? type)) {
            LogErrorFormat("Can't find type by full name: {0}", typeFullName);
            return;
        }
        LocalizeDerivedByType(type, methodName, localizations, includeSelf);
    }
    #endregion
    /// <summary>
    /// <br/>重写此方法以自定义如何替换字符串
    /// <br/>默认直接返回 <paramref name="new"/>, 代表直接用新字符串替代旧字符串
    /// </summary>
    protected virtual string ReplaceString(string old, string @new) => @new;
    /// <summary>
    /// <br/>重写此方法以自定义在 IL 中如何替换字符串
    /// <br/>默认为调用 <see cref="ReplaceString(string, string)"/> 方法后直接将其返回值赋予 cursor.Next
    /// </summary>
    /// <param name="cursor">IL 指针, 它指向的语句为 ldstr <paramref name="old"/> (如果要插入语句的话需先调用 <see cref="ILCursor.MoveAfterLabels()"/>)</param>
    /// <param name="old">原字符串</param>
    /// <param name="new">通过 Localize 系方法转入的新字符串</param>
    protected virtual void ReplaceString_IL(ILCursor cursor, string old, string @new) {
        string replaced = ReplaceString(old, @new);
        cursor.Next!.Operand = replaced;
    }
    /// <summary>
    /// 要汉化的模组的名字
    /// </summary>
    protected abstract string ModName { get; }
    #region Log
    /// <summary>
    /// 是否报告错误和警告
    /// </summary>
    protected virtual bool NeedLog => true;
    /// <summary>
    /// 是否在有错误时抛出
    /// </summary>
    protected virtual bool ThrowException => false;
    /// <summary>
    /// 使用什么以报告日志
    /// </summary>
    protected virtual ILog Logger { get; } = Logging.PublicLogger;
    private const string LogConditionalString = "DEBUG";
    // [Conditional(LogConditionalString)]
    // private static void LogWarn(object message) {
    //     if (Instance.NeedLog) {
    //         Instance.Logger.Warn(message);
    //     }
    // }
    [Conditional(LogConditionalString)]
    private static void LogWarnFormat(string message, object? arg0) {
        if (Instance.NeedLog) {
            Instance.Logger.WarnFormat(message, arg0);
        }
    }
    [Conditional(LogConditionalString)]
    private static void LogError(object message) {
        if (Instance.NeedLog) {
            Instance.Logger.Error(message);
        }
        if (Instance.ThrowException) {
            throw new Exception("Localize error!");
        }
    }
    [Conditional(LogConditionalString)]
    private static void LogError(object message, Exception exception) {
        if (Instance.NeedLog) {
            Instance.Logger.Error(message, exception);
        }
        if (Instance.ThrowException) {
            throw new Exception("Localize error!");
        }
    }
    [Conditional(LogConditionalString)]
    private static void LogErrorFormat(string message, object? arg0) {
        if (Instance.NeedLog) {
            Instance.Logger.ErrorFormat(message, arg0);
        }
        if (Instance.ThrowException) {
            throw new Exception("Localize error!");
        }
    }
    [Conditional(LogConditionalString)]
    private static void LogErrorFormat(string message, object? arg0, object? arg1) {
        if (Instance.NeedLog) {
            Instance.Logger.ErrorFormat(message, arg0, arg1);
        }
        if (Instance.ThrowException) {
            throw new Exception("Localize error!");
        }
    }
    #endregion

    static ForceLocalizeSystem() {
        Hook? postSetupRecipesHook = null;
        postSetupRecipesHook = new(typeof(SystemLoader).GetMethod("PostSetupRecipes", BFALL) ?? throw new NullReferenceException("Can't find SystemLoader.PostSetupRecipes"), (Action<Mod> orig, Mod mod) => {
            orig(mod);
            TypeHelper.Clear();
            if (postSetupRecipesHook != null) {
                postSetupRecipesHook.Dispose(); // 包含 Undo
                postSetupRecipesHook = null;
            }
        });
    }

    private const BindingFlags BFALL = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    /// <summary>
    /// 用以辅助获取类型等
    /// </summary>
    public static class TypeHelper {
        private static Assembly? modAssembly;
        /// <summary>
        /// 模组的程序集
        /// </summary>
        public static Assembly ModAssembly {
            get {
                if (modAssembly == null) {
                    Initialize();
                }
                return modAssembly;
            }
        }
        private static Dictionary<string, Type>? typesByName;
        /// <summary>
        /// 通过 Type.Name 为键构造的字典
        /// </summary>
        public static Dictionary<string, Type> TypeByName {
            get {
                if (typesByName == null) {
                    Initialize();
                }
                return typesByName;
            }
        }
        private static HashSet<string>? duplicatedNames;
        /// <summary>
        /// 在 <see cref="TypeByName"/> 中有重复的键
        /// </summary>
        public static HashSet<string> DuplicatedNames {
            get {
                if (duplicatedNames == null) {
                    Initialize();
                }
                return duplicatedNames;
            }
        }
        private static Dictionary<string, Type>? typeByFullName;
        /// <summary>
        /// 通过 <see cref="Type.FullName"/> 为键构造的字典
        /// </summary>
        public static Dictionary<string, Type> TypeByFullName {
            get {
                if (typeByFullName == null) {
                    Initialize();
                }
                return typeByFullName;
            }
        }
        private static bool initialized;
        [MemberNotNull(nameof(modAssembly), nameof(typesByName), nameof(typeByFullName), nameof(duplicatedNames))]
        private static void Initialize() {
            if (cleared) {
                throw new Exception("在完成后初始化!");
            }
            if (initialized) {
                throw new Exception("重复初始化!");
            }
            if (!ModLoader.TryGetMod(Instance.ModName, out Mod mod)) {
                throw new Exception($"模组 \"{Instance.ModName}\" 未找到!");
            }
            initialized = true;
            modAssembly = mod.Code;
            typesByName = [];
            typeByFullName = [];
            duplicatedNames = [];
            foreach (Type type in AssemblyManager.GetLoadableTypes(modAssembly)) {
                if (!typesByName.TryAdd(type.Name, type)) {
                    duplicatedNames.Add(type.Name);
                }
                if (type.FullName != null) {
                    typeByFullName.TryAdd(type.FullName, type);
                }
            }
        }
        private static bool cleared;
        internal static void Clear() {
            cleared = true;
            initialized = false;
            modAssembly = null;
            typesByName = null;
            typeByFullName = null;
            duplicatedNames = null;
        }
    }
}

/// <summary>
/// <br/>此类需继承后使用, 这样不用重写 <see cref="ModName"/>, 其他与 <see cref="ForceLocalizeSystem{TSelf}"/> 一致
/// <br/>同样需注意弱引用的处理, 而且由于在继承时直接使用了其他模组中的类, 所以弱引用时必须使用 <see cref="ExtendsFromModAttribute"/>
/// </summary>
/// <typeparam name="TMod">要汉化的模组</typeparam>
/// <inheritdoc cref="ForceLocalizeSystem{TSelf}"/>
/// <typeparam name="TSelf"></typeparam>
public abstract class ForceLocalizeSystemByMod<TMod, TSelf> : ForceLocalizeSystem<TSelf> where TMod : Mod where TSelf : ForceLocalizeSystemByMod<TMod, TSelf> {
    private readonly string modName = typeof(TMod).Name;
    /// <inheritdoc/>
    protected override string ModName => modName;
}

/// <summary>
/// <br/>此类可直接使用, 规则基本与 <see cref="ForceLocalizeSystem{TSelf}"/> 一致
/// </summary>
/// <inheritdoc cref="ForceLocalizeSystemByMod{TMod, TSelf}"/>
public sealed class ForceLocalizeSystemImpl<TMod> : ForceLocalizeSystemByMod<TMod, ForceLocalizeSystemImpl<TMod>> where TMod : Mod { }

/// <summary>
/// <br/>此类需继承后使用
/// <br/>此类使用 Language.GetTextValue(key) 代替原字符串, 使用 Localize 系方法时传入的新字符串默认应是 hjson 中的键
/// </summary>
/// <inheritdoc cref="ForceLocalizeSystem{TSelf}"/>
public abstract class ForceLocalizeSystemByLocalizeText<TSelf> : ForceLocalizeSystem<TSelf> where TSelf : ForceLocalizeSystemByLocalizeText<TSelf> {
    /// <inheritdoc/>
    protected override void ReplaceString_IL(ILCursor cursor, string old, string @new) {
        Language.GetOrRegister(@new, () => old);
        cursor.MoveAfterLabels();
        cursor.EmitLdstr(@new);
        cursor.EmitCall(languageGetTextValueMethod);
        cursor.Remove();
    }
    private readonly MethodInfo languageGetTextValueMethod = typeof(Language).GetMethod(nameof(Language.GetTextValue), BindingFlags.Static | BindingFlags.Public, [typeof(string)])!;
}

/// <summary>
/// <br/>此类需继承后使用, 这样不用重写 <see cref="ModName"/>
/// <br/>此类使用 Language.GetTextValue(key) 代替原字符串, 使用 Localize 系方法时传入的新字符串默认应是 hjson 中的键
/// </summary>
/// <inheritdoc cref="ForceLocalizeSystemImpl{TMod}"/>
public abstract class ForceLocalizeSystemByLocalizeText<TMod, TSelf> : ForceLocalizeSystemByLocalizeText<TSelf> where TMod : Mod where TSelf : ForceLocalizeSystemByLocalizeText<TMod, TSelf> {
    private readonly string modName = typeof(TMod).Name;
    /// <inheritdoc/>
    protected override string ModName => modName;
}

/// <summary>
/// <br/>此类可直接使用
/// <br/>此类使用 Language.GetTextValue(key) 代替原字符串, 使用 Localize 系方法时传入的新字符串应是 hjson 中的键
/// </summary>
/// <inheritdoc cref="ForceLocalizeSystemImpl{TMod}"/>
public sealed class ForceLocalizeSystemByLocalizeTextImpl<TMod> : ForceLocalizeSystemByLocalizeText<TMod, ForceLocalizeSystemByLocalizeTextImpl<TMod>> where TMod : Mod { }