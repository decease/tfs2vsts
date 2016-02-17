using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.TeamFoundation.TestManagement.Client;
using Newtonsoft.Json;

namespace ConsoleApplication2.Classes
{
    public static class Constants
    {
        public const int RetryCount = 10;

        public const string SOURCE_TFS_URL = "http://vancouver.copemanhealthcare.com:8899/tfs/DefaultCollection";
        public const string SOURCE_PROJECT_NAME = "CHMS";
        public const string SOURCE_USER_NAME = @"COPEMAN\omishenkin";
        public const string SOURCE_PASSWORD = "Password123";
        public const int    SOURCE_PROJECT_ID = 142;

        public const string TARGET_TFS_URL = "https://carebook.visualstudio.com/DefaultCollection";
        public const string TARGET_PROJECT_NAME = "CHMS";
        public const string TARGET_USER_NAME = @"decease";
        public const string TARGET_PASSWORD = "Test123$";

        public static readonly Dictionary<string, string> UserMapping = new Dictionary<string, string>
        {
            ["Ilya Vasilyev"] = "Ilya Vasilev",
            ["Vera Summers"] = "vsummers",
            ["Vera Gladkikh"] = "vgladkikh",
            ["Andy Summers"] = "Andy Summers",
            ["Alexander Ischenko"] = "Alexander Ishchenko",
            ["Alexey Babenko"] = "Aleksey Babenko",
            ["Alisa Sergienko"] = "alisa.sergienko",
            ["GeSir Lee"] = "Lee Ge Sir",
            ["Dmitry Nikitenko"] = "Dmitry Nikitenko",
            ["Ilya Popov"] = "Ilya Popov",
            ["Sergey Nemtsev"] = "Sergey Nemtsev",
            ["Sofya Berezovskaya"] = "Sofya Berezovskaya",
            ["Oleg Mishenkin"] = "Oleg Mishenkin"
        };

        public static readonly List<string> AllowIterations = new List<string>
        {
            "Iteration 39  (catch up)",
            "Iteration 41",
            "Iteration 42",
            "Bug Backlog",
            "Iteration 40"
        };
    }


    public static class Utils
    {
        public static T GetItemFormJson<T>(string json)
        {
            var data = JsonConvert.DeserializeObject<T>(json);
            return data;
        }

        public static ICollection<T> GetItemCollectionFormJson<T>(string json)
        {
            var data = JsonConvert.DeserializeObject<WorkItemCollectionJson<T>>(json);

            return data.value;
        }

        /// <summary>
        /// Find TestSuite recursive in tree by suiteId
        /// </summary>
        /// <param name="rootSuite"></param>
        /// <param name="suiteId"></param>
        /// <returns></returns>
        public static IStaticTestSuite FindSuiteRecursive(IStaticTestSuite rootSuite, int suiteId)
        {
            var suiteEntry = rootSuite.Entries.FirstOrDefault(s => s.Id == suiteId);
            if (suiteEntry == null && rootSuite.Entries.Count > 0)
            {
                foreach (ITestSuiteEntry entry in rootSuite.Entries.Where(e => e.EntryType == TestSuiteEntryType.StaticTestSuite))
                {
                    var s = entry.TestSuite as IStaticTestSuite;
                    var suite = FindSuiteRecursive(s, suiteId);
                    if (suite != null)
                    {
                        return suite;
                    }
                }
            }

            return suiteEntry?.TestSuite as IStaticTestSuite;
        }

        /// <summary>
        /// Get TestSuite Id by test suite name recursive in tree
        /// </summary>
        /// <param name="rootSuite"></param>
        /// <param name="suiteName"></param>
        /// <returns></returns>
        public static int? GetSuiteIdRecursive(IStaticTestSuite rootSuite, string suiteName)
        {
            if (suiteName == rootSuite.Title)
            {
                return rootSuite.Id;
            }

            var suiteEntry = rootSuite.Entries.FirstOrDefault(s => s.Title == suiteName);
            if (suiteEntry == null && rootSuite.Entries.Count > 0)
            {
                foreach (ITestSuiteEntry entry in rootSuite.Entries.Where(e => e.EntryType == TestSuiteEntryType.StaticTestSuite))
                {
                    var s = entry.TestSuite as IStaticTestSuite;
                    var suite = GetSuiteIdRecursive(s, suiteName);
                    if (suite != null)
                    {
                        return suite;
                    }
                }
            }

            return suiteEntry?.Id;
        }
    }

    public static class Logger
    {
        public static void Log(string message,
            ConsoleColor color = ConsoleColor.White,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "")
        {
            Console.ForegroundColor = color;

            Console.WriteLine($"[{DateTime.Now} | {sourceFilePath.Split('\\').Last()} {memberName}()] => \r\n\t{message}");

            Console.ResetColor();
        }

        public static void Success(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "")
        {
            Log(message, ConsoleColor.DarkGreen, memberName, sourceFilePath);
        }

        public static void Error(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "")
        {
            Log(message, ConsoleColor.DarkRed, memberName, sourceFilePath);
        }

        public static void Warn(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "")
        {
            Log(message, ConsoleColor.DarkYellow, memberName, sourceFilePath);
        }
    }
}
