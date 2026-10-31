namespace SushiAndFish.FriendsLife
{
    // C009 core gate. Off means: no new starts and no UI entry. Saved data of a
    // hidden feature is kept as it is. Turn flags back on one at a time, in the
    // order AutoRomance -> Cohabit -> HousingUpgrade -> Minigames -> Travel/Dreams -> Casino.
    public static class FriendsLifeFeatureFlags
    {
        public static readonly bool NativePairEvents = false;   // native Mii event graphs, C004 native motion
        public static readonly bool AutoRomance = false;        // Confess, Date, Propose, Wedding, honeymoon
        public static readonly bool Children = false;           // Baby, Birth, Care, Grow
        public static readonly bool Cohabit = false;            // family housing, moving in together
        public static readonly bool HousingUpgrade = false;     // house upgrade and split
        public static readonly bool Casino = false;             // casino journeys
        public static readonly bool PublicJobs = false;         // casino, cinema and town restaurant jobs
        public static readonly bool Minigames = false;
        public static readonly bool Travel = false;
        public static readonly bool Dreams = false;
        public static readonly bool Photography = false;        // photo studio and album
        public static readonly bool Creative = false;           // clothes, rooms, habits, UGC, pets, goods, traits, phrases, culture
        public static readonly bool IslandExtras = false;       // facilities, news, accidents, fountain, market, recycle, donate
        public static readonly bool DebugActivityPage = false;  // "当前活动" stop-activity page
    }
}
