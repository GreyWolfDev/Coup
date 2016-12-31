using CoupForTelegram.Helpers;
using CoupForTelegram.Models;
using Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
namespace CoupForTelegram.Handlers
{
    public static class UpdateHandler
    {
        internal static void HandleMessage(Message m)
        {
            if (m.Date < Program.StartTime.AddSeconds(-10))
                return;
            //start@coup2bot gameid

            //get the command if any
            var cmd = m.Text.Split(' ')[0];
            cmd = cmd.Replace("@" + Bot.Me.Username, "").Replace("!", "").Replace("/", "").ToLower();
            if (String.IsNullOrEmpty(cmd)) return;
            Game g;
            //TODO - Move all of these to Commands using reflection
            //basic commands
            switch (cmd)
            {
                case "stats":
                    var stats = $"<b>Stats for {m.From.FirstName.FormatHTML()}</b>\n";
                    using (var db = new CoupContext())
                    {
                        var p = db.Players.FirstOrDefault(x => x.TelegramId == m.From.Id);
                        if (p == null) return;
                        var gps = p.GamePlayers.ToList();
                        stats += $"Games played: {gps.Count()}\n" +
                            $"Games won: {gps.Count(x => x.Won)}\n" +
                            $"Checks cards on turn: {gps.Average(x => x.LookedAtCardsTurn)}\n" +
                            $"Total coins collected: {gps.Sum(x => x.CoinsCollected)}\n" +
                            $"Total coins stolen: {gps.Sum(x => x.CoinsStolen)}\n" +
                            $"Successful blocks: {gps.Sum(x => x.ActionsBlocked)}\n" +
                            $"Successful bluffs: {gps.Sum(x => x.BluffsMade)}\n" +
                            $"Bluffs called: {gps.Sum(x => x.BluffsCalled)}\n" +
                            $"Coups made: {gps.Sum(x => x.PlayersCouped)}\n" +
                            $"Assassinations: {gps.Sum(x => x.PlayersAssassinated)}";
                        Bot.SendAsync(stats, m.Chat.Id);
                    }
                    break;
                case "help":
                    Bot.SendAsync("https://www.youtube.com/watch?v=xUNWl5fWfEY", m.Chat.Id);
                    break;
                case "ping":
                    Ping(m);
                    break;
                case "start":
                    if (!m.From.BetaCheck())
                    {
                        Bot.Api.SendTextMessageAsync(m.Chat.Id, "Sorry, but beta testing is full.  Please wait until the next beta extension.", replyToMessageId: m.MessageId);
                        return;
                    }
                    Bot.SendAsync("During the beta, we ask that players join the beta feedback group: https://telegram.me/joinchat/B7EXdEE_fl3Jmsi7TL02_A", m.Chat.Id);
                    //check for gameid
                    //Console.WriteLine(m.From.FirstName + ": " + m.From.Username + ": " + m.From.Id);
                    try
                    {
                        var id = int.Parse(m.Text.Split(' ')[1]);
                        var startgame = Program.Games.FirstOrDefault(x => x.GameId == id);
                        if (startgame != null)
                        {
                            //check if group or PM
                            if (m.Chat.Type == Telegram.Bot.Types.Enums.ChatType.Private)
                            {
                                //private chat - was it sent by the actual person?

                            }
                            else if (m.Chat.Type != Telegram.Bot.Types.Enums.ChatType.Channel)
                            {
                                startgame.ChatId = m.Chat.Id;
                                var menu = new InlineKeyboardMarkup(new[]
                                    {
                                        new InlineKeyboardButton("Join", "join|" + id.ToString()),
                                        new InlineKeyboardButton("Start", "start|" + id.ToString())
                                    });
                                var r = Bot.SendAsync(m.From.FirstName + " wants to play coup.  Up to 6 players total can join.  Click below to join the game!", m.Chat.Id, customMenu: menu
                                    ).Result;
                                startgame.LastMessageId = r.MessageId;
                                startgame.LastMessageSent = r.Text;
                                startgame.LastMenu = menu;

                            }
                        }
                    }
                    catch
                    {
                        //no game / parameter
                        Bot.SendAsync("Well hi there!  Would you like to play a game?\r\nHow about a nice game of tic tac toe?\r\n....Global Thermonuclear War it is.  Just kidding!  Use /newgame to play a game of Coup with your friends, or complete strangers!", m.From.Id);

                    }
                    break;
                case "maint":
                    if (m.From.Id == Bot.Para)
                    {
                        Bot.Maintenance = true;
                        Bot.SendAsync("Maintenance mode enabled, no new games", m.Chat.Id);
                    }
                    break;
                case "newgame":
                    if (Bot.Maintenance)
                    {
                        Bot.SendAsync("Cannot start game, bot is about to restart for patching", m.Chat.Id);
                        return;
                    }
                    if (!m.From.BetaCheck())
                    {
                        Bot.Api.SendTextMessageAsync(m.Chat.Id, "Sorry, but beta testing is full.  Please wait until the next beta extension.", replyToMessageId: m.MessageId);
                        return;
                    }
                    Bot.SendAsync("During the beta, we ask that players join the beta feedback group: https://telegram.me/joinchat/B7EXdEE_fl3Jmsi7TL02_A", m.Chat.Id);
                    //Console.WriteLine(m.From.FirstName + ": " + m.From.Username + ": " + m.From.Id);
                    //check to see if an existing game is already being played.
                    // if group, just look for a group game with the chat id
                    // if PM, look for a game with the user as one of the players (alive)
                    if (!UserCanStartGame(m.From.Id, m.Chat.Id)) return;
                    //all is good?  Ask if PM or Group game (if in PM, otherwise assume group)
                    if (m.Chat.Type == Telegram.Bot.Types.Enums.ChatType.Private)
                    {
                        Bot.Api.SendTextMessageAsync(m.Chat.Id, "You've chosen to start a new game.  Do you want to play in private with friends, private with random players, or in a group?", replyMarkup: new InlineKeyboardMarkup(new InlineKeyboardButton[][] {
                        new InlineKeyboardButton[] { new InlineKeyboardButton("Private game - Friends", "spgf") },
                        new InlineKeyboardButton[] { new InlineKeyboardButton("Private game - Strangers", "spgs") },
                        //new InlineKeyboardButton[] { new InlineKeyboardButton("Group game", "sgg") }
                        }));
                        Thread.Sleep(500);
                        Bot.SendAsync("Alternatively, you can start a game directly in a group", m.Chat.Id);
                    }
                    else
                    {
                        //group game
                        g = CreateGame(m.From, true, c: m.Chat);
                        var menu = new InlineKeyboardMarkup(new[]
                                    {
                                        new InlineKeyboardButton("Join", "join|" + g.GameId.ToString()),
                                        new InlineKeyboardButton("Start", "start|" + g.GameId.ToString())
                                    });
                        var r = Bot.SendAsync(m.From.FirstName + " wants to play coup.  Up to 6 players total can join.  Click below to join the game!", m.Chat.Id, customMenu: menu
                                    ).Result;
                        g.LastMessageId = r.MessageId;
                        g.LastMessageSent = r.Text;
                        g.LastMenu = menu;
                    }
                    break;
                case "leave":
                    //find the game
                    g = Program.Games.FirstOrDefault(x => x.Players.Any(p => p.Id == m.From.Id));
                    var rem = g?.RemovePlayer(m.From);
                    if (rem == 1)
                        Bot.Api.SendTextMessageAsync(m.From.Id, $"You have been remove from game {g.GameId}");
                    break;

                case "test":
                    break;
            }
        }

