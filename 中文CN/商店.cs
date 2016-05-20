using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TShockAPI;
using Terraria;
using Newtonsoft.Json;
using TerrariaApi.Server;
using Wolfje.Plugins.SEconomy;
namespace 商店
{
	[ApiVersion(1, 22)]
	public class 商店插件 : TerrariaPlugin
	{
		private Config config;
		public override Version Version
		{
			get { return new Version("1.3.0.0"); }
		}
		public override string Name
		{
			get { return "商店插件"; }
		}
		public override string Author
		{
			get { return "Touhou汉化组 紧闭的 恋之瞳"; }
		}
		public override string Description
		{
			get { return "商店插件"; }
		}
		public 商店插件(Main game) : base(game)
		{
			Order = 1;
		}
		public override void Initialize()
		{
			TShockAPI.Commands.ChatCommands.Add(new Command(买, "买", "buy"));
			TShockAPI.Commands.ChatCommands.Add(new Command("shop.reload", Reload_Config, "重载商店插件"));
			ReadConfig();
		}
		private static string 物品转标签(物品 参数)
		{
			string 返回值 = ((参数.前缀 != 0) ? "[i/p" +参数.前缀 : "[i");
			返回值 = (参数.数量 != 1) ? 返回值 + "/s" + 参数.数量 : 返回值;
			返回值 = 返回值 + ":" + 参数.编号 + "]";
			if (参数.编号 == 0) return "";
			return 返回值;
		}
		public static bool 参数找页码(List<string> commandParameters, int expectedParameterIndex, TSPlayer errorMessageReceiver, out int pageNumber)
		{
			pageNumber = 1;
			if (commandParameters.Count <= expectedParameterIndex)
				return true;
			string pageNumberRaw = commandParameters[expectedParameterIndex];
			if (!int.TryParse(pageNumberRaw, out pageNumber) || pageNumber < 1)
			{
				if (errorMessageReceiver != null)
					errorMessageReceiver.SendErrorMessage("\"{0}\" 不是一个有效的页码.", pageNumberRaw);
				return false;
			}
			return true;
		}
		private void 买(CommandArgs args)
		{
			var rn = Main.rand.Next(1, 1000);
			if (rn == 520)
			{
				args.Player.SendSuccessMessage("恋恋喜欢彩铅哦呐~");
			}
			if ((args.Parameters.Count < 1) || ((args.Parameters.Count > 1)  && (args.Parameters[0] != "菜单")))
			{
				args.Player.SendErrorMessage("[商店插件]用法: /买 商品名 或 /买 菜单");
				return;
			}
			if (args.Parameters[0] == "菜单")
			{
				int pageNumber = 1;
				if (!参数找页码(args.Parameters, 1, args.Player, out pageNumber))
					return;
				var lines = new List<string>{};
				foreach (var 商品1 in config.All)
				{
					string total = "商品名:" + 商品1.品名 + " 价格:" + 商品1.价格 + " 内容:";
					foreach (var item in 商品1.包含物品)
					{
						total = total + 物品转标签(item);
					}
					if (args.Player.Group.HasPermission(商品1.权限))
					{
						lines.Add(total);
					}
					else if (!config.隐藏不可购买的物品)
					{
						string perm = args.Player.Group.HasPermission(商品1.权限) ? "[可购买]" : "[无权限]";
						lines.Add(perm + total);
					}
				}
				PaginationTools.SendPage(args.Player, pageNumber, lines,
				                         new PaginationTools.Settings
				                         {
				                         	HeaderFormat = "[商店插件]查看菜单({0}/{1}):",
				                         	FooterFormat = "输入 {0}买 菜单 {{0}} 查看其他。".SFormat(Commands.Specifier)
				                         }
				                        );
				return;
			}
			var 购入物 = new 商品();
			bool 匹配 = false;
			foreach (var 商品 in config.All)
			{
				if (args.Parameters[0] == 商品.品名)
				{
					购入物 = 商品;
					匹配 = true;
				}
			}
			if (!匹配 || (!args.Player.Group.HasPermission(购入物.权限) && config.隐藏不可购买的物品))
			{
				args.Player.SendErrorMessage("[商店插件]没有该商品. 输入/买 菜单 查看所有可购买物品.");
				return;
			}
			if (!args.Player.Group.HasPermission(购入物.权限))
			{
				args.Player.SendErrorMessage("[商店插件]没有购买该物品的权限.");
				return;
			}
			var UsernameBankAccount = SEconomyPlugin.Instance.GetBankAccount(args.Player.Name);
			var playeramount = UsernameBankAccount.Balance;
			Money amount = -购入物.价格;
			Money amount2 = 购入物.价格;
			var Journalpayment = Wolfje.Plugins.SEconomy.Journal.BankAccountTransferOptions.AnnounceToSender;
			if (args.Player == null || UsernameBankAccount == null)
			{
				args.Player.SendErrorMessage("服务器未能找到{0}的经验信息, 请注册登陆后重试.", args.Player.Name);
				return;
			}
			if (playeramount < amount2)
			{
				args.Player.SendErrorMessage("商品" + 购入物.品名 + "价格为" + 购入物.价格	 + ", 你只有" + UsernameBankAccount.Balance + ".");
				return;
			}
			if (!args.Player.InventorySlotAvailable)
			{
				args.Player.SendErrorMessage("你的背包已满.");
				return;
			}
			SEconomyPlugin.Instance.WorldAccount.TransferToAsync(UsernameBankAccount, amount,
			                                                     Journalpayment, string.Format("花费{0}用于购买", amount2),
			                                                     string.Format("购买"));
			args.Player.SendSuccessMessage("你花了{0}购买了{1}.", amount2, 购入物.品名);
			TShock.Log.ConsoleInfo("[商店插件]{0}花了{2}买了{1}.", args.Player.Name, 购入物.品名, amount2);
			foreach (var item in 购入物.包含物品)
			{
				var q = new Item();
				q.netDefaults(item.编号);
				q.stack = item.数量;
				q.Prefix(item.前缀);
				args.Player.GiveItemCheck(q.type, q.name, q.width, q.height, q.stack, q.prefix);
			}
		}
		private void CreateConfig()
		{
			string filepath = Path.Combine(TShock.SavePath, "商店插件.json");
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
			string filepath = Path.Combine(TShock.SavePath, "商店插件.json");
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
							foreach (var element in config.All) 
							{
								foreach (var element2 in element.包含物品) 
								{
									element2.补全();
								}
							}
						}
						stream.Close();
					}
					return true;
				}
				else
				{
					TShock.Log.ConsoleError("已新建商店插件配置文件.");
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
		private void Reload_Config(CommandArgs args)
		{
			if (ReadConfig())
			{
				args.Player.SendInfoMessage("商店插件加载成功.");
				return;
			}
			args.Player.SendErrorMessage("商店插件加载错误, 请检查日志.");
		}
	}
	public class Config
	{
		public List<商品> All;
		public bool 隐藏不可购买的物品;
		public Config()
		{}
		public Config(int a)
		{
			隐藏不可购买的物品 = true;
			All = new List<商品>{new 商品(1), new 商品(2)};
		}
	}
	public class 商品
	{
		public string 品名 = "";
		public string 权限 = "shop.buy";
		public int 价格 = 0;
		public List<物品> 包含物品 = new List<物品>{};
		public 商品() {}
		public 商品(int a)
		{
			if (a == 1)
			{
				var i1 = new 物品(2760);
				var i2 = new 物品(2761);
				var i3 = new 物品(2762);
				品名 = "示例星云套";
				价格 = 500000;
				包含物品 = new List<物品>{i1, i2, i3};
			}
			if (a == 2)
			{
				品名 = "示例2";
				价格 = 20;
				for (int i = 0; i < 10; i++)
				{
					包含物品.Add(new 物品(i + 2702));
				}
			}
		}
	}
	public class 物品
	{
		public int 编号 = 0;
		public int 数量 = 1;
		public int 前缀 = 0;
		public string 名称 = "";
		public 物品() {}
		public 物品(int a)
		{
			this.名称 = TShockAPI.Utils.Instance.GetItemByIdOrName(a.ToString())[0].name;
			this.编号 = TShockAPI.Utils.Instance.GetItemByIdOrName(a.ToString())[0].type;
		}
		public void 补全()
		{
			this.编号 = TShockAPI.Utils.Instance.GetItemByIdOrName((编号 != 0) ? 编号.ToString() : 名称)[0].type;
			this.名称 = TShockAPI.Utils.Instance.GetItemByIdOrName(编号.ToString())[0].name;
		}
	}
}