using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Server;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.TeamFoundation.TestManagement.Client;

namespace ConsoleApplication2.Classes
{
    public class VSTSMigrationManager : IDisposable
    {
        private static string _sourceCollectionUri = Constants.SOURCE_TFS_URL;
        private readonly NetworkCredential _sourceCredentials = new NetworkCredential(Constants.SOURCE_USER_NAME, Constants.SOURCE_PASSWORD);

        private static string _targetCollectionUri = Constants.TARGET_TFS_URL;
        private readonly NetworkCredential _targetCredentials = new NetworkCredential(Constants.TARGET_USER_NAME, Constants.TARGET_PASSWORD);

        private readonly TfsTeamProjectCollection _targetTfs;
        private readonly WorkItemStore _targetWorkItemStore;
        private readonly ITestManagementTeamProject _targetProject;
        private readonly Project _vsProject;

        private ITestPlan _currentTestPlan;
        private ICommonStructureService _css;

        /// <summary>
        /// Relations between TFS wi id and VSTS wi id
        /// Key "TFS id
        /// Value "VSTS id
        /// </summary>
        private readonly Dictionary<int, int> _wiIdRealtions = new Dictionary<int, int>();

        private readonly WorkItemStore _sourceWorkItemStore;
        private readonly ITestManagementTeamProject _sourceProject;
        private NodeInfo[] _targetNodes;
        private ICommonStructureService _targetCss;


        public VSTSMigrationManager()
        {
            // Target connections
            var targetTfsUri = new Uri(_targetCollectionUri);
            var targetBasicCredential = new BasicAuthCredential(_targetCredentials);
            var targetTfsCredentials = new TfsClientCredentials(targetBasicCredential) { AllowInteractive = false };
            _targetTfs = new TfsTeamProjectCollection(targetTfsUri, targetTfsCredentials);
            _targetTfs.EnsureAuthenticated();

            _targetWorkItemStore = _targetTfs.GetService<WorkItemStore>();
            _targetProject = _targetTfs.GetService<ITestManagementService>().GetTeamProject(Constants.TARGET_PROJECT_NAME);

            _targetCss = _targetTfs.GetService<ICommonStructureService>();
            var targetProject = _targetCss.GetProjectFromName(Constants.TARGET_PROJECT_NAME);
            _targetNodes = _targetCss.ListStructures(targetProject.Uri);

            // Source connections
            var sourceTfsUri = new Uri(_sourceCollectionUri);
            var sourceTfs = new TfsTeamProjectCollection(sourceTfsUri, _sourceCredentials);
            sourceTfs.EnsureAuthenticated();

            _sourceWorkItemStore = sourceTfs.GetService<WorkItemStore>();
            _sourceProject = sourceTfs.GetService<ITestManagementService>().GetTeamProject(Constants.SOURCE_PROJECT_NAME);
        }

        /// <summary>
        /// Migrate all areas
        /// </summary>
        public void MigrateAreas()
        {
            // SOURCE TFS
            var targetAreas = _targetNodes.FirstOrDefault(node => node.StructureType == "ProjectModelHierarchy");

            var sourceProject = _sourceWorkItemStore.Projects.GetById(Constants.SOURCE_PROJECT_ID);

            foreach (Node areaRootNode in sourceProject.AreaRootNodes)
            {
                CreateArea(areaRootNode, targetAreas);
            }
        }

        private void CreateArea(Node areaRootNode, NodeInfo parentArea)
        {
            NodeInfo newNode;
            var nodeName = areaRootNode.Name.Replace('+', ' ');
            try
            {
                var newUri = _targetCss.CreateNode(nodeName, parentArea.Uri);
                newNode = _targetCss.GetNode(newUri);
                Logger.Success($"Area \"{nodeName}\" was created.");
            }
            catch (CommonStructureSubsystemException e)
            {
                Logger.Warn($"Area \"{nodeName}\" already exists.");
                newNode = _targetCss.GetNodeFromPath(parentArea.Path + "\\" + nodeName);
            }
            catch (Exception e)
            {
                Logger.Error($"Some error while creating \"{nodeName}\" area.");
                Logger.Error(e.Message);

                return;
            }

            if (areaRootNode.HasChildNodes)
            {
                foreach (Node childNode in areaRootNode.ChildNodes)
                {
                    CreateArea(childNode, newNode);
                }
            }
        }


