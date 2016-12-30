using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using Telegram.Bot;
using Telegram.Bot.Args;
using System.Threading;
using CoupForTelegram.Handlers;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using System.Reflection;
using System.IO;

namespace CoupForTelegram
{
    internal static class Bot
    {

        internal static TelegramBotClient Api;
        internal static User Me;
        internal static string RootDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }
        internal static void Initialize()
        {
            var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                        .OpenSubKey("SOFTWARE\\Coup");
#if DEBUG
            var TelegramAPIKey = key.GetValue("DebugAPI").ToString();
#elif RELEASE
            var TelegramAPIKey = key.GetValue("ProductionAPI").ToString();
#endif
            Api = new TelegramBotClient(TelegramAPIKey);
            Api.OnMessage += ApiOnOnMessage;
            Api.OnCallbackQuery += Api_OnCallbackQuery;
            Me = Api.GetMeAsync().Result;
            Api.StartReceiving();

        }

        private static void Api_OnCallbackQuery(object sender, CallbackQueryEventArgs e)
        {
            new Thread(() => { UpdateHandler.HandleCallback(e.CallbackQuery); }).Start();
        }

        private static void ApiOnOnMessage(object sender, MessageEventArgs messageEventArgs)
        {
            switch (messageEventArgs.Message.Type)
            {
                case Telegram.Bot.Types.Enums.MessageType.UnknownMessage:
                    break;
                case Telegram.Bot.Types.Enums.MessageType.TextMessage:
                    new Thread(() => { UpdateHandler.HandleMessage(messageEventArgs.Message); }).Start();
                    break;
                case Telegram.Bot.Types.Enums.MessageType.PhotoMessage:
                    break;
                case Telegram.Bot.Types.Enums.MessageType.AudioMessage:
                    break;
                case Telegram.Bot.Types.Enums.MessageType.VideoMessage:
                    break;
                case Telegram.Bot.Types.Enums.MessageType.VoiceMessage:
                    break;
                case Telegram.Bot.Types.Enums.MessageType.DocumentMessage:
                    break;
                case Telegram.Bot.Types.Enums.MessageType.StickerMessage:
                    break;
                case Telegram.Bot.Types.Enums.MessageType.LocationMessage:
                    break;
                case Telegram.Bot.Types.Enums.MessageType.ContactMessage:
                    break;
                case Telegram.Bot.Types.Enums.MessageType.ServiceMessage:
                    break;
                case Telegram.Bot.Types.Enums.MessageType.VenueMessage:
                    break;
                case Telegram.Bot.Types.Enums.MessageType.GameMessage:
                    break;
            }
        }


        internal static void ReplyToCallback(CallbackQuery query, string text = null, bool edit = true, bool showAlert = false, InlineKeyboardMarkup replyMarkup = null)
        {
            //first answer the callback
            Bot.Api.AnswerCallbackQueryAsync(query.Id, edit ? null : showAlert ? text : null, showAlert, null, 0);
            if (!edit & !showAlert)
            {
                SendAsync(text, query.From.Id, customMenu: replyMarkup);
            }
            //edit the original message
            if (edit)
                Edit(query, text, replyMarkup);
        }

        internal static Task<Message> Edit(CallbackQuery query, string text, InlineKeyboardMarkup replyMarkup = null)
        {
            return Edit(query.Message.Chat.Id, query.Message.MessageId, text, replyMarkup);
        }

        internal static Task<Message> Edit(long id, int msgId, string text, InlineKeyboardMarkup replyMarkup = null)
        {
            //Bot.MessagesSent++;
            return Bot.Api.EditMessageTextAsync(id, msgId, text, replyMarkup: replyMarkup, disableWebPagePreview: true, parseMode: ParseMode.Html);
        }

        internal static async Task<Message> SendAsync(string message, long id, bool clearKeyboard = false, InlineKeyboardMarkup customMenu = null, Game game = null)
        {
            if (clearKeyboard)
            {
                var menu = new ReplyKeyboardRemove() { RemoveKeyboard = true };
                return await Api.SendTextMessageAsync(id, message, replyMarkup: menu, disableWebPagePreview: true, parseMode: ParseMode.Html);
            }
            else if (customMenu != null)
            {
                return await Api.SendTextMessageAsync(id, message, replyMarkup: customMenu, disableWebPagePreview: true, parseMode: ParseMode.Html);
            }
            else
            {
                return await Api.SendTextMessageAsync(id, message, disableWebPagePreview: true, parseMode: ParseMode.Html);
            }
        }
    }
}
