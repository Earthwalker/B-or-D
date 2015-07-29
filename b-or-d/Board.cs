//-----------------------------------------------------------------------
// <copyright file="Board.cs" company="Company">
//     Copyright (c) Ethan Vandersaul, Company. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace B_or_d
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Diagnostics;
    using System.Linq;
    using EmailValidation;
    using MimeKit;

    /// <summary>
    /// Determines which permissions are granted to users
    /// </summary>
    public enum UserRole
    {
        /// <summary>
        /// Receives posts
        /// </summary>
        Guest,

        /// <summary>
        /// Allowed to post content
        /// </summary>
        Subscriber,

        /// <summary>
        /// Approves posts and removes offending content
        /// </summary>
        Mod,

        /// <summary>
        /// Oversees the board, while also appointing and removing mods
        /// </summary>
        Owner,

        /// <summary>
        /// Number of user roles
        /// </summary>
        Number
    }

    /// <summary>
    /// A place where users can join to create and share posts within the rules the owners have set
    /// </summary>
    public class Board
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Board"/> class.
        /// </summary>
        [Obsolete("Only needed for serialization and materialization", true)]
        public Board()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Board"/> class.
        /// </summary>
        /// <param name="name">Name of the board</param>
        public Board(string name)
        {
            Name = name;

            // board name must be not empty and unique
            if (!string.IsNullOrEmpty(name) && Program.Context.Boards.Find(name) == null)
                Program.Context.Boards.Add(this);
        }

        /// <summary>
        /// Name of the board
        /// </summary>
        [Key]
        public string Name { get; set; }

        /// <summary>
        /// Board tags used for discovery
        /// </summary>
        public HashSet<string> Tags { get; private set; } = new HashSet<string>();

        /// <summary>
        /// Points required to post without verification. -1 for never.
        /// </summary>
        public int Points { get; private set; }

        /// <summary>
        /// Whether this board requires owner approval to receive posts
        /// </summary>
        public bool Private { get; set; }

        /// <summary>
        /// Default user role
        /// </summary>
        public UserRole DefaultUserRole { get; private set; } = UserRole.Subscriber;

        /// <summary>
        /// Users of the board
        /// </summary>
        public virtual ICollection<User> Users { get; private set; }

        /// <summary>
        /// Holds all message ids (key) waiting to be moderated by the selected mod (value)
        /// </summary>
        private Dictionary<string, string> modHandledMessages = new Dictionary<string, string>();

        /// <summary>
        /// Adds a new user to the board
        /// </summary>
        /// <param name="address">User address</param>
        /// <returns>Created user</returns>
        public User AddUser(string address)
        {
            // make sure address is valid
            if (!EmailValidator.Validate(address))
                return null;

            if (Users?.FirstOrDefault(u => u.Address == address) != null)
            {
                Trace.TraceError("User " + address + " already exists in board " + Name);
                return null;
            }

            var user = Program.Context.Users.Create();

            if (user.Init(address, this))
            {
                Console.WriteLine("Added user " + address + " to board " + Name);
                return user;
            }
            else
                Console.WriteLine("Failed to add user " + address + " to board " + Name);

            return null;
        }

        /// <summary>
        /// Removes a user from the board
        /// </summary>
        /// <param name="address">>User address</param>
        /// <returns>Whether the user was removed</returns>
        public bool RemoveUser(string address)
        {
            // make sure address is valid
            if (!EmailValidator.Validate(address))
                return false;

            var user = Users.FirstOrDefault(u => u.Address == address);

            if (user == null)
            {
                Console.WriteLine("Failed to remove non-member " + address);
                return false;
            }

            if (user.Role == UserRole.Owner)
            {
                // check if there are any other owners
                if (user.Board.GetUsersOfRole(UserRole.Owner).Count() == 1)
                {
                    // check for any mods we can upgrade to owner
                    var mods = user.Board.GetUsersOfRole(UserRole.Mod);

                    if (mods.Count() > 0)
                    {
                        foreach (var mod in mods)
                            mod.Role = UserRole.Owner;
                    }
                    else
                    {
                        // delete the board
                        Console.WriteLine("Removed board " + user.Board.Name);
                        Program.Context.Boards.Remove(user.Board);

                        // TODO: notify users that the board has been shut down
                    }
                }

                // TODO: notify board members that an owner has left the board
            }
            Program.Context.Users.Remove(user);

            Console.WriteLine("Removed user " + address + " from board " + Name);

            return true;
        }

        /// <summary>
        /// Gets a list of users off a given role or higher
        /// </summary>
        /// <param name="role">User roles to find</param>
        /// <returns>List of matching users</returns>
        public IEnumerable<User> GetUsersOfRole(UserRole role)
        {
            if (role < 0 || role >= UserRole.Number)
                throw new ArgumentOutOfRangeException("role");

            return Users.Where(u => u.Role >= role);
        }

        /// <summary>
        /// New post
        /// </summary>
        /// <param name="message">Message to post</param>
        public void Post(MimeMessage message)
        {
            // check if the user is a member
            User user = Users?.FirstOrDefault(u => u.Address == Program.GetAddress(message.From[0]));

            message.ReplyTo.Clear();
            message.ReplyTo.Add(Program.FormatMailboxAddress(user.Name, Name));

            message.From.Clear();
            message.From.Add(Program.FormatMailboxAddress(user.Name, Name));

            if (user != null)
            {
                switch (user.Role)
                {
                    case UserRole.Guest:
                        // guests can't post
                        break;
                    case UserRole.Subscriber: // users with not enough points must be verified by a mod
                        // limited users' posts need to be mod approved   
                        if (user.Points < Points)
                        {
                            // forward the message to the appropriate mod so it can be verified

                            // get a mod to handle this message
                            var mod = GetNextMod();

                            // record who is handling this for future reference
                            modHandledMessages.Add(message.MessageId, mod);

                            // forward the message
                            Program.Outbox.ForwardMessage(message, Program.FormatMailboxAddress(mod));
                        }
                        else
                        {
                            // send the message out to everyone
                            Program.Outbox.ForwardMessage(message,
                                                          Program.FormatMailboxAddress(Name),
                                                          GetUsersOfRole(UserRole.Guest).Select(u => u.MailboxAddress));
                        }
                        return;
                    case UserRole.Mod: // unrestricted posting and sometimes on behalf of non-verified users
                        // check if this is a mod-approved message
                        if (message.Sender.Address != user.Address)
                        {
                            var sender = Users.FirstOrDefault(u => u.Address == message.Sender.Address);

                            if (sender != null)
                            {
                                // check if this is a forwarded message
                                if (string.IsNullOrEmpty(message.InReplyTo))
                                {
                                    // forwarded messages mean the message has been approved
                                    if (sender.Points < Points)
                                        sender.Points++;
                                }
                                else
                                {
                                    // replied messages mean the message has not been approved
                                    if (sender.Points > 0)
                                        sender.Points--;
                                }

                                // we no longer need to keep track of this message
                                modHandledMessages.Remove(message.MessageId);
                            }
                            else
                                Trace.TraceError("Sender " + message.Sender.Address + " does not exist on this board");
                        }
                        else
                        {
                            // send the message out to everyone
                            Program.Outbox.ForwardMessage(message,
                                                          Program.FormatMailboxAddress(Name),
                                                          GetUsersOfRole(UserRole.Guest).Select(u => u.MailboxAddress));
                        }

                        return;
                    case UserRole.Owner:
                        // send the message out to owners
                        Program.Outbox.ForwardMessage(
                                                      message,
                                                      Program.FormatMailboxAddress(Name),
                                                      GetUsersOfRole(UserRole.Owner).Select(u => u.MailboxAddress));
                        return;
                }
            }

            // notify the owner that this is a private message and not a post
            message.Subject = "PM: " + message.Subject;

            // send the message out to owners
            Program.Outbox.ForwardMessage(
                                          message,
                                          Program.FormatMailboxAddress(Name),
                                          GetUsersOfRole(UserRole.Owner).Select(u => u.MailboxAddress));
        }

        /// <summary>
        /// Reports a user
        /// </summary>
        /// <param name="address">Address of the user</param>
        /// <returns>Whether the user was reported</returns>
        public bool Report(string address)
        {
            // make sure address is valid
            if (!EmailValidator.Validate(address))
                return false;

            // make sure the user is a member  of the board
            var user = Users.FirstOrDefault(u => u.Address == address);

            if (user != null)
            {
                if (user.Points > 0)
                    user.Points--;
                return true;
            }
            else
                Trace.TraceError("User not found");

            return false;
        }

        /// <summary>
        /// Handles commands
        /// </summary>
        /// <param name="command">User command</param>
        /// <param name="userAddress">User address calling the command</param>
        /// <returns>Whether the command was handled</returns>
        public bool HandleCommand(string command, string userAddress)
        {
            // ensure command and userAddress are not empty or null
            if (string.IsNullOrWhiteSpace(command) || string.IsNullOrWhiteSpace(userAddress))
                return false;

            var user = Users.FirstOrDefault(u => u.Address == userAddress);
            var commandArray = command.Split(':');

            if (!HandleGeneralCommand(commandArray[0], userAddress) && user != null)
            {
                if (HandleNonOwnerCommand(command, user))
                    return true;
                if (user.Role == UserRole.Owner)
                {
                    if (HandleOwnerCommand(commandArray))
                        return true;
                }
            }
            else
                return true;

            return false;
        }

        /// <summary>
        /// Handles general commands
        /// </summary>
        /// <param name="command">User command</param>
        /// <param name="userAddress">User address calling the command</param>
        /// <returns>Whether the command was handled</returns>
        private bool HandleGeneralCommand(string command, string userAddress)
        {
            switch (command.ToUpperInvariant())
            {
                case "JOIN":
                    // joins the selected board
                    AddUser(userAddress);

                    // send the reply message
                    Program.Outbox.SendReply(Program.LoadMailMessage(Name + "_rules"));
                    return true;
                case "RULES":
                    // receives the board’s rules
                    Program.Outbox.SendReply(Program.LoadMailMessage(Name + "_rules"));
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Handles non-owner commands
        /// </summary>
        /// <param name="command">User command</param>
        /// <param name="user">User calling the command</param>
        /// <returns>Whether the command was handled</returns>
        private bool HandleNonOwnerCommand(string command, User user)
        {
            switch (command.ToUpperInvariant())
            {
                case "LEAVE":
                    // leaves the selected board
                    RemoveUser(user.Address);

                    return true;
            }

            return false;
        }

        /// <summary>
        /// Handles owner commands
        /// </summary>
        /// <param name="commandArray">User commands</param>
        /// <returns>Whether the command was handled</returns>
        private bool HandleOwnerCommand(string[] commandArray)
        {
            if (commandArray.Length == 1)
                return false;

            switch (commandArray[0].ToUpperInvariant())
            {
                case "POINTS":
                    // sets the board's point threshold for posting without verification
                    int points;
                    if (int.TryParse(commandArray[1], out points))
                    {
                        Points = points;
                        return true;
                    }
                    return false;
                case "DEFAULTROLE":
                    // sets the board's default role
                    UserRole role;
                    if (Enum.TryParse(commandArray[1], true, out role))
                    {
                        DefaultUserRole = role;
                        return true;
                    }
                    return false;
                case "TAGS":
                    // sets the board's tags
                    Tags = new HashSet<string>(commandArray[1].Split(','));
                    return true;
                case "GUEST":
                case "SUBSCRIBER":
                case "MOD":
                case "OWNER":
                    // makes a user an owner
                    var target = Users.FirstOrDefault(u => u.Name == commandArray[1]);

                    if (target != null)
                    {
                        UserRole newRoll;
                        if (Enum.TryParse(commandArray[1], true, out newRoll))
                        {
                            target.Role = newRoll;
                            return true;
                        }
                    }

                    return false;
            }

            return false;
        }

        /// <summary>
        /// Saves a message to a file
        /// </summary>
        /// <param name="message">Message to save</param>
        private void SaveMailMessage(MimeMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.Subject))
            {
                Trace.TraceError("Subject is empty");
                return;
            }

            message.WriteTo(Name + '_' + message.Subject);
        }

        /// <summary>
        /// Gets which mod should handle the next message
        /// </summary>
        /// <returns>Mod address</returns>
        public string GetNextMod()
        {
            // get all the mods as a list
            var mods = GetUsersOfRole(UserRole.Mod).ToList();

            if (mods.Count == 0)
                throw new MemberAccessException("No mods or owners found");

            // shuffle the list so hopefully mods get more equal work
            mods.Shuffle();

            int count;
            var modMessages = new Dictionary<string, int>();

            foreach (var mod in mods)
            {
                // get how many messages this mod is currently handling
                count = modHandledMessages.Count(m => m.Value == mod.Address);

                // if a mod has no pending messages, select him
                if (count == 0)
                    return mod.Address;

                modMessages.Add(mod.Address, count);
            }

            // sort based on message count
            modMessages.OrderBy(m => m.Value);

            return modMessages.First().Key;
        }
    }
}