namespace Sitecore.Support.ContentTesting.Pipelines.GetContentEditorWarnings
{
  using System;
  using System.Linq;
  using Sitecore.ContentTesting;
  using Sitecore.ContentTesting.Configuration;
  using Sitecore.ContentTesting.ContentSearch;
  using Sitecore.ContentTesting.Data;
  using Sitecore.ContentTesting.Model.Data.Items;
  using Sitecore.ContentTesting.Pipelines.GetTestCandidates;
  using Sitecore.ContentTesting.Services;
  using Sitecore.Data;
  using Sitecore.Diagnostics;
  using Sitecore.Globalization;
  using Sitecore.Pipelines.GetContentEditorWarnings;

  /// <summary>
  /// Processor that adds warnings in the Content Editor if the selected item contains active test(s).
  /// </summary>
  public class GetContentTestingWarnings
  {
    /// <summary>
    /// The content test store.
    /// </summary>
    private readonly IContentTestStore contentTestStore;

    /// <summary>
    /// The test candidate initiator.
    /// </summary>
    private readonly ITestCandidateInspectionInitiator testCandidateInitiator;

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GetContentTestingWarnings"/> class.
    /// </summary>
    public GetContentTestingWarnings() : this(null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GetContentTestingWarnings"/> class.
    /// </summary>
    /// <param name="contentTestStore">
    /// The content test store.
    /// </param>
    ///  <param name="startTestOptions">
    /// The start test options service.
    /// </param>
    public GetContentTestingWarnings([CanBeNull] IContentTestStore contentTestStore, [CanBeNull] ITestCandidateInspectionInitiator startTestOptions)
    {
      this.contentTestStore = contentTestStore ?? ContentTestingFactory.Instance.ContentTestStore;
      this.testCandidateInitiator = startTestOptions ?? ContentTestingFactory.Instance.TestCandidateInspectionInitiator;
    }

    #endregion Constructors

    #region Public methods

    public void Process(GetContentEditorWarningsArgs args)
    {
      if (!Settings.IsAutomaticContentTestingEnabled)
      {
        return;
      }

      var item = args.Item;
      if (item == null)
      {
        return;
      }

      if (this.testCandidateInitiator.GetTestInitiator(args.Item) != TestCandidatesInitiatorsEnum.Notification)
      {
        return;
      }

      try
      {
        if (this.AddSuspendedTestWarning(args))
        {
          return;
        }

        if (this.AddActiveTestWarning(args))
        {
          return;
        }

        if (this.AddPartOfActiveTestWarning(args))
        {
          return;
        }
      }
      catch (Exception e)
      {
        Log.Error("Sitecore.Support.255567: Intercepted a failed attempt to build Content Editor warnings for " + args.Item.Uri, e, this);
      }

      this.AddContentEditorTestCandidateNotification(args);
    }

    public bool AddActiveTestWarning(GetContentEditorWarningsArgs args)
    {
      var activeTests = this.contentTestStore.GetActiveTests(args.Item.Uri.ToDataUri());

      if (activeTests.Any())
      {
        args.Add(
          Translate.Text(Sitecore.ContentTesting.Texts.THIS_PAGE_HAS_ACTIVE_TEST),
          Translate.Text(Sitecore.ContentTesting.Texts.IF_YOU_EDIT_CONTENT_IT_COULD_HAVE_NEGATIVE_IMPACT));

        return true;
      }

      return false;
    }

    public bool AddSuspendedTestWarning(GetContentEditorWarningsArgs args)
    {
      var tests = this.contentTestStore.GetAllTestsForItem(args.Item.Uri.ToDataUri());

      var testDefinitions = from test in tests
                            let testItem = Database.GetItem(test.Uri)
                            where testItem != null
                            let testDefinitionItem = TestDefinitionItem.Create(testItem)
                            where testDefinitionItem != null
                            select testDefinitionItem;

      if (testDefinitions.Any(x => x.IsSuspended))
      {
        args.Add(
           Translate.Text(Sitecore.ContentTesting.Texts.THIS_PAGE_HAS_SUSPENDED_TEST),
           Translate.Text(Sitecore.ContentTesting.Texts.FIX_TEST_AND_DEPLOY_AGAIN));

        return true;

      }

      return false;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:Mark members as static", Justification = "This will introduce breaking changes.")]
    public bool AddPartOfActiveTestWarning(GetContentEditorWarningsArgs args)
    {
      var testingSearch = new TestingSearch();

      if (testingSearch.GetRunningTestsWithDataSourceItem(args.Item).Any())
      {
        args.Add(
          Translate.Text(Sitecore.ContentTesting.Texts.THIS_COMPONENT_PART_OF_ACTIVE_TEST),
          Translate.Text(Sitecore.ContentTesting.Texts.IF_EDIT_CONTENT_COULD_HAVE_NEGATIVE_IMPACT_ON_STATISTICAL_SIGNIFICANCE));

        return true;
      }

      return false;
    }

    /// <summary>
    /// Called when the Content Editor requests for warning when test can be triggred from the notification.
    /// </summary>
    /// <param name="args">
    /// The content editor notification arguments.
    /// </param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:Mark members as static", Justification = "This will introduce breaking changes.")]
    public void AddContentEditorTestCandidateNotification(GetContentEditorWarningsArgs args)
    {
      var testCandidateArgs = new GetTestCandidatesArgs(args.Item);
      GetTestCandidatesPipeline.Run(testCandidateArgs);

      if (!testCandidateArgs.Candidates.Any())
      {
        return;
      }

      var warning = new GetContentEditorWarningsArgs.ContentEditorWarning();

      warning.Title = Translate.Text(Sitecore.ContentTesting.Texts.NEW_COMPONENTS_HAS_BEEN_ADDED_TO_THIS_PAGE);
      warning.Text = Translate.Text(Sitecore.ContentTesting.Texts.DO_YOU_WANT_TO_CREATE_A_TEST);

      string createTestCommand = string.Format("test:createTest");
      warning.AddOption(Translate.Text(Sitecore.ContentTesting.Texts.CREATE_A_TEST), createTestCommand);

      args.Warnings.Add(warning);
    }


    #endregion Public methods
  }
}