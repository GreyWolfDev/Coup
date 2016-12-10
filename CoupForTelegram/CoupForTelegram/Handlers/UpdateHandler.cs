using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CoupForTelegram.Handlers
{
    public static class UpdateHandler
    {
        internal static void HandleMessage(Message m)
        {
            //get the command if any
            var cmd = m.Text.Split(' ')[0];
            cmd = cmd.Replace("@" + Bot.Me.Username, "").Replace("!", "").Replace("/", "");
            if (String.IsNullOrEmpty(cmd)) return;
            //TODO - Move all of these to Commands using reflection
            //basic commands
            switch (cmd)
            {
                case "help":
                    
                    break;
                case "start":
                    
                    break;
                case "join":

                    break;
                case "newgame":
                    //check to see if an existing game is already being played.
                    // if group, just look for a group game with the chat id
                    // if PM, look for a game with the user as one of the players (alive)
                    if (!UserCanStartGame(m.From.Id, m.Chat.Id)) return;
                    //all is good?  Ask if PM or Group game (if in PM, otherwise assume group)
                    Bot.Api.SendTextMessageAsync(m.Chat.Id, "You've chosen to start a new game.  Do you want to play in private with friends, private with random players, or in a group?", replyMarkup: new InlineKeyboardMarkup(new InlineKeyboardButton[][] {
                        new InlineKeyboardButton[] { new InlineKeyboardButton("Private game - Friends", "spgf") },
                        new InlineKeyboardButton[] { new InlineKeyboardButton("Private game - Strangers", "spgs") },
                        new InlineKeyboardButton[] { new InlineKeyboardButton("Group game", "sgg") }
                    }));
                    break;

                case "test":
                    var g = new Game(432, m.From);
                    g.AddPlayer(null);
                    break;
            }
        }

        internal static void HandleCallback(CallbackQuery c)
        {
            //https://telegram.me/coup2bot?startgroup=gameid
            //https://telegram.me/coup2bot?start=gameid

            switch (c.Data)
            {
                case "spgf":
                    Bot.ReplyToCallback(c, "Great! I've created a game for you.  Click below to invite your friends", replyMarkup: new InlineKeyboardMarkup(new[] { new InlineKeyboardButton("Invite") { SwitchInlineQuery = "randomid" } }));
                    break;
                case "spgs":
                    break;
                case "sgg":
                    break;
            }

        }

        private static bool UserCanStartGame(int userid, long chatid)
        {
            return true;
        }
    }
}