        /// <summary>
        /// Migrate allowed iterations
        /// </summary>
        public void MigrateIterations()
        {
            var iterationsNode = _targetNodes.First(node => node.StructureType == "ProjectLifecycle");

            foreach (var iterationName in Constants.AllowIterations)
            {
                _targetCss.CreateNode(iterationName, iterationsNode.Uri);
                Logger.Success($"Iteration \"{iterationName}\" was created.");
            }
        }

        public void MigrateTestCases()
        {
            var testCases = _sourceProject.TestCases.Query("SELECT [Id] FROM WorkItems");
            var total = testCases.Count();
            var left = total;
            foreach (var testCase in testCases)
            {
                var id = testCase.Id;
                var testCaseWi = _sourceWorkItemStore.GetWorkItem(id);
                var vstsId = CreateTestCase(testCaseWi.Fields);

                _wiIdRealtions[id] = vstsId;

                Logger.Log($"{--left}/{total} test cases migrated.");
            }
        }

        public void CreateTestPlan(TestPlanJson plan)
        {
            _currentTestPlan = _targetProject.TestPlans.Create();
            _currentTestPlan.AreaPath = plan.area.name.Replace(Constants.SOURCE_PROJECT_NAME, Constants.TARGET_PROJECT_NAME);
            _currentTestPlan.Name = plan.name;
            _currentTestPlan.Description = $"Assigned to: {plan.assignedTo}";

            _currentTestPlan.Save();
            Logger.Success($"Test plan was created with id({_currentTestPlan.Id}) name({_currentTestPlan.Name})");
        }

        public int? GetSuiteId(string suiteName)
        {
            _currentTestPlan.Refresh();
            return Utils.GetSuiteIdRecursive(_currentTestPlan.RootSuite, suiteName);
        }

        public int CreateTestSuite(TestSiuteJson suite, int parentId)
        {
            ITestSuiteBase testSuite;
            if (suite.suiteType == TestSiuteJson.StaticType)
            {
                testSuite = _targetProject.TestSuites.CreateStatic();
                
            }
            else
            {
                testSuite = _targetProject.TestSuites.CreateDynamic();
                ((IDynamicTestSuite)testSuite).Query = _targetProject.CreateTestQuery(suite.queryString.Replace(Constants.SOURCE_PROJECT_NAME, Constants.TARGET_PROJECT_NAME));
            }

            testSuite.Title = suite.name;
            testSuite.Description = $"Assigned to: {suite.assignedTo}";

            _currentTestPlan.Refresh();

            if (parentId == _currentTestPlan.RootSuite.Id)
            {
                _currentTestPlan.RootSuite.Entries.Add(testSuite);
            }
            else
            {
                var parentSuite = Utils.FindSuiteRecursive(_currentTestPlan.RootSuite, parentId);
                if (parentSuite != null)
                {
                    parentSuite.Entries.Add(testSuite);
                }
                else
                {
                    Logger.Error($"Can't found suite with id ({parentId})");
                }
            }

            _currentTestPlan.Save();
            Logger.Success($"Test suite was created with id({testSuite.Id}) name({testSuite.Title})");

            return testSuite.Id;
        }

        /// <summary>
        /// Create new unrelated test case
        /// </summary>
        /// <param name="fields"></param>
        private int CreateTestCase(FieldCollection fields)
        {
            var testCase = _targetProject.TestCases.Create();
            testCase.Title = fields["System.Title"].Value.ToString();
            testCase.Area = fields["System.AreaPath"].Value.ToString().Replace(Constants.SOURCE_PROJECT_NAME, Constants.TARGET_PROJECT_NAME);

            if (!string.IsNullOrEmpty(fields["System.AssignedTo"].Value.ToString()))
            {
                testCase.Description = $"Assigned to: {fields["System.AssignedTo"].Value}";
            } else if (!string.IsNullOrEmpty(fields["System.CreatedBy"].Value.ToString()))
            {
                testCase.Description = $"Created by: {fields["System.CreatedBy"].Value}";
            }

            testCase.Save();

            var wiTestCase = _targetWorkItemStore.GetWorkItem(testCase.Id);
            wiTestCase["Microsoft.VSTS.TCM.Steps"] = fields["Microsoft.VSTS.TCM.Steps"].Value.ToString();
            wiTestCase.Save();

            Logger.Success($"Test case was created with id('{testCase.Id}') name('{testCase.Title}')");

            return testCase.Id;
        }

