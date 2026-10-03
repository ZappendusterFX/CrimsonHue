namespace CrimsonHue.Core;

public sealed record OverlayShortcut(int Key = 'H', bool Control = true, bool Shift = true, bool Alt = false)
{
    public static bool IsAllowedKey(int key) => key is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A or
        >= 0x60 and <= 0x87 or 0x08 or 0x09 or 0x0D or 0x13 or 0x1B or >= 0x20 and <= 0x28 or
        0x2D or 0x2E or 0x91 or >= 0xBA and <= 0xC0 or >= 0xDB and <= 0xDF;

    public void Validate()
    {
        if (!IsAllowedKey(Key)) throw new CrimsonHueException("Choose a keyboard key for the overlay shortcut.");
    }
}
