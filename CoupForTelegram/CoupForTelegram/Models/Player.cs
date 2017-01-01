using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace CoupForTelegram.Models
{
    public class CPlayer
    {
        public int Id { get; set; }
        public User TeleUser { get; set; }
        private string _name;
        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;
                if (_name.Length > 20)
                {
                    _name = _name.Substring(0, 20);
                    if (_name.LastIndexOf(' ') > 15)
                        _name = _name.Substring(0, _name.LastIndexOf(' '));
                }
            }
        }
        public List<Card> Cards { get; set; } = new List<Card>();
        public int Coins { get; set; } = 2;
        public int LastMessageId { get; set; } = 0;
        public string LastMessageSent { get; set; } = "";
        public bool? CallBluff { get; set; } = null;
        public bool? Block { get; internal set; } = null;
        public string Language { get; internal set; }
        public int DBPlayerId { get; internal set; }
        public int DBGamePlayerId { get; internal set; }
        public bool HasCheckedCards { get; internal set; }
        public int AfkCount { get; internal set; }
    }
}
