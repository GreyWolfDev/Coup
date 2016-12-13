using CoupForTelegram.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            //start@coup2bot gameid

            //get the command if any
            var cmd = m.Text.Split(' ')[0];
            cmd = cmd.Replace("@" + Bot.Me.Username, "").Replace("!", "").Replace("/", "");
            if (String.IsNullOrEmpty(cmd)) return;
            Game g;
            //TODO - Move all of these to Commands using reflection
            //basic commands
            switch (cmd)
            {
                case "help":

                    break;
                case "start":
                    //check for gameid
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

                    }
                    break;
                case "join":

                    break;
                case "newgame":
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
                        new InlineKeyboardButton[] { new InlineKeyboardButton("Group game", "sgg") }
                        }));
                    }
                    else
                    {
                        //group game
                        g = CreateGame(m.From, true, chatid: m.Chat.Id);
                    }
                    break;

                case "test":
                    g = new Game(432, m.From, false, false);
                    g.AddPlayer(null);
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
            switch (cmd)
            {
                //TODO: change these to enums with int values
                case "spgf":
                    g = CreateGame(c.From);
                    Bot.ReplyToCallback(c, $"Great! I've created a game for you.  Share this link to invite friends: https://telegram.me/{Bot.Me.Username}?start={g.GameId}");
                    break;
                case "spgs":
                    //check for a game waiting for more players
                    g = Program.Games.FirstOrDefault(x => x.State == GameState.Joining && x.Players.Count() < 6);
                    if (g != null)
                    {
                        var result = g.AddPlayer(c.From);
                        switch (result)
                        {
                            case 1:
                                Bot.ReplyToCallback(c, "You are already in a game!");
                                break;
                            case 0:
                                Bot.ReplyToCallback(c, "You have joined a game!");
                                break;
                        }
                        //TODO: give player list, total count
                        //Bot.ReplyToCallback(c, "You have joined a game!");
                    }
                    else
                    {
                        g = CreateGame(c.From, false, true);
                        Bot.ReplyToCallback(c, $"There were no games available, so I have created a new game for you.  Please wait for others to join!");
                    }
                    Console.WriteLine($"{c.From.FirstName} has joined game: {g.GameId}");
                    break;
                case "sgg":
                    g = CreateGame(c.From, true);
                    Bot.ReplyToCallback(c, $"Great! I've created a game for you.  Click below to send the game to the group!", replyMarkup: new InlineKeyboardMarkup(new[] { new InlineKeyboardButton("Click here") { Url = $"https://telegram.me/{Bot.Me.Username}?startgroup={g.GameId}" } }));
                    break;
                case "join":
                    var id = int.Parse(c.Data.Split('|')[1]);
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
                                Bot.ReplyToCallback(c, "You are already in a game!", false, true);
                                break;
                        }
                    }
                    break;
            }

        }

        private static Game CreateGame(User u, bool group = false, bool random = false, long chatid = 0)
        {
            var g = new Game(GenerateGameId(), u, group, random, chatid);
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
