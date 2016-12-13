using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace CoupForTelegram.Models
{
    public class Player
    {
        public int Id { get; set; }
        public User TeleUser { get; set; }
        public string Name { get; set; }
        public List<Card> Cards { get; set; } = new List<Card>();
        public int Coins { get; set; } = 7;
        public int LastMessageId { get; set; } = 0;
        public string LastMessageSent { get; set; } = "";
        public bool? CallBluff { get; set; } = null;
        public bool? Block { get; internal set; } = null;
    }
}
