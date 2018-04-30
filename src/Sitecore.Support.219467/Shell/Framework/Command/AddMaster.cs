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
using Sitecore.Common;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Managers;
using Sitecore.SecurityModel;
using Sitecore.Shell.Framework.Commands;
using Sitecore.Sites;
using Sitecore.Workflows;

namespace Sitecore.Support.Shell.Framework.Commands
{


	[Serializable]
	public class AddMaster : Sitecore.Buckets.Pipelines.UI.AddMaster
	{

	  private class WorkflowSubstitute
	  {

	    public bool Enabled
	    {
	      get
	      {
	        switch (Switcher<WorkflowContextState, WorkflowContextState>.CurrentValue)
	        {
	          case WorkflowContextState.Default:
	          {
	            SiteContext site = Context.Data.Site;
	            if (site != null)
	            {
	              return site.EnableWorkflow;
	            }
	            return false;
	          }
	          case WorkflowContextState.Disabled:
	            return false;
	          default:
	            return true;
	        }
	      }
	    }

			/// <summary>
			/// Adds the item.
			/// </summary>
			/// <param name="name">The name.</param>
			/// <param name="branch">The master.</param>
			/// <param name="parent">The parent.</param>
			/// <returns></returns>
			public Item AddItem(string name, BranchItem branch, Item parent)
	    {
	      Error.AssertString(name, "name", false);
	      Error.AssertObject(branch, "master");
	      Error.AssertObject(parent, "parent");
	      Item item = null;
	      try
	      {
	        item = parent.Add(name, branch);
	        this.ProcessAdded(item);
	        return item;
	      }
	      catch (WorkflowException ex)
	      {
	        this.HandleException(ex);
	        throw;
	      }
	    }

	    /// <summary>
	    /// Adds the item.
	    /// </summary>
	    /// <param name="name">The name.</param>
	    /// <param name="template">The template.</param>
	    /// <param name="parent">The parent.</param>
	    /// <returns></returns>
	    public Item AddItem(string name, TemplateItem template, Item parent)
	    {
	      Error.AssertString(name, "name", false);
	      Error.AssertObject(template, "template");
	      Error.AssertObject(parent, "parent");
	      try
	      {
	        Item item = parent.Add(name, template);
	        this.ProcessAdded(item);
	        return item;
	      }
	      catch (WorkflowException ex)
	      {
	        this.HandleException(ex);
	        throw;
	      }
	    }

			private void HandleException(WorkflowException ex)
			{
			  try
			  {
			    if (ex.Item != null)
			    {
			      using (new SecurityDisabler())
			      {
			        ex.Item.Delete();
			      }
			    }
			  }
			  catch (Exception exception)
			  {
			    Log.Error("Error handling workflow exception", exception, this);
			  }
			}

			private void ProcessAdded(Item item)
	    {
	      if (item != null)
	      {
	        this.StartEditing(item);
	      }
	    }

	    public bool HasDefaultWorkflow(Item item)
	    {
	      Field field = item.Fields[FieldIDs.DefaultWorkflow];
	      if (!this.Enabled)
	      {
	        return false;
	      }
	      if (field != null)
	      {
	        string inheritedValue = field.InheritedValue;
	        return inheritedValue.Length > 0;
	      }
	      return false;
	    }

	    /// <summary>
	    /// Determines whether the specified item has a workflow.
	    /// </summary>
	    /// <param name="item">The item.</param>
	    /// <returns>
	    /// 	<c>true</c> if the specified item has a workflow; otherwise, <c>false</c>.
	    /// </returns>
	    public bool HasWorkflow(Item item)
	    {
	      if (!this.Enabled)
	      {
	        return false;
	      }
	      return this.GetWorkflow(item) != null;
	    }

			public Item StartEditing(Item item)
	    {
	      Error.AssertObject(item, "item");
	      if (Settings.RequireLockBeforeEditing && !Context.User.IsAdministrator)
	      {
	        if (Context.Data.IsAdministrator)
	        {
	          return this.Lock(item);
	        }
	        if (StandardValuesManager.IsStandardValuesHolder(item))
	        {
	          return this.Lock(item);
	        }
	        if (!this.HasWorkflow(item) && !this.HasDefaultWorkflow(item))
	        {
	          return this.Lock(item);
	        }
	        if (!this.IsApproved(item))
	        {
	          return this.Lock(item);
	        }
	        Item item2 = item.Versions.AddVersion();
	        if (item2 != null)
	        {
	          return this.Lock(item2);
	        }
	        return null;
	      }
	      return item;
	    }

	    public bool IsApproved(Item item)
	    {
	      return this.IsApproved(item, null);
	    }

	    /// <summary>
	    /// Determines whether the specified item is approved.
	    /// </summary>
	    /// <param name="item">The item.</param>
	    /// <param name="targetDatabase">The pre-production database to check approval for.</param>
	    /// <returns>
	    ///   <c>true</c> if the specified item is approved; otherwise, <c>false</c>.
	    /// </returns>
	    public bool IsApproved(Item item, Database targetDatabase)
	    {
	      Error.AssertObject(item, "item");
	      IWorkflow workflow = this.GetWorkflow(item);
	      if (workflow != null)
	      {
	        return workflow.IsApproved(item, targetDatabase);
	      }
	      return true;
	    }

	    public IWorkflow GetWorkflow(Item item)
	    {
	      Error.AssertObject(item, "item");
	      if (this.Enabled)
	      {
	        IWorkflowProvider workflowProvider = item.Database.WorkflowProvider;
	        if (workflowProvider != null)
	        {
	          return workflowProvider.GetWorkflow(item);
	        }
	      }
	      return null;
	    }

			private Item Lock(Item item)
	    {
	      if (TemplateManager.IsFieldPartOfTemplate(FieldIDs.Lock, item) && !item.Locking.Lock())
	      {
	        return null;
	      }
	      return item;
	    }

		}



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
              WorkflowSubstitute workflow = new WorkflowSubstitute();
							try
							{
								if (item.TemplateID == TemplateIDs.BranchTemplate)
								{
									BranchItem branch = item;
									item3 = workflow.AddItem(args.Result, branch, parent);
									Log.Audit(this, "Add from branch: {0}", new string[] { AuditFormatter.FormatItem((Item)branch) });
								}
								else
								{
									TemplateItem template = item;
									item3 = workflow.AddItem(args.Result, template, parent);
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
