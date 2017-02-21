using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Database;

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

        internal static Random R = new Random();

        internal static List<Game> Games = new List<Game>();
        internal static int GamesPlayed = 0;
        internal static DateTime StartTime = DateTime.UtcNow;
        static void Main(string[] args)
        {

            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
            {
                //drop the error to log file and exit
                using (var sw = new StreamWriter(Path.Combine(Bot.RootDirectory, "Logs\\error.log"), true))
                {
                    
                    var e = (eventArgs.ExceptionObject as Exception);
                    sw.WriteLine(DateTime.Now);
                    sw.WriteLine(e.Message);
                    sw.WriteLine(e.StackTrace + "\n");
                    if (eventArgs.IsTerminating)
                        Environment.Exit(5);
                }
            };

            //initialize EF before we start receiving
            using (var db = new CoupContext())
            {
                var count = db.ChatGroups.Count();
            }


            new Thread(Bot.Initialize).Start();
            new Thread(Cleaner).Start();
            new Thread(Monitor).Start();
            Thread.Sleep(-1);
        }

        static void Monitor()
        {
            while (true)
            {
                var msg = $"Games: {Games.Count()}   \nPlayers: {Games.Sum(x => x.Players.Count())}   \nTotal Games Played: {GamesPlayed}\nUptime: {DateTime.UtcNow - StartTime}";
                Console.SetCursorPosition(0, 0);
                Console.WriteLine(msg);
                Thread.Sleep(1000);
            }
        }

        static void Cleaner()
        {
            while (true)
            {
                
                for (int i = Games.Count() - 1; i >= 0; i--)
                {
                    if (Games[i].State == Models.GameState.Ended)
                    {
                        Games[i] = null;
                        Games.RemoveAt(i);
                    }
                }
                Thread.Sleep(1000);
            }
        }

        
    }
}
