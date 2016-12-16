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

        internal static Random R = new Random();

        internal static List<Game> Games = new List<Game>();
        internal static int GamesPlayed = 0;
        internal static DateTime StartTime = DateTime.UtcNow;
        static void Main(string[] args)
        {
            new Thread(Bot.Initialize).Start();
            new Thread(Cleaner).Start();
            new Thread(Monitor).Start();
            Thread.Sleep(-1);
        }

        static void Monitor()
        {
            while (true)
            {
                var msg = $"Games: {Games.Count()}\tPlayers: {Games.Sum(x => x.Players.Count())}\nTotal Games Played: {GamesPlayed}\nUptime: {DateTime.UtcNow - StartTime}";
                Console.Clear();
                Console.WriteLine(msg);
                Thread.Sleep(5000);
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
