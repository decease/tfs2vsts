using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Server;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.TeamFoundation.TestManagement.Client;

namespace ConsoleApplication2.Classes
{
    public class VSTSClient : IDisposable
    {
        private static string _sourceCollectionUri = Constants.SOURCE_TFS_URL;
        private readonly NetworkCredential _sourceCredentials = new NetworkCredential(Constants.SOURCE_USER_NAME, Constants.SOURCE_PASSWORD);

        private static string _targetCollectionUri = Constants.TARGET_TFS_URL;
        private static string _targetTeamProjectName = Constants.TARGET_PROJECT_NAME;
        private readonly NetworkCredential _targetCredentials = new NetworkCredential(Constants.TARGET_USER_NAME, Constants.TARGET_PASSWORD);

        private readonly TfsTeamProjectCollection _tpc;
        private readonly WorkItemStore _workItemStore;
        private readonly ITestManagementTeamProject _project;
        private readonly Project _vsProject;

        private ITestPlan _currentTestPlan;
        private ICommonStructureService _css;


        public VSTSClient()
        {
            var tfsUri = new Uri(_targetCollectionUri);
            var basicCredential = new BasicAuthCredential(_targetCredentials);
            var tfsCredentials = new TfsClientCredentials(basicCredential) { AllowInteractive = false };
            _tpc = new TfsTeamProjectCollection(tfsUri, tfsCredentials);
            _tpc.EnsureAuthenticated();

            _workItemStore = _tpc.GetService<WorkItemStore>();
            _project = _tpc.GetService<ITestManagementService>().GetTeamProject(_targetTeamProjectName);
        }


        public void CopyAreas()
        {
            // SOURCE TFS
            var tfsUri = new Uri(_sourceCollectionUri);
            var sourceTfs = new TfsTeamProjectCollection(tfsUri, _sourceCredentials);
            sourceTfs.EnsureAuthenticated();

            var targetCss = _tpc.GetService<ICommonStructureService>();
            var targetProject = targetCss.GetProjectFromName(_targetTeamProjectName);
            var targetNodes = targetCss.ListStructures(targetProject.Uri);

            var targetAreas = targetNodes.FirstOrDefault(node => node.StructureType == "ProjectModelHierarchy");

            var sourceWorkItemStore = sourceTfs.GetService<WorkItemStore>();
            var sourceProject = sourceWorkItemStore.Projects.GetById(Constants.SOURCE_PROJECT_ID);

            foreach (Node areaRootNode in sourceProject.AreaRootNodes)
            {
                CreateArea(targetCss, areaRootNode, targetAreas);
            }
        }

        private void CreateArea(ICommonStructureService targetCss, Node areaRootNode, NodeInfo parentArea)
        {
            NodeInfo newNode;
            var nodeName = areaRootNode.Name.Replace('+', ' ');
            try
            {
                var newUri = targetCss.CreateNode(nodeName, parentArea.Uri);
                newNode = targetCss.GetNode(newUri);
            }
            catch (CommonStructureSubsystemException e)
            {
                Logger.Warn($"Area \"{nodeName}\" already exists.");
                newNode = targetCss.GetNodeFromPath(parentArea.Path + "\\" + nodeName);
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
                    CreateArea(targetCss, childNode, newNode);
                }
            }
        }

        public void CreateTestPlan(TestPlanJson plan)
        {
            _currentTestPlan = _project.TestPlans.Create();
            _currentTestPlan.AreaPath = plan.area.name;
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
                testSuite = _project.TestSuites.CreateStatic();
                
            }
            else
            {
                testSuite = _project.TestSuites.CreateDynamic();
                ((IDynamicTestSuite)testSuite).Query = _project.CreateTestQuery(suite.queryString);
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

        public void CreateTestCase(Dictionary<string, string> fields, int suiteId)
        {
            var testCase = _project.TestCases.Create();
            testCase.Title = fields["System.Title"];
            testCase.Area = fields["System.AreaPath"];
            //TODO: [OM] Add AssignTO
            testCase.Save();

            _currentTestPlan.Refresh();
            var parentSuite = Utils.FindSuiteRecursive(_currentTestPlan.RootSuite, suiteId);
            parentSuite.Entries.Add(testCase);

            _currentTestPlan.Save();

            var wiTestCase = _workItemStore.GetWorkItem(testCase.Id);
            wiTestCase["Microsoft.VSTS.TCM.Steps"] = fields["Microsoft.VSTS.TCM.Steps"];
            wiTestCase.Save();
            Logger.Success($"Test case was created with id({testCase.Id}) name({testCase.Title})");
        }

        public void Dispose()
        {
            _tpc.Dispose();
        }
    }
}