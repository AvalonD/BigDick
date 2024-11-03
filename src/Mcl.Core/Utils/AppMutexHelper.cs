using Mcl.Core.Inject;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

namespace Mcl.Core.Utils
{
    public class AppMutexHelper
	{
		public static Mutex AppMutex;

		public static bool CheckAppMutex()
		{
			string name = Assembly.GetEntryAssembly().GetName().Name;
			AppMutex = new Mutex(initiallyOwned: true, name, out var createdNew); 

            return createdNew;
		}

		public static bool CheckAppMutex(string appId)
        {
            InjectManager.Init();


            AppMutex = new Mutex(initiallyOwned: true, appId, out var createdNew);
			return createdNew;
		}



    }
}
