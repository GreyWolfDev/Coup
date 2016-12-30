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
using Database;

namespace CoupForTelegram
{
    public class Game
    {
        public int GameId;
        public int DBGameId;
        public long ChatId;
        public List<Card> Cards = CardHelper.GenerateCards();
        public List<Card> Graveyard = new List<Card>();
        public List<CPlayer> Players = new List<CPlayer>();
        public GameState State = GameState.Joining;
        public int LastMessageId = 0;
        public string LastMessageSent = "";
        public Action ChoiceMade = Action.None;
        public int ChoiceTarget = 0;
        public int Turn = 0;
        public int ActualTurn = 0;
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
                Players.Add(new CPlayer { Id = 32432, Name = "Test" });
                StartGame();
                return 0;
            }
            CPlayer p = null;
            if (!Players.Any(x => x.Id == u.Id))
            {
                p = new CPlayer { Id = u.Id, Name = (u.FirstName + " " + u.LastName).Trim(), TeleUser = u };
                //check for player in database, add if needed
                using (var db = new CoupContext())
                {
                    var pl = db.Players.FirstOrDefault(x => x.TelegramId == p.Id);
                    if (pl == null)
                    {
                        pl = new Player { TelegramId = p.Id, Created = DateTime.UtcNow };
                        db.Players.Add(pl);
                    }
                    pl.Name = p.Name;
                    pl.Username = u.Username;
                    db.SaveChanges();
                }
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
                Send("Not enough players to start the game..");
                State = GameState.Ended;
                return;
            }
            Program.GamesPlayed++;
            State = GameState.Initializing;

