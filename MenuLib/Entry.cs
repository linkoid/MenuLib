﻿using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MenuLib
{
    [BepInPlugin("nickklmao.menulib", MOD_NAME, "2.0.0")]
    internal sealed class Entry : BaseUnityPlugin
    {
        private const string MOD_NAME = "Menu Lib";
        
        internal static readonly ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(MOD_NAME);
        
        private static void MenuPageMain_StartHook(Action<MenuPageMain> orig, MenuPageMain self)
        {
            orig.Invoke(self);
            MenuAPI.mainMenuBuilderDelegates?.Invoke(self.transform);
        }
        
        private static void MenuPageEsc_StartHook(Action<MenuPageEsc> orig, MenuPageEsc self)
        {
            orig.Invoke(self);
			MenuAPI.escapeMenuBuilderDelegates?.Invoke(self.transform);
        }
        
        private static void MenuPageLobby_StartHook(Action<MenuPageLobby> orig, MenuPageLobby self)
        {
            orig.Invoke(self);
            MenuAPI.lobbyMenuBuilderDelegate?.Invoke(self.transform);
        }
        
        private static void SemiFunc_UIMouseHoverILHook(ILContext il)
        {
            var cursor = new ILCursor(il);

            cursor.GotoNext(instruction => instruction.MatchBrfalse(out var label) && label.Target.OpCode == OpCodes.Ldarg_1);

            cursor.Index += 2;
            cursor.RemoveRange(27);

            cursor.Emit(OpCodes.Ldloc_0);
            cursor.EmitDelegate((MenuScrollBox menuScrollBox, Vector2 vector) =>
            {
                var mask = (RectTransform) menuScrollBox.scroller.parent;

                var bottom = mask.position.y;
                var top = bottom + mask.sizeDelta.y;

                return vector.y > bottom && vector.y < top;
            });

            var jumpToLabel = cursor.DefineLabel();

            cursor.Emit(OpCodes.Brtrue_S, jumpToLabel);
            cursor.Emit(OpCodes.Ldc_I4_0);
            cursor.Emit(OpCodes.Ret);

            cursor.MarkLabel(jumpToLabel);
        }

        private static void MenuPage_StateClosingILHook(ILContext il)
        {
            var cursor = new ILCursor(il);

            cursor.GotoNext(instruction => instruction.MatchCall<Object>("Destroy"));

            cursor.Index -= 5;
            cursor.RemoveRange(6);

            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate((MenuPage menuPage) =>
            {
                if (!MenuAPI.cachedMenuPages.Contains(menuPage))
                {
                    MenuManager.instance.PageRemove(menuPage);
                    Destroy(menuPage.gameObject);
                }
                else
                {
                    menuPage.gameObject.SetActive(false);
                    menuPage.PageStateSet(MenuPage.PageState.Opening);
                }
            });
        }
        
        private void Awake()
        {
            logger.LogDebug("Hooking `MenuPageMain.Start`");
            new Hook(AccessTools.Method(typeof(MenuPageMain), "Start"), MenuPageMain_StartHook);
            
            logger.LogDebug("Hooking `MenuPageEsc.Start`");
            new Hook(AccessTools.Method(typeof(MenuPageEsc), "Start"), MenuPageEsc_StartHook);
            
            logger.LogDebug("Hooking `MenuPageLobby.Start`");
            new Hook(AccessTools.Method(typeof(MenuPageLobby), "Start"), MenuPageLobby_StartHook);
            
            logger.LogDebug("Hooking `SemiFunc.UIMouseHover`");
            new ILHook(AccessTools.Method(typeof(SemiFunc), "UIMouseHover"), SemiFunc_UIMouseHoverILHook);
            
            logger.LogDebug("Hooking `MenuPage.StateClosing`");
            new ILHook(AccessTools.Method(typeof(MenuPage), "StateClosing"), MenuPage_StateClosingILHook);
        }
    }
}