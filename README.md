# TigerForceLocalizationLib
 
## 介绍
 这是用来辅助本地化硬编码文本的模组.
 不止于汉化, 用来翻译为其他语言或者多语言都是可以的

## 如何使用

### 添加依赖
1. 将 TigerForceLocalizationLib.dll 和 TigerForceLocalizationLib.xml (.xml 提供注释, 不是必须, 但建议携带) 放入你的Mod中的 lib 文件夹下 (如果没有就创建一个)
1. 在 build.txt 中添加一行: `modReferences = TigerForceLocalizationLib`  (当你的 mod 有多个 modReferences 时可以用 ',' 隔开).
    或者也可以使用 `dllReferences = TigerForceLocalizationLib`, 这样则不用模组依赖
1. 在你的项目中添加对此 dll 的引用<br/>
	对于 VS, 在资源管理器中右键依赖项 -> 添加项目引用 -> 右下角浏览 -> 找到 lib 文件夹下的 dll 文件 -> 添加<br/>
	或者也可以直接在 .csproj 文件中添加:
	```HTML
	<ItemGroup>
		<Reference Include="lib\*.dll" />
	</ItemGroup>
	```
    如果你有源码的话也可以直接添加此项目的引用 (此时要注意源码与 dll 需保持一致)<br/>
### 使用 TigerForceLocalizationHelper.LocalizeAll 直接本地化整个目标模组
1. 在 Mod 或 ModSystem 中使用 TigerForceLocalizationHelper.LocalizeAll, 并将 registerKey 参数设置为 true
1. 连同目标模组一起加载一次, 本地化模组的 hjson 文件将会自动更新
1. 将 TigerForceLocalizationHelper.LocalizeAll 的 registerKey 参数设置为 false
1. 注意不同语言的 hjson 需要同步操作, 尤其是 en-US
1. 将 hjson 中不需要本地化的项删除. 注意删除需要保证索引仍然连续, 例如如果有 1, 2, 3, .. n, 如果要删除 3, 那么可以在删除 3 后将 n 改为 3 并放在 3 原来的位置
1. 将 hjson 中对应语言的 NewString 键中的文本改为对应语言的文本 (即进行实际的本地化工作)
1. 如果要将一个方法中不同地方出现的相同的字符串改为不同的文本,
    则可以在 OldString 旁添加 NewString_1, NewString_2 ...
    代表第一, 第二次出现的字符串会被替换为什么,
    当出现次数多于 NewString_j 的个数时会使用默认的 NewString,
    如果 NewString 不存在则会使用 NewString_1 (也就是说当 NewString_1 存在时可以不用 NewString).
    修改时也要注意第四点.
### 使用 TigerForceLocalizationHelper.LocalizeMethod 本地化特定方法
其规则几乎与 TigerForceLocalizationHelper.LocalizeAll 一致, 只是针对特定方法而已.
### 使用 ForceLocalizeSystem
一般来说你需要新定义一个类继承自 ForceLocalizeSystem&lt;TSelf>,
其中 TSelf 填此类本身的名字.
然后就可以用此类的 Localize 或 LocalizeXxx 静态方法以进行本地化.<br/>
当为弱引用时此类和用到此类的类或方法需要添加 `[JITWhenModsEnabled(modName)]`,
其中 modName 为需要被本地化的模组, 有可能还要添加 `[ExtendsFromMod(modName)]`.<br/>
此外, ForceLocalizeSystemImpl&lt;TMod> 和 ForceLocalizeSystemByLocalizeTextImpl&lt;TMod>
可以直接使用, 其中 ForceLocalizeSystemByLocalizeTextImpl 为使用 Language.GetTextValue 的方式代替字符串,
在使用它的 Localize 系方法时传入的新字符串应该是 hjson 的键.
更加详细的说明参见代码内的 xml 注释.
## 链接
示例: [TerrariaTigerForceLocalizationExample](https://github.com/TigerChenzzz/TerrariaTigerForceLocalizationExample)<br/>
[Steam 创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3358131784)
