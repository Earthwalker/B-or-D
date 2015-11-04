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
        /// Gets or sets the row identifier.
        /// </summary>
        /// <value>
        /// The row identifier.
        /// </value>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the email address.
        /// </summary>
        /// <value>
        /// The email address.
        /// </value>
        public string Address { get; set; }

        /// <summary>
        /// Gets or sets the role.
        /// </summary>
        /// <value>
        /// The role.
        /// </value>
        public UserRole Role { get; set; }

        /// <summary>
        /// Gets or sets the generated name.
        /// </summary>
        /// <value>
        /// The generated name.
        /// </value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the points which determines what the user can do.
        /// </summary>
        /// <value>
        /// The points.
        /// </value>
        public int Points { get; set; }

        /// <summary>
        /// Gets or sets the join date. Automatically set.
        /// </summary>
        /// <value>
        /// The join date.
        /// </value>
        public DateTime JoinDate { get; set; }

        /// <summary>
        /// Gets our data as a <see cref="MailboxAddress"/>.
        /// </summary>
        /// <value>
        /// Our data as a <see cref="MailboxAddress"/>.
        /// </value>
        public MailboxAddress MailboxAddress
        {
            get
            {
                return new MailboxAddress(Name, Address);
            }
        }

        /// <summary>
        /// Gets or sets the board we belong to.
        /// </summary>
        /// <value>
        /// The board we belong to.
        /// </value>
        public virtual Board Board { get; set; }

        /// <summary>
        /// Initializes required properties.
        /// </summary>
        /// <param name="address">Mail address.</param>
        /// <param name="board">Owner board.</param>
        /// <returns>Whether initialization was successful.</returns>
        public bool Init(string address, Board board)
        {
            // make sure address is valid
            if (!EmailValidator.Validate(address))
                return false;

            // ensure the board exists
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
        /// Generates a new name.
        /// </summary>
        public void GenerateName()
        {
            Random rng = new Random();

            // NOTE: this while loop could pose a problem
            while (true)
            { 
                // set the name to a random adjective and noun
                Name = Program.Adjectives[rng.Next(Program.Adjectives.Count - 1)] + '_' + Program.Nouns[rng.Next(Program.Nouns.Count - 1)];

                // make sure the generated name is not already taken
                if (Program.Context.Users.Local.FirstOrDefault(u => u.Name == Name) == null)
                {
                    Console.WriteLine("Generated new user name " + Name);
                    return;
                }
            }
        }
    }
}
