using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SilentBackupService
{
	static class DebugIO
	{
		public static void WriteInstruction(string s)
		{
			return;
			//Console.WriteLine();
			//Console.WriteLine();
			//Console.ForegroundColor = ConsoleColor.Red;
			//Console.Write("-> " + s + ": ");
			//Console.ForegroundColor = ConsoleColor.White;
		}

		public static void WriteStatement(string src, string s)
		{
			return;
			//string t = s;
			//Console.WriteLine();
			//Console.WriteLine();
			//Console.ForegroundColor = ConsoleColor.DarkGreen;
			//Console.Write(src + " says \"" + s + "\"");
			//Console.ForegroundColor = ConsoleColor.White;
			bool tryAgain;
			do
			{
				tryAgain = false;
				try
				{
					using (StreamWriter sw = new StreamWriter(AppInfo.ServiceLogPath, true))
					{
						sw.WriteLine("-----------------------------------------------------------------------");
						sw.WriteLine(DateTime.Now.ToString() + ":::: " + src + " says: " + s);
						sw.WriteLine("-----------------------------------------------------------------------");
					}
				}
				catch (Exception ex)
				{
					tryAgain = true;
				}
				System.Threading.Thread.Sleep(1000);
			} while (tryAgain);
		}
	}

	static class ReportIO
	{
		public static void WriteStatement(string s)
		{
   //         new System.Threading.Thread(() => {

   //         }).Start();
			//bool tryAgain;
			//do
			//{
			//	tryAgain = false;
			//	try
			//	{
					using (StreamWriter sw = new StreamWriter(AppInfo.ServiceLogPath, true))
					{
						sw.WriteLine(DateTime.Now.ToString() + ":::: " + s);
					}
			//	}
			//	catch (Exception ex)
			//	{
			//		tryAgain = true;
			//	}
			//	System.Threading.Thread.Sleep(1000);
			//} while (tryAgain);
		}
	}
}
