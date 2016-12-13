using CoupForTelegram.Helpers;
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
        public int LastMessageId = 0;
        public string LastMessageSent = "";
        /// <summary>
        /// Is this game for friends only, or strangers
        /// </summary>
        public bool IsRandom = false;

        /// <summary>
        /// Is this game in PM or Group
        /// </summary>
        public bool IsGroup = false;

        public InlineKeyboardMarkup LastMenu { get; internal set; }

        public Game(int id, User u, bool group, bool random, long chatid = 0)
        {
            GameId = id;
            ChatId = chatid;
            IsGroup = group;
            IsRandom = random;
            AddPlayer(u);
        }

        public int AddPlayer(User u)
        {
            if (u == null)
            {
                Players.Add(new Player { Id = 32432, Name = "Test" });
                StartGame();
                return 0;
            }
            Player p = null;
            if (!Players.Any(x => x.Id == u.Id))
            {
                p = new Player { Id = u.Id, Name = (u.FirstName + " " + u.LastName).Trim(), TeleUser = u };
                Players.Add(p);
            }
            else
                return 1;
            var name = p.GetName();
            Send($"{name} has joined the game").ToList();
            if (Players.Count >= 6)
                new Task(StartGame).Start();
            return 0;
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
            for (int i = 0; i < Players.Count(); i++)
            {
                Console.WriteLine($"Player {i}: {Players[i].Cards[0].Name}, {Players[i].Cards[1].Name}");
            }
            Console.WriteLine($"Deck:\n{Cards.Aggregate("", (a, b) => a + "\n" + b.Name)}");
#endif
            State = GameState.Running;

            while (true)
            {
                //will use break to end the game
                foreach (var p in Players)
                {
                    //it is the players turn.  Notify
                }
            }
        }



        public void TellCards(Player p)
        {
            Send(p.Cards.Aggregate("Your cards:\n", (a, b) => a + "\n" + b.Name), p.Id);
        }



        #region Communications
        private IEnumerable<Message> Send(string message, long id = 0, bool clearKeyboard = false, InlineKeyboardMarkup menu = null, bool newMsg = false)
        {
            var result = new List<Message>();
            if (id == 0)
            {
                if (IsGroup)
                    id = ChatId;
                else
                {
                    foreach (var p in Players)
                    {
                        yield return Send(message, p.Id, clearKeyboard, menu, newMsg).First();
                    }
                }
            }
            if (id != 0)
            {
                Message r = null;
                try
                {

                    var p = Players.FirstOrDefault(x => x.Id == id);
                    var last = p?.LastMessageId ?? LastMessageId;
                    var lastStr = p?.LastMessageSent ?? LastMessageSent;
                    if (last != 0 & !newMsg)
                    {
                        message = lastStr + Environment.NewLine + message;
                        r = Bot.Edit(id, last, message, menu ?? LastMenu).Result;
                    }
                    else
                    {
                        r = Bot.SendAsync(message, id, clearKeyboard, menu, game: this).Result;
                        LastMenu = menu;
                    }
                    if (p != null)
                    {
                        p.LastMessageId = r.MessageId;
                        p.LastMessageSent = message;
                    }
                    else
                    {
                        LastMessageId = r.MessageId;
                        LastMessageSent = message;
                        LastMenu = menu ?? LastMenu;
                    }

                }
                catch (AggregateException e)
                {

                }
                yield return r;
            }
        }
        #endregion
    }
}
