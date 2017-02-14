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
        public int Round = 0;
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
        public string CardToLose = null;
        /// <summary>
        /// Is this game for friends only, or strangers
        /// </summary>
        public bool IsRandom = false;

        /// <summary>
        /// Is this game in PM or Group
        /// </summary>
        public bool IsGroup = false;

        public InlineKeyboardMarkup LastMenu { get; internal set; }

        public Game(int id, User u, bool group, bool random, Chat c = null)
        {
            GameId = id;
            ChatId = c?.Id ?? 0;
            IsGroup = group;
            if (IsGroup)
            {
                using (var db = new CoupContext())
                {
                    var grp = db.ChatGroups.FirstOrDefault(x => x.TelegramId == c.Id);
                    if (grp == null)
                    {
                        grp = new ChatGroup
                        {
                            TelegramId = c.Id,
                            Created = DateTime.UtcNow,
                            Language = "English",
                            Name = c.Title,
                            Username = c.Username
                        };
                        db.ChatGroups.Add(grp);

                    }

                    grp.Name = c.Title;
                    grp.Username = c.Username;

                    db.SaveChanges();
                }
            }
            IsRandom = random;
            AddPlayer(u);
            new Task(JoinTimer).Start();
        }

        private void JoinTimer()
        {
            var joinTime = 360; //3 minutes
            while (true)
            {
                joinTime--;
                if (Players.Count >= 6 || joinTime == 0)
                {
                    if (State == GameState.Joining)
                        StartGame();
                    return;
                }
                Thread.Sleep(500);
            }
        }

        public int AddPlayer(User u)
        {
            if (State != GameState.Joining) return 2;
            if (Players.Count > 5)
            {
                return 2; //game is full
            }

            CPlayer p = null;
            if (!Players.Any(x => x.Id == u.Id))
            {
                p = new CPlayer { Id = u.Id, Name = (u.FirstName + " " + u.LastName).Trim(), TeleUser = u };
                //check for player in database, add if needed
                //using (var db = new CoupContext())
                //{
                //    var pl = db.Players.FirstOrDefault(x => x.TelegramId == p.Id);
                //    if (pl == null)
                //    {
                //        pl = new Player { TelegramId = p.Id, Created = DateTime.UtcNow, Language = "English" };
                //        db.Players.Add(pl);
                //    }
                //    pl.Name = p.Name;
                //    pl.Username = u.Username;
                //    db.SaveChanges();
                //}
                try
                {
                    Players.Add(p);
                }
                catch (Exception e)
                {
                    Send($"{p.GetName()} unable to join: {e.Message}");
                }
            }
            else
                return 1;
            var name = p.GetName();
            Send($"", joinMessage: true);

            return 0;
        }

        public int RemovePlayer(User u)
        {
            if (State == GameState.Joining)
            {
                Players.RemoveAll(x => x.TeleUser.Id == u.Id);
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
            if (State != GameState.Joining) return;
            LastMenu = null;
            Send("Game is starting!");
            if (Players.Count < 2)
            {
                Send("Not enough players to start the game..");
                State = GameState.Ended;
                return;
            }
            if (State != GameState.Joining) return;
            Program.GamesPlayed++;
            State = GameState.Initializing;
            Players.Shuffle();
            Players.Shuffle();
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
                        dbp = new Player { TelegramId = p.Id, Created = DateTime.UtcNow, Language = "English", Name = p.Name, Username = p.TeleUser.Username };
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
                SendMenu(p);
                //TellCards(p);
            }
            //DEBUG OUT
#if DEBUG
            //for (int i = 0; i < Players.Count(); i++)
            //{
            //    Console.WriteLine($"Player {i}: {Players[i].Cards[0].Name}, {Players[i].Cards[1].Name}");
            //}
            //Console.WriteLine($"Deck:\n{Cards.Aggregate("", (a, b) => a + "\n" + b.Name)}");
#endif
            State = GameState.Running;

            while (true)
            {
                try
                {
                    Round++;
                    //will use break to end the game
                    foreach (var p in Players)
                    {
                        Cards.Shuffle();
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
                            Send($"{p.Name.ToBold()} please choose an action.", p.Id, menu: CreateActionMenu(p), menuTo: p.Id, newMsg: true);
                            LastMenu = null;
                            if (IsGroup)
                                Send($"{p.Name.ToBold()} please choose an action.");
                        }
                        var choice = WaitForChoice(ChoiceType.Action);
                        Console.WriteLine($"{p.Name} has chosen to {ChoiceMade}");
                        CPlayer target;
                        CPlayer blocker;
                        CPlayer bluffer;
                        IEnumerable<CPlayer> choices;
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
                                if (p.Coins < 10) //else, we have already sent the message "...must Coup someone."
                                    Send($"{p.Name.ToBold()} has chosen to Coup.");
                                p.Coins -= 7;
                                choices = Players.Where(x => x.Id != p.Id && x.Cards.Count() > 0);
                                if (choices.Count() == 1)
                                    target = choices.First();
                                else
                                {
                                    Send($"Please choose who to Coup.", p.Id, menu: CreateTargetMenu(p), menuTo: p.Id);
                                    LastMenu = null;
                                    WaitForChoice(ChoiceType.Target);
                                    target = Players.FirstOrDefault(x => x.Id == ChoiceTarget);
                                }
                                DBAddValue(p, Models.ValueType.PlayersCouped);
                                if (target == null)
                                {
                                    LastMenu = null;
                                    Send($"{p.Name.ToBold()} did not choose in time, and has lost 7 coins");
                                    Send("Time's up!", p.Id, menuTo: p.Id);
                                    break;
                                }

                                if (target.Cards.Count() == 1)
                                {
                                    Send($"{p.Name.ToBold()} has chosen to Coup {target.Name.ToBold()}!");
                                    PlayerLoseCard(target);
                                    //Graveyard.Add(target.Cards.First());
                                    target.Cards.Clear();
                                    LastMenu = null;
                                    Send($"{target.Name.ToBold()}, you have been couped. You are out of cards, and therefore out of the game!");
                                }
                                else
                                {
                                    Send($"{p.Name.ToBold()} has chosen to Coup {target.Name.ToBold()}! {target.Name.ToBold()} please choose a card to lose.");
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
                                Send($"{p.Name.ToBold()} has paid 3 coins to assassinate.");
                                choices = Players.Where(x => x.Id != p.Id && x.Cards.Count() > 0);
                                if (choices.Count() == 1)
                                    target = choices.First();
                                else
                                {
                                    Send($"Please choose who to assassinate.", p.Id, menu: CreateTargetMenu(p), menuTo: p.Id);
                                    LastMenu = null;
                                    WaitForChoice(ChoiceType.Target);
                                    target = Players.FirstOrDefault(x => x.Id == ChoiceTarget);
                                }

                                if (target == null)
                                {
                                    LastMenu = null;
                                    Send("Time's up!", p.Id, menuTo: p.Id);
                                    Send($"{p.Name.ToBold()} did not choose in time, and has lost 3 coins!");
                                    break;
                                }
                                if (PlayerMadeBlockableAction(p, choice, target, "Assassin"))
                                {
                                    DBAddValue(p, Models.ValueType.PlayersAssassinated);
                                    //unblocked!
                                    if (target.Cards.Count() == 1)
                                    {
                                        PlayerLoseCard(target);
                                        Send($"{target.Name.ToBold()}, you have been assassinated. You are out of cards, and therefore out of the game!");
                                        //Graveyard.Add(target.Cards.First());
                                        target.Cards.Clear();
                                    }
                                    else
                                    {
                                        LastMenu = null;
                                        Send($"{p.Name.ToBold()} was not blocked! {target.Name.ToBold()} please choose a card to lose.");
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
                                    Turn = p.Id; //just to be sure
                                    Send($"{p.Name.ToBold()} was not blocked. Please choose your new cards in PM @{Bot.Me.Username}.");
                                    Send($"{p.Name}, choose your first card", p.Id, menu: menu, newMsg: true);
                                    WaitForChoice(ChoiceType.Card);
                                    if (String.IsNullOrEmpty(CardToLose))
                                    {
                                        LastMenu = null;
                                        Send($"Time ran out, moving on");
                                        p.Cards.AddRange(newCards.Take(count));
                                        TellCards(p);
                                        break;
                                    }
                                    var card2 = "";

                                    newCards.Remove(newCards.First(x => x.Name == CardToLose));
                                    var card1 = CardToLose;
                                    CardToLose = null;
                                    if (count == 2)
                                    {
                                        menu = new InlineKeyboardMarkup(new[]
                                        {
                                            newCards.Select(x => new InlineKeyboardButton(x.Name, $"card|{GameId}|{x.Name}")).ToArray()
                                        });
                                        Send($"{p.Name}, choose your second card", p.Id, menu: menu, menuTo: p.Id);
                                        WaitForChoice(ChoiceType.Card);
                                        if (String.IsNullOrEmpty(CardToLose))
                                        {
                                            LastMenu = null;
                                            Send($"Time ran out, moving on");
                                            p.Cards.Add(newCards.First());
                                            TellCards(p);
                                            break;
                                        }
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
                                Send($"{p.Name.ToBold()} has chosen to steal.");
                                choices = Players.Where(x => x.Id != p.Id && x.Cards.Count() > 0);
                                if (choices.Count() == 1)
                                    target = choices.First();
                                else
                                {
                                    Send($"{p.Name.ToBold()} please choose who to steal from.", p.Id, menu: CreateTargetMenu(p), menuTo: p.Id);
                                    LastMenu = null;
                                    WaitForChoice(ChoiceType.Target);
                                    target = Players.FirstOrDefault(x => x.Id == ChoiceTarget);
                                }
                                if (target == null)
                                {
                                    LastMenu = null;
                                    Send("Time's up!", p.Id, menuTo: p.Id);
                                    Send($"{p.Name.ToBold()} did not choose in time");
                                    break;
                                }
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
                            case Action.Concede:
                                LastMenu = null;
                                Send($"{p.Name} has chosen to concede the game, and is out!\n");
                                if (p.Cards.Count() == 1)
                                    Send($"{p.Name}'s card was {p.Cards.First().Name}. It is now in the graveyard.");
                                else
                                    Send($"{p.Name}'s cards were {p.Cards.First().Name} and {p.Cards.Last().Name}. They are now in the graveyard.");
                                Graveyard.AddRange(p.Cards);
                                p.Cards.Clear();
                                break;
                            default:
                                LastMenu = null;
                                Send($"{p.Name.ToBold()} did not do anything, moving on...");
                                p.AfkCount++;
                                if (p.AfkCount >= 2)
                                {
                                    //out!
                                    Send($"{p.Name} is AFK, and is out!");
                                    if (p.Cards.Count() == 1)
                                        Send($"{p.Name}'s card was {p.Cards.First().Name}. It is now in the graveyard.");
                                    else
                                        Send($"{p.Name}'s cards were {p.Cards.First().Name} and {p.Cards.Last().Name}. They are now in the graveyard.");
                                    Graveyard.AddRange(p.Cards);
                                    p.Cards.Clear();
                                }
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

                                var g = db.Games.Find(DBGameId);
                                g.TimeEnded = DateTime.UtcNow;
                                db.SaveChanges();
                            }
                            LastMenu = null;
                            Send($"{winner.GetName()} has won the game!", newMsg: true);
                            State = GameState.Ended;
                            return;
                        }

                    }
                }
                catch (Exception e)
                {
                    Send($"Game error: {e.Message}\n{e.StackTrace}");
                    State = GameState.Ended;
                    return;
                }

            }

        }

        internal void Concede()
        {
            ChoiceMade = Action.Concede;
        }

        private void SendMenu(CPlayer p)
        {
            /*
             * actions: 
             * get status (players, card / coin counts)
             * see graveyard
             * get cards (see what is in your hand)
             * resign from / concede the game
            */
            var menu = new InlineKeyboardMarkup(new[]
            {
                new[] {
                    new InlineKeyboardButton("Game Status",$"players|{GameId}"),
                    new InlineKeyboardButton("Graveyard",$"grave|{GameId}")
                },
                new[]
                {
                    new InlineKeyboardButton("See Cards",$"cards|{GameId}|{p.Id}"),
                    new InlineKeyboardButton("Resign / Concede", $"concede|{GameId}|{p.Id}")
                }
            });
            Send("Options:", p.Id, menu: menu, newMsg: true, specialMenu: true);
        }

        /// <summary>
        /// Sleeps until a choice has been made
        /// </summary>
        /// <param name="timeToChoose">Time in half seconds to choose</param>
        /// <returns>Whether or not a choice was actually made</returns>
        private Action WaitForChoice(ChoiceType type, int Id = 0, bool canBlock = false, bool canBluff = false, int timeToChoose = 60)
        {
            if (type == ChoiceType.Card)
                CardToLose = null;
            var allowed = 0;

            while (!HasChoiceBeenMade(type) && timeToChoose > 0)
            {
                Thread.Sleep(500);
                if (type == ChoiceType.Block)
                {
                    var temp = Players.Count(x => x.Cards.Count() > 0 && x.Block == false && x.CallBluff == false) - 1;
                    if (temp != allowed)
                    {
                        allowed = temp;
                        //group or PM
                        if (IsGroup)
                        {
                            var r = Bot.Edit(ChatId, LastMessageId, LastMessageSent, CreateBlockMenu(Id, canBlock, canBluff, allowed)).Result;
                            LastMessageId = r.MessageId;
                        }
                        else
                        {
                            foreach (var p in Players.Where(x => x.Cards.Count() > 0 && x.Id != Id))
                            {
                                var r = Bot.Edit(p.Id, p.LastMessageId, p.LastMessageSent, CreateBlockMenu(Id, canBlock, canBluff, allowed)).Result;
                                p.LastMessageId = r.MessageId;
                            }
                        }
                    }
                }
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
                    return CardToLose != null;
                case ChoiceType.Block:
                    //check if ANYONE has blocked, so first person to block is the one that has to deal with it
                    return Players.Any(x => x.CallBluff == true || x.Block == true) || Players.Where(x => x.Cards.Count() > 0).All(x => x.CallBluff == false && x.Block == false);
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
                    cardUsed = "💀 Assassin";
                    aMsg = $"{p.Name.ToBold()} has chosen to assassinate {t.Name.ToBold()} [{cardUsed}]. Does anyone want to block / call bluff?";
                    canBlock = true;
                    break;
                case Action.Tax:
                    cardUsed = "💰 Duke";
                    aMsg = $"{p.Name.ToBold()} has chosen to take tax (3 coins) [{cardUsed}]. Does anyone want to call bluff?";
                    break;
                case Action.Exchange:
                    cardUsed = "👳 Ambassador";
                    aMsg = $"{p.Name.ToBold()} has chosen to exchange cards with the deck [{cardUsed}]. Does anyone want to call bluff?";

                    break;
                case Action.Steal:
                    cardUsed = "🛡 Captain";
                    aMsg = $"{p.Name.ToBold()} has chosen to steal from {t.Name.ToBold()} [{cardUsed}]. Does anyone want to block / call bluff?";
                    canBlock = true;
                    break;
                case Action.BlockSteal:
                    aMsg = $"{p.Name.ToBold()} has chosen to block {t.Name.ToBold()} from stealing [{cardUsed}]. Does anyone want to call bluff?";
                    break;
                case Action.BlockAssassinate:
                    cardUsed = "👠 Contessa";
                    aMsg = $"{p.Name.ToBold()} has chosen to block {t.Name.ToBold()} from assassinating [{cardUsed}]. Does anyone want to call bluff?";
                    break;
                case Action.BlockForeignAid:
                    cardUsed = "💰 Duke";
                    aMsg = $"{p.Name.ToBold()} has chosen to block {t.Name.ToBold()} from taking foreign aid [{cardUsed}]. Does anyone want to call bluff?";
                    break;
            }
            Send(aMsg, menu: CreateBlockMenu(p.Id, canBlock, canBluff), menuNot: p.Id);

            foreach (var pl in Players)
            {
                if (canBlock && (pl.Id == t?.Id || a == Action.ForeignAid))
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

            WaitForChoice(ChoiceType.Block, p.Id, canBlock, canBluff);
            Turn = ActualTurn;
            //check to see if anyone called block or bluff
            bluffer = Players.FirstOrDefault(x => x.CallBluff == true);
            if (canBlock)
                blocker = Players.FirstOrDefault(x => x.Block == true);

            var isBluff = false;
            if (a.Equals(Action.BlockSteal) || a.Equals(Action.Steal))
            {
                //check the cardUsed
                isBluff = (p.Cards.All(x => x.Name != cardUsed));
            }
            else
            {
                isBluff = !PlayerCanDoAction(a, p);
            }

            if (blocker != null)
            {
                foreach (var pl in Players)
                    pl.Block = null;
                if (a == Action.Steal)
                {
                    //more than one card can block stealing
                    var menu = new InlineKeyboardMarkup(new[] { "🛡 Captain", "👳 Ambassador" }.Select(x => new InlineKeyboardButton(x, $"card|{GameId}|{x}")).ToArray());
                    ActualTurn = Turn;
                    Turn = blocker.Id;
                    Send($"{blocker.Name.ToBold()} has chosen to block. Please choose which card you are blocking with", menu: menu);
                    WaitForChoice(ChoiceType.Card);
                    Turn = ActualTurn;
                    cardUsed = CardToLose;
                    CardToLose = null;
                    if (cardUsed == null)
                        return true;
                }
                var blocked = PlayerMadeBlockableAction(blocker, (Action)Enum.Parse(typeof(Action), "Block" + a.ToString(), true), p, cardUsed);
                if (blocked)
                {
                    DBAddValue(blocker, Models.ValueType.ActionsBlocked);
                    LastMenu = null;
                    Send($"{blocker.Name.ToBold()} has blocked {p.Name.ToBold()}");
                }
                return !blocked;
            }
            else if (bluffer != null)
            {
                LastMenu = null;
                //fun time
                var msg = $"{bluffer.Name} has chosen to call a bluff.\n";
                
                //if stealing, simply checking is not good enough... 
                if (!isBluff)
                {
                    //player has a card!
                    Send($"{bluffer.Name.ToBold()}, {p.Name.ToBold()} had {cardUsed.ToBold()}!" + ((bluffer.Cards.Count() > 1) ? " You must pick a card to lose." : ""));
                    PlayerLoseCard(bluffer);
                    if (bluffer.Cards.Count() == 0)
                        Send($"{bluffer.Name.ToBold()} is out of cards, so out of the game!");

                    //players card goes back in deck, given new card
                    try
                    {
                        var card = p.Cards.First(x => x.Name == cardUsed);
                        Cards.Add(card);
                        p.Cards.Remove(card);
                        Cards.Shuffle();
                        Cards.Shuffle();
                        Cards.Shuffle();
                        card = Cards.First();
                        Cards.Remove(card);
                        p.Cards.Add(card);
                        Send($"You have lost your {cardUsed}.", p.Id, newMsg: true);
                    }
                    catch (Exception e)
                    {
                        Bot.Api.SendTextMessageAsync(Bot.Para, $"Error in blocking.\n{LastMessageSent}\n\n{e.Message}\n{p.Name}\nCard Used: {cardUsed}\nPlayers cards: {p.Cards.Aggregate("", (current, b) => current + ", " + b.Name)}");
                    }
                    TellCards(p);

                    return true;
                }
                else
                {
                    Send($"{p.Name.ToBold()}, you did not have {cardUsed.ToBold()}!" + ((p.Cards.Count() > 1) ? " You must pick a card to lose." : ""));
                    PlayerLoseCard(p);
                    if (p.Cards.Count() == 0)
                        Send($"{p.Name.ToBold()} is out of cards, so out of the game!");
                    //successful bluff called
                    DBAddBluff(p, cardUsed, true, bluffer);
                    return false;
                }
            }
            else
            {
                if (isBluff)
                {
                    //was a successful bluff
                    DBAddBluff(p, cardUsed);
                }
                return true;
            }
        }



        private void PlayerLoseCard(CPlayer p)
        {
            Card card = null;
            //send menu
            if (p.Cards.Count() == 0)
            {
                Send($"{p.Name.ToBold()} is out of cards, so out of the game!");
                return;
            }
            else if (p.Cards.Count() == 1)
                card = p.Cards.First();
            else
            {
                var menu = new InlineKeyboardMarkup(new[]
                                    {
                                p.Cards.Select(x => new InlineKeyboardButton(x.Name, $"card|{GameId}|{x.Name}")).ToArray()
                            });
                ActualTurn = Turn;
                Turn = p.Id;
                Send($"Please choose a card to lose", p.Id, menu: menu, newMsg: true, menuTo: p.Id);

                WaitForChoice(ChoiceType.Card);
                Turn = ActualTurn;
                if (CardToLose == null)
                {
                    card = p.Cards.First();
                    Send($"I chose for you, you lost {card.Name.ToBold()}", p.Id);
                }
                else
                    card = p.Cards.FirstOrDefault(x => x.Name == CardToLose);
            }
            p.Cards.Remove(card);
            Graveyard.Add(card);
            TellCards(p);
            LastMenu = null;
            Send($"{p.Name.ToBold()} has lost {card.Name.ToBold()}. It is now in the graveyard.");
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

        public InlineKeyboardMarkup CreateBlockMenu(int id, bool canBlock, bool canBluff = true, int allowed = 0)
        {
            ActualTurn = Turn;
            Turn = id;
            var choices = new List<InlineKeyboardButton>();
            choices.Add(new InlineKeyboardButton($"Allow ({allowed})", $"bluff|allow|{GameId}"));
            if (canBluff)
                choices.Add(new InlineKeyboardButton("Call Bluff", $"bluff|call|{GameId}"));
            if (canBlock)
                choices.Add(new InlineKeyboardButton("Block", $"bluff|block|{GameId}"));
            return new InlineKeyboardMarkup(choices.ToArray());

        }
        public InlineKeyboardMarkup CreateActionMenu(CPlayer p)
        {
            var choices = new List<InlineKeyboardButton>();
            //seed choices with all actions
            foreach (var a in Enum.GetValues(typeof(Action)).Cast<Action>())
            {
                var icon = p.HasCheckedCards ? PlayerCanDoAction(a, p) ? "✅ " : "💢 " : "";
                var data = $"{a}|{GameId}";
                switch (a)
                {
                    case Action.Income:
                        choices.Add(new InlineKeyboardButton($"{icon}Income (+1)", data));
                        break;
                    case Action.ForeignAid:
                        choices.Add(new InlineKeyboardButton($"{icon}Foreign Aid (+2)", data));
                        break;
                    case Action.Coup:
                        if (p.Coins >= 7)
                            choices.Add(new InlineKeyboardButton($"{icon}Coup (-7)", data));
                        break;
                    case Action.Tax:
                        choices.Add(new InlineKeyboardButton($"{icon}Tax (+3) 💰", data));
                        break;
                    case Action.Assassinate:
                        if (p.Coins >= 3)
                            choices.Add(new InlineKeyboardButton($"{icon}Assassinate (-3) 💀", data));
                        break;
                    case Action.Exchange:
                        choices.Add(new InlineKeyboardButton($"{icon}Exchange 👳", data));
                        break;
                    case Action.Steal:
                        choices.Add(new InlineKeyboardButton($"{icon}Steal (+2) 🛡", data));
                        break;
                    case Action.BlockSteal:
                        break;
                    case Action.BlockAssassinate:
                        break;
                    case Action.BlockForeignAid:
                        break;
                    case Action.None:
                        break;
                    case Action.Concede:
                        break;
                }
            }
            //var choices = Enum.GetValues(typeof(Action)).Cast<Action>().Where(x => !x.ToString().StartsWith("Block") && x != Action.None && x != Action.Concede).ToList();
            //remove actions player doesn't have the coins for
            //if (p.Coins < 3) choices.Remove(Action.Assassinate);
            //if (p.Coins < 7) choices.Remove(Action.Coup);
            //return choice menu
            return new InlineKeyboardMarkup(choices.Select(x => new[] { x }).ToArray());
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
            if (p.Cards.Count() == 0) return;
            var cards = p?.Cards.Aggregate("<b>Your cards</b>", (cur, b) => cur + $"\n{b.Name}");
            if (!p.HasCheckedCards)
            {
                using (var db = new CoupContext())
                {
                    var dbp = db.Players.FirstOrDefault(x => x.TelegramId == p.Id);
                    var gp = dbp?.GamePlayers.FirstOrDefault(x => x.GameId == DBGameId);
                    if (gp != null)
                    {
                        gp.LookedAtCardsTurn = Math.Max(Round, 1);
                        db.SaveChanges();
                    }
                }
                p.HasCheckedCards = true;
            }
            Send(cards, p.Id, newMsg: true).ToList();
        }
        private List<Message> Send(string message, long id = 0, bool clearKeyboard = false, InlineKeyboardMarkup menu = null, bool newMsg = false, int menuTo = 0, int menuNot = 0, bool specialMenu = false, bool joinMessage = false)
        {
            var result = new List<Message>();
            if (id == 0)
            {
                if (IsGroup)
                    id = ChatId;
                else
                {
                    foreach (var p in Players.ToList())
                    {
                        var newMenu = menu;
                        if (p.Id == menuNot)
                            newMenu = null;
                        if (menuTo != 0 && menuTo != p.Id)
                            newMenu = null;
                        result.AddRange(Send(message, p.Id, clearKeyboard, newMenu, newMsg, joinMessage: joinMessage));
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
                    if (last != 0 && !newMsg && !joinMessage)
                    {
                        message = lastStr + Environment.NewLine + message;
                        r = Bot.Edit(id, last, message, menu ?? LastMenu).Result;
                    }
                    else if (joinMessage)
                    {
                        message = Players.Aggregate(message, (c, v) => c + v.GetName() + " has joined\n");
                        if (IsGroup)
                        {
                            LastMessageSent = "";
                            r = Bot.Edit(ChatId, last, message, LastMenu).Result;
                        }
                        else if (last == 0)
                        {
                            p.LastMessageSent = "";
                            r = Bot.SendAsync(message, id, customMenu: menu ?? LastMenu).Result;
                        }
                        else if (last != 0)
                        {
                            r = Bot.Edit(id, last, message, customMenu: menu ?? LastMenu).Result;
                        }
                    }
                    else
                    {
                        r = Bot.SendAsync(message, id, clearKeyboard, menu, game: this).Result;
                        if (!specialMenu)
                            LastMenu = menu;
                    }
                    if (p != null && !specialMenu)
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
            return player?.GamePlayers.FirstOrDefault(x => x.GameId == DBGameId);
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
