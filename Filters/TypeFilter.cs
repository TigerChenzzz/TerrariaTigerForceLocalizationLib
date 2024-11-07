using System;
using System.Collections.Generic;
using System.Linq;

namespace TigerForceLocalizationLib.Filters;

/// <summary>
/// 用以筛选类型
/// </summary>
/// <inheritdoc/>
public class TypeFilter(Func<Type, bool> filter) : FilterBase<Type>(filter) {
    #region 运算符重载
    /// <summary>
    /// 两个筛选规则满足其一即可
    /// </summary>
    public static TypeFilter operator |(TypeFilter left, TypeFilter right) {
        return new(type => left.Filter(type) || right.Filter(type));
    }
    /// <summary>
    /// 两个筛选规则需同时满足
    /// </summary>
    public static TypeFilter operator &(TypeFilter left, TypeFilter right) {
        return new(type => left.Filter(type) && right.Filter(type));
    }
    /// <summary>
    /// 筛选规则不满足
    /// </summary>
    public static TypeFilter operator !(TypeFilter self) => new(type => !self.Filter(type));
    /// <summary>
    /// 多个筛选规则需同时满足
    /// </summary>
    public static TypeFilter MatchAll(params TypeFilter[] filters) => new(type => filters.All(f => f.Filter(type)));
    /// <summary>
    /// 多个筛选规则满足其一即可
    /// </summary>
    public static TypeFilter MatchAny(params TypeFilter[] filters) => new(type => filters.Any(f => f.Filter(type)));
    #endregion
    #region 常用筛选规则
    #region 匹配 FullName
    /// <summary>
    /// 类型的全名是<paramref name="fullName"/>
    /// </summary>
    public static TypeFilter MatchFullName(string fullName) => new(type => type.FullName == fullName);
    /// <summary>
    /// 类型的全名不是<paramref name="fullName"/>
    /// </summary>
    public static TypeFilter MismatchFullName(string fullName) => new(type => type.FullName != fullName);
    /// <summary>
    /// 类型的全名是这些名称中的一个
    /// </summary>
    public static TypeFilter MatchFullNames(params string[] fullNames) {
        HashSet<string> keys = [.. fullNames];
        return new(type => type.FullName != null && keys.Contains(type.FullName));
    }
    /// <summary>
    /// 类型的全名不是这些名称中的任意一个
    /// </summary>
    public static TypeFilter MismatchFullNames(params string[] fullNames) {
        HashSet<string> keys = [.. fullNames];
        return new(type => type.FullName == null || !keys.Contains(type.FullName));
    }
    #endregion
    #endregion
}
