using CoupForTelegram.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CoupForTelegram
{
    public class Game
    {
        public int GameId;
        public long ChatId;
        public List<Card> Cards = CardHelper.GenerateCards();
        public List<Player> Players = new List<Player>();
        public GameState State = GameState.Joining;
        public Game(int id, User u)
        {
            GameId = id;
            AddPlayer(u);
        }

        public void AddPlayer(User u)
        {
            if (u == null)
            {
                Players.Add(new Player { Id = 32432, Name = "Test" });
                StartGame();
                return;
            }

            if (!Players.Any(x => x.Id == u.Id))
                Players.Add(new Player { Id = u.Id, Name = (u.FirstName + " " + u.LastName).Trim() });
            if (Players.Count >= 6)
                StartGame();
        }

        public void StartGame()
        {
            if (Players.Count < 2)
            {
                //end the game
            }

            State = GameState.Initializing;

            //hand out cards
            foreach (var p in Players)
            {
                var card = Cards.First();
                Cards.Remove(card);
                p.Cards.Add(card);
            }
            //round robin :P
            foreach (var p in Players)
            {
                var card = Cards.First();
                Cards.Remove(card);
                p.Cards.Add(card);

                //tell player their cards
                TellCards(p);
            }
            //DEBUG OUT
#if DEBUG
            for (int i = 0; i< Players.Count(); i++)
            {
                Console.WriteLine($"Player {i}: {Players[i].Cards[0].Name}, {Players[i].Cards[1].Name}");
            }
            Console.WriteLine($"Deck:\n{Cards.Aggregate("", (a, b) => a + "\n" + b.Name)}");
#endif
        }

        public void TellCards(Player p)
        {
            Send(p.Cards.Aggregate("Your cards:\n", (a, b) => a + "\n" + b.Name), p.Id);
        }



        #region Communications
        private Task<Telegram.Bot.Types.Message> Send(string message, long id = 0, bool clearKeyboard = false, InlineKeyboardMarkup menu = null)
        {
            if (id == 0)
                id = ChatId;
            return Bot.SendAsync(message, id, clearKeyboard, menu, game: this);
        }
        #endregion
    }
}
