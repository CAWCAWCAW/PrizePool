﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Data;
using System.IO;
using System.Text;
using System.Net;
using System.Reflection;

// Terraria related API References
using Newtonsoft.Json;
using TerrariaApi.Server;
using Terraria;
using TShockAPI;
using Wolfje.Plugins.SEconomy;

namespace PrizePool
{
    [ApiVersion(1, 15)]
    public class PrizePool : TerrariaPlugin
    {
        private PrizePoolConfig configObj { get; set; }
        private String SavePath = TShock.SavePath;
        internal static string filepath { get { return Path.Combine(TShock.SavePath, "prizepool.json"); } }
        private Random r = new Random();

        public override string Name
        {
            get { return "PrizePool"; }
        }
        public override string Author
        {
            get { return "IcyPhoenix"; }
        }
        public override string Description
        {
            get { return "Gives admins the ability to give out currency from a pool"; }
        }
        public override Version Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                writeConfig();
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
            }
            base.Dispose(disposing);
        }
        public PrizePool(Main game)
            : base(game)
        {
        }

        public void OnInitialize(EventArgs args)
        {
            Commands.ChatCommands.Add(new Command("prizepool.default", award, "award"));
            configObj = new PrizePoolConfig();
            loadConfig();
        }

        private void award(CommandArgs args)
        {
            if (args.Parameters.Count() != 2 && args.Parameters.Count() != 1)
            {
                args.Player.SendInfoMessage("Info: /award <user> <amount>");
                return;
            }
            //check if user exists
            List<TSPlayer> awardedUsers = TShock.Utils.FindPlayer(args.Parameters[0]);
            //check if more then one match
            if (awardedUsers.Count() > 1)
            {
                args.Player.SendErrorMessage("Error: More then one user matched, please use exact name!");
                return;
            }
            //check if no matches
            if (awardedUsers.Count() == 0)
            {
                args.Player.SendErrorMessage("Error: No users matched, please check your spelling and try again");
                return;
            }
            TSPlayer awardedUser = awardedUsers[0];
            //check if user is logged in
            if (!awardedUser.IsLoggedIn)
            {
                args.Player.SendErrorMessage("Error: Current user is not logged in!");
                return;
            }
            int awardAmount;
            if (!int.TryParse(args.Parameters[1], out awardAmount))
            {
                args.Player.SendErrorMessage("Error: Non-Numerical Amount Detected!");
                return;
            }
            if (awardAmount <= 0)
            {
                args.Player.SendErrorMessage("Error: Amount awarded must be greater then 0!");
                return;
            }
            foreach (PoolUsers obj in configObj.PrizePoolUsers)
            {
                if (obj.user == args.Player.Name)
                {
                    transfer(obj, awardAmount, awardedUser, args.Player);
                    return;
                }
            }
            //else not found - add user then do action
            configObj.PrizePoolUsers.Add(new PoolUsers(args.Player.Name, configObj.defaultAmount, DateTime.Now));
            transfer(configObj.PrizePoolUsers.Last(), awardAmount, awardedUser, args.Player);
            return;
        }

        private void transfer(PoolUsers obj, int awardAmount, TSPlayer awardedUser, TSPlayer player)
        {
            //if found check if it has been longer then a day
            if ((DateTime.Now - obj.time).TotalDays >= configObj.daysrefresh)
            {
                obj.pool = configObj.defaultAmount;
                obj.time = DateTime.Now;
            }
            //check if player has enough to award other player
            if (awardAmount > obj.pool)
            {
                player.SendErrorMessage("Error: Cannot award player, you have exceeded your daily limit");
                player.SendErrorMessage("Error: Current Balance left is {0}", ((Money)obj.pool).ToLongString());
                return;
            }
            obj.pool -= awardAmount;
            var eaccount = SEconomyPlugin.GetEconomyPlayerSafe(awardedUser.Index);
            SEconomyPlugin.WorldAccount.TransferToAsync(eaccount.BankAccount, awardAmount, Wolfje.Plugins.SEconomy.Journal.BankAccountTransferOptions.AnnounceToReceiver, string.Format("Award, done by {0}", player.Name), string.Format("Awarded by {0}", player.Name));
            TSPlayer.All.SendInfoMessage("Player {0} has been Awarded {1} by {2}!", awardedUser, ((Money)awardAmount).ToLongString(), obj.user);
            return;
        }

        private void loadConfig()
        {
            try
            {
                if (File.Exists(filepath))
                {
                    configObj = new PrizePoolConfig();
                    configObj = PrizePoolConfig.Read(filepath);
                    return;
                }
                else
                {
                    configObj.Write(filepath);
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.ConsoleError(ex.Message);
                return;
            }
        }
        private void writeConfig()
        {
            try
            {
                configObj.Write(filepath);
                return;
            }
            catch (Exception ex)
            {
                Log.ConsoleError(ex.Message);
                return;
            }
        }
    }
}
