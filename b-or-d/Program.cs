//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="Company">
//     Copyright (c) Ethan Vandersaul, Company. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace B_or_d
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using MimeKit;
    using SharpConfig;

    /// <summary>
    /// Main program
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Messages to be processed
        /// </summary>
        private static Queue<MimeMessage> messages = new Queue<MimeMessage>();

        /// <summary>
        /// Whether we should have boards use the +something format
        /// </summary>
        private static bool UseAlias = true;

        /// <summary>
        /// Inbox interval loaded from config
        /// </summary>
        private static int inboxInterval = 0;

        /// <summary>
        /// Outbox interval loaded from config
        /// </summary>
        private static int outboxInterval = 0;

        /// <summary>
        /// Random number generator
        /// </summary>
        private static Random rng = new Random();

        /// <summary>
        /// Path  to our config file
        /// </summary>
        private static readonly string configPath = "config.ini";

        /// <summary>
        /// Path  to our adjectives list file
        /// </summary>
        private static readonly string adjectivesListPath = "adjectives.txt";

        /// <summary>
        /// Path  to our nouns list file
        /// </summary>
        private static readonly string nounsListPath = "nouns.txt";

        /// <summary>
        /// Adjectives used in generating user names
        /// </summary>
        public static Collection<string> Adjectives { get; private set; }

        /// <summary>
        /// Nouns used in generating user names
        /// </summary>
        public static Collection<string> Nouns { get; private set; }

        /// <summary>
        /// Context for the underlying database
        /// </summary>
        public static BoardContext Context { get; set; }

        /// <summary>
        /// Inbox to handle receiving messages
        /// </summary>
        public static Inbox Inbox { get; } = new Inbox();

        /// <summary>
        /// Outbox to handle sending messages
        /// </summary>
        public static Outbox Outbox { get; } = new Outbox();

        /// <summary>
        /// UserName of the account to check
        /// </summary>
        public static string UserName { get; set; } // = string.Empty;

        /// <summary>
        /// Host of the mail server
        /// </summary>
        public static string Host { get; set; } // = "localhost";

        /// <summary>
        /// Host pop port to check
        /// </summary>
        public static int PopPort { get; set; } // = 995;

        /// <summary>
        /// Host smtp port to check
        /// </summary>
        public static int SmtpPort { get; set; } // = 465;

        /// <summary>
        /// Password of the account to check
        /// </summary>
        public static string Password { get; set; } // = "password";

        /// <summary>
        /// Whether the program is running
        /// </summary>
        public static bool Running { get; set; } = false;

        /// <summary>
        /// Defines the program entry point. 
        /// </summary>
        public static void Main()
        {
            Console.WriteLine("Welcome to b-or-d");

            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Trace.AutoFlush = true;

            // create new context
            using (Context = new BoardContext())
            {
                // create the database if it doesn't exist
                Context.Database.CreateIfNotExists();

                // load config
                LoadConfig();

                // load word lists
                PopulateWordlists(nounsListPath, adjectivesListPath);

                // start timers for sending and receiving messages
                StartInboxTimer(inboxInterval);
                StartOutboxTimer(outboxInterval);

                Running = true;

                while (Running)
                {
                    switch (Console.ReadLine().ToUpperInvariant())
                    {
                        case "QUIT":
                            Running = false;
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Load config file
        /// </summary>
        /// <param name="fileName">File name</param>
        public static void LoadConfig(string fileName = null)
        {
            if (string.IsNullOrEmpty(fileName))
                fileName = configPath;

            // make sure the config file exists
            if (!File.Exists(fileName))
                throw new FileNotFoundException("fileName");

            // load config file
            var config = Configuration.LoadFromFile(fileName);

            // load server settings
            UserName = config["Server"]["UserName"].GetValue<string>();
            Host = config["Server"]["Host"].GetValue<string>();
            PopPort = config["Server"]["PopPort"].GetValue<int>();
            SmtpPort = config["Server"]["SmtpPort"].GetValue<int>();
            Password = config["Server"]["Password"].GetValue<string>();

            // load inbox setting
            inboxInterval = config["Inbox"]["InboxInterval"].GetValue<int>();

            // load outbox settings
            outboxInterval = config["Outbox"]["OutboxInterval"].GetValue<int>();

            Console.WriteLine("Loaded settings from " + fileName);
        }

        /// <summary>
        /// Loads word lists used for generating user names
        /// </summary>
        /// <param name="adjectivesFileName">File containing adjectives</param>
        /// <param name="nounsFileName">File containing nouns</param>
        public static void PopulateWordlists(string adjectivesFileName, string nounsFileName)
        {
            if (string.IsNullOrEmpty(adjectivesFileName))
                adjectivesFileName = adjectivesListPath;

            if (string.IsNullOrEmpty(nounsFileName))
                nounsFileName = nounsListPath;

            // make sure the adjectives file exists
            if (!File.Exists(adjectivesFileName))
                throw new FileNotFoundException("File containing adjectives", "adjectivesFileName");

            // make sure the nouns file exists
            if (!File.Exists(nounsFileName))
                throw new FileNotFoundException("File containing nouns", "nounsFileName");

            // read from the files
            var adjectiveList = File.ReadAllLines(adjectivesFileName).ToList();
            var nounList = File.ReadAllLines(nounsFileName).ToList();

            // shuffle the lists to give a little bit more randomness
            adjectiveList.Shuffle();
            nounList.Shuffle();

            // put our newly created lists into our collections
            Adjectives = new Collection<string>(adjectiveList);
            Nouns = new Collection<string>(nounList);

            Console.WriteLine("Loaded word lists from " + adjectivesFileName + " and " + nounsFileName);
        }

        /// <summary>
        /// Loads a saved message from a file
        /// </summary>
        /// <param name="messageName">Message name</param>
        /// <returns>Loaded message</returns>
        public static MimeMessage LoadMailMessage(string messageName)
        {
            if (string.IsNullOrWhiteSpace(messageName))
            {
                Trace.TraceError("Message name empty");
                return null;
            }

            if (!File.Exists(messageName))
            {
                Trace.TraceError("File " + messageName + " does not exist");
                return null;
            }

            // load the message
            var message = MimeMessage.Load(messageName);

            // clear to and from fields
            message.From.Clear();
            message.To.Clear();

            // return the loaded message
            return message;
        }

        /// <summary>
        /// Gets the julian day number
        /// </summary>
        /// <param name="date">Date to convert</param>
        /// <returns>Julian day</returns>
        public static int GetDayNumber(DateTime date)
        {
            return (int)(date - new DateTime(1, 1, 1)).TotalDays + 1;
        }

        /// <summary>
        /// Formats the mailbox address
        /// </summary>
        /// <param name="sender">Sender of the mail</param>
        /// <param name="displayName">Name to display</param>
        /// <returns></returns>
        public static MailboxAddress FormatMailboxAddress(string sender, string displayName = null)
        {
            if (string.IsNullOrEmpty(displayName))
                displayName = UserName;

            if (!string.IsNullOrEmpty(sender))
                displayName = sender + " at " + displayName;

            if (string.IsNullOrEmpty(sender))
                return new MailboxAddress(displayName, UserName + '@' + Host);

            if (UseAlias)
                return new MailboxAddress(displayName, UserName + '+' + sender + '@' + Host);
            else
                return new MailboxAddress(displayName, sender + '@' + Host);
        }

        /// <summary>
        /// Gets the sender from and InternetAddress
        /// </summary>
        /// <param name="address">InternetAddress to parse</param>
        /// <returns></returns>
        public static string GetSender(InternetAddress address)
        {
            if (UseAlias)
            {
                var sender = GetAddress(address).Split('@')[0].Split('+');

                if (sender.Length > 1)
                    return sender[1];
                else
                    return sender[0];
            }
            else
                return GetAddress(address).Split('@')[0];
        }

        /// <summary>
        /// Gets the address of an internet address
        /// </summary>
        /// <param name="address">Internet address</param>
        /// <returns>Address of the internet address</returns>
        public static string GetAddress(InternetAddress address)
        {
            return ((MailboxAddress)address).Address;
        }

        /// <summary>
        /// Starts the timer for the inbox to check for messages
        /// </summary>
        /// <param name="intervalSeconds"></param>
        public static void StartInboxTimer(int intervalSeconds)
        {
            // set up inbox events
            Inbox.NewMessagesEvent += Inbox_NewMessagesEvent;

            Inbox.StartTimer(intervalSeconds);
        }


        /// <summary>
        /// Starts the timer for the outbox to empty itself
        /// </summary>
        /// <param name="intervalSeconds"></param>
        public static void StartOutboxTimer(int intervalSeconds)
        {
            Outbox.StartTimer(intervalSeconds);
        }

        /// <summary>
        /// Shuffles a list based off of the Fisher-Yates shuffle
        /// </summary>
        /// <typeparam name="T">List type</typeparam>
        /// <param name="list">List to shuffle</param>
        public static void Shuffle<T>(this List<T> list)
        {
            if (list == null)
                return;

            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        /// <summary>
        /// Save changes to the underlying database
        /// </summary>
        private static void SaveDB()
        {
            // only save if the program is running and not just testing
            if (Running)
                Context.SaveChanges();
        }

        /// <summary>
        /// Handles messages
        /// </summary>
        private static void HandleMessages()
        {
            // loop through each message in the queue
            while (messages.Count > 0)
            {
                // dequeue the message
                var message = messages.Dequeue();

                foreach (var to in message.To)
                {
                    var board = Context.Boards?.Find(GetSender(to));

                    if (board == null)
                    {
                        HandleCommand(message.Subject, GetAddress(message.From[0]));
                        SaveDB();
                    }
                    else
                    {
                        if (board.HandleCommand(message.Subject, GetAddress(message.From[0])))
                            SaveDB();
                        else
                        {
                            if (message.Subject.ToUpperInvariant() == "ADMIN")
                            {
                                // sends the message to the moderators and owners
                                Outbox.ForwardMessage(
                                                      message,
                                                      FormatMailboxAddress(board.Name),
                                                      board.GetUsersOfRole(UserRole.Mod).Select(u => u.MailboxAddress));

                            }
                            else
                            {
                                if (string.IsNullOrEmpty(message.InReplyTo))
                                {
                                    // new post
                                    board.Post(message);
                                    SaveDB();
                                }
                                else
                                {
                                    // check if the user is a mod or owner
                                    if (Context.Users.FirstOrDefault(u => u.Address == message.Sender.Address && u.Board == board)?.Role == UserRole.Mod)
                                    {
                                        // report the user
                                        board.Report(message.Sender.Address);
                                        SaveDB();
                                    }
                                    else
                                    {
                                        // forward to the mods so they can validate the report
                                        Outbox.ForwardMessage(
                                                              message,
                                                              FormatMailboxAddress(board.Name),
                                                              board.GetUsersOfRole(UserRole.Mod).Select(u => u.MailboxAddress));
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handle general commands
        /// </summary>
        /// <param name="command">User command</param>
        /// <param name="userAddress">User address calling the command</param>
        private static void HandleCommand(string command, string userAddress)
        {
            var commands = command.ToUpperInvariant().Split(':');

            switch (commands[0])
            {
                case "JOIN":
                    // receives a list of popular boards and main rules
                    Outbox.SendReply(LoadMailMessage("rules"));
                    // TODO: append a list popular boards to the reply
                    return;
                case "LEAVE":
                    // leaves every board
                    Context.Users.RemoveRange(Context.Users.Where(u => u.Address == userAddress));
                    return;
                case "RULES":
                    // receives the general rules
                    Outbox.SendReply(LoadMailMessage("rules"));
                    return;
                case "BOARDS":
                    // receives a list of boards with the given tags
                    if (commands.Length == 1)
                        break;

                    var tags = commands[1].Split(',');
                    var matches = Context.Boards.Where(b => b.Tags.IsSupersetOf(tags));

                    // create the message
                    var newMessage = new MimeMessage();

                    // set the subject
                    newMessage.Subject = "Boards with tags " + commands[1];

                    // build the body of the message
                    var builder = new BodyBuilder();

                    // TODO: add new lines after each match
                    foreach (var match in matches)
                        builder.TextBody += match.Name + "\r\n";

                    newMessage.Body = builder.ToMessageBody();

                    // send the reply
                    Outbox.SendReply(newMessage);
                    return;
            }

            // receives help
            Outbox.SendReply(LoadMailMessage("help"));
        }

        /// <summary>
        /// New messages event
        /// </summary>
        /// <param name="sender">Inbox that called the event</param>
        /// <param name="e">Event arguments</param>
        private static void Inbox_NewMessagesEvent(object sender, EventArgs e)
        {
            var newMessages = ((Inbox)sender).Messages;
            var newMessagesNum = newMessages.Count;

            // enqueue all the new messages
            for (int i = 0; i < newMessagesNum; i++)
                messages.Enqueue(newMessages.Peek());

            // if the new messages are the only ones in the queue, process them right away
            if (newMessagesNum == messages.Count)
                HandleMessages();
        }
    }
}