        /// <summary>
        /// Create relation between test case and suite
        /// </summary>
        /// <param name="testCaseId"></param>
        /// <param name="suiteId"></param>
        public void RelateTestCaseToSuite(int testCaseId, int suiteId)
        {
            var testCase = _targetProject.TestCases.Find(_wiIdRealtions[testCaseId]);
            if (testCase == null)
            {
                Logger.Error($"Can't find test case with Id = '{_wiIdRealtions[testCaseId]}'");
                return;
            }

            _currentTestPlan.Refresh();
            var parentSuite = Utils.FindSuiteRecursive(_currentTestPlan.RootSuite, suiteId);
            parentSuite.Entries.Add(testCase);

            _currentTestPlan.Save();

            Logger.Success($"Test case (id: \"{testCase.Id}\" name: \"{testCase.Title}\") was related to suite (id: \"{suiteId}\")");
        }

        public void Dispose()
        {
            _targetTfs.Dispose();
        }

        public void MigrateWorkItems()
        {
            var workItemMigrationManager = new WorkItemMigrationManager(_sourceWorkItemStore, _targetWorkItemStore);

            workItemMigrationManager.Migrate();
        }
    }

    public class WorkItemMigrationManager
    {
        private readonly WorkItemStore _sourceWorkItemStore;
        private readonly WorkItemStore _targetWorkItemStore;

        private readonly List<string> _taskFields = new List<string>
        {
            "Original Estimate",
            "Activity",
            "Stack Rank",
            "Completed Work",
            "Remaining Work",
            "Priority",
            "Description",
            "Title"
        };

        private readonly List<string> _bugFields = new List<string>
        {
            "Original Estimate",
            "Activity",
            "Stack Rank",
            "Completed Work",
            "Remaining Work",
            "Priority",
            "Repro Steps",
            "Title",
            "Spec"
        };

        private readonly List<string> _userStoryFields = new List<string>
        {
            "Priority",
            "Stack Rank",
            "Severity",
            "Description",
            "Title"
        };

        public WorkItemMigrationManager(WorkItemStore sourceWorkItemStore, WorkItemStore targetWorkItemStore)
        {
            _sourceWorkItemStore = sourceWorkItemStore;
            _targetWorkItemStore = targetWorkItemStore;
        }


        public void Migrate()
        {
            //TODO: [OM] Uncomment
            MigrateTasks();
            //MigrateBugs();
            //MigrateUserStories();
        }

        private void MigrateTasks()
        {
            var query =
                $@"
                    select * from workitems
                    where 
                        (System.State = 'Active' OR System.State = 'Resolved') AND
                        System.WorkItemType = 'Task' AND
                        System.IterationPath IN ({
                        string.Join(", ", Constants.AllowIterations.Select(a => $"'{Constants.SOURCE_PROJECT_NAME}\\{a}'"))})";

            var tasks = _sourceWorkItemStore.Query(query);
            var wiType = _targetWorkItemStore.Projects[Constants.TARGET_PROJECT_NAME].WorkItemTypes["Task"];

            var total = tasks.Count;
            var left = total;

            var fixRegExp = new Regex("[Ff]ix\\s\\d");
            foreach (WorkItem task in tasks)
            {
                if (fixRegExp.IsMatch(task.Title))
                {
                    // Skip all "Fix" tasks for bugs
                    continue;
                }

                var workItem = CreateWorkItem(wiType, task, _taskFields);

                Logger.Success($"Task [{left--}/{total}] (id: \"{workItem.Id}\" name: \"{workItem.Title}\") was created");
            }
        }

