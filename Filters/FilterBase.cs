using System.Reflection;
using System;

namespace TigerForceLocalizationLib.Filters;

/// <summary>
/// 用以筛选一个类型 <typeparamref name="T"/>
/// </summary>
/// <param name="filter">筛选规则, 返回 <see langword="true"/> 代表通过筛选</param>
public class FilterBase<T>(Func<T, bool> filter) {
    /// <summary>
    /// 筛选规则, 返回 <see langword="true"/> 代表通过筛选
    /// </summary>
    public Func<T, bool> Filter => filter;
}
