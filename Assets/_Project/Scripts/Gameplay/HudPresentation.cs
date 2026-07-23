namespace ProjectC.Gameplay
{
    public enum HudPresentationMode
    {
        Auto = 0,
        Mobile = 1,
        Desktop = 2
    }

    public static class HudPresentation
    {
        public static HudPresentationMode Resolve(
            HudPresentationMode requestedMode,
            bool isMobilePlatform)
        {
            if (requestedMode != HudPresentationMode.Auto)
                return requestedMode;
            return isMobilePlatform
                ? HudPresentationMode.Mobile
                : HudPresentationMode.Desktop;
        }
    }
}
