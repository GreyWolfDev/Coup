using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Telegram.Bot;
using Telegram.Bot.Args;

namespace CoupForTelegram
{
    class Program
    {
        internal static string RootDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        
        static void Main(string[] args)
        {
            new Thread(Bot.Initialize).Start();
            Thread.Sleep(-1);
        }

        
    }
}
