using CoupForTelegram.Models;
using Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace CoupForTelegram.Helpers
{
    public static class Extensions
    {
        public static void Shuffle<T>(this IList<T> list)
        {
            RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider();
            int n = list.Count;
            while (n > 1)
            {
                byte[] box = new byte[1];
                do provider.GetBytes(box);
                while (!(box[0] < n * (Byte.MaxValue / n)));
                int k = (box[0] % n);
                n--;
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public static bool BetaCheck(this User u)
        {
            using (var db = new CoupContext())
            {
                var p = db.Players.FirstOrDefault(x => x.TelegramId == u.Id);
                if (p == null && db.Players.Count() < 52)
                {
                    p = new Player
                    {
                        Created = DateTime.UtcNow,
                        Language = "English",
                        Name = (u.FirstName + " " + u.LastName).Trim(),
                        TelegramId = u.Id,
                        Username = u.Username
                    };
                    db.Players.Add(p);
                    db.SaveChanges();
                    return true;
                }
                else if (p != null)
                    return true;

                return false;
            }
        }

        public static string ToBold(this object str)
        {
            if (str == null)
                return null;
            return $"<b>{str.ToString().FormatHTML()}</b>";
        }

        public static string ToItalic(this object str)
        {
            if (str == null)
                return null;
            return $"<i>{str.ToString().FormatHTML()}</i>";
        }

        public static string FormatHTML(this string str)
        {
            return str.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        public static string GetName(this CPlayer player, bool menu = false)
        {
            if (menu)
                return player.Name;
            if (!String.IsNullOrEmpty(player.TeleUser.Username))
                return $"<a href=\"telegram.me/{player.TeleUser.Username}\">{player.Name.FormatHTML()}</a>";

            return player.Name.ToBold();
        }
    }
}
