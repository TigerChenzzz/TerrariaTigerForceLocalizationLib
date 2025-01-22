using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.Localization;

namespace TigerForceLocalizationLib.Filters;

/// <summary>
/// 用以筛选<see cref="ILCursor"/>
/// </summary>
/// <inheritdoc/>
public class ILCursorFilter(Func<ILCursor, bool> filter) : FilterBase<ILCursor>(filter) {
    #region 运算符重载
    /// <summary>
    /// 两个筛选规则满足其一即可
    /// </summary>
    public static ILCursorFilter operator |(ILCursorFilter left, ILCursorFilter right) {
        return new(cursor => left.Filter(cursor) || right.Filter(cursor));
    }
    /// <summary>
    /// 两个筛选规则需同时满足
    /// </summary>
    public static ILCursorFilter operator &(ILCursorFilter left, ILCursorFilter right) {
        return new(cursor => left.Filter(cursor) && right.Filter(cursor));
    }
    /// <summary>
    /// 筛选规则不满足
    /// </summary>
    public static ILCursorFilter operator !(ILCursorFilter self) => new(cursor => !self.Filter(cursor));
    /// <summary>
    /// 多个筛选规则需同时满足
    /// </summary>
    public static ILCursorFilter MatchAll(params ILCursorFilter[] filters) => new(cursor => filters.All(f => f.Filter(cursor)));
    /// <summary>
    /// 多个筛选规则满足其一即可
    /// </summary>
    public static ILCursorFilter MatchAny(params ILCursorFilter[] filters) => new(cursor => filters.Any(f => f.Filter(cursor)));
    #endregion
    #region 常用
    /// <summary>
    /// 忽略在 Language.Xxx 方法中的字符串
    /// </summary>
    public static ILCursorFilter NoLanguageMethods => new(cursor => {
        var methodReference = FindMethodUsage(cursor);
        if (methodReference == null)
            return true;
        if (methodReference.DeclaringType.Is(typeof(Language)))
            return false;
        return true;
    });

