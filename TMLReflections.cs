using System;
using System.Reflection;
using TMLMain = Terraria.Main;
using TMLLanguage = Terraria.Localization.Language;
using TMLLanguageManager = Terraria.Localization.LanguageManager;
using TMLLocalizedText = Terraria.Localization.LocalizedText;
using TMLLocalizationLoader = Terraria.ModLoader.LocalizationLoader;
using TMLModContent = Terraria.ModLoader.ModContent;
using MonoMod.Utils;
using System.Collections.Generic;

namespace TigerForceLocalizationLib;

internal static class TMLReflections {
    public const BindingFlags BFALL = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    public const BindingFlags BFS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    public const BindingFlags BFI = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    public static Assembly MainAssembly { get; } = typeof(TMLMain).Assembly;
    #region Terraria.Localization
    public static class Language {
        public static Type Type { get; } = typeof(TMLLanguage);
        public static MethodInfo GetText { get; } = Type.GetMethod(nameof(TMLLanguage.GetText), BFS)!;
        public static MethodInfo GetTextValue_string { get; } = Type.GetMethod(nameof(TMLLanguage.GetTextValue), BFS, [typeof(string)])!;
        public static MethodInfo GetTextValue_string_object { get; } = Type.GetMethod(nameof(TMLLanguage.GetTextValue), BFS, [typeof(string), typeof(object)])!;
        public static MethodInfo GetTextValue_string_object_object { get; } = Type.GetMethod(nameof(TMLLanguage.GetTextValue), BFS, [typeof(string), typeof(object), typeof(object)])!;
        public static MethodInfo GetTextValue_string_object_object_object { get; } = Type.GetMethod(nameof(TMLLanguage.GetTextValue), BFS, [typeof(string), typeof(object), typeof(object), typeof(object)])!;
        public static MethodInfo GetTextValue_string_objectArray { get; } = Type.GetMethod(nameof(TMLLanguage.GetTextValue), BFS, [typeof(string), typeof(object[])])!;
        public static MethodInfo GetTextValueWith { get; } = Type.GetMethod(nameof(TMLLanguage.GetTextValueWith), BFS)!;
        public static MethodInfo Exists { get; } = Type.GetMethod(nameof(TMLLanguage.Exists), BFS)!;
        public static MethodInfo GetCategorySize { get; } = Type.GetMethod(nameof(TMLLanguage.GetCategorySize), BFS)!;
        public static MethodInfo RandomFromCategory { get; } = Type.GetMethod(nameof(TMLLanguage.RandomFromCategory), BFS)!;
        public static MethodInfo GetOrRegister { get; } = Type.GetMethod(nameof(TMLLanguage.GetOrRegister), BFS, [typeof(string), typeof(Func<string>)])!;
    }
    public static class LanguageManager {
        public static Type Type { get; } = typeof(TMLLanguageManager);
        public static FieldInfo LocalizedTextField { get; } = Type.GetField("_localizedTexts", BFI)!;
        public static Dictionary<string, TMLLocalizedText> LocalizedText { get; } = (Dictionary<string, TMLLocalizedText>)LocalizedTextField.GetValue(TMLLanguageManager.Instance)!;
    }
    #endregion
    #region Terraria.ModLoader
    public class LocalizationLoader {
        public static Type Type { get; } = typeof(TMLLocalizationLoader);
        public static MethodInfo AddEntryToHJSONMethod { get; } = Type.GetMethod("AddEntryToHJSON", BFS)!;
        public static MethodInfo UpdateLocalizationFilesMethod { get; } = Type.GetMethod("UpdateLocalizationFiles", BFS)!;
        public static MethodInfo UpdateLocalizationFilesForModMethod { get; } = Type.GetMethod("UpdateLocalizationFilesForMod", BFS)!;
    }
    public class ModContent {
        public static Type Type { get; } = typeof(TMLModContent);
        public static MethodInfo LoadMethod { get; } = Type.GetMethod("Load", BFS)!;
    }
    #endregion
    #region Terraria.ModLoader.UI
    public static class Interface {
        public static Type Type { get; } = MainAssembly.GetType("Terraria.ModLoader.UI.Interface")!;
        public static FieldInfo LoadModsField { get; } = Type.GetField("loadMods", BFS)!;
        public static object LoadMods { get; } = LoadModsField.GetValue(null)!;
    }
    public static class UILoadMods {
        public static Type Type { get; } = MainAssembly.GetType("Terraria.ModLoader.UI.UILoadMods")!;
        #region SetProgressText
        public static MethodInfo SetProgressTextMethod { get; } = Type.GetMethod("SetProgressText", BFI)!;
        private static Action<object, string, string?>? _setProgressTextFunction;
        private static Action<object, string, string?> SetProgressTextFunction {
            get {
                if (_setProgressTextFunction != null)
                    return _setProgressTextFunction;
                var invoker = SetProgressTextMethod.GetFastInvoker();
                return _setProgressTextFunction = (obj, str1, str2) => invoker.Invoke(obj, [str1, str2]);
            }
        }
        public static void SetProgressText(string text, string? logText = null) => SetProgressTextFunction(Interface.LoadMods, text, logText);
        #endregion
        #region SetSubProgressText
        public static MethodInfo SetSubProgressTextMethod { get; } = Type.GetProperty("SubProgressText", BFI)!.SetMethod!;
        private static Action<object, string>? _setSubProgressTextFunction;
        private static Action<object, string> SetSubProgressTextFunction {
            get {
                if (_setSubProgressTextFunction != null)
                    return _setSubProgressTextFunction;
                var invoker = SetSubProgressTextMethod.GetFastInvoker();
                return _setSubProgressTextFunction = (obj, str) => invoker.Invoke(obj, [str]);
            }
        }
        public static void SetSubProgressText(string text) => SetSubProgressTextFunction(Interface.LoadMods, text);
        #endregion
        #region SetProgress
        public static MethodInfo SetProgressMethod { get; } = Type.GetProperty("Progress", BFI)!.SetMethod!;
        private static Action<object, float>? _setProgressFunction;
        private static Action<object, float> SetProgressFunction {
            get {
                if (_setProgressFunction != null)
                    return _setProgressFunction;
                var invoker = SetProgressMethod.GetFastInvoker();
                return _setProgressFunction = (obj, f) => invoker.Invoke(obj, [f]);
            }
        }
        public static void SetProgress(float progress) => SetProgressFunction(Interface.LoadMods, progress);
        #endregion
    }
    #endregion
}
