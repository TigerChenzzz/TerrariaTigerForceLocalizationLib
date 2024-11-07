using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TigerForceLocalizationLib.Filters;

/// <summary>
/// 用以筛选方法
/// </summary>
/// <inheritdoc/>
public class MethodFilter(Func<MethodInfo, bool> filter) : FilterBase<MethodInfo>(filter) {
    #region 运算符重载
    /// <summary>
    /// 两个筛选规则满足其一即可
    /// </summary>
    public static MethodFilter operator |(MethodFilter left, MethodFilter right) {
        return new(method => left.Filter(method) || right.Filter(method));
    }
    /// <summary>
    /// 两个筛选规则需同时满足
    /// </summary>
    public static MethodFilter operator &(MethodFilter left, MethodFilter right) {
        return new(method => left.Filter(method) && right.Filter(method));
    }
    /// <summary>
    /// 筛选规则不满足
    /// </summary>
    public static MethodFilter operator !(MethodFilter self) => new(method => !self.Filter(method));
    /// <summary>
    /// 多个筛选规则需同时满足
    /// </summary>
    public static MethodFilter MatchAll(params MethodFilter[] filters) => new(method => filters.All(f => f.Filter(method)));
    /// <summary>
    /// 多个筛选规则满足其一即可
    /// </summary>
    public static MethodFilter MatchAny(params MethodFilter[] filters) => new(method => filters.Any(f => f.Filter(method)));
    #endregion
    #region 静态常用筛选规则
    #region 匹配  Name
    /// <summary>
    /// 方法的名称是<paramref name="name"/>
    /// </summary>
    public static MethodFilter MatchName(string name) => new(method => method.Name == name);
    /// <summary>
    /// 方法的名称不是<paramref name="name"/>
    /// </summary>
    public static MethodFilter MismatchName(string name) => new(method => method.Name != name);
    /// <summary>
    /// 方法的名称是这些名称中的一个
    /// </summary>
    public static MethodFilter MatchNames(params string[] names) {
        HashSet<string> keys = [.. names];
        return new(method => keys.Contains(method.Name));
    }
    /// <summary>
    /// 方法的名称不是这些名称中的任意一个
    /// </summary>
    public static MethodFilter MismatchNames(params string[] names) {
        HashSet<string> keys = [.. names];
        return new(method => !keys.Contains(method.Name));
    }
    #endregion
    #region 匹配 DeclaringType
    /// <summary>
    /// 匹配 method.DeclaringType
    /// </summary>
    /// <param name="useDerivedCheck">是否检查是否是继承的类</param>
    /// <param name="types">所匹配的类型</param>
    public static MethodFilter MatchDeclaringTypes(bool useDerivedCheck, params Type[] types) {
        if (useDerivedCheck) {
            return new(method => method.DeclaringType != null && types.Any(t => t.IsAssignableFrom(method.DeclaringType)));
        }
        else {
            HashSet<Type> keys = [.. types];
            return new(method => method.DeclaringType != null && keys.Contains(method.DeclaringType));
        }
    }
    /// <summary>
    /// 匹配 method.DeclaringType 是否是 <paramref name="types"/> 中任意类或其子类
    /// </summary>
    /// <param name="types">所匹配的类型</param>
    public static MethodFilter MatchDeclaringTypes(params Type[] types) => MatchDeclaringTypes(true, types);
    /// <summary>
    /// 匹配 method.DeclaringType
    /// </summary>
    /// <param name="useDerivedCheck">是否检查是否是继承的类</param>
    /// <param name="type">所匹配的类型</param>
    public static MethodFilter MatchDeclaringType(bool useDerivedCheck, Type type) {
        return useDerivedCheck ? new(method => type.IsAssignableFrom(method.DeclaringType)) : new(method => type == method.DeclaringType);
    }
    /// <summary>
    /// 匹配 method.DeclaringType 是否是 <paramref name="type"/> 或其子类
    /// </summary>
    /// <param name="type">所匹配的类型</param>
    public static MethodFilter MatchDeclaringType(Type type) => MatchDeclaringType(true, type);
    #endregion
    // TODO:筛选特定类的 get_Texture, get_HighlightTexture, GetLocalization, GetLocalizationKey, GetLocalizedValue
    #endregion
}