            //create db entry for game
            using (var db = new CoupContext())
            {
                var grp = db.ChatGroups.FirstOrDefault(x => x.TelegramId == ChatId);
                var g = new Database.Game { GroupId = grp?.Id, GameType = IsGroup ? "Group" : IsRandom ? "Stranger" : "Friend", TimeStarted = DateTime.Now };
                db.Games.Add(g);
                db.SaveChanges();
                DBGameId = g.Id;
            }

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
                using (var db = new CoupContext())
                {
                    //create db entry
                    var dbp = db.Players.FirstOrDefault(x => x.TelegramId == p.Id);
                    if (dbp == null)
                    {
                        dbp = new Player { TelegramId = p.Id, Created = DateTime.UtcNow };
                        db.Players.Add(dbp);
                        db.SaveChanges();
                    }
                    dbp.Name = p.Name;
                    dbp.Username = p.TeleUser.Username;

                    p.Language = dbp.Language;

                    var gp = new GamePlayer()
                    {
                        GameId = DBGameId,
                        StartingCards = p.Cards.Aggregate("", (a, b) => a + "," + b.Name)
                    };
                    dbp.GamePlayers.Add(gp);
                    db.SaveChanges();
                }
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
                    if (p.Cards.Count() == 0)
                        continue;
                    Turn = p.Id;
                    Send($"{p.GetName()}'s turn. {p.Name.ToBold()} has {p.Coins} coins.", newMsg: true).ToList();
                    p.CallBluff = false;
                    //it is the players turn.
                    //first off, do they have to Coup?
                    if (p.Coins >= 10)
                    {
                        //Coup time!
                        Send($"{p.GetName()} has 10 or more coins, and must Coup someone.");
                        ChoiceMade = Action.Coup;
                    }
                    else
                    {
                        //ok, no, so get actions that they can make
                        Send($"{p.Name.ToBold()} please choose an action.", menu: CreateActionMenu(p), menuTo: p.Id);
                    }
                    var choice = WaitForChoice(ChoiceType.Action);
                    Console.WriteLine($"{p.Name} has chosen to {ChoiceMade}");
                    CPlayer target;
                    CPlayer blocker;
                    CPlayer bluffer;
                    switch (choice)
                    {
                        //DONE
                        case Action.Income:
                            LastMenu = null;
                            Send($"{p.Name.ToBold()} has chosen to take income (1 coin).");
                            DBAddValue(p, Models.ValueType.CoinsCollected);
                            p.Coins++;
                            break;

                        case Action.ForeignAid:
                            if (PlayerMadeBlockableAction(p, choice, cardUsed: "Ambassador"))
                            {
                                LastMenu = null;
                                Send($"{p.Name.ToBold()} was not blocked, and has gained two coins.");
                                DBAddValue(p, Models.ValueType.CoinsCollected, 2);
                                p.Coins += 2;

                            }
                            break;
                        case Action.Coup:
                            p.Coins -= 7;
                            Send($"{p.Name.ToBold()} please choose who to Coup.", menu: CreateTargetMenu(p), menuTo: p.Id);
                            LastMenu = null;
                            WaitForChoice(ChoiceType.Target);
                            target = Players.FirstOrDefault(x => x.Id == ChoiceTarget);
                            DBAddValue(p, Models.ValueType.PlayersCouped);
                            if (target == null) break;
                            if (target.Cards.Count() == 1)
                            {
                                PlayerLoseCard(target, target.Cards.First());
                                //Graveyard.Add(target.Cards.First());
                                target.Cards.Clear();
                                Send($"{target.Name.ToBold()}, you have been couped.  You are out of cards, and therefore out of the game!");
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
                                LastMenu = null;
                                Send($"{p.Name.ToBold()} was not blocked, and has gained three coins.");
                                DBAddValue(p, Models.ValueType.CoinsCollected, 3);
                                p.Coins += 3;
                            }
                            break;
                        case Action.Assassinate:
                            //OH BOY
                            p.Coins -= 3;
                            Send($"{p.Name.ToBold()} has paid 3 coins.  Please choose who to assassinate.", menu: CreateTargetMenu(p), menuTo: p.Id);
                            LastMenu = null;
                            WaitForChoice(ChoiceType.Target);
                            target = Players.FirstOrDefault(x => x.Id == ChoiceTarget);
                            if (target == null) break;
                            if (PlayerMadeBlockableAction(p, choice, target, "Assassin"))
                            {
                                DBAddValue(p, Models.ValueType.PlayersAssassinated);
                                //unblocked!
                                if (target.Cards.Count() == 1)
                                {
                                    PlayerLoseCard(target, target.Cards.First());
                                    //Graveyard.Add(target.Cards.First());
                                    target.Cards.Clear();
                                    Send($"{target.Name.ToBold()}, you have been assassinated.  You are out of cards, and therefore out of the game!");
                                }
                                else
                                {
                                    Send($"{p.Name.ToBold()} was not blocked!  {target.Name.ToBold()} please choose a card to lose.");
                                    PlayerLoseCard(target);
                                }
                            }
                            break;
                        case Action.Exchange:
                            if (PlayerMadeBlockableAction(p, choice))
                            {
                                LastMenu = null;
                                Cards.Shuffle();
                                var count = p.Cards.Count();
                                var newCards = Cards.Take(count).ToList();
                                Cards.AddRange(p.Cards);
                                newCards.AddRange(p.Cards.ToList());
                                p.Cards.Clear();
                                var menu = new InlineKeyboardMarkup(new[]
                                {
                                    newCards.Select(x => new InlineKeyboardButton(x.Name, $"card|{GameId}|{x.Name}")).ToArray()
                                });
                                LastMenu = null;
                                Send($"{p.Name.ToBold()} was not blocked. Please choose your new cards in PM @{Bot.Me.Username}.");
                                Send($"{p.Name}, choose your first card", p.Id, menu: menu, menuTo: p.Id, newMsg: true);
                                WaitForChoice(ChoiceType.Card);
                                var card1 = CardToLose;
                                var card2 = "";
                                CardToLose = "";
                                newCards.Remove(newCards.First(x => x.Name == card1));
                                if (count == 2)
                                {
                                    menu = new InlineKeyboardMarkup(new[]
                                    {
                                        newCards.Select(x => new InlineKeyboardButton(x.Name, $"card|{GameId}|{x.Name}")).ToArray()
                                    });
                                    Send($"{p.Name}, choose your second card", p.Id, menu: menu, menuTo: p.Id);
                                    WaitForChoice(ChoiceType.Card);
                                    card2 = CardToLose;
                                    CardToLose = null;
                                    newCards.Remove(newCards.First(x => x.Name == card2));
                                }
                                var newCard = Cards.First(x => x.Name == card1);
                                Cards.Remove(newCard);
                                p.Cards.Add(newCard);
                                if (count == 2)
                                {
                                    newCard = Cards.First(x => x.Name == card2);
                                    Cards.Remove(newCard);
                                    p.Cards.Add(newCard);
                                }
                                Cards.Shuffle();
                                TellCards(p);
                            }
                            break;
                        case Action.Steal:
                            Send($"{p.Name.ToBold()} please choose who to steal from.", menu: CreateTargetMenu(p), menuTo: p.Id);
                            LastMenu = null;
                            WaitForChoice(ChoiceType.Target);
                            target = Players.FirstOrDefault(x => x.Id == ChoiceTarget);
                            if (PlayerMadeBlockableAction(p, choice, target, "Captain"))
                            {

                                LastMenu = null;
                                var coinsTaken = Math.Min(target.Coins, 2);
                                DBAddValue(p, Models.ValueType.CoinsStolen, coinsTaken);
                                p.Coins += coinsTaken;
                                target.Coins -= coinsTaken;
                                Send($"{p.Name.ToBold()} was not blocked, and has taken {coinsTaken} coins from {target.Name.ToBold()}.");
                            }
                            break;
                        default:
                            break;
                    }

