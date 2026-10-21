namespace Netcode.Transports.Local
{
    // This project starts a local, host-only game. The identifier is a local
    // save-profile key, never a Steam authentication result or online ticket.
    public static class OfflineSingleplayerMode
    {
        public static bool Enabled => true;
        public static ulong LocalUserId {get;private set;}=1;
        public static void SelectSaveProfile(string existingOwner)
        {LocalUserId=ulong.TryParse(existingOwner,out var id)&&id!=0?id:1;}
    }
}
