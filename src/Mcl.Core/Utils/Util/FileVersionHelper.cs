using System;
using System.Diagnostics;
using System.Reflection;

namespace Mcl.Core.Utils.Util
{
	public class FileVersionHelper
	{
		public static string GetVersion(string executablePath = null)
		{
			try
			{
				string text = null;
				if (string.IsNullOrEmpty(executablePath))
				{
					text = Assembly.GetExecutingAssembly().GetName().Version.ToString();
				}
				else
				{
					FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(executablePath);
					text = versionInfo.FileVersion;
				}
				return text;
			}
			catch (Exception)
			{
				return "0.0.0.0";
			}
		}
	}
}
