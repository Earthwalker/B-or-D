//-----------------------------------------------------------------------
// <copyright file="User.cs" company="Company">
//     Copyright (c) Ethan Vandersaul, Company. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace B_or_d
{
    using System;
    using System.Linq;
    using EmailValidation;
    using MimeKit;

    /// <summary>
    /// User of a board
    /// </summary>
    public class User
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="User"/> class.
        /// </summary>
        public User()
        {
        }

        /// <summary>
        /// Row Id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Email address
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// User role determines the privileges of the user
        /// </summary>
        public UserRole Role { get; set; }

        /// <summary>
        /// The name generated for this user
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Points determine what the user can do
        /// </summary>
        public int Points { get; set; }

        /// <summary>
        /// Automatically set
        /// </summary>
        public DateTime JoinDate { get; set; }

        /// <summary>
        /// Gets our info as a mailbox address
        /// </summary>
        public MailboxAddress MailboxAddress
        {
            get
            {
                return new MailboxAddress(Name, Address);
            }
        }

        /// <summary>
        /// Board that we belong to
        /// </summary>
        public virtual Board Board { get; set; }

        /// <summary>
        /// Initializes required properties
        /// </summary>
        /// <param name="address">mail address</param>
        /// <param name="board">Owner board</param>
        public bool Init(string address, Board board)
        {
            // make sure address is valid
            if (!EmailValidator.Validate(address))
                return false;

            if (board == null)
                return false;

            // user address must be unique in that board
            if (board.Users?.FirstOrDefault(u => u.Address == address) != null)
                return false;

            Address = address;
            Role = board.DefaultUserRole;
            GenerateName();
            Points = 0;
            JoinDate = DateTime.Today;
            Board = board;

            Program.Context.Users.Add(this);
            return true;
        }

        /// <summary>
        /// Generates a new name
        /// </summary>
        public void GenerateName()
        {
            foreach (var adjective in Program.Adjectives)
            {
                foreach (var noun in Program.Nouns)
                {
                    Name = adjective + '_' + noun;

                    if (Program.Context.Users?.FirstOrDefault(u => u.Name == Name) == null)
                    {
                        Console.WriteLine("Generated new user name " + Name);
                        return;
                    }
                }
            }

            throw new NotSupportedException("No available user names");
        }
    }
}
