using CoupForTelegram.Helpers;
using CoupForTelegram.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Action = CoupForTelegram.Models.Action;

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
        public Action ChoiceMade = Action.None;
        public int ChoiceTarget = 0;
        public int Turn = 0;
        public int CounterActionPlayer = 0;
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
            Send($"{name} has joined the game");
            if (Players.Count >= 6)
                new Task(StartGame).Start();
            return 0;
        }

        public int RemovePlayer(User u)
        {
            if (State == GameState.Joining)
            {
                Players.RemoveAll(x => x.Id == u.Id);
                if (Players.Count() == 0)
                {
                    State = GameState.Ended;
                }
                return 1;
            }
            return 0;
        }

        public void StartGame()
        {
            if (Players.Count < 2)
            {
                //end the game
                //TODO cancel game
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
                    Turn = p.Id;
                    Send($"{p.GetName()}'s turn.", newMsg: true).ToList();
                    //it is the players turn.
                    //first off, do they have to Coup?
                    if (p.Coins >= 10)
                    {
                        //Coup time!
                        Send($"{p.GetName()} has 10 or more coins, and must Coup someone.");
                        ChoiceMade = Action.Coup;

                    }
                    //ok, no, so get actions that they can make
                    Send($"{p.GetName()} please choose an action.  You have 1 minute to choose.", menu: CreateActionMenu(p), menuTo: p.Id);
                    var choice = WaitForChoice();
                    Console.WriteLine($"{p.Name} has chosen to {ChoiceMade}");
                    Player target;
                    switch (choice)
                    {
                        case Action.Income:
                            Send($"{p.Name} has chosen to take income (1 coin).");
                            p.Coins++;
                            break;
                        case Action.ForeignAid:
                            break;
                        case Action.Coup:
                            Send($"{p.Name} please choose who to Coup.  You have 1 minute to choose.", menu: CreateCoupMenu(p), menuTo: p.Id);
                            WaitForChoice(true);
                            target = Players.FirstOrDefault(x => x.Id == ChoiceTarget);
                            Send($"{p.Name} has chosen to Coup {target.Name}!  {target.Name} please choose a card to lose.");
                            break;
                        case Action.Tax:
                            Send($"{p.Name} has chosen to Tax (3 coins) with their Duke.  Does anyone wish to call bluff?", menu: CreateBluffMenu(), menuNot: p.Id);
                            break;
                        case Action.Assassinate:
                            break;
                        case Action.Exchange:
                            break;
                        case Action.Steal:
                            break;
                        case Action.BlockSteal:
                            break;
                        case Action.BlockAssassin:
                            break;
                        case Action.BlockForeignAid:
                            break;
                        case Action.None:
                            break;
                    }

                    ChoiceMade = Action.None;
                }
            }
        }

        /// <summary>
        /// Sleeps until a choice has been made
        /// </summary>
        /// <param name="timeToChoose">Time in half seconds to choose</param>
        /// <returns>Whether or not a choice was actually made</returns>
        private Action WaitForChoice(bool choosingPlayer = false, int timeToChoose = 120)
        {
            while (choosingPlayer ? ChoiceTarget == 0 : ChoiceMade == Action.None && timeToChoose > 0)
            {
                Thread.Sleep(500);
                timeToChoose--;
            }
            return ChoiceMade;
        }


        #region Menus
        public InlineKeyboardMarkup CreateBluffMenu()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new InlineKeyboardButton("Call Bluff", $"bluff|{GameId}"),
                new InlineKeyboardButton("Allow", $"allow|{GameId}")
            });
        }
        public InlineKeyboardMarkup CreateActionMenu(Player p)
        {
            //seed choices with all actions
            var choices = Enum.GetValues(typeof(Action)).Cast<Action>().Where(x => !x.ToString().StartsWith("Block") && x != Action.None).ToList();
            //remove actions player doesn't have the coins for
            if (p.Coins < 3) choices.Remove(Action.Assassinate);
            if (p.Coins < 7) choices.Remove(Action.Coup);
            //return choice menu
            return new InlineKeyboardMarkup(choices.Select(x => new[] { new InlineKeyboardButton(x.ToString(), $"{x}|{GameId}") }).ToArray());
        }
        public InlineKeyboardMarkup CreateCoupMenu(Player p)
        {
            var choices = Players.Where(x => x.Id != p.Id).Select(x => new[] { new InlineKeyboardButton(x.Name, $"choose|{GameId}|{x.Id}") }).ToArray();
            return new InlineKeyboardMarkup(choices);
        }
        #endregion

        #region Communications
        public void TellCards(Player p)
        {
            Send(p.Cards.Aggregate("Your cards:\n", (a, b) => a + "\n" + b.Name), p.Id, newMsg: true).ToList();
        }
        private List<Message> Send(string message, long id = 0, bool clearKeyboard = false, InlineKeyboardMarkup menu = null, bool newMsg = false, int menuTo = 0, int menuNot = 0)
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
                        var newMenu = p.Id == menuTo && p.Id != menuNot ? menu : null;
                        result.AddRange(Send(message, p.Id, clearKeyboard, newMenu, newMsg));
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
                result.Add(r);
            }
            return result;
        }
        #endregion
    }
}
