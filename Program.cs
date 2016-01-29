using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using ConsoleApplication2.Classes;
using Newtonsoft.Json;
using RestSharp;

namespace ConsoleApplication2
{
    class Program
    {
        static void Main(string[] args)
        {
            var app = new App();

            app.Run();

            Console.ReadKey();
        }
    }
}