    /// <summary>
    /// 忽略通常的调用本地化相关的字符串
    /// </summary>
    public static ILCursorFilter NoCommonLanguageMethods => new(cursor => {
        var methodReference = FindMethodUsage(cursor);
        if (methodReference == null)
            return true;
        if (methodReference.DeclaringType.Is(typeof(Language)))
            return false;
        if (methodReference.DeclaringType.Is(typeof(LanguageManager)))
            return false;
        return true;
    });
    private readonly static HashSet<string> CommonFilteredTypeFullNames = [
        #region 本地化
        typeof(Language).FullName,
        typeof(LanguageManager).FullName,
        #endregion

        #region TML 内
        #region 主要
        typeof(Terraria.ModLoader.Mod).FullName, // 可能会漏掉 mod.Call
        typeof(Terraria.ModLoader.ModContent).FullName,
	    #endregion

        #region Terraria
        typeof(Terraria.Chest).FullName,
        typeof(Terraria.Condition).FullName,
        typeof(Terraria.Entity).FullName, // 各种 GetSource
        typeof(Terraria.Lang).FullName,
        typeof(nativefiledialog).FullName, // <- 这东西没有命名空间的
        typeof(Terraria.Netplay).FullName,
        typeof(Terraria.Recipe).FullName,
        typeof(Terraria.RecipeGroup).FullName,
	    #endregion
        #region Terraria.Achievements
        typeof(Terraria.Achievements.Achievement).FullName,
        typeof(Terraria.Achievements.AchievementManager).FullName,
	    #endregion
        #region Terraria.Audio
        typeof(Terraria.Audio.ASoundEffectBasedAudioTrack).FullName,
        typeof(Terraria.Audio.CueAudioTrack).FullName,
        typeof(Terraria.Audio.DisabledAudioSystem).FullName,
        typeof(Terraria.Audio.LegacyAudioSystem).FullName,
        typeof(Terraria.Audio.MusicCueHolder).FullName,
        typeof(Terraria.Audio.SoundStyle).FullName,
	    #endregion
        #region Terraria.Chat
        typeof(Terraria.Chat.ChatCommandId).FullName,
	    #endregion
        #region Terraria.DataStructures
        typeof(Terraria.DataStructures.AEntitySource_Tile).FullName,
        typeof(Terraria.DataStructures.EntitySource_BossSpawn).FullName,
        typeof(Terraria.DataStructures.EntitySource_Buff).FullName,
        typeof(Terraria.DataStructures.EntitySource_Caught).FullName,
        typeof(Terraria.DataStructures.EntitySource_Death).FullName,
        typeof(Terraria.DataStructures.EntitySource_DebugCommand).FullName,
        typeof(Terraria.DataStructures.EntitySource_DropAsItem).FullName,
        typeof(Terraria.DataStructures.EntitySource_Film).FullName,
        typeof(Terraria.DataStructures.EntitySource_FishedOut).FullName,
        typeof(Terraria.DataStructures.EntitySource_Gift).FullName,
        typeof(Terraria.DataStructures.EntitySource_ItemOpen).FullName,
        typeof(Terraria.DataStructures.EntitySource_ItemUse).FullName,
        typeof(Terraria.DataStructures.EntitySource_ItemUse_OnHurt).FullName,
        typeof(Terraria.DataStructures.EntitySource_ItemUse_WithAmmo).FullName,
        typeof(Terraria.DataStructures.EntitySource_Loot).FullName,
        typeof(Terraria.DataStructures.EntitySource_Misc).FullName,
        typeof(Terraria.DataStructures.EntitySource_Mount).FullName,
        "Terraria.DataStructures.EntitySource_HitEffect", // Obsolete
        typeof(Terraria.DataStructures.EntitySource_OldOnesArmy).FullName,
        typeof(Terraria.DataStructures.EntitySource_OnHit).FullName,
        typeof(Terraria.DataStructures.EntitySource_OnHurt).FullName,
        typeof(Terraria.DataStructures.EntitySource_OverfullChest).FullName,
        typeof(Terraria.DataStructures.EntitySource_OverfullInventory).FullName,
        typeof(Terraria.DataStructures.EntitySource_Parent).FullName,
        typeof(Terraria.DataStructures.EntitySource_RevengeSystem).FullName,
        typeof(Terraria.DataStructures.EntitySource_ShakeTree).FullName,
        typeof(Terraria.DataStructures.EntitySource_SpawnNPC).FullName,
        typeof(Terraria.DataStructures.EntitySource_Sync).FullName,
        typeof(Terraria.DataStructures.EntitySource_TileBreak).FullName,
        typeof(Terraria.DataStructures.EntitySource_TileEntity).FullName,
        typeof(Terraria.DataStructures.EntitySource_TileInteraction).FullName,
        typeof(Terraria.DataStructures.EntitySource_TileUpdate).FullName,
        typeof(Terraria.DataStructures.EntitySource_Wiring).FullName,
        typeof(Terraria.DataStructures.EntitySource_WorldEvent).FullName,
        typeof(Terraria.DataStructures.EntitySource_WorldGen).FullName,
        // typeof(Terraria.DataStructures.MethodSequenceListItem).FullName, // 不知道是什么
        // typeof(Terraria.DataStructures.PlayerDeathReason).FullName, // 既然它标了 reasion in english...
	    #endregion
        #region Terraria.GameContent
        typeof(Terraria.GameContent.Profiles.LegacyNPCProfile).FullName,
        typeof(Terraria.GameContent.Profiles.TransformableNPCProfile).FullName,
        typeof(Terraria.GameContent.Profiles.VariantNPCProfile).FullName,
        typeof(Terraria.GameContent.Profiles.DefaultNPCProfile).FullName,
        typeof(Terraria.GameContent.ShopHelper).FullName,
        typeof(Terraria.GameContent.TownNPCProfiles).FullName,
        typeof(Terraria.GameContent.VanillaContentValidator).FullName,
	    #endregion
        #region Terraria.GameContent.Animations
        typeof(Terraria.GameContent.Animations.Segments.LocalizedTextSegment).FullName,
        typeof(Terraria.GameContent.Animations.Segments.SpriteSegment.MaskedFadeEffect).FullName,
	    #endregion
        #region Terraria.GameContent.Bestiary
        typeof(Terraria.GameContent.Bestiary.BestiaryEntry).FullName,
        typeof(Terraria.GameContent.Bestiary.CommonEnemyUICollectionInfoProvider).FullName,
        typeof(Terraria.GameContent.Bestiary.CritterUICollectionInfoProvider).FullName,
        typeof(Terraria.GameContent.Bestiary.CustomEntryIcon).FullName,
        typeof(Terraria.GameContent.Bestiary.FilterProviderInfoElement).FullName,
        typeof(Terraria.GameContent.Bestiary.FlavorTextBestiaryInfoElement).FullName,
        typeof(Terraria.GameContent.Bestiary.GoldCritterUICollectionInfoProvider).FullName,
        // typeof(Terraria.GameContent.Bestiary.ModBiomeBestiaryInfoElement).FullName, // 为什么你要给个 displayName??? 像别个给个 languageKey 不好吗???
        // typeof(Terraria.GameContent.Bestiary.ModSourceBestiaryInfoElement).FullName, // displayName???
        typeof(Terraria.GameContent.Bestiary.NamePlateInfoElement).FullName,
        typeof(Terraria.GameContent.Bestiary.NPCKillsTracker).FullName,
        typeof(Terraria.GameContent.Bestiary.NPCWasChatWithTracker).FullName,
        typeof(Terraria.GameContent.Bestiary.NPCWasNearPlayerTracker).FullName,
        typeof(Terraria.GameContent.Bestiary.SalamanderShellyDadUICollectionInfoProvider).FullName,
        typeof(Terraria.GameContent.Bestiary.SpawnConditionBestiaryInfoElement).FullName,
        typeof(Terraria.GameContent.Bestiary.SpawnConditionBestiaryOverlayInfoElement).FullName,
        typeof(Terraria.GameContent.Bestiary.SpawnConditionDecorativeOverlayInfoElement).FullName,
        typeof(Terraria.GameContent.Bestiary.TownNPCUICollectionInfoProvider).FullName,
        typeof(Terraria.GameContent.Bestiary.UnlockableNPCEntryIcon).FullName,
	    #endregion
        #region Terraria.GameContent.Creative
        typeof(Terraria.GameContent.Creative.CreativePowerManager).FullName,
        typeof(Terraria.GameContent.Creative.CreativePowersHelper).FullName,
        typeof(Terraria.GameContent.Creative.ItemsSacrificedUnlocksTracker).FullName,
	    #endregion
        #region Terraria.GameContent.Dyes
        typeof(Terraria.GameContent.Dyes.ReflectiveArmorShaderData).FullName,
        typeof(Terraria.GameContent.Dyes.TeamArmorShaderData).FullName,
        typeof(Terraria.GameContent.Dyes.TwilightDyeShaderData).FullName,
        typeof(Terraria.GameContent.Dyes.TwilightHairDyeShaderData).FullName,
	    #endregion
        #region Terraria.GameContent.Generation
        typeof(Terraria.GameContent.Generation.PassLegacy).FullName,
	    #endregion
        #region Terraria.GameContent.Shaders
        typeof(Terraria.GameContent.Shaders.BlizzardShaderData).FullName,
        typeof(Terraria.GameContent.Shaders.BloodMoonScreenShaderData).FullName,
        typeof(Terraria.GameContent.Shaders.MoonLordScreenShaderData).FullName,
        typeof(Terraria.GameContent.Shaders.SandstormShaderData).FullName,
        typeof(Terraria.GameContent.Shaders.SepiaScreenShaderData).FullName,
        typeof(Terraria.GameContent.Shaders.WaterShaderData).FullName,
	    #endregion
        #region Terraria.GameContent.Skies.CreditsRoll
        typeof(Terraria.GameContent.Skies.CreditsRoll.CreditsRollComposer).FullName,
	    #endregion
        #region Terraria.GameContent.UI.Elements
        typeof(Terraria.GameContent.UI.Elements.PowerStripUIElement).FullName,
        typeof(Terraria.GameContent.UI.Elements.UICreativeItemsInfiniteFilteringOptions).FullName,
        typeof(Terraria.GameContent.UI.Elements.UIIconTextButton).FullName,
        typeof(Terraria.GameContent.UI.Elements.UIKeybindingListItem).FullName,
	    #endregion
        #region Terraria.GameContent.UI.Minimap
        typeof(Terraria.GameContent.UI.Minimap.MinimapFrameManager).FullName,
        typeof(Terraria.GameContent.UI.Minimap.MinimapFrameTemplate).FullName,
	    #endregion
        #region Terraria.GameContent.UI.ResourceSets
        typeof(Terraria.GameContent.UI.ResourceSets.ClassicPlayerResourcesDisplaySet).FullName,
        typeof(Terraria.GameContent.UI.ResourceSets.FancyClassicPlayerResourcesDisplaySet).FullName,
        typeof(Terraria.GameContent.UI.ResourceSets.HorizontalBarsPlayerResourcesDisplaySet).FullName,
        typeof(Terraria.GameContent.UI.ResourceSets.PlayerResourceSetsManager).FullName,
	    #endregion
        #region Terraria.GameContent.UI.States
        typeof(Terraria.GameContent.UI.States.UIGamepadHelper).FullName,
        typeof(Terraria.GameContent.UI.States.UIManageControls).FullName,
        typeof(Terraria.GameContent.UI.States.UIResourcePackSelectionMenu).FullName,
        typeof(Terraria.GameContent.UI.States.UIWorldCreation).FullName,
	    #endregion
        #region Terraria.GameInput
        typeof(Terraria.GameInput.KeyConfiguration).FullName,
        typeof(Terraria.GameInput.PlayerInput).FullName,
        typeof(Terraria.GameInput.PlayerInputProfile).FullName,
	    #endregion
        #region Terraria.Graphics
        typeof(Terraria.Graphics.WindowStateController).FullName,
	    #endregion
        #region Terraria.Graphics.CameraModifiers
        typeof(Terraria.Graphics.CameraModifiers.PunchCameraModifier).FullName,
	    #endregion
        #region Terraria.Graphics.Effects
        typeof(Terraria.Graphics.Effects.EffectManager<Terraria.Graphics.Effects.Filter>).FullName, // FilterManager
        typeof(Terraria.Graphics.Effects.EffectManager<Terraria.Graphics.Effects.Overlay>).FullName, // OverlayManager
        typeof(Terraria.Graphics.Effects.EffectManager<Terraria.Graphics.Effects.CustomSky>).FullName, // SkyManager
        typeof(Terraria.Graphics.Effects.SimpleOverlay).FullName,
	    #endregion
        #region Terraria.Graphics.Shaders
        typeof(Terraria.Graphics.Shaders.ArmorShaderData).FullName,
        typeof(Terraria.Graphics.Shaders.HairShaderData).FullName,
        typeof(Terraria.Graphics.Shaders.MiscShaderData).FullName,
        typeof(Terraria.Graphics.Shaders.ScreenShaderData).FullName,
        typeof(Terraria.Graphics.Shaders.ShaderData).FullName,
	    #endregion
        #region Terraria.ID
        typeof(Terraria.ID.ItemID).FullName, // FromLegacyName
        typeof(Terraria.ID.NPCID).FullName, // FromLegacyName
        typeof(Terraria.ID.SoundID).FullName,
	    #endregion
        #region Terraria.Initializers
        typeof(Terraria.Initializers.AssetInitializer).FullName, // LoadAsset<>
	    #endregion
        #region Terraria.IO
        typeof(Terraria.IO.FavoritesFile).FullName,
        typeof(Terraria.IO.FileData).FullName,
        typeof(Terraria.IO.GameConfiguration).FullName,
        typeof(Terraria.IO.PlayerFileData).FullName,
        typeof(Terraria.IO.Preferences).FullName,
        typeof(Terraria.IO.ResourcePack).FullName,
        typeof(Terraria.IO.ResourcePackList).FullName,
        typeof(Terraria.IO.WorldFile).FullName,
        typeof(Terraria.IO.WorldFileData).FullName,
	    #endregion
        #region Terraria.Localization
        typeof(Terraria.Localization.GameCulture).FullName,
	    #endregion
        #region Terraria.ModLoader
        typeof(Terraria.ModLoader.BackgroundTextureLoader).FullName,
        typeof(Terraria.ModLoader.DamageClass).FullName, // ShowStatTooltipLine
        typeof(Terraria.ModLoader.EquipLoader).FullName,
        typeof(Terraria.ModLoader.ILocalizedModTypeExtensions).FullName,
        typeof(Terraria.ModLoader.KeybindLoader).FullName,
        typeof(Terraria.ModLoader.ModLoader).FullName,
        typeof(Terraria.ModLoader.MusicLoader).FullName,
        typeof(Terraria.ModLoader.NPCHeadLoader).FullName,
        // typeof(Terraria.ModLoader.NPCLoader).FullName, // 是否需要?
        typeof(Terraria.ModLoader.NPCShopDatabase).FullName,
	    #endregion
        #region Terraria.ModLoader.Assets
        typeof(Terraria.ModLoader.Assets.AssemblyResourcesContentSource).FullName,
        typeof(Terraria.ModLoader.Assets.TModContentSource).FullName,
	    #endregion
        #region Terraria.ModLoader.Config
        typeof(Terraria.ModLoader.Config.ConfigManager).FullName,
        typeof(Terraria.ModLoader.Config.ItemDefinition).FullName,
        typeof(Terraria.ModLoader.Config.ProjectileDefinition).FullName,
        typeof(Terraria.ModLoader.Config.NPCDefinition).FullName,
        typeof(Terraria.ModLoader.Config.PrefixDefinition).FullName,
        typeof(Terraria.ModLoader.Config.BuffDefinition).FullName,
        typeof(Terraria.ModLoader.Config.TileDefinition).FullName,
	    #endregion
        #region Terraria.ModLoader.Core
        typeof(Terraria.ModLoader.Core.AssemblyManager).FullName,
        typeof(Terraria.ModLoader.Core.TmodFile).FullName,
	    #endregion
        #region Terraria.ModLoader.IO
        typeof(Terraria.ModLoader.IO.TagCompound).FullName,
        typeof(Terraria.ModLoader.IO.TagIO).FullName,
        typeof(Terraria.ModLoader.IO.UploadFile).FullName,
	    #endregion
        #region Terraria.Social.Base
        typeof(Terraria.Social.Base.AchievementsSocialModule).FullName,
        typeof(Terraria.Social.Base.CloudSocialModule).FullName,
        typeof(Terraria.Social.Base.ModWorkshopEntry).FullName,
        typeof(Terraria.Social.Base.WorkshopSocialModule).FullName,
        typeof(Terraria.Social.Base.WorkshopTagOption).FullName,
	    #endregion
        #region Terraria.Social.Steam
        typeof(Terraria.Social.Steam.AchievementsSocialModule).FullName,
        typeof(Terraria.Social.Steam.CloudSocialModule).FullName,
        typeof(Terraria.Social.Steam.SteamedWraps).FullName,
        typeof(Terraria.Social.Steam.WorkshopSocialModule).FullName,
	    #endregion
        #region Terraria.Social.WeGame
        typeof(Terraria.Social.WeGame.AchievementsSocialModule).FullName,
        typeof(Terraria.Social.WeGame.CloudSocialModule).FullName,
        // ... WeGame 还是不管了吧
	    #endregion
        #region Terraria.UI
        typeof(Terraria.UI.GameInterfaceLayer).FullName,
        typeof(Terraria.UI.SnapPoint).FullName,
        typeof(Terraria.UI.UIElement).FullName, // SetSnapPoint
	    #endregion
        #region Terraria.Utilities
        typeof(Terraria.Utilities.CrashDump).FullName,
        typeof(Terraria.Utilities.FileUtilities).FullName,
	    #endregion
        #region Terraria.Utilities.FileBrowser
        typeof(Terraria.Utilities.FileBrowser.ExtensionFilter).FullName,
	    #endregion
        #region Terraria.WorldBuilding
        typeof(Terraria.WorldBuilding.WorldGenConfiguration).FullName,
	    #endregion
	    #endregion

        #region TML 外
        #region System
        typeof(System.Type).FullName,
        "System.RuntimeType",
	    #endregion
        #region System.IO
        typeof(System.IO.Directory).FullName,
        typeof(System.IO.File).FullName,
        typeof(System.IO.Path).FullName,
	    #endregion
        #region System.Reflection
        typeof(System.Reflection.Assembly).FullName,
        typeof(System.Reflection.TypeInfo).FullName,
	    #endregion
	    #endregion
    ];

