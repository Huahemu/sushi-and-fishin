using System;
using System.Collections.Generic;
using System.Linq;
namespace SushiAndFish.FriendsLife
{
    [Serializable]public sealed class FriendCasinoTicketWallet
    {public string personId;public long tickets;}
    [Serializable]public sealed class FriendCasinoTicketReceipt
    {public string paymentId,personId;public long delta,balance;}
    public sealed partial class FriendsEconomySave
    {
        public List<FriendCasinoTicketWallet> casinoTicketWallets=new List<FriendCasinoTicketWallet>();
        public List<FriendCasinoTicketReceipt> casinoTicketReceipts=new List<FriendCasinoTicketReceipt>();
    }
    public sealed partial class FriendsEconomyService
    {
        public bool CommitNativeCasinoTickets(string personId,long delta,string paymentId,out long result)
        {
            result=0;
            if(!Authority||runtime.GetPerson(personId)?.controllerKind!=PersonControllerKind.Npc||string.IsNullOrEmpty(paymentId))return false;
            State.casinoTicketWallets=State.casinoTicketWallets??new List<FriendCasinoTicketWallet>();
            State.casinoTicketReceipts=State.casinoTicketReceipts??new List<FriendCasinoTicketReceipt>();
            var wallet=State.casinoTicketWallets.FirstOrDefault(x=>x.personId==personId);
            var old=State.casinoTicketReceipts.FirstOrDefault(x=>x.paymentId==paymentId);
            if(old!=null){result=wallet?.tickets??0;return old.personId==personId&&old.delta==delta;}
            long value;try{value=checked((wallet?.tickets??0)+delta);}catch(OverflowException){return false;}
            if(value<0)return false;
            if(wallet==null){wallet=new FriendCasinoTicketWallet{personId=personId};State.casinoTicketWallets.Add(wallet);}
            wallet.tickets=value;State.casinoTicketReceipts.Add(new FriendCasinoTicketReceipt{personId=personId,paymentId=paymentId,delta=delta,balance=value});
            result=value;Commit();return true;
        }
    }
}