        private void MigrateBugs()
        {
            var query =
                $@"
                    select * from workitems
                    where 
                        (System.State = 'Active' OR System.State = 'Resolved') AND
                        System.WorkItemType = 'Bug' AND
                        System.IterationPath IN ({
            string.Join(", ", Constants.AllowIterations.Select(a => $"'{Constants.SOURCE_PROJECT_NAME}\\{a}'"))})";

            var bugs = _sourceWorkItemStore.Query(query);
            var wiType = _targetWorkItemStore.Projects[Constants.TARGET_PROJECT_NAME].WorkItemTypes["Bug"];

            var total = bugs.Count;
            var left = total;
            foreach (WorkItem bug in bugs)
            {
                var workItem = CreateWorkItem(wiType, bug, _bugFields);

                Logger.Success($"Bug [{left--}/{total}] (id: \"{workItem.Id}\" name: \"{workItem.Title}\") was created");
            }
        }

        private void MigrateUserStories()
        {
            var query =
                $@"
                    select * from workitems
                    where 
                        (System.State = 'Active' OR System.State = 'Resolved') AND
                        System.WorkItemType = 'User Story' AND
                        System.IterationPath IN ({
            string.Join(", ", Constants.AllowIterations.Select(a => $"'{Constants.SOURCE_PROJECT_NAME}\\{a}'"))})";

            var userStories = _sourceWorkItemStore.Query(query);
            var wiType = _targetWorkItemStore.Projects[Constants.TARGET_PROJECT_NAME].WorkItemTypes["User Story"];

            var total = userStories.Count;
            var left = total;
            foreach (WorkItem userStory in userStories)
            {
                var workItem = CreateWorkItem(wiType, userStory, _userStoryFields);

                Logger.Success($"User story [{left--}/{total}] (id: \"{workItem.Id}\" name: \"{workItem.Title}\") was created");
            }
        }

        private WorkItem CreateWorkItem(WorkItemType wiType, WorkItem sourceWorkItem, List<string> allowFields)
        {
            var workItem = new WorkItem(wiType);

            foreach (var taskField in allowFields)
            {
                workItem[taskField] = sourceWorkItem[taskField];
            }

            workItem["Iteration Path"] = sourceWorkItem["Iteration Path"].ToString().Replace(Constants.SOURCE_PROJECT_NAME, Constants.TARGET_PROJECT_NAME);
            workItem["Area Path"] = sourceWorkItem["Area Path"].ToString().Replace(Constants.SOURCE_PROJECT_NAME, Constants.TARGET_PROJECT_NAME);

            if (Constants.UserMapping.Keys.Any(k => k == sourceWorkItem["Assigned To"].ToString()))
            {
                workItem["Assigned To"] = Constants.UserMapping[sourceWorkItem["Assigned To"].ToString()];
            }
            else
            {
                workItem["Assigned To"] = "Ilya Vasilev";
                Logger.Warn($"User '{sourceWorkItem["Assigned To"]}' does not found in user map rules");
            }

            if (workItem.Validate().Count == 0)
            {
                workItem.Save();

                if (sourceWorkItem.AttachedFileCount > 0)
                {
                    var request = new WebClient
                    {
                        Credentials = CredentialCache.DefaultCredentials
                    };

                    foreach (Attachment attachment in sourceWorkItem.Attachments)
                    {
                        request.DownloadFile(attachment.Uri, Path.Combine(Path.GetTempPath(), attachment.Name));
                        var newAttachment = Path.GetTempPath() + attachment.Name;
                        var attNew = new Attachment(newAttachment, attachment.Comment);
                        workItem.Attachments.Add(attNew);
                    }

                    workItem.Save();
                }

                if (sourceWorkItem.State == "Resolved")
                {
                    // Set "Active" for tasks
                    workItem.State = wiType.Name != "Task" ? "Resolved" : "Active";
                    workItem.Reason = sourceWorkItem.Reason;

                    if (workItem.Validate().Count == 0)
                    {
                        workItem.Save();
                    }
                    else
                    {
                        Logger.Error($"Can't save {wiType.Name} (id: '{sourceWorkItem.Id}') - (Setting State to Resolved)");
                    }
                }
            }
            else
            {
                Logger.Error($"Can't save {wiType.Name} (id: '{sourceWorkItem.Id}'). Validation error.");
            }

            return workItem;
        }
    }
}