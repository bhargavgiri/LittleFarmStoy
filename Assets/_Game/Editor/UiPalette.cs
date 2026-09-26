using UnityEngine;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// The UI colour system: semantic names, one definition each.
    ///
    /// The prototype HUD picked colours at the call site, which is how a UI ends up with four
    /// nearly-identical creams and no way to restate its intent. Here a widget asks for
    /// <see cref="Panel"/> or <see cref="Success"/> and never for a hex value, so the whole
    /// interface can be re-themed by editing this file alone.
    ///
    /// The hues are pulled toward the world palette - straw, soil, leaf, terracotta, sky - so
    /// the interface reads as part of the same farm rather than as a layer floating above it.
    /// Saturation is kept off the ceiling on purpose: colour carries hierarchy and state here,
    /// not decoration.
    /// </summary>
    public static class UiPalette
    {
        public static Color Hex(string hex)
        {
            return ProtoPalette.Hex(hex);
        }

        // ---------------------------------------------------------------- brand
        /// <summary>Ripe wheat. Primary actions, the coin, anything the eye should land on first.</summary>
        public static Color Primary => Hex("F2A93B");

        /// <summary>The shadow under Primary. Rims, pressed states, depth.</summary>
        public static Color PrimaryDeep => Hex("C4761A");

        /// <summary>Young leaf. Confirmation, growth, "this worked".</summary>
        public static Color Secondary => Hex("6FBF52");

        public static Color SecondaryDeep => Hex("46873A");

        /// <summary>Clear sky. Reserved for information that is neither an action nor a reward.</summary>
        public static Color Accent => Hex("57A8D8");

        // ---------------------------------------------------------------- state
        public static Color Success => Hex("5BA843");
        public static Color Warning => Hex("E08A16");
        public static Color Error => Hex("D2553C");
        public static Color Disabled => Hex("B9AC97");

        // ---------------------------------------------------------------- surfaces
        /// <summary>Warm parchment. Every card and chip sits on this.</summary>
        public static Color Panel => Hex("FFF8EA");

        /// <summary>One step down, for a recessed row or an inactive tab.</summary>
        public static Color PanelSunken => Hex("F0E3CA");

        /// <summary>The rim drawn around a panel. Warm, never grey.</summary>
        public static Color PanelEdge => Hex("D8C3A0");

        /// <summary>Sits behind a panel to lift it off the world.</summary>
        public static Color Shadow => new Color(0.20f, 0.14f, 0.08f, 0.26f);

        /// <summary>Full-screen dimmer behind a modal sheet.</summary>
        public static Color Scrim => new Color(0.16f, 0.11f, 0.07f, 0.52f);

        // ---------------------------------------------------------------- text
        /// <summary>Deep soil brown rather than black: black on cream reads as a spreadsheet.</summary>
        public static Color TextPrimary => Hex("4A3524");

        public static Color TextSecondary => Hex("8A755B");

        /// <summary>For text sitting on Primary or on a photograph of the world.</summary>
        public static Color TextOnDark => Hex("FFF8EA");

        // ---------------------------------------------------------------- touch controls
        /// <summary>The joystick well. Translucent so the farm stays visible through it.</summary>
        public static Color JoystickBase => new Color(1f, 0.97f, 0.90f, 0.22f);

        public static Color JoystickRim => new Color(1f, 0.97f, 0.90f, 0.40f);

        public static Color JoystickHandle => new Color(1f, 0.98f, 0.92f, 0.88f);
    }
}
