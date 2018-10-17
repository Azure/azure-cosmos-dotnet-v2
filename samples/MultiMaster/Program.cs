using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CosmosDB.Sql.DotNet.MultiMaster
{
    class Program
    {
        public static void Main(string[] args)
        {
            Program.RunScenariosAsync().GetAwaiter().GetResult();
        }

        private static async Task RunScenariosAsync()
        {
            MultiMasterScenario scenario = new MultiMasterScenario();

            await scenario.InitializeAsync();
            await scenario.RunBasicAsync();
            await scenario.RunManualConflictAsync();
            await scenario.RunLWWAsync();
            await scenario.RunUDPAsync();
        }
    }
}
