# TigerForceLocalizationLib
 
## 介绍
 这是用来辅助本地化硬编码文本的模组.
 不止于汉化, 用来翻译为其他语言或者多语言都是可以的.

## 如何使用

### 添加依赖
1. 将 TigerForceLocalizationLib.dll 和 TigerForceLocalizationLib.xml (.xml 提供注释, 不是必须, 但建议携带) 放入你的模组源码中的 lib 文件夹下 (如果没有就创建一个).
    (.dll 和 .xml 都可以在 [Releases](https://github.com/TigerChenzzz/TerrariaTigerForceLocalizationLib/releases) 中获取)
1. 在 build.txt 中添加一行: `modReferences = TigerForceLocalizationLib`  (当你的 mod 有多个 modReferences 时可以用 ',' 隔开).<br/>
    或者也可以使用 `dllReferences = TigerForceLocalizationLib`, 这样则不用模组依赖.
1. 在你的项目中添加对此 dll 的引用.<br/>
	对于 VS, 在资源管理器中右键依赖项 -> 添加项目引用 -> 右下角浏览 -> 找到 lib 文件夹下的 dll 文件 -> 添加;<br/>
	或者也可以直接在 .csproj 文件中添加:
	```HTML
	<ItemGroup>
		<Reference Include="lib\*.dll" />
	</ItemGroup>
	```
    如果你有源码的话也可以直接添加此项目的引用 (此时要注意源码与 dll 需保持一致).
### 使用 `TigerForceLocalizationHelper.LocalizeAll` 直接本地化整个目标模组
1. 在 `Mod` 或 `ModSystem` 的 `PostSetupContent` 中使用 `TigerForceLocalizationHelper.LocalizeAll`, 并将 `registerKey` 参数设置为 `true`.
1. 连同目标模组一起加载一次, 本地化模组的 hjson 文件将会自动更新.
1. 将 `TigerForceLocalizationHelper.LocalizeAll` 的 `registerKey` 参数设置为 `false`.
1. 注意不同语言的 hjson 需要同步操作, 尤其是 en-US.
1. 将 hjson 中不需要本地化的项删除.<br/>
    注意删除需要保证索引仍然连续, 例如如果有 1, 2, 3, ... , n, 如果要删除 3, 那么可以在删除 3 后将 n 改为 3 并放在 3 原来的位置.
1. 将 hjson 中对应语言的 NewString 键中的文本改为对应语言的文本 (即进行实际的本地化工作).
- 如果要将一个方法中不同地方出现的相同的字符串改为不同的文本,
    则可以在 OldString 旁添加 NewString_1, NewString_2 ...
    代表第一, 第二次出现的字符串会被替换为什么;<br/>
    当出现次数多于 NewString_j 的个数时会使用默认的 NewString,
    如果 NewString 不存在则会使用 NewString_1 (也就是说当 NewString_1 存在时可以不用 NewString).<br/>
    修改时也要注意第四点.
#### 筛选需要本地化的内容
 可以向 `TigerForceLocalizationHelper.LocalizeAll` 传入 `filters` 参数以筛选需要本地化的内容.<br/>
 一般来说只需要在 `registerKey` 为 `true` 时传入此参数, 因为在它为 `false` 时 hjson 本身就起到了这个筛选作用.<br/>
 如果没有特殊需求可以使用库中预先设置好的过滤器:
```C#
TigerForceLocalizationHelper.LocalizeAll(selfMod, targetMod, true, filters: new() {
    MethodFilter = MethodFilter.CommonMethodFilter,
    CursorFilter = ILCursorFilter.CommonCursorFilter,
});
```
- 没有特殊需求的话记得在将 `registerKey` 改为 `false` 时删除 `filters` 参数以避免不必要的筛选.
#### 显示注册本地化键的进度
 在 `LocalizeAll` 的 `registerKey` 参数设置为 `true` 的初次运行时,
 往往会因为需要注册大量的键而卡在加载的最后阶段
 (一般这个时候进度条标题会显示为 "添加合成配方").<br/>
 在加载阶段 (`Load`, `OnModLoad`, `PostSetupContent` 等地方都可以) 使用 `TigerForceLocalizationHelper.ShowLocalizationRegisterProgress()`
 以在注册键时可以直观的看到其进度.

- 没有特殊需求的话记得在将 `registerKey` 改为 `false` 时删除此方法的调用.

### 使用 `TigerForceLocalizationHelper.LocalizeMethod` 本地化特定方法
其规则几乎与 `TigerForceLocalizationHelper.LocalizeAll` 一致, 只是针对特定方法而已.
### 使用 `ForceLocalizeSystem`
一般来说你需要新定义一个类继承自 `ForceLocalizeSystem<TSelf>`,
其中 `TSelf` 填此类本身的名字.
然后就可以用此类的 `Localize` 或 `LocalizeXxx` 静态方法以进行本地化.<p/>
当为弱引用时此类和用到此类的类或方法需要添加 `[JITWhenModsEnabled(modName)]`,
其中 `modName` 为需要被本地化的模组, 有可能还要添加 `[ExtendsFromMod(modName)]`.<p/>
此外, `ForceLocalizeSystemImpl<TMod>` 和 `ForceLocalizeSystemByLocalizeTextImpl<TMod>`
可以直接使用, 其中 `ForceLocalizeSystemByLocalizeTextImpl` 为使用 `Language.GetTextValue` 的方式代替字符串,
在使用它的 `Localize` 系方法时传入的新字符串应该是 hjson 的键.<p/>
更加详细的说明参见代码内的 xml 注释.
## 局限
此库目前仅能够替换方法中的直接的字符串使用, 对于初始化, 加载时就已经保存下来的字符串等特殊情况,
则仍需要额外的代码专门处理 (比如需要修改对应的字段).
## 杂项
### 开头空格的转义
因为 hjson 的某些问题,
当需要本地化的值的开头为空格或者制表符 ('\t') 或单引号时,
会在开头额外添加一个单引号,
此规则无论对于 OldString 或者 NewString 都适用.<p/>
例如若一个 NewString 需为 `" \" "`,
对于 hjson 它本应表示为 `NewString: ''' " '''`,
但现需写为 `NewString: '''' " '''` (此处NewString: 后为4个单引号),
对于多行字符串`" after whitespace\nthe second line"`则是:
~~~
NewString:
    '''
    ' after whitespace
    the second line
    '''
~~~
## 链接
示例: [TerrariaTigerForceLocalizationExample](https://github.com/TigerChenzzz/TerrariaTigerForceLocalizationExample)<br/>
[Steam 创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3358131784)