    /// <summary>
    /// 能够筛除一些基本不可能需要本地化的字符串
    /// </summary>
    public static ILCursorFilter CommonCursorFilter => new(cursor => {
        var methodReference = FindMethodUsage(cursor);
        if (methodReference == null)
            return true;
        if (CommonFilteredTypeFullNames.Contains(methodReference.DeclaringType.FullName))
            return false;
        // 待筛类型: Main, Mod, NPC, Player, WorkshopIssueReporter, Utils
        // 泛型: EffectManager, ModTypeLookup
        // 筛选参数: TooltipLine 构造, Condition 构造?
        return true;
    });

    /// <summary>
    /// 筛选对此字符串的方法调用
    /// </summary>
    /// <param name="methodFilter">如果找不到方法调用, 那么参数为 <see langword="null"/></param>
    public static ILCursorFilter MatchMethodUsage(Func<MethodReference?, bool> methodFilter) => new(cursor => methodFilter(FindMethodUsage(cursor)));

    // TODO: 筛选字典取键 ("get_Item")
    #endregion
    #region 辅助方法
    private static int PushCount(OpCode code) => PushCount(code.StackBehaviourPush);
    private static int PopCount(OpCode code) => -PushCount(code.StackBehaviourPop);
    private static int PushCount(StackBehaviour behaviour) {
        return behaviour switch {
            StackBehaviour.Pop0 => 0,
            StackBehaviour.Pop1 => -1,
            StackBehaviour.Pop1_pop1 => -2,
            StackBehaviour.Popi => -1,
            StackBehaviour.Popi_pop1 => -2,
            StackBehaviour.Popi_popi => -2,
            StackBehaviour.Popi_popi8 => -2,
            StackBehaviour.Popi_popi_popi => -3,
            StackBehaviour.Popi_popr4 => -2,
            StackBehaviour.Popi_popr8 => -2,
            StackBehaviour.Popref => -1,
            StackBehaviour.Popref_pop1 => -2,
            StackBehaviour.Popref_popi => -2,
            StackBehaviour.Popref_popi_popi => -3,
            StackBehaviour.Popref_popi_popi8 => -3,
            StackBehaviour.Popref_popi_popr4 => -3,
            StackBehaviour.Popref_popi_popr8 => -3,
            StackBehaviour.Popref_popi_popref => -3,
            StackBehaviour.PopAll => int.MinValue / 2,
            StackBehaviour.Push0 => 0,
            StackBehaviour.Push1 => 1,
            StackBehaviour.Push1_push1 => 2,
            StackBehaviour.Pushi => 1,
            StackBehaviour.Pushi8 => 1,
            StackBehaviour.Pushr4 => 1,
            StackBehaviour.Pushr8 => 1,
            StackBehaviour.Pushref => 1,
            // StackBehaviour.Varpop => 0,
            // StackBehaviour.Varpush => 0,
            _ => 0,
        };
    }
    private static MethodReference? FindMethodUsage(ILCursor cursor) {
        var ins = cursor.Next;
        if (ins == null)
            return null;
        int stack = 1;
        for (ins = ins.Next; ins != null; ins = ins.Next) {
            // TODO?: 跳转处理
            if (ins.OpCode.FlowControl != FlowControl.Next && ins.OpCode.FlowControl != FlowControl.Call)
                return null;
            if (ins.MatchCallOrCallvirt(out var methodReference) || ins.MatchNewobj(out methodReference)) {
                stack -= !methodReference.HasParameters ? 0 : methodReference.Parameters.Count;
                if (stack <= 0)
                    return methodReference;
                if (methodReference.ReturnType != null)
                    stack += 1;
            }
            else if (ins.MatchCalli(out var methodSignature)) {
                stack -= !methodSignature.HasParameters ? 0 : methodSignature.Parameters.Count;
                if (stack <= 0)
                    return null;
                if (methodSignature.ReturnType != null)
                    stack += 1;
            }
            else {
                stack -= PopCount(ins.OpCode);
                if (stack <= 0)
                    return null;
                stack += PushCount(ins.OpCode);
            }
        }
        return null;
    }
    #endregion
}
