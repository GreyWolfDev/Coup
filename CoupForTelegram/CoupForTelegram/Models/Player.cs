using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoupForTelegram.Models
{
    public class Player
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<Card> Cards { get; set; } = new List<Card>();
        public int Coins { get; set; } = 2;
    }
}
