using System;
using Microsoft.Win32;

namespace Mcl.Core.Utils
{
	public class RegistryHelper
	{
		private static RegistryKey registrykey = null;

		public static void InitRegistryKey(string path)
		{
			try
			{
				registrykey = Registry.CurrentUser.CreateSubKey(path);
			}
			catch (Exception)
			{
			}
		}

		public static void SetValue(string key, string value)
		{
			try
			{
				registrykey?.SetValue(key, value);
			}
			catch (Exception)
			{
			}
		}

		public static string GetValue(string key)
		{
			try
			{
				return registrykey?.GetValue(key)?.ToString();
			}
			catch (Exception)
			{
				return null;
			}
		}
	}
}
