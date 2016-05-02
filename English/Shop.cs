﻿using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TShockAPI;
using Terraria;
using Newtonsoft.Json;
using TerrariaApi.Server;
using Wolfje.Plugins.SEconomy;
namespace Shop
{
	[ApiVersion(1, 22)]
	public class Shop : TerrariaPlugin
	{
		private Config config;
		public override Version Version
		{
			get { return new Version("1.2.0.0"); }
		}
		public override string Name
		{
			get { return "Shop"; }
		}
		public override string Author
		{
			get { return "RYH"; }
		}
		public override string Description
		{
			get { return "Shop plugin allow players buy item(s). Require SEconomy."; }
		}
		public Shop(Main game) : base(game)
		{
			Order = 1;
		}
		public override void Initialize()
		{
			TShockAPI.Commands.ChatCommands.Add(new Command(Buy, "buy"));
			TShockAPI.Commands.ChatCommands.Add(new Command("shop.reload", ReloadConfig, "reloadshop"));
			ReadConfig();
		}
		private static string ItemToTag(SimpleItem args)
		{
			string ret = ((args.prefix != 0) ? "[i/p" +args.prefix : "[i");
			ret = (args.stack != 1) ? ret + "/s" + args.stack : ret;
			ret = ret + ":" + args.netID + "]";
			if (args.netID == 0) return "";
			return ret;
		}
		private void Buy(CommandArgs args)
		{
			if ((args.Parameters.Count < 1) || ((args.Parameters.Count > 1)  && (args.Parameters[0] != "menu")))
			{
				args.Player.SendErrorMessage("[Shop]Usage: /buy name or /buy menu");
				return;
			}
			if (args.Parameters[0] == "menu")
			{
				int pageNumber = 1;
				if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
					return;
				var lines = new List<string>{};
				foreach (var gooda in config.All)
				{
					string perm = args.Player.Group.HasPermission(gooda.RequirePermission) ? "[Available]" : "[Shortage]";
					string total = "Name:" + gooda.DisplayName + " Price:" + gooda.Price + " Include:";
					foreach (var item in gooda.IncludeItems)
					{
						total = total + ItemToTag(item);
					}
					lines.Add(total);
				}
				PaginationTools.SendPage(args.Player, pageNumber, lines,
				                         new PaginationTools.Settings
				                         {
				                         	HeaderFormat = "[Shop]Menu({0}/{1}):",
				                         	FooterFormat = "Type {0}buy menu {{0}} for more goods.".SFormat(Commands.Specifier)
				                         }
				                        );
				return;
			}
			var Find = new Goods();
			bool FindSuccess = false;
			foreach (var i1 in config.All)
			{
				if (args.Parameters[0] == i1.DisplayName)
				{
					Find = i1;
					FindSuccess = true;
				}
			}
			if (!FindSuccess)
			{
				args.Player.SendErrorMessage("[Shop]Can't find a good with given name. Type {0}buy menu for list.");
				return;
			}
			if (!args.Player.Group.HasPermission(Find.RequirePermission))
			{
				args.Player.SendErrorMessage("[Shop]There is a shortage! Why not try another goods?");
				return;
			}
			var UsernameBankAccount = SEconomyPlugin.Instance.GetBankAccount(args.Player.Name);
			var playeramount = UsernameBankAccount.Balance;
			Money amount = -Find.Price;
			Money amount2 = Find.Price;
			var Journalpayment = Wolfje.Plugins.SEconomy.Journal.BankAccountTransferOptions.AnnounceToSender;
			if (args.Player == null || UsernameBankAccount == null)
			{
				args.Player.SendErrorMessage("Can't find the account for {0}.", args.Player.Name);
				return;
			}
			if (playeramount < amount2)
			{
				args.Player.SendErrorMessage("The price of " + Find.DisplayName + " is " + Find.Price	 + ", but you have " + UsernameBankAccount.Balance + " only.");
				return;
			}
			if (!args.Player.InventorySlotAvailable)
			{
				args.Player.SendErrorMessage("Your inventory is full.");
				return;
			}
			SEconomyPlugin.Instance.WorldAccount.TransferToAsync(UsernameBankAccount, amount,
			                                                     Journalpayment, string.Format("Pay {0} to shop", amount2),
			                                                     string.Format("Buying"));
			args.Player.SendSuccessMessage("You have paid {0} for buying {1}.", amount2, Find.DisplayName);
			TShock.Log.ConsoleInfo("[Shop]{0} has paid {2} for buying {1}.", args.Player.Name, Find.DisplayName, amount2);
			foreach (var item in Find.IncludeItems)
			{
				var q = new Item();
				q.netDefaults(item.netID);
				q.stack = item.stack;
				q.Prefix(item.prefix);
				args.Player.GiveItemCheck(q.type, q.name, q.width, q.height, q.stack, q.prefix);
			}
		}
		private void CreateConfig()
		{
			string filepath = Path.Combine(TShock.SavePath, "Shop.json");
			try
			{
				using (var stream = new FileStream(filepath, FileMode.Create, FileAccess.Write, FileShare.Write))
				{
					using (var sr = new StreamWriter(stream))
					{
						config = new Config(1);
						var configString = JsonConvert.SerializeObject(config, Formatting.Indented);
						sr.Write(configString);
					}
					stream.Close();
				}
			}
			catch (Exception ex)
			{
				TShock.Log.ConsoleError(ex.Message);
			}
		}
		private bool ReadConfig()
		{
			string filepath = Path.Combine(TShock.SavePath, "Shop.json");
			try
			{
				if (File.Exists(filepath))
				{
					using (var stream = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read))
					{
						using (var sr = new StreamReader(stream))
						{
							var configString = sr.ReadToEnd();
							config = JsonConvert.DeserializeObject<Config>(configString);
						}
						stream.Close();
					}
					return true;
				}
				else
				{
					TShock.Log.ConsoleError("Create new config file for Shop.");
					CreateConfig();
					return false;
				}
			}
			catch (Exception ex)
			{
				TShock.Log.ConsoleError(ex.Message);
			}
			return false;
		}
		private void ReloadConfig(CommandArgs args)
		{
			if (ReadConfig())
			{
				args.Player.SendInfoMessage("[Shop]Load success.");
				return;
			}
			args.Player.SendErrorMessage("[Shop]Load fails. Check log for more details.");
		}
	}
	public class Config
	{
		public List<Goods> All;
		public Config()
		{}
		public Config(int a)
		{
			All = new List<Goods>{new Goods(1), new Goods(2)};
		}
	}
	public class Goods
	{
		public string DisplayName = "";
		public string RequirePermission = "";
		public int Price = 0;
		public List<SimpleItem> IncludeItems = new List<SimpleItem>{};
		public Goods() {}
		public Goods(int a)
		{
			if (a == 1)
			{
				var i1 = new SimpleItem(2760);
				var i2 = new SimpleItem(2761);
				var i3 = new SimpleItem(2762);
				DisplayName = "ExampleNebula";
				RequirePermission = "shop.buy";
				Price = 500000;
				IncludeItems = new List<SimpleItem>{i1, i2, i3};
			}
			if (a == 2)
			{
				DisplayName = "Example2";
				Price = 20;
				for (int i = 0; i < 10; i++)
				{
					IncludeItems.Add(new SimpleItem(i + 2702));
				}
			}
		}
	}
	public class SimpleItem
	{
		public int netID = 0;
		public int stack = 1;
		public int prefix = 0;
		public SimpleItem() {}
		public SimpleItem(int a)
		{
			this.netID = a;
		}
	}
}