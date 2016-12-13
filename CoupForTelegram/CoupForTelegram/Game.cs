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
        public List<Card> Graveyard = new List<Card>();
        public List<Player> Players = new List<Player>();
        public GameState State = GameState.Joining;
        public int LastMessageId = 0;
        public string LastMessageSent = "";
        public Action ChoiceMade = Action.None;
        public int ChoiceTarget = 0;
        public int Turn = 0;
        public int CounterActionPlayer = 0;
        public string CardToLose = "";
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
            LastMenu = null;
            Send("Game is starting!");
            if (Players.Count < 2)
            {
                //end the game
                //TODO cancel game
            }

            State = GameState.Initializing;

            //hand out cards
            //foreach (var p in Players)
            //{
            //    var card = Cards.First();
            //    Cards.Remove(card);
            //    p.Cards.Add(card);
            //}
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
                //Console.WriteLine($"Player {i}: {Players[i].Cards[0].Name}, {Players[i].Cards[1].Name}");
            }
            Console.WriteLine($"Deck:\n{Cards.Aggregate("", (a, b) => a + "\n" + b.Name)}");
#endif
            State = GameState.Running;

            while (true)
            {
                //will use break to end the game
                foreach (var p in Players)
                {
                    if (p.Cards.Count() == 0)
                        continue;
                    Turn = p.Id;
                    Send($"{p.GetName()}'s turn. {p.Name} has {p.Coins} coins.", newMsg: true).ToList();
                    p.CallBluff = false;
                    //it is the players turn.
                    //first off, do they have to Coup?
                    if (p.Coins >= 10)
                    {
                        //Coup time!
                        Send($"{p.GetName()} has 10 or more coins, and must Coup someone.");
                        ChoiceMade = Action.Coup;

                    }
                    //ok, no, so get actions that they can make
                    Send($"{p.Name} please choose an action.  You have 1 minute to choose.", menu: CreateActionMenu(p), menuTo: p.Id);
                    var choice = WaitForChoice(ChoiceType.Action);
                    Console.WriteLine($"{p.Name} has chosen to {ChoiceMade}");
                    Player target;
                    Player blocker;
                    Player bluffer;
                    switch (choice)
                    {
                        //DONE
                        case Action.Income:
                            LastMenu = null;
                            Send($"{p.Name} has chosen to take income (1 coin).");
                            p.Coins++;
                            break;

                        case Action.ForeignAid:
                            if (PlayerMadeBlockableAction(p, choice))
                            {
                                p.Coins += 2;
                            }
                            break;
                        case Action.Coup:
                            Send($"{p.Name} please choose who to Coup.  You have 1 minute to choose.", menu: CreateTargetMenu(p), menuTo: p.Id);
                            LastMenu = null;
                            WaitForChoice(ChoiceType.Target);
                            target = Players.FirstOrDefault(x => x.Id == ChoiceTarget);
                            if (target.Cards.Count() == 1)
                            {
                                Graveyard.Add(target.Cards.First());
                                target.Cards.Clear();
                                Send($"{target.Name}, you have been couped.  You are out of cards, and therefore out of the game!");
                            }
                            else
                            {
                                Send($"{p.Name.ToBold()} has chosen to Coup {target.Name.ToBold()}!  {target.Name.ToBold()} please choose a card to lose.");
                                PlayerLoseCard(target);
                            }
                            break;
                        case Action.Tax:
                            if (PlayerMadeBlockableAction(p, Action.Tax))
                            {
                                p.Coins += 3;
                            }
                            break;
                        case Action.Assassinate:
                            //TODO: get the target
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

                    //reset all the things
                    ChoiceMade = Action.None;
                    ChoiceTarget = 0;
                    CardToLose = "";
                    foreach (var pl in Players)
                        pl.CallBluff = null;
                    if (Players.Count(x => x.Cards.Count() > 0) == 1)
                    {
                        //game is over
                        var winner = Players.FirstOrDefault(x => x.Cards.Count() > 0);
                        Send($"{winner.GetName()} has won the game!", newMsg: true);
                        State = GameState.Ended;
                        return;
                    }

                }
            }
        }
        /// <summary>
        /// Sleeps until a choice has been made
        /// </summary>
        /// <param name="timeToChoose">Time in half seconds to choose</param>
        /// <returns>Whether or not a choice was actually made</returns>
        private Action WaitForChoice(ChoiceType type, int timeToChoose = 120)
        {
            while (!HasChoiceBeenMade(type) && timeToChoose > 0)
            {
                Thread.Sleep(500);
                timeToChoose--;
            }
            return ChoiceMade;
        }


        private bool HasChoiceBeenMade(ChoiceType type)
        {
            switch (type)
            {
                case ChoiceType.Action:
                    return ChoiceMade != Action.None;
                case ChoiceType.Target:
                    return ChoiceTarget != 0;
                case ChoiceType.Card:
                    return CardToLose != "";
                case ChoiceType.Block:
                    //check if ANYONE has blocked, so first person to block is the one that has to deal with it
                    return Players.Any(x => x.CallBluff == true) || Players.All(x => x.CallBluff == false);
                default:
                    return true;
            }
        }

        private bool PlayerMadeBlockableAction(Player p, Action a, Player t = null, string cardUsed = "")
        {
            //TODO finish this up.
            var aMsg = "";
            Player bluffer;
            bool canBlock = false;
            switch (a)
            {
                case Action.ForeignAid:
                    aMsg = $"{p.Name} has chosen to take foreign aid (2 coins). Does anyone want to block / call bluff?";
                    canBlock = true;
                    break;
                case Action.Assassinate:
                    aMsg = $"{p.Name} has chosen to assassinate {t.Name} [Assassin]. Does anyone want to block / call bluff?";
                    canBlock = true;
                    break;
                case Action.Tax:
                    aMsg = $"{p.Name} has chosen to take tax (3 coins) [Duke]. Does anyone want to call bluff?";
                    break;
                case Action.Exchange:
                    aMsg = $"{p.Name} has chosen to exchange cards with the deck [Ambassador]. Does anyone want to call bluff?";
                    break;
                case Action.Steal:
                    aMsg = $"{p.Name} has chosen to steal from {t.Name} [Captain]. Does anyone want to block / call bluff?";
                    canBlock = true;
                    break;
                case Action.BlockSteal:
                    aMsg = $"{p.Name} has chosen to block {t.Name} from stealing [{cardUsed}]. Does anyone want to call bluff?";
                    break;
                case Action.BlockAssassin:
                    aMsg = $"{p.Name} has chosen to block {t.Name} from assassinating [Contessa]. Does anyone want to call bluff?";
                    break;
                case Action.BlockForeignAid:
                    aMsg = $"{p.Name} has chosen to block {t.Name} from taking foreign aid [Duke]. Does anyone want to call bluff?";
                    break;
            }
            Send(aMsg, menu: CreateBlockMenu(canBlock), menuNot: p.Id);
            WaitForChoice(ChoiceType.Block);
            var blocker = Players.FirstOrDefault(x => x.CallBluff == true);
            if (blocker != null)
            {
                foreach (var pl in Players)
                    pl.CallBluff = null;
                Send($"{blocker.Name} has chosen to block with their Duke.  Does anyone wish to call {blocker.Name}'s bluff?", menu: CreateBluffMenu(), menuNot: blocker.Id);
                LastMenu = null;
                WaitForChoice(ChoiceType.Block);
                bluffer = Players.FirstOrDefault(x => x.CallBluff == true);
                if (bluffer != null)
                {
                    //fun time
                    var msg = $"{bluffer.Name} has chosen to call a bluff.\n";
                    //check that the blocker has a duke
                    if (PlayerCanDoAction(Action.BlockForeignAid, blocker))
                    {
                        //player has a duke!
                        if (blocker.Cards.Count() == 1)
                        {
                            Graveyard.Add(blocker.Cards.First());
                            blocker.Cards.Clear();
                            Send($"{blocker.Name}, your bluff was called.  You are out of cards, and therefore out of the game!");
                        }
                        else
                        {
                            Send(msg + $"{bluffer.Name}, {blocker.Name} had a Duke.  You must pick a card to lose!");
                            //TODO pick card to lose
                            PlayerLoseCard(bluffer);
                        }
                        //blocker's Duke goes back in deck, blocker is given new card
                        var duke = blocker.Cards.First(x => x.Name == "Duke");
                        Cards.Add(duke);
                        blocker.Cards.Remove(duke);
                        Cards.Shuffle();
                        var card = Cards.First();
                        Cards.Remove(card);
                        blocker.Cards.Add(card);
                        Send($"You have lost your Duke.  Your new card is the " + card.Name, blocker.Id, newMsg: true);
                    }
                    else
                    {
                        if (blocker.Cards.Count() == 1)
                        {
                            Graveyard.Add(blocker.Cards.First());
                            blocker.Cards.Clear();
                            Send($"{blocker.Name}, your bluff was called.  You are out of cards, and therefore out of the game!");
                        }
                        else
                        {
                            Send(msg + $"{blocker.Name}, you did not have a Duke! You must pick a card to lose.");
                        }
                    }
                }
                else
                {
                    Send($"{blocker.Name}'s block was not challenged.  {p.Name} does not take any foreign aid.");
                }
            }
            else
            {
                LastMenu = null;
                p.Coins += 2;
                Send($"No one has blocked.  {p.Name} has taken 2 coins.");
            }
        }

        private void PlayerLoseCard(Player p)
        {
            //send menu
            WaitForChoice(ChoiceType.Card);
            Card card;
            if (CardToLose == "")
                card = p.Cards.First();
            else
                card = p.Cards.FirstOrDefault(x => x.Name == CardToLose);

            p.Cards.Remove(card);
            g.Graveyard.Add(card);
        }

        private bool PlayerCanDoAction(Action a, Player p)
        {
            return p.Cards.Any(x => x.ActionsAllowed.Contains(a));
        }

        #region Menus
        public InlineKeyboardMarkup CreateCardMenu(Player p)
        {
            return new InlineKeyboardMarkup(new[] { p.Cards.Select(x => new InlineKeyboardButton(x.Name, $"card|{GameId}|{x.Name}")).ToArray() });
        }
        //public InlineKeyboardMarkup CreateBluffMenu()
        //{
        //    return new InlineKeyboardMarkup(new[]
        //    {
        //        new InlineKeyboardButton("Call Bluff", $"bluff|call|{GameId}"),
        //        new InlineKeyboardButton("Allow", $"bluff|allow|{GameId}")
        //    });
        //}

        public InlineKeyboardMarkup CreateBlockMenu(bool canBlock)
        {
            var choices = new List<InlineKeyboardButton>();

            choices.Add(new InlineKeyboardButton("Call Bluff", $"bluff|call|{GameId}"));
            choices.Add(new InlineKeyboardButton("Allow", $"bluff|allow|{GameId}"));
            if (canBlock)
                choices.Add(new InlineKeyboardButton("Block", $"bluff|block|{GameId}"));
            return new InlineKeyboardMarkup(choices.ToArray());

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
        public InlineKeyboardMarkup CreateTargetMenu(Player p)
        {
            var choices = Players.Where(x => x.Id != p.Id && x.Cards.Count() > 0).Select(x => new[] { new InlineKeyboardButton(x.Name, $"choose|{GameId}|{x.Id}") }).ToArray();
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