        internal static void HandleCallback(CallbackQuery c)
        {
            //https://telegram.me/coup2bot?startgroup=gameid
            //https://telegram.me/coup2bot?start=gameid
            var cmd = c.Data;
            if (cmd.Contains("|"))
                cmd = cmd.Split('|')[0];
            Game g;
            int id;
            CPlayer p;

            Models.Action a;
            if (Enum.TryParse(cmd, out a))
            {
                id = int.Parse(c.Data.Split('|')[1]);
                g = Program.Games.FirstOrDefault(x => x.GameId == id);
                if (g != null)
                {
                    if (g.Turn == c.From.Id)
                    {
                        g.ChoiceMade = a;
                        Bot.ReplyToCallback(c, $"Choice accepted: {cmd}");
                    }
                    else
                        Bot.ReplyToCallback(c, "It's not your turn!", false, true);
                }
                else
                    Bot.ReplyToCallback(c, $"That game isn't active anymore...");
            }

            switch (cmd)
            {
                case "players":
                    id = int.Parse(c.Data.Split('|')[1]);
                    g = Program.Games.FirstOrDefault(x => x.GameId == id);
                    if (g == null)
                    {
                        Bot.ReplyToCallback(c, "Game is no longer active!");
                        break;
                    }
                    //player wants status
                    var msg = g.Players.Aggregate("<b>Players</b>", (cur, b) => cur + $"\n{b.GetName()}: {b.Cards.Count} card(s), {b.Coins} coin(s)");
                    Bot.ReplyToCallback(c, msg, false, false);
                    break;
                case "grave":
                    id = int.Parse(c.Data.Split('|')[1]);
                    g = Program.Games.FirstOrDefault(x => x.GameId == id);
                    if (g == null)
                    {
                        Bot.ReplyToCallback(c, "Game is no longer active!");
                        break;
                    }
                    //player wants graveyard
                    var grave = g.Graveyard.Aggregate("<b>Graveyard</b>", (cur, b) => cur + $"\n{b.Name}");
                    Bot.ReplyToCallback(c, grave, false, false);
                    break;
                case "cards":
                    id = int.Parse(c.Data.Split('|')[1]);
                    g = Program.Games.FirstOrDefault(x => x.GameId == id);
                    if (g == null)
                    {
                        Bot.ReplyToCallback(c, "Game is no longer active!");
                        break;
                    }
                    //player wants graveyard
                    var player = g.Players.FirstOrDefault(x => x.Id == c.From.Id);

                    var cards = player?.Cards.Aggregate("<b>Your cards</b>", (cur, b) => cur + $"\n{b.Name}");
                    if (!player.HasCheckedCards)
                    {
                        using (var db = new CoupContext())
                        {
                            var dbp = db.Players.FirstOrDefault(x => x.TelegramId == c.From.Id);
                            var gp = dbp?.GamePlayers.FirstOrDefault(x => x.GameId == g.DBGameId);
                            if (gp != null)
                            {
                                gp.LookedAtCardsTurn = Math.Max(g.Round, 1);
                                db.SaveChanges();
                            }
                        }
                        player.HasCheckedCards = true;
                    }
                    Bot.ReplyToCallback(c, cards, false, false);
                    break;
                case "concede":
                    id = int.Parse(c.Data.Split('|')[1]);
                    g = Program.Games.FirstOrDefault(x => x.GameId == id);
                    if (g == null)
                    {
                        Bot.ReplyToCallback(c, "Game is no longer active!");
                        break;
                    }
                    if (g.Turn != c.From.Id)
                    {
                        Bot.ReplyToCallback(c, "Please concede on your turn only", false, false);
                        break;
                    }
                    g.Concede();
                    Bot.ReplyToCallback(c, "Accepted");
                    break;
                case "card":
                    //player is losing a card
                    var cardStr = c.Data.Split('|')[2];

                    id = int.Parse(c.Data.Split('|')[1]);
                    g = Program.Games.FirstOrDefault(x => x.GameId == id);
                    if (g != null)
                    {
                        p = g.Players.FirstOrDefault(x => x.Id == c.From.Id);
                        if (p != null)
                        {
                            g.CardToLose = cardStr;
                            Bot.ReplyToCallback(c, "Choice Accepted - " + cardStr);
                        }
                    }
                    break;
                case "bluff":
                    bool call = c.Data.Split('|')[1] == "call";
                    id = int.Parse(c.Data.Split('|')[2]);
                    g = Program.Games.FirstOrDefault(x => x.GameId == id);
                    if (g != null)
                    {
                        //get the player
                        p = g.Players.FirstOrDefault(x => x.Id == c.From.Id);
                        if (p != null && p.Cards.Count() > 0)
                        {
                            if (p.Id == g.Turn)
                            {
                                Bot.ReplyToCallback(c, "You can't block yourself!", false, true);
                            }
                            else
                            {
                                switch (c.Data.Split('|')[1])
                                {
                                    case "call":
                                        p.CallBluff = true;
                                        p.Block = false;
                                        break;
                                    case "allow":
                                        p.Block = p.CallBluff = false;
                                        break;
                                    case "block":
                                        p.Block = true;
                                        p.CallBluff = false;
                                        break;
                                }

                                Bot.ReplyToCallback(c, "Choice accepted", false, true);
                            }
                        }
                        else
                        {
                            Bot.ReplyToCallback(c, "You aren't in the game!", false, true);
                        }

                    }
                    break;
                case "choose":
                    //picking a target
                    var target = int.Parse(c.Data.Split('|')[2]);
                    id = int.Parse(c.Data.Split('|')[1]);
                    g = Program.Games.FirstOrDefault(x => x.GameId == id);


                    if (g != null)
                    {
                        //get the player
                        p = g.Players.FirstOrDefault(x => x.Id == c.From.Id);
                        if (p != null && p.Cards.Count() > 0)
                        {
                            if (g.Turn != c.From.Id)
                            {
                                Bot.ReplyToCallback(c, "It's not your choice!", false, true);
                            }
                            else
                            {
                                g.ChoiceTarget = target;
                                Bot.ReplyToCallback(c, "Choice accepted", false, true);
                            }
                        }
                        else
                        {
                            Bot.ReplyToCallback(c, "You aren't in the game!", false, true);
                        }

                    }
                    break;
                //TODO: change these to enums with int values
                case "spgf":
                    g = CreateGame(c.From);
                    Bot.ReplyToCallback(c, $"Great! I've created a game for you.  Share this link to invite friends: https://telegram.me/{Bot.Me.Username}?start={g.GameId}");
                    break;
                case "spgs":
                    //check for a game waiting for more players
                    if (Program.Games.Any(x => x.Players.Any(pl => pl.Id == c.From.Id)))
                        Bot.ReplyToCallback(c, "You are already in a game!");

                    g = Program.Games.FirstOrDefault(x => x.State == GameState.Joining && x.Players.Count() < 6);
                    if (g != null)
                    {
                        var result = g.AddPlayer(c.From);
                        switch (result)
                        {
                            case 1:
                                Bot.ReplyToCallback(c, "You are already in the game!");
                                break;
                            case 0:
                                Bot.ReplyToCallback(c, "You have joined the game!");
                                break;
                        }
                        //TODO: give player list, total count
                        //Bot.ReplyToCallback(c, "You have joined a game!");
                    }
                    else
                    {
                        g = CreateGame(c.From, false, true);
                        Bot.ReplyToCallback(c, $"There were no games available, so I have created a new game for you.  Please wait for others to join!", replyMarkup: new InlineKeyboardMarkup(new[] { new InlineKeyboardButton("Start Game", $"start|{g.GameId}") }));
                    }
                    //Console.WriteLine($"{c.From.FirstName} has joined game: {g.GameId}");
                    break;
                case "sgg":
                    if (Program.Games.Any(x => x.Players.Any(pl => pl.Id == c.From.Id)))
                        Bot.ReplyToCallback(c, "You are already in a game!");
                    g = CreateGame(c.From, true);
                    Bot.ReplyToCallback(c, $"Great! I've created a game for you.  Click below to send the game to the group!", replyMarkup: new InlineKeyboardMarkup(new[] { new InlineKeyboardButton("Click here") { Url = $"https://telegram.me/{Bot.Me.Username}?startgroup={g.GameId}" } }));
                    break;
                case "join":
                    if (!c.From.BetaCheck())
                    {
                        Bot.ReplyToCallback(c, "Sorry, but beta testing is full.  Please wait until the next beta extension.", false, true);
                        return;
                    }
                    id = int.Parse(c.Data.Split('|')[1]);
                    g = Program.Games.FirstOrDefault(x => x.GameId == id);
                    if (g != null)
                    {
                        var success = g.AddPlayer(c.From);
                        switch (success)
                        {
                            case 0:
                                Bot.ReplyToCallback(c, "You have joined a game!", false, true);
                                break;
                            case 1:
                                Bot.ReplyToCallback(c, "You are already in the game!", false, true);
                                break;
                        }
                    }
                    break;
                case "start":
                    id = int.Parse(c.Data.Split('|')[1]);
                    g = Program.Games.FirstOrDefault(x => x.GameId == id);
                    g?.StartGame();
                    break;

            }

        }

        public static void Ping(Message m)
        {
            var ts = DateTime.UtcNow - m.Date;
            var send = DateTime.UtcNow;
            var message = $"Time to receive ping message: {ts:mm\\:ss\\.ff}";
            var result = Bot.SendAsync(message, m.Chat.Id).Result;
            ts = DateTime.UtcNow - send;
            message += $"\nTime to send ping message: {ts:mm\\:ss\\.ff}";
            Bot.Api.EditMessageTextAsync(m.Chat.Id, result.MessageId, message);
        }

        private static Game CreateGame(User u, bool group = false, bool random = false, Chat c = null)
        {
            var g = new Game(GenerateGameId(), u, group, random, c);
            Program.Games.Add(g);
            return g;
        }

        private static int GenerateGameId()
        {
            var result = 0;
            do
            {
                result = Program.R.Next(10000, 10000000);
            } while (Program.Games.Any(x => x.GameId == result));
            return result;
        }


        private static bool UserCanStartGame(int userid, long chatid)
        {
            return !(Program.Games.Any(x => x.ChatId == chatid || x.Players.Any(p => p.Id == userid)));
        }
    }
}
