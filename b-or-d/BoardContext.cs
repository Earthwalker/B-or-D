//-----------------------------------------------------------------------
// <copyright file="BoardContext.cs" company="Company">
//     Copyright (c) Ethan Vandersaul, Company. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System.Data.Entity;

namespace B_or_d
{
    /// <summary>
    /// Database context
    /// </summary>
    public class BoardContext : DbContext
    {
        /// <summary>
        /// Users in the database
        /// </summary>
        public DbSet<User> Users { get; set; }

        /// <summary>
        /// Boards in the database
        /// </summary>
        public DbSet<Board> Boards { get; set; }
    }
}
