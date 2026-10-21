public static partial class CityExpansionInstallation
{
    public static void RefreshMenuAndAudit()
    {
        IslandFrontEndBuilder.RefreshMenuPresentation();
        CityExpansionPlayAudit.All();
    }
}
