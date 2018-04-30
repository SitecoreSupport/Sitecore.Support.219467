using System;
using Sitecore;
using Sitecore.Configuration;
using Sitecore.Data.Events;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Exceptions;
using Sitecore.Globalization;
using Sitecore.Web.UI.Sheer;
using System.Collections.Specialized;
using System.Threading;

namespace Sitecore.Shell.Framework.Commands
{


	[Serializable]
	public class AddMaster : Command
	{
		protected event ItemCreatedDelegate ItemCreated;

		protected void Add(ClientPipelineArgs args)
		{
			if (SheerResponse.CheckModified())
			{
				Item item = Context.ContentDatabase.GetItem(args.Parameters["Master"]);
				if (item == null)
				{
					SheerResponse.Alert(Translate.Text("Branch \"{0}\" not found.", new object[] { args.Parameters["Master"] }), new string[0]);
				}
				else if (item.TemplateID == TemplateIDs.CommandMaster)
				{
					string str = item["Command"];
					if (!string.IsNullOrEmpty(str))
					{
						Context.ClientPage.SendMessage(this, str);
					}
				}
				else if (args.IsPostBack)
				{
					if (args.HasResult)
					{
						string str2 = StringUtil.GetString(new string[] { args.Parameters["ItemID"] });
						string name = StringUtil.GetString(new string[] { args.Parameters["Language"] });
						Item parent = Context.ContentDatabase.Items[str2, Language.Parse(name)];
						if (parent == null)
						{
							SheerResponse.Alert("Parent item not found.", new string[0]);
						}
						else if (!parent.Access.CanCreate())
						{
							Context.ClientPage.ClientResponse.Alert("You do not have permission to create items here.");
						}
						else
						{
							Item item3 = null;
							try
							{
								if (item.TemplateID == TemplateIDs.BranchTemplate)
								{
									BranchItem branch = item;
									item3 = Context.Workflow.AddItem(args.Result, branch, parent);
									Log.Audit(this, "Add from branch: {0}", new string[] { AuditFormatter.FormatItem((Item)branch) });
								}
								else
								{
									TemplateItem template = item;
									item3 = Context.Workflow.AddItem(args.Result, template, parent);
									Log.Audit(this, "Add from template: {0}", new string[] { AuditFormatter.FormatItem((Item)template) });
								}
							}
							catch (WorkflowException exception)
							{
								Log.Error("Workflow error: could not add item from master", exception, this);
								SheerResponse.Alert(exception.Message, new string[0]);
							}
							if ((item3 != null) && (this.ItemCreated != null))
							{
								this.ItemCreated(this, new ItemCreatedEventArgs(item3));
							}
						}
					}
				}
				else
				{
					SheerResponse.Input("Enter a name for the new item:", item.DisplayName, Settings.ItemNameValidation, "'$Input' is not a valid name.", Settings.MaxItemNameLength);
					args.WaitForPostBack();
				}
			}
		}

		public override void Execute(CommandContext context)
		{
			if ((context.Items.Length == 1) && context.Items[0].Access.CanCreate())
			{
				Item item = context.Items[0];
				NameValueCollection parameters = new NameValueCollection
				{
					["Master"] = context.Parameters["master"],
					["ItemID"] = item.ID.ToString(),
					["Language"] = item.Language.ToString(),
					["Version"] = item.Version.ToString()
				};
				Context.ClientPage.Start(this, "Add", parameters);
			}
		}

		public override CommandState QueryState(CommandContext context)
		{
			Error.AssertObject(context, "context");
			if (context.Items.Length != 1)
			{
				return CommandState.Hidden;
			}
			if (!context.Items[0].Access.CanCreate())
			{
				return CommandState.Disabled;
			}
			return base.QueryState(context);
		}
	}
}
