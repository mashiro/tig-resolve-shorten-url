using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Web;
using Misuzilla.Applications.TwitterIrcGateway;
using Misuzilla.Applications.TwitterIrcGateway.AddIns;
using Misuzilla.Applications.TwitterIrcGateway.AddIns.Console;

namespace Spica.Applications.TwitterIrcGateway.AddIns.ResolveShortenUrl
{
	public class ResolveShortenUrlPattern : IConfiguration
	{
		public Boolean Enabled { get; set; }
		public String Pattern { get; set; }

		public ResolveShortenUrlPattern()
		{
			Enabled = true;
			Pattern = String.Empty;
		}

		public override string ToString()
		{
			return ToShortString();
		}

		public string ToShortString()
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendFormat("{0}", Pattern);
			return sb.ToString();
		}

		public string ToLongString()
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendFormat("[{0}]", Enabled ? "*" : " ");
			sb.AppendFormat(" {0}", Pattern);
			return sb.ToString();
		}
	}

	public class ResolveShortenUrlConfiguration : IConfiguration
	{
		[Browsable(false)]
		public List<ResolveShortenUrlPattern> Items { get; set; }
		public Boolean EnableResolveShortenUrl { get; set; }
		public Int32 TimeOut { get; set; }

		public ResolveShortenUrlConfiguration()
		{
			Items = new List<ResolveShortenUrlPattern>();
			EnableResolveShortenUrl = true;
			TimeOut = 1000;
		}
	}

	[Description("短縮されたURLを展開する設定を行うコンテキストに切り替えます")]
	public class ResolveShortenUrlContext : Context
	{
		private ResolveShortenUrlAddIn AddIn { get { return CurrentSession.AddInManager.GetAddIn<ResolveShortenUrlAddIn>(); } }

		public override IConfiguration[] Configurations { get { return new IConfiguration[] { AddIn.Config }; } }
		protected override void OnConfigurationChanged(IConfiguration config, System.Reflection.MemberInfo memberInfo, object value)
		{
			if (config is ResolveShortenUrlConfiguration)
			{
				AddIn.Config = config as ResolveShortenUrlConfiguration;
				CurrentSession.AddInManager.SaveConfig(AddIn.Config);
			}
		}

		[Description("存在するパターンをすべて表示します")]
		public void List()
		{
			if (AddIn.Config.Items.Count == 0)
			{
				Console.NotifyMessage("パターンは現在設定されていません。");
				return;
			}

			for (Int32 i = 0; i < AddIn.Config.Items.Count; ++i)
			{
				ResolveShortenUrlPattern item = AddIn.Config.Items[i];
				Console.NotifyMessage(String.Format("{0}: {1}", i, item.ToLongString()));
			}
		}

		[Description("指定したパターンを有効化します")]
		public void Enable(String arg)
		{
			SwitchEnable(arg, true);
		}

		[Description("指定したパターンを無効化します")]
		public void Disable(String arg)
		{
			SwitchEnable(arg, false);
		}

		[Description("指定したパターンを削除します")]
		public void Remove(String arg)
		{
			FindAt(arg, item =>
			{
				AddIn.Config.Items.Remove(item);
				CurrentSession.AddInManager.SaveConfig(AddIn.Config);
				Console.NotifyMessage(String.Format("パターン {0} を削除しました。", item.ToShortString()));
			});
		}

		[Description("指定したパターンを編集します")]
		public void Edit(String arg)
		{
			FindAt(arg, item =>
			{
				Type type = typeof(EditResolveShortenUrlContext);
				EditResolveShortenUrlContext ctx = Console.GetContext(type, CurrentServer, CurrentSession) as EditResolveShortenUrlContext;
				ctx.SetDefaultPattern(item);
				Console.PushContext(ctx);
			});
		}

		[Description("パターンを新規追加します")]
		public void New()
		{
			Type type = typeof(EditResolveShortenUrlContext);
			EditResolveShortenUrlContext ctx = Console.GetContext(type, CurrentServer, CurrentSession) as EditResolveShortenUrlContext;
			Console.PushContext(ctx);
		}

		private void SwitchEnable(String arg, Boolean enable)
		{
			FindAt(arg, item =>
			{
				item.Enabled = enable;
				CurrentSession.AddInManager.SaveConfig(AddIn.Config);
				Console.NotifyMessage(String.Format("パターン {0} を{1}化しました。", item.ToShortString(), (enable ? "有効" : "無効")));
			});
		}

		private void FindAt(String arg, Action<ResolveShortenUrlPattern> action)
		{
			Int32 index;
			if (Int32.TryParse(arg, out index))
			{
				if (index < AddIn.Config.Items.Count && index > -1)
				{
					action(AddIn.Config.Items[index]);
				}
				else
				{
					Console.NotifyMessage("存在しないパターンが指定されました。");
				}
			}
			else
			{
				Console.NotifyMessage("パターンの指定が正しくありません。");
			}
		}
	}

	public class EditResolveShortenUrlContext : Context
	{
		private ResolveShortenUrlAddIn AddIn { get { return CurrentSession.AddInManager.GetAddIn<ResolveShortenUrlAddIn>(); } }

		private Boolean IsNewRecord { get; set; }
		private ResolveShortenUrlPattern Pattern { get; set; }

		public override IConfiguration[] Configurations { get { return new IConfiguration[] { Pattern }; } }
		public override string ContextName { get { return (IsNewRecord ? "New" : "Edit") + typeof(ResolveShortenUrlPattern).Name; } }

		public EditResolveShortenUrlContext()
		{
			IsNewRecord = true;
			Pattern = new ResolveShortenUrlPattern();
		}

		internal void SetDefaultPattern(ResolveShortenUrlPattern pattern)
		{
			IsNewRecord = false;
			Pattern = pattern;
		}

		[Description("パターンを保存してコンテキストを終了します")]
		public void Save()
		{
			if (IsNewRecord) AddIn.Config.Items.Add(Pattern);
			CurrentSession.AddInManager.SaveConfig(AddIn.Config);
			Console.NotifyMessage(String.Format("パターンを{0}しました。", (IsNewRecord ? "新規作成" : "保存")));
			Exit();
		}
	}

	public class ResolveShortenUrlAddIn : AddInBase
	{
		internal ResolveShortenUrlConfiguration Config { get; set; }

		public override void Initialize()
		{
			Config = CurrentSession.AddInManager.GetConfig<ResolveShortenUrlConfiguration>();
			CurrentSession.PostFilterProcessTimelineStatus += new EventHandler<TimelineStatusEventArgs>(Session_PostFilterProcessTimelineStatus);
			CurrentSession.AddInsLoadCompleted += (sender, e) => CurrentSession.AddInManager.GetAddIn<ConsoleAddIn>().RegisterContext<ResolveShortenUrlContext>();
		}

		private void Session_PostFilterProcessTimelineStatus(object sender, TimelineStatusEventArgs e)
		{
			e.Text = (Config.EnableResolveShortenUrl) ? Resolve(e.Text) : e.Text;
		}

		private String Resolve(String text)
		{
			foreach (ResolveShortenUrlPattern item in Config.Items)
			{
				if (item.Enabled)
				{
					RegexOptions opt = RegexOptions.IgnoreCase | RegexOptions.Compiled;
					text = Regex.Replace(text, item.Pattern, (Match m) => Resolve(m.Value, Config.TimeOut), opt);
				}
			}

			return text;
		}

		private String Resolve(String url, Int32 timeout)
		{
			HttpWebResponse res = null;
			try
			{
				HttpWebRequest req = HttpWebRequest.Create(url) as HttpWebRequest;
				req.AllowAutoRedirect = false;
				req.Timeout = timeout;
				req.Method = "HEAD";
				res = req.GetResponse() as HttpWebResponse;

				if (res.StatusCode == HttpStatusCode.MovedPermanently)
				{
					if (!String.IsNullOrEmpty(res.Headers["Location"]))
					{
						return res.Headers["Location"];
					}
				}
				return url;
			}
			catch (WebException)
			{
				return url;
			}
			finally
			{
				if (res != null)
				{
					res.Close();
				}
			}
		}
	}
}
