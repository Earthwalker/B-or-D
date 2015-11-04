//-----------------------------------------------------------------------
// <copyright file="BoardContext.cs" company="Company">
//     Copyright (c) Ethan Vandersaul, Company. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace B_or_d
{
    using System.Data.Entity;

    /// <summary>
    /// Database context.
    /// </summary>
    public class BoardContext : DbContext
    {
        /// <summary>
        /// Gets or sets the users in the database.
        /// </summary>
        /// <value>
        /// The users in the database.
        /// </value>
        public DbSet<User> Users { get; set; }

        /// <summary>
        /// Gets or sets the boards in the database.
        /// </summary>
        /// <value>
        /// The boards in the database.
        /// </value>
        public DbSet<Board> Boards { get; set; }
    }
}
