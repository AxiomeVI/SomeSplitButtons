using Celeste.Mod.SomeSplitButtons.Splits;
using Celeste.Mod.SomeSplitButtons.UI;
using Monocle;

namespace Celeste.Mod.SomeSplitButtons.UI;

public static class ModMenuOptions {
    public static void CreateMenu(TextMenu menu)
    {
        TextMenu.OnOff showSkipCutsceneSplitButton = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.EnableSkipCutsceneSplitButtonId),
            SomeSplitButtonsModule.Settings.ShowSkipCutsceneSplitButton).Change(
                b => SplitFeatures.SkipCutscene.Toggle(b)
        );

        TextMenu.OnOff saveAndQuitAndRetry = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.SaveAndQuitAndRetryId),
            SomeSplitButtonsModule.Settings.SaveAndQuitAndRetry).Change(
                b => SomeSplitButtonsModule.Settings.SaveAndQuitAndRetry = b
        );

        TextMenu.OnOff showSaveAndQuitSplitButton = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.EnableSaveAndQuitSplitButtonId),
            SomeSplitButtonsModule.Settings.ShowSaveAndQuitSplitButton).Change(
                b =>
                {
                    SplitFeatures.SaveAndQuit.Toggle(b);
                    saveAndQuitAndRetry.Disabled = !b;
                }
        );

        TextMenu.OnOff showReturnToMapSplitButton = (TextMenu.OnOff)new TextMenu.OnOff(
            Dialog.Clean(DialogIds.EnableReturnToMapSplitButtonId),
            SomeSplitButtonsModule.Settings.ShowReturnToMapSplitButton).Change(
                b => SplitFeatures.ReturnToMap.Toggle(b)
        );

        TextMenu.Button keybindButton = new TextMenu.Button(Dialog.Clean(DialogIds.KeybindConfigId));
        keybindButton.Pressed(() => {
            menu.Focused = false;
            var ui = new KeybindConfigUi();
            ui.OnClose = () => menu.Focused = true;
            Engine.Scene.Add(ui);
            Engine.Scene.OnEndOfFrame += () => Engine.Scene.Entities.UpdateLists();
        });

        // Everything below the master toggle appears and disappears with it. Written once and called
        // from both the initial state and the toggle's Change, because the two used to be separate
        // copies of the same five assignments — the kind of pair that stays in step until someone
        // adds a sixth entry to one of them.
        void SetSubOptionsVisible(bool visible) {
            showSkipCutsceneSplitButton.Visible = visible;
            showSaveAndQuitSplitButton.Visible = visible;
            saveAndQuitAndRetry.Visible = visible;
            showReturnToMapSplitButton.Visible = visible;
            keybindButton.Visible = visible;
            // Not part of the visibility rule, but always true alongside it: the retry option belongs
            // to the Save and Quit button and is greyed out whenever that button is off.
            saveAndQuitAndRetry.Disabled = !SomeSplitButtonsModule.Settings.ShowSaveAndQuitSplitButton;
        }

        menu.Add(new TextMenu.OnOff(Dialog.Clean(DialogIds.EnabledId), SomeSplitButtonsModule.Settings.Enabled).Change(
            value =>
            {
                SomeSplitButtonsModule.Settings.Enabled = value;
                SetSubOptionsVisible(value);
                SplitFeatures.ResetAll();
                // Unconditional where this used to test ShowSkipCutsceneSplitButton as well. What it
                // refreshes is only ever read by an enabled feature's Update, and a level load
                // refreshes it regardless — so the extra test could only ever agree with what was
                // already there.
                if (value && Engine.Scene is Level level) SplitFeatures.RefreshAll(level);
            }
        ));

        menu.Add(showReturnToMapSplitButton);
        menu.Add(showSkipCutsceneSplitButton);
        menu.Add(showSaveAndQuitSplitButton);
        menu.Add(saveAndQuitAndRetry);
        menu.Add(keybindButton);

        SetSubOptionsVisible(SomeSplitButtonsModule.Settings.Enabled);

        // After the Add calls, not before: AddDescription inserts the description at the option's
        // index in the menu and does nothing at all when the option is not in it yet.
        //
        // The descriptions are not listed in SetSubOptionsVisible, and must not be. They start
        // invisible and only fade in from the option's OnEnter — an option hidden by the master
        // toggle can never be hovered, so it can never show its description.
        showSkipCutsceneSplitButton.AddDescription(menu, Dialog.Clean(DialogIds.EnableSkipCutsceneSplitButtonDescId));
        saveAndQuitAndRetry.AddDescription(menu, Dialog.Clean(DialogIds.SaveAndQuitAndRetryDescId));
    }
}
