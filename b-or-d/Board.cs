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
    /// Determines which permissions are granted to users.
    /// </summary>
    public enum UserRole
    {
        /// <summary>
        /// Receives posts.
        /// </summary>
        Guest,

        /// <summary>
        /// Allowed to post content.
        /// </summary>
        Subscriber,

        /// <summary>
        /// Approves posts and removes offending content.
        /// </summary>
        Mod,

        /// <summary>
        /// Oversees the board, while also appointing and removing mods.
        /// </summary>
        Owner,

        /// <summary>
        /// Number of user roles.
        /// </summary>
        Number
    }

    /// <summary>
    /// A place where users can join to create and share posts within the rules the owners have set.
    /// </summary>
    public class Board
    {
        /// <summary>
        /// Holds all message ids (key) waiting to be moderated by the selected mod (value).
        /// </summary>
        private Dictionary<string, string> modHandledMessages = new Dictionary<string, string>();

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
        /// <param name="name">The name of the board.</param>
        public Board(string name)
        {
            Name = name;

            // board name must be unique and not empty
            if (!string.IsNullOrEmpty(name) && Program.Context.Boards.Find(name) == null)
                Program.Context.Boards.Add(this);
        }

        /// <summary>
        /// Gets or sets the name of the board. Used as the key for the database.
        /// </summary>
        /// <value>
        /// The name of the board.
        /// </value>
        [Key]
        public string Name { get; set; }

        /// <summary>
        /// Gets the board tags used for discovery.
        /// </summary>
        /// <value>
        /// The board tags.
        /// </value>
        public HashSet<string> Tags { get; private set; } = new HashSet<string>();

        /// <summary>
        /// Gets the points required to post without verification. -1 for never.
        /// </summary>
        /// <value>
        /// The points.
        /// </value>
        public int Points { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="Board"/> requires owner approval to receive posts.
        /// </summary>
        /// <value>
        /// Whether the board is private.
        /// </value>
        public bool Private { get; set; }

        /// <summary>
        /// Gets the default user role for new members.
        /// </summary>
        /// <value>
        /// The default user role.
        /// </value>
        public UserRole DefaultUserRole { get; private set; } = UserRole.Subscriber;

        /// <summary>
        /// Gets the users of the board.
        /// </summary>
        /// <value>
        /// The users of the board.
        /// </value>
        public virtual ICollection<User> Users { get; private set; }

        /// <summary>
        /// Adds a new user to the board.
        /// </summary>
        /// <param name="address">User address.</param>
        /// <returns>The created user.</returns>
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
        /// Removes a user from the board.
        /// </summary>
        /// <param name="address">User address.</param>
        /// <returns>Whether the user was removed.</returns>
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
        /// Gets a list of users off a given role or higher.
        /// </summary>
        /// <param name="role">Lowest user role to find.</param>
        /// <returns>A list of matching users.</returns>
        public IEnumerable<User> GetUsersOfRole(UserRole role)
        {
            if (role < 0 || role >= UserRole.Number)
                throw new ArgumentOutOfRangeException("role");

            return Users.Where(u => u.Role >= role);
        }

        /// <summary>
        /// Receive a new post.
        /// </summary>
        /// <param name="message">The received message.</param>
        public void Post(MimeMessage message)
        {
            // ensure the message isn't null
            if (message == null)
                return;

            // check if the user is a member
            User user = Users?.FirstOrDefault(u => u.Address == Program.GetAddress(message.From[0]));

            message.ReplyTo.Clear();
            message.ReplyTo.Add(Program.FormatMailboxAddress(user?.Name, Name));

            message.From.Clear();
            message.From.Add(Program.FormatMailboxAddress(user?.Name, Name));

            message.Sender = new MailboxAddress(string.Empty, string.Empty);

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

                            // set the sender to the user's name
                            message.Sender = new MailboxAddress(user.Name, string.Empty);

                            // forward the message
                            Program.Outbox.ForwardMessage(message, Program.FormatMailboxAddress(mod));
                        }
                        else
                        {
                            // send the message out to everyone
                            Program.Outbox.ForwardMessage(message, GetUsersOfRole(UserRole.Guest).Select(u => u.MailboxAddress).ToList());
                        }

                        return;
                    case UserRole.Mod: // unrestricted posting and sometimes on behalf of non-verified users
                        // check if this is a mod-approved message
                        if (message.Sender.Name != user.Name)
                        {
                            var sender = Users.FirstOrDefault(u => u.Name == message.Sender.Name);

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
                            Program.Outbox.ForwardMessage(message, GetUsersOfRole(UserRole.Guest).Select(u => u.MailboxAddress).ToList());
                        }

                        return;
                    case UserRole.Owner:
                        // send the message out to owners
                        Program.Outbox.ForwardMessage(message, GetUsersOfRole(UserRole.Owner).Select(u => u.MailboxAddress).ToList());
                        return;
                }
            }

            // notify the owner that this is a private message and not a post
            message.Subject = "PM: " + message.Subject;

            // send the message out to owners
            Program.Outbox.ForwardMessage(message, GetUsersOfRole(UserRole.Owner).Select(u => u.MailboxAddress).ToList());
        }

        /// <summary>
        /// Reports a user.
        /// </summary>
        /// <param name="address">Address of the user.</param>
        /// <returns>Whether the user was reported.</returns>
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
        /// Handles a message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>Whether the message was handled.</returns>
        public bool HandleMessage(MimeMessage message)
        {
            // ensure message is not null
            if (message == null)
                return false;

            // get the user calling the command
            var user = Users.FirstOrDefault(u => u.Address == Program.GetAddress(message.From[0]));
            var commands = message.Subject.ToUpperInvariant().Split(':');

            switch (commands[0])
            {
                case "JOIN":
                    // joins the selected board
                    AddUser(Program.GetAddress(message.From[0]));

                    // send the rules to the new user
                    Program.Outbox.SendReply(Program.LoadMailMessage(Name + "_rules"));
                    return true;
                case "RULES":
                    // sets the rules if an owner, otherwise receives the board’s rules
                    if (user?.Role == UserRole.Owner)
                        SaveMailMessage(message);
                    else
                        Program.Outbox.SendReply(Program.LoadMailMessage(Name + "_rules"));
                    return true;
                case "ADMIN":
                    // sends the message to the moderators and owners
                    Program.Outbox.ForwardMessage(message, GetUsersOfRole(UserRole.Mod).Select(u => u.MailboxAddress).ToList());

                    return true;
            }

            // these commands require the user to be a member
            if (user != null)
            {
                switch (commands[0])
                {
                    case "LEAVE":
                        // leaves the selected board
                        RemoveUser(user.Address);

                        return true;
                    case "POINTS":
                        // sets the board's point threshold for posting without verification
                        if (commands.Length == 1 || user.Role != UserRole.Owner)
                            return true;

                        int points;
                        if (int.TryParse(commands[1], out points))
                            Points = points;

                        return true;
                    case "DEFAULTROLE":
                        // sets the board's default role
                        if (commands.Length == 1 || user.Role != UserRole.Owner)
                            return true;

                        UserRole newRole;
                        if (Enum.TryParse(commands[1], true, out newRole))
                            DefaultUserRole = newRole;

                        return true;
                    case "TAGS":
                        // sets the board's tags
                        if (commands.Length == 1 || user.Role != UserRole.Owner)
                            return true;

                        Tags = new HashSet<string>(commands[1].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                        return true;
                    case "GUEST":
                    case "SUBSCRIBER":
                    case "MOD":
                    case "OWNER":
                        // makes a user an owner
                        if (commands.Length == 1 || user.Role != UserRole.Owner)
                            return true;

                        var target = Users.FirstOrDefault(u => u.Name.ToUpperInvariant() == commands[1]);

                        if (target != null)
                        {
                            UserRole newRoll;
                            if (Enum.TryParse(commands[0], true, out newRoll))
                                target.Role = newRoll;
                        }

                        return true;
                    default:
                        if (!string.IsNullOrEmpty(message.InReplyTo))
                        {
                            // message is being reported as against the board's rules
                            if (user.Role >= UserRole.Mod)
                            {
                                // report the user
                                Report(message.ResentReplyTo.First().Name);
                            }
                            else
                            {
                                // forward to the mods so they can validate the report
                                Program.Outbox.ForwardMessage(message, GetUsersOfRole(UserRole.Mod).Select(u => u.MailboxAddress).ToList());
                            }

                            return true;
                        }

                        break;
                }
            }

            // new post or private message
            Post(message);

            return false;
        }

        /// <summary>
        /// Gets which mod should handle the next message.
        /// </summary>
        /// <returns>The address of the selected mod.</returns>
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

        /// <summary>
        /// Saves a message to a file.
        /// </summary>
        /// <param name="message">Message to save.</param>
        private void SaveMailMessage(MimeMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.Subject))
            {
                Trace.TraceError("Subject is empty");
                return;
            }

            message.WriteTo(Name + '_' + message.Subject);
        }
    }
}