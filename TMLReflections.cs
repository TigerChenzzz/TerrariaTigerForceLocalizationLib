using System;
using System.Reflection;
using TMLLanguage = Terraria.Localization.Language;

namespace TigerForceLocalizationLib;

internal static class TMLReflections {
    public const BindingFlags BFALL = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    public const BindingFlags BFS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    public const BindingFlags BFI = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static MethodInfo? languageGetTextValueMethod;
    public static MethodInfo LanguageGetTextValueMethod => languageGetTextValueMethod ??= typeof(TMLLanguage).GetMethod(nameof(TMLLanguage.GetTextValue), BFS, [typeof(string)])!;
    #region Language
    public static class Language {
        private static Type Type { get; } = typeof(TMLLanguage);
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
    #endregion
}
