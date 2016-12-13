using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoupForTelegram.Models
{
    public enum Action
    {
        /// <summary>
        /// Take 1 coin
        /// </summary>
        Income,
        /// <summary>
        /// Take 2 coins. Can be blocked
        /// </summary>
        ForeignAid,
        /// <summary>
        /// Pay 7 coins, coup another player (they lose a card) - Unblockable
        /// </summary>
        Coup,
        /// <summary>
        /// Take 3 coins.
        /// </summary>
        Tax,
        /// <summary>
        /// Pay 3 coins. Assassinate another player
        /// </summary>
        Assassinate,
        /// <summary>
        /// Exchange cards with deck
        /// </summary>
        Exchange,
        /// <summary>
        /// Steal 2 coins from another player
        /// </summary>
        Steal,
        /// <summary>
        /// Block stealing
        /// </summary>
        BlockSteal,
        /// <summary>
        /// Block asssassination
        /// </summary>
        BlockAssassinate,
        /// <summary>
        /// Block foreign aid
        /// </summary>
        BlockForeignAid,
        /// <summary>
        /// No choice made
        /// </summary>
        None
    }
}