                    //reset all the things
                    ChoiceMade = Action.None;
                    ChoiceTarget = 0;
                    CardToLose = "";
                    foreach (var pl in Players)
                    {
                        pl.CallBluff = null;
                        pl.Block = null;
                    }
                    if (Players.Count(x => x.Cards.Count() > 0) == 1)
                    {
                        //game is over
                        var winner = Players.FirstOrDefault(x => x.Cards.Count() > 0);
                        //set the winner in the database
                        using (var db = new CoupContext())
                        {
                            var gp = GetDBGamePlayer(winner, db);
                            gp.Won = true;
                            gp.EndingCards = winner.Cards.Count() > 1 ? winner.Cards.Aggregate("", (a, b) => a + "," + b.Name) : winner.Cards.First().Name;
                            db.SaveChanges();
                        }
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
        private Action WaitForChoice(ChoiceType type, int timeToChoose = 60)
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
                    return Players.Any(x => x.CallBluff == true || x.Block == true) || Players.All(x => x.CallBluff == false && x.Block == false);
                default:
                    return true;
            }
        }

        private bool PlayerMadeBlockableAction(CPlayer p, Action a, CPlayer t = null, string cardUsed = "")
        {
            //TODO finish this up.
            var aMsg = "";
            CPlayer bluffer = null, blocker = null, bluffBlock = null;
            bool canBlock = false;
            bool canBluff = true;
            switch (a)
            {
                case Action.ForeignAid:
                    aMsg = $"{p.Name.ToBold()} has chosen to take foreign aid (2 coins). Does anyone want to block?";
                    canBluff = false;
                    canBlock = true;
                    break;
                case Action.Assassinate:
                    aMsg = $"{p.Name.ToBold()} has chosen to assassinate {t.Name.ToBold()} [Assassin]. Does anyone want to block / call bluff?";
                    cardUsed = "Assassin";
                    canBlock = true;
                    break;
                case Action.Tax:
                    aMsg = $"{p.Name.ToBold()} has chosen to take tax (3 coins) [Duke]. Does anyone want to call bluff?";
                    cardUsed = "Duke";
                    break;
                case Action.Exchange:
                    aMsg = $"{p.Name.ToBold()} has chosen to exchange cards with the deck [Ambassador]. Does anyone want to call bluff?";
                    cardUsed = "Ambassador";
                    break;
                case Action.Steal:
                    aMsg = $"{p.Name.ToBold()} has chosen to steal from {t.Name.ToBold()} [Captain]. Does anyone want to block / call bluff?";
                    cardUsed = "Captain";
                    canBlock = true;
                    break;
                case Action.BlockSteal:
                    aMsg = $"{p.Name.ToBold()} has chosen to block {t.Name.ToBold()} from stealing [{cardUsed}]. Does anyone want to call bluff?";
                    break;
                case Action.BlockAssassinate:
                    cardUsed = "Contessa";
                    aMsg = $"{p.Name.ToBold()} has chosen to block {t.Name.ToBold()} from assassinating [Contessa]. Does anyone want to call bluff?";
                    break;
                case Action.BlockForeignAid:
                    cardUsed = "Duke";
                    aMsg = $"{p.Name.ToBold()} has chosen to block {t.Name.ToBold()} from taking foreign aid [Duke]. Does anyone want to call bluff?";
                    break;
            }
            Send(aMsg, menu: CreateBlockMenu(p.Id, canBlock, canBluff), menuNot: p.Id);

            foreach (var pl in Players)
            {
                if (canBlock)
                    pl.Block = null;
                else
                    pl.Block = false;

                if (canBluff)
                    pl.CallBluff = null;
                else
                    pl.CallBluff = false;

                if (Turn == pl.Id)
                {
                    pl.CallBluff = pl.Block = false;
                }

            }

            WaitForChoice(ChoiceType.Block);
            Turn = ActualTurn;
            //check to see if anyone called block or bluff
            bluffer = Players.FirstOrDefault(x => x.CallBluff == true);
            if (canBlock)
                blocker = Players.FirstOrDefault(x => x.Block == true);
            if (blocker != null)
            {
                foreach (var pl in Players)
                    pl.Block = null;
                if (a == Action.Steal)
                {
                    //more than one card can block stealing
                    var menu = new InlineKeyboardMarkup(new[] { "Captain", "Ambassador" }.Select(x => new InlineKeyboardButton(x, $"card|{GameId}|{x}")).ToArray());
                    Send($"{blocker.Name.ToBold()} has chosen to block.  Please choose which card you are blocking with", menu: menu);
                    WaitForChoice(ChoiceType.Card);
                    cardUsed = CardToLose;
                    CardToLose = "";
                }
                var blocked = PlayerMadeBlockableAction(blocker, (Action)Enum.Parse(typeof(Action), "Block" + a.ToString(), true), p, cardUsed);
                if (blocked)
                {
                    DBAddValue(blocker, Models.ValueType.ActionsBlocked);
                }
                return !blocked;
            }
            else if (bluffer != null)
            {
                LastMenu = null;
                //fun time
                var msg = $"{bluffer.Name} has chosen to call a bluff.\n";
                //check that the blocker has a duke
                if (PlayerCanDoAction(a, p))
                {
                    //player has a card!
                    if (bluffer.Cards.Count() == 1)
                    {
                        PlayerLoseCard(bluffer, bluffer.Cards.First());
                        //Graveyard.Add(bluffer.Cards.First());
                        bluffer.Cards.Clear();
                        Send($"{bluffer.Name.ToBold()}, {p.Name.ToBold()} had {cardUsed.ToBold()}.  You are out of cards, and therefore out of the game!");
                    }
                    else
                    {
                        Send(msg + $"{bluffer.Name.ToBold()}, {p.Name.ToBold()} had {cardUsed.ToBold()}.  You must pick a card to lose!");
                        //TODO pick card to lose
                        PlayerLoseCard(bluffer);
                    }
                    //players card goes back in deck, given new card
                    var card = p.Cards.First(x => x.Name == cardUsed);
                    Cards.Add(card);
                    p.Cards.Remove(card);
                    Cards.Shuffle();
                    card = Cards.First();
                    Cards.Remove(card);
                    p.Cards.Add(card);
                    Send($"You have lost your {cardUsed}.  Your new card is " + card.Name, p.Id, newMsg: true);
                    return true;
                }
                else
                {
                    if (p.Cards.Count() == 1)
                    {
                        PlayerLoseCard(p, bluffer.Cards.First());
                        //Graveyard.Add(p.Cards.First());
                        p.Cards.Clear();
                        Send($"{p.Name.ToBold()}, your bluff was called.  You are out of cards, and therefore out of the game!");
                    }
                    else
                    {
                        Send(msg + $"{p.Name.ToBold()}, you did not have {cardUsed.ToBold()}! You must pick a card to lose.");
                        PlayerLoseCard(p);
                    }
                    //successful bluff called
                    DBAddBluff(p, cardUsed, true, bluffer);
                    return false;
                }
            }
            else
            {
                if (!PlayerCanDoAction(a, p))
                {
                    //was a successful bluff
                    DBAddBluff(p, cardUsed);
                }
                return true;
            }
        }

        

        private void PlayerLoseCard(CPlayer p, Card card = null)
        {
            //send menu
            if (card == null)
            {
                var menu = new InlineKeyboardMarkup(new[]
                                    {
                                    p.Cards.Select(x => new InlineKeyboardButton(x.Name, $"card|{GameId}|{x.Name}")).ToArray()
                                });
                Send($"Please choose a card to lose", p.Id, menu: menu, newMsg: true, menuTo: p.Id);
                WaitForChoice(ChoiceType.Card);

                if (CardToLose == "")
                    card = p.Cards.First();
                else
                    card = p.Cards.FirstOrDefault(x => x.Name == CardToLose);
            }
            p.Cards.Remove(card);
            Graveyard.Add(card);
            LastMenu = null;
            Send($"{p.Name.ToBold()} has lost {card.Name.ToBold()}.  It is now in the graveyard.");
        }

        private bool PlayerCanDoAction(Action a, CPlayer p)
        {
            return p.Cards.Any(x => x.ActionsAllowed.Contains(a));
        }

        #region Menus
        public InlineKeyboardMarkup CreateCardMenu(CPlayer p)
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

        public InlineKeyboardMarkup CreateBlockMenu(int id, bool canBlock, bool canBluff = true)
        {
            ActualTurn = Turn;
            Turn = id;
            var choices = new List<InlineKeyboardButton>();
            if (canBluff)
                choices.Add(new InlineKeyboardButton("Call Bluff", $"bluff|call|{GameId}"));
            choices.Add(new InlineKeyboardButton("Allow", $"bluff|allow|{GameId}"));
            if (canBlock)
                choices.Add(new InlineKeyboardButton("Block", $"bluff|block|{GameId}"));
            return new InlineKeyboardMarkup(choices.ToArray());

        }
        public InlineKeyboardMarkup CreateActionMenu(CPlayer p)
        {
            //seed choices with all actions
            var choices = Enum.GetValues(typeof(Action)).Cast<Action>().Where(x => !x.ToString().StartsWith("Block") && x != Action.None).ToList();
            //remove actions player doesn't have the coins for
            if (p.Coins < 3) choices.Remove(Action.Assassinate);
            if (p.Coins < 7) choices.Remove(Action.Coup);
            //return choice menu
            return new InlineKeyboardMarkup(choices.Select(x => new[] { new InlineKeyboardButton(x.ToString(), $"{x}|{GameId}") }).ToArray());
        }
        public InlineKeyboardMarkup CreateTargetMenu(CPlayer p)
        {
            var choices = Players.Where(x => x.Id != p.Id && x.Cards.Count() > 0).Select(x => new[] { new InlineKeyboardButton(x.Name, $"choose|{GameId}|{x.Id}") }).ToArray();
            return new InlineKeyboardMarkup(choices);
        }
        #endregion

        #region Communications
        public void TellCards(CPlayer p)
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
                        var newMenu = menu;
                        if (p.Id == menuNot)
                            newMenu = null;
                        if (menuTo != 0 && menuTo != p.Id)
                            newMenu = null;

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

        #region Database

        private void DBAddBluff(CPlayer p, string cardUsed, bool v = false, CPlayer caller = null)
        {
            using (var db = new CoupContext())
            {
                var gp = GetDBGamePlayer(p, db);
                var calledgp = GetDBGamePlayer(caller, db);
                var dbp = GetDBPlayer(p, db);
                int? calledBy = GetDBPlayer(caller, db)?.Id;
                if (!v)
                    gp.BluffsMade++;
                else
                    calledgp.BluffsCalled++;
                var b = new Bluff
                {
                    BluffCalled = v,
                    BlufferId = dbp.Id,
                    CardBluffed = cardUsed,
                    CalledById = calledBy,
                    GameId = DBGameId
                };
                db.Bluffs.Add(b);
                db.SaveChanges();
            }
        }

        private void DBAddValue(CPlayer p, Models.ValueType prop, int val = 1)
        {
            new Task(() =>
            {
                using (var db = new CoupContext())
                {
                    var gp = GetDBGamePlayer(p, db);
                    switch (prop)
                    {
                        case Models.ValueType.CoinsCollected:
                            gp.CoinsCollected += val;
                            break;
                        case Models.ValueType.CoinsStolen:
                            gp.CoinsStolen += val;
                            break;
                        case Models.ValueType.ActionsBlocked:
                            gp.ActionsBlocked += val;
                            break;
                        case Models.ValueType.BluffsCalled:
                            throw new Exception("Use DBAddBluff");
                        case Models.ValueType.PlayersCouped:
                            gp.PlayersCouped += val;
                            break;
                        case Models.ValueType.PlayersAssassinated:
                            gp.PlayersAssassinated += val;
                            break;
                    }
                    db.SaveChanges();
                }
            }).Start();
        }

        private Player GetDBPlayer(CPlayer player, CoupContext db)
        {
            if (player == null)
                return null;
            if (player.DBPlayerId == 0)
            {
                var p = db.Players.FirstOrDefault(x => x.TelegramId == player.Id);
                player.DBPlayerId = p?.Id ?? 0;
                return p;
            }
            try
            {
                return db.Players.Find(player.DBPlayerId);
            }
            catch
            {
                return null;
            }
        }

        private GamePlayer GetDBGamePlayer(Player player)
        {
            return player?.GamePlayers.FirstOrDefault(x => x.GameId == GameId);
        }

        private GamePlayer GetDBGamePlayer(CPlayer player, CoupContext db)
        {
            if (player == null)
                return null;
            if (player.DBGamePlayerId == 0)
            {
                var p = GetDBGamePlayer(GetDBPlayer(player, db));
                player.DBGamePlayerId = p?.Id ?? 0;
                return p;
            }

            try
            {
                return db.GamePlayers.Find(player.DBGamePlayerId);
            }
            catch
            {
                return null;
            }
        }
        #endregion
    }
}
