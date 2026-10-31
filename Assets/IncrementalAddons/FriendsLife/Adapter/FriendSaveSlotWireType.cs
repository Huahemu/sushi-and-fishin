using System;
using ES3Internal;
using ES3Types;
using PaperPixelGames.Core.SaveSystem;
using UnityEngine.Scripting;

namespace SushiAndFish.FriendsLife
{
    // Registered ONLY under the new derived CLR type. Original SaveStorage
    // verifies the physical header against its declared SaveSlotData type.
    // ES3File.Save uses runtime type metadata for that header, so this adapter
    // advertises the original wire header while retaining the normal derived
    // __type discriminator inside the value. No original type handler is
    // replaced and no type checking or write protection setting is relaxed.
    [Preserve]
    public sealed class FriendSaveSlotWireType : ES3ObjectType
    {
        readonly ES3Type originalBase;
        public FriendSaveSlotWireType() : base(typeof(FriendSaveSlotData))
        {
            priority = 1;
            originalBase = ES3TypeMgr.GetES3Type(typeof(SaveSlotData)) ?? ES3TypeMgr.GetOrCreateES3Type(typeof(SaveSlotData));
            GetMembers(true); // Cache all declared + inherited public members before choosing the wire type.
            type = typeof(SaveSlotData);
            RestoreOriginalLookup();
        }
        public static void EnsureRegistered()
        {
            if (!(ES3TypeMgr.GetES3Type(typeof(FriendSaveSlotData)) is FriendSaveSlotWireType)) new FriendSaveSlotWireType();
            ES3TypeMgr.GetES3Type(typeof(string));
            ES3TypeMgr.GetES3Type(typeof(SaveSlotData));
        }
        void RestoreOriginalLookup()
        {
            // ES3TypeMgr has a single last-access cache keyed by handler.type.
            // Querying the unchanged original dictionary entry prevents this
            // derived wire alias from being reused for an unrelated base call.
            ES3TypeMgr.GetES3Type(typeof(string));
            ES3TypeMgr.GetES3Type(typeof(SaveSlotData));
        }
        public override void Write(object obj, ES3Writer writer)
        {
            try
            {
                if (!(obj is FriendSaveSlotData)) { originalBase.Write(obj, writer); return; }
                writer.WriteType(typeof(FriendSaveSlotData));
                WriteProperties(obj, writer);
            }
            finally { RestoreOriginalLookup(); }
        }
        protected override void WriteObject(object obj, ES3Writer writer) => Write(obj, writer);
        protected override object ReadObject<T>(ES3Reader reader)
        {
            var result = new FriendSaveSlotData { FriendsLifeModuleVersion = 0 };
            bool ownFields = false;
            try
            {
                // Preserve even a malformed explicitly declared module for the
                // runtime version validator; do not silently turn it into an
                // empty legacy module. Unknown future properties are skipped
                // using the same public ES3 reader contract as reflected types.
                foreach (string property in reader.Properties)
                {
                    switch (property)
                    {
                        case "SlotID": result.SlotID = reader.Read<int>(); break;
                        case "HostSteamID": result.HostSteamID = reader.Read<string>(); break;
                        case "Version": result.Version = reader.Read<int>(); break;
                        case "BossGlitchResetApplied": result.BossGlitchResetApplied = reader.Read<bool>(); break;
                        case "TemporaryStaffStackRevision": result.TemporaryStaffStackRevision = reader.Read<int>(); break;
                        case "PlayerData": result.PlayerData = reader.Read<PlayerData>(); break;
                        case "RestaurantData": result.RestaurantData = reader.Read<RestaurantData>(); break;
                        case "GameProgress": result.GameProgress = reader.Read<GameProgressData>(); break;
                        case "DailyRecord": result.DailyRecord = reader.Read<DailyRecordData>(); break;
                        case "TabletData": result.TabletData = reader.Read<TabletData>(); break;
                        case "FriendsLifeModuleVersion": ownFields = true; result.FriendsLifeModuleVersion = reader.Read<int>(); break;
                        case "FriendsLifeJson": ownFields = true; result.FriendsLifeJson = reader.Read<string>(); break;
                        case "FriendsLifeRevision": ownFields = true; result.FriendsLifeRevision = reader.Read<long>(); break;
                        default: reader.Skip(); break;
                    }
                }
                return ownFields ? result : ToBase(result);
            }
            finally { RestoreOriginalLookup(); }
        }
        protected override void ReadObject<T>(ES3Reader reader, object obj)
        {
            var loaded = (SaveSlotData)ReadObject<T>(reader);
            if (loaded is FriendSaveSlotData && !(obj is FriendSaveSlotData))
                throw new InvalidOperationException("Cannot load a FriendsLife payload into a plain base slot");
            CopyBase(loaded, (SaveSlotData)obj);
            if (loaded is FriendSaveSlotData own)
            {
                var target = (FriendSaveSlotData)obj;
                target.FriendsLifeModuleVersion = own.FriendsLifeModuleVersion;
                target.FriendsLifeJson = own.FriendsLifeJson;
                target.FriendsLifeRevision = own.FriendsLifeRevision;
            }
        }
        static SaveSlotData ToBase(SaveSlotData source) { var target = new SaveSlotData(); CopyBase(source, target); return target; }
        static void CopyBase(SaveSlotData source, SaveSlotData target)
        {
            target.SlotID = source.SlotID; target.HostSteamID = source.HostSteamID; target.Version = source.Version;
            target.BossGlitchResetApplied = source.BossGlitchResetApplied; target.TemporaryStaffStackRevision = source.TemporaryStaffStackRevision;
            target.PlayerData = source.PlayerData; target.RestaurantData = source.RestaurantData; target.GameProgress = source.GameProgress;
            target.DailyRecord = source.DailyRecord; target.TabletData = source.TabletData;
        }
    }
}
